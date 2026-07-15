namespace AOTrino.Samples.WebBrowser;

internal static class Program
{
    private const string _urlOption = "--url";

    [STAThread]
    static void Main(string[] args)
    {
        // AOTrinoApplication closes the process itself (with a download link) if WebView2 is missing
        using var app = new AOTrinoApplication();
        using var window = new MainWindow(GetStartUrl(args));
        window.ResizeClient(1100, 780);
        window.Center();
        window.Show();
        app.Run();
    }

    // "AOTrino.Samples.WebBrowser.exe --url https://example.com", "--url=https://example.com", or just the URL
    // on its own, the way a real browser is called by a file association or a protocol handler.
    // http/https only. this window is a browser, so it would happily navigate to file:// or javascript: - but a
    // command line is an input like any other, and it arrives from shortcuts, associations and other processes,
    // not necessarily from the user. a URL that isn't one of the two schemes a browser is for is dropped, and
    // the browser opens on its own start page.
    private static string? GetStartUrl(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? value;
            if (arg.StartsWith(_urlOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg[(_urlOption.Length + 1)..];
            }
            else if (arg.Equals(_urlOption, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                value = args[++i];
            }
            else if (!arg.StartsWith('-'))
            {
                value = arg;
            }
            else
            {
                continue;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                return uri.ToString();
        }

        return null;
    }
}
