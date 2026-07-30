namespace AOTrino.Samples.ShellCommander;

// one label/value row of an item's details, read from the Windows property store.
// Category groups the rows in the "all properties" view, derived from the property's canonical name. null for the curated card.
public sealed record DetailRow(string Label, string Value, string? Category = null);
