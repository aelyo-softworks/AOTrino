using AOTrino;

namespace AOTrinoApp1;

// the window navigates to WebRoot\dist\index.html on its own, override StartUrl to point it elsewhere,
// and RegisterHostObjects to expose .NET to the page (see docs/BRIDGE.md).
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base("AOTrinoApp1")
    {
    }
}
