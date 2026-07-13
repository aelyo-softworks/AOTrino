namespace AOTrino;

// the AOTrino application: a composition app plus the WebView2 + WebRoot bootstrap.
// owns the per-app services (Paths, WebRoot) so the SDK exposes no process-global static state.
public partial class AOTrinoApplication : CompositionApplication
{
    // transparent, so the page paints its own background through the composition surface
    private const string _defaultBackgroundColor = "00000000";
    private const int _webView2MissingExitCode = 1;

    // https://developer.microsoft.com/microsoft-edge/webview2/ evergreen bootstrapper
    public const string WebView2DownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public AOTrinoApplication(Assembly? appAssembly = null)
    {
        // continuations after 'await' resume on the window message loop
        WindowSynchronizationContext.Install();

        var assembly = appAssembly ?? Assembly.GetEntryAssembly() ?? typeof(AOTrinoApplication).Assembly;
        Paths = CreatePaths() ?? throw new InvalidOperationException($"CreatePaths returned null for assembly {assembly.FullName}");
        WebRoot = CreateWebRoot(assembly, Paths) ?? throw new InvalidOperationException($"CreateWebRoot returned null for assembly {assembly.FullName} and paths {Paths}");

        // the native WebView2 loader is embedded in the AOTrino assembly (this one); the matching architecture is extracted
        WebView2Utilities.Initialize(typeof(AOTrinoApplication).Assembly);

        // no AOTrino app can run without the WebView2 runtime: this closes the process if it's missing (overridable)
        var version = WebView2Utilities.GetAvailableCoreWebView2BrowserVersionString();
        CheckWebView2Runtime(version);
        WebView2Version = version!;

        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", _defaultBackgroundColor);
        _ = WebRoot.EnsureFilesAsync();
    }

    public static new AOTrinoApplication? Current => Application.Current as AOTrinoApplication;

    public AOTrinoPaths Paths { get; }
    public WebRoot WebRoot { get; }

    // the installed WebView2 runtime version; guaranteed present (the app cannot start otherwise)
    public string WebView2Version { get; }

    // the AOTrino SDK version (this assembly)
    public string AOTrinoVersion { get; } = typeof(AOTrinoApplication).Assembly.GetInformationalVersion()!;

    protected virtual AOTrinoPaths CreatePaths() => new(Assembly.GetEntryAssembly() ?? typeof(AOTrinoApplication).Assembly);
    protected virtual WebRoot CreateWebRoot(Assembly assembly, AOTrinoPaths paths) => new(assembly, paths);

    // application-level tracing. default forwards to DirectN's static Application.Trace*;
    // override to redirect logs (file, telemetry, on-screen console, ...). captured JS console output routes here.
    public virtual void TraceInfo(object? message = null) => Application.TraceInfo(message);
    public virtual void TraceWarning(object? message = null) => Application.TraceWarning(message);
    public virtual void TraceError(object? message = null) => Application.TraceError(message);
    public virtual void TraceVerbose(object? message = null) => Application.TraceVerbose(message);

    // called at startup with the detected WebView2 runtime version (null/empty when absent).
    // default behavior: show a task dialog with a download link, then force-close the process.
    // override to change how a missing runtime is handled.
    protected virtual void CheckWebView2Runtime(string? version)
    {
        if (!string.IsNullOrWhiteSpace(version))
            return;

        TraceInfo(Res.WebView2NotFound);
        ShowWebView2DownloadDialog();
        Environment.Exit(_webView2MissingExitCode);
    }

    private void ShowWebView2DownloadDialog()
    {
        unsafe
        {
            delegate* unmanaged<HWND, uint, WPARAM, LPARAM, nint, HRESULT> callback = &TaskDialogCallback;
            var td = new TaskDialog
            {
                Title = Paths.AppTitle,
                MainIcon = new HICON { Value = TaskDialog.TD_WARNING_ICON },
                Flags = TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS,
                MainInstruction = Res.WebView2NotFound,
                Content = string.Format(Res.WebView2Link, WebView2DownloadUrl),
                Callback = (nint)callback,
            };
            td.Show(HWND.Null, false);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = nameof(TaskDialogCallback))]
    private static HRESULT TaskDialogCallback(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam, nint lpRefData)
    {
        if (msg == (uint)TASKDIALOG_NOTIFICATIONS.TDN_HYPERLINK_CLICKED)
        {
            Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = WebView2DownloadUrl });
            return DirectNConstants.E_FAIL;
        }
        return DirectNConstants.S_OK;
    }
}
