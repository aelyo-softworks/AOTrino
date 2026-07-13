namespace AOTrino.Samples.HelloWorld;

public partial class MainWindow : WebViewWindow
{
    public MainWindow()
        : base("AOTrino — Hello World")
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
