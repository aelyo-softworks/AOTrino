import { Badge, Body1, Link, makeStyles, tokens } from "@fluentui/react-components";
import { useHostProperties } from "@aotrino/react";
import { Example } from "../Example";
import { Page } from "./Page";
import { api } from "../api";

const useStyles = makeStyles({
    facts: {
        display: "grid",
        gridTemplateColumns: "max-content 1fr",
        columnGap: tokens.spacingHorizontalXL,
        rowGap: tokens.spacingVerticalS,
        width: "100%",
    },
    label: {
        color: tokens.colorNeutralForeground3,
    },
    stack: {
        display: "flex",
        flexDirection: "column",
        rowGap: tokens.spacingVerticalXS,
    },
});

export function HomePage() {
    const styles = useStyles();
    const { values } = useHostProperties(api, ["aotrinoVersion", "webView2Version", "framework"]);

    return (
        <Page
            title="AOTrino"
            lead={
                <>
                    An Electron-like platform for Windows desktop apps on .NET Native AOT and WebView2: one
                    executable, no runtime to install, no Chromium to ship. This window is one — everything you
                    see is React and Fluent UI, and everything it can do that a web page can't is on the pages
                    to the left, each with the code that does it.
                </>
            }
        >
            <Example
                title="What this window is"
                description="These come across the bridge from .NET, which is why they can name the runtime that's rendering them."
            >
                <div className={styles.facts}>
                    <Body1 className={styles.label}>AOTrino</Body1>
                    <Body1>{values.aotrinoVersion ?? "—"}</Body1>
                    <Body1 className={styles.label}>WebView2 runtime</Body1>
                    <Body1>{values.webView2Version ?? "—"}</Body1>
                    <Body1 className={styles.label}>.NET</Body1>
                    <Body1>{values.framework ?? "—"}</Body1>
                </div>
            </Example>

            <Example
                title="The stack"
                description="Four layers, each optional above the one below it. A hand-written index.html and no npm at all is a first-class way to use AOTrino — most of the other samples do exactly that."
            >
                <div className={styles.stack}>
                    <Body1>
                        <Badge appearance="tint" color="brand">AOTrino</Badge> — the C# runtime: the window, the
                        WebView, the bridge. Everything else is optional.
                    </Body1>
                    <Body1>
                        <Badge appearance="tint">@aotrino/client</Badge> — types over the bridge. Adds no runtime;
                        the C# side already injected it.
                    </Body1>
                    <Body1>
                        <Badge appearance="tint">@aotrino/react</Badge> — headless hooks and the caption gesture.
                        Behaviour, no CSS.
                    </Body1>
                    <Body1>
                        <Badge appearance="tint">@aotrino/fluent</Badge> — this look. A template choice, never a
                        platform mandate.
                    </Body1>
                </div>
            </Example>

            <Example
                title="Reading the gallery"
                description="Every card below is a live demo. Show code gives you the real source of the thing you just used, not a paraphrase."
                code={`// this card, for instance:
<Example title="Ping" code={/* ... */}>
    <Button onClick={() => void ping.call()}>ping()</Button>
    <Text>{ping.result}</Text>
</Example>`}
                source="src/Example.tsx"
            >
                <Body1>
                    Source paths are on the right of each card.{" "}
                    <Link onClick={() => void api.openExternal("https://github.com/aelyo-softworks/AOTrino")}>
                        AOTrino on GitHub
                    </Link>{" "}
                    — a real link, opened by .NET in your actual browser, because this window is{" "}
                    <code>NavigationMode.Local</code> and won't navigate away from itself. See Security.
                </Body1>
            </Example>
        </Page>
    );
}
