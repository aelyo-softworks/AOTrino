import { useEffect, useRef, useState } from "react";
import { Body1, Caption1, Spinner, makeStyles, mergeClasses, tokens } from "@fluentui/react-components";
import { api } from "./api";
import type { Row, RowPage } from "./api";

// a table of half a million rows that .NET never sends and JS never holds.
// the browser can't be handed 500,000 rows and it doesn't need to be: at any moment ~12 of them are on screen,
// so the page asks for those, by index, and forgets the rest. this is what a host object is *for* - not a ping,
// but a data source that stays in .NET.
// the windowing here is hand-rolled (a spacer to make the scrollbar the right length, a slice drawn at the
// right offset) rather than a library, so nothing about the paging is hidden behind someone else's abstraction.

const rowHeight = 32;
const viewportHeight = 384;

// one bridge call per page. small enough that a fling doesn't queue megabytes, big enough that scrolling a
// screen doesn't cost a round trip per row - the two failure modes this number sits between.
const pageSize = 200;

// draw a screen's worth beyond the viewport, so a slow page has somewhere to land before it's seen
const overscan = 8;

const columns = "72px 1fr 110px 110px 140px";

const useStyles = makeStyles({
    root: {
        display: "flex",
        flexDirection: "column",
        rowGap: tokens.spacingVerticalS,
        width: "100%",
        minWidth: 0,
    },
    grid: {
        display: "grid",
        gridTemplateColumns: columns,
        columnGap: tokens.spacingHorizontalM,
        alignItems: "center",
        paddingLeft: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingHorizontalM,
        height: `${rowHeight}px`,
        boxSizing: "border-box",
    },
    header: {
        backgroundColor: tokens.colorNeutralBackground2,
        fontWeight: tokens.fontWeightSemibold,
        borderTopLeftRadius: tokens.borderRadiusMedium,
        borderTopRightRadius: tokens.borderRadiusMedium,
        borderBottomWidth: "1px",
        borderBottomStyle: "solid",
        borderBottomColor: tokens.colorNeutralStroke2,
    },
    viewport: {
        height: `${viewportHeight}px`,
        overflowY: "auto",
        backgroundColor: tokens.colorNeutralBackground1,
        borderBottomLeftRadius: tokens.borderRadiusMedium,
        borderBottomRightRadius: tokens.borderRadiusMedium,
    },
    row: {
        borderBottomWidth: "1px",
        borderBottomStyle: "solid",
        borderBottomColor: tokens.colorNeutralStroke3,
        fontSize: tokens.fontSizeBase200,
    },
    numeric: {
        textAlign: "right",
        fontFamily: tokens.fontFamilyMonospace,
        color: tokens.colorNeutralForeground3,
    },
    name: {
        fontFamily: tokens.fontFamilyMonospace,
        overflow: "hidden",
        textOverflow: "ellipsis",
        whiteSpace: "nowrap",
    },
    muted: {
        color: tokens.colorNeutralForeground4,
    },
    stats: {
        display: "flex",
        alignItems: "center",
        columnGap: tokens.spacingHorizontalM,
        color: tokens.colorNeutralForeground3,
    },
});

export function VirtualTable() {
    const styles = useStyles();
    const [total, setTotal] = useState(0);
    const [scrollTop, setScrollTop] = useState(0);
    const [pages, setPages] = useState<ReadonlyMap<number, Row[]>>(new Map());
    const [requests, setRequests] = useState(0);
    const [failed, setFailed] = useState<string | null>(null);

    // pages already asked for. a ref, not state: a second request for a page in flight must be suppressed
    // *now*, during this render's effect, not after a re-render.
    const inflight = useRef(new Set<number>());

    useEffect(() => {
        void (async () => {
            try {
                setTotal(await api.rowCount);
            }
            catch (e) {
                setFailed(String(e));
            }
        })();
    }, []);

    const first = Math.max(0, Math.floor(scrollTop / rowHeight) - overscan);
    const last = Math.min(total - 1, Math.ceil((scrollTop + viewportHeight) / rowHeight) + overscan);

    useEffect(() => {
        if (total === 0)
            return;

        for (let page = Math.floor(first / pageSize); page <= Math.floor(last / pageSize); page++) {
            if (pages.has(page) || inflight.current.has(page))
                continue;

            inflight.current.add(page);
            void (async () => {
                try {
                    const json = await api.getRowsAsync(page * pageSize, pageSize);
                    const loaded = JSON.parse(json) as RowPage;

                    // key by the offset .NET answered with, not by the page we think we asked for: fling the
                    // scrollbar and replies land out of order
                    setPages(prev => new Map(prev).set(loaded.Offset / pageSize, loaded.Rows));
                    setRequests(n => n + 1);
                }
                catch (e) {
                    setFailed(String(e));
                }
                finally {
                    inflight.current.delete(page);
                }
            })();
        }
    }, [first, last, total, pages]);

    const visible: (Row | undefined)[] = [];
    for (let i = first; i <= last; i++)
        visible.push(pages.get(Math.floor(i / pageSize))?.[i % pageSize]);

    return (
        <div className={styles.root}>
            <div className={mergeClasses(styles.grid, styles.header)}>
                <div className={styles.numeric}>#</div>
                <div>Name</div>
                <div>Kind</div>
                <div className={styles.numeric}>Size</div>
                <div>Modified</div>
            </div>

            <div className={styles.viewport} onScroll={e => setScrollTop(e.currentTarget.scrollTop)}>
                {/* the scrollbar's whole job: be as long as 500,000 rows would be.
                    Chromium stops honouring a height past ~33.5 million pixels, which at 32px a row is about a
                    million of them - past that a spacer stops working and the scroll position has to be mapped
                    to the range by hand. */}
                <div style={{ height: `${total * rowHeight}px`, position: "relative" }}>
                    <div style={{ position: "absolute", top: 0, left: 0, right: 0, transform: `translateY(${first * rowHeight}px)` }}>
                        {visible.map((row, i) => (
                            <div key={first + i} className={mergeClasses(styles.grid, styles.row)}>
                                <div className={styles.numeric}>{first + i}</div>
                                <div className={mergeClasses(styles.name, !row && styles.muted)}>{row?.Name ?? "…"}</div>
                                <div className={styles.muted}>{row?.Kind ?? ""}</div>
                                <div className={styles.numeric}>{row ? row.Size.toLocaleString() : ""}</div>
                                <div className={styles.muted}>{row?.Modified ?? ""}</div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            <div className={styles.stats}>
                {total === 0 && <Spinner size="tiny" />}
                <Caption1>
                    {total.toLocaleString()} rows in .NET · {requests} bridge calls ·{" "}
                    {(requests * pageSize).toLocaleString()} rows ever sent · {pages.size} pages held by JS
                </Caption1>
            </div>
            {failed && <Body1>{failed}</Body1>}
        </div>
    );
}
