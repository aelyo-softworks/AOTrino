import { useCallback, useRef, useState } from "react";

export interface HostCall<A extends unknown[], R> {
    call(...args: A): Promise<R | undefined>;
    result: R | undefined;
    pending: boolean;
    error: Error | null;
}

// wraps a host-object call with the pending/result/error state a UI actually needs, and turns a rejection
// into `error` rather than an unhandled promise rejection: a .NET method that throws crosses the bridge as
// a rejected promise, which is easy to drop on the floor by accident.
// pass a lambda - useHostCall((text: string) => api.echoAsync(text)) - rather than `api.echoAsync`:
// the bridge's proxy members shouldn't be detached from the object they came from.
export function useHostCall<A extends unknown[], R>(fn: (...args: A) => Promise<R>): HostCall<A, R> {
    // hold the newest lambda without letting `call` change identity on every render
    const latest = useRef(fn);
    latest.current = fn;

    const [result, setResult] = useState<R>();
    const [pending, setPending] = useState(false);
    const [error, setError] = useState<Error | null>(null);

    const call = useCallback(async (...args: A) => {
        setPending(true);
        setError(null);
        try {
            const value = await latest.current(...args);
            setResult(value);
            return value;
        } catch (e) {
            setError(e as Error);
            return undefined;
        } finally {
            setPending(false);
        }
    }, []);

    return { call, result, pending, error };
}
