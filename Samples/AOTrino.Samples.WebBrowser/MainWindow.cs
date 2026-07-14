namespace AOTrino.Samples.WebBrowser;

// a minimal web browser. NavigationMode.Web lets the WebView navigate its top-level document to any site.
// a single WebView can't wrap native chrome around its own web content, so the browser bar (back / forward /
// reload / address / go / close) is injected into EVERY document from an embedded script (Scripts\BrowserChrome.js) —
// which also shows off AddStartupScriptResource. no web security is disabled: this stays the honest "browser" choice.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
        NavigationMode = NavigationMode.Web;
    }

    protected override void ControllerCreated()
    {
        // register the browser bar before the base navigates, so it runs on the very first document too
        AddStartupScriptResource(typeof(MainWindow).Assembly, "BrowserChrome.js");
        base.ControllerCreated();
    }

    // keyboard shortcuts alongside the injected bar's buttons (WebView2 also honors some of these natively)
    protected override void OnKeyDown(object? sender, KeyEventArgs e)
    {
        base.OnKeyDown(sender, e);
        if (e.Handled)
            return;

        switch (e.Key)
        {
            case VIRTUAL_KEY.VK_LEFT when e.WithMenu:
                WebView?.Object.GoBack();
                e.Handled = true;
                break;

            case VIRTUAL_KEY.VK_RIGHT when e.WithMenu:
                WebView?.Object.GoForward();
                e.Handled = true;
                break;

            case VIRTUAL_KEY.VK_F5:
                WebView?.Object.Reload();
                e.Handled = true;
                break;
        }
    }
}
