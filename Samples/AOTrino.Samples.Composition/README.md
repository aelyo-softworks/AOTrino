# Composition

The same page in two windows at once, hosted two different ways, so the difference between them is visible rather
than described.

## What it shows

A WebView can be hosted as **one visual in a Direct Composition tree**, which is the default with AOTrino, or as a **classic child HWND**.
Both windows open on the identical page. Only one of them has anything drawn over it:

* the composition window composites live .NET layers **on top of** the still fully interactive page, a soft glow that
  follows the OS mouse, a Direct2D glass HUD with a live CPU graph, and a translucent status bar with a pulsing dot.
* the HWND window shows none of it, because a child HWND is an opaque top most window inside its parent. Native code
  cannot draw over it, and nothing can be blended with it.

That is the whole reason AOTrino hosts the WebView as a Direct Composition visual by default. The page stops being a
rectangle the app has to work around, and becomes one layer among the layers the app is already drawing.

The HUD is worth a look on its own. It is a real Direct2D surface in the composition tree, drawn with the same device
the rest of the window uses, so the CPU graph and the text on it come from DirectWrite rather than from a canvas.

## Files worth reading

| File | What is in it |
| --- | --- |
| `CompositionShowcaseWindow.cs` | The visual tree, the glow, the HUD and the status bar. |
| `HwndShowcaseWindow.cs` | The same page with nothing over it, for the comparison. |

Run it with `dotnet run` from this folder. Both windows open together.
