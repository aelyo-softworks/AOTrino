namespace AOTrino.Samples.Translucid;

// a window whose border is translucent (a Windows 11 system backdrop — Mica / Acrylic / Tabbed — shows through)
// while the center pane stays opaque. this works because the window is composition-hosted and transparent: the
// WebView renders with a transparent background (AOTrino sets WEBVIEW2_DEFAULT_BACKGROUND_COLOR = transparent)
// so the material shows wherever the page is transparent — an HWND-hosted, opaque WebView could not do this.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class TranslucidWindow : AOTrinoWindow
{
    public TranslucidWindow()
        : base("AOTrino — Translucid")
    {
    }

    protected override void ControllerCreated()
    {
        base.ControllerCreated();
        SetSystemBackdrop(DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW); // Mica by default
    }

    // the page buttons post { __aotrino: "backdrop", type: "mica" | "acrylic" | "tabbed" | "none" }
    protected override void OnWebMessageJsonReceived(object sender, ValueEventArgs<string> json)
    {
        base.OnWebMessageJsonReceived(sender, json);
        try
        {
            using var doc = JsonDocument.Parse(json.Value ?? string.Empty);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("__aotrino", out var kind) || kind.GetString() != "backdrop" ||
                !root.TryGetProperty("type", out var t))
                return;

            var type = t.GetString() switch
            {
                "mica" => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
                "acrylic" => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
                "tabbed" => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW,
                _ => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE,
            };
            SetSystemBackdrop(type);
        }
        catch
        {
            // not one of our messages
        }
    }
}
