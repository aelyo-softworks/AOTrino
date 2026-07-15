# @aotrino/fluent

A [Fluent UI](https://react.fluentui.dev/) starter for AOTrino apps, over
[`@aotrino/react`](../react/README.md).

**This is a template choice, not a platform mandate.** AOTrino has no opinion about your front end: plain HTML
is a first-class citizen, and the other floors of the stack (`@aotrino/client`, `@aotrino/react`) are
design-system agnostic on purpose. This package is the opposite by design — it *is* the opinion, so that the
common "I want a Windows-looking app and I don't want to think about it" case is a few lines. If Fluent isn't
your kit, take `@aotrino/react` and dress it yourself; nothing below is load-bearing.

Not published to npm; samples resolve it through npm workspaces. See [docs/FRONTEND.md](../../docs/FRONTEND.md).

`@aotrino/client`, `@aotrino/react`, `@fluentui/react-components`, `@fluentui/react-icons` and `react` are peer
dependencies. Fluent is a large dependency: it takes the sample's bundle from ~190 kB to ~570 kB (~165 kB
gzipped). That's the price of the kit, and it's why this is a separate package. The sample raises Vite's
`chunkSizeWarningLimit` accordingly — that 500 kB default warns about download time on the web, and this
bundle ships inside the executable.

## A whole app

```tsx
import { AOTrinoProvider, TitleBar } from "@aotrino/fluent";

export function App() {
    return (
        <AOTrinoProvider>
            <TitleBar title="My app" onClose={() => void api.quit()} />
            <MyContent />
        </AOTrinoProvider>
    );
}
```

That is a themed, draggable, Windows-looking window with working minimize/maximize/close and no CSS of your
own.

## AOTrinoProvider

`FluentProvider` wired to the **Windows app theme**, and sized like a window rather than a document
(`height: 100vh`, column flex, no page scroll — so a caption stays put and content scrolls under it).

The theme follows the OS live: WebView2 maps the Windows setting onto `prefers-color-scheme`, so there's no
host object and no .NET involved, and flipping the theme in Windows Settings updates the running app. Pin one
instead if your app has its own switch:

```tsx
import { webDarkTheme } from "@fluentui/react-components";

<AOTrinoProvider theme={webDarkTheme}>…</AOTrinoProvider>
```

`useSystemTheme()` returns the matching Fluent theme, and `useSystemThemeName()` returns `"light" | "dark"` if
you just want to branch on it.

## TitleBar

```tsx
<TitleBar title="My app" showMinimize showMaximize onClose={() => void api.quit()} />
```

A Windows-looking caption built from Fluent parts — Fluent buttons, Fluent icons, Fluent tokens — so it
follows the theme, red close button included. Unlike `@aotrino/react`'s headless `TitleBar`, this one shows all
three window buttons by default: it stands in for the real caption.

It does **not** reimplement the gesture. The drag region and double-click-to-maximize come from
`@aotrino/react`'s `useDragRegion()`, which is precisely why that hook exists — the behaviour is subtle
(no `dblclick` ever fires on a drag region) and lives in exactly one place.

Accessible names are overridable, since they're user-visible text:

```tsx
<TitleBar labels={{ minimize: "Réduire", maximize: "Agrandir", close: "Fermer" }} />
```
