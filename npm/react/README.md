# @aotrino/react

React hooks and components over [`@aotrino/client`](../client/README.md).

**Headless by design.** This package ships *behaviour* — bridge calls, subscription lifetimes, pending and
error state, the window drag region — and stable class names. It ships no CSS. Styling belongs to your app, or
to `@aotrino/fluent`.

It is **not published to npm**; samples resolve it through npm workspaces. See
[docs/FRONTEND.md](../../docs/FRONTEND.md).

`@aotrino/client` and `react` are peer dependencies.

## useHostProperties

Every host-object property read crosses the bridge as its own Promise. This reads a set of them together —
one round of calls instead of one per property in sequence — and tracks loading/error for you.

```tsx
const { values, loading, error, refresh } = useHostProperties(api, ["machineName", "uptime"]);

<dd>{values.machineName ?? "—"}</dd>
<button onClick={refresh} disabled={loading}>refresh</button>
```

For values that move, poll them — the timer is cleared on unmount and never starts when there's no host:

```tsx
useHostProperties(api, ["uptime", "workingSet"], { refreshIntervalMs: 1000 });
```

Only *property* names are accepted. Passing a method is a compile error, because "reading" a method would
silently hand you the function:

```ts
useHostProperties(api, ["fail"]);
// error TS2322: Type '"fail"' is not assignable to type 'HostPropertyName<DashboardApi>'
```

Outside AOTrino nothing is read, `loading` settles to `false` and `values` stays empty, so a component
renders its fallback instead of throwing.

## useHostCall

Wraps a call with the state a UI needs, and captures a rejection into `error` rather than letting it escape as
an unhandled promise rejection.

```tsx
const analyze = useHostCall((text: string) => api.analyzeAsync(text));

<button onClick={() => void analyze.call(text)} disabled={analyze.pending}>
    {analyze.pending ? "analyzing…" : "analyze"}
</button>
<output>{analyze.result}</output>
```

Pass a lambda rather than `api.analyzeAsync` — the bridge's proxy members shouldn't be detached from the
object they came from. `call` keeps a stable identity across renders, so it's safe in a dependency array.

Note that a .NET exception arrives as the full exception text (message, inner exception, stack, absolute
source paths). That's right for a log and wrong for a UI — show `error.message.split("\n", 1)[0]`.

## TitleBar

```tsx
<TitleBar title="My app" showMinimize showMaximize onClose={() => void api.quit()} />
```

Renders the drag region (handled natively — no mousedown handler), the window buttons, and accessible names.
Class hooks: `aotrino-titlebar`, `-title`, `-buttons`, `-button`, `-close`.

Double-clicking the caption maximizes or restores the window, as a native caption does — the bar stands in for
the OS one, so it owes users that gesture. A double-click that lands on a button is ignored. Turn it off for a
window that shouldn't be maximized:

```tsx
<TitleBar title="My app" doubleClickToMaximize={false} />
```

The button `aria-label`s are the only user-visible text this package renders, so they're overridable:

```tsx
<TitleBar labels={{ minimize: "Réduire", maximize: "Agrandir", close: "Fermer" }} />
```

## useIsHosted, useSharedBuffer, useHostMessage

```tsx
const hosted = useIsHosted();                            // constant for the life of the document

const { buffer, meta } = useSharedBuffer("frame");       // re-renders when .NET re-hands the buffer

useHostMessage<{ kind: string }>(msg => { /* ... */ });  // inline lambdas don't resubscribe
```

`useSharedBuffer` re-renders with a *new* `ArrayBuffer` whenever .NET grows the buffer — never hold on to a
previous one. `useHostMessage` and `useSharedBuffer` unsubscribe on unmount.
