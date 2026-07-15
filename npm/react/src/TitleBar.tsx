import type { ReactNode } from "react";
import { appWindow } from "@aotrino/client";
import { dragExcludeProps, useDragRegion } from "./useDragRegion.js";

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
    doubleClickToMaximize?: boolean;
    // defaults to closing the window; override to confirm first, save state, ...
    onClose?(): void;
    labels?: Partial<TitleBarLabels>;
}

// a draggable window title bar.
// headless on purpose: it ships behaviour (via useDragRegion), the window commands and accessible names,
// plus stable class names - but no styling at all, so it drops into any design system. @aotrino/fluent
// builds its own caption on useDragRegion rather than restyling this one.
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
    const dragProps = useDragRegion({ doubleClickToMaximize });

    return (
        <header className={className} {...dragProps}>
            <span className="aotrino-titlebar-title">{title}</span>
            {children}
            <span className="aotrino-titlebar-buttons" {...dragExcludeProps}>
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
