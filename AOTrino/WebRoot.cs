namespace AOTrino;

// extracts the embedded front-end (resources whose logical name starts with "WebRoot\") from an
// assembly to a versioned folder on disk, so the WebView can navigate to it as a local file.
// instance (not static) so two apps in one process don't share extraction state — owned by AOTrinoApplication.
public class WebRoot
{
    private const string _prefix = @"WebRoot\";
    private const string _indexFileName = "index.html";
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
    public string IndexFilePath => Path.Combine(_paths.WebRootDistPath, _indexFileName);

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

        // the cache key is a hash of the embedded content itself, not the folder/version name: an incremental
        // build that re-embeds a changed WebRoot (same assembly version) still produces a new signature, so the
        // edit is always re-extracted. keying on the version folder alone served a stale copy without a full rebuild.
        var signature = ComputeSignature(names);
        var signaturePath = Path.Combine(_paths.WebRootPath, _signatureFileName);
        if (!forceRefresh && File.Exists(signaturePath) && File.ReadAllText(signaturePath) == signature)
        {
            _done = true;
            _task = null;
            return;
        }

        // wipe every extracted version, then extract the current one
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
        File.WriteAllText(signaturePath, signature);

        _done = true;
        _task = null;
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
