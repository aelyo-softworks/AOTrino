namespace AOTrino.Samples.Blazor.DiskMap;

using AOTrino.Samples.Blazor.DiskMap.Shared;

// walks a directory tree and totals what is in it.
// this is the half of the app the page could not do on its own: a browser has no arbitrary file system,
// and a wasm runtime walking millions of entries would be slow even if it had one.
//
// two things make it fast enough to point at a whole drive:
// * the sizes come from the directory enumeration itself. Windows already returns the length with each entry,
//   so asking a FileInfo for its Length afterwards costs a second call per file and roughly doubles the scan.
// * the top level fans out across the thread pool. a drive's first level is where the big independent subtrees are,
//   and they are pure IO, so they overlap well.
public sealed class DiskScanner
{
    private readonly Lock _lock = new();
    private CancellationTokenSource? _cancellation;
    private ScanNode? _root;
    private long _bytes;
    private int _files;
    private int _directories;
    private int _errors;
    private string? _currentPath;
    private bool _running;
    private bool _complete;
    private string? _error;
    private long _estimatedTotal;
    private long _recordsRead;
    private long _recordsTotal;
    private long _startedAt;
    private long _finishedAt;
    private bool _usedMft;
    private string? _method;

    // enumerate without following links. a junction pointing at an ancestor turns the walk into a loop,
    // and one pointing at another volume silently counts a second drive into this one's total.
    private static readonly EnumerationOptions _options = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        RecurseSubdirectories = false,
    };

    public ScanNode? Root
    {
        get { lock (_lock) return _root; }
    }

    public ScanProgress GetProgress()
    {
        lock (_lock)
        {
            var elapsed = _running
                ? Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds
                : Stopwatch.GetElapsedTime(_startedAt, _finishedAt).TotalMilliseconds;

            return new ScanProgress
            {
                IsRunning = _running,
                IsComplete = _complete,
                CurrentPath = _currentPath,
                BytesScanned = Interlocked.Read(ref _bytes),
                FilesScanned = Volatile.Read(ref _files),
                DirectoriesScanned = Volatile.Read(ref _directories),
                ErrorCount = Volatile.Read(ref _errors),
                ElapsedMilliseconds = (long)elapsed,
                EstimatedTotalBytes = _estimatedTotal,
                RecordsRead = Interlocked.Read(ref _recordsRead),
                RecordsTotal = Interlocked.Read(ref _recordsTotal),
                UsedMasterFileTable = _usedMft,
                Method = _method,
                Error = _error,
            };
        }
    }

    public void Cancel() => _cancellation?.Cancel();

    // starts a scan and returns at once, the caller polls GetProgress.
    // a scan already running is cancelled first, so the page can retarget without waiting for it.
    public void Start(string path, bool preferMasterFileTable = true)
    {
        Cancel();

        var cancellation = new CancellationTokenSource();
        lock (_lock)
        {
            _cancellation = cancellation;
            _root = null;
            _bytes = 0;
            _files = 0;
            _directories = 0;
            _errors = 0;
            _currentPath = path;
            _running = true;
            _complete = false;
            _error = null;
            _estimatedTotal = EstimateTotal(path);
            _recordsRead = 0;
            _recordsTotal = 0;
            _usedMft = false;
            _method = null;
            _startedAt = Stopwatch.GetTimestamp();
        }

        // a dedicated background thread rather than a pool one, this owns the walk for as long as it takes.
        new Thread(() => Run(path, preferMasterFileTable, cancellation.Token))
        {
            IsBackground = true,
            Name = "DiskMap scan",
        }.Start();
    }

    // the only total available before the walk is done is what the drive says it is using,
    // and that is only an answer when the whole drive is what is being scanned.
    // for a subfolder it would be a denominator many times too large, so no estimate is offered rather than a wrong one,
    // which is what made a scan of C:\Windows\WinSxS sit at a fraction of a percent.
    //
    // even for a whole drive it stays best effort. WinSxS is mostly hard links, so the same bytes are counted
    // once per name and the total can overshoot what the drive reports, and compressed or sparse files go the other way.
    private static long EstimateTotal(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root) || !full.TrimEnd(Path.DirectorySeparatorChar).EqualsIgnoreCase(root.TrimEnd(Path.DirectorySeparatorChar)))
                return 0;

            var drive = new DriveInfo(root);
            return drive.IsReady ? drive.TotalSize - drive.AvailableFreeSpace : 0;
        }
        catch
        {
            return 0;
        }
    }

    private void Run(string path, bool preferMasterFileTable, CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"'{path}' is not a directory that exists.");

            // the fast path first. reading the master file table is a sequential pass over a flat array of records,
            // where a directory walk pays for every folder it opens, so a whole drive goes from minutes to seconds.
            // it is not always available, and everything about why is reported to the page rather than hidden.
            if (preferMasterFileTable && NtfsVolume.CanRead(path, out var reason))
            {
                var reader = new MftReader(path);
                var mftRoot = reader.Read((read, total) =>
                {
                    Interlocked.Exchange(ref _bytes, reader.BytesRead);
                    Volatile.Write(ref _files, (int)reader.FileCount);
                    Volatile.Write(ref _directories, (int)reader.DirectoryCount);
                    Interlocked.Exchange(ref _recordsRead, read);
                    Interlocked.Exchange(ref _recordsTotal, total);
                    lock (_lock)
                    {
                        _currentPath = $"record {read:N0} of {total:N0}";
                    }
                },
                phase =>
                {
                    lock (_lock)
                    {
                        _currentPath = phase;
                    }
                }, cancellationToken);

                if (mftRoot != null)
                {
                    lock (_lock)
                    {
                        _root = mftRoot;
                        _complete = !cancellationToken.IsCancellationRequested;
                        _usedMft = true;
                        _method = "master file table";
                        _bytes = mftRoot.TotalSize;
                        _files = (int)reader.FileCount;
                        _directories = (int)reader.DirectoryCount;
                    }
                    return;
                }

                // opening the volume failed even though it should have worked, so the walk runs instead.
            }

            lock (_lock)
            {
                _usedMft = false;
                _method = "directory walk";
            }

            var root = new ScanNode(path, path, null);

            // the root's own files first, then its subtrees in parallel.
            ScanFiles(root);
            var children = ReadDirectories(root);

            // published before the walk rather than after it, so the page can show the top level filling in.
            // the root's children are all known by now, and nothing adds to that list again,
            // so reading it while the subtrees below are still being walked is safe.
            lock (_lock)
            {
                _root = root;
            }

            Parallel.ForEach(children, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount }, child => Walk(child, cancellationToken));
            Total(root);

            lock (_lock)
            {
                _root = root;
                _complete = !cancellationToken.IsCancellationRequested;
            }
        }
        catch (OperationCanceledException)
        {
            // cancelling is a normal end, not a failure to report.
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _error = ex.Message;
            }
        }
        finally
        {
            lock (_lock)
            {
                _running = false;
                _currentPath = null;
                _finishedAt = Stopwatch.GetTimestamp();
            }
        }
    }

    // iterative rather than recursive, a deep tree would otherwise be a stack overflow waiting for the wrong drive.
    private void Walk(ScanNode start, CancellationToken cancellationToken)
    {
        var stack = new Stack<ScanNode>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var node = stack.Pop();
            lock (_lock)
            {
                _currentPath = node.FullPath;
            }

            ScanFiles(node);

            // every ancestor gets this directory's bytes as they are found, so any folder in the tree carries
            // a running total rather than staying at zero until the walk finishes.
            // the final roll up replaces all of them with exact figures, this is what makes the map fill in live.
            for (var ancestor = node; ancestor != null; ancestor = ancestor.Parent)
            {
                ancestor.AddToTotal(node.OwnSize);
            }

            foreach (var child in ReadDirectories(node))
            {
                stack.Push(child);
            }
        }
    }

    // the length comes from the enumeration record Windows already returned, so this is one call per directory,
    // not one per directory plus one per file.
    private void ScanFiles(ScanNode node)
    {
        try
        {
            long own = 0;
            var count = 0;
            foreach (var file in new DirectoryInfo(node.FullPath).EnumerateFiles("*", _options))
            {
                own += file.Length;
                count++;
            }

            node.OwnSize = own;
            node.FileCount = count;
            Interlocked.Add(ref _bytes, own);
            Interlocked.Add(ref _files, count);
        }
        catch
        {
            // an unreadable directory is counted and skipped, System Volume Information is the usual one.
            Interlocked.Increment(ref _errors);
        }
    }

    private List<ScanNode> ReadDirectories(ScanNode node)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(node.FullPath, "*", _options))
            {
                node.Children.Add(new ScanNode(Path.GetFileName(directory), directory, node));
            }

            Interlocked.Add(ref _directories, node.Children.Count);
            return node.Children;
        }
        catch
        {
            Interlocked.Increment(ref _errors);
            return [];
        }
    }

    // totals roll up once the walk is done, a node's total is not known until its children are.
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
