namespace AOTrino.Samples.Localization;

internal static class Program
{
    // the one place the app's strings live. "AOTrino.Samples.Localization.Strings" is Strings.resx,
    // and the names after it are the satellite assemblies beside the executable.
    // de is deliberately incomplete, see Strings.de.resx, which is what a translation in progress looks like.
    // the first culture is the one written into the main assembly, and the fallback for anything missing.
    internal static readonly AOTrino.Localization _strings = new(new("AOTrino.Samples.Localization.Strings", typeof(Program).Assembly), "en", "fr", "de");

    [STAThread]
    static void Main()
    {
        using var app = new AOTrinoApplication();
        using var window = new MainWindow();
        window.ResizeClient(880, 560);
        window.Center();
        window.Show();
        app.Run();
    }
}
