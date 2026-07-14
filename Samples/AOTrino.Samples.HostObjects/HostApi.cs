namespace AOTrino.Samples.HostObjects;

// a JS-callable host object exposed as chrome.webview.hostObjects.dotnet.
// public property getters and public methods form the JS API (async methods return a Promise;
// call any method synchronously via chrome.webview.hostObjects.sync.dotnet). names are case-insensitive.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class HostApi(WebViewWindow window) : DispatchObject
{
    // host-object members are invoked on the instance by the WebView2 bridge (BindingFlags.Instance),
    // so they must stay instance members to appear in the JS API even when they don't touch instance state.
#pragma warning disable CA1822 // Mark members as static

    // --- properties: read from JS as dotnet.machineName, dotnet.architecture, ... ---
    public string MachineName => Environment.MachineName;
    public string UserName => Environment.UserName;
    public string Architecture => RuntimeInformation.ProcessArchitecture.ToString();
    public string Framework => RuntimeInformation.FrameworkDescription;
    public string Now => DateTime.Now.ToString("HH:mm:ss");

    // --- sync methods (primitives + arguments auto-converted from JS) ---
    public string Ping() => "pong from .NET";
    public int Add(int a, int b) => a + b;
    public string Upper(string? text) => text?.ToUpperInvariant() ?? string.Empty;

    // an array crosses the bridge as a JS array
    public int[] GetPrimes(int count)
    {
        var primes = new List<int>();
        for (var n = 2; primes.Count < Math.Clamp(count, 0, 1000); n++)
        {
            var isPrime = true;
            for (var d = 2; d * d <= n; d++)
            {
                if (n % d == 0)
                {
                    isPrime = false;
                    break;
                }
            }

            if (isPrime)
            {
                primes.Add(n);
            }
        }
        return [.. primes];
    }

    // complex data crosses as a JSON string (AOT-safe via source-gen); JS does JSON.parse
    public string GetSystemInfo()
    {
        var info = new SystemInfo(
            Environment.MachineName,
            Environment.UserName,
            Environment.OSVersion.VersionString,
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            Environment.ProcessorCount,
            Environment.Is64BitProcess,
            Environment.WorkingSet);
        return JsonSerializer.Serialize(info, HostJsonContext.Default.SystemInfo);
    }

    // --- async methods: return a real JS Promise (awaited via the private-interface continuation) ---
    public async Task<string> EchoAsync(string text)
    {
        await Task.Delay(150);
        return $".NET echoes: {text}";
    }

    public async Task<int> FactorialAsync(int n)
    {
        await Task.Delay(120);
        var result = 1;
        for (var i = 2; i <= Math.Clamp(n, 0, 20); i++)
        {
            result *= i;
        }
        return result;
    }

    // exceptions thrown in .NET surface to JS (rejected promise / thrown from the sync proxy)
    public string Fail(string? message) => throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "boom from .NET" : message);

    // a real native action driven from JS: open a URL in the user's default browser
    public bool OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = url });
        return true;
    }

    // bidirectional: .NET pushes to JS. the bridge invokes this on the UI thread and the awaits
    // resume there (window sync context), so ExecuteScript is safe. calls window.aotrinoTick(i) each
    // second, then window.aotrinoTick(0) when done.
    public async Task<int> CountdownAsync(int seconds)
    {
        var n = Math.Clamp(seconds, 1, 10);
        for (var i = n; i > 0; i--)
        {
            window.ExecuteScript($"window.aotrinoTick && window.aotrinoTick({i});");
            await Task.Delay(1000);
        }

        window.ExecuteScript("window.aotrinoTick && window.aotrinoTick(0);");
        return n;
    }

    // captured JS console output (see the console.* override in index.html) routes to the app's
    // overridable trace methods (AOTrinoApplication.Trace*), by level
    public void OnConsoleLog(string? level, string? message)
    {
        var app = AOTrinoApplication.Current;
        if (app == null)
            return;

        var text = $"[js] {message}";
        switch (level)
        {
            case "warn": app.TraceWarning(text); break;
            case "error": app.TraceError(text); break;
            case "debug": app.TraceVerbose(text); break;
            default: app.TraceInfo(text); break;
        }
    }

    // quit gracefully the whole app from JS (posts WM_CLOSE to the window)
    public void Quit() => window.Close();

#pragma warning restore CA1822

    // AOT-safe Task<T> unwrap: enumerate the concrete Task<T> types this object returns (no reflection)
    protected override object? GetTaskResult(Task task) => task switch
    {
        Task<string> t => t.Result,
        Task<int> t => t.Result,
        _ => base.GetTaskResult(task),
    };
}
