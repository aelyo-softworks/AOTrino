# Web Browser

A minimal browser. `NavigationMode.Web` lets the window navigate its top level document anywhere, which is the one
mode where an AOTrino window stops being an app and becomes a browser.

## What it shows

The contrast with every other sample here. The default, `NavigationMode.Local`, keeps a window on the app's own
content and hands off app links to the user's default browser, see [File Explorer](../AOTrino.Samples.FileExplorer)
for that side of it. This window opts out of the lock deliberately, and the difference is one property.

Because the mode changes what the window is, it also changes what the window should behave like, and the browser
behaviours AOTrino turns off for app windows come back on here:

* the right click menu, with Back, Reload and View source.
* the status bar that shows the target of the link under the pointer.
* the browser accelerator keys, Ctrl+R, F5, Ctrl+P and the rest.
* the browser's own failure page, which is written for someone browsing the web and offers to retry.

A single WebView cannot wrap native chrome around its own web content, so the bar at the top, back, forward, reload,
address, go and close, is **injected into every document** from a script embedded in the executable. That is
`AddStartupScriptResource` and `Scripts\BrowserChrome.js`, which is also how a front end keeps its JavaScript in
`.js` files rather than in C# string literals.

No web security is disabled to make any of this work.

Run it with `dotnet run` from this folder, optionally with a URL on the command line.
