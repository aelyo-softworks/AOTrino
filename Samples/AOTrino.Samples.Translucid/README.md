# Translucid

A window whose border is translucent, with a Windows 11 system backdrop showing through it, while the centre pane
stays opaque. Buttons in the page switch the material live.

## What it shows

Mica, Acrylic and Tabbed are the materials Windows 11 draws behind a window, and they are what makes an app look like
it belongs to the system rather than like a page in a frame. This window asks for one with a single call:

```csharp
SetSystemBackdrop(DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW);
```

A backdrop only appears where the window paints nothing, so two things have to be true at once, and both are why this
works here:

* the WebView renders with a transparent background, so the material shows wherever the page leaves itself
  transparent, which is the border in this sample.
* the WebView is a composition visual rather than an opaque child window. An opaque one would cover the material
  completely, and there would be nothing to see.

The four buttons post a message to the host, which switches the material on the live window with no reload and no
flicker. There is no CSS that can do this, the effect is drawn by the desktop compositor behind the window, from your
wallpaper and from whatever is behind it.

Run it with `dotnet run` from this folder. It needs Windows 11.
