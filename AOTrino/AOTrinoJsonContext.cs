namespace AOTrino;

// AOT-safe JSON for the few strings the core itself serializes into the injected runtime
[JsonSerializable(typeof(string))]
internal partial class AOTrinoJsonContext : JsonSerializerContext
{
}
