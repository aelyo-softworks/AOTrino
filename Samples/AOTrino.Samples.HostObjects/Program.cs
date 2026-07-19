namespace AOTrino.Samples.HostObjects;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // HostObjectsApplication overrides the trace methods to capture JS console output;
        // AOTrinoApplication closes the process itself (with a download link) if WebView2 is missing.
        using var app = new HostObjectsApplication();
        using var window = new MainWindow();
        window.ResizeClient(1000, 830);
        window.Center();
        window.Show();
        app.Run();
    }
}
