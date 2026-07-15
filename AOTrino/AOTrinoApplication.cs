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

        CheckErrorReporting();

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
    public new void TraceInfo(object? message = null, [CallerMemberName] string? methodName = null) => Trace(TraceLevel.Info, message, methodName);
    public new void TraceWarning(object? message = null, [CallerMemberName] string? methodName = null) => Trace(TraceLevel.Warning, message, methodName);
    public new void TraceError(object? message = null, [CallerMemberName] string? methodName = null) => Trace(TraceLevel.Error, message, methodName);
    public new void TraceVerbose(object? message = null, [CallerMemberName] string? methodName = null) => Trace(TraceLevel.Verbose, message, methodName);
    public virtual new void Trace(TraceLevel level, object? message = null, [CallerMemberName] string? methodName = null) => Application.Trace(level, message, methodName);

    // errors are reported in a task dialog, and TaskDialogIndirect only exists in comctl32 version 6,
    // which a process only gets from a manifest (AOTrino ships one - see build\AOTrino.app.manifest.
    // so this is about apps that bring their own and leave Common-Controls out of it).
    // without it the first error dies inside the error reporter, and what the user is shown is "Unable to find an  entry point named 'TaskDialogIndirect'",
    // a complaint about the messenger, while the actual exception is only in the trace.
    // so: check once, up front, and if the dialog can't work, report through MessageBox instead.
    // the manifest is still the fix; this just makes the app say what went wrong while you find that out.
    protected virtual void CheckErrorReporting()
    {
        if (IsTaskDialogAvailable())
            return;

        TraceWarning("comctl32 version 6 is not active: this application's manifest doesn't reference Microsoft.Windows.Common-Controls 6.0.0.0. Errors will be reported in a plain message box, and the UI is not visual-styled.");
        ShowFatalErrorFunc = ShowFatalErrorWithoutTaskDialog;
    }

    // comctl32 v6 exports TaskDialogIndirect; 5.82 (what an unmanifested process gets) doesn't.
    // LoadLibrary rather than GetModuleHandle: at this point in startup nothing has needed comctl32 yet, and the activation context decides which version this resolves to.
    private static bool IsTaskDialogAvailable()
    {
        var module = DirectNFunctions.LoadLibraryW(PWSTR.From("comctl32.dll"));
        if (module.Value == 0)
            return false;

        return DirectNFunctions.GetProcAddress(module, PSTR.From("TaskDialogIndirect")) != 0;
    }

    private static bool ShowFatalErrorWithoutTaskDialog(HWND hwnd)
    {
        var errors = GetErrors(true);
        if (errors.Count == 0)
            return false;

        var text = string.Join(Environment.NewLine + Environment.NewLine, errors.Select(e => e.GetInterestingExceptionMessage()));
        MessageBox.Show(hwnd, text, GetTitle(hwnd), MESSAGEBOX_STYLE.MB_ICONSTOP);
        return true;
    }

    // called at startup with the detected WebView2 runtime version (null/empty when absent).
    // default behavior: show a task dialog with a download link, then force-close the process.
    // override to change how a missing runtime is handled.
    protected virtual void CheckWebView2Runtime(string? version)
    {
        if (!string.IsNullOrWhiteSpace(version))
            return;

        // invariant English on purpose: Res is for what the user reads (the dialog below), not for the log.
        // a trace line that changes with the machine's locale is one support can't grep for.
        TraceInfo("WebView2 runtime not found.");
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
