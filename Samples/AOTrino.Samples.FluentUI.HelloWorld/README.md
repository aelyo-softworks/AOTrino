# Fluent UI · Hello World

One window that reads the host, calls .NET, and re-themes itself from a sun and moon button in its own caption.

## What it shows

The whole npm pyramid at once, `@aotrino/client` for the typed bridge, `@aotrino/react` for the hooks, and
`@aotrino/fluent` for the look. It is a template choice, never a platform mandate, and the other samples here work
with no npm at all.

Almost none of the styling is the sample's own. Fluent's design tokens carry it, which is why the window matches what
Windows 11 looks like without a stylesheet trying to imitate it by hand.

Theming is the part worth reading. Light and dark follow Windows by default, because WebView2 maps the Windows app
theme onto the `prefers-color-scheme` media query, so a window changes with the system setting while it is running,
with nothing to subscribe to. The caption button then overrides that choice and remembers it.

`FluentApi.cs` is the host end, small on purpose: an environment variable, the framework description, an async
`GreetAsync`, and `Quit` to close the window from the page.

Run it with `dotnet run` from this folder.
