import { useEffect, useRef } from "react";
import { isHosted, onMessage } from "@aotrino/client";

// subscribes to messages pushed from .NET (ICoreWebView2.PostWebMessageAsJson).
// the callback lives in a ref, so passing an inline lambda doesn't tear down and resubscribe the
// listener on every render.
export function useHostMessage<T = unknown>(callback: (message: T) => void): void {
    const latest = useRef(callback);
    latest.current = callback;

    useEffect(() => {
        if (!isHosted())
            return;

        return onMessage<T>(message => latest.current(message));
    }, []);
}
