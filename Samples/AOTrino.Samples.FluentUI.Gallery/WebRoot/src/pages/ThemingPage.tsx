import { useEffect, useRef, useState } from "react";
import { Badge, Body1, Button, Caption1, Text, makeStyles, shorthands, tokens } from "@fluentui/react-components";
import { useAOTrinoTheme, useSystemThemeName } from "@aotrino/fluent";
import { Example } from "../Example";
import { Page } from "./Page";

const useStyles = makeStyles({
    swatches: {
        display: "flex",
        flexWrap: "wrap",
        columnGap: tokens.spacingHorizontalL,
        rowGap: tokens.spacingVerticalM,
    },
    token: {
        display: "flex",
        flexDirection: "column",
        rowGap: tokens.spacingVerticalXXS,
    },
    swatch: {
        width: "100%",
        height: "32px",
        // Griffel forbids CSS shorthands through its types, so `border: ...` is a compile error.
        // this is the escape hatch it ships for exactly that - it expands to the longhands for you.
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border("1px", "solid", tokens.colorNeutralStroke2),
    },
    name: {
        fontFamily: tokens.fontFamilyMonospace,
    },
    value: {
        fontFamily: tokens.fontFamilyMonospace,
        color: tokens.colorNeutralForeground3,
    },
    brand: { backgroundColor: tokens.colorBrandBackground },
    neutral: { backgroundColor: tokens.colorNeutralBackground1 },
    subtle: { backgroundColor: tokens.colorNeutralBackground3 },
    danger: { backgroundColor: tokens.colorStatusDangerBackground3 },
});

const swatchNames = [
    "colorBrandBackground",
    "colorNeutralBackground1",
    "colorNeutralBackground3",
    "colorStatusDangerBackground3",
] as const;

export function ThemingPage() {
    const styles = useStyles();
    const theme = useAOTrinoTheme();
    const system = useSystemThemeName();
    const swatchStyles = [styles.brand, styles.neutral, styles.subtle, styles.danger];
    const swatches = useRef<HTMLDivElement>(null);
    const [values, setValues] = useState<Partial<Record<string, string>>>({});

    // read what the tokens currently resolve to. they're CSS variables set by the provider above us,
    // so ask an element inside it rather than :root, and ask again whenever the theme swaps them.
    useEffect(() => {
        const element = swatches.current;
        if (!element)
            return;

        const style = getComputedStyle(element);
        setValues(Object.fromEntries(swatchNames.map(name => [name, style.getPropertyValue(`--${name}`).trim()])));
    }, [theme?.resolved.label]);

    return (
        <Page
            title="Theming"
            lead={
                <>
                    A desktop app should look like the desktop it's on. This one follows the Windows app theme
                    with no .NET involved at all, and remembers you overriding it.
                </>
            }
        >
            <Example
                title="Following Windows"
                description={
                    <>
                        WebView2 maps the Windows setting onto the <code>prefers-color-scheme</code> media query,
                        so this is <code>matchMedia</code> plus a listener — no host object, no polling. Change
                        it in Settings → Personalization → Colors and this window follows while it runs.
                    </>
                }
                code={`// @aotrino/fluent, in one line
<AOTrinoProvider>...</AOTrinoProvider>

// what it's doing:
window.matchMedia("(prefers-color-scheme: dark)")`}
                source="npm/fluent/src/useSystemTheme.ts"
            >
                <Body1>
                    Windows says <Badge appearance="tint">{system}</Badge>, this window is showing{" "}
                    <Badge appearance="tint" color={theme?.resolved.isDark ? "informative" : "warning"}>
                        {theme?.resolved.label ?? "—"}
                    </Badge>{" "}
                    {theme?.choice === "system" ? "(following)" : "(pinned by you)"}
                </Body1>
            </Example>

            <Example
                title="The picker"
                description="The sun/moon button left of the window controls. The choice lives in localStorage — which works because this window serves its content from a virtual host: a file:// page has an opaque origin and wouldn't keep it."
                code={`// the caption renders it when there's a provider above it and nothing is pinned
<TitleBar showThemePicker />

// or drive it yourself
const theme = useAOTrinoTheme();
theme?.setChoice("dark");`}
                source="docs/THEMING.md"
            >
                <Button onClick={() => theme?.setChoice("system")}>System</Button>
                <Button onClick={() => theme?.setChoice("light")}>Light</Button>
                <Button onClick={() => theme?.setChoice("dark")}>Dark</Button>
                <Text>choice: {theme?.choice ?? "—"}</Text>
            </Example>

            <Example
                title="Why the theme switch is one line: tokens"
                description={
                    <>
                        A token is a name for a <em>role</em> — "the brand background", "the danger background" —
                        not a colour. You write the name, and whichever theme is loaded supplies the value, as a CSS
                        variable. That's the whole trick behind the picker above: nothing re-renders a palette,
                        the variables simply resolve to different colours, and everything built out of them
                        follows for free. Flip the theme and watch the hex codes below change while the names,
                        and this app's CSS, stay exactly as they are.
                    </>
                }
                code={`const useStyles = makeStyles({
    card: { backgroundColor: tokens.colorNeutralBackground3 },  // a role, not #f5f5f5
});

// which compiles to a CSS variable the theme fills in:
//   .fk9d3s { background-color: var(--colorNeutralBackground3); }`}
                source="npm/fluent/README.md"
            >
                <div className={styles.swatches} ref={swatches}>
                    {swatchNames.map((name, i) => (
                        <div key={name} className={styles.token}>
                            <div className={`${styles.swatch} ${swatchStyles[i]}`} />
                            <Caption1 className={styles.name}>{name}</Caption1>
                            <Caption1 className={styles.value}>{values[name] ?? "…"}</Caption1>
                        </div>
                    ))}
                </div>
            </Example>
        </Page>
    );
}
