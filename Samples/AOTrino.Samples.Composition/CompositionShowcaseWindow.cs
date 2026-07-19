namespace AOTrino.Samples.Composition;

// hosts the WebView as ONE visual in a composition tree, then composites live .NET layers ON TOP of it:
// * a soft glow that follows the OS mouse over the page,
// * a Direct2D-drawn "glass" HUD showing a live system-CPU graph (a real native 2D surface), and.
// * a translucent status bar with a pulsing dot.
// the page underneath stays fully interactive.
// an HWND-hosted WebView is an opaque top-most child window, so native code can't draw anything over it,
// which is why the HWND window shows none of this.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class CompositionShowcaseWindow : CompositionWebViewWindow
{
    private const float _barHeight = 52;
    private const float _glowSize = 280;
    private const float _hudWidth = 214;
    private const float _hudHeight = 84;

    private readonly SpriteVisual _pageVisual; // the WebView renders here (full size, fully interactive).
    private readonly SpriteVisual _glow; // a soft glow that follows the cursor, over the page.
    private readonly SpriteVisual _overlay; // a translucent status bar over the page.
    private readonly SpriteVisual _dot; // a pulsing indicator inside the bar.
    private SpriteVisual? _hud; // a Direct2D-drawn glass HUD (a live CPU graph).
    private CompositionDrawingSurface? _hudSurface;
    private readonly float[] _cpu = new float[30]; // rolling CPU-usage history.
    private float _cpuValue;
    private long _prevIdle, _prevKernel, _prevUser;
    private IComObject<IDWriteTextFormat>? _labelFormat;
    private IComObject<IDWriteTextFormat>? _valueFormat;
    private volatile bool _disposed;

    public CompositionShowcaseWindow()
        : base("AOTrino — Composition host")
    {
        RootVisual.Brush = Compositor.CreateColorBrush(new Color { A = 255, R = 13, G = 17, B = 23 });

        _pageVisual = Compositor.CreateSpriteVisual();
        _pageVisual.Size = ClientSize();
        RootVisual.Children.InsertAtTop(_pageVisual);

        // a native radial-gradient "flashlight" that tracks the mouse over the live page.
        _glow = Compositor.CreateSpriteVisual();
        _glow.Size = new Vector2(_glowSize, _glowSize);
        var radial = Compositor.CreateRadialGradientBrush();
        radial.ColorStops.Add(Compositor.CreateColorGradientStop(0f, new Color { A = 130, R = 88, G = 166, B = 255 }));
        radial.ColorStops.Add(Compositor.CreateColorGradientStop(1f, new Color { A = 0, R = 88, G = 166, B = 255 }));
        _glow.Brush = radial;
        _glow.Offset = new Vector3(-_glowSize, -_glowSize, 0);
        RootVisual.Children.InsertAtTop(_glow);

        // a translucent status bar (the page shows through it).
        _overlay = Compositor.CreateSpriteVisual();
        _overlay.Brush = Compositor.CreateColorBrush(new Color { A = 190, R = 13, G = 17, B = 23 });
        RootVisual.Children.InsertAtTop(_overlay);

        _dot = Compositor.CreateSpriteVisual();
        _dot.Brush = Compositor.CreateColorBrush(new Color { A = 255, R = 63, G = 185, B = 80 });
        _dot.Size = new Vector2(14, 14);
        RootVisual.Children.InsertAtTop(_dot);

        Layout();
        StartPulse();
        CompositorController.Commit();
    }

    protected override Visual WebViewVisualTarget => _pageVisual;

    protected override void ControllerCreated()
    {
        base.ControllerCreated();
        EnsureSharedRuntime(); // window.__aotrino (Close button) on the page.
        BuildHud(); // the Direct2D graphics device is ready by now.
        _ = NavigateToWebRootAsync();
    }

    // the window sees every mouse move (it forwards them to the composition-hosted WebView),
    // so we move a native visual to follow the cursor over the page.
    protected override void OnMouseMove(object? sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        _glow.Offset = new Vector3(e.Point.x - _glowSize / 2, e.Point.y - _glowSize / 2, 0);
        CompositorController.Commit();
    }

    private void StartPulse()
    {
        var pulse = Compositor.CreateScalarKeyFrameAnimation();
        pulse.InsertKeyFrame(0f, 1f);
        pulse.InsertKeyFrame(0.5f, 0.25f);
        pulse.InsertKeyFrame(1f, 1f);
        pulse.Duration = TimeSpan.FromSeconds(1.4);
        pulse.IterationBehavior = AnimationIterationBehavior.Forever;
        _dot.StartAnimation("Opacity", pulse);
    }

    // a HUD rendered with Direct2D onto a composition surface: two live monitors (CPU + GPU) with DWrite labels.
    private void BuildHud()
    {
        if (GraphicsDevice == null)
            return;

        _hudSurface = GraphicsDevice.CreateDrawingSurface(new Size(_hudWidth, _hudHeight), DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
        _hud = Compositor.CreateSpriteVisual();
        _hud.Size = new Vector2(_hudWidth, _hudHeight);
        _hud.Brush = Compositor.CreateSurfaceBrush(_hudSurface);
        RootVisual.Children.InsertAtTop(_hud);

        using (var dwrite = DWriteFunctions.DWriteCreateFactory())
        {
            _labelFormat = dwrite.CreateTextFormat("Segoe UI", 13f, weight: DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_SEMI_BOLD);
            _labelFormat.Object.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
            _valueFormat = dwrite.CreateTextFormat("Consolas", 13f);
            _valueFormat.Object.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_TRAILING);
            _valueFormat.Object.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
        }

        DrawHud();
        Layout();
        CompositorController.Commit();
        _ = MonitorAsync();
    }

    private void DrawHud()
    {
        if (_hudSurface == null)
            return;

        using var interop = _hudSurface.AsComObject<ICompositionDrawingSurfaceInterop>();
        using var dc = interop.BeginDraw(null);

        using var glass = dc.CreateSolidColorBrush(new D3DCOLORVALUE { r = 0.05f, g = 0.07f, b = 0.11f, a = 0.68f });
        dc.FillRectangle(new D2D_RECT_F { left = 0, top = 0, right = _hudWidth, bottom = _hudHeight }, glass);

        using var textBrush = dc.CreateSolidColorBrush(new D3DCOLORVALUE { r = 0.86f, g = 0.9f, b = 0.95f, a = 1f });
        using var accent = dc.CreateSolidColorBrush(new D3DCOLORVALUE { r = 0.345f, g = 0.651f, b = 1f, a = 1f });
        using var hot = dc.CreateSolidColorBrush(new D3DCOLORVALUE { r = 1f, g = 0.48f, b = 0.42f, a = 1f });
        using var track = dc.CreateSolidColorBrush(new D3DCOLORVALUE { r = 1f, g = 1f, b = 1f, a = 0.06f });

        DrawMonitor(dc, "CPU", _cpuValue, _cpu, 14, 36, 68, textBrush, accent, hot, track);

        interop.EndDraw();
    }

    private void DrawMonitor(IComObject<ID2D1DeviceContext> dc, string label, float value, float[] history,
        float textTop, float graphTop, float graphBottom,
        IComObject<ID2D1Brush> textBrush, IComObject<ID2D1Brush> accent, IComObject<ID2D1Brush> hot, IComObject<ID2D1Brush> track)
    {
        const float padX = 14;
        if (_labelFormat != null)
        {
            dc.DrawText(label, _labelFormat, new D2D_RECT_F { left = padX, top = textTop, right = padX + 90, bottom = textTop + 18 }, textBrush);
        }

        if (_valueFormat != null)
        {
            dc.DrawText((int)Math.Round(value * 100) + "%", _valueFormat, new D2D_RECT_F { left = 110, top = textTop, right = _hudWidth - padX, bottom = textTop + 18 }, textBrush);
        }

        var left = padX;
        var right = _hudWidth - padX;
        dc.FillRectangle(new D2D_RECT_F { left = left, top = graphTop, right = right, bottom = graphBottom }, track);

        var graphWidth = right - left;
        var graphHeight = graphBottom - graphTop;
        var slot = graphWidth / history.Length;
        var barWidth = Math.Max(2f, slot - 1.5f);
        for (var i = 0; i < history.Length; i++)
        {
            var v = history[i];
            var x = left + i * slot;
            var brush = v >= 0.85f ? hot : accent;
            dc.FillRectangle(new D2D_RECT_F { left = x, top = graphBottom - v * graphHeight, right = x + barWidth, bottom = graphBottom }, brush);
        }
    }

    // sample CPU every second and redraw (on the UI thread via the window sync context).
    private async Task MonitorAsync()
    {
        SampleCpu(); // prime the CPU deltas.
        try { await Task.Delay(1000); } catch { return; }

        while (!_disposed && _hudSurface != null)
        {
            _cpuValue = SampleCpu();
            Push(_cpu, _cpuValue);
            DrawHud();
            CompositorController.Commit();

            try { await Task.Delay(1000); }
            catch { break; }
        }
    }

    private static void Push(float[] history, float value)
    {
        Array.Copy(history, 1, history, 0, history.Length - 1);
        history[^1] = value;
    }

    private float SampleCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return 0;

        var idleDelta = idle - _prevIdle;
        var totalDelta = (kernel - _prevKernel) + (user - _prevUser); // kernel time includes idle time.
        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;
        if (totalDelta <= 0)
            return 0;

        return Math.Clamp((float)(totalDelta - idleDelta) / totalDelta, 0f, 1f);
    }

    [LibraryImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        if (disposing)
        {
            _labelFormat?.Dispose();
            _valueFormat?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override bool OnResized(WindowResizedType type, SIZE size)
    {
        var handled = base.OnResized(type, size); // sizes RootVisual + sets the WebView bounds.
        if (_pageVisual != null)
        {
            _pageVisual.Size = ClientSize();
            Layout();
            CompositorController.Commit();
        }
        return handled;
    }

    private Vector2 ClientSize()
    {
        var rc = ClientRect;
        return new Vector2(rc.Width, rc.Height);
    }

    private void Layout()
    {
        var rc = ClientRect;
        _overlay.Size = new Vector2(rc.Width, _barHeight);
        _overlay.Offset = new Vector3(0, rc.Height - _barHeight, 0);
        _dot.Offset = new Vector3(22, rc.Height - _barHeight + (_barHeight - 14) / 2, 0);
        _hud?.Offset = new Vector3(rc.Width - _hudWidth - 20, 20, 0);
    }
}
