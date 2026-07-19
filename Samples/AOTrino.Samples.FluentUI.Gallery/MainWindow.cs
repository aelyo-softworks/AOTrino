namespace AOTrino.Samples.FluentUI.Gallery;

// the flagship sample: one window that shows what AOTrino actually does, dressed in Fluent UI.
// every other sample is a single idea in isolation;
// this is all of them side by side, each page with a live demo and the code that produced it.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    // Vite emits ES modules, and a file:// page has an opaque origin that CORS-blocks them.
    // a virtual host gives the page a real https origin: modules load, localStorage (the theme choice) survives a restart,
    // and no browser security flag is involved.
    // see docs/SECURITY.md.
    protected override string? VirtualHostName => "aotrino.example";

    protected override void RegisterHostObjects()
    {
        AddHostObject("gallery", new GalleryApi(this));

        // AOTrino builds SystemInfo but never registers it: any page a window navigates to can call every host object on that window.
        // safe here, this window is Local and only shows its own WebRoot.
        AddHostObject("system", new SystemInfo(this));
    }
}
