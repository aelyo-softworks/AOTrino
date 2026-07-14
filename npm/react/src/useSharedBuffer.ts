import { useEffect, useState } from "react";
import type { BufferMetadata } from "@aotrino/client";
import { isHosted, onBuffer } from "@aotrino/client";

export interface SharedBufferState {
    buffer: ArrayBuffer | null;
    meta: BufferMetadata | null;
}

// subscribes to a named shared buffer (.NET's AOTrino.Bridge.SharedBuffer).
// .NET re-hands the buffer whenever it grows, and this re-renders with the new one, so never keep a
// reference to a previous buffer: the old ArrayBuffer is released on the .NET side.
export function useSharedBuffer(name: string): SharedBufferState {
    const [state, setState] = useState<SharedBufferState>({ buffer: null, meta: null });

    useEffect(() => {
        if (!isHosted())
            return;

        // onBuffer fires immediately when the buffer already arrived, and returns its unsubscribe
        return onBuffer(name, (buffer, meta) => setState({ buffer, meta }));
    }, [name]);

    return state;
}
