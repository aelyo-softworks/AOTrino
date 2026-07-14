namespace AOTrino.Samples.Capture;

// hosts the web page as a composition visual, then composites a LIVE screen capture (Windows.Graphics.Capture)
// as a thumbnail on top of it. a web page fundamentally cannot capture and display other OS windows / the
// screen — this is native-only, and only possible because the WebView is one layer we can draw over.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class CaptureWindow : AOTrinoWindow
{
    private const float _thumbWidth = 340;
    private const float _thumbHeight = 196;

    private readonly SpriteVisual _pageVisual; // the WebView renders here (full size, interactive)
    private readonly SpriteVisual _thumb;      // the live screen-capture thumbnail, over the page
    private CompositionDrawingSurface? _thumbSurface;
    private IDirect3DDevice? _direct3DDevice;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureItem? _captureItem;
    private GraphicsCaptureSession? _session;
    private volatile bool _disposed;

    public CaptureWindow()
        : base("AOTrino — Screen capture overlay")
    {
        _pageVisual = Compositor.CreateSpriteVisual();
        _pageVisual.Size = ClientSizeVector();
        RootVisual.Children.InsertAtTop(_pageVisual);

        _thumb = Compositor.CreateSpriteVisual();
        _thumb.Size = new Vector2(_thumbWidth, _thumbHeight);
        RootVisual.Children.InsertAtTop(_thumb);
        LayoutThumb();
    }

    protected override Visual WebViewVisualTarget => _pageVisual;

    protected override void ControllerCreated()
    {
        base.ControllerCreated();
        StartCapture();
    }

    private void StartCapture()
    {
        if (_disposed || !GraphicsCaptureSession.IsSupported() || GraphicsDevice == null || Device == null)
            return;

        _thumbSurface = GraphicsDevice.CreateDrawingSurface(new Size(_thumbWidth, _thumbHeight), DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
        _thumb.Brush = Compositor.CreateSurfaceBrush(_thumbSurface);

        var monitor = DirectN.Extensions.Utilities.Monitor.Primary ?? DirectN.Extensions.Utilities.Monitor.All.FirstOrDefault();
        if (monitor == null)
            return;

        _captureItem = GraphicsCaptureItem.TryCreateFromDisplayId(new DisplayId((ulong)monitor.Handle.Value));
        if (_captureItem == null)
            return;

        // a WinRT Direct3D device wrapping our D3D11 device
        DirectNFunctions.CreateDirect3D11DeviceFromDXGIDevice(Device.As<IDXGIDevice>()?.Object!, out var obj).ThrowOnError();
        using var inspectable = new ComObject<DirectN.IInspectable>(obj);
        ComObject.WithComInstance(inspectable, unk => _direct3DDevice = MarshalInspectable<IDirect3DDevice>.FromAbi(unk));

        _framePool = Direct3D11CaptureFramePool.Create(_direct3DDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _captureItem.Size);
        _session = _framePool.CreateCaptureSession(_captureItem);
        _session.IsBorderRequired = false;
        _framePool.FrameArrived += (pool, e) =>
        {
            using var frame = pool.TryGetNextFrame();
            DrawFrame(frame);
        };
        _session.StartCapture();
    }

    // FrameArrived is raised on our UI thread (AOTrino installs a DispatcherQueue), so drawing + Commit are safe
    private void DrawFrame(Direct3D11CaptureFrame? frame)
    {
        if (frame == null || _disposed || _thumbSurface == null)
            return;

        using var dxgiSurface = frame.Surface.AsDxgiComObject<IDXGISurface>();
        if (dxgiSurface == null)
            return;

        using var interop = _thumbSurface.AsComObject<ICompositionDrawingSurfaceInterop>();
        using var dc = interop.BeginDraw<ID2D1DeviceContext>();
        try
        {
            dc.Clear(new D3DCOLORVALUE { r = 0, g = 0, b = 0, a = 0 });
            using var bitmap = dc.CreateBitmapFromDxgiSurface(dxgiSurface);
            // scale the whole screen into the thumbnail rectangle
            dc.DrawBitmap(bitmap,
                interpolationMode: D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC,
                destinationRectangle: new D2D_RECT_F { left = 0, top = 0, right = _thumbWidth, bottom = _thumbHeight });
        }
        finally
        {
            interop.EndDraw();
        }
        CompositorController.Commit();
    }

    protected override bool OnResized(WindowResizedType type, SIZE size)
    {
        var handled = base.OnResized(type, size);
        if (_pageVisual != null)
        {
            _pageVisual.Size = ClientSizeVector();
            LayoutThumb();
            CompositorController.Commit();
        }
        return handled;
    }

    private Vector2 ClientSizeVector()
    {
        var rc = ClientRect;
        return new Vector2(rc.Width, rc.Height);
    }

    private void LayoutThumb()
    {
        var rc = ClientRect;
        _thumb.Offset = new Vector3(rc.Width - _thumbWidth - 20, 20, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _session?.Dispose();
            _framePool?.Dispose();
            _direct3DDevice?.Dispose();
        }
        base.Dispose(disposing);
    }
}
