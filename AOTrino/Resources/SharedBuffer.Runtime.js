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

  // an element is part of the caption when it, or an ancestor, is marked [data-aotrino-drag],
  // unless something nearer is marked [data-aotrino-nodrag], which is how a button inside the caption stays clickable.
  function isCaption(target) {
    for (var t = target; t && t.getAttribute; t = t.parentElement) {
      if (t.hasAttribute("data-aotrino-nodrag")) return false;
      if (t.hasAttribute("data-aotrino-drag")) return true;
    }
    return false;
  }

  // the caption drags the window on left-mousedown, and maximizes or restores it on a double click,
  // which is what a native caption does and what someone double clicking one expects.
  //
  // the second click is read from the click count on mousedown rather than from a dblclick event.
  // dragging hands the window to Windows with WM_NCLBUTTONDOWN, which runs a modal move loop until the button
  // comes back up, and no dblclick survives that. the next mousedown does arrive, and it carries the count.
  document.addEventListener("mousedown", function (e) {
    if (e.button !== 0 || !isCaption(e.target)) return;

    if (e.detail === 2) {
      window.__aotrino.maximizeWindow();
      return;
    }

    window.__aotrino.dragWindow();
  });
})();
