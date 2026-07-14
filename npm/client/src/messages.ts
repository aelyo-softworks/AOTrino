import type { WebViewMessageEvent } from "./runtime.js";
import { runtime, webView } from "./runtime.js";

// post a message to .NET; raised there as WebViewWindow.WebMessageJsonReceived
export function post(message: unknown): void {
    runtime().post(message);
}

// subscribe to messages pushed from .NET (ICoreWebView2.PostWebMessageAsJson).
// returns an unsubscribe function.
export function onMessage<T = unknown>(callback: (message: T) => void): () => void {
    const view = webView();
    const handler = (e: Event) => callback((e as WebViewMessageEvent).data as T);
    view.addEventListener("message", handler);
    return () => view.removeEventListener("message", handler);
}
