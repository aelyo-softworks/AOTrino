namespace AOTrino.Samples.HelloWorld;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    protected override void OnNavigationCompleted(object? sender, NavigationEventArgs e)
    {
        base.OnNavigationCompleted(sender, e);
        if (!e.IsSuccess)
            return;

        // push the WebView2 runtime version and the AOTrino SDK version into the page
        var app = AOTrinoApplication.Current!;
        ExecuteScript($"window.setVersions && window.setVersions('{app.WebView2Version}', '{app.AOTrinoVersion}');");
    }
}
