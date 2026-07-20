namespace AOTrino.Samples.FileExplorer;

// AOT-safe JSON: source-generated serialization for the file-browser payloads.
[JsonSerializable(typeof(DirListing))]
// the dropped paths, on their way to the page.
[JsonSerializable(typeof(IReadOnlyList<string>))]
// and the folder to show, quoted and escaped for the script that carries it.
[JsonSerializable(typeof(string))]
internal partial class FileExplorerJsonContext : JsonSerializerContext
{
}
