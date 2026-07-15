namespace AOTrino.Samples.FluentUI.HelloWorld;

// the .NET half of the Fluent starter. small on purpose: this sample is about the front-end layer, and the
// bridge itself is covered by AOTrino.Samples.HostObjects and docs/BRIDGE.md.
// note there is no GetTaskResult override: Task<string> is one of the types the bridge already knows.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class FluentApi(WebViewWindow window) : DispatchObject
{
    // host-object members are invoked on the instance by the WebView2 bridge (BindingFlags.Instance),
    // so they must stay instance members to appear in the JS API even when they don't touch instance state.
#pragma warning disable CA1822 // Mark members as static

    // one native call the page can ask anything of, instead of a property per value baked into C#.
    // the page passes "USERNAME" or "COMPUTERNAME"; nothing about the user or the machine is compiled into
    // the bundle, and adding a variable needs no .NET change at all.
    // a real app should think about what it exposes here: this hands the page every variable in the process
    // environment, which is fine for local content you wrote and a poor idea for anything you didn't.
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string Framework => RuntimeInformation.FrameworkDescription;
    public string AOTrinoVersion => AOTrinoApplication.Current!.AOTrinoVersion;
    public string WebView2Version => AOTrinoApplication.Current!.WebView2Version;

    // slow on purpose, so the Fluent Spinner has something to spin for
    public async Task<string> GreetAsync(string name)
    {
        await Task.Delay(700);
        var who = string.IsNullOrWhiteSpace(name) ? "stranger" : name.Trim();
        return $"Hello {who}, from .NET on {Environment.MachineName}.";
    }

#pragma warning restore CA1822

    public void Quit() => window.Close();
}
