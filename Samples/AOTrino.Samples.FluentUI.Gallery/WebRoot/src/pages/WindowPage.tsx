import { useState } from "react";
import { Body1, Button, Text } from "@fluentui/react-components";
import { appWindow } from "@aotrino/client";
import { useHostCall } from "@aotrino/react";
import { Example } from "../Example";
import { Page } from "./Page";
import { api } from "../api";

type BackdropType = "mica" | "acrylic" | "tabbed" | "none";

const backdrops: BackdropType[] = ["mica", "acrylic", "tabbed", "none"];

export function WindowPage() {
    const backdrop = useHostCall((type: BackdropType) => api.setBackdrop(type));
    const [current, setCurrent] = useState<BackdropType>("none");

    async function apply(type: BackdropType) {
        await backdrop.call(type);
        setCurrent(type);

        // the material is behind the window; the page has to stop painting over it (app.css)
        document.documentElement.toggleAttribute("data-backdrop", type !== "none");
    }

    return (
        <Page
            title="Window"
            lead={
                <>
                    A web page can't move its own window, and it can't ask Windows for a material. This page is a
                    real HWND: the caption above is HTML, the behaviour under it is Win32.
                </>
            }
        >
            <Example
                title="The caption"
                description={
                    <>
                        The bar at the top is not a native caption — it's this app's markup. Drag it: the window
                        moves. Double-click it: it maximizes and restores, at whatever double-click speed you
                        have set in Mouse Properties.
                    </>
                }
                code={`// @aotrino/fluent
<TitleBar title="AOTrino — Gallery" onClose={() => void api.quit()} />

// any element can be a drag region - the injected runtime handles it, no mousedown handler:
<header data-aotrino-drag>
    <button data-aotrino-nodrag>...</button>   {/* stays clickable */}
</header>`}
                source="npm/fluent/src/TitleBar.tsx"
            >
                <Body1>Try it on the bar above — drag, then double-click.</Body1>
            </Example>

            <Example
                title="Window commands"
                description="The same three commands the caption buttons use. maximize() toggles: the native side does maximize-or-restore."
                code={`import { appWindow } from "@aotrino/client";

appWindow.minimize();
appWindow.maximize();   // toggles
appWindow.close();`}
                source="npm/client/src/appWindow.ts"
            >
                <Button onClick={() => appWindow.minimize()}>minimize()</Button>
                <Button onClick={() => appWindow.maximize()}>maximize()</Button>
            </Example>

            <Example
                title="System backdrop"
                description={
                    <>
                        Mica and Acrylic are DWM materials: Windows draws them behind the window, so they are only
                        visible through pixels the page didn't paint. Two things have to be true, and both are here
                        — the WebView is transparent, and this page lifts Fluent's background while a backdrop is
                        on. Pick one and move the window over something colourful: the tint follows what's behind.
                        The caption, the nav and the cards keep painting, so they stay opaque on top of it.
                    </>
                }
                code={`// C#: a window-level Windows feature, not a CSS one
window.SetSystemBackdrop(DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW);   // Mica

/* CSS: and the page has to get out of the way, or it just paints over the material */
html[data-backdrop] .fui-FluentProvider {
    background-color: transparent;
}`}
                source="Samples/…/GalleryApi.cs"
            >
                {backdrops.map(type => (
                    <Button
                        key={type}
                        appearance={current === type ? "primary" : "secondary"}
                        onClick={() => void apply(type)}
                    >
                        {type}
                    </Button>
                ))}
                <Text>{backdrop.error ? String(backdrop.error) : ""}</Text>
            </Example>
        </Page>
    );
}
