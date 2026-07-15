import { TitleBar, useIsHosted } from "@aotrino/react";

export function App() {
    // false in a plain browser (npm run dev): there's no .NET on the other side, so the window controls
    // no-op and anything host-shaped has to render a fallback rather than throw
    const hosted = useIsHosted();

    return (
        <div className="app">
            {/* the drag region, double-click-to-maximize, the window buttons and their accessible names come
                with this; the styling is yours. no title prop: it defaults to the window's own caption. */}
            <TitleBar showMinimize showMaximize />

            <main>
                <h1>AOTrinoApp1</h1>
                <p>{hosted ? "Running in an AOTrino window." : "Running in a browser - no .NET host."}</p>
                <p>
                    Edit <code>WebRoot/src/App.tsx</code>. To call .NET, add a host object in
                    <code> MainWindow.cs</code> and type it on this side - see docs/BRIDGE.md.
                </p>
            </main>
        </div>
    );
}
