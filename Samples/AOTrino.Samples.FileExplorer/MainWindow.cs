namespace AOTrino.Samples.FileExplorer;

// a reduced local file explorer.
// stays NavigationMode.Local (the default): browsing goes through the fs host object,
// and the "AOTrino on GitHub" link in the page, a real <a href>, is cancelled by the Local nav-lock and opened in the system browser instead.
// that contrast with the WebBrowser sample is the whole point.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    private FileSystemApi? _fs;

    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    // expose the file-system backend to JS as chrome.webview.hostObjects.fs.
    protected override void RegisterHostObjects()
    {
        _fs = new FileSystemApi(this);
        AddHostObject("fs", _fs);

        // AOTrino builds SystemInfo but never registers it:
        // any page a window navigates to can call every host object on that window, so exposing it is a per-window decision.
        // safe here as this window is Local and only ever shows content from WebRoot.
        // a window with NavigationMode.Web must not do this.
        var info = new SystemInfo(this);

        // ...and the values are just a JSON DOM until you hand them over: drop what this app has no business reporting, add what it does.
        // neither needs AOTrino's permission.
        // just remove something you don't want to report, or add something you do. the page can read the values but can't change them:
        //      info.Values.Remove("adapters");
        info.Values["FileExplorer sample specific"] = new JsonObject
        {
            ["startedAt"] = DateTime.Now.ToString("HH:mm:ss"),
            ["elevation"] = SystemUtilities.GetTokenElevationType().ToString()
        };
        AddHostObject("system", info);
    }

    // Explorer may drop files here. what arrives is the real paths, which is the difference that matters:
    // an HTML5 drop in the page would give File objects, the bytes and the names, and no way to know where
    // any of them came from or to reopen them later.
    protected override bool AcceptsFileDrops => true;

    // This PC is a list of drives, not a folder, so a drop there is refused and the cursor shows it
    // while the file is still in the air, rather than after it lands.
    protected override DROPEFFECT GetFileDropEffect(DROPEFFECT allowedEffects) => _fs?.CurrentFolder != null ? base.GetFileDropEffect(allowedEffects) : DROPEFFECT.DROPEFFECT_NONE;

    protected override void OnFilesDropped(FileDropEventArgs e)
    {
        base.OnFilesDropped(e);
        if (_fs == null)
            return;

        // a file dropped on a file manager is a copy into the folder on screen, so that is what happens,
        // and the page is told where the copies landed so it can show them rather than guess.
        var added = _fs.CopyInto(e.Paths, out var message);

        var paths = JsonSerializer.Serialize(added, FileExplorerJsonContext.Default.IReadOnlyListString);
        var folder = JsonSerializer.Serialize(_fs.CurrentFolder ?? string.Empty, FileExplorerJsonContext.Default.String);
        var note = JsonSerializer.Serialize(message, FileExplorerJsonContext.Default.String);
        ExecuteScript($"window.onFilesDropped && window.onFilesDropped({paths}, {folder}, {note});");
    }

    // the preview pane loads local files (file://) in an <iframe>; allow a file:// page to reach other local files.
    // safe here: the window stays Local (it never loads remote content) and this is the developer's explicit environment choice,
    // exactly where docs/SECURITY.md says the trust decision belongs.
    protected override CoreWebView2EnvironmentOptions? GetEnvironmentOptions()
    {
        var options = new CoreWebView2EnvironmentOptions();
        options.put_AdditionalBrowserArguments(PWSTR.From("--allow-file-access-from-files"));
        return options;
    }
}
