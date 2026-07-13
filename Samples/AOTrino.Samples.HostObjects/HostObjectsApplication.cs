using System.Runtime.CompilerServices;

namespace AOTrino.Samples.HostObjects;

// shows that AOTrinoApplication's trace methods are overridable: forwards to the base (DirectN
// Application.Trace*) and, in --selftest mode, also captures every trace line to a file so the
// batch harness can confirm captured JS console output reached .NET.
public class HostObjectsApplication : AOTrinoApplication
{
    private static readonly bool _selfTest = Environment.GetCommandLineArgs().Contains("--selftest");
    private static readonly string _logPath = Path.Combine(Path.GetTempPath(), "aotrino-trace-selftest.log");

    public override void TraceInfo(object? message = null, [CallerMemberName] string? methodName = null) { base.TraceInfo(message); Capture("info", message); }
    public override void TraceWarning(object? message = null, [CallerMemberName] string? methodName = null) { base.TraceWarning(message); Capture("warn", message); }
    public override void TraceError(object? message = null, [CallerMemberName] string? methodName = null) { base.TraceError(message); Capture("error", message); }
    public override void TraceVerbose(object? message = null, [CallerMemberName] string? methodName = null) { base.TraceVerbose(message); Capture("verbose", message); }

    private static void Capture(string level, object? message)
    {
        if (!_selfTest)
            return;

        try { File.AppendAllText(_logPath, $"[{level}] {message}{Environment.NewLine}"); }
        catch { /* best effort */ }
    }
}
