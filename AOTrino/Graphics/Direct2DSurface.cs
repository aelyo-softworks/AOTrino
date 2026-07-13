namespace AOTrino.Graphics;

// renders an animated Direct2D scene in .NET on the GPU and displays it at high performance on a
// <canvas data-aotrino-surface="NAME"> — via a WebView2 shared buffer + WebGL.
// built ENTIRELY on the generic AOTrino.SharedBuffer (the transport is renderer-agnostic). the scene is drawn
// on the GPU (the window's Direct2D device), copied GPU->CPU into the shared memory, and the injected WebGL
// runtime uploads it (zero-copy) to the canvas every animation frame. the canvas size is reported back so
// .NET always renders at display resolution.
public sealed class Direct2DSurface : IDisposable
{
    private const int _bytesPerPixel = 4;
    private const int _defaultFrameIntervalMs = 16; // ~60 fps

    private readonly WebViewWindow _window;
    private readonly string _name;
    private readonly SharedBuffer _buffer;

    private IComObject<ID2D1DeviceContext>? _dc;
    private IComObject<ID2D1Bitmap1>? _target;   // GPU render target
    private IComObject<ID2D1Bitmap1>? _staging;  // CPU-readable copy
    private int _requestedWidth;
    private int _requestedHeight;
    private int _width;
    private int _height;
    private CancellationTokenSource? _animation;
    private Action<IComObject<ID2D1RenderTarget>, int, int, float>? _draw;
    private long _startTick;

    public Direct2DSurface(WebViewWindow window, string name)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrEmpty(name);
        _window = window;
        _name = name;
        _buffer = window.CreateSharedBuffer(name, SharedBufferAccess.ReadOnly); // injects the generic __aotrino runtime
        window.AddStartupScript(EmbeddedResource.Load("Direct2DSurface.Runtime.js")); // injects the WebGL display runtime (runs after the generic one)
        window.WebMessageJsonReceived += OnWebMessage;
    }

    public string Name => _name;
    public int FrameIntervalMs { get; set; } = _defaultFrameIntervalMs;

    // begin an animation loop on the UI thread. 'draw' is called each frame with the Direct2D context
    // (already BeginDraw'd), the pixel width/height and elapsed seconds. draw with ID2D1RenderTarget extensions.
    public void StartAnimation(Action<IComObject<ID2D1RenderTarget>, int, int, float> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        StopAnimation();
        _draw = draw;
        _startTick = Environment.TickCount64;
        _animation = new CancellationTokenSource();
        _ = RunAsync(_animation.Token);
    }

    public void StopAnimation()
    {
        _animation?.Cancel();
        _animation = null;
    }

    private void OnWebMessage(object? sender, ValueEventArgs<string> json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json.Value ?? string.Empty);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

            if (!root.TryGetProperty("__aotrino", out var kind) || kind.GetString() != "surface-size")
                return;

            if (!root.TryGetProperty("name", out var n) || n.GetString() != _name)
                return;

            var width = root.GetProperty("width").GetInt32();
            var height = root.GetProperty("height").GetInt32();
            var changed = width != _requestedWidth || height != _requestedHeight;
            _requestedWidth = width;
            _requestedHeight = height;

            // re-render on the spot so a resize shows fresh pixels immediately instead of after the next frame tick
            if (changed)
            {
                RenderFrame();
            }
        }
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Direct2DSurface '{_name}' message error: {ex.Message}");
            // not one of our messages
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                RenderFrame();
            }
            catch (Exception ex)
            {
                AOTrinoApplication.Current?.TraceWarning($"Direct2DSurface '{_name}' frame error: {ex.Message}");
            }

            try { await Task.Delay(FrameIntervalMs, token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void RenderFrame()
    {
        var draw = _draw;
        if (draw == null)
            return;

        var width = _requestedWidth;
        var height = _requestedHeight;
        if (width <= 0 || height <= 0)
            return;

        var seconds = (Environment.TickCount64 - _startTick) / 1000f;
        EnsureTarget(width, height);
        var dc = _dc!;
        dc.SetTarget(_target!);
        dc.BeginDraw();
        draw(dc, width, height, seconds);
        dc.EndDraw();

        // GPU -> CPU: copy the render target into the CPU-readable staging bitmap, map it, memcpy into shared memory
        _staging!.CopyFromBitmap(_target!);
        CopyToBuffer(width, height);
    }

    private void EnsureTarget(int width, int height)
    {
        _dc ??= (_window.D2D1Device ?? throw new InvalidOperationException("The window has no Direct2D device.")).CreateDeviceContext();
        if (_target != null && _width == width && _height == height)
            return;

        _target?.Dispose();
        _staging?.Dispose();

        var size = new D2D_SIZE_U((uint)width, (uint)height);
        var pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED };
        _target = _dc.CreateBitmap<ID2D1Bitmap1>(size, new D2D1_BITMAP_PROPERTIES1
        {
            pixelFormat = pixelFormat,
            bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_TARGET,
        });
        _staging = _dc.CreateBitmap<ID2D1Bitmap1>(size, new D2D1_BITMAP_PROPERTIES1
        {
            pixelFormat = pixelFormat,
            bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_CPU_READ | D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        });
        _width = width;
        _height = height;

        // during a resize the buffer only actually reallocates when it outgrows its (power-of-two) capacity.
        // re-handing the whole buffer to the page is a heavy cross-process op, so only do it on a real realloc;
        // otherwise just tell the page the new dimensions with a light message (the memory is unchanged).
        if (_buffer.EnsureSize(width * height * _bytesPerPixel))
        {
            _buffer.Post($"\"width\":{width},\"height\":{height}");
        }
        else
        {
            PostDimensions(width, height);
        }
    }

    private void PostDimensions(int width, int height)
    {
        var webView = _window.SharedWebView;
        if (webView == null)
            return;

        webView.Object.PostWebMessageAsJson(PWSTR.From($"{{\"__aotrino\":\"surface-dims\",\"name\":\"{_name}\",\"width\":{width},\"height\":{height}}}"));
    }

    private unsafe void CopyToBuffer(int width, int height)
    {
        var dest = _buffer.Pointer;
        if (dest == 0 || _staging == null)
            return;

        var map = _staging.Map(D2D1_MAP_OPTIONS.D2D1_MAP_OPTIONS_READ);
        try
        {
            var rowBytes = width * _bytesPerPixel;
            var srcStride = (int)map.pitch;
            for (var y = 0; y < height; y++)
            {
                Buffer.MemoryCopy((void*)(map.bits + (nint)y * srcStride), (void*)(dest + (nint)y * rowBytes), rowBytes, rowBytes);
            }
        }
        finally
        {
            _staging.Unmap();
        }
    }

    public void Dispose()
    {
        StopAnimation();
        _window.WebMessageJsonReceived -= OnWebMessage;
        _staging?.Dispose();
        _target?.Dispose();
        _dc?.Dispose();
        _buffer.Dispose();
    }
}
