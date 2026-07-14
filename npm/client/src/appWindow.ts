import { isHosted, runtime } from "./runtime.js";

// the native window hosting this page. the calls no-op in a plain browser so a custom title bar
// keeps rendering under `npm run dev` instead of throwing.
export const appWindow = {
    drag(): void {
        if (isHosted()) {
            runtime().dragWindow();
        }
    },

    close(): void {
        if (isHosted()) {
            runtime().closeWindow();
        }
    },

    minimize(): void {
        if (isHosted()) {
            runtime().minimizeWindow();
        }
    },

    // toggles: the native side maximizes a normal window and restores a maximized one
    maximize(): void {
        if (isHosted()) {
            runtime().maximizeWindow();
        }
    },
};

// any element (or ancestor) carrying this attribute drags the window on left-mousedown - the injected
// runtime handles it, so there's no need to call appWindow.drag() from an onMouseDown.
export const dragAttribute = "data-aotrino-drag";

// marks an interactive child inside a draggable region as clickable (buttons, inputs, ...)
export const dragExcludeAttribute = "data-aotrino-nodrag";
