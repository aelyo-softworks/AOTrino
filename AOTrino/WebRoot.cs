namespace AOTrino;

// extracts the embedded front-end (resources whose logical name starts with "WebRoot\") from an assembly to a versioned folder on disk,
// so the WebView can navigate to it as a local file.
// instance (not static) so two apps in one process don't share extraction state (owned by AOTrinoApplication).
public class WebRoot
{
    // public: a window that serves the WebRoot from a virtual host builds its own start URL from it.
    public const string IndexFileName = "index.html";

    private const string _prefix = @"WebRoot\";
    private const string _signatureFileName = ".webroot";

    private readonly Assembly _assembly;
    private readonly AOTrinoPaths _paths;
    private readonly Lock _lock = new();
    private Task? _task;
    private bool _done;

    public WebRoot(Assembly assembly, AOTrinoPaths paths)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(paths);
        _assembly = assembly;
        _paths = paths;
    }

    public string DistPath => _paths.WebRootDistPath;
    public string IndexFilePath => Path.Combine(_paths.WebRootDistPath, IndexFileName);

    public Task EnsureFilesAsync(bool forceRefresh = false)
    {
        if (_done)
            return Task.CompletedTask;

        if (_task != null)
            return _task;

        lock (_lock)
        {
            _task ??= Task.Run(() => EnsureFiles(forceRefresh));
            return _task;
        }
    }

    protected virtual void EnsureFiles(bool forceRefresh)
    {
        var names = _assembly.GetManifestResourceNames().Where(n => n.StartsWith(_prefix)).Order().ToArray();
        if (names.Length == 0)
        {
            _done = true;
            _task = null;
            return;
        }

        // the cache key is a hash of the embedded content itself,
        // not the folder/version name: an incremental build that re-embeds a changed WebRoot (same assembly version) still produces a new signature,
        // so the edit is always re-extracted. keying on the version folder alone served a stale copy without a full rebuild.
        var signature = ComputeSignature(names);
        var signaturePath = Path.Combine(_paths.WebRootPath, _signatureFileName);
        if (!forceRefresh && File.Exists(signaturePath) && File.ReadAllText(signaturePath) == signature)
        {
            _done = true;
            _task = null;
            return;
        }

        // wipe every extracted version, then extract the current one.
        var parent = Path.GetDirectoryName(_paths.WebRootPath);
        if (parent != null && Directory.Exists(parent))
        {
            try { Directory.Delete(parent, true); } catch { /* best effort cleanup of stale versions */ }
        }

        foreach (var name in names)
        {
            using var stream = _assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException();
            var path = Path.Combine(_paths.WebRootPath, name[_prefix.Length..]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fs);
        }

        Directory.CreateDirectory(_paths.WebRootPath);
        EnsureFavicon();
        File.WriteAllText(signaturePath, signature);

        _done = true;
        _task = null;
    }

    // Chromium asks every document it loads for /favicon.ico, whether or not the document mentions one,
    // so a front end with no icon produces "Failed to load resource: net::ERR_FILE_NOT_FOUND ... /favicon.ico"
    // in the console of every window, for a file nobody asked for and no page can avoid being asked for.
    //
    // answering it is the only thing that silences it, and intercepting the request does not work here:
    // a WebRoot served through SetVirtualHostNameToFolderMapping is served inside WebView2 and never raises
    // WebResourceRequested, so there is nothing to answer. a file on disk is served either way, over file:// too.
    //
    // an app that ships its own favicon.ico keeps it, this only fills the gap, and the default is fully transparent
    // because an app's icon is the app's to choose.
    protected virtual void EnsureFavicon()
    {
        try
        {
            var path = Path.Combine(_paths.WebRootDistPath, "favicon.ico");
            if (File.Exists(path))
                return;

            using var stream = typeof(WebRoot).Assembly.GetManifestResourceStream("favicon.ico");
            if (stream == null)
                return;

            Directory.CreateDirectory(_paths.WebRootDistPath);
            using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
            stream.CopyTo(file);
        }
        catch (Exception ex)
        {
            // the front end works without it, the console message comes back and that is all.
            AOTrinoApplication.Current?.TraceWarning($"The default favicon could not be written: {ex.Message}");
        }
    }

    private string ComputeSignature(IEnumerable<string> names)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var buffer = new byte[81920];
        foreach (var name in names)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(name + "\n"));
            using var stream = _assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException();
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0, read));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
