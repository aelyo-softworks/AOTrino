namespace AOTrino;

// hosts the WebView as a classic child window (ICoreWebView2Controller on the HWND).
// the WebView renders and handles its own input, so there's no composition tree and no manual input forwarding.
// the trade-off is that the WebView is an opaque child window that can't be transformed/composited like a visual.
// use this for simplicity or maximum compatibility (e.g. WinForms-style interop).
// use CompositionWebViewWindow for the composition-engine capabilities.
// the window is a normal redirected window (not NoRedirectionBitmap).
public partial class HwndWebViewWindow(
    string? title = null,
    WINDOW_STYLE style = WINDOW_STYLE.WS_THICKFRAME,
    WINDOW_EX_STYLE extendedStyle = 0,
    RECT? rect = null) : WebViewWindow(title, style: style, extendedStyle: extendedStyle, rect: rect)
{
    private ComObject<ICoreWebView2Controller>? _controller;

    protected override void CreateController(ICoreWebView2Environment12 environment, Action onControllerReady)
    {
        environment.CreateCoreWebView2Controller(Handle, new CoreWebView2CreateCoreWebView2ControllerCompletedHandler((result, controller) =>
        {
            try
            {
                _controller = new ComObject<ICoreWebView2Controller>(controller);
                controller.put_Bounds(ClientRect).ThrowOnError();
                controller.get_CoreWebView2(out var webView2).ThrowOnError();
                SetWebViewController(controller, webView2);
                onControllerReady();
            }
            catch (Exception ex)
            {
                Application.AddError(ex, true);
            }
        })).ThrowOnError();
    }

    // no D3D/D2D device is needed: the child WebView renders itself.
    protected override void CreateDeviceResources() { }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachController(); // before disposing the controller: teardown focus/size messages must not hit it.
            _controller?.Dispose();
        }
        base.Dispose(disposing);
    }
}
