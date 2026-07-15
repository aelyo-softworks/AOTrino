import type { DependencyList } from "react";
import { useCallback, useEffect, useRef, useState } from "react";
import { isHosted } from "@aotrino/client";

export interface HostValue<T> {
    value: T | undefined;
    loading: boolean;
    error: Error | null;
    // call it again (for a value that moves)
    refresh(): void;
}

// calls a host method when the component mounts, and again whenever `deps` change, exposing the result.
// this is the method-shaped counterpart of useHostProperties: use it when the data comes from a call rather than a property. 
// use useHostCall instead when the * user * decides when it happens, not the render.
// outside AOTrino nothing is called and loading settles to false, so a component renders its fallback.
export function useHostValue<T>(fn: () => Promise<T>, deps: DependencyList): HostValue<T> {
    // hold the newest lambda so an inline arrow doesn't have to be in `deps`
    const latest = useRef(fn);
    latest.current = fn;

    const [value, setValue] = useState<T>();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<Error | null>(null);
    const [nonce, setNonce] = useState(0);

    useEffect(() => {
        if (!isHosted()) {
            setLoading(false);
            return;
        }

        let cancelled = false;
        setLoading(true);
        void (async () => {
            try {
                const read = await latest.current();
                if (cancelled)
                    return;

                setValue(read);
                setError(null);
            } catch (e) {
                if (!cancelled) {
                    setError(e as Error);
                }
            } finally {
                if (!cancelled) {
                    setLoading(false);
                }
            }
        })();

        return () => { cancelled = true; };
        // the caller's deps, plus refresh()
    }, [...deps, nonce]);

    const refresh = useCallback(() => setNonce(n => n + 1), []);
    return { value, loading, error, refresh };
}
