namespace AOTrino.Samples.ShellCommander;

// an item's details card, shown in the preview pane for a folder, an un-previewable file, or on demand (F4).
// Name and TypeName head the card, Rows are the property store values Windows itself reports.
public sealed record ItemDetails(string Name, string TypeName, bool IsFolder, IReadOnlyList<DetailRow> Rows, string? Error);
