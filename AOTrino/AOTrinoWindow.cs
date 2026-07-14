namespace AOTrino;

// a WebViewWindow that navigates to the application's extracted WebRoot once the controller is ready.
// host apps derive from this and typically only set a title (or override StartUrl).
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class AOTrinoWindow(
    string? title = null,
    WINDOW_STYLE style = WINDOW_STYLE.WS_THICKFRAME,
    WINDOW_EX_STYLE extendedStyle = WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP,
    RECT? rect = null) : CompositionWebViewWindow(title, style, extendedStyle, rect)
{
    // navigated to once the controller is created; defaults to the app's WebRoot index.html
    protected virtual string? StartUrl => AOTrinoApplication.Current?.WebRoot.IndexFilePath;

    // where this window may navigate. Local (default) keeps the window on the app's own content and hands
    // off-app links to the default browser; Web turns the window into a browser. this governs navigation
    // only and makes no security claim (web security / file access stay a developer choice via env options).
    public NavigationMode NavigationMode { get; set; } = NavigationMode.Local;

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

    // decides whether a navigation may proceed in this window. the default enforces NavigationMode;
    // override for a custom allow-list (e.g. Local, but permit a specific service origin).
    // return false to keep the navigation out of the window, OnNavigationStarting then opens it in the default browser instead.
    protected virtual bool IsNavigationAllowed(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (NavigationMode == NavigationMode.Web)
            return true;

        // Local: the app's own content (file://) plus in-page schemes; anything else leaves the app
        if (uri.Scheme == Uri.UriSchemeFile)
            return true;

        return uri.Scheme is "about" or "data" or "blob";
    }

    protected override void OnNavigationStarting(object? sender, NavigationEventArgs e)
    {
        base.OnNavigationStarting(sender, e);
        if (e.Cancel)
            return;

        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && !IsNavigationAllowed(uri))
        {
            e.Cancel = true;
            OpenExternal(uri);
        }
    }

    // hands a blocked navigation to the OS default handler (the real browser for web links)
    protected virtual void OpenExternal(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri.ToString(), UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Failed to open '{uri}' externally: {ex.Message}");
        }
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
