namespace AOTrino.Samples.FileExplorer;

// a JS-callable host object exposed as chrome.webview.hostObjects.fs: a tiny, read-only local file browser backend.
// the window stays NavigationMode.Local, so browsing happens through these host calls, not by navigating the WebView.
// complex results cross the bridge as JSON strings.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class FileSystemApi(MainWindow window) : DispatchObject
{
    // bridge invokes members on the instance, so they stay instance members even without instance state.
#pragma warning disable CA1822 // Mark members as static.

    // the folder the browser opens on.
    public string GetHome() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // the folder the page is showing, remembered from the last listing, which is where a drop lands.
    // null while This PC is showing, which is a list of drives rather than a place a file can be copied to.
    internal string? CurrentFolder { get; private set; }

    // list a folder (empty/null path lists the drive roots). returns DirListing as JSON.
    public string List(string? path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                CurrentFolder = null;
                return ListDrives();
            }

            var full = Path.GetFullPath(path);
            CurrentFolder = full;
            var dir = new DirectoryInfo(full);

            var dirs = dir.EnumerateDirectories()
                .Where(IsVisible)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => new DirEntry(d.Name, d.FullName, true, 0, Stamp(d.LastWriteTime)));

            var files = dir.EnumerateFiles()
                .Where(IsVisible)
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new DirEntry(f.Name, f.FullName, false, f.Length, Stamp(f.LastWriteTime)));

            // empty parent = back to the drives root, the JS "Up" button navigates there.
            var parent = Directory.GetParent(full)?.FullName ?? string.Empty;
            return Serialize(new DirListing(full, parent, null, [.. dirs, .. files]));
        }
        catch (Exception ex)
        {
            return Serialize(new DirListing(path ?? string.Empty, string.Empty, ex.Message, []));
        }
    }

    // copies what was dropped into the folder on screen, which is what dropping a file onto a file manager means.
    // returns where each item landed, so the page can show them, and a line saying what happened.
    //
    // internal, not public: every public member of a host object is callable from the page, and copying files
    // is not something this page needs to be able to ask for. a drop is a gesture the window received,
    // not a request the page made.
    internal IReadOnlyList<string> CopyInto(IReadOnlyList<string> sources, out string message)
    {
        var target = CurrentFolder;
        if (string.IsNullOrEmpty(target) || !Directory.Exists(target))
        {
            message = "Nothing was copied, This PC is a list of drives rather than a folder.";
            return [];
        }

        var added = new List<string>();
        var failures = new List<string>();
        foreach (var source in sources)
        {
            try
            {
                // only real paths are handled, which is what a drop from Explorer gives for anything on a disk.
                // a virtual shell item, a mail attachment or a phone over MTP, has no path to copy from.
                var name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
                if (string.IsNullOrEmpty(name))
                {
                    failures.Add($"{source}: not a file or folder on disk.");
                    continue;
                }

                // never overwrite, and never copy a file onto itself when it is dropped on its own folder.
                var destination = UniquePath(Path.Combine(target, name));
                if (Directory.Exists(source))
                {
                    CopyDirectory(source, destination);
                }
                else
                {
                    File.Copy(source, destination);
                }

                added.Add(destination);
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(source)}: {ex.Message}");
            }
        }

        message = failures.Count > 0
            ? $"Copied {added.Count} of {sources.Count}. {string.Join(", ", failures)}"
            : added.Count == 1 ? $"Copied {Path.GetFileName(added[0])} here." : $"Copied {added.Count} items here.";

        return added;
    }

    // the name Explorer would have picked, "report.txt", then "report (2).txt", so a drop never overwrites
    // and dropping a file on the folder it already lives in works the way it does everywhere else.
    private static string UniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return path;

        var folder = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(folder, $"{name} ({i}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    // open a file with its default app (folders are browsed in-app via List, not opened here).
    public bool Open(string path)
    {
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        return true;
    }

    // starts an OLE drag carrying this file, which Explorer, the desktop and any other program that takes files will accept and copy.
    // it does not return until the drop is finished, because the drag runs its own modal loop, which is also why the page calls it while the mouse button is still down.
    public string BeginDrag(string path)
    {
        if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path)))
            return "none";

        var effect = FileDragSource.Drag(window.Handle, [path]);
        return effect switch
        {
            DROPEFFECT.DROPEFFECT_COPY => "copy",
            DROPEFFECT.DROPEFFECT_MOVE => "move",
            DROPEFFECT.DROPEFFECT_LINK => "link",
            _ => "none",
        };
    }

    // shows the item in a real Explorer window, selected.
    public bool RevealInExplorer(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // "/select," is what makes Explorer open the parent folder and highlight the item, instead of just
        // opening a folder. the app is not reimplementing Explorer here, it hands Windows a path and lets
        // Windows be Windows, which is most of what being a program on a desktop is worth.
        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{path}\"", UseShellExecute = true });
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

    private static bool IsVisible(FileSystemInfo info) => (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
    private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm");
    private static string Serialize(DirListing listing) => JsonSerializer.Serialize(listing, FileExplorerJsonContext.Default.DirListing);
}
