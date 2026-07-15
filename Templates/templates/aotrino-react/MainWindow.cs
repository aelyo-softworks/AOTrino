using AOTrino;

namespace AOTrinoApp1;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base("AOTrinoApp1")
    {
    }

    // Vite emits the app as an ES module, and a file:// page has an opaque origin, so Chromium CORS-blocks the
    // module and the page renders blank. serving WebRoot from a virtual host gives it an ordinary https origin.
    // .example is reserved by RFC 2606, so the name can never collide with a real domain. see docs/SECURITY.md.
    protected override string? VirtualHostName => "aotrinoapp1.example";

    // expose .NET to the page here (see docs/BRIDGE.md):
    // protected override void RegisterHostObjects() => AddHostObject("app", new MyApi(this));
}
