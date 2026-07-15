namespace AOTrino.Samples.FluentUI.Gallery;

// the .NET half of the gallery: one member per thing the Bridge page demonstrates, and the few native
// actions the Window page drives.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class GalleryApi(WebViewWindow window) : DispatchObject
{
    private const string _userNameVariable = "USERNAME";
    private readonly long _startTick = Environment.TickCount64;

    // host-object members are invoked on the instance by the WebView2 bridge (BindingFlags.Instance),
    // so they must stay instance members to appear in the JS API even when they don't touch instance state.
#pragma warning disable CA1822 // Mark members as static

    // --- properties: read from JS as `await gallery.framework` (a property read is async too) ---
    public string Framework => RuntimeInformation.FrameworkDescription;
    public string AOTrinoVersion => AOTrinoApplication.Current!.AOTrinoVersion;
    public string WebView2Version => AOTrinoApplication.Current!.WebView2Version;

    // moves between reads, so auto-refresh has something to show
    public string Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64 - _startTick).ToString(@"hh\:mm\:ss");
    public string WorkingSet => $"{Environment.WorkingSet / (1024 * 1024):N0} MB";

    // --- sync methods ---
    public string Ping() => "pong from .NET";
    public int Add(int a, int b) => a + b;

    // read the process environment rather than baking values into the bundle
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string GetUserName() => Environment.GetEnvironmentVariable(_userNameVariable) ?? Environment.UserName;

    // an array crosses as a JS array.
    // nesting does not (WebView2Feedback #3183), which is why anything structured below goes as JSON instead.
    // see docs/BRIDGE.md
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

    // complex data crosses as JSON, serialized with the source generator (reflection doesn't survive AOT)
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

    // --- async: a real Promise on the JS side ---
    public async Task<string> EchoAsync(string text)
    {
        await Task.Delay(600);
        return $".NET echoes: {text}";
    }

    // --- an exception: crosses as a rejected promise, not a crash ---
    public string Fail() => throw new InvalidOperationException("this .NET method always throws");

    // --- native actions the page can't do itself ---
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

#pragma warning restore CA1822

    // --- .NET -> JS: pushes window.galleryTick(n) each second, then 0. the bridge invokes this on the UI
    //     thread and the awaits resume there (window sync context), so ExecuteScript is safe ---
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

    // the system backdrop is a window-level Windows feature, not a CSS one
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
