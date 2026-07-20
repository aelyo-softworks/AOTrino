// the bridge, seen from Blazor.
//
// AOTrino exposes host objects on chrome.webview.hostObjects, where every member is a promise, even a property read,
// and its own window controls on window.__aotrino.
// Blazor's JS interop can only call a JS function, so each of them gets a one line wrapper here,
// and DiskMapHost.cs turns those into ordinary awaitable C# calls.
window.diskmap = {
    isHosted: () => !!(window.chrome && window.chrome.webview && window.chrome.webview.hostObjects),

    getDrives: async () => await window.chrome.webview.hostObjects.diskmap.GetDrives(),
    startScan: async (path, quick) => await window.chrome.webview.hostObjects.diskmap.StartScan(path, quick),
    cancelScan: async () => await window.chrome.webview.hostObjects.diskmap.CancelScan(),
    getProgress: async () => await window.chrome.webview.hostObjects.diskmap.GetProgress(),
    getChildren: async (path) => await window.chrome.webview.hostObjects.diskmap.GetChildren(path),
    getNode: async (path) => await window.chrome.webview.hostObjects.diskmap.GetNode(path),
    openInExplorer: async (path) => await window.chrome.webview.hostObjects.diskmap.OpenInExplorer(path),

    isElevated: async () => await window.chrome.webview.hostObjects.diskmap.IsElevated(),
    canUseMft: async (path) => await window.chrome.webview.hostObjects.diskmap.CanUseMasterFileTable(path),
    mftReason: async (path) => await window.chrome.webview.hostObjects.diskmap.GetMasterFileTableReason(path),
    restartElevated: async () => await window.chrome.webview.hostObjects.diskmap.RestartElevated(),

    setTreemapPath: async (path) => await window.chrome.webview.hostObjects.diskmap.SetTreemapPath(path),
    setTreemapPointer: async (x, y) => await window.chrome.webview.hostObjects.diskmap.SetTreemapPointer(x, y),
    treemapHitTest: async (x, y) => await window.chrome.webview.hostObjects.diskmap.TreemapHitTest(x, y),
    setTreemapTheme: async (dark) => await window.chrome.webview.hostObjects.diskmap.SetTreemapTheme(dark),

    // the canvas the treemap is displayed on.
    //
    // AOTrino's WebGL display runtime auto-attaches every canvas carrying data-aotrino-surface at DOMContentLoaded,
    // which on a Blazor page is before anything has been rendered, so it finds no canvas and attaches nothing.
    // a page that renders its own DOM has to attach explicitly once the element really exists,
    // which is what Index.razor does from OnAfterRenderAsync.
    attachTreemap: (page) => {
        const canvas = document.querySelector('canvas[data-aotrino-surface="treemap"]');
        if (!canvas || !window.__aotrinoGL || canvas.dataset.attached) return false;

        canvas.dataset.attached = "1";
        window.__aotrinoGL.attach("treemap", canvas);

        // normalized coordinates, because the canvas is laid out in CSS pixels and rendered at device pixels,
        // and those differ on a scaled display. the host multiplies by whatever it is actually rendering at.
        const at = (e) => {
            const r = canvas.getBoundingClientRect();
            return [(e.clientX - r.left) / Math.max(1, r.width), (e.clientY - r.top) / Math.max(1, r.height)];
        };

        // the pointer goes straight to the host rather than through Blazor.
        // hovering is a highlight in a frame that is already being drawn, so a round trip into wasm and back
        // would add latency to something that only has to reach the renderer.
        // coalesced onto animation frames, since mousemove fires far more often than the treemap is redrawn.
        let pending = null;
        let queued = false;
        canvas.addEventListener("mousemove", (e) => {
            pending = at(e);
            if (queued) return;

            queued = true;
            requestAnimationFrame(() => {
                queued = false;
                if (pending) { window.diskmap.setTreemapPointer(pending[0], pending[1]); }
            });
        });

        canvas.addEventListener("mouseleave", () => { pending = null; window.diskmap.setTreemapPointer(-1, -1); });

        // a click is different: descending changes what the page is showing, so that one does go to Blazor.
        canvas.addEventListener("click", async (e) => {
            const p = at(e);
            const path = await window.diskmap.treemapHitTest(p[0], p[1]);
            if (path) { await page.invokeMethodAsync("OnTreemapClick", path); }
        });

        // switching Windows between light and dark changes a running window, so the treemap has to follow it too,
        // the same way the CSS does through the media query.
        if (window.matchMedia) {
            const light = window.matchMedia("(prefers-color-scheme: light)");
            light.addEventListener("change", () => window.diskmap.setTreemapTheme(!light.matches));
        }

        return true;
    },

    // the theme is the Windows app theme, which WebView2 maps onto prefers-color-scheme.
    // the CSS follows it on its own, the treemap is pixels drawn in the other runtime, so it has to be told.
    isDarkTheme: () => !window.matchMedia || !window.matchMedia("(prefers-color-scheme: light)").matches,

    // the window itself. dragging needs no wrapper, the injected runtime handles data-aotrino-drag on its own.
    minimize: () => window.__aotrino && window.__aotrino.minimizeWindow(),
    maximize: () => window.__aotrino && window.__aotrino.maximizeWindow(),
    close: () => window.__aotrino && window.__aotrino.closeWindow(),
    setTitle: (title) => window.__aotrino && window.__aotrino.setWindowTitle(title),
};
