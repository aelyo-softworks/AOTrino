namespace AOTrino;

// hosts the WebView as ONE visual in a Windows.UI.Composition tree (ICoreWebView2CompositionController +
// RootVisualTarget). the window is a NoRedirectionBitmap composition window, so the WebView composes with any
// other visuals you add to RootVisual, and can be transformed/animated/effected like any layer. because a
// composition-hosted WebView receives no OS input, this class forwards mouse/pointer input to it.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class CompositionWebViewWindow : WebViewWindow, IDropTarget
{
    private ComObject<ICoreWebView2CompositionController>? _controller;
    private IComObject<ICoreWebView2CompositionController3>? _controller3;
    private WebView2.EventRegistrationToken _cursorChangedToken;
    private bool _isDropTarget;

    public CompositionWebViewWindow(
        string? title = null,
        WINDOW_STYLE style = WINDOW_STYLE.WS_THICKFRAME,
        WINDOW_EX_STYLE extendedStyle = WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP,
        RECT? rect = null)
        : base(title, style: style, extendedStyle: extendedStyle, rect: rect)
    {
        DoUseDirect2D = UseDirect2D;
        if (DoUseDirect2D)
        {
            DeviceCreateFlags |= D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT; // need Direct2D support
        }

        CompositorController = new CompositorController();
        CompositorController.CommitNeeded += (s, e) => s.Commit();

        var desktopInterop = CompositorController.Compositor.As<ICompositorDesktopInterop>();
        desktopInterop.CreateDesktopWindowTarget(Handle, TopMostDesktopWindowTarget, out var target).ThrowOnError();
        var compositionTarget = MarshalInspectable<CompositionTarget>.FromAbi(target);

        RootVisual = CreateWindowVisual();
        compositionTarget.Root = RootVisual;
        SetVisualSize();
    }

    public CompositorController CompositorController { get; }
    public SpriteVisual RootVisual { get; }
    public Compositor Compositor => CompositorController.Compositor;
    public CompositionGraphicsDevice? GraphicsDevice { get; private set; } // not null after device resources are created
    public IComObject<ID2D1Device>? D2D1Device { get; private set; }       // not null when UseDirect2D, after device resources are created

    protected ComObject<ICoreWebView2CompositionController>? Controller => _controller;
    protected bool DoUseDirect2D { get; }
    protected virtual bool TopMostDesktopWindowTarget => true;
    protected virtual bool UseDirect2D => true;
    protected virtual SpriteVisual CreateWindowVisual() => Compositor.CreateSpriteVisual();

    protected override void CreateController(ICoreWebView2Environment12 environment, Action onControllerReady)
    {
        environment.CreateCoreWebView2CompositionController(Handle, new CoreWebView2CreateCoreWebView2CompositionControllerCompletedHandler((result, controller) =>
        {
            try
            {
                _controller = new ComObject<ICoreWebView2CompositionController>(controller);
                _controller3 = ComExtensions.As<ICoreWebView2CompositionController3>(_controller);
                _controller.Object.add_CursorChanged(new CoreWebView2CursorChangedEventHandler((sender, args) =>
                {
                    var cursor = new HCURSOR();
                    if (sender.get_Cursor(ref cursor).IsSuccess && CanChangeCursor)
                    {
                        Cursor = cursor;
                    }
                }), ref _cursorChangedToken).ThrowOnError();

                var cb = RootVisual.As<IUnknown>();
                _controller.Object.put_RootVisualTarget(cb).ThrowOnError();

                var ctrl = (ICoreWebView2Controller)controller;
                ctrl.put_Bounds(ClientRect).ThrowOnError();
                ctrl.get_CoreWebView2(out var webView2).ThrowOnError();
                SetWebViewController(ctrl, webView2);
                onControllerReady();
            }
            catch (Exception ex)
            {
                Application.AddError(ex, true);
            }
        })).ThrowOnError();
    }

    // a composition-hosted WebView gets no OS input; inject it via the composition controller
    protected override void ForwardMouseInput(COREWEBVIEW2_MOUSE_EVENT_KIND kind, COREWEBVIEW2_MOUSE_EVENT_VIRTUAL_KEYS keys, uint data, POINT point)
        => _controller?.Object.SendMouseInput(kind, keys, data, point).ThrowOnError();

    protected override bool TryForwardPointerInput(uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (Controller == null)
            return false;

        var pointerId = wParam.GetPointerId();
        var point = lParam.ToPOINT().ScreenToClient(Handle);
        var pointerStartedInWebView = _pointerIdsStartingInWebView.Contains(pointerId);
        if (!pointerStartedInWebView && !ClientRect.Contains(point))
            return false;

        if (!pointerStartedInWebView && (msg == MessageDecoder.WM_POINTERENTER || msg == MessageDecoder.WM_POINTERDOWN))
        {
            _pointerIdsStartingInWebView.Add(pointerId);
        }
        else if (msg == MessageDecoder.WM_POINTERLEAVE)
        {
            _pointerIdsStartingInWebView.Remove(pointerId);
        }

        var ctrl4 = Controller.As<ICoreWebView2ExperimentalCompositionController4>();
        if (ctrl4 == null)
            return false;

        var matrix = D2D_MATRIX_4X4_F.Identity();
        if (ctrl4.Object.CreateCoreWebView2PointerInfoFromPointerId(pointerId, Handle, matrix, out var infoObj).IsError)
            return false;

        using var info = new ComObject<ICoreWebView2PointerInfo>(infoObj);
        return Controller.Object.SendPointerInput((COREWEBVIEW2_POINTER_EVENT_KIND)msg, info.Object).IsSuccess;
    }

    protected override void CreateDeviceResources()
    {
        base.CreateDeviceResources();

        object? device;
        if (DoUseDirect2D)
        {
            // a D2D device is needed for the composition graphics device (ICompositionDrawingSurfaceInterop.BeginDraw)
            using var fac = D2D1Functions.D2D1CreateFactory1();
            D2D1Device = fac.CreateDevice(Device.As<IDXGIDevice>()!);
            device = D2D1Device.Object;
        }
        else
        {
            device = Device;
        }
        if (device == null)
            return;

        var interop = CompositorController.Compositor.As<ICompositorInterop>();
        ComObject.WithComInstance(D2D1Device, unk =>
        {
            interop.CreateGraphicsDevice(unk, out var obj).ThrowOnError();
            GraphicsDevice = MarshalInterface<CompositionGraphicsDevice>.FromAbi(obj);
        });
    }

    protected virtual void SetVisualSize()
    {
        if (RootVisual != null)
        {
            var rc = ClientRect;
            RootVisual.Size = new Vector2(rc.Width, rc.Height);
        }
    }

    protected override bool OnResized(WindowResizedType type, SIZE size)
    {
        SetVisualSize();
        return base.OnResized(type, size);
    }

    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            if (value == _isDropTarget)
                return;

            if (value)
            {
                // we need to ensure this as STAThread doesn't always call it for some reason
                DirectNFunctions.OleInitialize(0); // don't check error
                var hr = DirectNFunctions.RegisterDragDrop(Handle, this);
                if (hr.IsError && hr != DirectNConstants.DRAGDROP_E_ALREADYREGISTERED)
                    throw new Exception("Cannot enable drag & drop operations. Make sure the thread is initialized as an STA thread.", Marshal.GetExceptionForHR((int)hr)!);

                _isDropTarget = true;
            }
            else
            {
                var hr = DirectNFunctions.RevokeDragDrop(Handle);
                hr.ThrowOnErrorExcept(DirectNConstants.DRAGDROP_E_NOTREGISTERED);
                _isDropTarget = false;
            }
        }
    }

    protected virtual void OnAfterDragEnter(IDataObject dataObject, MODIFIERKEYS_FLAGS flags, POINTL point, DROPEFFECT effect) { }
    protected virtual HRESULT OnBeforeDragEnter(IDataObject dataObject, MODIFIERKEYS_FLAGS flags, POINTL point, ref DROPEFFECT effect, out bool handled)
    {
        handled = false;
        return DirectNConstants.S_OK;
    }

    protected virtual void OnAfterDragOver(MODIFIERKEYS_FLAGS flags, POINTL point, DROPEFFECT effect) { }
    protected virtual HRESULT OnBeforeDragOver(MODIFIERKEYS_FLAGS flags, POINTL point, ref DROPEFFECT effect, out bool handled)
    {
        handled = false;
        return DirectNConstants.S_OK;
    }

    protected virtual void OnAfterDragLeave() { }
    protected virtual HRESULT OnBeforeDragLeave(out bool handled)
    {
        handled = false;
        return DirectNConstants.S_OK;
    }

    protected virtual void OnAfterDrop(IDataObject dataObject, MODIFIERKEYS_FLAGS flags, POINTL point, DROPEFFECT effect) { }
    protected virtual HRESULT OnBeforeDrop(IDataObject dataObject, MODIFIERKEYS_FLAGS flags, POINTL point, DROPEFFECT effect, out bool handled)
    {
        handled = false;
        return DirectNConstants.S_OK;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_cursorChangedToken.value != 0)
            {
                _controller?.Object.remove_CursorChanged(_cursorChangedToken);
                _cursorChangedToken.value = 0;
            }

            _controller3?.Dispose();
            _controller?.Dispose();
            CompositorController?.Dispose();
            RootVisual?.Dispose();
            D2D1Device?.Dispose();
        }
        base.Dispose(disposing);
    }

    HRESULT IDropTarget.DragEnter(IDataObject pDataObj, MODIFIERKEYS_FLAGS grfKeyState, POINTL pt, ref DROPEFFECT pdwEffect)
    {
        if (_controller3 == null)
            return DirectNConstants.S_OK;

        var hr = OnBeforeDragEnter(pDataObj, grfKeyState, pt, ref pdwEffect, out var handled);
        if (handled)
            return hr;

        var effect = (uint)pdwEffect;
        hr = _controller3.Object.DragEnter(pDataObj, (uint)grfKeyState, ScreenToClient(new POINT(pt.x, pt.y)), ref effect);
        pdwEffect = (DROPEFFECT)effect;
        OnAfterDragEnter(pDataObj, grfKeyState, pt, pdwEffect);
        return hr;
    }

    HRESULT IDropTarget.DragOver(MODIFIERKEYS_FLAGS grfKeyState, POINTL pt, ref DROPEFFECT pdwEffect)
    {
        if (_controller3 == null)
            return DirectNConstants.S_OK;

        var hr = OnBeforeDragOver(grfKeyState, pt, ref pdwEffect, out var handled);
        if (handled)
            return hr;

        var effect = (uint)pdwEffect;
        hr = _controller3.Object.DragOver((uint)grfKeyState, ScreenToClient(new POINT(pt.x, pt.y)), ref effect);
        pdwEffect = (DROPEFFECT)effect;
        OnAfterDragOver(grfKeyState, pt, pdwEffect);
        return hr;
    }

    HRESULT IDropTarget.DragLeave()
    {
        if (_controller3 == null)
            return DirectNConstants.S_OK;

        var hr = OnBeforeDragLeave(out var handled);
        if (handled)
            return hr;

        hr = _controller3.Object.DragLeave();
        OnAfterDragLeave();
        return hr;
    }

    HRESULT IDropTarget.Drop(IDataObject pDataObj, MODIFIERKEYS_FLAGS grfKeyState, POINTL pt, ref DROPEFFECT pdwEffect)
    {
        if (_controller3 == null)
            return DirectNConstants.S_OK;

        var hr = OnBeforeDrop(pDataObj, grfKeyState, pt, pdwEffect, out var handled);
        if (handled)
            return hr;

        var effect = (uint)pdwEffect;
        hr = _controller3.Object.Drop(pDataObj, (uint)grfKeyState, ScreenToClient(new POINT(pt.x, pt.y)), ref effect);
        pdwEffect = (DROPEFFECT)effect;
        OnAfterDrop(pDataObj, grfKeyState, pt, pdwEffect);
        return hr;
    }
}
