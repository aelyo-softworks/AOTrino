import type { BufferCallback, BufferMetadata } from "./runtime.js";
import { runtime } from "./runtime.js";

// the shared buffer .NET handed to the page (AOTrino.Bridge.SharedBuffer), or null if it hasn't arrived yet
export function getBuffer(name: string): ArrayBuffer | null {
    return runtime().getBuffer(name);
}

// the metadata that came with the buffer (whatever SharedBuffer.Post sent, plus its name)
export function getBufferMetadata(name: string): BufferMetadata | null {
    return runtime().getMeta(name);
}

// subscribe to a named shared buffer.
// fires immediately if the buffer already arrived, and again on every reallocation (.NET re-hands the buffer when it grows). 
// returns an unsubscribe function.
export function onBuffer(name: string, callback: BufferCallback): () => void {
    let active = true;
    runtime().onBuffer(name, (buffer, meta) => {
        if (active) {
            callback(buffer, meta);
        }
    });
    return () => { active = false; };
}
