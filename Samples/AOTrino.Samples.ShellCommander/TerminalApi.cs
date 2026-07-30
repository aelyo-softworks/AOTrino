namespace AOTrino.Samples.ShellCommander;

// the console backend, exposed as chrome.webview.hostObjects.term.
// it runs a shell in a pseudo console (Terminal) and bridges it to xterm.js in the page:
// input and resize come in as method calls, output goes back as a script the window runs on the page.
// the shell opens in the browsed folder when that folder is a real file system path, which is the shell namespace tying into the terminal.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class TerminalApi(Action<string> runScript) : DispatchObject, IDisposable
{
    // in preference order. only the ones actually installed are shown
    private static readonly (string Key, string Name, string Executable, string Arguments)[] _shellCandidates =
    [
        ("powershell", "Windows PowerShell", "powershell.exe", "-NoLogo"),
        ("bash", "WSL Bash", "bash.exe", string.Empty),
        ("pwsh", "PowerShell 7", "pwsh.exe", "-NoLogo"),
        ("cmd", "Command Prompt", "cmd.exe", string.Empty),
    ];

    private Terminal? _session;
    private string? _shellKey;   // the running shell's key, so a directory sync uses the right cd syntax

    // the shells installed on this machine, as JSON, so the page can offer them and remember the pick.
#pragma warning disable CA1822 // Mark members as static
    public string GetShells()
    {
        var shells = new List<TerminalShell>();
        foreach (var (Key, Name, Executable, _) in _shellCandidates)
        {
            if (FindExecutable(Executable) != null)
            {
                shells.Add(new TerminalShell(Key, Name));
            }
        }

        return JsonSerializer.Serialize(shells, ShellCommanderJsonContext.Default.ListTerminalShell);
    }
#pragma warning restore CA1822 // Mark members as static

    // start the chosen shell for the folder (its parsing id), sized to the terminal. returns the working directory, or an error.
    public string Start(string folderId, int columns, int rows, string shellKey)
    {
        Stop();
        var resolved = ResolveShell(shellKey);
        if (resolved == null)
            return "error: no shell available";

        var workingDirectory = ResolveWorkingDirectory(folderId);
        try
        {
            var session = new Terminal(ClampAxis(columns, 2), ClampAxis(rows, 1));
            session.Output += block => runScript($"window.__termWrite('{Convert.ToBase64String(block)}')");
            session.Exited += () => runScript("window.__termExited()");
            session.Start(resolved.Value.Command, workingDirectory);
            _session = session;
            _shellKey = resolved.Value.Key;
            return workingDirectory ?? string.Empty;
        }
        catch (Exception ex)
        {
            return "error: " + ex.Message;
        }
    }

    public void Write(string data)
    {
        if (!string.IsNullOrEmpty(data))
        {
            _session?.Write(Encoding.UTF8.GetBytes(data));
        }
    }

    public void Resize(int columns, int rows) => _session?.Resize(ClampAxis(columns, 2), ClampAxis(rows, 1));
    ~TerminalApi() => Stop();
    public void Stop() => Interlocked.Exchange(ref _session, null)?.Dispose();
    public void Dispose() { Stop(); GC.SuppressFinalize(this); }

    // send a directory change to the running shell, so the console follows the file manager (one-way sync).
    // a folder that is not a real file system path, or a shell with no known cd syntax (bash), is ignored.
    public void ChangeDirectory(string folderId)
    {
        var session = _session;
        if (session == null)
            return;

        var path = ResolveFileSystemPath(folderId);
        if (path == null)
            return;

        var command = BuildChangeDirectoryCommand(_shellKey, path);
        if (command != null)
        {
            session.Write(Encoding.UTF8.GetBytes(command));
        }
    }

    // the shell specific cd, ending in a carriage return. bash (WSL) needs a translated path, so it is left alone.
    private static string? BuildChangeDirectoryCommand(string? shellKey, string path) => shellKey switch
    {
        "cmd" => $"cd /d \"{path}\"\r",
        "pwsh" or "powershell" => $"Set-Location -LiteralPath '{path.Replace("'", "''")}'\r",
        _ => null,
    };

    // the requested shell if it is installed, otherwise the first installed one (so there is always a shell).
    private static (string Command, string Key)? ResolveShell(string? shellKey)
    {
        foreach (var (Key, _, Executable, Arguments) in _shellCandidates)
        {
            if (!string.IsNullOrEmpty(shellKey) && !string.Equals(Key, shellKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var path = FindExecutable(Executable);
            if (path != null)
                return (Arguments.Length == 0 ? $"\"{path}\"" : $"\"{path}\" {Arguments}", Key);
        }

        // the requested shell was not found, fall back to whatever is installed.
        if (!string.IsNullOrEmpty(shellKey))
            return ResolveShell(null);

        return null;
    }

    // an executable's full path: itself if rooted and present, else the first PATH directory that holds it.
    private static string? FindExecutable(string executable)
    {
        try
        {
            if (Path.IsPathRooted(executable))
                return File.Exists(executable) ? executable : null;

            var paths = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(paths))
                return null;

            foreach (var directory in paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    var full = Path.Combine(directory, executable);
                    if (File.Exists(full))
                        return full;
                }
                catch
                {
                    // continue
                }
            }
        }
        catch
        {
            // continue
        }
        return null;
    }

    // the shell's start directory: the folder's own path when it is on disk, or the user profile for a virtual place.
    private static string? ResolveWorkingDirectory(string? folderId) => ResolveFileSystemPath(folderId) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // the folder's own file system path, or null when it is a virtual place with none.
    private static string? ResolveFileSystemPath(string? folderId)
    {
        try
        {
            if (!string.IsNullOrEmpty(folderId))
            {
                using var item = ShellItem.FromParsingName(folderId, throwOnError: false);
                var path = item?.SIGDN_FILESYSPATH;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;
            }
        }
        catch
        {
            // continue
        }

        return null;
    }

    private static short ClampAxis(int value, int min) => (short)Math.Clamp(value, min, 1000);
}
