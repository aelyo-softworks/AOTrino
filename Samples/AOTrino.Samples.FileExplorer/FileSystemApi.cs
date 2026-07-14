namespace AOTrino.Samples.FileExplorer;

// a JS-callable host object exposed as chrome.webview.hostObjects.fs — a tiny, read-only local file browser
// backend (a heavily reduced ShellBat). the window stays NavigationMode.Local, so browsing happens through
// these host calls, not by navigating the WebView. complex results cross the bridge as JSON strings.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class FileSystemApi : DispatchObject
{
    // bridge invokes members on the instance, so they stay instance members even without instance state
#pragma warning disable CA1822 // Mark members as static

    // the folder the browser opens on
    public string GetHome() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // list a folder (empty/null path lists the drive roots). returns DirListing as JSON.
    public string List(string? path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
                return ListDrives();

            var full = Path.GetFullPath(path);
            var dir = new DirectoryInfo(full);

            var dirs = dir.EnumerateDirectories()
                .Where(IsVisible)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => new DirEntry(d.Name, d.FullName, true, 0, Stamp(d.LastWriteTime)));

            var files = dir.EnumerateFiles()
                .Where(IsVisible)
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new DirEntry(f.Name, f.FullName, false, f.Length, Stamp(f.LastWriteTime)));

            // empty parent = back to the drives root; the JS "Up" button navigates there
            var parent = Directory.GetParent(full)?.FullName ?? string.Empty;
            return Serialize(new DirListing(full, parent, null, [.. dirs, .. files]));
        }
        catch (Exception ex)
        {
            return Serialize(new DirListing(path ?? string.Empty, string.Empty, ex.Message, []));
        }
    }

    // open a file with its default app (folders are browsed in-app via List, not opened here)
    public bool Open(string path)
    {
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        return true;
    }
#pragma warning restore CA1822

    private static string ListDrives()
    {
        var entries = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new DirEntry(d.Name, d.Name, true, 0, d.DriveType.ToString()))
            .ToList();
        return Serialize(new DirListing(string.Empty, null, null, entries));
    }

    private static bool IsVisible(FileSystemInfo info) =>
        (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;

    private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm");

    private static string Serialize(DirListing listing) => JsonSerializer.Serialize(listing, FileExplorerJsonContext.Default.DirListing);
}
