namespace AOTrino.Samples.ShellCommander;

// one batch of rows drained from a running listing, plus whether the listing is now complete.
// the page keeps draining until Done, appending each batch as it arrives, so the first rows show without waiting for the last.
public sealed record ListChunk(IReadOnlyList<ShellEntry> Entries, bool Done, string? Error);
