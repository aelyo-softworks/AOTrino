namespace AOTrino.Samples.FileExplorer;

// dragging files OUT of this window and into Explorer, the desktop, or any other program that takes files.
//
// this is the direction a web page has no answer for at all.
// HTML5 drag and drop moves text and elements inside the document, and a page can be dropped ON,
// but nothing in a browser can start a drag the shell will accept and turn into a copied file.
// that needs an OLE drag, and an OLE drag needs a data object.
//
// the data object is the shell's own. the paths are turned into item id lists and handed to SHCreateDataObject,
// which returns an object carrying every format the shell publishes and needs for those items
internal static partial class FileDragSource
{
    // starts the drag and does not return until the user drops or gives up,
    // because the drag runs its own modal loop, which is also why the mouse button has to still be down when this is called.
    public static DROPEFFECT Drag(HWND owner, IReadOnlyList<string> paths)
    {
        using var data = CreateDataObject(paths);
        if (data == null)
            return DROPEFFECT.DROPEFFECT_NONE;

        // SHDoDragDrop rather than DoDragDrop: the shell version draws the drag image under the cursor,
        // so this looks like every other drag on the machine instead of like a bare rectangle.
        // copy and link only, this sample never offers to move a file it did not create.
        var source = new DropSource();
        DirectNFunctions.SHDoDragDrop(owner, data.Object, source, DROPEFFECT.DROPEFFECT_COPY | DROPEFFECT.DROPEFFECT_LINK, out var effect);
        return effect;
    }

    // one shell data object for a set of paths.
    private static unsafe IComObject<IDataObject>? CreateDataObject(IReadOnlyList<string> paths)
    {
        var pidls = new List<nint>(paths.Count);
        try
        {
            foreach (var path in paths)
            {
                // the shell's own parse, so anything it can name works here, not only a path on a disk.
                if (SHParseDisplayName(PWSTR.From(path), 0, out var pidl, 0, 0).IsError || pidl == 0)
                    continue;

                pidls.Add(pidl);
            }

            if (pidls.Count == 0)
                return null;

            var items = pidls.ToArray();
            var terminator = 0;
            var hr = DirectNFunctions.SHCreateDataObject(
                (nint)(&terminator),
                (uint)items.Length,
                (nint)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(items)),
                null!,
                typeof(IDataObject).GUID,
                out var unknown);

            if (hr.IsError || unknown == 0)
                return null;

            return ComObject.FromPointer<IDataObject>(unknown);
        }
        finally
        {
            // the lists came from the shell's allocator and go back to it, the data object kept what it needed.
            foreach (var pidl in pidls)
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }
    }

    // https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shparsedisplayname
    // declared here because DirectN does not have it.
    // ShellN & ShellN.Extensions has it https://github.com/smourier/ShellBat/tree/main/ShellN but we don't include the whole thing just for this
    [LibraryImport("SHELL32")]
    private static partial HRESULT SHParseDisplayName(PWSTR name, nint bindContext, out nint pidl, uint attributesIn, nint attributesOut);

    // the drag itself: keep going, drop, or give up, decided from the mouse and the escape key.
    [System.Runtime.InteropServices.Marshalling.GeneratedComClass]
    private sealed partial class DropSource : IDropSource
    {
        public HRESULT QueryContinueDrag(BOOL escapePressed, MODIFIERKEYS_FLAGS keys)
        {
            if (escapePressed)
                return DirectNConstants.DRAGDROP_S_CANCEL;

            // the button that started the drag coming back up is the drop.
            if (!keys.HasFlag(MODIFIERKEYS_FLAGS.MK_LBUTTON))
                return DirectNConstants.DRAGDROP_S_DROP;

            return DirectNConstants.S_OK;
        }

        // the shell's own cursors, so the feedback matches every other drag the user has ever done.
        public HRESULT GiveFeedback(DROPEFFECT effect) => DirectNConstants.DRAGDROP_S_USEDEFAULTCURSORS;
    }
}
