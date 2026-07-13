namespace AOTrino.Samples.Direct2D;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var app = new AOTrinoApplication();
        using var window = new MainWindow();
        window.ResizeClient(1000, 720);
        window.Center();
        window.Show();
        app.Run();
    }
}
