namespace AOTrino.Samples.ShellCommander;

// AOT-safe JSON: source-generated serialization for the shell listing and preview payloads.
[JsonSerializable(typeof(ListStart))]
[JsonSerializable(typeof(ListChunk))]
[JsonSerializable(typeof(PreviewResult))]
[JsonSerializable(typeof(MarkdownResult))]
[JsonSerializable(typeof(List<TerminalShell>))]
[JsonSerializable(typeof(ItemDetails))]
[JsonSerializable(typeof(ShellChange))]
internal partial class ShellCommanderJsonContext : JsonSerializerContext
{
}
