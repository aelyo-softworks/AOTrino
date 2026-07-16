# Architecture and maintenance

What the repo is made of, where every version number lives, and what to do about it next week, next month
and in six months.

## The map

```
AOTrino.slnx                 the solution: the core lib, every sample, and the npm tree as folders
Directory.Build.props        shared by every project: TFM, versions, platforms, assembly attributes
Directory.Build.targets      WebRoot embedding, Scripts embedding, the npm build hook
package.json                 the npm workspace root: the @aotrino/* list and build:libs
cr.bat                       MY script, not in this repo: refreshes External\ from local builds of the
                             sibling repos. It points at paths on my machine, so it's git-ignored - nothing
                             here needs it, see External/ below

AOTrino/                     the core library. everything else is a consumer of it
  runtimes/win-{x86,x64,arm64}/native/WebView2Loader.dll
External/                    OPTIONAL, git-ignored. If present, DirectN/DirectN.Extensions/WebView2 are
                             referenced from here instead of from their NuGet packages - which is how a change
                             in one of those libs gets tried here without publishing it first. Absent (a fresh
                             clone), the published packages are used and everything builds. Detected in
                             Directory.Build.targets; override with -p:UseLocalExternal=true|false
npm/
  AOTrino.Npm.proj           runs npm install + build:libs ONCE per build (see BRIDGE/FRONTEND docs)
  client/  react/  fluent/   the @aotrino/* packages: source in src/, built to dist/ (git-ignored)
Samples/
  AOTrino.Samples.*          plain samples: hand-written WebRoot/dist, no npm
  AOTrino.Samples.React.*    React samples: WebRoot is a Vite project
  AOTrino.Samples.FluentUI.* Fluent UI samples (Fluent implies React, so no React. prefix)
docs/                        SECURITY, BRIDGE, FRONTEND, THEMING, and this file
```

The dependency direction is one-way and worth keeping that way:

```
client  ->  react  ->  fluent          (each one's types come from the previous one's dist)
AOTrino (core)  ->  every sample
```

`build:libs` in the root `package.json` encodes that order. npm does not guarantee an order for workspace
scripts, which is why the list is explicit and why the libraries have **no `prepare` script** — a root
`postinstall` runs `build:libs` instead.

## Where every version lives

| What | Pinned in | Notes |
| --- | --- | --- |
| .NET TFM, Windows SDK | `Directory.Build.props` (`TargetFramework`, `WindowsSdkPackageVersion`) | one place, every project |
| Product/file/informational version | `Directory.Build.props` | `SourceRevisionId` appends the build timestamp on purpose |
| DirectN, DirectN.Extensions, WebView2 | `Directory.Build.targets` (`DirectNAotVersion`, `WebView2AotVersion`) | the published packages by default; `External\*.dll` instead when that folder exists |
| `WebView2Loader.dll` (x86/x64/arm64) | `AOTrino\runtimes\...` in the core lib | committed binaries |
| WebView2 **Runtime** | nothing — it's evergreen on the user's machine | `AOTrinoApplication` refuses to start without it and shows a download link |
| React, Vite, TypeScript, `@types/*` | each `package.json`: `npm/*` and every `Samples/*/WebRoot` | duplicated by design — samples are meant to be copy-pasteable |
| Fluent UI (`@fluentui/react-components`, `-icons`) | `npm/fluent/package.json` (peer + dev) and `Samples/AOTrino.Samples.FluentUI.*/WebRoot/package.json` | keep the two in step |
| Node itself | not pinned | any current LTS |

There is **no `PackageReference` anywhere**. That's deliberate: the .NET dependencies are local DLLs, so a
`dotnet restore` pulls essentially nothing and the build can't break because of someone else's release.

## The routine

### Every week — nothing

Really. Nothing here expires weekly. The WebView2 Runtime updates itself; the rest is pinned.

### Every month — the npm tree

```bash
npm outdated                    # at the repo root: covers the libraries and every sample
npm update                      # patch + minor, within the ranges already in package.json
```

Then run **the checklist** below. Patch and minor bumps of React/Vite/TypeScript/Fluent are usually silent,
but the checklist is cheap and the failure mode isn't (see *the traps*).

### Every six months — the real pass

1. **.NET.** A new SDK/TFM: change `TargetFramework` in `Directory.Build.props`, rebuild, AOT-publish one
   sample (AOT is where a runtime change actually shows up).
2. **Majors.** `npm outdated` shows them in the "Latest" column. Take them one package at a time — React,
   Vite, TypeScript and Fluent all have opinions and you want to know which one broke you.
3. **The interop libraries.** Bump `DirectNAotVersion` / `WebView2AotVersion` in `Directory.Build.targets`
   when new ones ship, and rebuild an AOT sample. If you keep an `External\` folder, remember it wins over
   those versions while it's there - and nothing tells you it's six months behind, which is the one that
   silently rots. Delete it to build against what everyone else builds against.
4. **Re-read** `docs/BRIDGE.md` on nested arrays: if WebView2Feedback #3183 ever closes, the flat-array
   advice can change.

### When you actually need it

- **A new WebView2 API**: regenerate/rebuild the WebView2 bindings in their own repo, publish or drop them
  into `External\`, and bump `WebView2AotVersion` when the package is out.
- **A new AOTrino sample**: nothing to wire. The `.slnx` needs the project, and the root `workspaces` globs
  (`Samples/AOTrino.Samples.React.*/WebRoot`, `...FluentUI.*/WebRoot`) already cover a new front end.

## The checklist

After **any** dependency change, in this order. It takes about a minute and catches everything that has ever
actually broken here.

```bash
# 1. a real fresh clone, not an incremental build.
#    NOTE the glob: only the npm-built samples have a generated dist. the plain samples' WebRoot\dist is
#    hand-written and committed - `rm -rf Samples/*/WebRoot/dist` deletes your source. (ask how I know.)
rm -rf node_modules npm/*/dist Samples/AOTrino.Samples.React.*/WebRoot/dist Samples/AOTrino.Samples.FluentUI.*/WebRoot/dist
dotnet build AOTrino.slnx -c Release -p:Platform=x64 -t:Rebuild
git status --porcelain          # must be empty: nothing generated belongs to git, nothing tracked was deleted
```

**2. `npm install` must appear exactly once.** More than once means the per-project npm work is racing again
(it used to: two installs collided on the workspace symlinks with `EEXIST`, and two `tsc` wrote one `dist`).

**3. The resources must still be embedded** — this is the one that fails *silently*, with a green build and
apps that show a blank window:

```powershell
$dll = "Samples\AOTrino.Samples.React.HelloWorld\bin\x64\Release\net10.0-windows10.0.19041.0\AOTrino.Samples.React.HelloWorld.dll"
([System.Reflection.Assembly]::LoadFrom($dll)).GetManifestResourceNames()
# expect: WebRoot\dist\index.html + assets — never an empty list
```

**4. Run one of each kind**: a plain sample (`HelloWorld`), a React one (`React.Dashboard`), a Fluent one
(`FluentUI.HelloWorld`). Click the theme picker; open a menu.

## The traps

Each of these cost real time once. They are documented where they live, and listed here so an update knows
what to look at.

| Symptom | Cause | Where |
| --- | --- | --- |
| Blank page, no error | Vite emits ES modules; a `file://` page has an opaque origin and CORS-blocks them | `VirtualHostName` — [SECURITY.md](SECURITY.md) |
| Whole window blank while a menu is open | layout styles on `<FluentProvider className>` leak onto every portal mount node | `AOTrinoProvider` — put layout on a div *inside* it |
| Green build, app has no front end | the `WebRoot\dist` glob ran before Vite generated it, or the embed target moved too late | `Directory.Build.targets` — glob **inside** the target, embed at `BeforeBuild` |
| A plain sample suddenly embeds 0 resources | its `WebRoot\dist` was deleted — it's **hand-written and committed**, only `React.*`/`FluentUI.*` regenerate theirs | `git checkout -- Samples/.../WebRoot/dist` |
| `'tsc'/'vite' is not recognized` | `npm install` was skipped (an empty `node_modules` counted as installed) | `AOTrino.Npm.proj` — Inputs/Outputs on `node_modules\.package-lock.json` |
| `TS2307: cannot find module '@aotrino/client'` | a library built before the one it depends on | `build:libs` order; no `prepare` scripts |
| `Type 'number' is not assignable to type 'undefined'` | Griffel forbids CSS shorthands via its types | use longhands in `makeStyles` |
| JSON says `Entries`, JS reads `entries` | System.Text.Json's source generator keeps PascalCase | set a naming policy, or match it |
| 8 build warnings from a Fluent sample | Vite's 500 kB chunk warning; that budget is about web download time | `chunkSizeWarningLimit` |

## Visual Studio

Each `@aotrino/*` package is a real **`.esproj`** — Visual Studio's JavaScript project type, the one its
"React app" template creates. VS globs `src/` itself and surfaces the npm scripts, so nothing has to be listed
by hand.

**They build nothing.** Their npm hooks are off:

```xml
<ShouldRunNpmInstall>false</ShouldRunNpmInstall>
<ShouldRunBuildScript>false</ShouldRunBuildScript>
<ShouldRunNpmCi>false</ShouldRunNpmCi>
```

`npm install` and the library builds belong to `AOTrino.Npm.proj`, which does them once for the whole
solution. If those hooks are ever turned on, two npm installs will race the same `node_modules` and two `tsc`
will write the same `dist` — verify with the checklist above that `npm install` still appears exactly once.

Two things this costs, worth knowing:

- `Microsoft.VisualStudio.JavaScript.SDK` comes from nuget.org. It is the repo's **only** NuGet dependency;
  everything .NET is a local DLL in `External\`.
- Opening the solution needs the VS **JavaScript/TypeScript** workload. Without it those three projects won't
  load (the rest of the solution still will).

`package.json` (the workspace root) and `AOTrino.Npm.proj` sit in the `/npm/` solution folder as file links,
since no project owns them.
