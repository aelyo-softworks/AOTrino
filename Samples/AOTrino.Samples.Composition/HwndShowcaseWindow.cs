namespace AOTrino.Samples.Composition;

// hosts the same page as a classic child window (ICoreWebView2Controller on the HWND).
// there's no composition tree: the WebView is an opaque child window that fills the client area.
// it can't be scaled, layered under other visuals, or given a backdrop. that's the contrast with CompositionShowcaseWindow.
public partial class HwndShowcaseWindow : HwndWebViewWindow
{
    public HwndShowcaseWindow()
        : base("AOTrino — HWND host")
    {
    }

    protected override void ControllerCreated()
    {
        base.ControllerCreated();
        EnsureSharedRuntime(); // window.__aotrino (Close button) on the page.
        _ = NavigateToWebRootAsync();
    }
}
