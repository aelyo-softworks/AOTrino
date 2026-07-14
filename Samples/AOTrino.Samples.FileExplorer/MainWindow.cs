namespace AOTrino.Samples.FileExplorer;

// a reduced local file explorer.
// stays NavigationMode.Local (the default): browsing goes through the fs host object,
// and the "AOTrino on GitHub" link in the page, a real <a href>, is cancelled by the Local nav-lock and opened in the system browser instead.
// that contrast with the WebBrowser sample is the whole point.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    // expose the file-system backend to JS as chrome.webview.hostObjects.fs
    protected override void RegisterHostObjects() => AddHostObject("fs", new FileSystemApi());

    // the preview pane loads local files (file://) in an <iframe>; allow a file:// page to reach other local files.
    // safe here: the window stays Local (it never loads remote content) and this is the developer's
    // explicit environment choice, exactly where docs/SECURITY.md says the trust decision belongs.
    protected override CoreWebView2EnvironmentOptions? GetEnvironmentOptions()
    {
        var options = new CoreWebView2EnvironmentOptions();
        options.put_AdditionalBrowserArguments(PWSTR.From("--allow-file-access-from-files"));
        return options;
    }
}
