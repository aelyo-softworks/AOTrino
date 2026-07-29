namespace AOTrino.Samples.ShellCommander;

// the answer to a preview request:
// Path is the file the WebView2 should load, the item's own path when it is on disk, or a temp file its stream was copied or decoded into.
// Kind tells the page how to show it, an "image" it can fit to the pane, or a "document" (pdf, media, text) the WebView2 renders in a frame.
// Error is set when there is nothing to preview.
public sealed record PreviewResult(string? Path, string? Kind, string? Error);
