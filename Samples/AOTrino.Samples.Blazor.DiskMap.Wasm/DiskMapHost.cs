namespace AOTrino.Samples.Blazor.DiskMap.Wasm;

using System.Text.Json;
using AOTrino.Samples.Blazor.DiskMap.Shared;
using Microsoft.JSInterop;

// the host, as the page sees it.
//
// this is the interesting seam of the sample: both sides are C#, but this side is wasm in the WebView
// and the other side is native AOT in the process that owns the window.
// they meet through JS interop and AOTrino's host objects, wwwroot\diskmap.js is the one line per member that joins them.
// everything here is something the browser sandbox forbids, which is the reason to be in an AOTrino window at all.
public sealed class DiskMapHost(IJSRuntime js)
{
    // the same source-generated serializer the host writes with, linked from the Shared project,
    // so the two sides cannot disagree about the wire format and neither needs reflection.
    private static readonly DiskMapJsonContext _json = DiskMapJsonContext.Default;

    // false when the page is opened in a plain browser (dotnet run on this project), where there is no host to call.
    public async Task<bool> IsHostedAsync() => await js.InvokeAsync<bool>("diskmap.isHosted");

    public async Task<DriveEntry[]> GetDrivesAsync() => Parse(await js.InvokeAsync<string>("diskmap.getDrives"), _json.DriveEntryArray) ?? [];

    public async Task StartScanAsync(string path, bool quick) => await js.InvokeVoidAsync("diskmap.startScan", path, quick);
    public async Task CancelScanAsync() => await js.InvokeVoidAsync("diskmap.cancelScan");

    public async Task<ScanProgress> GetProgressAsync() => Parse(await js.InvokeAsync<string>("diskmap.getProgress"), _json.ScanProgress) ?? new ScanProgress();

    public async Task<NodeEntry[]> GetChildrenAsync(string path) => Parse(await js.InvokeAsync<string>("diskmap.getChildren", path), _json.NodeEntryArray) ?? [];

    public async Task<NodeEntry> GetNodeAsync(string path) => Parse(await js.InvokeAsync<string>("diskmap.getNode", path), _json.NodeEntry) ?? new NodeEntry();

    public async Task OpenInExplorerAsync(string path) => await js.InvokeVoidAsync("diskmap.openInExplorer", path);

    // whether the fast path is reachable, and if not, why not.
    public async Task<bool> IsElevatedAsync() => await js.InvokeAsync<bool>("diskmap.isElevated");
    public async Task<bool> CanUseMftAsync(string path) => await js.InvokeAsync<bool>("diskmap.canUseMft", path);
    public async Task<string> MftReasonAsync(string path) => await js.InvokeAsync<string>("diskmap.mftReason", path) ?? string.Empty;

    // asks Windows to restart this app elevated. the prompt is the user's to answer, so false means they said no.
    public async Task<bool> RestartElevatedAsync() => await js.InvokeAsync<bool>("diskmap.restartElevated");

    // the treemap. it is drawn by Direct2D in the native process and displayed on a canvas here,
    // so what crosses the bridge is the folder to show and where the pointer is, never the rectangles.
    // the page hands itself over so a click on the canvas can call back into it to descend.
    public async Task<bool> AttachTreemapAsync<T>(DotNetObjectReference<T> page) where T : class => await js.InvokeAsync<bool>("diskmap.attachTreemap", page);
    public async Task SetTreemapPathAsync(string path) => await js.InvokeVoidAsync("diskmap.setTreemapPath", path);
    public async Task SetTreemapPointerAsync(double x, double y) => await js.InvokeVoidAsync("diskmap.setTreemapPointer", x, y);
    public async Task<string> TreemapHitTestAsync(double x, double y) => await js.InvokeAsync<string>("diskmap.treemapHitTest", x, y) ?? string.Empty;
    public async Task SetTreemapThemeAsync(bool dark) => await js.InvokeVoidAsync("diskmap.setTreemapTheme", dark);
    public async Task<bool> IsDarkThemeAsync() => await js.InvokeAsync<bool>("diskmap.isDarkTheme");

    // the window controls, which a page in a browser has no equivalent of.
    public async Task MinimizeAsync() => await js.InvokeVoidAsync("diskmap.minimize");
    public async Task MaximizeAsync() => await js.InvokeVoidAsync("diskmap.maximize");
    public async Task CloseAsync() => await js.InvokeVoidAsync("diskmap.close");
    public async Task SetTitleAsync(string title) => await js.InvokeVoidAsync("diskmap.setTitle", title);

    private static T? Parse<T>(string? json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) => string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize(json, typeInfo);
}
