import { useState } from "react";
import { TitleBar, useHostCall, useHostProperties, useIsHosted } from "@aotrino/react";
import { api } from "./api";

const autoRefreshMs = 1000;

export function App() {
    const hosted = useIsHosted();

    return (
        <div className="app">
            {/* behaviour comes from the package: the drag region, double-click to maximize, the window
                commands and the accessible names. the styling is entirely this app's, through the
                aotrino-titlebar-* class names. */}
            <TitleBar showMinimize showMaximize onClose={() => void api.quit()} />

            <main>
                {!hosted && (
                    <p className="warning">
                        Running in a plain browser, so there is no .NET host. The hooks all degrade instead of
                        throwing: <code>useIsHosted()</code> is false, <code>useHostProperties</code> settles
                        with no values and never starts its timer.
                    </p>
                )}

                <h1>Host dashboard</h1>
                <p className="lead">
                    Live .NET process state in a React front end, through <code>@aotrino/react</code>. No{" "}
                    <code>useEffect</code>, no <code>Promise.all</code>, no manual pending flags and no polling
                    timer — the hooks own all of it.
                </p>

                <Machine />
                <Process hosted={hosted} />
                <Analyze hosted={hosted} />
                <Failure hosted={hosted} />
            </main>
        </div>
    );
}

// fixed values: read once, no timer. one hook call fetches all nine in a single round of bridge calls.
function Machine() {
    const { values } = useHostProperties(api, [
        "machineName",
        "userName",
        "operatingSystem",
        "architecture",
        "processorCount",
        "framework",
        "aotrinoVersion",
        "webView2Version",
        "processId",
    ]);

    return (
        <section className="card">
            <h2>Machine</h2>
            <dl>
                <dt>Machine</dt>
                <dd>{values.machineName ?? "—"}</dd>
                <dt>User</dt>
                <dd>{values.userName ?? "—"}</dd>
                <dt>Operating system</dt>
                <dd>{values.operatingSystem ?? "—"}</dd>
                <dt>Architecture</dt>
                <dd>{values.architecture ?? "—"}</dd>
                <dt>Processors</dt>
                <dd>{values.processorCount ?? "—"}</dd>
                <dt>.NET</dt>
                <dd>{values.framework ?? "—"}</dd>
                <dt>AOTrino</dt>
                <dd>{values.aotrinoVersion ?? "—"}</dd>
                <dt>WebView2</dt>
                <dd>{values.webView2Version ?? "—"}</dd>
                <dt>Process id</dt>
                <dd>{values.processId ?? "—"}</dd>
            </dl>
        </section>
    );
}

// live values. ticking the checkbox polls them through the same hook: refreshIntervalMs is the whole
// change, and the timer stops on unmount and never starts when there is no host.
function Process({ hosted }: { hosted: boolean }) {
    const [auto, setAuto] = useState(false);
    const { values, loading, refresh } = useHostProperties(
        api,
        ["uptime", "workingSet", "managedHeap", "collections", "threadCount"],
        { refreshIntervalMs: auto ? autoRefreshMs : undefined },
    );
    const collect = useHostCall(() => api.collectGarbage());

    return (
        <section className="card">
            <h2>Process</h2>
            <dl>
                <dt>Uptime</dt>
                <dd>{values.uptime ?? "—"}</dd>
                <dt>Working set</dt>
                <dd>{values.workingSet ?? "—"}</dd>
                <dt>Managed heap</dt>
                <dd>{values.managedHeap ?? "—"}</dd>
                <dt>Collections</dt>
                <dd>{values.collections ?? "—"}</dd>
                <dt>Threads</dt>
                <dd>{values.threadCount ?? "—"}</dd>
            </dl>
            <div className="row">
                <button onClick={refresh} disabled={!hosted || auto || loading}>
                    refresh()
                </button>
                <label className="check">
                    <input type="checkbox" checked={auto} disabled={!hosted} onChange={e => setAuto(e.target.checked)} />
                    auto-refresh every {autoRefreshMs / 1000}s
                </label>
                <button disabled={!hosted || collect.pending} onClick={() => void collect.call()}>
                    collectGarbage()
                </button>
                <output>{collect.result ?? "—"}</output>
            </div>
        </section>
    );
}

// useHostCall owns pending, so the button disables itself while .NET is working
function Analyze({ hosted }: { hosted: boolean }) {
    const [text, setText] = useState("hooks make this boring, which is the point");
    const analyze = useHostCall((value: string) => api.analyzeAsync(value));

    return (
        <section className="card">
            <h2>useHostCall — pending</h2>
            <div className="row">
                <input value={text} onChange={e => setText(e.target.value)} disabled={!hosted} />
                <button onClick={() => void analyze.call(text)} disabled={!hosted || analyze.pending}>
                    {analyze.pending ? "analyzing…" : "analyzeAsync()"}
                </button>
            </div>
            <output>{analyze.result ?? "—"}</output>
        </section>
    );
}

// a .NET method that throws crosses the bridge as a rejected promise; the hook catches it into `error`
// instead of letting it become an unhandled rejection.
// what arrives is the full .NET exception: ToString() with the inner exception and the stack, including
// absolute source paths. that's useful in a log and wrong in a UI, so show the first line and keep the
// rest for the tooltip.
function Failure({ hosted }: { hosted: boolean }) {
    const fail = useHostCall(() => api.fail());
    const firstLine = fail.error?.message.split("\n", 1)[0];

    return (
        <section className="card">
            <h2>useHostCall — error</h2>
            <div className="row">
                <button onClick={() => void fail.call()} disabled={!hosted || fail.pending}>
                    fail()
                </button>
                <output className={fail.error ? "error" : undefined} title={fail.error?.message}>
                    {firstLine ?? "—"}
                </output>
            </div>
        </section>
    );
}
