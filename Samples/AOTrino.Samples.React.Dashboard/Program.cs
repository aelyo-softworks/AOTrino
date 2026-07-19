namespace AOTrino.Samples.React.Dashboard;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // AOTrinoApplication closes the process itself (with a download link) if WebView2 is missing.
        using var app = new AOTrinoApplication();
        using var window = new MainWindow();
        window.ResizeClient(1000, 1000);
        window.Center();
        window.Show();
        app.Run();
    }
}
