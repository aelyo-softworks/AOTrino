namespace AOTrino;

// per-application paths derived from the entry assembly (title + informational version).
// generalizes what a host app would otherwise hardcode in its own Settings class.
public static class AOTrinoPaths
{
    static AOTrinoPaths()
    {
        var entry = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        AppName = entry.GetTitle().Nullify() ?? entry.GetName().Name ?? "AOTrino";
        var version = entry.GetInformationalVersion().Nullify() ?? entry.GetName().Version?.ToString() ?? "1.0.0";

        ConfigurationDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        WebView2UserDataPath = Path.Combine(ConfigurationDirectoryPath, "WebView2");
        WebRootPath = Path.Combine(ConfigurationDirectoryPath, "WebRoot", version);
        WebRootDistPath = Path.Combine(WebRootPath, "dist");
    }

    public static string AppName { get; }
    public static string ConfigurationDirectoryPath { get; }
    public static string WebView2UserDataPath { get; }

    // WebRoot resources are versioned so an app update re-extracts a fresh copy
    public static string WebRootPath { get; }
    public static string WebRootDistPath { get; }
}
