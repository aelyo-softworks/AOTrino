import type { MouseEvent, ReactNode } from "react";
import { useRef } from "react";
import { appWindow, dragAttribute, dragExcludeAttribute } from "@aotrino/client";

// Windows' default double-click time. we time the gesture ourselves rather than listening for `dblclick`,
// because no dblclick ever arrives here: the first mousedown on a drag region starts a native window-move
// loop (ReleaseCapture + WM_NCLBUTTONDOWN), which swallows the mouseup, so the browser never completes a
// click pair. the second mousedown does reach us, and that's what this measures.
const doubleClickMs = 500;

// the drag region is handled by the runtime AOTrino injects, so there is no mousedown handler here;
// the buttons opt out of it so they stay clickable
const dragProps = { [dragAttribute]: "" };
const noDragProps = { [dragExcludeAttribute]: "" };

// accessible names for the window buttons. they're the only user-visible text this package renders, so
// they're overridable rather than baked in: an app that ships in another language passes its own.
export interface TitleBarLabels {
    minimize: string;
    maximize: string;
    close: string;
}

const defaultLabels: TitleBarLabels = { minimize: "Minimize", maximize: "Maximize", close: "Close" };

export interface TitleBarProps {
    title?: ReactNode;
    // rendered between the title and the window buttons (a toolbar, tabs, ...)
    children?: ReactNode;
    className?: string;
    showMinimize?: boolean;
    showMaximize?: boolean;
    showClose?: boolean;
    // double-clicking the caption maximizes or restores the window, as a native caption does. on by default
    // because this bar stands in for the OS one and users expect the gesture; turn it off for a window that
    // shouldn't be maximized.
    doubleClickToMaximize?: boolean;
    // defaults to closing the window; override to confirm first, save state, ...
    onClose?(): void;
    labels?: Partial<TitleBarLabels>;
}

// a draggable window title bar.
// headless on purpose: it ships behaviour (the drag region, the window commands, accessible names) and
// stable class names, but no styling at all, so it drops into any design system. A styled one belongs in
// @aotrino/fluent.
export function TitleBar({
    title,
    children,
    className = "aotrino-titlebar",
    showMinimize = false,
    showMaximize = false,
    showClose = true,
    doubleClickToMaximize = true,
    onClose,
    labels,
}: TitleBarProps) {
    const text = { ...defaultLabels, ...labels };
    const lastDownAt = useRef(0);

    // the native command is maximize-or-restore, so this toggles.
    function onMouseDown(e: MouseEvent<HTMLElement>) {
        if (!doubleClickToMaximize || e.button !== 0)
            return;

        // ignore presses on a button (or anything else opted out of the drag region): they bubble up here,
        // and a window that maximized because you double-clicked its close button would be a nasty surprise
        if ((e.target as Element).closest(`[${dragExcludeAttribute}]`))
            return;

        const secondClick = e.timeStamp - lastDownAt.current <= doubleClickMs;
        lastDownAt.current = secondClick ? 0 : e.timeStamp;
        if (!secondClick)
            return;

        // React listens on its root container, which is inside document, so this handler runs before the
        // injected runtime's document-level mousedown: stopping propagation here keeps it from starting
        // another native drag on top of the maximize.
        e.stopPropagation();
        appWindow.maximize();
    }

    return (
        <header className={className} onMouseDown={onMouseDown} {...dragProps}>
            <span className="aotrino-titlebar-title">{title}</span>
            {children}
            <span className="aotrino-titlebar-buttons" {...noDragProps}>
                {showMinimize && (
                    <button type="button" className="aotrino-titlebar-button" aria-label={text.minimize} onClick={() => appWindow.minimize()}>
                        &#x2500;
                    </button>
                )}
                {showMaximize && (
                    <button type="button" className="aotrino-titlebar-button" aria-label={text.maximize} onClick={() => appWindow.maximize()}>
                        &#x25A1;
                    </button>
                )}
                {showClose && (
                    <button type="button" className="aotrino-titlebar-button aotrino-titlebar-close" aria-label={text.close} onClick={onClose ?? (() => appWindow.close())}>
                        &#x2715;
                    </button>
                )}
            </span>
        </header>
    );
}
