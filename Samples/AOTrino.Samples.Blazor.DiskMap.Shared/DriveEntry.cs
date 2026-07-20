namespace AOTrino.Samples.Blazor.DiskMap.Shared;

// a drive the scan can be pointed at.
public sealed class DriveEntry
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long FreeSpace { get; set; }
    public long UsedSize => TotalSize - FreeSpace;

    // NTFS, ReFS, FAT32 and so on. shown on the card, and it decides whether a quick scan is possible at all:
    // only NTFS has a master file table, so a Dev Drive, which is ReFS, can only be walked.
    public string Format { get; set; } = string.Empty;

    public bool SupportsQuickScan => Format.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
}
