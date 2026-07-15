import type { ReactNode } from "react";
import { useState } from "react";
import { Body1, Button, Card, Subtitle2, Tooltip, makeStyles, mergeClasses, tokens } from "@fluentui/react-components";
import { Checkmark16Regular, ChevronDown16Regular, ChevronRight16Regular, Copy16Regular } from "@fluentui/react-icons";

const useStyles = makeStyles({
    card: {
        display: "flex",
        flexDirection: "column",
        rowGap: tokens.spacingVerticalS,
        padding: tokens.spacingVerticalL,
    },
    header: {
        display: "flex",
        alignItems: "baseline",
        justifyContent: "space-between",
        columnGap: tokens.spacingHorizontalM,
    },
    description: {
        color: tokens.colorNeutralForeground3,
    },
    demo: {
        display: "flex",
        flexWrap: "wrap",
        alignItems: "center",
        columnGap: tokens.spacingHorizontalM,
        rowGap: tokens.spacingVerticalS,
        padding: tokens.spacingVerticalM,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground3,
    },
    codeBar: {
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
    },
    code: {
        margin: 0,
        padding: tokens.spacingVerticalM,
        overflowX: "auto",
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground3,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase300,
    },
    hidden: {
        display: "none",
    },
});

export interface ExampleProps {
    title: string;
    description?: ReactNode;
    // the live thing
    children?: ReactNode;
    // ...and what produced it. shown on demand, because the demo is the point and the code is the evidence.
    code?: string;
    // where the code lives, when reading the whole file beats reading a snippet
    source?: string;
}

// the gallery's unit: a live demo, and the code behind it, in one card.
export function Example({ title, description, children, code, source }: ExampleProps) {
    const styles = useStyles();
    const [open, setOpen] = useState(false);
    const [copied, setCopied] = useState(false);

    async function copy() {
        if (!code)
            return;

        await navigator.clipboard.writeText(code);
        setCopied(true);
        setTimeout(() => setCopied(false), 1500);
    }

    return (
        <Card className={styles.card}>
            <div className={styles.header}>
                <Subtitle2>{title}</Subtitle2>
                {source && <Body1 className={styles.description}><code>{source}</code></Body1>}
            </div>
            {description && <Body1 className={styles.description}>{description}</Body1>}

            <div className={styles.demo}>{children}</div>

            {code && (
                <>
                    <div className={styles.codeBar}>
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={open ? <ChevronDown16Regular /> : <ChevronRight16Regular />}
                            onClick={() => setOpen(o => !o)}
                        >
                            {open ? "Hide code" : "Show code"}
                        </Button>
                        {open && (
                            <Tooltip content={copied ? "Copied" : "Copy"} relationship="label">
                                <Button
                                    appearance="subtle"
                                    size="small"
                                    icon={copied ? <Checkmark16Regular /> : <Copy16Regular />}
                                    onClick={() => void copy()}
                                />
                            </Tooltip>
                        )}
                    </div>
                    <pre className={mergeClasses(styles.code, !open && styles.hidden)}>{code}</pre>
                </>
            )}
        </Card>
    );
}
