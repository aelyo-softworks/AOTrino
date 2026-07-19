namespace AOTrino.Samples.React.HelloWorld;

// the .NET half of the sample: a host object the React front end calls through @aotrino/client.
// AOTrino.Samples.HostObjects is the exhaustive tour of the bridge (arrays, JSON source-gen, exceptions,
// .NET->JS push), this one stays small on purpose and only shows what the typed client wraps:
// a property, a sync method, an async method and a native action.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class DemoApi(WebViewWindow window) : DispatchObject
{
    // host-object members are invoked on the instance by the WebView2 bridge (BindingFlags.Instance),
    // so they must stay instance members to appear in the JS API even when they don't touch instance state.
#pragma warning disable CA1822 // Mark members as static.

    // a property: read from TS as `await api.machineName` (property reads cross the bridge as Promises too).
    public string MachineName => Environment.MachineName;
    public string Framework => RuntimeInformation.FrameworkDescription;

    // the AOTrino SDK's informational version (it carries the build timestamp, so it changes every rebuild) and the version of the WebView2 runtime actually rendering this page.
    public string AOTrinoVersion => AOTrinoApplication.Current!.AOTrinoVersion;
    public string WebView2Version => AOTrinoApplication.Current!.WebView2Version;

    public string Ping() => "pong from .NET";
    public int Add(int a, int b) => a + b;

    // an async method surfaces to JS as a real Promise.
    public async Task<string> EchoAsync(string text)
    {
        await Task.Delay(150);
        return $".NET echoes: {text}";
    }

#pragma warning restore CA1822

    // closes the window (and the app) from the page's custom title bar.
    public void Quit() => window.Close();
}
