namespace AOTrino.Samples.ShellCommander;

// AOT-safe JSON: source-generated serialization for the shell listing and preview payloads.
[JsonSerializable(typeof(ShellListing))]
[JsonSerializable(typeof(PreviewResult))]
[JsonSerializable(typeof(MarkdownResult))]
internal partial class ShellCommanderJsonContext : JsonSerializerContext
{
}
