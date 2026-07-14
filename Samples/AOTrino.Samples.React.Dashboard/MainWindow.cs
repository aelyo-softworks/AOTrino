namespace AOTrino.Samples.React.Dashboard;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    // serve the bundler-built WebRoot over https rather than file://, so its ES modules load without any
    // browser flag. see docs/SECURITY.md.
    protected override string? VirtualHostName => "aotrino.example";

    protected override void RegisterHostObjects() => AddHostObject("dotnet", new DashboardApi(this));
}
