namespace AOTrino.Utilities;

// loads and caches embedded script resources (the __aotrino JS runtimes) from this assembly
internal static class EmbeddedResource
{
    private static readonly ConcurrentDictionary<string, string> _cache = new();

    public static string Load(string name) => _cache.GetOrAdd(name, static n =>
    {
        var assembly = typeof(EmbeddedResource).Assembly;
        using var stream = assembly.GetManifestResourceStream(n) ?? throw new InvalidOperationException($"Embedded script '{n}' was not found in {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
