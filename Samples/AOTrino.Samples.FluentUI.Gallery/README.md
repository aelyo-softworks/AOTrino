# Fluent UI · Gallery

The flagship. Every other sample here is one idea in isolation, this is all of them side by side, shaped like the
WinUI Gallery, where each page is a live demo with the code that produced it.

## What it shows

Every card runs for real. The code shown beside a demo is the source of the thing you just used, not a paraphrase of
it, and the source paths are live links that open in your actual editor, opened by .NET, because a page cannot open
your editor.

The section to look at first is the table of **500000 rows**. The rows live in .NET and cross the bridge two hundred
at a time as the view scrolls, so the page holds a window onto the data rather than the data. Nothing is serialized
that nobody is looking at, and the front end stays a front end.

That is the shape most real applications end up needing, and it is the one that decides whether a stack survives
contact with a real data set: the state stays on the side that can hold it, and only what is on screen crosses.

The **Packages** page turns the same idea on the app itself. It lists what the app is built on, current versions read
from the loaded .NET assemblies and the resolved npm build, next to what each project declared, and a button that
asks each registry for the latest. A page cannot read a `.csproj` or a `package.json`, and cannot reach nuget.org or
the npm registry from a `Local` window, so the .NET host reads the versions and makes the calls. The declared npm
versions are baked into the bundle at build time, because the running page has no `package.json` to read. It is the
one sample that reaches the network, behind an explicit button, and it fails soft when there is none.

## Files worth reading

| File | What is in it |
| --- | --- |
| `GalleryApi.cs` | The host API behind every demo: the paged rows, and the package versions and registry lookups for the Packages page. |
| `GalleryRowPage.cs` | One page of rows, which is what actually crosses. |
| `WebRoot\vite.config.ts` | Bakes the frontend package versions into the bundle, since the running page has no `package.json`. |
| `WebRoot\src` | The gallery shell, the navigation and the pages. |

Run it with `dotnet run` from this folder.
