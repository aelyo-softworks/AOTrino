import { useEffect, useState } from "react";
import { isHosted } from "@aotrino/client";
import { api } from "./api";

export function App() {
    const hosted = isHosted();

    return (
        <div className="app">
            {/* data-aotrino-drag makes the bar drag the window: the runtime AOTrino injects handles it,
                so there's no mousedown handler here. data-aotrino-nodrag keeps the button clickable. */}
            <header className="titlebar" data-aotrino-drag>
                <span className="title">AOTrino — React — Hello World</span>
                <button className="close" data-aotrino-nodrag onClick={() => void api.quit()} title="Close">
                    ✕
                </button>
            </header>

            <main>
                {!hosted && (
                    <p className="warning">
                        Running in a plain browser, so there is no .NET host. <code>isHosted()</code> is false,
                        window controls no-op and the calls below would throw. Launch the sample's executable to
                        see it hosted.
                    </p>
                )}

                <h1>React on AOTrino</h1>
                <p className="lead">
                    A React + TypeScript front end talking to .NET through <code>@aotrino/client</code>. The
                    host object is typed by the <code>DemoApi</code> interface in <code>src/api.ts</code>, which
                    mirrors <code>DemoApi.cs</code>.
                </p>

                <HostInfo hosted={hosted} />
                <Calls hosted={hosted} />
            </main>
        </div>
    );
}

interface HostInfoValues {
    machineName: string;
    framework: string;
    aotrinoVersion: string;
    webView2Version: string;
}

const noHostInfo: HostInfoValues = { machineName: "", framework: "", aotrinoVersion: "", webView2Version: "" };

// property reads cross the bridge as Promises, so even `api.machineName` is awaited. they're independent,
// so Promise.all fetches them in one round of calls instead of four sequential ones.
function HostInfo({ hosted }: { hosted: boolean }) {
    const [info, setInfo] = useState<HostInfoValues>(noHostInfo);

    useEffect(() => {
        if (!hosted)
            return;

        void (async () => {
            const [machineName, framework, aotrinoVersion, webView2Version] = await Promise.all([
                api.machineName,
                api.framework,
                api.aotrinoVersion,
                api.webView2Version,
            ]);
            setInfo({ machineName, framework, aotrinoVersion, webView2Version });
        })();
    }, [hosted]);

    return (
        <section className="card">
            <h2>Properties</h2>
            <dl>
                <dt>Machine</dt>
                <dd>{info.machineName || "—"}</dd>
                <dt>.NET Version</dt>
                <dd>{info.framework || "—"}</dd>
                <dt>AOTrino Version</dt>
                <dd>{info.aotrinoVersion || "—"}</dd>
                <dt>WebView2 Version</dt>
                <dd>{info.webView2Version || "—"}</dd>
            </dl>
        </section>
    );
}

function Calls({ hosted }: { hosted: boolean }) {
    const [ping, setPing] = useState("");
    const [sum, setSum] = useState<number | null>(null);
    const [text, setText] = useState("hello from React");
    const [echo, setEcho] = useState("");
    const [busy, setBusy] = useState(false);

    // every call is checked against DemoApi at compile time: rename a member in the interface
    // without renaming it in C# and TypeScript still passes, but rename the argument types and it won't
    const runEcho = async () => {
        setBusy(true);
        try {
            setEcho(await api.echoAsync(text));
        } finally {
            setBusy(false);
        }
    };

    return (
        <section className="card">
            <h2>Methods</h2>

            <div className="row">
                <button disabled={!hosted} onClick={async () => setPing(await api.ping())}>
                    ping()
                </button>
                <output>{ping || "—"}</output>
            </div>

            <div className="row">
                <button disabled={!hosted} onClick={async () => setSum(await api.add(2, 40))}>
                    add(2, 40)
                </button>
                <output>{sum ?? "—"}</output>
            </div>

            <div className="row">
                <input value={text} onChange={e => setText(e.target.value)} disabled={!hosted} />
                <button disabled={!hosted || busy} onClick={() => void runEcho()}>
                    {busy ? "…" : "echoAsync()"}
                </button>
                <output>{echo || "—"}</output>
            </div>
        </section>
    );
}
