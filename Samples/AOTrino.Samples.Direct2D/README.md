# Direct2D

An animated scene drawn by .NET on the GPU and displayed in a `<canvas>`, with **no copy between the two**.

The whole app is two lines in `MainWindow.cs`:

```csharp
_surface = new Direct2DSurface(this, "scene");
_surface.StartAnimation(DrawScene);
```

and one element in the page, `<canvas data-aotrino-surface="scene">`.

## What it shows

This is the path for pixels, which the bridge is the wrong tool for. Serializing a frame as JSON sixty times a second
is not a thing anyone should do, so frames do not go through the bridge at all:

* .NET draws the scene with Direct2D onto a GPU render target, using the window's own device.
* the result is copied into a **shared buffer**, which is a block of memory both processes can see rather than a copy
  handed from one to the other.
* the injected WebGL runtime uploads that memory straight to a texture on every animation frame, and the fragment
  shader swizzles BGRA to RGBA on the GPU for free.
* the canvas reports its own size back, so .NET always renders at the display resolution, on a scaled monitor too.

`DrawScene` is an ordinary method taking a Direct2D render target, a width, a height and elapsed seconds. Everything
Direct2D can draw is available to it, which is the whole of Windows' 2D graphics stack, including DirectWrite for text
and the effect pipeline.

The same `Direct2DSurface` powers the treemap in [Blazor DiskMap](../AOTrino.Samples.Blazor.DiskMap), where it draws
tens of thousands of rectangles from a tree that is being modified while it renders.

Run it with `dotnet run` from this folder.
