namespace AOTrino.Samples.Translucid;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var app = new AOTrinoApplication();
        using var window = new TranslucidWindow();
        window.ResizeClient(760, 560);
        window.Center();
        window.Show();
        app.Run();
    }
}
