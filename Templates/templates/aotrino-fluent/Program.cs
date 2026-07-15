using AOTrino;

namespace AOTrinoApp1;

internal static class Program
{
    [System.STAThread]
    static void Main()
    {
        // AOTrinoApplication closes the process itself (with a download link) if the WebView2 runtime is missing
        using var app = new AOTrinoApplication();
        using var window = new MainWindow();
        window.ResizeClient(1000, 700);
        window.Center();
        window.Show();
        app.Run();
    }
}
