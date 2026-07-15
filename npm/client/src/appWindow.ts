import { isHosted, runtime } from "./runtime.js";

// the native window hosting this page.
// the calls no - op in a plain browser so a custom title bar keeps rendering under `npm run dev` instead of throwing.
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

    // renames the window itself: the taskbar, Alt-Tab and the thumbnails, not just document.title.
    // a page drawing its own caption should call this, or the window ends up answering to two names.
    // outside AOTrino there's no window to rename, so this falls back to the document's title - the closest
    // equivalent a browser has, and what `npm run dev` shows in the tab.
    setTitle(title: string): void {
        if (isHosted()) {
            runtime().setWindowTitle(title);
        }
        else if (typeof document !== "undefined") {
            document.title = title;
        }
    },
};

// any element (or ancestor) carrying this attribute drags the window on left-mousedown
// the injected runtime handles it, so there's no need to call appWindow.drag() from an onMouseDown.
export const dragAttribute = "data-aotrino-drag";

// marks an interactive child inside a draggable region as clickable (buttons, inputs, ...)
export const dragExcludeAttribute = "data-aotrino-nodrag";
