// AOTrino generic shared-buffer runtime (embedded resource; injected once per window).
// exposes window.__aotrino: getBuffer(name)/getMeta(name)/onBuffer(name,cb)/post(msg). renderer-agnostic.
(function () {
  if (window.__aotrino) return;
  var buffers = {}, meta = {}, subs = {};
  window.chrome.webview.addEventListener("sharedbufferreceived", function (e) {
    var name = e.additionalData && e.additionalData.name;
    if (name == null) return;
    var old = buffers[name];
    buffers[name] = e.getBuffer();
    meta[name] = e.additionalData;
    if (old) { try { window.chrome.webview.releaseBuffer(old); } catch (x) {} }
    var cbs = subs[name];
    if (cbs) for (var i = 0; i < cbs.length; i++) { try { cbs[i](buffers[name], e.additionalData); } catch (x) {} }
  });
  function post(msg) { try { window.chrome.webview.postMessage(msg); } catch (x) {} }
  function command(c, extra) { var m = { __aotrino: "window-command", command: c }; if (extra) for (var k in extra) m[k] = extra[k]; post(m); }

  window.__aotrino = {
    getBuffer: function (name) { return buffers[name] || null; },
    getMeta: function (name) { return meta[name] || null; },
    onBuffer: function (name, cb) { (subs[name] = subs[name] || []).push(cb); if (buffers[name]) { try { cb(buffers[name], meta[name]); } catch (x) {} } },
    post: post,
    // window controls (handled natively by WebViewWindow)
    dragWindow: function () { command("drag"); },
    closeWindow: function () { command("close"); },
    minimizeWindow: function () { command("minimize"); },
    maximizeWindow: function () { command("maximize"); },
    // renames the window itself (taskbar, Alt-Tab, thumbnails), not just document.title.
    // window.title is injected after this script, hence the guard; keep it truthful so a later read agrees.
    setWindowTitle: function (t) { t = String(t); command("title", { title: t }); if (this.window) { this.window.title = t; } }
  };

  // any element (or ancestor) marked [data-aotrino-drag] drags the window on left-mousedown;
  // mark interactive children [data-aotrino-nodrag] to keep them clickable.
  document.addEventListener("mousedown", function (e) {
    if (e.button !== 0) return;
    for (var t = e.target; t && t.getAttribute; t = t.parentElement) {
      if (t.hasAttribute("data-aotrino-nodrag")) return;
      if (t.hasAttribute("data-aotrino-drag")) { window.__aotrino.dragWindow(); return; }
    }
  });
})();
