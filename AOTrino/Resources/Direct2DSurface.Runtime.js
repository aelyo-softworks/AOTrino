// AOTrino WebGL display runtime for AOTrino.Graphics.Direct2DSurface (embedded resource; injected once).
// uses the generic window.__aotrino, auto-attaches every <canvas data-aotrino-surface="NAME">, uploads the
// shared buffer to a texture each animation frame and reports the canvas pixel size back to .NET.
// exposes window.__aotrinoGL (attach/readPixel/frames) for headless verification.
(function () {
  if (window.__aotrinoGL) return;
  var surfaces = {};
  function sh(gl, t, src) { var s = gl.createShader(t); gl.shaderSource(s, src); gl.compileShader(s); return s; }
  function initGL(canvas) {
    var gl = canvas.getContext("webgl2", { alpha: true, premultipliedAlpha: false }) ||
             canvas.getContext("webgl", { alpha: true, premultipliedAlpha: false });
    if (!gl) throw new Error("WebGL not available");
    var vs = sh(gl, gl.VERTEX_SHADER, "attribute vec2 a_pos;attribute vec2 a_uv;varying vec2 v_uv;void main(){v_uv=a_uv;gl_Position=vec4(a_pos,0.0,1.0);}");
    var fs = sh(gl, gl.FRAGMENT_SHADER, "precision mediump float;varying vec2 v_uv;uniform sampler2D u_tex;void main(){gl_FragColor=texture2D(u_tex,v_uv).bgra;}");
    var p = gl.createProgram(); gl.attachShader(p, vs); gl.attachShader(p, fs); gl.linkProgram(p);
    if (!gl.getProgramParameter(p, gl.LINK_STATUS)) throw new Error("link: " + gl.getProgramInfoLog(p));
    var pb = gl.createBuffer(), ub = gl.createBuffer(), tex = gl.createTexture();
    gl.bindBuffer(gl.ARRAY_BUFFER, pb); gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1,-1, 1,-1, -1,1, -1,1, 1,-1, 1,1]), gl.STATIC_DRAW);
    gl.bindBuffer(gl.ARRAY_BUFFER, ub); gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([0,1, 1,1, 0,0, 0,0, 1,1, 1,0]), gl.STATIC_DRAW);
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
    return { gl: gl, program: p, aPos: gl.getAttribLocation(p, "a_pos"), aUv: gl.getAttribLocation(p, "a_uv"),
             uTex: gl.getUniformLocation(p, "u_tex"), pb: pb, ub: ub, tex: tex, texW: 0, texH: 0 };
  }
  function draw(s) {
    if (!s.src || !s.bufW || !s.bufH) return;
    var w = s.bufW, h = s.bufH, need = w * h * 4;
    if (s.src.byteLength < need) return;
    var src = new Uint8Array(s.src, 0, need), g = s.gl;
    if (s.canvas.width !== w) s.canvas.width = w;
    if (s.canvas.height !== h) s.canvas.height = h;
    g.bindTexture(g.TEXTURE_2D, s.tex);
    if (s.texW !== w || s.texH !== h) { g.texImage2D(g.TEXTURE_2D, 0, g.RGBA, w, h, 0, g.RGBA, g.UNSIGNED_BYTE, null); s.texW = w; s.texH = h; }
    g.texSubImage2D(g.TEXTURE_2D, 0, 0, 0, w, h, g.RGBA, g.UNSIGNED_BYTE, src);
    g.viewport(0, 0, w, h); g.useProgram(s.program);
    g.activeTexture(g.TEXTURE0); g.bindTexture(g.TEXTURE_2D, s.tex); g.uniform1i(s.uTex, 0);
    g.bindBuffer(g.ARRAY_BUFFER, s.pb); g.enableVertexAttribArray(s.aPos); g.vertexAttribPointer(s.aPos, 2, g.FLOAT, false, 0, 0);
    g.bindBuffer(g.ARRAY_BUFFER, s.ub); g.enableVertexAttribArray(s.aUv); g.vertexAttribPointer(s.aUv, 2, g.FLOAT, false, 0, 0);
    g.drawArrays(g.TRIANGLES, 0, 6); s.frames = (s.frames || 0) + 1;
  }
  function attach(name, canvas) {
    if (surfaces[name]) return;
    var s = initGL(canvas); s.name = name; s.canvas = canvas; s.src = null; s.bufW = 0; s.bufH = 0; surfaces[name] = s;
    window.__aotrino.onBuffer(name, function (buf, meta) { s.src = buf; s.bufW = meta.width | 0; s.bufH = meta.height | 0; });
    function report() {
      var dpr = window.devicePixelRatio || 1;
      var w = Math.max(1, Math.round(canvas.clientWidth * dpr)), h = Math.max(1, Math.round(canvas.clientHeight * dpr));
      if (w === s.rw && h === s.rh) return;
      s.rw = w; s.rh = h;
      window.__aotrino.post({ __aotrino: "surface-size", name: name, width: w, height: h });
    }
    new ResizeObserver(report).observe(canvas); report();
    (function frame() { try { draw(s); } catch (e) {} requestAnimationFrame(frame); })();
  }
  // .NET reuses the same shared buffer across a resize and only sends new dimensions (a light message)
  // instead of re-handing the whole buffer; keep the texture size in sync from it.
  window.chrome.webview.addEventListener("message", function (e) {
    var d = e.data;
    if (!d || d.__aotrino !== "surface-dims") return;
    var s = surfaces[d.name];
    if (s) { s.bufW = d.width | 0; s.bufH = d.height | 0; }
  });
  function autoAttach() { var els = document.querySelectorAll("canvas[data-aotrino-surface]"); for (var i = 0; i < els.length; i++) { attach(els[i].getAttribute("data-aotrino-surface"), els[i]); } }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", autoAttach); else autoAttach();
  window.__aotrinoGL = {
    attach: attach,
    readPixel: function (n, x, y) { var s = surfaces[n]; if (!s) return null; var p = new Uint8Array(4); s.gl.readPixels(x, y, 1, 1, s.gl.RGBA, s.gl.UNSIGNED_BYTE, p); return [p[0], p[1], p[2], p[3]]; },
    frames: function (n) { var s = surfaces[n]; return s ? (s.frames | 0) : 0; }
  };
})();
