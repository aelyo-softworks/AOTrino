namespace AOTrino.Samples.ShellCommander;

internal static class Program
{
    // the app's user facing strings, read from Strings.resx. one place the text is written, whatever reads it:
    // the C# side calls GetString, the page loads the whole catalog once through the localization host object.
    // this sample ships one language, so only the neutral resx (in the main assembly) is declared.
    internal static readonly Localization _strings = new(new ResourceManager("AOTrino.Samples.ShellCommander.Strings", typeof(Program).Assembly), "en");

    [STAThread]
    static void Main()
    {
        // AOTrinoApplication closes the process itself (with a download link) if WebView2 is missing.
        using var app = new AOTrinoApplication();
        using var window = new MainWindow();
        window.ResizeClient(1100, 720);
        window.Center();
        window.Show();
        app.Run();
    }
}
