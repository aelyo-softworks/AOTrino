import { useEffect, useState } from "react";
import type { Theme } from "@fluentui/react-components";
import { webDarkTheme, webLightTheme } from "@fluentui/react-components";

const darkQuery = "(prefers-color-scheme: dark)";

export type SystemThemeName = "light" | "dark";

// follows the Windows app theme: WebView2 maps the OS setting onto prefers-color-scheme,
// so a desktop app gets light/dark for free, no host object, no .NET, and it tracks the user changing it in Settings while the app is running.
export function useSystemThemeName(): SystemThemeName {
    const [name, setName] = useState<SystemThemeName>(() =>
        typeof window !== "undefined" && window.matchMedia(darkQuery).matches ? "dark" : "light",
    );

    useEffect(() => {
        const media = window.matchMedia(darkQuery);
        const onChange = (e: MediaQueryListEvent) => setName(e.matches ? "dark" : "light");
        media.addEventListener("change", onChange);
        return () => media.removeEventListener("change", onChange);
    }, []);

    return name;
}

// the matching Fluent theme
export function useSystemTheme(): Theme {
    return useSystemThemeName() === "dark" ? webDarkTheme : webLightTheme;
}
