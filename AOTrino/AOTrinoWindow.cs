namespace AOTrino;

// a WebViewWindow that navigates to the application's extracted WebRoot once the controller is ready.
// host apps derive from this and typically only set a title (or override StartUrl).
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class AOTrinoWindow(
    string? title = null,
    WINDOW_STYLE style = WINDOW_STYLE.WS_THICKFRAME,
    WINDOW_EX_STYLE extendedStyle = WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP,
    RECT? rect = null) : WebViewWindow(title, style, extendedStyle, rect)
{
    // navigated to once the controller is created; defaults to the app's WebRoot index.html
    protected virtual string? StartUrl => AOTrinoApplication.Current?.WebRoot.IndexFilePath;

    protected override void ControllerCreated()
    {
        base.ControllerCreated();
        EnsureSharedRuntime(); // window.__aotrino (window controls + shared buffers) available on every AOTrino app
        RegisterHostObjects();
        _ = NavigateToStartAsync();
    }

    // override to expose JS-callable host objects (via AddHostObject) before the page navigates
    protected virtual void RegisterHostObjects()
    {
    }

    private async Task NavigateToStartAsync()
    {
        // extraction runs on a worker thread; the continuation resumes on the window's
        // synchronization context, so Navigate is called back on the UI thread
        var app = AOTrinoApplication.Current;
        if (app != null)
        {
            await app.WebRoot.EnsureFilesAsync();
        }

        var url = StartUrl;
        if (!string.IsNullOrEmpty(url))
        {
            Navigate(url);
        }
    }
}
