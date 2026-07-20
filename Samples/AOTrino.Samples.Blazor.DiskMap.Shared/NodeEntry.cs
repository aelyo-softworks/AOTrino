namespace AOTrino.Samples.Blazor.DiskMap.Shared;

// one row of a level, everything the page needs to draw and label it.
public sealed class NodeEntry
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }

    // true for the synthetic row that totals the files sitting directly in a directory, which is not somewhere to descend into.
    public bool IsFileBucket => FullPath.Length == 0;
}
