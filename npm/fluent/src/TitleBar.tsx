import type { ReactNode } from "react";
import {
    Button,
    Menu,
    MenuItemRadio,
    MenuList,
    MenuPopover,
    MenuTrigger,
    Text,
    makeStyles,
    mergeClasses,
    tokens,
} from "@fluentui/react-components";
import { Dismiss16Regular, Maximize16Regular, Subtract16Regular, WeatherMoon16Regular, WeatherSunny16Regular } from "@fluentui/react-icons";
import { appWindow } from "@aotrino/client";
import type { TitleBarLabels } from "@aotrino/react";
import { dragExcludeProps, useDragRegion, useWindowTitle } from "@aotrino/react";
import { useAOTrinoTheme } from "./ThemeContext.js";
import { systemThemeChoice } from "./themes.js";

// user-visible text. the window-button names come from @aotrino/react; the picker adds two of its own.
export interface FluentTitleBarLabels extends TitleBarLabels {
    theme: string;
    system: string;
}

const defaultLabels: FluentTitleBarLabels = {
    minimize: "Minimize",
    maximize: "Maximize",
    close: "Close",
    theme: "Theme",
    system: "System",
};

const themeMenuName = "theme";

const useStyles = makeStyles({
    root: {
        display: "flex",
        alignItems: "center",
        flexShrink: 0,
        height: "32px",
        paddingLeft: tokens.spacingHorizontalM,
        backgroundColor: tokens.colorNeutralBackground3,
        borderBottomWidth: "1px",
        borderBottomStyle: "solid",
        borderBottomColor: tokens.colorNeutralStroke2,
        // the whole bar is a drag region: don't let a drag (or the double-click) select the caption
        userSelect: "none",
    },
    title: {
        flexGrow: 1,
        color: tokens.colorNeutralForeground3,
    },
    buttons: {
        display: "flex",
        alignSelf: "stretch",
    },

    // Griffel forbids shorthands in makeStyles, it types them as `undefined`, so `borderRadius: 0` is a compile error rather than a surprise at runtime.
    button: {
        height: "100%",
        minWidth: "46px",
        maxWidth: "46px",
        borderTopLeftRadius: 0,
        borderTopRightRadius: 0,
        borderBottomLeftRadius: 0,
        borderBottomRightRadius: 0,
        borderTopWidth: 0,
        borderRightWidth: 0,
        borderBottomWidth: 0,
        borderLeftWidth: 0,
    },
    close: {
        ":hover": {
            backgroundColor: tokens.colorStatusDangerBackground3,
            color: tokens.colorNeutralForegroundOnBrand,
        },
        ":hover:active": {
            backgroundColor: tokens.colorStatusDangerBackground3Hover,
            color: tokens.colorNeutralForegroundOnBrand,
        },
    },
});

export interface TitleBarProps {
    // defaults to the window's own caption, the one Windows shows in the taskbar and Alt-Tab.
    // pass a string and the window is renamed to match, so the two can't disagree.
    // pass a node and only the bar changes (there's nothing sensible to call the window), so name the window in C# in that case.
    title?: ReactNode;
    // rendered between the title and the window buttons (a toolbar, tabs, ...)
    children?: ReactNode;
    className?: string;
    // the theme picker, to the left of the window buttons. hidden anyway outside an <AOTrinoProvider>, or when the app pinned a theme.
    showThemePicker?: boolean;
    showMinimize?: boolean;
    showMaximize?: boolean;
    showClose?: boolean;
    doubleClickToMaximize?: boolean;
    // defaults to closing the window; override to confirm first, save state, ...
    onClose?(): void;
    labels?: Partial<FluentTitleBarLabels>;
}

// a Windows-looking caption built from Fluent parts: Fluent buttons, Fluent icons, Fluent tokens, so it follows the theme (red close button included) with no CSS of your own.
// the gesture is not reimplemented here, the drag region and double-click-to-maximize come from @aotrino/react's useDragRegion, which is exactly why that hook exists.
// unlike @aotrino/react's headless TitleBar, this one shows all three window buttons by default: it stands in for the real caption.
export function TitleBar({
    title,
    children,
    className,
    showThemePicker = true,
    showMinimize = true,
    showMaximize = true,
    showClose = true,
    doubleClickToMaximize = true,
    onClose,
    labels,
}: TitleBarProps) {
    const styles = useStyles();
    const text = { ...defaultLabels, ...labels };
    const dragProps = useDragRegion({ doubleClickToMaximize });
    const caption = useWindowTitle(title);
    const theme = useAOTrinoTheme();
    const pickable = showThemePicker && theme != null && !theme.pinned && theme.options.length > 0;

    return (
        <header className={mergeClasses(styles.root, className)} {...dragProps}>
            <Text size={200} className={styles.title}>
                {caption}
            </Text>
            {children}
            <div className={styles.buttons} {...dragExcludeProps}>
                {pickable && (
                    <Menu
                        checkedValues={{ [themeMenuName]: [theme.choice] }}
                        onCheckedValueChange={(_, data) => theme.setChoice(data.checkedItems[0] ?? systemThemeChoice)}
                    >
                        <MenuTrigger disableButtonEnhancement>
                            <Button
                                appearance="subtle"
                                className={styles.button}
                                icon={theme.resolved.isDark ? <WeatherMoon16Regular /> : <WeatherSunny16Regular />}
                                aria-label={text.theme}
                            />
                        </MenuTrigger>
                        <MenuPopover>
                            <MenuList>
                                <MenuItemRadio name={themeMenuName} value={systemThemeChoice}>
                                    {text.system}
                                </MenuItemRadio>
                                {theme.options.map(option => (
                                    <MenuItemRadio key={option.key} name={themeMenuName} value={option.key}>
                                        {option.label}
                                    </MenuItemRadio>
                                ))}
                            </MenuList>
                        </MenuPopover>
                    </Menu>
                )}
                {showMinimize && (
                    <Button
                        appearance="subtle"
                        className={styles.button}
                        icon={<Subtract16Regular />}
                        aria-label={text.minimize}
                        onClick={() => appWindow.minimize()}
                    />
                )}
                {showMaximize && (
                    <Button
                        appearance="subtle"
                        className={styles.button}
                        icon={<Maximize16Regular />}
                        aria-label={text.maximize}
                        onClick={() => appWindow.maximize()}
                    />
                )}
                {showClose && (
                    <Button
                        appearance="subtle"
                        className={mergeClasses(styles.button, styles.close)}
                        icon={<Dismiss16Regular />}
                        aria-label={text.close}
                        onClick={onClose ?? (() => appWindow.close())}
                    />
                )}
            </div>
        </header>
    );
}
