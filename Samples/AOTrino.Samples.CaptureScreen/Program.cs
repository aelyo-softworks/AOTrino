namespace AOTrino.Samples.CaptureScreen;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var app = new AOTrinoApplication();
        using var window = new CaptureWindow();
        window.ResizeClient(900, 620);
        window.Center();
        window.Show();
        app.Run();
    }
}
