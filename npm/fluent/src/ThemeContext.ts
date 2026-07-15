import { createContext, useContext } from "react";
import type { AOTrinoThemeOption, ThemeChoice } from "./themes.js";

export interface AOTrinoThemeContextValue {
    // what the user picked: "system", or an option key
    choice: ThemeChoice;
    setChoice(choice: ThemeChoice): void;
    // the themes on offer; empty when the app pinned a theme
    options: readonly AOTrinoThemeOption[];
    // the option actually applied right now (with "system" already resolved to light or dark)
    resolved: AOTrinoThemeOption;
    // true when the app pinned a theme, so there is nothing to pick
    pinned: boolean;
}

export const AOTrinoThemeContext = createContext<AOTrinoThemeContextValue | null>(null);

// the current theme and how to change it. returns null outside an <AOTrinoProvider>, 
// so a component can degrade (the caption simply hides its theme button) instead of throwing.
export function useAOTrinoTheme(): AOTrinoThemeContextValue | null {
    return useContext(AOTrinoThemeContext);
}
