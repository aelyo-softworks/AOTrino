namespace AOTrino.Samples.ShellCommander;

// rendered markdown for the preview. Html is the document body, rendered on the host side with Markdig.
// BaseHref is the file:// of the markdown's own folder, so its relative images and links resolve when clicked.
public sealed record MarkdownResult(string Html, string BaseHref);
