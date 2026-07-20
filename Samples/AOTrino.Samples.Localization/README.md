# Localization

Every string in this window, the page and the caption alike, comes from a `.resx` compiled into the executable. A
combo switches between three languages, and nothing restarts.

## What it shows

An app with a web front end usually keeps its text in two places, a `.resx` for the host and a JSON catalog for the
page, and then spends its life keeping them in step. Here the .NET side owns every string and hands the page a
catalog, so a translation is added once.

The page's entire translation code is a lookup in an object it loaded once:

```js
function t(key) {
    return catalog[key] ?? key;
}
```

There is no call per string, on purpose. Every host object call is a promise, so a `t()` that crossed the bridge
could not be called from a render, and one that crossed it synchronously would stop the renderer once per string.
This crosses once per language change.

Two things are worth watching as you switch:

* **The window caption changes with the page.** It is read by .NET from the same resources, so the taskbar and
  Alt+Tab cannot end up in a different language from the window's contents.
* **The panel shows the negotiation.** "Windows asked for" is the ordered list of languages the user chose, and
  "Best match this app ships" is what could be served from it. A page cannot obtain that list.
  `navigator.language` gives the one locale the WebView started with, not the ordering and not the fallbacks, so
  serving someone in their second language rather than in the app's default needs the host.

## Files worth reading

| File | What is in it |
| --- | --- |
| `Strings.resx`, `Strings.fr.resx`, `Strings.de.resx` | The strings. Each translation becomes a satellite assembly, `fr\...resources.dll`. German is deliberately three keys of eight. |
| `Program.cs` | The one `Localization` the app uses, naming the resources and the cultures it ships. |
| `LocalizationApi.cs` | The four members the page can call, only one of which is the translation. |
| `WebRoot\dist\index.html` | The `t()` above, and the `data-t` attributes it fills. |

There is no plural rule or message format here. See [docs/LOCALIZATION.md](../../docs/LOCALIZATION.md) for why, and
for how to keep this single source of truth while handing the catalog to an i18n library that has the tables for it.

Run it with `dotnet run` from this folder. To see French without changing Windows, pick it in the combo.
