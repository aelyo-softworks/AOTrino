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
  window.__aotrino = {
    getBuffer: function (name) { return buffers[name] || null; },
    getMeta: function (name) { return meta[name] || null; },
    onBuffer: function (name, cb) { (subs[name] = subs[name] || []).push(cb); if (buffers[name]) { try { cb(buffers[name], meta[name]); } catch (x) {} } },
    post: function (msg) { try { window.chrome.webview.postMessage(msg); } catch (x) {} }
  };
})();
