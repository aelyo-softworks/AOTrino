namespace AOTrino.Samples.HostObjects;

// returned to JS as a JSON string by HostApi.GetSystemInfo (JSON.parse on the JS side).
public sealed record SystemInfo(
    string MachineName,
    string UserName,
    string Os,
    string Architecture,
    string Framework,
    int Processors,
    bool Is64Bit,
    long WorkingSet);
