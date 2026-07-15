import type { ReactNode } from "react";
import { Body1, Title2, makeStyles, tokens } from "@fluentui/react-components";

const useStyles = makeStyles({
    // no max width: this is a resizable window, not a column of prose, so the cards follow it
    root: {
        display: "flex",
        flexDirection: "column",
        rowGap: tokens.spacingVerticalL,
    },
    // ...the lead paragraph is prose, though, and prose past ~70 characters is hard to read
    lead: {
        maxWidth: "68ch",
        color: tokens.colorNeutralForeground2,
    },
});

export interface PageProps {
    title: string;
    lead?: ReactNode;
    children?: ReactNode;
}

// every gallery page: a title, one paragraph saying what this is actually about, then Examples
export function Page({ title, lead, children }: PageProps) {
    const styles = useStyles();
    return (
        <div className={styles.root}>
            <Title2 as="h1">{title}</Title2>
            {lead && <Body1 className={styles.lead}>{lead}</Body1>}
            {children}
        </div>
    );
}
