namespace AOTrino.Samples.Blazor.DiskMap;

using System.Buffers.Binary;

// reads a whole NTFS drive by parsing its Master File Table, rather than walking directories.
//
// this is the Windows-native path, and the difference is one of kind rather than degree.
// a directory walk asks the file system about one folder at a time and pays for the tree it climbs,
// while the MFT is a flat array of records, one per file, that says who each file's parent is.
// reading it is a sequential pass over a few hundred megabytes, so a drive that takes minutes to walk takes seconds.
//
// the cost is that it needs administrator rights and only exists on NTFS,
// which is why DiskScanner keeps its directory walk as the path everyone can always use.
//
// only four things are taken from each record, which is what keeps this a few hundred lines rather than a forensics tool:
// the name, the parent, the size and whether it is a directory.
internal sealed class MftReader(string root)
{
    // NTFS reserves the first 16 records for its own metadata, and record 5 is the root directory.
    private const long _rootRecordNumber = 5;

    private const uint _attributeFileName = 0x30;
    private const uint _attributeData = 0x80;
    private const uint _attributeEnd = 0xFFFFFFFF;

    private const ushort _flagInUse = 0x0001;
    private const ushort _flagDirectory = 0x0002;

    // the DOS 8.3 name of a file that also has a long one, skipped so a file is not counted under two names.
    private const byte _nameTypeDos = 2;

    // one MFT record, reduced to what a disk usage map needs.
    private struct Record
    {
        public long ParentRecordNumber;
        public long Size;
        public string? Name;
        public bool IsDirectory;
        public bool InUse;
    }

    public long FileCount { get; private set; }
    public long DirectoryCount { get; private set; }
    public long BytesRead { get; private set; }
    public long RecordCount { get; private set; }

    // reads every record, then rebuilds the tree from the parent of each one.
    // returns null when the volume cannot be opened, which the caller treats as "use the directory walk instead".
    public ScanNode? Read(Action<long, long>? onProgress, Action<string>? onPhase, CancellationToken cancellationToken)
    {
        using var volume = NtfsVolume.OpenVolume(root, out var data);
        if (volume == null)
            return null;

        var recordSize = (int)data.BytesPerFileRecordSegment;
        if (recordSize <= 0 || data.MftValidDataLength <= 0)
            return null;

        var total = data.MftValidDataLength / recordSize;
        var records = new Record[total];

        // where the table actually lives. the MFT is a file like any other, so it is fragmented like any other,
        // and its own record says where its pieces are.
        // reading straight on from MftStartLcn instead, as if it were contiguous, reads whatever else is on the disk
        // once the first fragment ends. those bytes fail the FILE check and get skipped, so the scan silently
        // returns the fraction of the drive that happened to be in the first extent.
        var extents = ReadMftExtents(volume, data, recordSize);
        if (extents == null || extents.Count == 0)
            return null;

        // the MFT is read in large blocks. one read per record would be as slow as the directory walk it replaces.
        const int recordsPerBlock = 1024;
        var block = new byte[recordSize * recordsPerBlock];
        long index = 0;

        foreach (var (offset, length) in extents)
        {
            var remaining = length;
            var position = offset;

            while (remaining >= recordSize && index < total)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var wanted = (int)Math.Min(Math.Min(block.Length, remaining), (total - index) * recordSize);
                volume.Seek(position, SeekOrigin.Begin);
                var got = ReadFully(volume, block, wanted);
                if (got < recordSize)
                    break;

                var parsed = got / recordSize;
                for (var i = 0; i < parsed && index < total; i++, index++)
                {
                    ParseRecord(block.AsSpan(i * recordSize, recordSize), ref records[index]);
                }

                position += (long)parsed * recordSize;
                remaining -= (long)parsed * recordSize;

                RecordCount = index;
                onProgress?.Invoke(index, total);
            }
        }

        // building the tree from the records takes a noticeable moment on a drive with millions of them,
        // and it reports nothing while it runs. without this the last thing the page was told is the final record,
        // so it sits on "record N of N" looking like it has stopped.
        onPhase?.Invoke($"building the tree from {index:N0} records");
        return BuildTree(records);
    }

    // record 0 of the MFT is $MFT itself, and its unnamed $DATA attribute holds the run list,
    // the list of where on the volume the table's own pieces are.
    // that list is what turns "the MFT starts at this cluster" into "the MFT is these extents".
    private static List<(long Offset, long Length)>? ReadMftExtents(FileStream volume, NtfsVolume.NtfsVolumeData data, int recordSize)
    {
        var record = new byte[recordSize];
        volume.Seek(data.MftStartLcn * data.BytesPerCluster, SeekOrigin.Begin);
        if (ReadFully(volume, record, recordSize) < recordSize)
            return null;

        var span = record.AsSpan();
        if (span[0] != (byte)'F' || span[1] != (byte)'I' || span[2] != (byte)'L' || span[3] != (byte)'E' || !ApplyFixups(span))
            return null;

        int offset = BinaryPrimitives.ReadUInt16LittleEndian(span[0x14..]);
        while (offset + 8 <= span.Length)
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);
            if (type == _attributeEnd)
                break;

            var length = BinaryPrimitives.ReadUInt32LittleEndian(span[(offset + 4)..]);
            if (length == 0 || offset + length > span.Length)
                break;

            var attribute = span.Slice(offset, (int)length);

            // the unnamed, non-resident $DATA. $MFT is far too large to be resident, so anything else here is wrong.
            if (type == _attributeData && attribute[8] != 0 && attribute[9] == 0)
                return ParseRuns(attribute, data);

            offset += (int)length;
        }

        return null;
    }

    // a run list is a sequence of (length, offset) pairs, each preceded by a byte saying how many bytes each of the two takes.
    // the offset is a delta from the previous run's start, and it is signed, so the table can go backwards on the disk.
    private static List<(long Offset, long Length)> ParseRuns(Span<byte> attribute, NtfsVolume.NtfsVolumeData data)
    {
        var extents = new List<(long, long)>();
        var runsOffset = BinaryPrimitives.ReadUInt16LittleEndian(attribute[0x20..]);
        var lcn = 0L;

        for (int i = runsOffset; i < attribute.Length && attribute[i] != 0;)
        {
            var header = attribute[i++];
            int lengthSize = header & 0x0F;
            int offsetSize = (header >> 4) & 0x0F;
            if (lengthSize == 0 || i + lengthSize + offsetSize > attribute.Length)
                break;

            var runLength = ReadVariable(attribute.Slice(i, lengthSize), signed: false);
            i += lengthSize;

            // no offset means a sparse run, which has no place on disk. $MFT has none, but skipping is the safe answer.
            if (offsetSize == 0)
                continue;

            lcn += ReadVariable(attribute.Slice(i, offsetSize), signed: true);
            i += offsetSize;

            extents.Add((lcn * data.BytesPerCluster, runLength * data.BytesPerCluster));
        }

        return extents;
    }

    // little endian, of whatever width the run list said, and sign extended when it is an offset.
    private static long ReadVariable(Span<byte> bytes, bool signed)
    {
        long value = 0;
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            value = (value << 8) | bytes[i];
        }

        if (signed && bytes.Length > 0 && (bytes[^1] & 0x80) != 0)
        {
            value -= 1L << (bytes.Length * 8);
        }

        return value;
    }

    private static int ReadFully(FileStream volume, byte[] buffer, int count)
    {
        var read = 0;
        while (read < count)
        {
            var n = volume.Read(buffer, read, count - read);
            if (n <= 0)
                break;

            read += n;
        }
        return read;
    }

    private void ParseRecord(Span<byte> record, ref Record result)
    {
        // "FILE". anything else is an unused or damaged record, and there are plenty of both.
        if (record.Length < 48 || record[0] != (byte)'F' || record[1] != (byte)'I' || record[2] != (byte)'L' || record[3] != (byte)'E')
            return;

        if (!ApplyFixups(record))
            return;

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(record[0x16..]);
        result.InUse = (flags & _flagInUse) != 0;
        result.IsDirectory = (flags & _flagDirectory) != 0;
        if (!result.InUse)
            return;

        result.ParentRecordNumber = -1;
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(record[0x14..]);
        var counted = false;

        while (offset + 8 <= record.Length)
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(record[offset..]);
            if (type == _attributeEnd)
                break;

            var length = BinaryPrimitives.ReadUInt32LittleEndian(record[(offset + 4)..]);
            if (length == 0 || offset + length > record.Length)
                break;

            var attribute = record.Slice(offset, (int)length);
            var nonResident = attribute[8] != 0;

            if (type == _attributeFileName && !nonResident)
            {
                ReadFileName(attribute, ref result);
            }
            else if (type == _attributeData && result.Size == 0)
            {
                // the unnamed $DATA attribute is the file's content, a named one is an alternate stream.
                // a named stream is left out, since Explorer does not count it either.
                if (attribute[9] == 0)
                {
                    result.Size = nonResident
                        ? BinaryPrimitives.ReadInt64LittleEndian(attribute[0x30..])   // real size, not allocated
                        : BinaryPrimitives.ReadUInt32LittleEndian(attribute[0x10..]);
                }
            }

            offset += (int)length;
        }

        // running totals for the progress the page polls. BuildTree recounts these exactly once the tree exists,
        // this is only so a scan that takes seconds does not look like a scan that is doing nothing.
        if (!counted && result.Name != null)
        {
            counted = true;
            if (result.IsDirectory)
            {
                DirectoryCount++;
            }
            else
            {
                FileCount++;
                BytesRead += result.Size;
            }
        }
    }

    private static void ReadFileName(Span<byte> attribute, ref Record result)
    {
        var valueOffset = BinaryPrimitives.ReadUInt16LittleEndian(attribute[0x14..]);
        if (valueOffset + 0x42 > attribute.Length)
            return;

        var value = attribute[valueOffset..];

        // the parent is a file reference, 48 bits of record number and 16 of sequence. only the record number matters here.
        var parent = BinaryPrimitives.ReadInt64LittleEndian(value) & 0x0000FFFFFFFFFFFF;
        var nameLength = value[0x40];
        var nameType = value[0x41];

        // a file with both a long name and its 8.3 alias has two of these attributes.
        // taking the DOS one as well would count the file twice, so the long name wins.
        if (nameType == _nameTypeDos && result.Name != null)
            return;

        var nameBytes = value.Slice(0x42, nameLength * 2);
        result.Name = System.Text.Encoding.Unicode.GetString(nameBytes);
        result.ParentRecordNumber = parent;
    }

    // every record is protected by an update sequence array: the last two bytes of each sector hold a check value,
    // and the real bytes live in the array at the top of the record. putting them back is what makes the record readable.
    // a record whose check values do not match is torn, and is skipped rather than trusted.
    private static bool ApplyFixups(Span<byte> record)
    {
        var arrayOffset = BinaryPrimitives.ReadUInt16LittleEndian(record[4..]);
        var arrayCount = BinaryPrimitives.ReadUInt16LittleEndian(record[6..]);
        if (arrayCount == 0 || arrayOffset + arrayCount * 2 > record.Length)
            return false;

        var check = BinaryPrimitives.ReadUInt16LittleEndian(record[arrayOffset..]);
        for (var i = 1; i < arrayCount; i++)
        {
            var end = i * 512 - 2;
            if (end + 2 > record.Length)
                return false;

            if (BinaryPrimitives.ReadUInt16LittleEndian(record[end..]) != check)
                return false;

            var replacement = BinaryPrimitives.ReadUInt16LittleEndian(record[(arrayOffset + i * 2)..]);
            BinaryPrimitives.WriteUInt16LittleEndian(record[end..], replacement);
        }

        return true;
    }

    // the records name their parents, so the tree is built by walking that list once and hanging each entry off its parent.
    private ScanNode? BuildTree(Record[] records)
    {
        // the counts so far were the running ones from the read, the tree is what makes them exact:
        // a record whose parent is missing belongs to no folder, and should be in no total either.
        FileCount = 0;
        DirectoryCount = 0;
        BytesRead = 0;

        var nodes = new ScanNode?[records.Length];

        // directories first, so that a file always finds a parent waiting for it.
        for (long i = 0; i < records.Length; i++)
        {
            ref var record = ref records[i];
            if (!record.InUse || record.Name == null || !record.IsDirectory)
                continue;

            nodes[i] = new ScanNode(record.Name, record.Name, null);
        }

        var rootNode = nodes[_rootRecordNumber] ?? new ScanNode(root, root, null);
        nodes[_rootRecordNumber] = rootNode;
        rootNode.FullPath = root;

        for (long i = 0; i < records.Length; i++)
        {
            ref var record = ref records[i];
            if (!record.InUse || record.Name == null || i == _rootRecordNumber)
                continue;

            var parentNumber = record.ParentRecordNumber;
            if (parentNumber < 0 || parentNumber >= nodes.Length)
                continue;

            var parent = nodes[parentNumber];
            if (parent == null)
                continue;

            if (record.IsDirectory)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                parent.Children.Add(node);
                node.Parent = parent;
                DirectoryCount++;
            }
            else
            {
                parent.OwnSize += record.Size;
                parent.FileCount++;
                BytesRead += record.Size;
                FileCount++;
            }
        }

        // the names are stored per record, so a full path only exists once the tree does.
        Resolve(rootNode, root);
        Total(rootNode);
        return rootNode;
    }

    private static void Resolve(ScanNode node, string path)
    {
        node.FullPath = path;
        foreach (var child in node.Children)
        {
            Resolve(child, Path.Combine(path, child.Name));
        }
    }

    private static long Total(ScanNode node)
    {
        var total = node.OwnSize;
        foreach (var child in node.Children)
        {
            total += Total(child);
        }

        node.TotalSize = total;
        return total;
    }
}
