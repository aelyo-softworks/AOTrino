namespace AOTrino.Samples.FluentUI.Gallery;

// one row of the virtual table. deliberately small: a row crosses the bridge as JSON, and a page of them is the unit of traffic,
// so what a row costs is what scrolling costs.
public sealed record GalleryRow(
    int Index,
    string Name,
    string Kind,
    long Size,
    string Modified);
