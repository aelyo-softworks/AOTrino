import type { ReactNode } from "react";
import { useEffect } from "react";
import { appWindow, windowInfo } from "@aotrino/client";

// keeps a drawn caption and the window's real name the same thing, and returns what the caption should say.
// a window has one name, and a page that draws its own title bar has taken over showing it: if the markup says
// one thing while the taskbar, Alt-Tab and the thumbnails say another, the app is answering to two names, and
// nobody wrote that on purpose.
// so: pass a string and the window is renamed to match; pass a node (an icon, a path, markup) and only the bar
// changes, because there's nothing sensible to call the window - name that window in C#; pass nothing and the
// window's own name is drawn, which is the common case and needs no code at all.
export function useWindowTitle(title?: ReactNode): ReactNode {
    const rename = typeof title === "string" ? title : null;

    useEffect(() => {
        if (rename !== null)
            appWindow.setTitle(rename);
    }, [rename]);

    return title ?? windowInfo().title;
}
