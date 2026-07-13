namespace AOTrino.Samples.HostObjects;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    // expose the host API to JS as chrome.webview.hostObjects.dotnet
    protected override void RegisterHostObjects() => AddHostObject("dotnet", new HostApi(this));

    protected override void OnNavigationCompleted(object? sender, NavigationEventArgs e)
    {
        base.OnNavigationCompleted(sender, e);
        if (!e.IsSuccess)
            return;

        var app = AOTrinoApplication.Current!;
        ExecuteScript($"window.setVersions && window.setVersions('{app.WebView2Version}', '{app.AOTrinoVersion}');");
    }
}
