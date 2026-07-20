# Front end

AOTrino does not care what renders your page. A hand-written `index.html` is a first-class citizen: most of the
samples are exactly that, with no `package.json` anywhere, and a whole application can be written in plain JS
against the globals below. Nothing here is required.

This document is about the optional layer for the other kind of app, the one you would otherwise write in
Electron with React and a component kit.

## What the page always has

AOTrino's C# side injects a runtime into every page before any of your scripts run
(`WebViewWindow.EnsureSharedRuntime`, from `AOTrino/Resources/SharedBuffer.Runtime.js`). It exposes
`window.__aotrino`: shared buffers, messages, and window controls. Alongside it, WebView2 exposes any host object
you registered as `chrome.webview.hostObjects.dotnet`.

Both are plain globals. You can use them directly, and a zero-build page should.

## The packages

| Package | Status | What it adds |
| --- | --- | --- |
| `@aotrino/client` | built (`npm/client`) | TypeScript types over `window.__aotrino` and the host-object bridge |
| `@aotrino/react` | built (`npm/react`) | headless hooks and components over the client, behaviour and class names, no CSS |
| `@aotrino/fluent` | built (`npm/fluent`) | a Fluent UI starter: system-themed provider + a Fluent caption. A template choice, never a platform mandate |

`@aotrino/client` deliberately **ships no runtime**. The C# side owns the runtime and it travels inside the
executable, the package is a typed facade over what is already on the page. Only the type shape can drift, not
behaviour.

The value is the typing. Without it, `chrome.webview.hostObjects.dotnet` is untyped and nothing checks a call
against the .NET member it lands on:

```ts
interface DemoApi {
    add(a: number, b: number): number;   // C#: public int Add(int a, int b)
}

const api = host<DemoApi>();
await api.add(2, "forty");               // error TS2345: string is not assignable to number
```

See [npm/client/README.md](../npm/client/README.md) for the API.

## Not published to npm

There is no `@aotrino` scope on any registry, and publishing is not planned. Two mechanisms replace it.

**Today, npm workspaces.** The repo-root `package.json` lists `npm/client` and each JS sample as workspaces.
`npm install` symlinks `node_modules/@aotrino/client` to `npm/client`, so a sample just declares
`"@aotrino/client": "*"` and imports it normally. No registry, and an edit to the client is visible to the sample
immediately.

**Outside the repo, tarballs in the NuGet.** `AOTrino.Templates` packs `npm pack`'s tarballs into the React and
Fluent templates, so a generated project's `package.json` asks for `"file:../packages/aotrino-client.tgz"` and
`npm install` resolves it offline. `npm pack` names its output after the version inside it
(`aotrino-client-1.0.0.tgz`), pack renames it, because a version in that path is a version every template's
`package.json` would have to be edited for on each release.

Not publishing is what keeps the versions honest: the JS and the runtime it wraps ship in the same artifact, so
they cannot be mismatched. (`npm/client` is marked `"private": true`, which blocks an accidental `npm publish`
while still allowing `npm pack`.)

## Building a generated WebRoot

A hand-written sample commits `WebRoot/dist` and MSBuild embeds it as-is. A Vite sample generates it, so the
project opts into one more flag:

```xml
<AOTrinoEmbedWebRoot>true</AOTrinoEmbedWebRoot>
<AOTrinoNpmBuild>true</AOTrinoNpmBuild>   <!-- WebRoot\dist is generated, build it first -->
```

`Directory.Build.targets` then runs `npm install` (first time only), rebuilds `@aotrino/client`, and runs
`npm run build` in `WebRoot` before embedding. Plain `dotnet build` and F5 in Visual Studio both work with no
extra step.

Two things matter in the Vite config, and both are load-bearing:

* `base: "./"`, the page is extracted to disk and loaded over `file://`, so asset URLs must be relative.
* `build.outDir: "dist"`, this is what `$(AOTrinoWebRootDir)` embeds.

`WebRoot/dist` is git-ignored for a generated front end, it is an output, not a source.

## Developing in a browser

`npm run dev` in `WebRoot` serves the page in a normal browser with HMR, where there is no .NET host. The client
degrades instead of exploding: `isHosted()` returns `false`, `appWindow.*` calls no-op, and a `host<T>()` proxy
throws only if something actually calls it, so a module-scope `const api = host<DemoApi>()` is safe. Branch on
`isHosted()` to render mock data.

## A Blazor WebAssembly front end

The front end does not have to be TypeScript. AOTrino can embed a Blazor WebAssembly app instead, so the UI is
C# too, and an app opts in with one property:

```xml
<PropertyGroup>
  <AOTrinoBlazorProject>..\MyApp.Wasm\MyApp.Wasm.csproj</AOTrinoBlazorProject>
</PropertyGroup>
```

`AOTrinoEmbedBlazor` in `AOTrino\build\AOTrino.targets` then restores that project, publishes it, and embeds its
`wwwroot` exactly as `AOTrinoEmbedWebRoot` embeds a folder. Nothing is copied into the source tree.
`Samples/AOTrino.Samples.Blazor.DiskMap` is the sample, with `...DiskMap.Wasm` as its front end.

**Be clear about the cost first.** The page carries a second .NET runtime, mono for wasm, on top of the native
AOT one in the host process, so the exe goes from about 11 MB to about 19 MB and startup is visibly slower than
the JS samples. It buys C# in the page. If that is not what you are after, the TypeScript levels stay the better
default, which is why this is a sample and not a fourth `@aotrino/*` package.

Four rules come with it, and each one is a real failure that took a while to read:

* **Keep the wasm project beside the host project, never inside it.** Nested, its sources fall into the host's
  default globs and the build stops on CS0579, a duplicate of every assembly attribute.
* **Give it its own empty `Directory.Build.props`.** MSBuild stops walking up when it finds one, which is what
  keeps this repo's windows target framework and Windows runtime identifiers away from a project that compiles to
  wasm. Without it the restore looks for `Microsoft.NETCore.App.Runtime.Mono.win-x64`, which does not exist.
* **Tell the solution not to build it**, `<Build Project="false" />` on it in the `.slnx`. The host builds it
  once, and two builds of it at once write one obj folder and fail on "the process cannot access the file ...
  blazor.build.boot-extension.json". Nothing is lost by this: the project still loads, still has full IntelliSense,
  and a Razor error still appears in the build output with its file, line and column, because the host compiles it
  while publishing. It is also why the target restores the project explicitly, since a project the solution does
  not build is one it does not restore, which ends in NETSDK1004.
* **Route on the file, not on the root.** WebView2 serves the virtual host from a folder with no directory index,
  so `https://myapp.example/` answers ERR_ACCESS_DENIED and the window opens `/index.html`. A page needs
  `@page "/index.html"` next to its `@page "/"`, or Blazor boots and then renders "Sorry, there's nothing at this
  address.", which reads exactly like a failure to load. A virtual host is required here in any case, since Blazor
  fetches its runtime from `_framework` and a `file://` page has an opaque origin.

### Browser behaviour in an app window

WebView2 arrives with the behaviours of a browser, because it is one, and most of them are wrong in an app window
where there is no address bar to explain them: Reload on a page that *is* the app reads as the app resetting, and
View source offers to show someone the front end of the program they are running. `WebViewWindow` therefore applies
app defaults once, before the first navigation, and each one is a separate virtual so a window can change it without
touching COM:

| Setting | Default | Why |
| --- | --- | --- |
| `AreDefaultContextMenusEnabled` | `false` | An app that wants a context menu almost always wants its own, in the page. |
| `IsStatusBarEnabled` | `false` | The link target strip over the bottom left corner belongs to a browser. |
| `AreBrowserAcceleratorKeysEnabled` | `false` | Ctrl+R and F5 lose the page's state and look like a crash. Editing keys, Ctrl+C and Ctrl+V, are untouched. |
| `IsBuiltInErrorPageEnabled` | `false` | AOTrino shows its own, see above. Leaving it on flashes Edge's page first. |
| `AreDevToolsEnabled` | `true` | Being able to open the tools on a window that misbehaves is worth more during development than hiding them is worth in a shipped app. Set it to `false` when you ship. |

`AOTrinoWindow` turns every one of them back on in `NavigationMode.Web`, since a mini browser without a context menu,
a status bar or Ctrl+R is not a browser. Anything not listed, zoom, pinch zoom, swipe navigation, autofill, script
dialogs, is reachable through the `ConfigureSettings(settings)` override, which receives the same settings object.

### Files dropped from Explorer

A window opts in with one property, and gets the paths:

```csharp
protected override bool AcceptsFileDrops => true;

protected override void OnFilesDropped(FileDropEventArgs e)
{
    foreach (var path in e.Paths) { ... }
}
```

It is off by default, because registering a drop target changes what the cursor does over the whole window and a
window that would ignore a drop should not invite one. `GetFileDropEffect` decides what the drag is offered, so a
window can refuse a drop it cannot honour and say so while the file is still in the air. `FilesDropped` is the same
thing as an event for code that would rather subscribe than override.

What arrives is **real paths**. An HTML5 drop in the page gives `File` objects, the bytes and the names, with no way
to know where anything came from or to open it again later, and that is as far as a browser can go.

The drop target belongs to the host window rather than to the page for a reason worth knowing: a composition hosted
WebView has no window of its own to drop onto, which is why WebView2 exposes `DragEnter`, `DragOver`, `DragLeave` and
`Drop` on the composition controller. The host is expected to own the drop target, and may forward to the page.
So this is not competing with the page for drops, it is the only thing that can receive one.

Dragging the other way, out of the window and into Explorer, is an app decision rather than an SDK one, since what
you drag is yours. `Samples\AOTrino.Samples.FileExplorer` does it in about ninety lines with `SHCreateDataObject`
and `SHDoDragDrop`.

### The favicon

Chromium asks every document it loads for `/favicon.ico`, whether or not the document mentions one, so a front end
with no icon used to log `Failed to load resource: net::ERR_FILE_NOT_FOUND ... /favicon.ico` in the console of every
window, for a file nobody asked for and no page can avoid being asked for.

AOTrino writes a fully transparent 16x16 `favicon.ico` into the extracted WebRoot when the front end does not ship
one, so the request is answered instead of failing. The default only fills the gap, since an app's icon is the app's
to choose. Override `WebRoot.EnsureFavicon` to change that.

**Using your own.** Put a `favicon.ico` where your front end's build leaves it at the root of `dist`, beside
`index.html`, and it is used untouched: AOTrino only writes the default when that file is not already there. Nothing
has to be declared, in the page or in the project, because everything under the WebRoot is embedded as it stands.
Where to put it depends on how the front end is built:

| Front end | Where the icon goes |
| --- | --- |
| Hand-written pages | `WebRoot\dist\favicon.ico`, next to `index.html`. |
| npm and Vite | `WebRoot\public\favicon.ico`. Vite copies its public folder to the root of `dist` on build. |
| Blazor WebAssembly | `wwwroot\favicon.ico` in the front end project. Publishing copies it, and the published `wwwroot` is what gets embedded. |

**Any name or format.** `/favicon.ico` is only Chromium's fallback for a page that declares nothing. Declare one and
that is used instead, in any format the browser understands, and the request for `/favicon.ico` is never made:

```html
<link rel="icon" href="logo.svg" />
```

**This is not the window icon.** The favicon belongs to the page, and shows in the developer tools and in any browser
UI. The icon of the window, of the taskbar button and of the executable in Explorer, comes from the `ApplicationIcon`
of the project, which `Directory.Build.targets` in this repo defaults to `AOTrino.ico` for every executable.
Setting one does nothing for the other.

Intercepting the request instead does not work, and it is worth writing down why: a WebRoot served through
`SetVirtualHostNameToFolderMapping` is served inside WebView2 and raises no `WebResourceRequested` at all, not for
the favicon and not for the document either, so there is nothing to answer. A file on disk is served either way,
over `file://` as well as over the virtual host.

### When the front end does not load

A failed navigation used to leave the WebView showing the browser's own failure page, which is written for someone
browsing the web: it says a site cannot be reached and offers to retry. For content embedded in the executable both
halves are misleading, there is no site and retrying cannot help, and `ERR_FILE_NOT_FOUND` on a page nobody typed
reads as a broken app rather than as the missing content it nearly always is.

AOTrino now replaces it with its own page, `AOTrino\Resources\NavigationError.html`, which names the address, gives
the reason in a sentence, and lists what causes it in an AOTrino app. Two hooks on `WebViewWindow` control this:
`ReplacesNavigationErrorPage`, which `AOTrinoWindow` already turns off in `NavigationMode.Web` so a window that
browses the real web keeps the browser's page, and `GetNavigationErrorPage`, to word it differently or return a page
of your own. Cancelled navigations are not treated as failures, so `NavigationMode.Local` handing a link to the
default browser, and a link that turns into a download, both stay silent.

### Editing the front end and pressing F5

Because the front end is embedded at build time, an edit to it only reaches the window through a build of the host.
Visual Studio decides whether to build a project before MSBuild is ever asked, with its fast up-to-date check, and
that check only looks at the project's own files. The front end belongs to another project, one the solution is
told not to build, so a change to a stylesheet or a page marked nothing as out of date: F5 relaunched the previous
exe with the previous front end still inside it, and the edit looked as though it had been ignored.

`AOTrino.targets` lists the front end project's files as `UpToDateCheckInput` of the host, so that check sees them
and the host rebuilds. Editing the front end and pressing F5 is enough, with no rebuild by hand in between.
The cost is that a change there republishes the front end, so that F5 is slower than one after touching nothing.

## The samples

React samples are named `AOTrino.Samples.React.*`, and the repo-root `workspaces` glob
(`Samples/AOTrino.Samples.React.*/WebRoot`) picks up a new one with no edit.

Both React samples use `@aotrino/react`, because in a React app that is the recommended path. The raw
`window.__aotrino` / `chrome.webview.hostObjects` surface is still demonstrated, by the samples that aren't
React at all (`HelloWorld`, `HostObjects`, `FileExplorer`), which use it directly with no npm anywhere.

`Samples/AOTrino.Samples.React.HelloWorld` is the minimal one: a `TitleBar`, the four host properties, and a
typed call for each shape the bridge supports (property, sync method, async method).

`Samples/AOTrino.Samples.React.Dashboard` is the fuller tour: live .NET process state (uptime, working set,
managed heap, collections, threads) with manual `refresh()` and auto-refresh, alongside a slow call
(`pending`) and a throwing one (`error`). Same split as `HelloWorld` vs `HostObjects` on the C# side.

Fluent UI samples are named **`AOTrino.Samples.FluentUI.*`** rather than `React.*`: Fluent implies React, so
saying both is noise. The repo-root `workspaces` globs cover both families, so a new sample in either needs no
edit.

`Samples/AOTrino.Samples.FluentUI.HelloWorld` is the whole pyramid in one window: the client types the bridge,
the react hooks supply the data and the caption gesture, and `@aotrino/fluent` dresses it, light and dark
following Windows, pickable from the caption and remembered, with six lines of app CSS. See
[THEMING.md](THEMING.md).

**`Samples/AOTrino.Samples.FluentUI.Gallery` is the flagship.** Every other sample is one idea in isolation;
the gallery is all of them in one window, seven pages deep:

* **Controls**, Fluent UI itself, in five categories (Basics, Inputs, Collections, Feedback, Surfaces).
  None of it is AOTrino, which is the point: the shell is native and small, and the widgets are the web's.
* **Window**, caption, drag, double-click to maximize, DWM backdrops.
* **Bridge**, one card per shape: properties, arrays, JSON, async, exceptions, .NET→JS push.
* **Theming**, **System**, **Security**, following Windows, what only Windows knows, and the model
  demonstrated rather than asserted.

Every card is a live demo with the code that produced it behind a *Show code* button, in the spirit of
WinUI-Gallery's `ControlExample`. It's the one to open first, and the one that keeps these docs honest: if a
claim here isn't demonstrable there, the claim is what's wrong.

### Serve it from a virtual host

Vite emits the app as an ES module. Chromium gives a `file://` page an opaque origin and CORS-blocks module
scripts, so a bundler-built front end renders **blank with no visible error**, the hand-written samples never
hit this because their scripts are classic, not modules.

The fix is one line, and it is the only thing a React sample needs beyond the two MSBuild flags:

```csharp
// .example is reserved (RFC 2606), so it can never collide with a real domain
protected override string? VirtualHostName => "aotrino.example";
```

The WebRoot is then served under `https://aotrino.example/` instead of read off disk, the page gets an ordinary
https origin, and the module loads. No browser security flag is involved: in particular this does **not** need
`--allow-file-access-from-files`, and the page cannot read arbitrary local files. `Local` mode allows the app's
own virtual host and still hands every other origin to the real browser.

The trade-off (throughput vs. origin) and when to prefer `file://` instead are in
[SECURITY.md](SECURITY.md#how-your-content-is-served-file-or-a-virtual-host).
