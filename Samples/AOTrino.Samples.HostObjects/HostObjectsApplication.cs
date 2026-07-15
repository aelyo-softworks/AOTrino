namespace AOTrino.Samples.HostObjects;

// demonstrates that AOTrinoApplication's trace methods are overridable: here we simply tag every line before
// forwarding to the base (which writes to DirectN's Application.Trace*). captured JS console output
// (see HostApi.OnConsoleLog) flows through these, so console.log in the page shows up tagged in the .NET trace.
public class HostObjectsApplication : AOTrinoApplication
{
    private const string _tag = "[HostObjects] ";

    public override void Trace(TraceLevel level, object? message = null, [CallerMemberName] string? methodName = null) => base.Trace(level, _tag + message, methodName);
}
