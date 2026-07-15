import { useState } from "react";
import {
    Badge,
    Button,
    Card,
    CardHeader,
    Field,
    Input,
    MessageBar,
    MessageBarBody,
    Spinner,
    Text,
    makeStyles,
    tokens,
} from "@fluentui/react-components";
import { AOTrinoProvider, TitleBar, useAOTrinoTheme, useSystemThemeName } from "@aotrino/fluent";
import { useHostCall, useHostProperties, useHostValue, useIsHosted } from "@aotrino/react";
import { api, machineNameVariable, userNameVariable } from "./api";

const useStyles = makeStyles({
    content: {
        flexGrow: 1,
        overflowY: "auto",
        display: "flex",
        flexDirection: "column",
        rowGap: tokens.spacingVerticalL,
        paddingTop: tokens.spacingVerticalXXL,
        paddingBottom: tokens.spacingVerticalXXL,
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
    },
    values: {
        display: "grid",
        gridTemplateColumns: "max-content 1fr",
        columnGap: tokens.spacingHorizontalXL,
        rowGap: tokens.spacingVerticalS,
    },
    label: {
        color: tokens.colorNeutralForeground3,
    },
    row: {
        display: "flex",
        alignItems: "end",
        columnGap: tokens.spacingHorizontalM,
    },
});

export function App() {
    // AOTrinoProvider follows the Windows app theme, so there is no theme state to own here
    return (
        <AOTrinoProvider>
            <TitleBar title="AOTrino — FluentUI — Hello World" onClose={() => void api.quit()} />
            <Content />
        </AOTrinoProvider>
    );
}

function Content() {
    const styles = useStyles();
    const hosted = useIsHosted();
    const theme = useAOTrinoTheme();
    const system = useSystemThemeName();

    return (
        <div className={styles.content}>
            {!hosted && (
                <MessageBar intent="warning">
                    <MessageBarBody>
                        Running in a plain browser, so there is no .NET host. Fluent still renders and the theme
                        still works — only the bridge is missing.
                    </MessageBarBody>
                </MessageBar>
            )}

            <Text as="h1" size={700} weight="semibold">
                Fluent UI on AOTrino
            </Text>
            <Text>
                The whole pyramid in one window: <code>@aotrino/client</code> types the bridge,{" "}
                <code>@aotrino/react</code> supplies the hooks and the caption gesture, and{" "}
                <code>@aotrino/fluent</code> dresses it in Fluent. This app writes almost no CSS —{" "}
                Fluent&apos;s tokens do it.
            </Text>
            <Text>
                Pick a theme from the sun/moon button left of the window controls: it&apos;s{" "}
                <Badge appearance="tint" color={theme?.resolved.isDark ? "informative" : "warning"}>
                    {theme?.resolved.label ?? "—"}
                </Badge>{" "}
                now, {theme?.choice === "system" ? `following Windows (${system})` : "pinned by you"}. The
                choice is remembered across restarts, and while it follows Windows, changing the setting in
                Settings updates this live. None of that is code in this app — see{" "}
                <code>docs/THEMING.md</code>.
            </Text>

            <Host />
            <Greet hosted={hosted} />
        </div>
    );
}

function Host() {
    const styles = useStyles();
    const { values } = useHostProperties(api, ["framework", "aotrinoVersion", "webView2Version"]);

    // the machine and the user come from the process environment, asked for by name from here: the page
    // decides which variables it wants, and .NET just reads them. nothing about this machine is in the bundle.
    const environment = useHostValue(
        () => Promise.all([api.getEnvironmentVariable(machineNameVariable), api.getEnvironmentVariable(userNameVariable)]),
        [],
    );
    const [machineName, userName] = environment.value ?? [];

    return (
        <Card>
            <CardHeader header={<Text weight="semibold">Host</Text>} />
            <div className={styles.values}>
                <Text className={styles.label}>{machineNameVariable}</Text>
                <Text>{machineName ?? "—"}</Text>
                <Text className={styles.label}>{userNameVariable}</Text>
                <Text>{userName ?? "—"}</Text>
                <Text className={styles.label}>.NET</Text>
                <Text>{values.framework ?? "—"}</Text>
                <Text className={styles.label}>AOTrino</Text>
                <Text>{values.aotrinoVersion ?? "—"}</Text>
                <Text className={styles.label}>WebView2</Text>
                <Text>{values.webView2Version ?? "—"}</Text>
            </div>
        </Card>
    );
}

// useHostCall's pending drives a Fluent Spinner: the hook owns the state, Fluent owns the look
function Greet({ hosted }: { hosted: boolean }) {
    const styles = useStyles();
    // the name comes from the USERNAME environment variable, read by .NET - never a literal in the bundle.
    // `edited` stays null until the user types, so the field picks up the host value the moment it arrives
    // without clobbering anything typed in the meantime.
    const environment = useHostValue(() => api.getEnvironmentVariable(userNameVariable), []);
    const [edited, setEdited] = useState<string | null>(null);
    const name = edited ?? environment.value ?? "";
    const greet = useHostCall((who: string) => api.greetAsync(who));

    return (
        <Card>
            <CardHeader header={<Text weight="semibold">Call .NET</Text>} />
            <div className={styles.row}>
                <Field label="Your name">
                    <Input value={name} disabled={!hosted} onChange={(_, d) => setEdited(d.value)} />
                </Field>
                <Button appearance="primary" disabled={!hosted || greet.pending} onClick={() => void greet.call(name)}>
                    greetAsync()
                </Button>
                {greet.pending && <Spinner size="tiny" />}
            </div>
            {greet.result && (
                <MessageBar intent="success">
                    <MessageBarBody>{greet.result}</MessageBarBody>
                </MessageBar>
            )}
        </Card>
    );
}
