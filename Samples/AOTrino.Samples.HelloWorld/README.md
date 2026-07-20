# Hello World

The smallest complete app there is: a window, a page, and two version strings pushed into it from .NET.

Read this one first. `Program.cs` starts the application, `MainWindow.cs` is about fifteen lines, and
`WebRoot\dist\index.html` is a hand-written page. There is no npm, no bundler, no configuration and no build step for
the front end at all.

## What it shows

* **One file to ship.** The page is embedded in the executable and unpacked beside it at startup, so what you copy to
  another machine is a single exe. Nothing is fetched, installed or unpacked by an installer.
* **The window is yours.** The caption is drawn by the page. `data-aotrino-drag` makes that strip move the window,
  and double clicking it maximizes or restores it, the same as a caption Windows drew itself.
* **Talking to the page.** `ExecuteScript` pushes the WebView2 and AOTrino versions into the document once it has
  loaded, which is the simplest direction of traffic there is across the bridge.

## Files worth reading

| File | What is in it |
| --- | --- |
| `Program.cs` | The whole startup, an application and a window. |
| `MainWindow.cs` | The window, and the one call that pushes values into the page. |
| `WebRoot\dist\index.html` | The front end, hand written, embedded as it stands. |

Run it with `dotnet run` from this folder, or set it as the startup project in Visual Studio.
