namespace AOTrino.Samples.ShellCommander;

// one item in a shell folder listing, sent to JS as part of ShellListing.
// Id is a desktop absolute parsing name, the string the page hands back to List to navigate into this item.
// for a real drive or folder it is the path, for a virtual place it is a ::{CLSID}.
// Size is bytes, -1 when the item has no size (a folder or a virtual place). Modified is preformatted, empty when unknown.
public sealed record ShellEntry(
    string Name,
    string Id,
    bool IsFolder,
    long Size,
    string Modified);
