# Theming

Every `AOTrino.Samples.FluentUI.*` app ships light and dark, follows Windows by default, lets the user
override it from the caption, and remembers the choice. This page is what that costs and how to change it.

None of it is a platform feature. AOTrino itself has no opinion about colour — this all lives in
[`@aotrino/fluent`](../npm/fluent/README.md), the opinionated floor of the stack. A hand-written page or an
app on `@aotrino/react` themes itself however it likes.

## What an app writes

```tsx
import { AOTrinoProvider, TitleBar } from "@aotrino/fluent";

<AOTrinoProvider>
    <TitleBar title="My app" onClose={() => void api.quit()} />
    <MyContent />
</AOTrinoProvider>
```

That is the whole thing: light and dark, following the OS, a picker in the caption, and the choice remembered
across restarts. There is no theme state to own, no CSS variables to wire, no `useEffect`.

## The three states

The picker sits **left of the window buttons** — the sun/moon button before minimize — and offers:

| Choice | Behaviour |
| --- | --- |
| **System** *(default)* | Follows the Windows app theme, live. Change it in Settings → Personalization → Colors and the running app switches. |
| **Light** | Pinned to `webLightTheme`, regardless of Windows. |
| **Dark** | Pinned to `webDarkTheme`, regardless of Windows. |

The button's icon reflects what is *applied* (sun or moon), not what was picked, so "System" still tells you
at a glance which way it resolved.

## How following Windows works

WebView2 maps the Windows app theme onto the CSS `prefers-color-scheme` media query. So the whole mechanism is
`matchMedia("(prefers-color-scheme: dark)")` plus a `change` listener — **no host object, no .NET, no polling**.
That is `useSystemThemeName()`, and `useSystemTheme()` returns the matching Fluent theme if you want it
directly.

Because it's a media query rather than a value read once at startup, it tracks the user changing the setting
while the app runs.

## How the choice is remembered

`localStorage`, under `aotrino.theme`.

This is one of the places where [`AOTrinoWindow.VirtualHostName`](SECURITY.md#how-your-content-is-served-file-or-a-virtual-host)
earns its keep: storage is keyed by origin, and a `file://` page has an **opaque** origin, so the choice would
not reliably survive a restart. Served from a virtual host the page has a real `https` origin and storage
behaves normally. Every `FluentUI` sample sets `VirtualHostName` — it has to anyway, for its ES modules.

Storage access is wrapped in `try`/`catch` throughout: a theme is never worth crashing an app over.

```tsx
<AOTrinoProvider storageKey="myapp.theme">   // somewhere else
<AOTrinoProvider storageKey={null}>          // don't remember
```

## Offering other themes

Light and dark are the default because they're what Windows itself offers, but any Fluent `Theme` works. Fluent
ships several (`teamsLightTheme`, `teamsDarkTheme`, `teamsHighContrastTheme`, …) and `createLightTheme` /
`createDarkTheme` build one from a brand ramp.

```tsx
import { teamsHighContrastTheme, webDarkTheme, webLightTheme } from "@fluentui/react-components";
import { AOTrinoProvider } from "@aotrino/fluent";

const themes = [
    { key: "light", label: "Light", theme: webLightTheme },
    { key: "dark", label: "Dark", theme: webDarkTheme, isDark: true },
    { key: "contrast", label: "High contrast", theme: teamsHighContrastTheme, isDark: true },
];

<AOTrinoProvider themes={themes}>…</AOTrinoProvider>
```

The list drives the menu in order, and `key` is what gets stored. `isDark` does two things: it picks the
caption icon, and it's how **System** resolves — the provider matches the OS light/dark against your list, so
system-following keeps working with a custom set as long as one entry is light and one is dark.

`label` is user-visible text, which is why it lives in your list rather than inside the package: an app that
ships in French passes French labels. The picker's own two strings go the same way:

```tsx
<TitleBar labels={{ theme: "Thème", system: "Système" }} />
```

## Pinning, and opting out

```tsx
<AOTrinoProvider theme={webDarkTheme}>…</AOTrinoProvider>   // one theme, picker hides itself
<TitleBar showThemePicker={false} />                        // keep the choice, hide the button
```

Pinning a theme empties `options`, and the caption hides the picker rather than showing a menu with nothing to
choose. Same if there's no `AOTrinoProvider` above it at all — `useAOTrinoTheme()` returns `null` and the
caption simply renders without the button.

## Reading the theme yourself

```tsx
import { useAOTrinoTheme } from "@aotrino/fluent";

const theme = useAOTrinoTheme();
theme?.choice;            // "system" | "light" | "dark" | your key
theme?.resolved.label;    // what's actually applied
theme?.resolved.isDark;
theme?.setChoice("dark");
```

Useful for a settings page that offers the same choice somewhere roomier than a 46px button.
