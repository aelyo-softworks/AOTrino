namespace AOTrino.Samples.React.Dashboard;

// the .NET half of the dashboard sample: what the host knows about the machine and about itself.
// the members split into two kinds on purpose. the machine/version ones are fixed and read once; the
// process ones move on every read, which is what gives useHostProperties' refresh() and auto-refresh
// something real to show. AnalyzeAsync is slow and Fail throws, driving useHostCall's pending and error.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class DashboardApi(WebViewWindow window) : DispatchObject
{
    private const int _bytesPerMegabyte = 1024 * 1024;

    // app uptime, not the machine's: TickCount64 at the moment this object was built
    private readonly long _startTick = Environment.TickCount64;

    // host-object members are invoked on the instance by the WebView2 bridge (BindingFlags.Instance),
    // so they must stay instance members to appear in the JS API even when they don't touch instance state.
#pragma warning disable CA1822 // Mark members as static

    // --- the machine: fixed for the life of the app ---
    public string MachineName => Environment.MachineName;
    public string UserName => Environment.UserName;
    public string OperatingSystem => RuntimeInformation.OSDescription;
    public string Architecture => RuntimeInformation.ProcessArchitecture.ToString();
    public int ProcessorCount => Environment.ProcessorCount;

    // --- versions: also fixed ---
    public string Framework => RuntimeInformation.FrameworkDescription;
    public string AOTrinoVersion => AOTrinoApplication.Current!.AOTrinoVersion;
    public string WebView2Version => AOTrinoApplication.Current!.WebView2Version;
    public int ProcessId => Environment.ProcessId;

#pragma warning restore CA1822

    // --- the process: these move between reads ---
    public string Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64 - _startTick).ToString(@"hh\:mm\:ss");

#pragma warning disable CA1822

    // what the OS says the process holds, versus what the managed heap actually uses
    public string WorkingSet => $"{Environment.WorkingSet / _bytesPerMegabyte:N1} MB";
    public string ManagedHeap => $"{GC.GetTotalMemory(false) / (double)_bytesPerMegabyte:N1} MB";
    public int Collections => GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

    public int ThreadCount
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            return process.Threads.Count;
        }
    }

    // slow on purpose: drives useHostCall's `pending`
    public async Task<string> AnalyzeAsync(string text)
    {
        await Task.Delay(900);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return $"{words} word(s), {text.Length} char(s), analyzed by .NET";
    }

    // throws on purpose: crosses the bridge as a rejected promise and drives useHostCall's `error`
    public string Fail() => throw new InvalidOperationException("this .NET method always throws");

    // forces a full collection, so the managed-heap and collection counters visibly move
    public string CollectGarbage()
    {
        var before = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var after = GC.GetTotalMemory(true);
        return $"freed {(before - after) / (double)_bytesPerMegabyte:N1} MB";
    }

#pragma warning restore CA1822

    // closes the window (and the app) from the title bar
    public void Quit() => window.Close();
}
