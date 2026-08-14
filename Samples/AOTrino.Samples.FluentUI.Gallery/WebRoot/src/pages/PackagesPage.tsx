import { useState } from "react";
import {
    Badge,
    Body1,
    Button,
    Spinner,
    Table,
    TableBody,
    TableCell,
    TableCellLayout,
    TableHeader,
    TableHeaderCell,
    TableRow,
    makeStyles,
    tokens,
} from "@fluentui/react-components";
import { ArrowSync20Regular } from "@fluentui/react-icons";
import { useHostValue } from "@aotrino/react";
import { Example } from "../Example";
import { Page } from "./Page";
import { api, npmPackages, type Package } from "../api";

const useStyles = makeStyles({
    toolbar: {
        display: "flex",
        alignItems: "center",
        columnGap: tokens.spacingHorizontalM,
        marginBottom: tokens.spacingVerticalM,
    },
    version: {
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
    },
    dim: {
        color: tokens.colorNeutralForeground3,
    },
});

type Status = "checking" | "current" | "update" | "major" | "unknown";

// the first dotted number run in a version string. current can carry noise (".NET 10.0.6", "1.6.5+abc"), latest is clean.
function numeric(v: string): number[] {
    const m = v.match(/\d+(?:\.\d+)*/);
    return m ? m[0].split(".").map(n => parseInt(n, 10) || 0) : [];
}

function newer(current: string, latest: string): Status {
    if (!latest)
        return "unknown";

    const c = numeric(current);
    const l = numeric(latest);
    if (!c.length || !l.length)
        return "unknown";

    for (let i = 0; i < Math.max(c.length, l.length); i++) {
        const d = (l[i] || 0) - (c[i] || 0);
        if (d !== 0)
            return d < 0 ? "current" : (i === 0 ? "major" : "update");
    }

    return "current";
}

const badges: Record<Exclude<Status, "checking">, { color: "success" | "warning" | "danger" | "subtle"; text: string }> = {
    current: { color: "success", text: "up to date" },
    update: { color: "warning", text: "update" },
    major: { color: "danger", text: "major" },
    unknown: { color: "subtle", text: "—" },
};

function StatusCell({ status }: { status?: Status }) {
    if (!status)
        return null;

    if (status === "checking")
        return <Spinner size="extra-tiny" />;

    const b = badges[status];
    return <Badge appearance="filled" color={b.color}>{b.text}</Badge>;
}

export function PackagesPage() {
    const styles = useStyles();

    // the .NET/NuGet half comes from the host, the npm half was baked into the bundle by Vite. merge once loaded.
    const dotnet = useHostValue(async () => JSON.parse(await api.getPackages()) as Package[], []);
    const packages: Package[] = dotnet.value ? [...dotnet.value, ...npmPackages] : [];

    const [latest, setLatest] = useState<Record<string, string>>({});
    const [status, setStatus] = useState<Record<string, Status>>({});
    const [checking, setChecking] = useState(false);
    const key = (p: Package) => `${p.Ecosystem}:${p.Name}`;

    // the button: ask each package's registry for its latest version, in parallel. the host makes the calls.
    async function checkUpdates() {
        setChecking(true);
        setStatus(Object.fromEntries(packages.filter(p => p.RegistryId).map(p => [key(p), "checking" as Status])));
        setLatest({});

        const results = await Promise.all(
            packages
                .filter(p => p.RegistryId)
                .map(async p => {
                    const version = await api.getLatestVersionAsync(p.Ecosystem, p.RegistryId!);
                    return { k: key(p), version, status: newer(p.Current, version) };
                })
        );

        setLatest(Object.fromEntries(results.map(r => [r.k, r.version])));
        setStatus(Object.fromEntries(results.map(r => [r.k, r.status])));
        setChecking(false);
    }

    return (
        <Page
            title="Packages"
            lead={
                <>
                    What this app is actually built on, current versions read from the loaded .NET assemblies and the
                    baked-in npm build, next to what each project declared. A page can't read a `.csproj` or a
                    `package.json`, and it certainly can't reach nuget.org or the npm registry from a `Local` window,
                    so the .NET host does both and hands over the result.
                </>
            }
        >
            <Example
                title="Current vs declared, and what's newer"
                description={
                    <>
                        <strong>Current</strong> is the version running right now, the loaded assembly for NuGet, the
                        resolved lockfile version for npm. <strong>Declared</strong> is the range or version the
                        project asked for. <strong>Check for updates</strong> asks each registry for the latest, on
                        the .NET side, so the page never touches the network itself.
                    </>
                }
                code={`// the host reaches the registry, the page can't from a Local window
public async Task<string> GetLatestVersionAsync(string ecosystem, string id)
{
    using var http = new HttpClient();
    var index = await http.GetStringAsync(
        $"https://api.nuget.org/v3-flatcontainer/{id.ToLowerInvariant()}/index.json");
    // ...newest stable version
}`}
                source="GalleryApi.cs"
            >
                <div style={{ width: "100%" }}>
                    <div className={styles.toolbar}>
                        <Button
                            appearance="primary"
                            icon={checking ? <Spinner size="extra-tiny" /> : <ArrowSync20Regular />}
                            disabled={checking || dotnet.loading}
                            onClick={() => void checkUpdates()}
                        >
                            Check for updates
                        </Button>
                        <Body1 className={styles.dim}>
                            {packages.length} package{packages.length === 1 ? "" : "s"}
                        </Body1>
                    </div>

                    {dotnet.loading ? (
                        <Spinner size="tiny" label="reading" />
                    ) : (
                        <Table size="small" aria-label="packages">
                            <TableHeader>
                                <TableRow>
                                    <TableHeaderCell>Package</TableHeaderCell>
                                    <TableHeaderCell>From</TableHeaderCell>
                                    <TableHeaderCell>Current</TableHeaderCell>
                                    <TableHeaderCell>Declared</TableHeaderCell>
                                    <TableHeaderCell>Latest</TableHeaderCell>
                                    <TableHeaderCell>Status</TableHeaderCell>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {packages.map(p => (
                                    <TableRow key={key(p)}>
                                        <TableCell>
                                            <TableCellLayout>{p.Name}</TableCellLayout>
                                        </TableCell>
                                        <TableCell className={styles.dim}>{p.Ecosystem}</TableCell>
                                        <TableCell className={styles.version}>{p.Current}</TableCell>
                                        <TableCell className={styles.version}>{p.Declared ?? "—"}</TableCell>
                                        <TableCell className={styles.version}>{latest[key(p)] ?? ""}</TableCell>
                                        <TableCell>
                                            <StatusCell status={status[key(p)]} />
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    )}
                </div>
            </Example>
        </Page>
    );
}
