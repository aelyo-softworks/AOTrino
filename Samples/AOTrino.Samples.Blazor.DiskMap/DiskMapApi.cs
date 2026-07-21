namespace AOTrino.Samples.Blazor.DiskMap;

using AOTrino.Samples.Blazor.DiskMap.Shared;

// what the Blazor page can call. every member here is something a browser cannot do:
// walking an arbitrary directory tree, reading real drives, opening a folder in Explorer.
// the page is C# too, but it runs in wasm inside the WebView, so it reaches this through the bridge like any other front end.
//
// results cross as JSON strings rather than as objects: one call for a whole level beats a bridge round trip per node,
// and System.Text.Json's source generator keeps it AOT safe (the serializer is shared with the page, see the Shared project).
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class DiskMapApi(MainWindow window) : DispatchObject
{
#pragma warning disable CA1822 // Mark members as static
    [Browsable(false)]
    public MainWindow Window { get; } = window;

    // the drives the machine actually has, the page offers them as the starting points.
    public string GetDrives()
    {
        var drives = new List<DriveEntry>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                    continue;

                drives.Add(new DriveEntry
                {
                    Name = drive.Name,
                    Label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel,
                    TotalSize = drive.TotalSize,
                    FreeSpace = drive.AvailableFreeSpace,
                    Format = drive.DriveFormat,
                });
            }
            catch
            {
                // a drive can be ready and still refuse to describe itself, a locked BitLocker volume for instance.
            }
        }

        return JsonSerializer.Serialize(drives, DiskMapJsonContext.Default.ListDriveEntry);
    }

    // quick means the master file table, normal means the directory walk.
    // the page decides, because the two differ in what they can see as well as in how long they take,
    // and that is a choice worth leaving to whoever is looking at the numbers.
    public void StartScan(string path, bool quick) => Window.Scanner.Start(path, quick);
    public void CancelScan() => Window.Scanner.Cancel();

    public string GetProgress() => JsonSerializer.Serialize(Window.Scanner.GetProgress(), DiskMapJsonContext.Default.ScanProgress);

    // the children of one directory, largest first, which is what a treemap level needs.
    // an empty path means the root of the last scan.
    public string GetChildren(string path)
    {
        var root = Window.Scanner.Root;
        if (root == null)
            return "[]";

        // while a scan runs, only the top level can be read. the lists deeper down are still being added to,
        // and walking one as it grows is how a live view turns into an exception.
        var running = Window.Scanner.GetProgress().IsRunning;
        if (running && !string.IsNullOrEmpty(path))
            return "[]";

        var node = string.IsNullOrEmpty(path) ? root : root.Find(path);
        if (node == null)
            return "[]";

        // no snapshot needed, unlike the treemap, which walks the whole tree at every depth while it is still being built.
        // the guard above means a running scan only ever gets here for the root, and the root's children are all known
        // before the root is published, the walk only ever adds to the levels below them.
        var entries = node.Children
            .Select(c => new NodeEntry
            {
                Name = c.Name,
                FullPath = c.FullPath,
                TotalSize = c.TotalSize,
                FileCount = c.FileCount,
                DirectoryCount = c.Children.Count,
            })
            .Where(e => e.TotalSize > 0)
            .OrderByDescending(e => e.TotalSize)
            .ToList();

        // files sitting directly in this directory are shown as one entry, so the sizes on screen add up to the total.
        if (node.OwnSize > 0)
        {
            entries.Add(new NodeEntry
            {
                Name = node.FileCount == 1 ? "1 file in this folder" : $"{node.FileCount:N0} files in this folder",
                FullPath = string.Empty,
                TotalSize = node.OwnSize,
                FileCount = node.FileCount,
                DirectoryCount = 0,
            });
            entries = [.. entries.OrderByDescending(e => e.TotalSize)];
        }

        return JsonSerializer.Serialize(entries, DiskMapJsonContext.Default.ListNodeEntry);
    }

    // the total of a directory, so the page can show what the level it is on adds up to.
    public string GetNode(string path)
    {
        var root = Window.Scanner.Root;
        var running = Window.Scanner.GetProgress().IsRunning;
        var node = root == null ? null : string.IsNullOrEmpty(path) ? root : running ? null : root.Find(path);
        var entry = node == null
            ? new NodeEntry { Name = string.Empty, FullPath = string.Empty }
            : new NodeEntry
            {
                Name = node.Name,
                FullPath = node.FullPath,
                TotalSize = node.TotalSize,
                FileCount = node.FileCount,
                DirectoryCount = node.Children.Count,
            };

        return JsonSerializer.Serialize(entry, DiskMapJsonContext.Default.NodeEntry);
    }

    // whether the fast path is available, and what would have to change for it to be.
    // the page shows this, rather than silently taking three times as long with no explanation.
    public bool IsElevated() => NtfsVolume.IsElevated();

    public bool CanUseMasterFileTable(string path) => NtfsVolume.CanRead(path, out _);

    public string GetMasterFileTableReason(string path)
    {
        NtfsVolume.CanRead(path, out var reason);
        return reason;
    }

    // restarts elevated, which is the one thing an app cannot do to itself: Windows raises the prompt, and the user answers it.
    // the running instance closes only once the new one has actually been allowed to start.
    public bool RestartElevated()
    {
        if (!NtfsVolume.TryRestartElevated())
            return false;

        Window.Close();
        return true;
    }

    // the treemap, drawn by Direct2D straight into the canvas the page shows. see Treemap.cs.
    //
    // only the folder to show and where the pointer is cross the bridge, never the rectangles,
    // which is the point of drawing it on this side: a level of a real drive is thousands of tiles,
    // and they are recomputed every frame while a scan is still growing the tree.
    public void SetTreemapPath(string path) => Window.Treemap.Path = path ?? string.Empty;

    // normalized coordinates, so the page never has to know the render resolution.
    // the canvas is laid out in CSS pixels and rendered at device pixels, and on a scaled display those differ.
    public void SetTreemapPointer(double x, double y) => Window.Treemap.SetPointer(x, y);

    // the folder under the pointer, which is what a click on the canvas descends into.
    // an empty string means the click landed on nothing, or on the files-in-this-folder tile, which is not a place to go.
    public string TreemapHitTest(double x, double y) => Window.Treemap.HitTest(x, y) ?? string.Empty;

    // the page follows the Windows app theme through prefers-color-scheme, and the pixels drawn here
    // have to match the chrome drawn around them, so it says which one is showing.
    public void SetTreemapTheme(bool dark) => Window.Treemap.IsDark = dark;

    // the other thing a page cannot do, hand a real path to the shell.
    public void OpenInExplorer(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = path });
    }

#pragma warning restore CA1822 // Mark members as static
}
