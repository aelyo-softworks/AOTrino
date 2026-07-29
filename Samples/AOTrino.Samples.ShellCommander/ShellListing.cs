namespace AOTrino.Samples.ShellCommander;

// the contents of one shell folder, sent to JS as JSON.
// Id is the folder's own parsing name (empty for the Desktop root),
// ParentId is where the Up button goes (empty means back to the Desktop, null means there is nowhere up from here).
public sealed record ShellListing(
    string Name,
    string Id,
    string? ParentId,
    string? Error,
    IReadOnlyList<ShellEntry> Entries);
