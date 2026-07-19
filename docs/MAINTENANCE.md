# Architecture and maintenance

What the repo is made of, where every version number lives, how a release goes out, and what to do about it
next week, next month and in six months.

## The map

```
AOTrino.slnx                 the solution: the core lib, every sample, the templates, and the npm tree
Directory.Build.props        shared by every project: TFM, THE version, platforms, assembly attributes
Directory.Build.targets      the repo's own build logic: DirectN/WebView2 references, the npm workspace hook.
                             imports AOTrino\build\AOTrino.targets, so the samples build on the same logic the
                             package ships rather than on a second copy of it
package.json                 the npm workspace root: the @aotrino/* list, build:libs, pack:libs

AOTrino/                     the core library. everything else is a consumer of it
  build/AOTrino.targets      the build logic that ships INSIDE the package: WebRoot/Scripts embedding, the npm
                             hooks, the default app manifest. what a consumer's project gets for free
  build/AOTrino.app.manifest DPI awareness + common controls, the default when an app has no manifest
  PackageReadme.md           the package's front page. nuget.org can't resolve the repo README's relative
                             links, so it gets its own
  runtimes/win-{x86,x64,arm64}/native/WebView2Loader.dll
Templates/                   the AOTrino.Templates package: `dotnet new aotrino[-react|-fluent]`
  templates/*/               one folder per template, the smallest thing that runs at each level
npm/
  AOTrino.Npm.proj           runs npm install + build:libs ONCE per build (see BRIDGE/FRONTEND docs)
  client/  react/  fluent/   the @aotrino/* packages: source in src/, built to dist/ (git-ignored)
Samples/
  AOTrino.Samples.*          plain samples: hand-written WebRoot/dist, no npm
  AOTrino.Samples.React.*    React samples: WebRoot is a Vite project
  AOTrino.Samples.FluentUI.* Fluent UI samples (Fluent implies React, so no React. prefix)
publish.bat                  every sample x x86/x64/arm64 into publish\, optionally UPX'd, zipped, released
PublishSamples.proj          what publish.bat actually runs
.github/workflows/           build.yml on every push; release.yml on a v* tag, it runs publish.bat, so the
                             released binaries come out of the same script as a local drop
docs/                        SECURITY, BRIDGE, FRONTEND, THEMING, and this file
External/                    OPTIONAL, git-ignored, absent from a normal clone. If present, DirectN and
                             WebView2 are referenced from these DLLs instead of from their NuGet packages -
                             which is how a change to one of those libs gets tried here before it's published.
                             Without it, the published packages are used and everything builds; that's the
                             normal case. Detected in Directory.Build.targets; -p:UseLocalExternal=true|false
                             overrides the detection either way
```

The dependency direction is one-way and worth keeping that way:

```
client  ->  react  ->  fluent          (each one's types come from the previous one's dist)
AOTrino (core)  ->  every sample
AOTrino (package)  ->  what AOTrino.Templates generates
```

`build:libs` in the root `package.json` encodes that order. npm does not guarantee an order for workspace
scripts, which is why the list is explicit and why the libraries have **no `prepare` script**, a root
`postinstall` runs `build:libs` instead.

## Where every version lives

| What | Pinned in | Notes |
| --- | --- | --- |
| **The version**, assemblies, `AOTrino`, `AOTrino.Templates` | `Directory.Build.props` (`Version`) | one number for the whole product, see below |
| .NET TFM | `Directory.Build.props` (`TargetFramework`) | one place, every project |
| Windows SDK projection (`WindowsSdkPackageVersion`) | **nothing, by design**, except `Samples\AOTrino.Samples.CaptureScreen` | the TFM picks it, see *the traps* before you pin it anywhere else |
| DirectN, DirectN.Extensions, WebView2 | `Directory.Build.targets` (`DirectNAotVersion`, `WebView2AotVersion`) | NuGet packages by default, `External\*.dll` instead when that folder exists |
| `WebView2Loader.dll` (x86/x64/arm64) | `AOTrino\runtimes\...` in the core lib | committed binaries |
| WebView2 **Runtime** | nothing by default, evergreen on the user's machine | `AOTrinoApplication` refuses to start without it and shows a download link. An app can pin it (Fixed Version) via `BrowserExecutableFolder`, see [SECURITY.md](SECURITY.md) for the tradeoff |
| React, Vite, TypeScript, `@types/*` | each `package.json`: `npm/*`, every `Samples/*/WebRoot`, each template | duplicated by design, samples are meant to be copy-pasteable |
| Fluent UI (`@fluentui/react-components`, `-icons`) | `npm/fluent/package.json` (peer + dev) and each Fluent `WebRoot/package.json` | keep the two in step |
| `@aotrino/*` npm versions | `npm/*/package.json` | their own, nothing references them *by number*, so they can't mismatch. `npm version <v> --workspaces` bumps all of them |
| Node itself | not pinned | any current LTS |

### One number, and why nothing else is allowed to name it

`<Version>` in `Directory.Build.props` is the whole product's version. The SDK derives `AssemblyVersion`,
`FileVersion` and `InformationalVersion` from it, writing those out separately, as this used to, is three more
places to forget on a bump and an assembly that reports 1.0.0 while the package on nuget.org says 1.0.1.

Two places genuinely need the number written into a *file*, and neither is allowed to hold a literal:

* the templates' `AOTrinoApp1.csproj`, a generated app has to reference the AOTrino its template shipped with
* `AOTrino\PackageReadme.md`, its "add this to your csproj" snippet

Both carry the token `AOTRINO_VERSION`, and each package's `_GetPackageFiles` hook substitutes `$(Version)` on
the way in. A version typed by hand into a template is right exactly once.

The `@aotrino/*` tarballs used to be the same problem from the other end: `npm pack` names its output after the
version inside it (`aotrino-client-1.0.0.tgz`), so the templates' `package.json` had to name that version too,
five paths, two templates, every release. Pack now stages them under a version-less `aotrino-client.tgz`; npm
reads the real version out of the tarball, and nothing has to be edited.

**So a bump is one edit**: `<Version>` in `Directory.Build.props`.

### The .NET dependencies

`DirectNAot`, `DirectNAot.Extensions` and `WebView2Aot` are `PackageReference`s, added to every C# project by
`Directory.Build.targets`, a fresh clone restores them from nuget.org and builds, with nothing else to install.
A local `External\` folder overrides them when it exists, which is a convenience for whoever is changing those
libraries, not a requirement for anyone else.

## Shipping a release

1. **Bump** `<Version>` in `Directory.Build.props`.
2. **Pack both**, and check the version came out where it belongs:

```bash
dotnet pack AOTrino/AOTrino.csproj -c Release
dotnet pack Templates/AOTrino.Templates.csproj -c Release
```

3. **Generate and build one app per template**, against the packages you just made and not against nuget.org,
   this is the only test that proves the tokens got substituted and the tarballs got carried. See the checklist.
4. **Push both packages to nuget.org, together.** `AOTrino` and `AOTrino.Templates` are one product with one
   version: a template that generates an app referencing an `AOTrino` that isn't published yet is a broken
   template, and a published version is immutable, the fix is another version, not a re-upload.
5. **Tag** `v<version>` and push the tag, that is the whole trigger. `release.yml` fires on any `v*` tag, builds
   every sample for x86/x64/arm64 on three runners at once, and attaches one zip per architecture to a GitHub
   release. Nothing else to press.

```bash
git release            # tags v<Version-from-Directory.Build.props>, pushes it, fires the workflow
# equivalently, by hand:
git tag -a v1.0.1 -m "Release v1.0.1" && git push origin v1.0.1
```

`git release` is a one-line git alias that reads `<Version>` straight from `Directory.Build.props`, so the tag
can't drift from the packages, set it up once (it's global, and refuses if the file's missing, the version is
unreadable, or the tag already exists):

```bash
git config --global alias.release '!f() { root=$(git rev-parse --show-toplevel) || return 1; props="$root/Directory.Build.props"; [ -f "$props" ] || { echo "release: run this from the AOTrino repo" >&2; return 1; }; ver=$(sed -n "s:.*<Version>\(.*\)</Version>.*:\1:p" "$props" | head -1); [ -n "$ver" ] || { echo "release: could not read <Version>" >&2; return 1; }; tag="v$ver"; git rev-parse -q --verify "refs/tags/$tag" >/dev/null 2>&1 && { echo "release: tag $tag already exists (bump <Version> first)" >&2; return 1; }; git tag -a "$tag" -m "Release $tag" && git push origin "$tag"; }; f'
```

Run it **after** the version bump is committed: the tag points at `HEAD`, so `HEAD` needs to be the commit that
carries the new `<Version>` (and the packages built from it). The `tag already exists` guard is the safety net,
a published tag, like a published package, is not a thing you casually move.

Why the two packages ship together, in the general case: the DLL in `AOTrino` and the `PackageReference` in the
templates are two halves of the same artifact. Anything that changes what a generated app must reference, the
version, the TFM, the SDK projection, breaks the older half silently, at the consumer's first build or, worse,
their first window.

## The routine

### Every week, nothing

Really. Nothing here expires weekly. The WebView2 Runtime updates itself, the rest is pinned.

### Every month, the npm tree

```bash
npm outdated                    # at the repo root: covers the libraries and every sample
npm update                      # patch + minor, within the ranges already in package.json
```

Then run **the checklist** below. Patch and minor bumps of React/Vite/TypeScript/Fluent are usually silent,
but the checklist is cheap and the failure mode isn't (see *the traps*).

### Every six months, the real pass

1. **.NET.** A new SDK/TFM: change `TargetFramework` in `Directory.Build.props`, rebuild, AOT-publish one
   sample (AOT is where a runtime change actually shows up). A TFM change moves the Windows SDK projection with
   it, which is a consumer-visible break, see *shipping a release*.
2. **Majors.** `npm outdated` shows them in the "Latest" column. Take them one package at a time, React,
   Vite, TypeScript and Fluent all have opinions and you want to know which one broke you.
3. **The interop libraries.** Bump `DirectNAotVersion` / `WebView2AotVersion` in `Directory.Build.targets`
   when new ones ship, and rebuild an AOT sample. If you keep an `External\` folder, remember it wins over
   those versions while it's there, and nothing tells you it's six months behind, which is the one that
   silently rots. Delete it to build against what everyone else builds against.
4. **The templates' npm deps.** They're a copy of the samples' and don't move with them. `npm outdated` at the
   root does *not* see them, the templates aren't workspaces (a template is content, not a project).
5. **Re-read** `docs/BRIDGE.md` on nested arrays: if WebView2Feedback #3183 ever closes, the flat-array
   advice can change.

### When you actually need it

* **A new WebView2 API**: regenerate/rebuild the WebView2 bindings in their own repo, publish them, and bump
  `WebView2AotVersion`.
* **A new AOTrino sample**: nothing to wire. The `.slnx` needs the project, and the root `workspaces` globs
  (`Samples/AOTrino.Samples.React.*/WebRoot`, `...FluentUI.*/WebRoot`) already cover a new front end.
* **A newer WinRT API in one sample**: set `WindowsSdkPackageVersion` **in that sample's csproj**, where restore
  can see it. Not repo-wide, see *the traps*.

## The checklist

After **any** dependency change, in this order. It takes about a minute and catches everything that has ever
actually broken here.

```bash
# 1. a real fresh clone, not an incremental build.
#    NOTE the glob: only the npm-built samples have a generated dist. the plain samples' WebRoot\dist is
#    hand-written and committed, `rm -rf Samples/*/WebRoot/dist` deletes your source. (ask how I know.)
rm -rf node_modules npm/*/dist Samples/AOTrino.Samples.React.*/WebRoot/dist Samples/AOTrino.Samples.FluentUI.*/WebRoot/dist
dotnet build AOTrino.slnx -c Release -p:Platform=x64 -t:Rebuild
git status --porcelain          # must be empty: nothing generated belongs to git, nothing tracked was deleted
```

**2. `npm install` must appear exactly once.** More than once means the per-project npm work is racing again
(it used to: two installs collided on the workspace symlinks with `EEXIST`, and two `tsc` wrote one `dist`).

**3. The resources must still be embedded**, this is the one that fails *silently*, with a green build and
apps that show a blank window:

```powershell
$dll = "Samples\AOTrino.Samples.React.HelloWorld\bin\x64\Release\net10.0-windows10.0.19041.0\AOTrino.Samples.React.HelloWorld.dll"
([System.Reflection.Assembly]::LoadFrom($dll)).GetManifestResourceNames()
# expect: WebRoot\dist\index.html + assets, never an empty list
```

**4. Run one of each kind**: a plain sample (`HelloWorld`), a React one (`React.Dashboard`), a Fluent one
(`FluentUI.HelloWorld`). Click the theme picker, open a menu.

**5. Before a release only, build what a consumer gets.** Nothing above tests the packages: the samples
reference the *project*, so they pass whatever the package does. Every packaging bug found here got found this
way and none of them turned a build red.

```bash
dotnet new install Templates/bin/Release/AOTrino.Templates.<version>.nupkg
# a nuget.config with a local source pointing at AOTrino/bin/Release, so it resolves the package you just
# built instead of the last one on nuget.org
dotnet new aotrino -o MyApp && cd MyApp && dotnet build
```

Then check, in the generated project: the `PackageReference` says the version you're shipping (not
`AOTRINO_VERSION`, not last release's number), and for `aotrino-react`/`aotrino-fluent`, `packages\` holds the
tarballs and `npm install` resolved them.

## The traps

Each of these cost real time once. They are documented where they live, and listed here so an update knows
what to look at.

| Symptom | Cause | Where |
| --- | --- | --- |
| Blank page, no error | Vite emits ES modules, a `file://` page has an opaque origin and CORS-blocks them | `VirtualHostName`, [SECURITY.md](SECURITY.md) |
| Whole window blank while a menu is open | layout styles on `<FluentProvider className>` leak onto every portal mount node | `AOTrinoProvider`, put layout on a div *inside* it |
| Green build, app has no front end | the `WebRoot\dist` glob ran before Vite generated it, or the embed target moved too late | `AOTrino\build\AOTrino.targets`, glob **inside** the target, embed at `BeforeBuild` |
| A plain sample suddenly embeds 0 resources | its `WebRoot\dist` was deleted, it's **hand-written and committed**, only `React.*`/`FluentUI.*` regenerate theirs | `git checkout -- Samples/.../WebRoot/dist` |
| `'tsc'/'vite' is not recognized` | `npm install` was skipped (an empty `node_modules` counted as installed) | `AOTrino.Npm.proj`, Inputs/Outputs on `node_modules\.package-lock.json` |
| `TS2307: cannot find module '@aotrino/client'` | a library built before the one it depends on | `build:libs` order, no `prepare` scripts |
| `Type 'number' is not assignable to type 'undefined'` | Griffel forbids CSS shorthands via its types | use longhands in `makeStyles` |
| JSON says `Entries`, JS reads `entries` | System.Text.Json's source generator keeps PascalCase | set a naming policy, or match it |
| 8 build warnings from a Fluent sample | Vite's 500 kB chunk warning, that budget is about web download time | `chunkSizeWarningLimit` |
| **`NETSDK1112`** on a consumer's machine: "the runtime pack for Microsoft.Windows.SDK.NET.Ref was not downloaded" | a `WindowsSdkPackageVersion` pin that arrives *with* the package: restore runs first, fetches the SDK's default projection, and the build then demands the pinned one | `AOTrino\build\AOTrino.targets`, pin nothing there, let the TFM decide |
| `Could not load file or assembly 'Microsoft.Windows.SDK.NET'` on the first window | the app and AOTrino were built against different projections. A *higher* projection in the app is fine, a lower one is not | the TFM is what makes both sides agree |
| A package builds green and is **missing files** | Content added after `_GetPackageFiles`, `Pack` and `GenerateNuspec` are both too late, and neither says a word | pack hooks: `BeforeTargets="_GetPackageFiles"` |
| A rewritten file comes out with its semicolon lists split across lines | `WriteLinesToFile` takes an *item list*; `win-x86;win-x64;win-arm64` is three items | `$([MSBuild]::Escape(...))` the text first |
| Packed content lands in `content\templates\aotrino\aotrino\` | pack appends each item's `RecursiveDir` to `PackagePath` itself | stop `PackagePath` at the parent |

## Visual Studio

Each `@aotrino/*` package is a real **`.esproj`**, Visual Studio's JavaScript project type, the one its
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
will write the same `dist`, verify with the checklist above that `npm install` still appears exactly once.

Two things this costs, worth knowing:

* **`dotnet build` does not restore `.esproj` at all, only Visual Studio does.** So a repo-wide property that
  is wrong for them (a `TargetFramework`, a `RuntimeIdentifiers`, a `PackageReference`) passes every command
  line and every CI run, and fails only when someone opens the solution. That's why everything in
  `Directory.Build.props`/`.targets` is scoped to `'$(MSBuildProjectExtension)' == '.csproj'`. Leave it scoped.
* Opening the solution needs the VS **JavaScript/TypeScript** workload. Without it those three projects won't
  load (the rest of the solution still will).

`package.json` (the workspace root) and `AOTrino.Npm.proj` sit in the `/npm/` solution folder as file links,
since no project owns them.
