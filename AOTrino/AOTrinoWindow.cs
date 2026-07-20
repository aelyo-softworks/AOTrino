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
    // when set, the WebRoot is served to the WebView from this host name over https (https://{VirtualHostName}/index.html) instead of being read straight off disk over file://.
    // set it when the front end is built by a bundler: a file:// page has an opaque origin, so Chromium
    // CORS-blocks ES modules and the page renders blank unless the app opts into --allow-file-access-from-files.
    // a virtual host gives the page an ordinary https origin instead, so module output loads with no browser flag at all.
    // only the app's own WebRoot folder is mapped.
    // null (the default) keeps file://, which reads local files faster; see docs/SECURITY.md.
    protected virtual string? VirtualHostName => null;

    // navigated to once the controller is created, defaults to the app's WebRoot index.html.
    protected virtual string? StartUrl => VirtualHostName != null
        ? $"https://{VirtualHostName}/{WebRoot.IndexFileName}"
        : AOTrinoApplication.Current?.WebRoot.IndexFilePath;

    // where this window may navigate.
    // Local (default) keeps the window on the app's own content and hands off-app links to the default browser;
    // Web turns the window into a browser.
    // this governs navigation only and makes no security claim (web security / file access stay a developer choice via env options).
    public NavigationMode NavigationMode { get; set; } = NavigationMode.Local;

    // a window that browses the real web keeps the browser's own failure page, which is written for exactly that
    // and offers to retry. every other window shows AOTrino's, see WebViewWindow.GetNavigationErrorPage.
    protected override bool ReplacesNavigationErrorPage => NavigationMode != NavigationMode.Web;

    // the browser behaviours WebViewWindow turns off for app windows are exactly the ones a browser needs,
    // so NavigationMode.Web puts every one of them back: a mini browser without a context menu, a status bar,
    // Ctrl+R or the page telling you a site is unreachable is not a browser.
    // see the comments on each of these in WebViewWindow for what they do and why an app wants them off.
    private bool IsBrowser => NavigationMode == NavigationMode.Web;

    protected override bool AreDefaultContextMenusEnabled => IsBrowser;
    protected override bool IsStatusBarEnabled => IsBrowser;
    protected override bool AreBrowserAcceleratorKeysEnabled => IsBrowser;
    protected override bool IsBuiltInErrorPageEnabled => IsBrowser;

    // the app's own content is the WebRoot, which is either read off disk over file:// or served from the virtual host.
    protected override bool IsAppContentUri(string uri)
    {
        var host = VirtualHostName;
        if (host != null && Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.Host.EqualsIgnoreCase(host))
            return true;

        return base.IsAppContentUri(uri);
    }

    protected override void ControllerCreated()
    {
        base.ControllerCreated();
        EnsureSharedRuntime(); // window.__aotrino (window controls + shared buffers) available on every AOTrino app.
        RegisterHostObjects();
        _ = NavigateToStartAsync();
    }

    // override to expose JS-callable host objects (via AddHostObject) before the page navigates.
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

        // Local: the app's own content (from disk, or from its virtual host) plus in-page schemes;
        // anything else leaves the app.
        if (uri.Scheme == Uri.UriSchemeFile)
            return true;

        var host = VirtualHostName;
        if (host != null && uri.Scheme == Uri.UriSchemeHttps && string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))
            return true;

        return uri.Scheme is "about" or "data" or "blob";
    }

    // whether the page may rename the window (window.__aotrino.setWindowTitle).
    // the default follows NavigationMode, like IsNavigationAllowed does:
    // a Local window shows the app's own content, and a caption it drew in HTML should agree with the taskbar;
    // a Web window is showing a stranger, and the window's name says which app you are looking at, not a site's to write.
    // a page still has document.title, as in any browser tab.
    // override to decide otherwise, e.g. a Local window with an allow-list that admits an origin you'd rather not let rename it.
    protected override void SetWindowTitleFromPage(string? title)
    {
        if (NavigationMode == NavigationMode.Web)
            return;

        base.SetWindowTitleFromPage(title);
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

    // hands a blocked navigation to the OS default handler (the real browser for web links).
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

    // serves the extracted WebRoot from VirtualHostName. DENY_CORS: the page loads its own files (they are its origin),
    // while other origins are refused.
    protected virtual void MapVirtualHost()
    {
        var host = VirtualHostName;
        if (host == null)
            return;

        var dist = AOTrinoApplication.Current?.WebRoot.DistPath;
        if (dist == null)
            return;

        WebView?.Object.SetVirtualHostNameToFolderMapping(PWSTR.From(host), PWSTR.From(dist),
            COREWEBVIEW2_HOST_RESOURCE_ACCESS_KIND.COREWEBVIEW2_HOST_RESOURCE_ACCESS_KIND_DENY_CORS).ThrowOnError();
    }

    protected virtual async Task NavigateToStartAsync()
    {
        // extraction runs on a worker thread, the continuation resumes on the window's synchronization context,
        // so Navigate is called back on the UI thread.
        var app = AOTrinoApplication.Current;
        if (app != null)
        {
            await app.WebRoot.EnsureFilesAsync();
        }

        MapVirtualHost(); // the folder must exist before it's served.

        var url = StartUrl;
        if (!string.IsNullOrEmpty(url))
        {
            Navigate(url);
        }
    }
}
