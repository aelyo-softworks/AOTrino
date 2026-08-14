namespace AOTrino.Samples.FluentUI.Gallery;

// the .NET half of the gallery: one member per thing the Bridge page demonstrates, and the few native actions the Window page drives.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class GalleryApi(WebViewWindow window) : DispatchObject
{
    private const string _userNameVariable = "USERNAME";

    // the virtual table the Collections page scrolls. no such table is ever built: rows are computed per request, which is exactly the point,
    // .NET owns the data, the page is handed only the slice on screen.
    private const int _virtualRowCount = 500_000;
    private const int _maxPageSize = 1_000;
    private static readonly string[] _rowKinds = ["C#", "TypeScript", "Markdown", "Project", "Folder"];
    private static readonly string[] _rowExtensions = [".cs", ".ts", ".md", ".csproj", string.Empty];
    private static readonly DateTime _rowEpoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly long _startTick = Environment.TickCount64;

    // host-object members are invoked on the instance by the WebView2 bridge (BindingFlags.Instance),
    // so they must stay instance members to appear in the JS API even when they don't touch instance state.
#pragma warning disable CA1822 // Mark members as static.

    // --- properties: read from JS as `await gallery.framework` (a property read is async too) ---.
    public string Framework => RuntimeInformation.FrameworkDescription;
    public string AOTrinoVersion => AOTrinoApplication.Current!.AOTrinoVersion;
    public string WebView2Version => AOTrinoApplication.Current!.WebView2Version;

    // moves between reads, so auto-refresh has something to show.
    public string Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64 - _startTick).ToString(@"hh\:mm\:ss");
    public string WorkingSet => $"{Environment.WorkingSet / (1024 * 1024):N0} MB";

    // --- sync methods ---.
    public string Ping() => "pong from .NET";
    public int Add(int a, int b) => a + b;

    // read the process environment rather than baking values into the bundle.
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string GetUserName() => Environment.GetEnvironmentVariable(_userNameVariable) ?? Environment.UserName;

    // an array crosses as a JS array.
    // nesting does not (WebView2Feedback #3183), which is why anything structured below goes as JSON instead.
    // see docs/BRIDGE.md.
    public int[] GetPrimes(int count)
    {
        var primes = new List<int>();
        for (var n = 2; primes.Count < Math.Clamp(count, 0, 200); n++)
        {
            var prime = true;
            for (var d = 2; d * d <= n; d++)
            {
                if (n % d == 0)
                {
                    prime = false;
                    break;
                }
            }

            if (prime)
            {
                primes.Add(n);
            }
        }
        return [.. primes];
    }

    // complex data crosses as JSON, serialized with the source generator (reflection doesn't survive AOT).
    public string GetProcessInfo()
    {
        var info = new GalleryProcessInfo(
            Environment.ProcessId,
            Environment.ProcessorCount,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.WorkingSet,
            GC.GetTotalMemory(false),
            GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2));
        return JsonSerializer.Serialize(info, GalleryJsonContext.Default.GalleryProcessInfo);
    }

    // --- async: a real Promise on the JS side ---.
    public async Task<string> EchoAsync(string text)
    {
        await Task.Delay(600);
        return $".NET echoes: {text}";
    }

    // --- the virtual table: how much there is, and one page of it ---.

    // read once by the page, to size the scrollbar. it never learns anything else about the other 499,800 rows.
    public int RowCount => _virtualRowCount;

    // a page of rows, as JSON. Task-returning rather than plain: this one is instant because it computes rows from an index,
    // but a real source is a database or a disk, and the page has to be written to await either.
    // count is clamped, deliberately: a page that asks for the whole table isn't paging,
    // and the whole point of this is that "give me everything" never happens.
    public Task<string> GetRowsAsync(int offset, int count)
    {
        offset = Math.Clamp(offset, 0, _virtualRowCount);
        count = Math.Clamp(count, 0, Math.Min(_maxPageSize, _virtualRowCount - offset));

        var rows = new GalleryRow[count];
        for (var i = 0; i < rows.Length; i++)
        {
            var index = offset + i;
            var kind = index % _rowKinds.Length;

            // no randomness: the same row must read the same on every request, or scrolling back would rewrite history.
            rows[i] = new GalleryRow(
                index,
                $"item-{index:D6}{_rowExtensions[kind]}",
                _rowKinds[kind],
                index * 7919L % 500_000 + 1024,
                _rowEpoch.AddMinutes(-index * 7d).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        }

        var page = new GalleryRowPage(offset, _virtualRowCount, rows);
        return Task.FromResult(JsonSerializer.Serialize(page, GalleryJsonContext.Default.GalleryRowPage));
    }

    // --- an exception: crosses as a rejected promise, not a crash ---.
    public string Fail() => throw new InvalidOperationException("this .NET method always throws");

    // --- native actions the page can't do itself ---.
    public bool OpenExternal(string url)
    {
        Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = url });
        return true;
    }

    public string CollectGarbage()
    {
        var before = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        return $"freed {(before - GC.GetTotalMemory(true)) / (1024.0 * 1024):N1} MB";
    }

    // --- the Packages page: what the app runs on, and whether newer versions exist ---.

    // the .NET / NuGet packages the app runs on. Current is the version of the assembly actually loaded, Declared is
    // what the project asked for (baked into this assembly's metadata by the .csproj, see AssemblyMetadata), RegistryId
    // is the id the "Check for updates" button looks up on nuget.org. the npm half of the table is baked into the page by Vite.
    public string GetPackages()
    {
        var packages = new List<PackageInfo>
        {
            new("dotnet", ".NET", RuntimeInformation.FrameworkDescription, null, null),
            NugetPackage("AOTrino", "AOTrino"),
            NugetPackage("DirectN", "DirectNAot"),
            NugetPackage("DirectN.Extensions", "DirectNAot.Extensions"),
            NugetPackage("WebView2", "WebView2Aot"),
        };
        return JsonSerializer.Serialize(packages.ToArray(), GalleryJsonContext.Default.PackageInfoArray);
    }

    // the latest published version of a package, from its registry, for the "Check for updates" button. the host makes
    // the call, not the page, so the Local navigation lock and CORS never enter into it. returns an empty string on any
    // failure (offline included), which the page reads as "couldn't check" rather than an error.
    public async Task<string> GetLatestVersionAsync(string ecosystem, string id)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.Add("User-Agent", "AOTrino.Samples.FluentUI.Gallery");

            if (string.Equals(ecosystem, "npm", StringComparison.OrdinalIgnoreCase))
            {
                var manifest = await http.GetStringAsync($"https://registry.npmjs.org/{id}/latest");
                return JsonSerializer.Deserialize(manifest, GalleryJsonContext.Default.NpmLatest)?.Version ?? string.Empty;
            }

            var index = await http.GetStringAsync($"https://api.nuget.org/v3-flatcontainer/{id.ToLowerInvariant()}/index.json");
            var versions = JsonSerializer.Deserialize(index, GalleryJsonContext.Default.NugetIndex)?.Versions ?? [];

            // the flat container index is oldest first, so the newest stable is the last one with no prerelease "-" suffix.
            for (var i = versions.Length - 1; i >= 0; i--)
            {
                if (!versions[i].Contains('-'))
                    return versions[i];
            }

            return versions.Length > 0 ? versions[^1] : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

#pragma warning restore CA1822

    // one NuGet package: the loaded assembly's version as Current, and the version the .csproj declared as metadata.
    // the registry id is both the display name and what nuget.org is queried by.
    private static PackageInfo NugetPackage(string assemblyName, string registryId) =>
        new("nuget", registryId, LoadedAssemblyVersion(assemblyName) ?? "not loaded", DeclaredVersion(registryId), registryId);

    // the informational version of a loaded assembly, by simple name, with the "+<commit>" build metadata trimmed off.
    private static string? LoadedAssemblyVersion(string simpleName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                continue;

            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(informational))
                return assembly.GetName().Version?.ToString();

            var plus = informational.IndexOf('+');
            return plus < 0 ? informational : informational[..plus];
        }

        return null;
    }

    // a version the .csproj baked in as assembly metadata, keyed by the package's registry id.
    private static string? DeclaredVersion(string key)
    {
        foreach (var metadata in Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.Equals(metadata.Key, key, StringComparison.OrdinalIgnoreCase))
                return metadata.Value;
        }

        return null;
    }

    // --- .NET -> JS: pushes window.galleryTick(n) each second, then 0.
    // the bridge invokes this on the UI thread and the awaits resume there (window sync context), so ExecuteScript is safe ---.
    public async Task<int> CountdownAsync(int seconds)
    {
        var n = Math.Clamp(seconds, 1, 10);
        for (var i = n; i > 0; i--)
        {
            window.ExecuteScript($"window.galleryTick && window.galleryTick({i});");
            await Task.Delay(1000);
        }

        window.ExecuteScript("window.galleryTick && window.galleryTick(0);");
        return n;
    }

    // the system backdrop is a window-level Windows feature, not a CSS one.
    public bool SetBackdrop(string type)
    {
        var backdrop = type switch
        {
            "mica" => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
            "acrylic" => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
            "tabbed" => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW,
            _ => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE,
        };
        window.SetSystemBackdrop(backdrop);
        return true;
    }

    public void Quit() => window.Close();
}
