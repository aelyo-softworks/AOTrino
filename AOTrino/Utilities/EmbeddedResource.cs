namespace AOTrino.Utilities;

// loads and caches embedded text resources (JS/CSS/HTML) by their logical name. AOTrino uses it for its own __aotrino runtimes,
// apps can use it (via WebViewWindow.AddStartupScriptResource, or directly) to keep scripts in .js files instead of C# string literals.
// drop files under a project's Scripts\ folder and they are embedded with their bare file name as the logical name (see Directory.Build.targets).
public static class EmbeddedResource
{
    private static readonly ConcurrentDictionary<string, string> _cache = new();

    // load + cache a text resource embedded in 'assembly' by its logical name, e.g. "BrowserChrome.js".
    public static string Load(Assembly assembly, string name)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _cache.GetOrAdd($"{assembly.FullName}\n{name}", _ =>
        {
            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Embedded resource '{name}' was not found in {assembly.GetName().Name}.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    // load from the AOTrino assembly (its own runtimes).
    internal static string Load(string name) => Load(typeof(EmbeddedResource).Assembly, name);
}
