import { isHosted, webView } from "./runtime.js";

// how the WebView2 bridge reshapes a .NET host object seen from JS:
// every member is asynchronous, methods return a Promise and property reads are themselves Promises.
// Awaited <> collapses the double wrapping for a C# method that already returns Task<T>.
export type AsyncHost<T> = {
    [K in keyof T]: T[K] extends (...args: infer A) => infer R ? (...args: A) => Promise<Awaited<R>> : Promise<T[K]>;
};

// the synchronous proxy (chrome.webview.hostObjects.sync): members behave like plain JS.
// every call blocks the page until .NET answers, so prefer the async proxy.
export type SyncHost<T> = {
    [K in keyof T]: T[K] extends (...args: infer A) => infer R ? (...args: A) => Awaited<R> : T[K];
};

const defaultHostName = "dotnet";

function unavailable<T extends object>(name: string): T {
    // throw on first use rather than on creation, so a module-scope `const api = host<Api>()` stays safe in a plain browser and callers can still branch on isHosted()
    return new Proxy({} as T, {
        get(_target, member) {
            throw new Error(`Host object '${name}.${String(member)}' is not available: this page is not running inside an AOTrino window.`);
        },
    });
}

// a typed view of a .NET host object registered with WebViewWindow.AddHostObject(name, ...).
// T describes the object's JS-visible surface; names cross the bridge case-insensitively, 
// so camelCase in TypeScript matches the PascalCase members in C#.
export function host<T extends object>(name: string = defaultHostName): AsyncHost<T> {
    if (!isHosted())
        return unavailable<AsyncHost<T>>(name);

    return webView().hostObjects[name] as AsyncHost<T>;
}

export function hostSync<T extends object>(name: string = defaultHostName): SyncHost<T> {
    if (!isHosted())
        return unavailable<SyncHost<T>>(name);

    return webView().hostObjects.sync[name] as SyncHost<T>;
}
