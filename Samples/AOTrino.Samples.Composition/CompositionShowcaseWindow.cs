namespace AOTrino.Samples.Composition;

// hosts the WebView as ONE visual in a composition tree, then composites live .NET layers ON TOP of it:
//  - a soft glow that follows the OS mouse over the page,
//  - a Direct2D-drawn "glass" HUD (a real native 2D surface: rounded panel + an equalizer), and
//  - a translucent status bar with a pulsing dot.
// the page underneath stays fully interactive. an HWND-hosted WebView is an opaque top-most child window, so
// native code can't draw anything over it — which is why the HWND window shows none of this.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class CompositionShowcaseWindow : CompositionWebViewWindow
{
    private const float _barHeight = 52;
    private const float _glowSize = 280;
    private const float _hudWidth = 190;
    private const float _hudHeight = 96;

    private readonly SpriteVisual _pageVisual; // the WebView renders here (full size, fully interactive)
    private readonly SpriteVisual _glow;       // a soft glow that follows the cursor, over the page
    private readonly SpriteVisual _overlay;    // a translucent status bar over the page
    private readonly SpriteVisual _dot;        // a pulsing indicator inside the bar
    private SpriteVisual? _hud;                // a Direct2D-drawn glass HUD
    private CompositionDrawingSurface? _hudSurface;

    public CompositionShowcaseWindow()
        : base("AOTrino — Composition host")
    {
        RootVisual.Brush = Compositor.CreateColorBrush(new Color { A = 255, R = 13, G = 17, B = 23 });

        _pageVisual = Compositor.CreateSpriteVisual();
        _pageVisual.Size = ClientSize();
        RootVisual.Children.InsertAtTop(_pageVisual);

        // a native radial-gradient "flashlight" that tracks the mouse over the live page
        _glow = Compositor.CreateSpriteVisual();
        _glow.Size = new Vector2(_glowSize, _glowSize);
        var radial = Compositor.CreateRadialGradientBrush();
        radial.ColorStops.Add(Compositor.CreateColorGradientStop(0f, new Color { A = 130, R = 88, G = 166, B = 255 }));
        radial.ColorStops.Add(Compositor.CreateColorGradientStop(1f, new Color { A = 0, R = 88, G = 166, B = 255 }));
        _glow.Brush = radial;
        _glow.Offset = new Vector3(-_glowSize, -_glowSize, 0);
        RootVisual.Children.InsertAtTop(_glow);

        // a translucent status bar (the page shows through it)
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
        EnsureSharedRuntime(); // window.__aotrino (Close button) on the page
        BuildHud();            // the Direct2D graphics device is ready by now
        _ = NavigateToWebRootAsync();
    }

    // the window sees every mouse move (it forwards them to the composition-hosted WebView), so we move a
    // native visual to follow the cursor over the page
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

    // a HUD rendered with Direct2D onto a composition surface — arbitrary native 2D graphics over the web page
    private void BuildHud()
    {
        if (GraphicsDevice == null)
            return;

        _hudSurface = GraphicsDevice.CreateDrawingSurface(new Size(_hudWidth, _hudHeight), DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
        _hud = Compositor.CreateSpriteVisual();
        _hud.Size = new Vector2(_hudWidth, _hudHeight);
        _hud.Brush = Compositor.CreateSurfaceBrush(_hudSurface);
        RootVisual.Children.InsertAtTop(_hud);

        DrawHud();
        Layout();
        CompositorController.Commit();
    }

    private void DrawHud()
    {
        if (_hudSurface == null)
            return;

        using var interop = _hudSurface.AsComObject<ICompositionDrawingSurfaceInterop>();
        using var dc = interop.BeginDraw(null);
        dc.Clear(new D3DCOLORVALUE { r = 0, g = 0, b = 0, a = 0 });

        using var glass = dc.CreateSolidColorBrush(new D3DCOLORVALUE { r = 0.05f, g = 0.07f, b = 0.11f, a = 0.62f });
        dc.FillRectangle(new D2D_RECT_F { left = 0, top = 0, right = _hudWidth, bottom = _hudHeight }, glass);

        using var accent = dc.CreateSolidColorBrush(new D3DCOLORVALUE { r = 0.345f, g = 0.651f, b = 1f, a = 1f });
        var heights = new[] { 0.45f, 0.8f, 0.6f, 0.95f, 0.55f, 0.75f, 0.5f };
        var barWidth = 14f;
        var gap = 8f;
        var baseline = _hudHeight - 20;
        var x = 20f;
        foreach (var h in heights)
        {
            var barHeight = h * 52;
            dc.FillRectangle(new D2D_RECT_F { left = x, top = baseline - barHeight, right = x + barWidth, bottom = baseline }, accent);
            x += barWidth + gap;
        }

        interop.EndDraw();
    }

    protected override bool OnResized(WindowResizedType type, SIZE size)
    {
        var handled = base.OnResized(type, size); // sizes RootVisual + sets the WebView bounds
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
        if (_hud != null)
        {
            _hud.Offset = new Vector3(rc.Width - _hudWidth - 20, 20, 0);
        }
    }
}
