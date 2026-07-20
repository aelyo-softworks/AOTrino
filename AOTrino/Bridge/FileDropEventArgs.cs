namespace AOTrino.Bridge;

// files dragged onto the window from Explorer, or from anything else that offers them as CF_HDROP.
//
// these are real paths, which is the whole point. a page can receive an HTML5 drop and get a File object,
// with the bytes and the name and nothing else: no directory, no way to reopen it later, no way to hand it to
// another program. what arrives here is what Explorer dropped, the path itself.
public class FileDropEventArgs(IReadOnlyList<string> paths, POINT point, DROPEFFECT allowedEffects) : EventArgs
{
    public IReadOnlyList<string> Paths { get; } = paths;

    // where the drop landed, in client coordinates, so it can be matched against whatever the page draws there.
    public POINT Point { get; } = point;

    // what the source is willing to allow, copy, move, link, in any combination.
    public DROPEFFECT AllowedEffects { get; } = allowedEffects;

    // what this window chose to do, which is what Explorer shows on the cursor and acts on when the button is released.
    // it has to be one the source allows, anything else is reported back as no drop at all.
    public DROPEFFECT Effect { get; set; } = DROPEFFECT.DROPEFFECT_COPY;
}
