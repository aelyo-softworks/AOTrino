import { Body1, Spinner, makeStyles, tokens } from "@fluentui/react-components";
import { useHostValue } from "@aotrino/react";
import { Example } from "../Example";
import { Page } from "./Page";
import { system } from "../api";

const useStyles = makeStyles({
    json: {
        margin: 0,
        width: "100%",
        maxHeight: "460px",
        overflow: "auto",
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase300,
    },
});

export function SystemPage() {
    const styles = useStyles();
    const info = useHostValue(async () => JSON.parse(await system.getInfo()) as unknown, []);

    return (
        <Page
            title="System"
            lead={
                <>
                    Read-only facts a page has no other way of learning. Not the ones the browser already knows —
                    theme, DPI ratio, locale and screen size stay the browser's job, and duplicating them would
                    just make a second, worse source of truth.
                </>
            }
        >
            <Example
                title="Shipped, not registered"
                description={
                    <>
                        AOTrino builds <code>SystemInfo</code> so nobody hand-rolls DXGI enumeration for an about
                        box — and never registers it. Any page a window navigates to can call every host object
                        on that window, so exposing this is a decision per window. This one is{" "}
                        <code>Local</code> and only ever shows its own content.
                    </>
                }
                code={`protected override void RegisterHostObjects() =>
    AddHostObject("system", new SystemInfo(this));

// the values are a JSON DOM until you hand them over - yours to edit:
var info = new SystemInfo(this);
info.Values.Remove("adapters");        // this app has no business reporting the GPU
info.Values["tenant"] = currentTenant; // ...but it does report this
AddHostObject("system", info);`}
                source="AOTrino/SystemInfo.cs"
            >
                <Body1>
                    Deliberately absent: elevation state, and the machine and user names — those are an app's to
                    expose, from its own host object, if at all.
                </Body1>
            </Example>

            <Example
                title="What Windows says"
                description={
                    <>
                        The kernel version rather than the marketing one; the language Windows was installed in,
                        which is often not the one you're reading; every keyboard layout loaded; the monitors and
                        where they sit; the adapters. One call, not a round-trip per value.
                    </>
                }
                code={`const info = JSON.parse(await system.getInfo());`}
                source="AOTrino/SystemInfo.cs"
            >
                {info.loading ? (
                    <Spinner size="tiny" label="reading" />
                ) : (
                    <pre className={styles.json}>{JSON.stringify(info.value, null, 2)}</pre>
                )}
            </Example>
        </Page>
    );
}
