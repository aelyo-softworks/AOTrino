namespace AOTrino.Samples.FluentUI.Gallery;

// what a page request answers with: the slice, and where it came from.
// Offset comes back so a late reply can be matched to the request that asked for it - scrolling fast means
// replies arrive out of order, and a page that trusted arrival order would paint the wrong rows.
public sealed record GalleryRowPage(
    int Offset,
    int Total,
    GalleryRow[] Rows);
