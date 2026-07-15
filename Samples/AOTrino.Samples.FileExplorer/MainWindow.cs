using DirectN.Extensions.Utilities;

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
    protected override void RegisterHostObjects()
    {
        AddHostObject("fs", new FileSystemApi());

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
