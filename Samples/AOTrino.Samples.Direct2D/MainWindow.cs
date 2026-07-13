namespace AOTrino.Samples.Direct2D;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    private Direct2DSurface? _surface;

    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    protected override void ControllerCreated()
    {
        // create the surface BEFORE base navigates: its document-created scripts (the __aotrino runtime +
        // WebGL display) must be registered before the page loads. the entire app is these two lines — a
        // Direct2D scene rendered in .NET, shown on the page's <canvas data-aotrino-surface="scene">.
        _surface = new Direct2DSurface(this, "scene");
        _surface.StartAnimation(DrawScene);

        base.ControllerCreated();

        if (System.Environment.GetCommandLineArgs().Contains("--selftest"))
        {
            _ = SelfTestAsync();
        }
    }

    // a self-contained animated scene: orbiting, pulsing, hue-cycling circles on a dark field
    private static void DrawScene(IComObject<ID2D1RenderTarget> rt, int width, int height, float t)
    {
        rt.Clear(new(1, 0.05f, 0.07f, 0.11f));

        const int electrons = 12;
        var min = Math.Min(width, height);
        var cx = width / 2f;
        var cy = height / 2f;
        for (var i = 0; i < electrons; i++)
        {
            var a = t * 0.6f + i * (MathF.PI * 2 / electrons);
            var orbit = min * 0.34f;
            var px = cx + MathF.Cos(a) * orbit;
            var py = cy + MathF.Sin(a * 1.3f) * orbit * 0.62f;
            var r = min * 0.05f * (1 + 0.45f * MathF.Sin(t * 2 + i));
            using var brush = rt.CreateSolidColorBrush(new Hsv(360 * (i / (float)electrons + t * 0.05f), 360 * 0.65f, 1).ToD3DCOLORVALUE());
            rt.FillEllipse(new D2D1_ELLIPSE { point = new D2D_POINT_2F { x = px, y = py }, radiusX = r, radiusY = r }, brush);
        }
    }

    private async Task SelfTestAsync()
    {
        await Task.Delay(2500);
        // read the WebGL canvas back: proves D2D -> shared buffer -> WebGL -> canvas end to end
        var json = await ExecuteScriptAsJson("({frames: (window.__aotrinoGL && window.__aotrinoGL.frames('scene')) || 0, px: (window.__aotrinoGL && window.__aotrinoGL.readPixel('scene', 8, 8)) || null})");
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "aotrino-direct2d-selftest.json"), json ?? "null");
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        Interlocked.Exchange(ref _surface, null)?.Dispose();
        base.Dispose(disposing);
    }
}
