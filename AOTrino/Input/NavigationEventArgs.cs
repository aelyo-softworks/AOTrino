namespace AOTrino.Input;

public class NavigationEventArgs(
    ulong id,
    string uri,
    bool isUserInitiated,
    bool isRedirected) : CancelEventArgs
{
    public ulong Id { get; } = id;
    public string Uri { get; } = uri;
    public bool IsUserInitiated { get; } = isUserInitiated;
    public bool IsRedirected { get; } = isRedirected;
    public NavigationEventType Type { get; internal set; }
    public COREWEBVIEW2_WEB_ERROR_STATUS WebErrorStatus { get; internal set; }
    public bool IsSuccess { get; internal set; }

    public override string ToString() => $"{Id}:{Type}:{Uri})";
}
