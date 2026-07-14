namespace AOTrino;

// hosting-agnostic WebView2 window base. it owns the environment, the WebView, navigation, host objects,
// scripts, the shared-buffer transport and all window/input plumbing — everything except HOW the WebView is
// hosted. the two hosting models are concrete subclasses: CompositionWebViewWindow (the WebView is one visual
// in a Windows.UI.Composition tree) and HwndWebViewWindow (the WebView is a classic child window). the only
// differences are CreateController (below), input forwarding and the window's redirection style.
public abstract partial class WebViewWindow : D3D11SwapChainWindow
{
    private readonly bool[] _capturedButtons = new bool[Enum.GetNames<MouseButton>().Length];
    private readonly Dictionary<ulong, NavigationEventArgs> _navigationEvents = [];
    // pointer ids whose gesture started over the WebView; read by the composition host to keep forwarding them
    private protected readonly HashSet<uint> _pointerIdsStartingInWebView = [];
    private ComObject<ICoreWebView2Environment12>? _environment;
    private ComObject<ICoreWebView2_17>? _webView;
    private ICoreWebView2Controller? _baseController; // the controller as its common base type (bounds/focus); owned/disposed by the subclass
    private bool _mouseTracking;
    private bool _hostObjectHelperInstalled;
    private bool _sharedRuntimeReady;
    private WebView2.EventRegistrationToken _webMessageReceivedToken;
    private ulong _lastPointerDownTime;
    private int _lastPointerDownPositionX = int.MinValue;
    private int _lastPointerDownPositionY = int.MinValue;
    private WebView2.EventRegistrationToken _navigationStarting;
    private WebView2.EventRegistrationToken _navigationCompleted;

    public event EventHandler<MouseEventArgs>? MouseMove;
    public event EventHandler<MouseEventArgs>? MouseLeave;
    public event EventHandler<MouseEventArgs>? MouseHover;
    public event EventHandler<MouseWheelEventArgs>? MouseWheel;
    public event EventHandler<MouseButtonEventArgs>? MouseButtonDown;
    public event EventHandler<MouseButtonEventArgs>? MouseButtonUp;
    public event EventHandler<MouseButtonEventArgs>? MouseButtonDoubleClick;
    public event EventHandler<PointerActivateEventArgs>? PointerActivate;
    public event EventHandler<PointerEnterEventArgs>? PointerEnter;
    public event EventHandler<PointerLeaveEventArgs>? PointerLeave;
    public event EventHandler<PointerWheelEventArgs>? PointerWheel;
    public event EventHandler<PointerPositionEventArgs>? PointerUpdate;
    public event EventHandler<PointerContactChangedEventArgs>? PointerContactChanged;
    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler<KeyEventArgs>? KeyUp;
    public event EventHandler<KeyPressEventArgs>? KeyPress;
    public event EventHandler? MonitorChanged;
    public event EventHandler<NavigationEventArgs>? NavigationStarting;
    public event EventHandler<NavigationEventArgs>? NavigationCompleted;
    // raw JSON of messages posted from the page (window.__aotrino.post / chrome.webview.postMessage)
    public event EventHandler<ValueEventArgs<string>>? WebMessageJsonReceived;

    protected WebViewWindow(
        string? title = null,
        WINDOW_STYLE style = WINDOW_STYLE.WS_THICKFRAME,
        WINDOW_EX_STYLE extendedStyle = 0,
        RECT? rect = null)
        : base(title, style: style, extendedStyle: extendedStyle, rect: rect)
    {
        InvalidateOnTick = false; // the WebView renders itself; we don't tick a swap chain

        MonitorHandle = DirectNFunctions.MonitorFromWindow(Handle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        if (IsFullScreen)
        {
            SetCorner(DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND);
        }
        else
        {
            SetCorner(DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);
        }

        var options = GetEnvironmentOptions();
        WebView2.Functions.CreateCoreWebView2EnvironmentWithOptions(PWSTR.Null, PWSTR.From(AOTrinoApplication.Current?.Paths.WebView2UserDataPath), options!,
            new CoreWebView2CreateCoreWebView2EnvironmentCompletedHandler((result, envObj) =>
            {
                try
                {
                    options?.Dispose();
                    var env3 = (ICoreWebView2Environment12)envObj;
                    _environment = new ComObject<ICoreWebView2Environment12>(env3);

                    // the one hosting-specific step: create the composition or HWND controller. the subclass
                    // sets the WebView + base controller (SetWebViewController) then calls onReady.
                    CreateController(env3, () =>
                    {
                        WireNavigationEvents();
                        ControllerCreated();
                    });
                }
                catch (Exception ex)
                {
                    Application.AddError(ex, true);
                }
            })).ThrowOnError();
    }

    protected override bool NeedsSwapChain => false;

    protected ComObject<ICoreWebView2_17>? WebView => _webView;
    protected ComObject<ICoreWebView2Environment12>? Environment => _environment;
    protected ICoreWebView2Controller? BaseController => _baseController;

    // for SharedBuffer (same assembly) to reach the environment/webview without exposing them publicly
    internal ComObject<ICoreWebView2Environment12>? SharedEnvironment => _environment;
    internal ComObject<ICoreWebView2_17>? SharedWebView => _webView;

    public HMONITOR MonitorHandle { get; private set; }
    public bool IsFullScreen => GetFullScreenBounds() == WindowRect;
    public virtual bool CanChangeCursor { get; set; } = true;
    public virtual bool SendDoubleClicks { get; set; } // WebView2 doesn't seem to care (chrome uses UP & DOWN events by itself)

    // create the WebView2 controller (composition or HWND). the implementation must, once its controller is
    // ready, call SetWebViewController(controller, coreWebView2) and then invoke onControllerReady.
    protected abstract void CreateController(ICoreWebView2Environment12 environment, Action onControllerReady);

    // forward a mouse event to a composition-hosted WebView (which gets no OS input). the HWND host leaves this
    // a no-op — its child window receives input directly.
    protected virtual void ForwardMouseInput(COREWEBVIEW2_MOUSE_EVENT_KIND kind, COREWEBVIEW2_MOUSE_EVENT_VIRTUAL_KEYS keys, uint data, POINT point) { }

    // forward a pointer event to a composition-hosted WebView. return true if handled (consumed). HWND: no-op.
    protected virtual bool TryForwardPointerInput(uint msg, WPARAM wParam, LPARAM lParam) => false;

    // called by a subclass from CreateController once its controller and core WebView are available
    protected void SetWebViewController(ICoreWebView2Controller controller, ICoreWebView2 webView)
    {
        _baseController = controller;
        _webView = new ComObject<ICoreWebView2_17>(webView);
    }

    // stop routing bounds/focus to the controller. a subclass MUST call this before disposing its controller,
    // because disposing it raises teardown focus/size messages that would otherwise hit a disposed COM object.
    protected void DetachController() => _baseController = null;

    private void WireNavigationEvents()
    {
        var webView = _webView;
        if (webView == null)
            return;

        webView.Object.add_NavigationStarting(new CoreWebView2NavigationStartingEventHandler((sender, args) =>
        {
            var id = 0UL;
            args.get_NavigationId(ref id).ThrowOnError();
            args.get_Uri(out var uri).ThrowOnError();
            using var pwstr = new Pwstr(uri.Value);

            var isUserInitiated = BOOL.FALSE;
            args.get_IsUserInitiated(ref isUserInitiated).ThrowOnError();

            var isRedirected = BOOL.FALSE;
            args.get_IsRedirected(ref isRedirected).ThrowOnError();

            var e = new NavigationEventArgs(id, uri.ToString()!, isUserInitiated, isRedirected);
            _navigationEvents[id] = e;

            OnNavigationStarting(this, e);
            if (e.Cancel)
            {
                args.put_Cancel(true).ThrowOnError();
            }
        }), ref _navigationStarting).ThrowOnError();

        webView.Object.add_NavigationCompleted(new CoreWebView2NavigationCompletedEventHandler((sender, args) =>
        {
            var id = 0UL;
            args.get_NavigationId(ref id).ThrowOnError();

            var status = COREWEBVIEW2_WEB_ERROR_STATUS.COREWEBVIEW2_WEB_ERROR_STATUS_UNKNOWN;
            args.get_WebErrorStatus(ref status).ThrowOnError();

            var success = BOOL.FALSE;
            args.get_IsSuccess(ref success).ThrowOnError();

            if (_navigationEvents.TryGetValue(id, out var e))
            {
                e.Type = NavigationEventType.NavigationCompleted;
                e.WebErrorStatus = status;
                e.IsSuccess = success;

                _navigationEvents.Remove(id);
                OnNavigationCompleted(this, e);
            }
        }), ref _navigationCompleted).ThrowOnError();
    }

    protected virtual CoreWebView2EnvironmentOptions? GetEnvironmentOptions() => null;
    protected virtual RECT? GetCaptionRect() => null;
    protected virtual void ControllerCreated()
    {
    }

    public virtual async Task NavigateToWebRootAsync()
    {
        var app = AOTrinoApplication.Current;
        if (app != null)
        {
            await app.WebRoot.EnsureFilesAsync();
        }

        var url = app?.WebRoot.IndexFilePath;
        if (!string.IsNullOrEmpty(url))
        {
            Navigate(url);
        }
    }

    public virtual RECT GetFullScreenBounds()
    {
        var monitor = GetMonitor(MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST)!;
        var bounds = monitor.Bounds;
        bounds = bounds.Inflate(1, 1, 1, 1); // work around a weird 1px gap that would otherwise appear on the right and bottom edges, probably due to some rounding issue in the DWM when using the exact monitor size
        return bounds;
    }

    protected override bool OnFocusChanged(bool setOrKill)
    {
        if (setOrKill)
        {
            _baseController?.MoveFocus(COREWEBVIEW2_MOVE_FOCUS_REASON.COREWEBVIEW2_MOVE_FOCUS_REASON_PROGRAMMATIC);
            return true;
        }
        return base.OnFocusChanged(setOrKill);
    }

    public virtual void MaximizeOrRestore()
    {
        if (IsZoomed)
        {
            Show(SHOW_WINDOW_CMD.SW_RESTORE);
        }
        else
        {
            Show(SHOW_WINDOW_CMD.SW_MAXIMIZE);
        }
    }

    public virtual void Navigate(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var webView = _webView ?? throw new InvalidOperationException();
        webView.Object.Navigate(PWSTR.From(url)).ThrowOnError();
    }

    public virtual void NavigateToString(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var webView = _webView ?? throw new InvalidOperationException();
        webView.Object.NavigateToString(PWSTR.From(html)).ThrowOnError();
    }

    // registers a JS-callable host object: AddHostObject("dotnet", obj) exposes it as
    // chrome.webview.hostObjects.dotnet (async) and chrome.webview.hostObjects.sync.dotnet (sync).
    // call after the controller is created (e.g. from AOTrinoWindow.RegisterHostObjects), before navigation.
    public virtual void AddHostObject(string name, DispatchObject hostObject)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(hostObject);
        var webView = _webView ?? throw new InvalidOperationException("The WebView2 controller is not ready yet.");

        EnsureHostObjectHelper(webView);

        // wrap the host object's IUnknown in a VARIANT and register it under 'name'
        ComObject.WithComInstance(hostObject, unk =>
        {
            using var variant = new Variant(unk, VARENUM.VT_UNKNOWN);
            var detached = variant.Detached;
            webView.Object.AddHostObjectToScript(PWSTR.From(name), ref detached).ThrowOnError();
        }, true);
    }

    // full .NET Task / Task<T> support for host objects, via undocumented private WebView2 interfaces (best effort)
    private void EnsureHostObjectHelper(ComObject<ICoreWebView2_17> webView)
    {
        if (_hostObjectHelperInstalled)
            return;

        _hostObjectHelperInstalled = true;
        if (webView.Object is ICoreWebView2PrivatePartial partial)
        {
            partial.AddHostObjectHelper(new WebViewHostObjectHelper()).ThrowOnError();
            DispatchObject.ContinueOnAsync = true;
            DispatchObject.OneStepInvoke = true;
        }
    }

    // creates a named shared-memory channel to the page (generic byte transport, .NET <-> JS).
    // write to the returned SharedBuffer.Pointer; read it in JS via window.__aotrino.getBuffer(name).
    // see AOTrino.Graphics for a Direct2D -> WebGL surface built on top of this.
    public virtual SharedBuffer CreateSharedBuffer(string name, SharedBufferAccess access = SharedBufferAccess.ReadOnly)
    {
        EnsureSharedRuntime();
        return new SharedBuffer(this, name, access);
    }

    // runs a script at the start of every document (and immediately on the current one)
    public virtual void AddStartupScript(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var webView = _webView ?? throw new InvalidOperationException("The WebView2 controller is not ready yet.");
        webView.Object.AddScriptToExecuteOnDocumentCreated(PWSTR.From(script), new CoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler((error, id) => { })).ThrowOnError();
        ExecuteScript(script, throwOnError: false);
    }

    // AddStartupScript from an embedded text resource (a .js file). keeps scripts out of C# string literals:
    // drop the file under the project's Scripts\ folder and pass its bare file name, e.g.
    // AddStartupScriptResource(typeof(MyWindow).Assembly, "BrowserChrome.js").
    public void AddStartupScriptResource(Assembly assembly, string resourceName) => AddStartupScript(EmbeddedResource.Load(assembly, resourceName));

    protected virtual void OnWebMessageJsonReceived(object sender, ValueEventArgs<string> json)
    {
        HandleWindowCommand(json.Value);
        WebMessageJsonReceived?.Invoke(this, json);
    }

    // starts a native window move (as if the title bar was grabbed). call while a mouse button is down,
    // e.g. from a JS drag region via window.__aotrino.dragWindow().
    public virtual void BeginDrag()
    {
        DirectNFunctions.ReleaseCapture();
        DirectNFunctions.SendMessageW(Handle, MessageDecoder.WM_NCLBUTTONDOWN, new WPARAM { Value = (nuint)HT.HTCAPTION }, new LPARAM());
    }

    // built-in window controls, driven from JS by window.__aotrino.dragWindow()/closeWindow()/minimizeWindow()/maximizeWindow()
    private void HandleWindowCommand(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

            if (!root.TryGetProperty("__aotrino", out var kind) || kind.GetString() != "window-command")
                return;

            if (!root.TryGetProperty("command", out var cmd))
                return;

            switch (cmd.GetString())
            {
                case "drag": BeginDrag(); break;
                case "close": Close(); break;
                case "minimize": Show(SHOW_WINDOW_CMD.SW_MINIMIZE); break;
                case "maximize": MaximizeOrRestore(); break;
            }
        }
        catch
        {
            // not a window command
        }
    }

    protected virtual void EnsureSharedRuntime()
    {
        if (_sharedRuntimeReady)
            return;

        var webView = _webView ?? throw new InvalidOperationException("The WebView2 controller is not ready yet.");
        _sharedRuntimeReady = true;

        // the generic __aotrino runtime, on every future document and the current one
        AddStartupScript(SharedBuffer.Runtime);

        webView.Object.add_WebMessageReceived(new CoreWebView2WebMessageReceivedEventHandler((sender, args) =>
        {
            if (args.get_WebMessageAsJson(out var json).IsError)
                return;

            using var pwstr = new Pwstr(json.Value);
            var text = json.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                OnWebMessageJsonReceived(this, new ValueEventArgs<string>(text));
            }
        }), ref _webMessageReceivedToken).ThrowOnError();
    }

    public virtual Task<T?> ExecuteScript<T>(string script, JsonTypeInfo<T> typeInfo, bool throwOnError = true)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(typeInfo);
        var webView = _webView ?? throw new InvalidOperationException();
        return webView.Object.ExecuteScript(script, typeInfo, throwOnError: throwOnError);
    }

    public virtual Task<string?> ExecuteScriptAsJson(string script, bool throwOnError = true)
    {
        ArgumentNullException.ThrowIfNull(script);
        var webView = _webView ?? throw new InvalidOperationException();
        return webView.Object.ExecuteScriptAsJon(script, throwOnError: throwOnError);
    }

    public virtual HRESULT ExecuteScript(string script, bool throwOnError = true)
    {
        ArgumentNullException.ThrowIfNull(script);
        var webView = _webView ?? throw new InvalidOperationException();
        return webView.Object.ExecuteScript(PWSTR.From(script), new CoreWebView2ExecuteScriptCompletedHandler((error, result) =>
        {
            if (error.IsError)
            {
                Application.AddError(new Exception("Script execution failed: " + error), true);
            }
        })).ThrowOnError(throwOnError);
    }

    protected virtual void OnPointerWheel(object? sender, PointerWheelEventArgs e) => PointerWheel?.Invoke(sender, e);
    protected virtual void OnPointerLeave(object? sender, PointerLeaveEventArgs e) => PointerLeave?.Invoke(sender, e);
    protected virtual void OnPointerEnter(object? sender, PointerEnterEventArgs e) => PointerEnter?.Invoke(sender, e);
    protected virtual void OnPointerActivate(object? sender, PointerActivateEventArgs e) => PointerActivate?.Invoke(sender, e);
    protected virtual void OnPointerUpdate(object? sender, PointerPositionEventArgs e) => PointerUpdate?.Invoke(sender, e);
    protected virtual void OnPointerContactChanged(object? sender, PointerContactChangedEventArgs e) => PointerContactChanged?.Invoke(sender, e);
    protected virtual void OnMouseMove(object? sender, MouseEventArgs e) => MouseMove?.Invoke(sender, e);
    protected virtual void OnMouseLeave(object? sender, MouseEventArgs e) => MouseLeave?.Invoke(sender, e);
    protected virtual void OnMouseHover(object? sender, MouseEventArgs e) => MouseHover?.Invoke(sender, e);
    protected virtual void OnMouseWheel(object? sender, MouseWheelEventArgs e) => MouseWheel?.Invoke(sender, e);
    protected virtual void OnMouseButtonDown(object? sender, MouseButtonEventArgs e) => MouseButtonDown?.Invoke(sender, e);
    protected virtual void OnMouseButtonUp(object? sender, MouseButtonEventArgs e) => MouseButtonUp?.Invoke(sender, e);
    protected virtual void OnMouseButtonDoubleClick(object? sender, MouseButtonEventArgs e) => MouseButtonDoubleClick?.Invoke(sender, e);
    protected virtual void OnKeyDown(object? sender, KeyEventArgs e) => KeyDown?.Invoke(sender, e);
    protected virtual void OnKeyUp(object? sender, KeyEventArgs e) => KeyUp?.Invoke(sender, e);
    protected virtual void OnKeyPress(object? sender, KeyPressEventArgs e) => KeyPress?.Invoke(sender, e);
    protected virtual void OnMonitorChanged(object? sender, EventArgs e) => MonitorChanged?.Invoke(sender, e);
    protected virtual void OnNavigationStarting(object? sender, NavigationEventArgs e) => NavigationStarting?.Invoke(sender, e);
    protected virtual void OnNavigationCompleted(object? sender, NavigationEventArgs e) => NavigationCompleted?.Invoke(sender, e);

    protected override void OnExitSizeMove(object? sender, EventArgs e)
    {
        base.OnExitSizeMove(sender, e);
        UpdateMonitor();
    }

    protected override void OnPositionChanged(object? sender, ValueEventArgs<WINDOWPOS> e)
    {
        base.OnPositionChanged(sender, e);
        UpdateMonitor();
    }

    protected override void OnPositionChanging(object? sender, ValueEventArgs<WINDOWPOS> e)
    {
        base.OnPositionChanging(sender, e);
        UpdateMonitor();
    }

    protected virtual void UpdateMonitor()
    {
        var monitor = DirectNFunctions.MonitorFromWindow(Handle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        if (monitor.Value == MonitorHandle.Value)
            return;

        MonitorHandle = monitor;
        OnMonitorChanged(this, EventArgs.Empty);
    }

    protected unsafe internal virtual void SetCorner(DWM_WINDOW_CORNER_PREFERENCE corner)
    {
        // works only on Windows 11, does nothing on Windows 10, so we don't check error
        DirectNFunctions.DwmSetWindowAttribute(Handle, (uint)DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, (nint)(&corner), 4);
    }

    // apply a Windows 11 system backdrop material (Mica / Acrylic / Tabbed) behind the window. it shows through
    // wherever the window is transparent (a composition-hosted, transparent WebView); it makes no visible
    // difference behind an opaque HWND-hosted WebView. no-op / ignored on Windows 10.
    public unsafe void SetSystemBackdrop(DWM_SYSTEMBACKDROP_TYPE type)
    {
        var value = (int)type;
        DirectNFunctions.DwmSetWindowAttribute(Handle, (uint)DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, (nint)(&value), 4);
    }

    protected virtual void ClearBrowsingDataAll()
    {
        var wv = _webView.As<ICoreWebView2_13>();
        if (wv == null)
            return;

        wv.Object.get_Profile(out var objProfile);
        using var profile = new ComObject<ICoreWebView2Profile2>(objProfile);
        profile?.Object.ClearBrowsingDataAll(new CoreWebView2ClearBrowsingDataCompletedHandler(h => { }));
    }

    private void OnMouseMove(MouseEventArgs e)
    {
        OnMouseMove(this, e);
        if (e.Handled)
            return;

        var keys = AOTrinoExtensions.GetKeys(e.Keys, null);
        ForwardMouseInput(COREWEBVIEW2_MOUSE_EVENT_KIND.COREWEBVIEW2_MOUSE_EVENT_KIND_MOVE, keys, 0, e.Point);
    }

    private void OnMouseLeave(MouseEventArgs e)
    {
        OnMouseLeave(this, e);
        if (e.Handled)
            return;

        ForwardMouseInput(
            COREWEBVIEW2_MOUSE_EVENT_KIND.COREWEBVIEW2_MOUSE_EVENT_KIND_LEAVE,
            COREWEBVIEW2_MOUSE_EVENT_VIRTUAL_KEYS.COREWEBVIEW2_MOUSE_EVENT_VIRTUAL_KEYS_NONE,
            0,
            POINT.Zero);
    }

    private static uint XButtonData(MouseButton button) => button == MouseButton.X1 ? 1u : button == MouseButton.X2 ? 2u : 0u;

    private void OnMouseButtonDown(MouseButtonEventArgs e)
    {
        OnMouseButtonDown(this, e);
        if (e.Handled)
            return;

        var keys = AOTrinoExtensions.GetKeys(e.Keys, e.Button);
        var kind = e.Button.GetKind(AOTrinoExtensions.ButtonAction.Down);
        ForwardMouseInput(kind, keys, XButtonData(e.Button), e.Point);
    }

    private void OnMouseButtonUp(MouseButtonEventArgs e)
    {
        OnMouseButtonUp(this, e);
        if (e.Handled)
            return;

        var keys = AOTrinoExtensions.GetKeys(e.Keys, e.Button);
        var kind = e.Button.GetKind(AOTrinoExtensions.ButtonAction.Up);
        ForwardMouseInput(kind, keys, XButtonData(e.Button), e.Point);
    }

    private void OnMouseButtonDoubleClick(MouseButtonEventArgs e)
    {
        OnMouseButtonDoubleClick(this, e);
        if (e.Handled)
            return;

        var keys = AOTrinoExtensions.GetKeys(e.Keys, e.Button);
        var kind = e.Button.GetKind(AOTrinoExtensions.ButtonAction.DoubleClick);
        ForwardMouseInput(kind, keys, XButtonData(e.Button), e.Point);
    }

    private void OnMouseWheel(MouseWheelEventArgs e)
    {
        OnMouseWheel(this, e);
        if (e.Handled)
            return;

        var keys = AOTrinoExtensions.GetKeys(e.Keys, null);
        var kind = e.Orientation == Orientation.Horizontal
            ? COREWEBVIEW2_MOUSE_EVENT_KIND.COREWEBVIEW2_MOUSE_EVENT_KIND_HORIZONTAL_WHEEL
            : COREWEBVIEW2_MOUSE_EVENT_KIND.COREWEBVIEW2_MOUSE_EVENT_KIND_WHEEL;
        ForwardMouseInput(kind, keys, (uint)(e.Delta * DirectNConstants.WHEEL_DELTA), e.Point);
    }

    protected override LRESULT? WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        MouseButton button;
        switch (msg)
        {
            //https://learn.microsoft.com/en-us/windows/win32/dwm/customframe#appendix-c-hittestnca-function
            case MessageDecoder.WM_ACTIVATE:

                MARGINS margins;
                margins.cxLeftWidth = -1;
                margins.cxRightWidth = -1;
                margins.cyBottomHeight = -1;
                margins.cyTopHeight = -1;
                DirectNFunctions.DwmExtendFrameIntoClientArea(Handle, margins);
                break;

            case MessageDecoder.WM_NCCALCSIZE:
                if (wParam.Value.ToUInt32() != 0)
                {
                    if (IsZoomed)
                    {
                        var monitor = GetMonitor();
                        if (monitor != null)
                        {
                            unsafe
                            {
                                // this is a NCCALCSIZE_PARAMS but we only need the first RECT
                                *(RECT*)lParam.Value = monitor.WorkingArea;
                            }
                        }
                    }
                    return 0;
                }
                break;

            case MessageDecoder.WM_CREATE:
                var rc = WindowRect;
                SetWindowPos(HWND.Null, rc.left, rc.top, rc.Width, rc.Height, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
                break;

            case MessageDecoder.WM_NCHITTEST:
                var htn = HitTestWithoutWindowsFrame();
                if (htn.HasValue)
                    return new LRESULT((nint)htn.Value);

                HT? HitTestWithoutWindowsFrame()
                {
                    var rc = WindowRect;
                    var ncx = lParam.Value.SignedLOWORD();
                    var ncy = lParam.Value.SignedHIWORD();
                    var clix = ncx - rc.left;
                    var cliy = ncy - rc.top;
                    if (clix >= 0 && clix < rc.Width && cliy >= 0 && cliy <= rc.Height)
                    {
                        if (clix < AOTrinoExtensions.BorderWidth)
                        {
                            if (cliy <= AOTrinoExtensions.BorderHeight)
                                return HT.HTTOPLEFT;

                            if (cliy >= rc.Height - AOTrinoExtensions.BorderHeight)
                                return HT.HTBOTTOMLEFT;

                            return HT.HTLEFT;
                        }

                        if (clix > rc.Width - AOTrinoExtensions.BorderWidth)
                        {
                            if (cliy <= AOTrinoExtensions.BorderHeight)
                                return HT.HTTOPRIGHT;

                            if (cliy >= rc.Height - AOTrinoExtensions.BorderHeight)
                                return HT.HTBOTTOMRIGHT;

                            return HT.HTRIGHT;
                        }

                        if (cliy < AOTrinoExtensions.BorderHeight)
                            return HT.HTTOP;

                        if (cliy > rc.Height - AOTrinoExtensions.BorderHeight)
                            return HT.HTBOTTOM;

                        var caption = GetCaptionRect();
                        if (caption != null && caption.Value.Contains(clix, cliy))
                            return HT.HTCAPTION;
                    }
                    return null;
                }
                break;

            case MessageDecoder.WM_MOUSEMOVE:
                if (!_mouseTracking)
                {
                    unsafe
                    {
                        // https://learn.microsoft.com/en-us/windows/win32/learnwin32/other-mouse-operations#mouse-tracking-events-hover-and-leave
                        var tme = new TRACKMOUSEEVENT
                        {
                            cbSize = (uint)sizeof(TRACKMOUSEEVENT),
                            dwFlags = TRACKMOUSEEVENT_FLAGS.TME_LEAVE | TRACKMOUSEEVENT_FLAGS.TME_HOVER,
                            hwndTrack = hwnd,
                        };
                        _mouseTracking = DirectNFunctions.TrackMouseEvent(ref tme);
                    }
                }

                OnMouseMove(new MouseEventArgs(lParam.ToPOINT(), (MODIFIERKEYS_FLAGS)wParam.Value.LOWORD()));
                break;

            case MessageDecoder.WM_MOUSELEAVE:
                _mouseTracking = false;
                OnMouseLeave(new MouseEventArgs(POINT.Zero, 0));
                return 0;

            case MessageDecoder.WM_MOUSEHOVER:
                OnMouseHover(this, new MouseEventArgs(lParam.ToPOINT(), (MODIFIERKEYS_FLAGS)wParam.Value.LOWORD()));
                return 0;

            case MessageDecoder.WM_LBUTTONDOWN:
            case MessageDecoder.WM_RBUTTONDOWN:
            case MessageDecoder.WM_MBUTTONDOWN:
            case MessageDecoder.WM_XBUTTONDOWN:
                button = AOTrinoExtensions.MessageToButton(msg, wParam);
                _capturedButtons[(int)button] = true;
                DirectNFunctions.SetCapture(hwnd);
                OnMouseButtonDown(new MouseButtonEventArgs(lParam.ToPOINT(), (MODIFIERKEYS_FLAGS)wParam.Value.LOWORD(), button));
                break;

            case MessageDecoder.WM_LBUTTONUP:
            case MessageDecoder.WM_RBUTTONUP:
            case MessageDecoder.WM_MBUTTONUP:
            case MessageDecoder.WM_XBUTTONUP:
                button = AOTrinoExtensions.MessageToButton(msg, wParam);
                _capturedButtons[(int)button] = false;
                DirectNFunctions.ReleaseCapture();
                OnMouseButtonUp(new MouseButtonEventArgs(lParam.ToPOINT(), (MODIFIERKEYS_FLAGS)wParam.Value.LOWORD(), button));
                break;

            case MessageDecoder.WM_LBUTTONDBLCLK:
            case MessageDecoder.WM_RBUTTONDBLCLK:
            case MessageDecoder.WM_MBUTTONDBLCLK:
            case MessageDecoder.WM_XBUTTONDBLCLK:
                if (!SendDoubleClicks)
                    break;

                button = AOTrinoExtensions.MessageToButton(msg, wParam);
                var e3 = new MouseButtonEventArgs(lParam.ToPOINT(), (MODIFIERKEYS_FLAGS)wParam.Value.LOWORD(), button);
                OnMouseButtonDoubleClick(e3);
                break;

            case MessageDecoder.WM_NCLBUTTONDBLCLK:
            case MessageDecoder.WM_NCRBUTTONDBLCLK:
            case MessageDecoder.WM_NCMBUTTONDBLCLK:
            case MessageDecoder.WM_NCXBUTTONDBLCLK:
                var pt4 = ScreenToClient(lParam.ToPOINT());
                if (msg == MessageDecoder.WM_NCLBUTTONDBLCLK)
                {
                    var caption = GetCaptionRect();
                    if (caption != null && caption.Value.Contains(pt4))
                    {
                        Show(IsZoomed ? SHOW_WINDOW_CMD.SW_RESTORE : SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED);
                        return 0;
                    }
                }

                if (!SendDoubleClicks)
                    break;

                button = AOTrinoExtensions.MessageToButton(msg, wParam);
                var flags = button.ToFlags();
                if (VIRTUAL_KEY.VK_SHIFT.IsPressed())
                {
                    flags |= MODIFIERKEYS_FLAGS.MK_SHIFT;
                }

                if (VIRTUAL_KEY.VK_CONTROL.IsPressed())
                {
                    flags |= MODIFIERKEYS_FLAGS.MK_CONTROL;
                }

                var e4 = new MouseButtonEventArgs(pt4, flags, button);
                OnMouseButtonDoubleClick(e4);
                break;

            case MessageDecoder.WM_MOUSEHWHEEL:
            case MessageDecoder.WM_MOUSEWHEEL:
                OnMouseWheel(new MouseWheelEventArgs(
                    lParam.ToPOINT().ScreenToClient(hwnd),
                    (MODIFIERKEYS_FLAGS)wParam.Value.LOWORD(),
                    wParam.Value.SignedHIWORD(),
                    msg == MessageDecoder.WM_MOUSEHWHEEL ? Orientation.Horizontal : Orientation.Vertical));
                break;

            case MessageDecoder.WM_POINTERHWHEEL:
            case MessageDecoder.WM_POINTERWHEEL:
                var pwe = new PointerWheelEventArgs(
                    wParam.GetPointerId(),
                    lParam.ToPOINT().ScreenToClient(hwnd),
                    wParam.Value.SignedHIWORD(),
                    msg == MessageDecoder.WM_POINTERHWHEEL ? Orientation.Horizontal : Orientation.Vertical);
                OnPointerWheel(this, pwe);
                if (!pwe.Handled)
                {
                    // send as mouse event
                    var winfo = pwe.PointerInfo;
                    var mwe = new MouseWheelEventArgs(pwe.Point, (MODIFIERKEYS_FLAGS)winfo.dwKeyStates, pwe.Delta, pwe.Orientation) { SourcePointerEvent = pwe };
                    OnMouseWheel(mwe);
                }
                break;

            case MessageDecoder.WM_POINTERACTIVATE:
                if (TryForwardPointerInput(msg, wParam, lParam))
                    return 0;

                var pa = new PointerActivateEventArgs(
                    wParam.GetPointerId(),
                    new(lParam),
                    wParam.GetPointerHitTest());
                OnPointerActivate(this, pa);
                if (pa.Result != null)
                    return new((int)pa.Result.Value);

                break;

            case MessageDecoder.WM_POINTERENTER:
                if (TryForwardPointerInput(msg, wParam, lParam))
                    return 0;

                OnPointerEnter(this, new PointerEnterEventArgs(
                    wParam.GetPointerId(),
                    lParam.ToPOINT().ScreenToClient(hwnd),
                    wParam.GetPointerFlags()));
                break;

            case MessageDecoder.WM_POINTERLEAVE:
                if (TryForwardPointerInput(msg, wParam, lParam))
                    return 0;

                OnPointerLeave(this, new PointerLeaveEventArgs(
                    wParam.GetPointerId(),
                    lParam.ToPOINT().ScreenToClient(hwnd),
                    wParam.GetPointerFlags()));
                break;

            case MessageDecoder.WM_POINTERUPDATE:
                if (TryForwardPointerInput(msg, wParam, lParam))
                    return 0;

                var ppe = new PointerUpdateEventArgs(
                    wParam.GetPointerId(),
                    lParam.ToPOINT().ScreenToClient(hwnd),
                    wParam.GetPointerFlags()
                    );
                OnPointerUpdate(this, ppe);
                if (!ppe.Handled)
                {
                    // send as mouse event
                    if (ppe.IsInContact)
                    {
                        OnMouseMove(new MouseEventArgs(ppe.Point, 0) { SourcePointerEvent = ppe });
                    }
                    else
                    {
                        OnMouseHover(this, new MouseEventArgs(ppe.Point, 0) { SourcePointerEvent = ppe });
                    }
                }
                break;

            case MessageDecoder.WM_POINTERDOWN:
            case MessageDecoder.WM_POINTERUP:
                if (TryForwardPointerInput(msg, wParam, lParam))
                    return 0;

                var pce = new PointerContactChangedEventArgs(
                    wParam.GetPointerId(),
                    lParam.ToPOINT().ScreenToClient(hwnd),
                    wParam.GetPointerFlags(),
                    msg == MessageDecoder.WM_POINTERUP);
                var info = pce.PointerInfo;
                var isUp = msg == MessageDecoder.WM_POINTERUP;

                // determine double click
                if (!isUp)
                {
                    var cx = DirectNFunctions.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXDOUBLECLK);
                    var cy = DirectNFunctions.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYDOUBLECLK);

                    var pt = pce.Point;
                    pce.IsDoubleClick = _lastPointerDownTime + DirectNFunctions.GetDoubleClickTime() * 10000 > info.PerformanceCount
                        && Math.Abs(_lastPointerDownPositionX - pt.x) < cx
                        && Math.Abs(_lastPointerDownPositionY - pt.y) < cy;

                    if (!pce.IsDoubleClick)
                    {
                        _lastPointerDownPositionX = pt.x;
                        _lastPointerDownPositionY = pt.y;
                        _lastPointerDownTime = info.PerformanceCount;
                    }
                }

                OnPointerContactChanged(this, pce);
                if (!pce.Handled)
                {
                    // send as mouse event
                    var mb = pce.MouseButton;
                    if (!mb.HasValue)
                    {
                        // huh? which button then?
                        Application.TraceWarning("msg: " + MessageDecoder.MsgToString(msg) + " unhandled");
                        break;
                    }

                    var me = new MouseButtonEventArgs(pce.Point, (MODIFIERKEYS_FLAGS)info.dwKeyStates, mb.Value) { SourcePointerEvent = pce };
                    if (pce.IsDoubleClick)
                    {
                        OnMouseButtonDoubleClick(me);
                    }
                    else
                    {
                        if (isUp)
                        {
                            OnMouseButtonUp(me);
                        }
                        else
                        {
                            OnMouseButtonDown(me);
                        }
                    }
                }
                break;

            case MessageDecoder.WM_CHAR:
            case MessageDecoder.WM_SYSCHAR:
                var e = new KeyPressEventArgs(wParam.Value.ToUInt32());
                OnKeyPress(this, e);
                if (e.Handled)
                    return null;

                break;

            case MessageDecoder.WM_KEYDOWN:
            case MessageDecoder.WM_KEYUP:
            case MessageDecoder.WM_SYSKEYDOWN:
            case MessageDecoder.WM_SYSKEYUP:
                var vk = (VIRTUAL_KEY)wParam.Value.ToUInt32();
                var e2 = new KeyEventArgs(vk, (uint)lParam.Value.ToInt64(), ((char)DirectNFunctions.MapVirtualKeyW((uint)vk, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_CHAR)).ToString());
                if (e2.IsUp)
                {
                    OnKeyUp(this, e2);
                }
                else
                {
                    OnKeyDown(this, e2);
                }
                if (e2.Handled)
                    return null;

                break;
        }
        return base.WindowProc(hwnd, msg, wParam, lParam);
    }

    protected override bool OnMoving(ref RECT rc)
    {
        _baseController?.NotifyParentWindowPositionChanged().ThrowOnError();
        return base.OnMoving(ref rc);
    }

    protected override bool OnMoved()
    {
        _baseController?.NotifyParentWindowPositionChanged().ThrowOnError();
        return base.OnMoved();
    }

    protected override bool OnResized(WindowResizedType type, SIZE size)
    {
        _baseController?.put_Bounds(ClientRect).ThrowOnError();
        return base.OnResized(type, size);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_navigationCompleted.value != 0)
            {
                WebView?.Object.remove_NavigationCompleted(_navigationCompleted);
                _navigationCompleted.value = 0;
            }

            if (_navigationStarting.value != 0)
            {
                WebView?.Object.remove_NavigationStarting(_navigationStarting);
                _navigationStarting.value = 0;
            }

            _environment?.Dispose();
        }
        base.Dispose(disposing);
    }
}
