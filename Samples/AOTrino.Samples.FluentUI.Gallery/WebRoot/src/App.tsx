import { useState } from "react";
import { Tab, TabList, makeStyles, tokens } from "@fluentui/react-components";
import type { SelectTabData } from "@fluentui/react-components";
import {
    Bug24Regular,
    Grid24Regular,
    Home24Regular,
    Info24Regular,
    PaintBrush24Regular,
    ShieldKeyhole24Regular,
    Window24Regular,
} from "@fluentui/react-icons";
import { AOTrinoProvider, TitleBar } from "@aotrino/fluent";
import { api } from "./api";
import { HomePage } from "./pages/HomePage";
import { ControlsPage } from "./pages/ControlsPage";
import { WindowPage } from "./pages/WindowPage";
import { BridgePage } from "./pages/BridgePage";
import { ThemingPage } from "./pages/ThemingPage";
import { SystemPage } from "./pages/SystemPage";
import { SecurityPage } from "./pages/SecurityPage";

const pages = [
    { value: "home", label: "Home", icon: <Home24Regular />, render: () => <HomePage /> },
    { value: "controls", label: "Controls", icon: <Grid24Regular />, render: () => <ControlsPage /> },
    { value: "window", label: "Window", icon: <Window24Regular />, render: () => <WindowPage /> },
    { value: "bridge", label: "Bridge", icon: <Bug24Regular />, render: () => <BridgePage /> },
    { value: "theming", label: "Theming", icon: <PaintBrush24Regular />, render: () => <ThemingPage /> },
    { value: "system", label: "System", icon: <Info24Regular />, render: () => <SystemPage /> },
    { value: "security", label: "Security", icon: <ShieldKeyhole24Regular />, render: () => <SecurityPage /> },
];

const useStyles = makeStyles({
    body: {
        display: "flex",
        flexGrow: 1,
        minHeight: 0,
    },
    nav: {
        flexShrink: 0,
        paddingTop: tokens.spacingVerticalM,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        borderRightWidth: "1px",
        borderRightStyle: "solid",
        borderRightColor: tokens.colorNeutralStroke2,
        backgroundColor: tokens.colorNeutralBackground2,
    },
    content: {
        flexGrow: 1,
        overflowY: "auto",
        paddingTop: tokens.spacingVerticalXXL,
        paddingBottom: tokens.spacingVerticalXXL,
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
    },
});

export function App() {
    return (
        <AOTrinoProvider>
            {/* the caption is @aotrino/fluent's: drag, double-click to maximize, the theme picker and the
                window buttons all come with it. no title prop: it defaults to the window's own caption */}
            <TitleBar title="AOTrino Fluent UI Gallery" onClose={() => void api.quit()} />
            <Shell />
        </AOTrinoProvider>
    );
}

function Shell() {
    const styles = useStyles();
    const [page, setPage] = useState("home");
    const current = pages.find(p => p.value === page) ?? pages[0];

    return (
        <div className={styles.body}>
            <TabList
                className={styles.nav}
                vertical
                size="large"
                selectedValue={page}
                onTabSelect={(_, data: SelectTabData) => setPage(data.value as string)}
            >
                {pages.map(p => (
                    <Tab key={p.value} value={p.value} icon={p.icon}>
                        {p.label}
                    </Tab>
                ))}
            </TabList>
            <main className={styles.content}>{current.render()}</main>
        </div>
    );
}
