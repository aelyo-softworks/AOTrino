import type { HTMLAttributes, MouseEvent } from "react";
import { useRef } from "react";
import { appWindow, dragAttribute, dragExcludeAttribute, system } from "@aotrino/client";

// the gesture is timed here rather than handled with an onDoubleClick, because no dblclick ever arrives on a drag region:
// the first mousedown starts a native window, move loop (ReleaseCapture + WM_NCLBUTTONDOWN) which swallows the mouseup, so the browser never completes a click pair.
// the second mousedown does reach us, and that is what this measures.
//
// the window it's measured against is Windows' own GetDoubleClickTime(), not a guess: the user can set it
// anywhere from 200 ms to 900 ms in Mouse Properties, and a caption that ignores that is a caption that
// feels broken to whoever changed it.

// TypeScript allows data-* attributes in JSX but not in HTMLAttributes, so name the two keys explicitly.
// dragAttribute/dragExcludeAttribute are const string literals, so `typeof` gives the exact key names and these stay in step with the client automatically.
export type DragRegionProps = HTMLAttributes<HTMLElement> & { [K in typeof dragAttribute]: string };
export type DragExcludeProps = { [K in typeof dragExcludeAttribute]: string };

export interface DragRegionOptions {
    // double-clicking the region maximizes or restores the window, as a native caption does.
    // on by default because a custom caption stands in for the OS one and users expect the gesture; turn it off for a window that shouldn't be maximized.
    doubleClickToMaximize?: boolean;
}

// props to spread on the element that should behave like a window caption:
// dragging it moves the window and double-clicking it maximizes or restores.
// this is the behaviour half of <TitleBar>, split out so a design system can render its own caption markup (Fluent buttons, icons, tokens) without reimplementing the gesture.
// mark interactive children with data-aotrino-nodrag to keep them clickable.
export function useDragRegion(options?: DragRegionOptions): DragRegionProps {
    const doubleClickToMaximize = options?.doubleClickToMaximize ?? true;
    const lastDownAt = useRef(0);

    // the native command is maximize-or-restore, so this toggles
    function onMouseDown(e: MouseEvent<HTMLElement>) {
        if (!doubleClickToMaximize || e.button !== 0)
            return;

        // ignore presses on a button (or anything else opted out of the drag region): they bubble up here,
        // and a window that maximized because you double-clicked its close button would be a nasty surprise
        if ((e.target as Element).closest(`[${dragExcludeAttribute}]`))
            return;

        const secondClick = e.timeStamp - lastDownAt.current <= system().doubleClickTimeMs;
        lastDownAt.current = secondClick ? 0 : e.timeStamp;
        if (!secondClick)
            return;

        // React listens on its root container, which is inside document,
        // so this handler runs before the injected runtime's document-level mousedown:
        // stopping propagation here keeps it from starting another native drag on top of the maximize.
        e.stopPropagation();
        appWindow.maximize();
    }

    return { onMouseDown, [dragAttribute]: "" };
}

// props to spread on an interactive child of a drag region, so it stays clickable
export const dragExcludeProps: DragExcludeProps = { [dragExcludeAttribute]: "" };
