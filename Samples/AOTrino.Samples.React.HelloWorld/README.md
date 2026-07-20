# React · Hello World

The first sample with a build step. React and TypeScript through Vite, calling .NET with types rather than with
strings.

## What it shows

The bridge is reachable from any front end as `chrome.webview.hostObjects`, which is untyped by nature. The optional
`@aotrino/client` package types it, so a call into the host is checked at compile time and completed by the editor:

```ts
const answer = await host.Add(2, 3);
```

`DemoApi.cs` is the other end of that, an ordinary C# class whose public members are the API. Nothing is generated,
nothing is registered twice, and adding a method to the class is the whole of adding a method to the API.

The npm build is wired into MSBuild, so building the project builds the front end and embeds the result. There is no
second command to remember and no way to ship an executable containing yesterday's front end.

## Files worth reading

| File | What is in it |
| --- | --- |
| `DemoApi.cs` | The host API, plain C#. |
| `WebRoot\src` | The React front end, TypeScript. |
| `WebRoot\vite.config.ts` | The bundler config, output goes to `dist`, which is what gets embedded. |

Run it with `dotnet run` from this folder. The npm install and the bundle happen as part of the build.
