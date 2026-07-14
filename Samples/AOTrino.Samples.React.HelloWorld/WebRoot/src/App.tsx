import { useState } from "react";
import { TitleBar, useHostCall, useHostProperties, useIsHosted } from "@aotrino/react";
import { api } from "./api";

export function App() {
    const hosted = useIsHosted();

    return (
        <div className="app">
            {/* the drag region, the double-click-to-maximize gesture, the window buttons and their
                accessible names all come from the package; the styling is this app's. */}
            <TitleBar title="AOTrino — React — Hello World" onClose={() => void api.quit()} />

            <main>
                {!hosted && (
                    <p className="warning">
                        Running in a plain browser, so there is no .NET host. <code>useIsHosted()</code> is
                        false, window controls no-op and the hooks settle with no values. Launch the sample's
                        executable to see it hosted.
                    </p>
                )}

                <h1>React on AOTrino</h1>
                <p className="lead">
                    A React + TypeScript front end talking to .NET through <code>@aotrino/react</code>. The host
                    object is typed by the <code>DemoApi</code> interface in <code>src/api.ts</code>, which
                    mirrors <code>DemoApi.cs</code>.
                </p>

                <HostInfo />
                <Calls hosted={hosted} />
            </main>
        </div>
    );
}

// property reads cross the bridge as Promises. one hook reads them all in a single round of calls and
// owns the loading state.
function HostInfo() {
    const { values } = useHostProperties(api, ["machineName", "framework", "aotrinoVersion", "webView2Version"]);

    return (
        <section className="card">
            <h2>Properties</h2>
            <dl>
                <dt>Machine</dt>
                <dd>{values.machineName ?? "—"}</dd>
                <dt>.NET Version</dt>
                <dd>{values.framework ?? "—"}</dd>
                <dt>AOTrino Version</dt>
                <dd>{values.aotrinoVersion ?? "—"}</dd>
                <dt>WebView2 Version</dt>
                <dd>{values.webView2Version ?? "—"}</dd>
            </dl>
        </section>
    );
}

// every call is checked against DemoApi at compile time: the argument types and the result type both come
// from the interface, so a signature that drifts from the C# side stops compiling here.
function Calls({ hosted }: { hosted: boolean }) {
    const [text, setText] = useState("hello from React");
    const ping = useHostCall(() => api.ping());
    const add = useHostCall((a: number, b: number) => api.add(a, b));
    const echo = useHostCall((value: string) => api.echoAsync(value));

    return (
        <section className="card">
            <h2>Methods</h2>

            <div className="row">
                <button disabled={!hosted} onClick={() => void ping.call()}>
                    ping()
                </button>
                <output>{ping.result ?? "—"}</output>
            </div>

            <div className="row">
                <button disabled={!hosted} onClick={() => void add.call(2, 40)}>
                    add(2, 40)
                </button>
                <output>{add.result ?? "—"}</output>
            </div>

            <div className="row">
                <input value={text} onChange={e => setText(e.target.value)} disabled={!hosted} />
                <button disabled={!hosted || echo.pending} onClick={() => void echo.call(text)}>
                    {echo.pending ? "…" : "echoAsync()"}
                </button>
                <output>{echo.result ?? "—"}</output>
            </div>
        </section>
    );
}
