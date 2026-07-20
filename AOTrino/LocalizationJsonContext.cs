namespace AOTrino;

// the catalog is the only thing this serializes, and a source-generated context is what keeps it
// working after a Native AOT publish, where the reflection based serializer has nothing to reflect over.
[JsonSerializable(typeof(SortedDictionary<string, string>))]
internal partial class LocalizationJsonContext : JsonSerializerContext
{
}
