namespace AOTrino.Samples.HelloWorld;

[GeneratedComClass]
public partial class MainWindow : WebViewWindow
{
    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    protected override void ControllerCreated()
    {
        base.ControllerCreated();
        _ = NavigateToWebRootAsync();
    }

    private async Task NavigateToWebRootAsync()
    {
        // extraction runs on a worker thread; the continuation resumes on the window's
        // synchronization context, so Navigate is called back on the UI thread
        await WebRoot.EnsureFilesAsync();
        Navigate(WebRoot.IndexFilePath);
    }
}
