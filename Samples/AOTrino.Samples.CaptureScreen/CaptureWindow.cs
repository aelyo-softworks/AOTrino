namespace AOTrino.Samples.CaptureScreen;

// a split window:
// * the LEFT pane is the web page (description, composited),
// * the RIGHT pane is a LIVE screen capture (Windows.Graphics.Capture) rendered with Direct2D onto a composition surface.
// a draggable divider resizes the split.
// a web page fundamentally can't capture and display other OS windows, this is native-only,
// and only possible because the WebView is one composition layer we can lay beside another.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class CaptureWindow : AOTrinoWindow
{
    private const float _dividerWidth = 6;

    private readonly SpriteVisual _pageVisual; // left pane: the WebView.
    private readonly SpriteVisual _capture; // right pane: the live screen capture.
    private readonly SpriteVisual _divider; // the draggable splitter.
    private readonly bool _ready; // OnResized can fire (from the base ctor) before our visuals exist.
    private CompositionDrawingSurface? _captureSurface;
    private IDirect3DDevice? _direct3DDevice;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureItem? _captureItem;
    private GraphicsCaptureSession? _session;
    private float _screenWidth;
    private float _screenHeight;
    private float _splitFraction = 0.44f;
    private int _splitX;
    private bool _draggingSplit;
    private volatile bool _disposed;

    public CaptureWindow()
        : base("AOTrino — Screen capture (split)")
    {
        _pageVisual = Compositor.CreateSpriteVisual();
        RootVisual.Children.InsertAtTop(_pageVisual);

        _capture = Compositor.CreateSpriteVisual();
        _capture.Brush = Compositor.CreateColorBrush(new Color { A = 255, R = 8, G = 11, B = 15 });
        RootVisual.Children.InsertAtTop(_capture);

        _divider = Compositor.CreateSpriteVisual();
        _divider.Brush = Compositor.CreateColorBrush(new Color { A = 255, R = 48, G = 54, B = 61 });
        RootVisual.Children.InsertAtTop(_divider);

        _ready = true;
        Relayout();
    }

    protected override Visual WebViewVisualTarget => _pageVisual;

    protected override void ControllerCreated()
    {
        base.ControllerCreated();
        StartCapture();
        Relayout();
    }

    private void StartCapture()
    {
        if (_disposed || !GraphicsCaptureSession.IsSupported() || GraphicsDevice == null || Device == null)
            return;

        var monitor = DirectN.Extensions.Utilities.Monitor.Primary ?? DirectN.Extensions.Utilities.Monitor.All.FirstOrDefault();
        if (monitor == null)
            return;

        _captureItem = GraphicsCaptureItem.TryCreateFromDisplayId(new DisplayId((ulong)monitor.Handle.Value));
        if (_captureItem == null)
            return;

        _screenWidth = _captureItem.Size.Width;
        _screenHeight = _captureItem.Size.Height;

        // a screen-sized surface, drawn 1:1 each frame, the right-pane visual stretches it to fit (Uniform).
        _captureSurface = GraphicsDevice.CreateDrawingSurface2(_captureItem.Size, DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
        var brush = Compositor.CreateSurfaceBrush(_captureSurface);
        brush.Stretch = CompositionStretch.Uniform;
        brush.HorizontalAlignmentRatio = 0.5f;
        brush.VerticalAlignmentRatio = 0.5f;
        _capture.Brush = brush;

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

    // FrameArrived is raised on our UI thread (AOTrino installs a DispatcherQueue), so drawing + Commit are safe.
    private void DrawFrame(Direct3D11CaptureFrame? frame)
    {
        if (frame == null || _disposed || _captureSurface == null)
            return;

        using var dxgiSurface = frame.Surface.AsDxgiComObject<IDXGISurface>();
        if (dxgiSurface == null)
            return;

        using var interop = _captureSurface.AsComObject<ICompositionDrawingSurfaceInterop>();
        using var dc = interop.BeginDraw<ID2D1DeviceContext>();
        try
        {
            //dc.Clear();
            using var bitmap = dc.CreateBitmapFromDxgiSurface(dxgiSurface);
            dc.DrawBitmap(bitmap,
                interpolationMode: D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC,
                destinationRectangle: new D2D_RECT_F { left = 0, top = 0, right = _screenWidth, bottom = _screenHeight });
        }
        finally
        {
            interop.EndDraw();
        }
        CompositorController.Commit();
    }

    protected override void OnMouseButtonDown(object? sender, MouseButtonEventArgs e)
    {
        base.OnMouseButtonDown(sender, e);
        if (e.Button == MouseButton.Left && IsOnDivider(e.Point.x))
        {
            _draggingSplit = true;
            e.Handled = true; // don't forward this to the WebView.
        }
    }

    protected override void OnMouseMove(object? sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (_draggingSplit)
        {
            var width = ClientRect.Width;
            _splitFraction = Math.Clamp((float)e.Point.x / Math.Max(1, width), 0.22f, 0.8f);
            Relayout();
            e.Handled = true;
        }
    }

    protected override void OnMouseButtonUp(object? sender, MouseButtonEventArgs e)
    {
        base.OnMouseButtonUp(sender, e);
        if (_draggingSplit)
        {
            _draggingSplit = false;
            e.Handled = true;
        }
    }

    private bool IsOnDivider(int x) => x >= _splitX - 4 && x <= _splitX + _dividerWidth + 4;

    protected override bool OnResized(WindowResizedType type, SIZE size)
    {
        var handled = base.OnResized(type, size);
        Relayout();
        return handled;
    }

    private void Relayout()
    {
        if (!_ready)
            return;

        var rc = ClientRect;
        var w = rc.Width;
        var h = rc.Height;
        _splitX = Math.Clamp((int)(w * _splitFraction), 200, Math.Max(200, w - 240));

        // left pane = the WebView.
        BaseController?.put_Bounds(new RECT { left = 0, top = 0, right = _splitX, bottom = h });
        _pageVisual.Size = new Vector2(_splitX, h);
        _pageVisual.Offset = new Vector3(0, 0, 0);

        _divider.Size = new Vector2(_dividerWidth, h);
        _divider.Offset = new Vector3(_splitX, 0, 0);

        var rightX = _splitX + _dividerWidth;
        _capture.Size = new Vector2(Math.Max(0, w - rightX), h);
        _capture.Offset = new Vector3(rightX, 0, 0);

        CompositorController.Commit();
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
