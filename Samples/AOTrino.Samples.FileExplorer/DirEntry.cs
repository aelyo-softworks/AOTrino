namespace AOTrino.Samples.FileExplorer;

// one row in a directory listing, sent to JS as part of DirListing (JSON.parse on the JS side).
public sealed record DirEntry(
    string Name,
    string Path,
    bool IsDirectory,
    long Size,
    string Modified);
