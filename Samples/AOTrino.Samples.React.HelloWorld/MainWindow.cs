namespace AOTrino.Samples.React.HelloWorld;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    // Vite emits the app as an ES module, and a file:// page has an opaque origin, so Chromium CORS-blocks the module and the page renders blank.
    // serving the WebRoot from a virtual host gives it an ordinary https origin, so the module loads.
    // this is all the sample needs: it depends on no browser flag and no environment setting,
    // and it behaves the same whether or not --allow-file-access-from-files happens to be on (that flag only relaxes file:// origins, and this page has none).
    // .example is reserved by RFC 2606, so the name can never collide with a real domain.
    protected override string? VirtualHostName => "aotrino.example";

    // the page reaches this as chrome.webview.hostObjects.dotnet,
    // typed on the TS side by the DemoApi interface in WebRoot/src/api.ts (see @aotrino/client's host<T>()).
    protected override void RegisterHostObjects() => AddHostObject("dotnet", new DemoApi(this));
}
