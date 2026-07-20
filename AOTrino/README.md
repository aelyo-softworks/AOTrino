# AOTrino, the SDK

What the `AOTrino` assembly gives an app, and where to look for it. Not a reference of every member, a map: the
half dozen types you actually touch, and what each one is for.

The repo's [README](../README.md) is the introduction and the samples. [docs](../docs) covers the bridge, security,
theming, localization and the front end in prose. This is the shape of the library itself.

## The four things it does

An AOTrino app is a real Windows process that draws part of itself with HTML. Everything here follows from that:

| | |
| --- | --- |
| **A window** | A real HWND with a WebView in it, hosted as a composition visual so the page is one layer among the app's layers rather than an opaque rectangle. |
| **A bridge** | Ordinary C# objects callable from JavaScript, and a small runtime injected into every page for the things a page has no API for. |
| **Pixels** | A shared buffer for frames, because a picture is the one thing the bridge is the wrong tool for. |
| **An executable** | The front end embedded in the exe and unpacked at startup, so what ships is one file. |

## Starting up

```csharp
using var app = new AOTrinoApplication();
using var window = new MainWindow();
window.ResizeClient(1000, 700);
window.Center();
window.Show();
app.Run();
```

`AOTrinoApplication` owns what is true for the whole process:

* `WebRoot`, the embedded front end, and `Paths`, where it is unpacked.
* `WebView2Version` and `AOTrinoVersion`, and the check that closes the process with a download link when the
  WebView2 runtime is missing, rather than failing at the first navigation.
* `Trace`, `TraceInfo`, `TraceWarning` and friends, which is also where the page's own `console.log` arrives.
* `Current`, the running application.

## The window

`AOTrinoWindow` is what an app derives from. `WebViewWindow` underneath it owns the WebView and every piece of
plumbing that is not about hosting. The two hosting models are `CompositionWebViewWindow`, the default, and
`HwndWebViewWindow`, a classic child window.

The members an app usually touches:

| Member | For |
| --- | --- |
| `RegisterHostObjects()` | Where `AddHostObject` calls go, before the page loads. |
| `StartUrl`, `VirtualHostName` | Where the window opens, and whether the WebRoot is served over `https` instead of `file://`. |
| `NavigationMode` | `Local`, the default, keeps the window on the app's own content and hands off-app links to the user's browser. `Web` makes it a browser. |
| `Navigate`, `NavigateToString`, `NavigateToWebRootAsync` | Going somewhere. |
| `ExecuteScript<T>` | Running script in the page and getting a typed result back. |
| `AddStartupScript`, `AddStartupScriptResource` | Script injected into every document, before any of the page's own. |
| `AcceptsFileDrops`, `OnFilesDropped` | Files dragged from Explorer, with their real paths. |
| `SetSystemBackdrop` | Mica, Acrylic or Tabbed behind a page that leaves itself transparent. |
| `BeginDrag`, `MaximizeOrRestore`, `Close` | The window controls a page-drawn caption needs. |

Almost everything is `virtual`. `IsNavigationAllowed`, `OpenExternal`, `SetWindowTitleFromPage`,
`GetNavigationErrorPage`, `ConfigureSettings` and the rest exist so an app can change one decision without
reimplementing the window.

### Browser behaviour, off by default

WebView2 arrives with the behaviours of a browser, and most are wrong in an app window: the context menu offers
View source, Ctrl+R throws away the page's state. `AreDefaultContextMenusEnabled`, `IsStatusBarEnabled`,
`AreBrowserAcceleratorKeysEnabled` and `IsBuiltInErrorPageEnabled` are all off for an app window and back on for
`NavigationMode.Web`. `AreDevToolsEnabled` stays on, and F12 is handled by the window because turning the
accelerators off takes the key away with them. See [docs/FRONTEND.md](../docs/FRONTEND.md).

## The bridge

`DispatchObject` is the base of anything the page can call. Every public member becomes part of the JS API:

```csharp
protected override void RegisterHostObjects() => AddHostObject("app", new MyApi(this));
```

```js
const api = chrome.webview.hostObjects.app;    // every member is a promise, even a property read
const answer = await api.Add(2, 3);            // ...or hostObjects.sync.app to block
```

Properties read as properties, `Task<T>` arrives as a real promise, a thrown exception becomes a rejection, and
anything more complex crosses as JSON. `IsMemberVisible` filters what is exposed, and `SetCustomValue` adds members
that have no C# member behind them. `docs/BRIDGE.md` is the detail, `Samples\AOTrino.Samples.HostObjects` is the
same thing you can click.

The page also gets `window.__aotrino` with no work at all: window controls, the caption drag region, shared buffer
delivery. That runtime is injected, not shipped in your front end, so it cannot drift from the C# side.

## Pixels, not JSON

`SharedBuffer` is memory both sides can see, for the traffic the bridge should never carry. `EnsureSize` grows it,
`Pointer` is where to write, `Post` hands it to the page, and the page receives it as a zero-copy `ArrayBuffer`.

`Direct2DSurface` is that with the drawing attached: give it a `<canvas data-aotrino-surface="name">` and a draw
callback, and .NET renders with Direct2D on the GPU while the injected WebGL runtime uploads each frame.

```csharp
_surface = new Direct2DSurface(this, "scene");
_surface.StartAnimation(DrawScene);
```

`Samples\AOTrino.Samples.Direct2D` is the small version. `Samples\AOTrino.Samples.Blazor.DiskMap` is the argument:
tens of thousands of rectangles from a tree that is being modified while it draws.

## The front end in the exe

`WebRoot` extracts resources whose name starts with `WebRoot\` into a versioned folder and hands back
`IndexFilePath`. The cache key is a hash of the embedded content, so an incremental build that changes a page
re-extracts it without a version bump. `EnsureFavicon` writes a transparent icon when the front end ships none,
because Chromium asks every document for one.

Embedding is MSBuild's half, in `build\AOTrino.targets`, which NuGet imports into a consuming project:
`AOTrinoEmbedWebRoot` for a folder, `AOTrinoBlazorProject` for a Blazor WebAssembly front end.

## What only a native process knows

* `SystemInfo` is a ready-made host object reporting versions, machine, displays, adapters, culture and input.
  It is built but never registered, because exposing it is a per-window decision. Its `Values` is a JSON DOM you
  can add to or take from before handing it over.
* `Localization` keeps the app's strings in one `.resx` and hands the front end a catalog per culture, resolved
  against the ordered list of languages the user actually asked Windows for. See
  [docs/LOCALIZATION.md](../docs/LOCALIZATION.md).
* `CultureUtilities.GetUserPreferredUILanguages()` is that ordered list, which `navigator.language` cannot give.
* `EmbeddedResource.Load` keeps injected scripts in `.js` files rather than in C# string literals.

## Input

`Input\` carries the event args: mouse, pointer, keyboard, navigation, file drop. A composition hosted WebView
receives no OS input of its own, so the window forwards it, which is also why the host owns the drop target and
why `FileDropEventArgs` is where dropped paths arrive.

## Ground rules

**Everything is AOT.** Nothing reflects over your types at runtime, JSON goes through source-generated contexts,
and COM interop is source-generated too. If something here needs `[GeneratedComClass]`, so does the equivalent in
your app.

**Nothing is hidden behind a policy.** The navigation lock, the browser settings and the drop target are defaults,
each one a `virtual` an app can change. Where a decision is a security decision, `docs/SECURITY.md` says so and
leaves it to you.
