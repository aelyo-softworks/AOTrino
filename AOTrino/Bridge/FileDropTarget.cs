namespace AOTrino;

// the window's OLE drop target, which is what makes a drop from Explorer arrive as file paths.
//
// a composition hosted WebView never receives an external drop of its own:
// it has no window to drop onto, and WebView2 exposes DragEnter, DragOver, DragLeave and Drop on the composition controller,
// precisely because the host is expected to own the drop target and forward to it.
// so this is not competing with the page for drops, it is the only thing that can receive one.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
internal sealed partial class FileDropTarget(WebViewWindow window) : IDropTarget
{
    private bool _hasFiles;

    HRESULT IDropTarget.DragEnter(IDataObject dataObject, MODIFIERKEYS_FLAGS keys, POINTL point, ref DROPEFFECT effect)
    {
        _hasFiles = HasFiles(dataObject);
        effect = _hasFiles ? window.GetFileDropEffect(effect) : DROPEFFECT.DROPEFFECT_NONE;
        return DirectNConstants.S_OK;
    }

    HRESULT IDropTarget.DragOver(MODIFIERKEYS_FLAGS keys, POINTL point, ref DROPEFFECT effect)
    {
        effect = _hasFiles ? window.GetFileDropEffect(effect) : DROPEFFECT.DROPEFFECT_NONE;
        return DirectNConstants.S_OK;
    }

    HRESULT IDropTarget.DragLeave()
    {
        _hasFiles = false;
        return DirectNConstants.S_OK;
    }

    HRESULT IDropTarget.Drop(IDataObject dataObject, MODIFIERKEYS_FLAGS keys, POINTL point, ref DROPEFFECT effect)
    {
        var allowed = effect;
        effect = DROPEFFECT.DROPEFFECT_NONE;
        _hasFiles = false;

        try
        {
            var paths = GetFiles(dataObject);
            if (paths.Count == 0)
                return DirectNConstants.S_OK;

            // OLE reports the drop in screen coordinates, the window wants to know where in itself it landed.
            var client = new POINT { x = point.x, y = point.y };
            DirectNFunctions.ScreenToClient(window.Handle, ref client);

            var e = new FileDropEventArgs(paths, client, allowed);
            window.OnFilesDropped(e);
            effect = e.Effect;
        }
        catch (Exception ex)
        {
            // a failed drop is not a reason to take the window with it, the cursor just says nothing happened.
            AOTrinoApplication.Current?.TraceWarning($"The drop could not be handled: {ex.Message}");
        }

        return DirectNConstants.S_OK;
    }

    // DirectN.Extensions already knows how to read a CF_HDROP, so this asks it rather than walking the block by hand.
    // owned: false, the data object belongs to the drag that is in progress, not to this wrapper.
    private static IReadOnlyList<string> GetFiles(IDataObject dataObject) => new DataObject(new ComObject<IDataObject>(dataObject), owned: false).GetFilesPath(throwOnError: false);
    private static bool HasFiles(IDataObject dataObject) => dataObject.Has((ushort)Clipboard.CF_HDROP);
}
