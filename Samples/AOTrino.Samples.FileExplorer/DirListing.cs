namespace AOTrino.Samples.FileExplorer;

// the result of FileSystemApi.List: the folder itself, its parent (empty = the drives root, null = top),
// an optional error (e.g. access denied), and the entries. returned to JS as a JSON string.
public sealed record DirListing(
    string Path,
    string? Parent,
    string? Error,
    IReadOnlyList<DirEntry> Entries);
