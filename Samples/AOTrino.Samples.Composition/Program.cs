namespace AOTrino.Samples.Composition;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // two windows, same page,
        // different hosting: one on the composition engine (the WebView is a visual we can transform + composite with other visuals),
        // one classic HWND-hosted (an opaque child window).
        using var app = new AOTrinoApplication();
        using var composition = new CompositionShowcaseWindow();
        using var hwnd = new HwndShowcaseWindow();

        // the app treats the FIRST window as the main one and later windows as "background", make them peers,
        // then close one when the other closes so Close / Alt+F4 on either window shuts down the whole demo.
        hwnd.IsBackground = false;
        composition.Destroyed += (s, e) => { if (!hwnd.IsDisposed) hwnd.Close(); };
        hwnd.Destroyed += (s, e) => { if (!composition.IsDisposed) composition.Close(); };

        PlaceSideBySide(composition, hwnd);
        composition.Show();
        hwnd.Show();
        app.Run();
    }

    private static void PlaceSideBySide(WebViewWindow left, WebViewWindow right)
    {
        var screenWidth = DirectNFunctions.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var screenHeight = DirectNFunctions.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);

        const int width = 680;
        const int height = 760;
        const int gap = 24;
        var total = width * 2 + gap;
        var x = (screenWidth - total) / 2;
        var y = (screenHeight - height) / 2;

        // these are custom-frame windows (client fills the window), so window size ~= client size.
        DirectNFunctions.SetWindowPos(left.Handle, HWND.Null, x, y, width, height, SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        DirectNFunctions.SetWindowPos(right.Handle, HWND.Null, x + width + gap, y, width, height, SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }
}
