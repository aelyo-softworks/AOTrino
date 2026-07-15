import type { Theme } from "@fluentui/react-components";
import { webDarkTheme, webLightTheme } from "@fluentui/react-components";

// a theme the user can pick from the caption.
// `label` is user-visible, so it belongs to the app, not to this package: replace the built-ins (or pass your own list) to ship another language or another design.
export interface AOTrinoThemeOption {
    key: string;
    label: string;
    theme: Theme;
    // drives the caption icon (sun or moon) and nothing else
    isDark?: boolean;
}

// "system" follows the Windows app theme; anything else is an option key
export type ThemeChoice = "system" | (string & {});

export const systemThemeChoice = "system";

// Light and Dark, because those are the two Windows itself offers.
// Fluent ships more(teamsLightTheme, teamsDarkTheme, teamsHighContrastTheme, ...) and any Theme object works.
// pass `themes` to AOTrinoProvider to add or replace these.
export const defaultThemeOptions: readonly AOTrinoThemeOption[] = [
    { key: "light", label: "Light", theme: webLightTheme },
    { key: "dark", label: "Dark", theme: webDarkTheme, isDark: true },
];
