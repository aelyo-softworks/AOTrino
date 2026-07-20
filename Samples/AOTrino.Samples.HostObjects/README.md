# Host Objects

What crosses the bridge, in one window. Every button on the page calls a real .NET member and shows what came back.

This is the live reference for [docs/BRIDGE.md](../../docs/BRIDGE.md). If you want to know how a value, a promise or
an exception looks on the other side, run this and press the button rather than reading a description of it.

## What it shows

`HostApi.cs` is registered as `chrome.webview.hostObjects.dotnet`, and every public member of it becomes callable
from JavaScript:

* **Properties** read like properties, `MachineName`, `UserName`, `Architecture`, `Framework`.
* **Methods** take and return the ordinary types, `Add(2, 3)`, `Upper("text")`, `GetPrimes(12)` coming back as a real
  JS array.
* **`Task<T>` is a promise.** `EchoAsync`, `FactorialAsync` and `CountdownAsync` are awaited in the page with `await`,
  with nothing to poll and no completion flag to manage.
* **Exceptions are rejections.** `Fail()` throws in .NET and lands in a `catch` in JavaScript, with the message intact.
* **Objects cross as JSON**, serialized by a source-generated serializer, so none of it needs reflection and all of it
  survives trimming into a Native AOT binary.

Two of the members are the interesting ones, because a page in a browser cannot do either: `OpenUrl` hands a link to
the user's default browser, and `Quit` closes the window from JavaScript.

`OnConsoleLog` goes the other way. The page's `console.log` is captured and delivered to .NET, so the front end's
diagnostics end up in the host's trace rather than only in a developer tools window nobody has open.

Run it with `dotnet run` from this folder.
