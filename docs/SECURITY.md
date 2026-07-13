# AOTrino security model

AOTrino is a thin platform: a native window hosting a WebView2 that renders your app. That means the same
question every embedded-browser framework faces applies here — **what is the web content allowed to touch, and
where is it allowed to go?**

AOTrino answers this with *one* honest, enforced default and otherwise gets out of your way. There is no
permission taxonomy, no capability manifest, no "security zones." The whole model fits on this page.

## The one rule

> An AOTrino window stays on **your app's own content** and will not navigate to the web unless you say so.

That is the entire security guarantee AOTrino makes for you. Everything else — disabling web security, granting
filesystem access, exposing host objects — is a decision *you* make explicitly, in your own code, by name.

This is deliberate. A platform that promises broad "safety" writes a check it can't cash; the first time
something goes wrong, "but the framework said it was safe" is the framework's problem. AOTrino instead makes the
**safe thing the default** and the **dangerous thing an explicit, clearly-named opt-in**, so that responsibility
lands where the decision was actually made.

## Why this rule is the one that matters

The real danger in any WebView-hosting app is never a single setting. It's a *combination*:

> **disabled web security (or elevated local privileges) × attacker-controlled content loaded into the WebView**

Neither half is dangerous alone:

- A local-only app can disable web security for performance (direct `file://` access, no CORS) and be perfectly
  safe — because it only ever loads content *you wrote and shipped*.
- An app that browses the web is fine — as long as web security stays on, so remote pages are sandboxed like any
  browser tab.

The catastrophe is mixing them: giving a **remote, untrusted page** the run of the **local machine**. So the one
thing AOTrino guards by default is exactly the join between those two worlds: **navigation.** Keep untrusted
content from ever loading into a privileged window and the combination can't occur.

## `NavigationMode`

Every `AOTrinoWindow` exposes a single knob:

```csharp
public NavigationMode NavigationMode { get; set; } = NavigationMode.Local;
```

| Mode | Behavior |
|---|---|
| `Local` *(default)* | Only the app's own content (`file://`) and in-page schemes (`about`, `data`, `blob`) load in the window. Any off-app navigation (e.g. an `https://` link) is **cancelled** and handed to the user's default browser. |
| `Web` | The window is a browser: navigation to any origin is allowed. |

`NavigationMode` governs **navigation only**. It does *not* touch web security, file access, or host-object
exposure, and it makes no claim to. The name says exactly what it does and nothing more.

### The escape hatches

Two overridable members, both invoked by name in *your* code:

```csharp
// custom allow-list: Local, but permit one specific service origin
protected override bool IsNavigationAllowed(Uri uri) =>
    base.IsNavigationAllowed(uri) || uri.Host == "maps.myservice.com";

// change how a blocked navigation is handed off (default: OS default handler / real browser)
protected override void OpenExternal(Uri uri) => /* ... */;
```

- **Whole-window browser:** set `NavigationMode = NavigationMode.Web`.
- **Local + a trusted origin:** override `IsNavigationAllowed`.
- **Custom handoff:** override `OpenExternal`.

The default `OnNavigationStarting` respects any cancel your own `NavigationStarting` handler already set, then
applies `IsNavigationAllowed`; blocked navigations are cancelled and passed to `OpenExternal`.

## What AOTrino does *not* own

AOTrino intentionally does **not** decide these for you — they stay explicit, in your window/app code:

- **Web security / CORS.** If you want the local-content performance path
  (`--disable-web-security --allow-file-access-from-files`), set it yourself by overriding
  `GetEnvironmentOptions`. It stays a visible line in your code, not a hidden platform default.
- **Filesystem access.** Reading arbitrary paths for performance is a legitimate need for a local app; AOTrino
  neither grants nor blocks it. Expose it through a host object if and when your app needs it.
- **Host objects (the JS ↔ .NET bridge).** Nothing is reachable from JS that you did not explicitly register
  (`AddHostObject`). The bridge is deny-all by default simply because it's empty until you fill it. If you host
  untrusted content, register nothing sensitive on it — or don't host it in a privileged window at all.

## The footgun, spelled out

Do **not** combine, in the same window:

- disabled web security / `--allow-file-access-from-files` (via your `GetEnvironmentOptions`), **and**
- content you don't fully control — i.e. `NavigationMode.Web`, or a `Local` allow-list that admits remote
  origins, or a bundled page that pulls in third-party script.

That combination hands the local machine to code you didn't write. If you need both a privileged local surface
*and* a web browser, use **two windows**: a `Local` window (privileged, your content only) and a separate `Web`
window (sandboxed, web security on).

## Recommended configurations

| App shape | `NavigationMode` | Web security | Notes |
|---|---|---|---|
| Local app, bundled content only (the common case) | `Local` | your choice — safe to disable for perf | This is what the samples do. |
| Local app that also opens external links | `Local` | on, or off for local perf | Links open in the real browser automatically. |
| Local app calling one trusted remote service | `Local` + `IsNavigationAllowed` allow-list | **on** | Never disable web security here. |
| A web browser | `Web` | **on** | Treat it like any browser tab. |
| Both privileged-local *and* web | two windows | per-window | Do not merge them. |

## Summary

AOTrino makes one promise and keeps it in code: **local-first, no wandering onto the web by default.** Every
capability beyond that is yours to grant, explicitly and by name. That keeps the platform small, keeps the safe
path the default path, and keeps responsibility where the decisions are actually made — with the app developer,
with their eyes open.
