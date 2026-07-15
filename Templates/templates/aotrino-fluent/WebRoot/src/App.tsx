import { Body1, Title2, makeStyles, tokens } from "@fluentui/react-components";
import { AOTrinoProvider, TitleBar } from "@aotrino/fluent";

const useStyles = makeStyles({
    main: {
        flexGrow: 1,
        overflowY: "auto",
        padding: tokens.spacingHorizontalXXL,
        display: "flex",
        flexDirection: "column",
        rowGap: tokens.spacingVerticalM,
    },
});

export function App() {
    const styles = useStyles();

    return (
        // FluentProvider wired to the Windows app theme, sized like a window rather than a document.
        // it follows the OS live - no host object, no .NET involved.
        <AOTrinoProvider>
            {/* drag, double-click to maximize, the theme picker and the window buttons all come with this.
                no title prop: it defaults to the window's own caption. */}
            <TitleBar />

            <main className={styles.main}>
                <Title2 as="h1">AOTrinoApp1</Title2>
                <Body1>
                    Fluent UI, rendering in a WebView2 that happens to be a Windows window. Edit
                    <code> WebRoot/src/App.tsx</code>.
                </Body1>
                <Body1>
                    Everything is a Fluent token, so the sun/moon button in the caption re-themes the whole app.
                    To call .NET, add a host object in <code>MainWindow.cs</code> - see docs/BRIDGE.md.
                </Body1>
            </main>
        </AOTrinoProvider>
    );
}
