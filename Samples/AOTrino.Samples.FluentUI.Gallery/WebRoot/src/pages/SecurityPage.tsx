import { Body1, Link, MessageBar, MessageBarBody, Text, makeStyles, tokens } from "@fluentui/react-components";
import { useHostValue } from "@aotrino/react";
import { Example } from "../Example";
import { Page } from "./Page";
import { api } from "../api";

const useStyles = makeStyles({
    mono: {
        fontFamily: tokens.fontFamilyMonospace,
    },
    // Example lays its demo out as a wrapping row: take the whole row, and let the text wrap in it
    fill: {
        flexBasis: "100%",
        minWidth: 0,
    },
});

export function SecurityPage() {
    const styles = useStyles();
    // location.origin is the whole point of this page, so read it rather than describe it
    const origin = useHostValue(async () => window.location.origin, []);

    return (
        <Page
            title="Security"
            lead={
                <>
                    AOTrino makes exactly one promise and keeps it in code: a window stays on your app's own
                    content and won't wander onto the web unless you say so. Everything else is yours to grant,
                    explicitly and by name — which is the point, not a gap.
                </>
            }
        >
            <Example
                title="The one rule: navigation"
                description={
                    <>
                        The link below is a real <code>&lt;a href&gt;</code> to github.com. This window is{" "}
                        <code>NavigationMode.Local</code>, so the navigation is cancelled and handed to your
                        actual browser — the window itself never leaves its own content.
                    </>
                }
                code={`public NavigationMode NavigationMode { get; set; } = NavigationMode.Local;

// Local, but allow one trusted origin:
protected override bool IsNavigationAllowed(Uri uri) =>
    base.IsNavigationAllowed(uri) || uri.Host == "maps.myservice.com";`}
                source="docs/SECURITY.md"
            >
                <Body1>
                    <Link href="https://github.com/aelyo-softworks/AOTrino">Click me — I open in your browser</Link>
                </Body1>
            </Example>

            <Example
                title="Where this content comes from"
                description={
                    <>
                        Not <code>file://</code>. The WebRoot is served from a virtual host, so the page has an
                        ordinary https origin: ES modules load, storage works, and the page cannot read
                        arbitrary local files — that capability is never granted rather than granted and hoped
                        about.
                    </>
                }
                code={`// .example is reserved (RFC 2606) - it can never collide with a real domain
protected override string? VirtualHostName => "aotrino.example";`}
                source="docs/SECURITY.md"
            >
                <Text className={styles.mono}>location.origin = {origin.value ?? "—"}</Text>
            </Example>

            <Example
                title="Host objects belong to the window"
                description={
                    <>
                        A host object is reachable from <em>every</em> document its window loads, whatever the
                        origin — WebView2's <code>AddHostObjectToScript</code> takes no origin filter. So the
                        rule is about the window, not the object.
                    </>
                }
                code={`// measured, with example.com loaded in a NavigationMode.Web window:
chrome.webview.hostObjects.sync.secret.getSecret()   // -> "simon@SMO03"

// so: register host objects on windows that show YOUR content.
// a window that browses the web gets none. need both? use two windows.`}
                source="docs/SECURITY.md"
            >
                <MessageBar intent="warning" layout="multiline" className={styles.fill}>
                    <MessageBarBody>
                        This window registers two host objects and never navigates off its own content. The
                        WebBrowser sample navigates anywhere and registers none. That contrast is the whole
                        model.
                    </MessageBarBody>
                </MessageBar>
            </Example>

            <Example
                title="What AOTrino doesn't own"
                description={
                    <>
                        Filesystem, shell, dialogs, printing, power. AOTrino neither grants nor blocks them — it
                        just doesn't pretend they're platform features. They're a class you own, twenty lines
                        long, and the responsibility lands where the decision was made.
                    </>
                }
                code={`// this gallery's own "open a link" is exactly that:
public bool OpenExternal(string url)
{
    Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = url });
    return true;
}`}
                source="Samples/…/GalleryApi.cs"
            >
                <Body1>
                    A platform that promises broad safety writes a cheque it can't cash. AOTrino makes the safe
                    thing the default and the dangerous thing an explicit, clearly-named opt-in —{" "}
                    <Link onClick={() => void api.openExternal("https://github.com/aelyo-softworks/AOTrino/blob/main/docs/SECURITY.md")}>
                        the whole model fits on one page
                    </Link>
                    .
                </Body1>
            </Example>
        </Page>
    );
}
