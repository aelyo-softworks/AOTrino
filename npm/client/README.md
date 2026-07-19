# @aotrino/client

Typed access to the AOTrino host from the page: window controls, .NET host objects, shared buffers and messages.

This package **adds no runtime**. AOTrino's C# side already injects `window.__aotrino` into every page
(`WebViewWindow.EnsureSharedRuntime`), this is a typed facade over it plus the WebView2 host-object bridge.
That is deliberate, the runtime ships inside the executable, so there is nothing here that can drift out of
sync with it beyond the type shape.

It is **not published to npm**. Samples in this repo resolve it through npm workspaces, see
[docs/FRONTEND.md](../../docs/FRONTEND.md) for how it is consumed and eventually shipped.

## Host objects

Describe the JS-visible surface of your `DispatchObject` and get it typed. Member names cross the bridge
case-insensitively, so camelCase in TypeScript matches PascalCase in C#.

```ts
import { host } from "@aotrino/client";

interface DemoApi {
    machineName: string;            // C#: public string MachineName => ...
    ping(): string;                 // C#: public string Ping()
    add(a: number, b: number): number;
    echoAsync(text: string): Promise<string>;
    quit(): void;
}

const api = host<DemoApi>();        // "dotnet" by default

await api.ping();                   // Promise<string>
await api.add(2, 3);                // Promise<number>
await api.machineName;              // Promise<string>, property reads are async too
```

Every member is asynchronous through the bridge: methods return a `Promise`, and so do property reads.
`hostSync<T>()` gives the blocking proxy (`chrome.webview.hostObjects.sync`) where members behave like plain
JS, it stalls the page until .NET answers, so prefer `host<T>()`.

## Window controls

```tsx
import { appWindow } from "@aotrino/client";

<header data-aotrino-drag>
    My app
    <button data-aotrino-nodrag onClick={() => appWindow.close()}>✕</button>
</header>
```

`data-aotrino-drag` is handled by the injected runtime, so a region marked with it drags the window with no
handler of your own, and `data-aotrino-nodrag` keeps an interactive child inside it clickable. The same two
names are exported as `dragAttribute` / `dragExcludeAttribute` for programmatic use (`setAttribute`, spreads).

`appWindow.*` calls no-op outside AOTrino.

## Running in a plain browser

`npm run dev` serves the page in a normal browser where there is no host. `isHosted()` returns `false`
there, `appWindow.*` no-ops, and a `host<T>()` proxy throws on first *use* (not on creation), so a
module-scope `const api = host<DemoApi>()` stays safe:

```ts
import { isHosted } from "@aotrino/client";

if (!isHosted()) {
    // render mock data, keep HMR usable
}
```

## Shared buffers and messages

```ts
import { onBuffer, onMessage, post } from "@aotrino/client";

const stop = onBuffer("frame", (buffer, meta) => { /* .NET SharedBuffer, zero-copy */ });
onMessage<{ __aotrino: string }>(msg => { /* ICoreWebView2.PostWebMessageAsJson */ });
post({ hello: "from the page" });   // → WebViewWindow.WebMessageJsonReceived
```

Both subscriptions return an unsubscribe function.
