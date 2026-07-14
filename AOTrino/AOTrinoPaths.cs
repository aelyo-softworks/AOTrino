namespace AOTrino;

// per-application paths derived from the app assembly (title + informational version).
// generalizes what a host app would otherwise hardcode in its own Settings class.
// instance (not static) so it carries no process-global state, owned by AOTrinoApplication.
public class AOTrinoPaths
{
    public AOTrinoPaths(Assembly appAssembly)
    {
        ArgumentNullException.ThrowIfNull(appAssembly);
        AppName = appAssembly.GetName().Name ?? nameof(AOTrino);
        AppTitle = appAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? AppName;
        var version = appAssembly.GetInformationalVersion().Nullify() ?? appAssembly.GetName().Version?.ToString() ?? "1.0.0";

        ConfigurationDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        WebView2UserDataPath = Path.Combine(ConfigurationDirectoryPath, "WebView2");
        WebRootPath = Path.Combine(ConfigurationDirectoryPath, "WebRoot", version);
        WebRootDistPath = Path.Combine(WebRootPath, "dist");
    }

    public string AppName { get; }
    public string AppTitle { get; }
    public string ConfigurationDirectoryPath { get; }
    public string WebView2UserDataPath { get; }

    // WebRoot resources are versioned so an app update re-extracts a fresh copy
    public string WebRootPath { get; }
    public string WebRootDistPath { get; }
}
