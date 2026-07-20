# Capture Screen

A split window. The left half is a web page, the right half is a live capture of the screen, and a draggable divider
sets where one ends and the other begins.

## What it shows

The right pane is `Windows.Graphics.Capture`, the same API the system's own recording tools use, rendered with
Direct2D onto a Direct Composition (a.k.a Visual Layer) surface. It is genuinely live, other windows moving on the desktop move in the pane.

A web page cannot do this. Screen capture in a browser exists only behind a permission prompt, gives back a stream rather
than frames to draw with, and never covers the whole desktop by default. Here it is an ordinary API call, because the
process is an ordinary Windows process that happens to display part of itself with HTML.

The layout is the point as much as the capture is. The page and the capture are **two Direct Composition visuals side by side in one
composition tree**, so the divider is not an iframe boundary or a CSS split, it is the app deciding how much of its
window each layer occupies. The page keeps its own layout inside its half and stays fully interactive.

## Files worth reading

| File | What is in it |
| --- | --- |
| `CaptureWindow.cs` | The visual tree, the capture session, the frame pool and the divider. |

Run it with `dotnet run` from this folder.
