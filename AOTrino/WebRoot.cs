namespace AOTrino;

// extracts the embedded front-end (resources whose logical name starts with "WebRoot\")
// from the entry assembly to a versioned folder on disk, so the WebView can navigate to it
// as a local file. see the WebRoot embedding target for how these resources are produced.
public static class WebRoot
{
    private const string _prefix = @"WebRoot\";
    private const string _indexFileName = "index.html";

    private static Task? _task;
    private static bool _done;
    private static readonly Lock _lock = new();

    public static string DistPath => AOTrinoPaths.WebRootDistPath;
    public static string IndexFilePath => Path.Combine(AOTrinoPaths.WebRootDistPath, _indexFileName);

    public static Task EnsureFilesAsync(bool forceRefresh = false) => EnsureFilesAsync(Assembly.GetEntryAssembly()!, forceRefresh);
    public static Task EnsureFilesAsync(Assembly assembly, bool forceRefresh = false)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (_done)
            return Task.CompletedTask;

        if (_task != null)
            return _task;

        lock (_lock)
        {
            _task ??= Task.Run(() => EnsureFiles(assembly, forceRefresh));
            return _task;
        }
    }

    private static void EnsureFiles(Assembly assembly, bool forceRefresh)
    {
        var names = assembly.GetManifestResourceNames().Where(n => n.StartsWith(_prefix)).Order().ToArray();
        if (names.Length == 0)
        {
            _done = true;
            _task = null;
            return;
        }

        if (!forceRefresh)
        {
            // the last file (ordered) existing with content is our "extraction complete" marker
            var lastPath = Path.Combine(AOTrinoPaths.WebRootPath, names[^1][_prefix.Length..]);
            if (File.Exists(lastPath) && new FileInfo(lastPath).Length > 0)
            {
                _done = true;
                _task = null;
                return;
            }
        }

        var parent = Path.GetDirectoryName(AOTrinoPaths.WebRootPath);
        if (parent != null && Directory.Exists(parent))
        {
            try { Directory.Delete(parent, true); } catch { /* best effort cleanup of stale versions */ }
        }

        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException();
            var path = Path.Combine(AOTrinoPaths.WebRootPath, name[_prefix.Length..]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fs);
        }

        _done = true;
        _task = null;
    }
}
