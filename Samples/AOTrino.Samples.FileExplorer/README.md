# File Explorer

A reduced local file browser. It starts at This PC, lists real folders, previews what it can in a second pane, and
exchanges files with Windows Explorer by dragging in both directions.

## What it shows

Everything the page does with the file system goes through a host object, `chrome.webview.hostObjects.fs`, because a
page has no file system of its own. What it gets instead is the real one, with no picker, no sandbox and no upload:

* `List(path)` enumerates a directory and returns it as JSON.
* `Open(path)` hands a file to the shell, which opens it in whatever the user has associated with it.
* `RevealInExplorer(path)` selects the item in a real Explorer window.

The last two are worth pausing on. They are not the app reimplementing Explorer, they are the app **asking Windows to
do what Windows already does**, which is what an app on a desktop is supposed to do and what a page in a browser has
no way to ask for. `Reveal` in the toolbar is the second one, on the selected file or on the folder being shown.

## Drag and drop, both ways

Drag a row out to Explorer or to the desktop, and drag files from Explorer onto the window. Neither direction is
something a web page can do, and they fail in different ways, which is why the sample does both.

**Dragging out** needs an OLE drag, and an OLE drag needs a data object. HTML5 drag and drop moves text and elements
inside a document. Nothing in a browser can start a drag the shell will accept and turn into a copied file.

The data object is the shell's own rather than one written by hand: the paths become item id lists through
`SHParseDisplayName`, and `SHCreateDataObject` returns an object carrying every format the shell publishes for those
items. Writing one by hand means offering `CF_HDROP` and nothing else, which lands in Explorer and disappoints
everything with a richer idea of a file. `SHDoDragDrop` then draws the shell's own drag image, so the drag looks like
every other drag on the machine. See `FileDragSource.cs`, which is about ninety lines including the comments.

**Dropping in** is what a file manager does with a dropped file: it copies it into the folder on screen. The paths
that arrive are **real paths**, which is the difference that matters. An HTML5 drop gives `File` objects, the bytes
and the names, with no way to know where any of them came from or to open them again later.

The window opts in with one property, `AcceptsFileDrops`, and AOTrino registers the OLE drop target. That part is in
the SDK rather than here, because a composition hosted WebView never receives an external drop of its own: the host
window is the only thing that can, which is also why WebView2 exposes `DragEnter` and `Drop` on the composition
controller for the host to forward to.

Three details are the difference between a demo and something that behaves:

* **This PC refuses the drop.** It is a list of drives, not a folder, so the cursor says no while the file is still
  in the air rather than after it lands.
* **Nothing is overwritten.** A collision becomes `report (2).txt`, the name Explorer would pick, which also makes
  dropping a file onto the folder it already lives in do the obvious thing.
* **The copies are selected and scrolled to.** A copy you cannot see is hard to tell apart from nothing happening.

## The navigation lock, from the other side

This window stays on the default `NavigationMode.Local`, and the sample uses that on purpose. The "AOTrino on GitHub"
link in the page is a real `<a href>` to a real site. Clicking it does not navigate this window, the lock cancels the
navigation and opens the link in the user's default browser instead.

Compare it with [Web Browser](../AOTrino.Samples.WebBrowser), which is the same machinery with the lock turned off.
That contrast is the point of having both samples.

Run it with `dotnet run` from this folder.
