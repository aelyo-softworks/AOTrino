namespace AOTrino.Samples.Blazor.DiskMap;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    private Direct2DSurface? _surface;

    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
        Treemap = new Treemap(Scanner);

        // Acrylic, the more translucent of the Windows 11 system backdrops.
        // it only shows where the page leaves its own background transparent, which the Blazor app does behind its chrome.
        // a browser cannot ask for this, and neither can a WebView hosted as an opaque child window,
        // it works here because the WebView is a composition visual.
        SetSystemBackdrop(DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW);
    }

    // the scan and the map both hang off the window rather than off the host object, because the Direct2D surface
    // has to be created before the host objects are registered, and both sides read the same tree.
    public DiskScanner Scanner { get; } = new();

    public Treemap Treemap { get; }

    // the page reaches this as chrome.webview.hostObjects.diskmap, and through JS interop from Blazor,
    // see wwwroot\diskmap.js in the wasm project for the shim that turns it into a plain async C# call.
    protected override void RegisterHostObjects() => AddHostObject("diskmap", new DiskMapApi(this));

    protected override void ControllerCreated()
    {
        // the surface has to exist before base navigates, its document-created scripts, the __aotrino runtime
        // and the WebGL display runtime, must be registered before the page loads.
        //
        // the canvas itself is rendered by Blazor, which happens long after DOMContentLoaded,
        // so the display runtime's auto-attach has already run and found nothing.
        // the page attaches it explicitly on its first render instead, see attachTreemap in wwwroot\diskmap.js.
        _surface = new Direct2DSurface(this, "treemap")
        {
            // 30 fps rather than 60. none of this is an animation, the redraws are for the tree growing during a scan
            // and for the tile under the pointer, and neither needs more.
            FrameIntervalMs = 33,
        };
        _surface.StartAnimation(Treemap.Draw);

        base.ControllerCreated();
    }

    // Blazor WebAssembly fetches its runtime and its assemblies from _framework, and a file:// page has an opaque origin,
    // so those requests are CORS-blocked and the app never starts, it just shows its loading text forever.
    // serving the WebRoot from a virtual host gives the page an ordinary https origin, which is all Blazor needs here.
    // .example is reserved by RFC 2606, so the name can never collide with a real domain.
    //
    // the window opens the default start url, which ends in index.html, because the virtual host serves files and has no directory index,
    // so a request for https://diskmap.example/ is answered with ERR_ACCESS_DENIED.
    // WebRoot\Pages\Index.razor carries a matching @page "/index.html" route for that reason.
    protected override string? VirtualHostName => "diskmap.example";

    protected override void Dispose(bool disposing)
    {
        Interlocked.Exchange(ref _surface, null)?.Dispose();
        Treemap.Dispose();
        base.Dispose(disposing);
    }
}
