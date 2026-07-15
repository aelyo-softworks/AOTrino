namespace AOTrino.Samples.FluentUI.Gallery;

// complex data for the Bridge page: crosses as JSON, because a nested array wouldn't survive (see docs/BRIDGE.md)
public sealed record GalleryProcessInfo(
    int ProcessId,
    int ProcessorCount,
    string Architecture,
    long WorkingSet,
    long ManagedHeap,
    int Collections);
