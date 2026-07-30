namespace AOTrino.Samples.ShellCommander;

// a shell change notification pushed to the page. Action is add/remove/update/rename/refresh.
// ParentId is the folder it happened in (the page applies it only when that is the folder on screen).
// Entry is the new/changed item (add, update, rename), OldId the item that went away (remove, rename).
// Message is the toast text.
public sealed record ShellChange(string Action, ShellEntry? Entry, string? OldId, string Message);
