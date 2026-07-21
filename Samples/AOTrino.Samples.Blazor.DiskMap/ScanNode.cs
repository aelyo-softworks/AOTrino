namespace AOTrino.Samples.Blazor.DiskMap;

// one directory in the scanned tree, with the total size of everything under it.
// a class rather than a struct because the tree is built by reference and can go deep.
//
// FullPath and Parent are settable because the two scanners build the tree from opposite ends.
// the directory walk knows a node's full path before the node exists,
// while the master file table knows only a name and a parent record number,
// so there a path is something that can only be worked out once every node is in place.
public sealed class ScanNode(string name, string fullPath, ScanNode? parent)
{
    public string Name { get; } = name;
    public string FullPath { get; set; } = fullPath;
    public ScanNode? Parent { get; set; } = parent;
    public List<ScanNode> Children { get; } = [];

    // bytes of the files directly in this directory, and of everything below it.
    public long OwnSize { get; set; }

    // the total is written from the scan threads and read from the UI thread while the scan runs, so it goes through interlocked access rather than being a plain property.
    private long _totalSize;
    public long TotalSize { get => Interlocked.Read(ref _totalSize); set => Interlocked.Exchange(ref _totalSize, value); }

    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }

    // adds a directory's own bytes to this node's running total.
    public void AddToTotal(long bytes) => Interlocked.Add(ref _totalSize, bytes);

    // the node at 'path' within this subtree, itself or a descendant, or null if it is not under here.
    // only the branch whose path could contain it is walked, so a navigation is not a full tree search.
    // the children are snapshotted because a scan on another thread may still be adding to them.
    public ScanNode? Find(string path)
    {
        if (FullPath.EqualsIgnoreCase(path))
            return this;

        foreach (var child in Children.ToArray())
        {
            if (IsSelfOrDescendant(path, child.FullPath))
                return child.Find(path);
        }

        return null;
    }

    // whether 'path' is 'ancestor' itself or something below it, matched on a directory separator so a folder
    // is never mistaken for a sibling that merely starts with the same characters, "C:\Program Files" against
    // "C:\Program Files (x86)".
    private static bool IsSelfOrDescendant(string path, string ancestor)
    {
        if (path.Length < ancestor.Length || !path.StartsWith(ancestor, StringComparison.OrdinalIgnoreCase))
            return false;

        return path.Length == ancestor.Length || ancestor.EndsWith('\\') || path[ancestor.Length] == '\\';
    }
}
