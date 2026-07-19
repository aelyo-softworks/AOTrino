namespace AOTrino.Samples.FluentUI.Gallery;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // AOTrinoApplication closes the process itself (with a download link) if WebView2 is missing.
        using var app = new AOTrinoApplication();
        using var window = new MainWindow();
        window.ResizeClient(1180, 820);
        window.Center();
        window.Show();
        app.Run();
    }
}
