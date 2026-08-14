namespace AOTrino.Samples.FluentUI.Gallery;

// the nuget flat container index: /v3-flatcontainer/{id}/index.json lists every published version, oldest first.
public sealed class NugetIndex
{
    [JsonPropertyName("versions")]
    public string[]? Versions { get; set; }
}
