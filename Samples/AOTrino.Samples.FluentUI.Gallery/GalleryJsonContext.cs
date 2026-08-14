namespace AOTrino.Samples.FluentUI.Gallery;

// AOT-safe JSON: source-generated, because reflection-based serialization doesn't survive AOT.
[JsonSerializable(typeof(GalleryProcessInfo))]
[JsonSerializable(typeof(GalleryRowPage))]
[JsonSerializable(typeof(PackageInfo[]))]
[JsonSerializable(typeof(NugetIndex))]
[JsonSerializable(typeof(NpmLatest))]
internal partial class GalleryJsonContext : JsonSerializerContext
{
}
