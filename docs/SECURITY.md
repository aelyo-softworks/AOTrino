# AOTrino security model

AOTrino is a thin platform: a native window hosting a WebView2 that renders your app. That means the same
question every embedded-browser framework faces applies here, **what is the web content allowed to touch, and
where is it allowed to go?**

AOTrino answers it with *one* conservative default and otherwise gets out of your way. There is no permission
taxonomy, no capability manifest, no "security zones." The whole model fits on this page.

## The default

> An AOTrino window is set up to stay on **your app's own content**, and won't navigate to the web unless you
> say so.

That's the extent of it. Everything else, disabling web security, granting filesystem access, exposing host
objects, is a decision *you* make explicitly, in your own code, by name.

This is deliberate, and it's a design stance rather than a promise. A platform that claims broad "safety" writes
a check it can't cash, the first time something goes wrong, "but the framework said it was safe" helps nobody.
AOTrino tries instead to make the **cautious thing the default** and the **risky thing an explicit,
clearly-named opt-in**, so the decision is visible where it's actually made. What your app then does with that
is yours to review, see the [licence](../LICENSE): this is free software, provided as is, with no warranty of
any kind.

## Why this rule is the one that matters

The real danger in any WebView-hosting app is never a single setting. It's a *combination*:

> **disabled web security (or elevated local privileges) × attacker-controlled content loaded into the WebView**

Neither half is dangerous alone:

* A local-only app can disable web security for performance (direct `file://` access, no CORS) and be perfectly
  safe, because it only ever loads content *you wrote and shipped*.
* An app that browses the web is fine, as long as web security stays on, so remote pages are sandboxed like any
  browser tab.

The catastrophe is mixing them: giving a **remote, untrusted page** the run of the **local machine**. So the one
thing AOTrino defaults to holding shut is exactly the join between those two worlds: **navigation.** Keep
untrusted content out of a privileged window and that combination shouldn't arise.

## `NavigationMode`

Every `AOTrinoWindow` exposes a single knob:

```csharp
public NavigationMode NavigationMode { get; set; } = NavigationMode.Local;
```

| Mode | Behavior |
|---|---|
| `Local` *(default)* | Only the app's own content (`file://`, or its `VirtualHostName` if set) and in-page schemes (`about`, `data`, `blob`) load in the window. Any off-app navigation (e.g. an `https://` link) is **cancelled** and handed to the user's default browser. The page may rename the window. |
| `Web` | The window is a browser: navigation to any origin is allowed, and the page may **not** rename the window. |

`NavigationMode` governs **navigation**, and one thing that follows from it: whether the page may rename the
window (`setWindowTitle` → `WebViewWindow.Text` → the taskbar, Alt-Tab, thumbnails). That isn't scope creep,
the mode's whole subject is *whose content is in this window*, and the answer decides whether a caption drawn
in HTML is the app naming itself or a stranger naming your app. A `Web` page still has `document.title`, like
any browser tab.

Past that, it does *not* touch web security, file access, or host-object exposure, and it makes no claim to.

### The escape hatches

Three overridable members, all invoked by name in *your* code:

```csharp
// custom allow-list: Local, but permit one specific service origin
protected override bool IsNavigationAllowed(Uri uri) =>
    base.IsNavigationAllowed(uri) || uri.Host == "maps.myservice.com";

// change how a blocked navigation is handed off (default: OS default handler / real browser)
protected override void OpenExternal(Uri uri) => /* ... */;

// who may name this window (default: anyone but a Web-mode page)
protected override void SetWindowTitleFromPage(string? title) => /* ... */;
```

* **Whole-window browser:** set `NavigationMode = NavigationMode.Web`.
* **Local + a trusted origin:** override `IsNavigationAllowed`.
* **Custom handoff:** override `OpenExternal`.
* **A window whose name must not move:** override `SetWindowTitleFromPage` to do nothing.

The default `OnNavigationStarting` respects any cancel your own `NavigationStarting` handler already set, then
applies `IsNavigationAllowed`; blocked navigations are cancelled and passed to `OpenExternal`.

## How your content is served: `file://` or a virtual host

There are two ways to get your own front end in front of the WebView, and the choice has both a security and a
performance consequence. It is a real trade-off, not a right answer.

**`file://` (the default).** The extracted WebRoot is loaded straight off disk. Reads go directly to the
filesystem, which is the fastest path available. The catch is that Chromium gives every `file://` document an
**opaque origin**: it cannot read any other local file, and, the part that bites, module scripts are
CORS-blocked, so a bundler-built front end (Vite, and anything else emitting `<script type="module">`) renders
**blank with no visible error**. The usual fix is to opt into `--allow-file-access-from-files` via
`GetEnvironmentOptions`, which collapses all `file://` URLs into a *single shared origin*. That flag is what
makes direct local reads work, and it also means any script executing in your page can read any file the user
can read, and send it anywhere.

**A virtual host (`VirtualHostName`).** Set it and the WebRoot folder is served to the WebView under
`https://<name>/` instead:

```csharp
// .example is reserved (RFC 2606), so it can never collide with a real domain
protected override string? VirtualHostName => "myapp.example";
```

The page then has an ordinary `https` origin. Module scripts load with **no browser flag at all**, and the page
is not handed the run of the disk, the capability isn't granted in the first place, rather than granted and
hoped about. Only
the mapped folder is exposed, and `DENY_CORS` keeps other origins out of it. `Local` mode allows the app's own
virtual host and still hands everything else to the real browser.

The cost is throughput: those requests are served through the host mapping rather than read straight from disk,
so each one carries more overhead than a direct `file://` read.

| Your app | Serve with | Why |
|---|---|---|
| A bundler-built front end (React/Vite/etc.) | **`VirtualHostName`** | Modules need a real origin. The bundle is loaded once at startup, so the per-request cost is irrelevant, and you avoid the flag entirely. |
| A hand-written page, no modules | `file://` *(default)* | Nothing to fix. `file://` is fine and fastest. |
| High-throughput local file access (a file browser, a viewer streaming documents or media off disk) | `file://` + `--allow-file-access-from-files` | Direct disk reads are the point, routing them through a virtual host would tax every read. Accept the flag knowingly, and keep the window `Local`. |

The short version: a virtual host suits serving your app's own bundle, and is the wrong tool for bulk local file
access. Reach for the flag when you actually need the speed, and then don't pair it with untrusted content
(see below).

## What AOTrino does *not* own

AOTrino intentionally does **not** decide these for you, they stay explicit, in your window/app code:

* **Web security / CORS.** If you want the local-content performance path
  (`--disable-web-security --allow-file-access-from-files`), set it yourself by overriding
  `GetEnvironmentOptions`. It stays a visible line in your code, not a hidden platform default. Before reaching
  for it, check whether `VirtualHostName` gets you there with no flag, for serving your own front end it
  usually does.
* **Filesystem access.** Reading arbitrary paths for performance is a legitimate need for a local app, AOTrino
  neither grants nor blocks it. Expose it through a host object if and when your app needs it.
* **Host objects (the JS ↔ .NET bridge).** Nothing is reachable from JS that you did not explicitly register
  (`AddHostObject`). The bridge is deny-all by default simply because it's empty until you fill it. If you host
  untrusted content, register nothing sensitive on it, or don't host it in a privileged window at all. See
  the next section: that last sentence is not a style note.
* **The window's name, past the default.** A `Local` page can rename its own window,
  `window.__aotrino.setWindowTitle()`, which is `WebViewWindow.Text` and therefore the taskbar, Alt-Tab and the
  thumbnails. That exists so a page drawing its own caption can't end up disagreeing with Windows about what the
  window is called. It follows `NavigationMode` on its own (see below), but if your `Local` allow-list admits an
  origin you'd rather not let name your window, `SetWindowTitleFromPage` is yours to override.

## The WebView2 runtime: evergreen (default) vs pinned

By default an AOTrino app uses the **evergreen** WebView2 runtime, the shared Chromium runtime already installed
on Windows, which Microsoft keeps patched. Two consequences, and the second is the one that matters here: the app
stays small (no browser to ship), and **it inherits Chromium security fixes for free**, a CVE in the engine is
Microsoft's to patch on the machine, not yours to chase.

The trade is that you don't own the version. Evergreen updates on its own schedule, so in principle a future
Chromium could change a rendering detail under your app. In practice this is rare, but for a kiosk, digital
signage, an offline/air-gapped deployment, or anywhere you need every install to render *identically*, you may
want to pin the engine instead.

WebView2 supports that with **Fixed Version** distribution: you ship a specific Chromium build with the app and
point AOTrino at its folder.

```csharp
// pin the engine: a Fixed Version runtime shipped next to the app, so the browser can't change under it
var app = new AOTrinoApplication(browserExecutableFolder: Path.Combine(AppContext.BaseDirectory, "WebView2Runtime"));
```

Pass it to the constructor, as above, so the startup runtime check validates the *pinned* runtime, this matters
on a machine that has your bundled runtime but no evergreen one (the air-gapped case), where an evergreen-only
check would wrongly refuse to start. The `AOTrinoApplication.BrowserExecutableFolder` property is also settable,
but a value set there *after* construction is used by the window and missed by that startup check, so the
constructor is the one to reach for. For a per-window choice, override `WebViewWindow.GetBrowserExecutableFolder`;
and if you'd rather not touch code at all, WebView2 honours the `WEBVIEW2_BROWSER_EXECUTABLE_FOLDER` environment
variable whenever the folder is left null. Point any of them at a runtime you got from Microsoft's Fixed Version
distribution (see the [WebView2 download page](https://developer.microsoft.com/microsoft-edge/webview2/)). If the
folder is set but missing, AOTrino logs a warning and falls back to evergreen rather than failing to start.

Two costs, stated plainly, because pinning is a decision with a bill attached:

* **Size.** A Fixed Version runtime is roughly **150 MB, per architecture**. Your "single small exe" becomes an
  exe plus a runtime folder. That's a fine trade when you've chosen it, and the wrong default for everyone else,
  which is why evergreen stays the default.
* **You now own Chromium security updates.** This is the real one. Pinning turns off the auto-patching that is
  evergreen's best property: the day Chromium ships a security fix, *you* have to ship a refreshed runtime. Pin
  the version and forget it, and you are distributing a browser with known, unpatched vulnerabilities. If you go
  Fixed Version, put "refresh the WebView2 runtime" on your release checklist and keep it there.

Recommendation: stay evergreen unless you have a concrete reason not to. When you do pin, do it with eyes open on
the patching commitment above.

## Host objects belong to the window, not to your page

A host object registered on a window is reachable from **every document that window loads**, whatever its
origin. WebView2's `AddHostObjectToScript` takes no origin filter (`AddHostObjectToScriptWithOrigins` exists
only for iframes), so this is not something AOTrino can tighten for you.

Measured, with `https://example.com` loaded in a `NavigationMode.Web` window that had registered a host object:

```js
chrome.webview.hostObjects.sync.secret.getSecret()   // -> "simon@SMO03"
```

The injected runtime reaches remote pages too, `window.__aotrino` and its window controls are there on any
site. That is deliberate and harmless (they drive *this* window, a page can already `window.close()`), but a
**host object is not harmless**, because it's your code.

So the rule is simple, and it is about the window, not the object:

> Register host objects on windows that show **your** content. A window that browses the web gets none.

If an app needs both, that's the two-window shape from *the footgun* above: a `Local` window with the host
objects, a `Web` window with none. `NavigationMode` is a property you can set at any time, flipping a window
to `Web` after registering does **not** unregister anything, so don't.

### The case that tempts everyone: signing in

There's no auth sample, because an OAuth or SSO flow is an identity provider's business and not a platform's.
But it's worth naming, because it's the moment the rule above gets broken, an app needs the user to sign in at
`login.microsoftonline.com`, the app already has a window, and pointing that window at the login page is one
line.

Don't point *that* window at it. The window with your host objects on it is the last one that should load
someone else's page, and an auth flow is a redirect chain you don't control, ending wherever the identity
provider decides. Instead:

* open a **second window** (`NavigationMode.Web`, no host objects) for the sign-in, or hand the URL to the
  user's real browser with `OpenExternal`, which is what `Local` already does with an off-app link, and what
  a native app is supposed to do;
* catch the redirect where a desktop app is meant to: a loopback `http://127.0.0.1:<port>/` listener, or a
  custom scheme registered to your exe (`myapp://auth`), both of which are the documented desktop patterns and
  neither of which needs the token to pass through a page;
* keep the token in .NET. A token in `localStorage` is a token available to every script the window ever runs.

The one AOTrino-specific detail worth knowing: WebView2's user-data folder is per-app, so a sign-in in a `Web`
window persists its cookies across restarts like a browser profile would, convenient for "stay signed in",
and a reason to think about where that folder lives (`AOTrinoApplication.Current.Paths.WebView2UserDataPath`)
before shipping.

## `SystemInfo`: shipped, not registered

`AOTrino.SystemInfo` is a ready-made host object of read-only facts a page can't otherwise learn: versions
(down to the kernel), cultures and keyboard layouts, DPI and the monitors, graphics adapters, VM and remote
session detection. It exists so nobody hand-rolls DXGI enumeration for an about box.

**AOTrino never registers it.** That would violate the rule above, and everything in it is exactly what
fingerprinting wants. You register it, on a window you trust:

```csharp
protected override void RegisterHostObjects() => AddHostObject("system", new SystemInfo(this));
```

Its values are a JSON DOM, and they're yours before you hand them over, drop what your app has no business
reporting, add what it does:

```csharp
var info = new SystemInfo(this);
info.Values.Remove("adapters");
info.Values["tenant"] = currentTenant;
AddHostObject("system", info);
```

`AOTrino.Samples.FileExplorer` does this behind its **System** button. Deliberately absent (but demonstrated
in samples) is elevation state (the first thing an exploit wants to know), and the machine and user names, those are the app's to
expose, from its own host object, if it wants them at all.

## The footgun, spelled out

Do **not** combine, in the same window:

* disabled web security / `--allow-file-access-from-files` (via your `GetEnvironmentOptions`), **and**
* content you don't fully control, i.e. `NavigationMode.Web`, or a `Local` allow-list that admits remote
  origins, or a bundled page that pulls in third-party script.

That combination hands the local machine to code you didn't write. If you need both a privileged local surface
*and* a web browser, use **two windows**: a `Local` window (privileged, your content only) and a separate `Web`
window (sandboxed, web security on).

## Recommended configurations

| App shape | `NavigationMode` | Web security | Notes |
|---|---|---|---|
| Local app, bundled content only (the common case) | `Local` | your choice, safe to disable for perf | This is what the samples do. |
| Local app that also opens external links | `Local` | on, or off for local perf | Links open in the real browser automatically. |
| Local app calling one trusted remote service | `Local` + `IsNavigationAllowed` allow-list | **on** | Never disable web security here. |
| A web browser | `Web` | **on** | Treat it like any browser tab. |
| Both privileged-local *and* web | two windows | per-window | Do not merge them. |

## Summary

AOTrino has one default worth stating: **local-first, no wandering onto the web unless you ask for it.** Every
capability beyond that is yours to grant, explicitly and by name. That keeps the platform small, keeps the
cautious path the default path, and keeps the decisions where they're actually made, in your app, with your
eyes open. It is not a sandbox, it is not an audit, and it comes with no warranty: read the code, and decide
what your own app should allow.
