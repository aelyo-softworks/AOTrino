// injected into every document (AddStartupScriptResource): a fixed browser bar. it navigates the top-level
// document (location.href), so it works on any site, unlike an <iframe>, which most sites refuse to be framed in.
(function () {
    if (window.top !== window.self) return; // don't inject into sub-frames
    function build() {
        if (document.getElementById("__aotrinoBar")) return;
        var css = document.createElement("style");
        css.textContent =
            "#__aotrinoBar{position:fixed;top:0;left:0;right:0;height:44px;z-index:2147483647;display:flex;align-items:center;gap:6px;padding:0 8px;background:#11161d;border-bottom:1px solid #30363d;box-sizing:border-box;font-family:'Segoe UI',system-ui,sans-serif}" +
            "#__aotrinoBar button{font:15px system-ui;height:30px;min-width:32px;padding:0 9px;border-radius:6px;border:1px solid #30363d;background:#21262d;color:#e6edf3;cursor:pointer}" +
            "#__aotrinoBar button:hover{border-color:#58a6ff;background:#262c34}" +
            "#__aotrinoBar .x:hover{border-color:#ff7b72;background:#2d1f1f}" +
            "#__aotrinoBar input{flex:1;height:30px;padding:0 11px;border-radius:6px;border:1px solid #30363d;background:#0b0f14;color:#e6edf3;font:14px 'Cascadia Code',Consolas,monospace;outline:none}" +
            "#__aotrinoBar input:focus{border-color:#58a6ff}";
        document.documentElement.appendChild(css);

        var bar = document.createElement("div");
        bar.id = "__aotrinoBar";
        bar.innerHTML =
            '<button data-a="back" title="Back (Alt+Left)">&#9664;</button>' +
            '<button data-a="fwd" title="Forward (Alt+Right)">&#9654;</button>' +
            '<button data-a="reload" title="Reload (F5)">&#8635;</button>' +
            '<input id="__aotrinoUrl" spellcheck="false" placeholder="Enter a URL or search, then Enter" />' +
            '<button data-a="go">Go</button>' +
            '<button data-a="close" class="x" title="Close">&#10005;</button>';
        document.documentElement.appendChild(bar);
        document.body.style.paddingTop = "44px";
        location.href = "https://github.com/aelyo-softworks/AOTrino";

        var url = document.getElementById("__aotrinoUrl");
        url.value = location.href;

        function nav(raw) {
            raw = (raw || "").trim();
            if (!raw) return;
            var u = /^[a-z][a-z0-9+.-]*:\/\//i.test(raw) ? raw
                : /^\S+\.\S+$/.test(raw) ? "https://" + raw
                    : "https://www.bing.com/search?q=" + encodeURIComponent(raw);
            location.href = u;
        }

        bar.addEventListener("click", function (e) {
            var b = e.target.closest("button");
            if (!b) return;
            var a = b.getAttribute("data-a");
            if (a === "back") history.back();
            else if (a === "fwd") history.forward();
            else if (a === "reload") location.reload();
            else if (a === "go") nav(url.value);
            else if (a === "close" && window.__aotrino) window.__aotrino.closeWindow();
        });
        url.addEventListener("keydown", function (e) { if (e.key === "Enter") nav(url.value); });
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", build);
    else build();
})();
