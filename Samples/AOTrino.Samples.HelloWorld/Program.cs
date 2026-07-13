namespace AOTrino.Samples.HelloWorld;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        WindowSynchronizationContext.Install();

        WebView2Utilities.Initialize(Assembly.GetEntryAssembly());
        var version = WebView2Utilities.GetAvailableCoreWebView2BrowserVersionString();
        if (string.IsNullOrWhiteSpace(version))
        {
            // no WebView2 runtime installed; a real app would prompt the user to install it
            return;
        }

        // let the page paint its own background through the (otherwise transparent) composition surface
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00000000");
        _ = WebRoot.EnsureFilesAsync();

        using var app = new CompositionApplication();
        using var window = new MainWindow();
        window.ResizeClient(1000, 700);
        window.Center();
        window.Show();
        app.Run();
    }
}
