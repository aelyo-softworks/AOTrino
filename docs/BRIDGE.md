# The bridge

How JavaScript calls .NET, and .NET calls JavaScript.

## The shape of it

You register a **host object** on a window, and the page finds it on `chrome.webview.hostObjects`:

```csharp
protected override void RegisterHostObjects() => AddHostObject("dotnet", new MyApi(this));
```

```csharp
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MyApi(WebViewWindow window) : DispatchObject
{
    public string MachineName => Environment.MachineName;
    public int Add(int a, int b) => a + b;
    public async Task<string> EchoAsync(string text) { await Task.Delay(50); return text; }
}
```

```js
await chrome.webview.hostObjects.dotnet.add(2, 40);   // 42
```

That is the whole model: **public members of the object you registered, and nothing else**. The bridge is
deny-all by default because it is empty until you fill it. Names cross case-insensitively, so `add` finds
`Add`.

TypeScript apps should use [`@aotrino/client`](../npm/client/README.md) rather than touch
`chrome.webview.hostObjects` directly — it's the same object with types on it.

## What crosses, and how

| In C# | In JS |
| --- | --- |
| a property | `await api.machineName` — a property **read is async too** |
| a method | `await api.add(2, 40)` |
| `async Task<T>` | a real `Promise` |
| an array (`int[]`, `string[]`) | a JS array |
| a `throw` | a rejected promise / a throw from the sync proxy |
| anything else | serialize it — see below |

Everything is asynchronous through `hostObjects`, including reading a property. There is a synchronous proxy
(`chrome.webview.hostObjects.sync.dotnet`) where members behave like plain JS, but each call blocks the page
until .NET answers. Prefer the async one.

### Nested arrays do not work

WebView2's documentation says host-object arrays nest "up to a depth of 3". They don't. The *shape* crosses
and the *values* don't:

```csharp
public object[] GetNested() => [1, 2, "foo", new object[] { 5, 6, "bar" }];
```

```js
await api.getNested();   // [1, 2, "foo", [null, null, null]]
```

Measured on WebView2 150.0.4078.65, in both directions and through both the async and the sync proxy.
Tracked upstream as [WebView2Feedback #3183](https://github.com/MicrosoftEdge/WebView2Feedback/issues/3183)
(open, priority-low).

So **a row of structured data has to reach the page as a single value**. Send JSON.

### Complex types: send JSON

Serialize it — with **source-generated** `System.Text.Json`, because reflection-based serialization does not
survive AOT:

```csharp
public string GetSystemInfo() => JsonSerializer.Serialize(info, HostJsonContext.Default.SystemInfo);
```

```ts
const info: SystemInfo = JSON.parse(await api.getSystemInfo());
```

`AOTrino.Samples.HostObjects` and `AOTrino.Samples.FileExplorer` both do this end to end. Note the source
generator keeps **PascalCase** by default, so the payload says `Entries`, not `entries`, unless you set a
naming policy.

### The VARIANT array, and why not to bother

There is another way, and it is worth knowing about only so you can rule it out. Since nesting is broken, you
can flatten each row into one string and return a flat array of them:

```csharp
// don't
public object[] ListAsArray() => rows.Select(r => string.Join('|', r.Name, r.Path, r.Size)).ToArray();
```

It works. A returned array even arrives as a real JS array rather than a proxy, so reading `a[i]` is a plain
property access. **It is still the wrong choice**, because it costs you real things and buys nothing:

- Rows can't nest, so every structure collapses into a separator convention both sides must agree on, that no
  compiler checks, and that breaks the day a filename contains your separator.
- No records, no types, no `@aotrino/client` interface worth the name — just `string.split`.
- **It isn't faster.** Measured on the same directory listing (5 fields per row, min of 10–20 iterations, in
  milliseconds):

| rows | flat VARIANT array | JSON |
| --- | --- | --- |
| 100 | 0.4 | 0.3 |
| 1 000 | 3.2 | 2.4 |
| 10 000 | 15.6 | 16.3 |

Within a millisecond of each other at every size — the gap is smaller than the run-to-run noise, in both
directions. Post-processing is free either way (~1 ms per 10 000 rows, `split` and `JSON.parse` alike), and
JSON keeps pace while carrying a *larger* payload (1265 kB at 10 000 rows).

So: **use JSON**. The array path is a workaround for a bug, priced like the thing it works around.

And if you're moving enough data for any of this to matter, you want the shared buffer instead — see the
bottom of this page.

## Async results: `GetTaskResult`

An `async` host method returns a `Task<T>`, and the bridge has to get the `T` out of it. It cannot use
reflection or `dynamic` for that — the AOT compiler has to see every type ahead of time — so `DispatchObject`
switches over a list of **well-known types** instead: `string`, `bool`, the integer and floating-point types,
`decimal`, `char`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, `Uri`, `object`, arrays of the common
ones, and the nullable value types.

**Most host objects need nothing**: return `Task<string>` or `Task<int>` and it just works.

If you return a `Task<T>` of your own type, you get a clear exception telling you so. Override and chain:

```csharp
protected override object? GetTaskResult(Task task) => task switch
{
    Task<MyThing> t => t.Result,
    _ => base.GetTaskResult(task),
};
```

The base still handles everything it knows, and your override adds the one type it doesn't.

## Exceptions

A host method that throws is **control flow, not a crash**. It crosses as a rejected promise, so JS catches
it, and AOTrino only traces a warning rather than reporting an error.

What arrives is the **full .NET exception text** — message, inner exception, stack, and absolute source paths
from the build machine. That is right for a log and wrong for a UI:

```ts
catch (e) { show((e as Error).message.split("\n", 1)[0]); }
```

`@aotrino/react`'s `useHostCall` captures it into `error` for you.

## .NET calling JavaScript

Two ways out:

```csharp
window.ExecuteScript("window.myApp.tick(1);");                  // fire a script
await window.ExecuteScriptAsJson("document.title");             // ...and read the result back
webView.PostWebMessageAsJson("{\"kind\":\"ping\"}");            // a message; @aotrino/client's onMessage()
```

`ExecuteScript` does **not** await promises: if the script evaluates to a `Promise` you get `{}`. Return a
value synchronously, or post a message back when the async work finishes.

Async continuations resume on the window's UI thread (AOTrino installs a synchronization context), so calling
`ExecuteScript` after an `await` inside a host method is safe.

For bulk data — pixels, buffers, anything per-frame — don't use the bridge at all. Use
`WebViewWindow.CreateSharedBuffer`, which hands the page real shared memory (`AOTrino.Graphics.Direct2DSurface`
is built on it).

## Rules the bridge imposes

- **Host object classes need `[GeneratedComClass]` and `partial`.** They're COM objects underneath.
- **Members must be instance members.** The bridge looks them up with `BindingFlags.Instance`, so a `static`
  helper simply won't appear in JS. This trips CA1822 ("mark members as static") on members that don't touch
  instance state; the samples suppress it with a comment saying why.
- **Registration happens before navigation** — `RegisterHostObjects` is called for you at the right moment.

## Tweaking it

Most apps never touch these:

| Member | For |
| --- | --- |
| `GetTaskResult` | a `Task<T>` of your own type |
| `TryConvertArgument` | custom conversion of an argument coming from JS |
| `CreateTaskFunction` | intercepting how an async call is invoked |

## The escape hatch

If the bridge is not the shape you need, it isn't in the way: `WebViewWindow` exposes the raw
`ICoreWebView2` and the window handle. `AddHostObjectToScript`, CDP, custom schemes — all still yours.
AOTrino is a platform, not a cage.
