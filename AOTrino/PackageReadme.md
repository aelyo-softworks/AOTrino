# AOTrino

**Electron-like desktop apps on .NET Native AOT + WebView2.** One executable, no runtime to install, no Chromium
to ship — Windows already has both. x86, x64 and ARM64.

A window is a real HWND, the UI is a web page, and the two talk over a typed bridge.

## Getting started

```
dotnet new install AOTrino.Templates

dotnet new aotrino        -o MyApp    # a plain HTML page, no build step
dotnet new aotrino-react  -o MyApp    # React + @aotrino/react
dotnet new aotrino-fluent -o MyApp    # Fluent UI + @aotrino/fluent

cd MyApp
dotnet run
dotnet publish -r win-x64 -c Release  # the single exe, about 11 MB
```

Or add it to a plain WinExe project:

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
  <OutputType>WinExe</OutputType>
  <PublishAot>true</PublishAot>
  <!-- everything under WebRoot\dist is embedded in the exe and extracted at startup -->
  <AOTrinoEmbedWebRoot>true</AOTrinoEmbedWebRoot>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="AOTrino" Version="AOTRINO_VERSION" />
</ItemGroup>
```

```csharp
using AOTrino;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow() : base("My app") { }

    // expose .NET to the page; every member is async on the JS side, even a property read
    protected override void RegisterHostObjects() => AddHostObject("app", new MyApi(this));
}
```

The package brings the build logic with it: `WebRoot\dist` embedding, `npm install`/`npm run build` for a
bundler front end, a default application manifest (DPI awareness and common controls), and the Windows SDK
projection AOTrino is compiled against.

## Requirements

To **build and run**: the .NET 10 SDK. The WebView2 runtime is already on any up-to-date Windows — an AOTrino
app offers a download link if it isn't.

To **publish the single exe**: also the MSVC linker, because the SDK compiles to native code but doesn't ship a
linker (`dotnet publish` otherwise stops with *"Platform linker not found"*). No IDE needed:

```
winget install Microsoft.VisualStudio.BuildTools --override "--passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```

Add `--add Microsoft.VisualStudio.Component.VC.Tools.ARM64` for ARM64, or tick **Desktop development with C++**
if you already have Visual Studio. See https://aka.ms/nativeaot-prerequisites.

## The rest

Twelve samples with screenshots, four optional levels (plain page → `@aotrino/client` → `@aotrino/react` →
`@aotrino/fluent`), and the docs — the bridge, the security defaults, theming, front end, maintenance — are on
GitHub:

**https://github.com/aelyo-softworks/AOTrino**

MIT licensed. Free software, provided as is, without warranty of any kind.
