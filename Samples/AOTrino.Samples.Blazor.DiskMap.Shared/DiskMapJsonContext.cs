namespace AOTrino.Samples.Blazor.DiskMap.Shared;

using System.Text.Json.Serialization;

// the serializer, generated at compile time, and shared by both sides.
// the host serializes with it and the page deserializes with it, from the same source file,
// so the wire format cannot drift between the two and neither side needs reflection,
// which native AOT on one side and a trimmed wasm build on the other would both refuse anyway.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<DriveEntry>))]
[JsonSerializable(typeof(DriveEntry[]))]
[JsonSerializable(typeof(List<NodeEntry>))]
[JsonSerializable(typeof(NodeEntry[]))]
[JsonSerializable(typeof(NodeEntry))]
[JsonSerializable(typeof(ScanProgress))]
public partial class DiskMapJsonContext : JsonSerializerContext
{
}
