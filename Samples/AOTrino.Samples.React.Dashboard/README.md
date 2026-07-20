# React · Dashboard

Live state from the host process, on a page that does no polling and manages no pending flags.

## What it shows

The window reports what a process knows about itself and about the machine it is on, the operating system build, the
architecture, the processor count, the working set, the uptime, the process id, and it keeps them current.

The interesting part is what the front end does **not** contain. `@aotrino/react` turns a host call into a hook:

```tsx
const { data, loading, error } = useHostCall(host => host.GetProcessInfo());
```

so the page has no `useEffect` chasing a promise, no `isLoading` state to set and unset, no cancellation on unmount to
remember, and no `setInterval` polling something that could have told it directly. The hook is a thin one, the bridge
underneath is the same one every other sample uses.

The numbers themselves are the argument for being a real process. Working set, uptime, processor count and OS build
are not values a page can obtain, at any price, from inside a browser. Here they are properties on a C# class.

## Files worth reading

| File | What is in it |
| --- | --- |
| `DashboardApi.cs` | The host API, one member per figure on screen. |
| `WebRoot\src` | The React front end and the hooks. |

Run it with `dotnet run` from this folder.
