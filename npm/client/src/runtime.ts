// the shape of the runtime AOTrino injects into every page (AOTrino/Resources/SharedBuffer.Runtime.js, installed by WebViewWindow.EnsureSharedRuntime).
// this package deliberately injects nothing of its own: 
// the C# side owns the runtime, and @aotrino/client only types and wraps what the host already put on the page. that keeps the two from drifting apart.

export interface BufferMetadata {
    readonly name: string;
    readonly [key: string]: unknown;
}

export type BufferCallback = (buffer: ArrayBuffer, meta: BufferMetadata) => void;

// Windows settings the web platform can't answer, injected with the runtime so they can be read
// synchronously. deliberately tiny: anything the browser already knows (theme, reduced motion, DPI, locale,
// screen, clipboard) is the browser's job, and anything an app wants is the app's, through a host object.
export interface AOTrinoSystem {
    // GetDoubleClickTime(). the user's setting, not a guess: Mouse Properties can put it anywhere
    // from 200 ms to 900 ms.
    readonly doubleClickTimeMs: number;
}

export interface AOTrinoRuntime {
    readonly system: AOTrinoSystem;
    getBuffer(name: string): ArrayBuffer | null;
    getMeta(name: string): BufferMetadata | null;
    onBuffer(name: string, callback: BufferCallback): void;
    post(message: unknown): void;
    dragWindow(): void;
    closeWindow(): void;
    minimizeWindow(): void;
    maximizeWindow(): void;
}

export interface WebViewMessageEvent extends Event {
    readonly data: unknown;
}

export interface WebView extends EventTarget {
    postMessage(message: unknown): void;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    hostObjects: Record<string, any> & { sync: Record<string, any> };
}

declare global {
    interface Window {
        __aotrino?: AOTrinoRuntime;
        chrome?: { webview?: WebView };
    }
}

const notHostedMessage = "AOTrino is not available: this page is not running inside an AOTrino window.";

// true when the page runs inside an AOTrino window, false in a plain browser (e.g. `npm run dev`).
// branch on this to render a fallback instead of throwing.
export function isHosted(): boolean {
    return typeof window !== "undefined" && !!window.__aotrino;
}

export function runtime(): AOTrinoRuntime {
    const injected = typeof window !== "undefined" ? window.__aotrino : undefined;
    if (!injected)
        throw new Error(notHostedMessage);

    return injected;
}

// what Windows says, with the documented defaults when there's no host (a plain browser under `npm run dev`)
export function system(): AOTrinoSystem {
    return (typeof window !== "undefined" ? window.__aotrino?.system : undefined) ?? defaultSystem;
}

// Windows' own default double-click time, for when nothing can be asked
const defaultSystem: AOTrinoSystem = { doubleClickTimeMs: 500 };

export function webView(): WebView {
    const view = typeof window !== "undefined" ? window.chrome?.webview : undefined;
    if (!view)
        throw new Error(notHostedMessage);

    return view;
}
