import type { ReactNode } from "react";
import { useCallback, useMemo, useState } from "react";
import type { Theme } from "@fluentui/react-components";
import { FluentProvider, makeStyles, mergeClasses } from "@fluentui/react-components";
import type { AOTrinoThemeContextValue } from "./ThemeContext.js";
import { AOTrinoThemeContext } from "./ThemeContext.js";
import type { AOTrinoThemeOption, ThemeChoice } from "./themes.js";
import { defaultThemeOptions, systemThemeChoice } from "./themes.js";
import { useSystemThemeName } from "./useSystemTheme.js";

const useStyles = makeStyles({
    // a desktop window, not a document: fill it, and stack a caption above scrolling content.
    //
    // this goes on a div INSIDE FluentProvider, never on FluentProvider itself.
    // the provider copies its own className onto every portal mount node it creates (applyStylesToPortals, on by default, and needed: it's what themes popups).
    // put `height: 100vh` there and each portal becomes a full, window node,
    // which, carrying the theme's background too, paints an opaque rectangle at z-index 1000000 over the whole app for as long as a menu is open.
    window: {
        height: "100vh",
        display: "flex",
        flexDirection: "column",
        overflow: "hidden",
    },
});

export const defaultThemeStorageKey = "aotrino.theme";

export interface AOTrinoProviderProps {
    children?: ReactNode;
    // pin a theme: the app stops following Windows and the caption hides its theme picker
    theme?: Theme;
    // the themes on offer; defaults to Light and Dark
    themes?: readonly AOTrinoThemeOption[];
    // what to use before the user has picked anything. defaults to following Windows
    defaultChoice?: ThemeChoice;
    // where the choice is remembered. pass null to not remember it.
    // localStorage needs a real origin, which is why AOTrinoWindow.VirtualHostName matters here: a file:// page has an opaque origin and the choice would not survive a restart. 
    // see docs / THEMING.md.
    storageKey?: string | null;
    className?: string;
}

function readStoredChoice(storageKey: string | null): ThemeChoice | null {
    if (!storageKey)
        return null;

    // storage can throw outright (opaque origin, or the user disabled it), and a theme is never worth a crash
    try {
        return localStorage.getItem(storageKey);
    } catch {
        return null;
    }
}

// FluentProvider, wired to the Windows app theme and sized like a window rather than a page, plus the theme choice that <TitleBar>'s picker drives.
// this is the opinionated layer: it exists to make the common desktop case a one-liner.
// Everything it does is a choice you can undo, pass `theme` to pin one, `themes` to offer your own, `storageKey: null` to forget.
export function AOTrinoProvider({
    children,
    theme,
    themes = defaultThemeOptions,
    defaultChoice = systemThemeChoice,
    storageKey = defaultThemeStorageKey,
    className,
}: AOTrinoProviderProps) {
    const styles = useStyles();
    const systemName = useSystemThemeName();
    const [choice, setChoiceState] = useState<ThemeChoice>(() => readStoredChoice(storageKey) ?? defaultChoice);

    const setChoice = useCallback(
        (next: ThemeChoice) => {
            setChoiceState(next);
            if (!storageKey)
                return;

            try {
                localStorage.setItem(storageKey, next);
            } catch {
                // remembering the choice is a nicety; losing it must not break the app
            }
        },
        [storageKey],
    );

    const value = useMemo<AOTrinoThemeContextValue>(() => {
        const pinned = !!theme;
        // "system" resolves by matching the OS light/dark against the offered themes, so a custom list still gets system-following as long as it has a dark one and a light one
        const wanted = choice === systemThemeChoice ? undefined : themes.find(t => t.key === choice);
        const bySystem = themes.find(t => !!t.isDark === (systemName === "dark"));
        const resolved = wanted ?? bySystem ?? themes[0] ?? defaultThemeOptions[0];
        return { choice, setChoice, options: pinned ? [] : themes, resolved, pinned };
    }, [choice, setChoice, themes, systemName, theme]);

    return (
        <AOTrinoThemeContext.Provider value={value}>
            <FluentProvider theme={theme ?? value.resolved.theme}>
                <div className={mergeClasses(styles.window, className)}>{children}</div>
            </FluentProvider>
        </AOTrinoThemeContext.Provider>
    );
}
