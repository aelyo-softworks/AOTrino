namespace AOTrino.Samples.FluentUI.Gallery;

// the npm registry's latest tag document: /{name}/latest returns the manifest of the version tagged "latest".
public sealed class NpmLatest
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
