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
