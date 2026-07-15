import { useState } from "react";
import { isHosted } from "@aotrino/client";

// whether the page is running inside an AOTrino window rather than a plain browser.
// AOTrino injects its runtime before any page script runs, so this can't change for the life of the document: 
// it's read once and kept, which also keeps it stable across renders.
export function useIsHosted(): boolean {
    const [hosted] = useState(() => isHosted());
    return hosted;
}
