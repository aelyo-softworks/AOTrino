import { useEffect, useState } from "react";
import { Body1, Button, Input, MessageBar, MessageBarBody, Spinner, Text, makeStyles, tokens } from "@fluentui/react-components";
import { useHostCall, useHostProperties, useHostValue } from "@aotrino/react";
import { Example } from "../Example";
import { Page } from "./Page";
import type { ProcessInfo } from "../api";
import { api } from "../api";

const useStyles = makeStyles({
    facts: {
        display: "grid",
        gridTemplateColumns: "max-content 1fr",
        columnGap: tokens.spacingHorizontalXL,
        rowGap: tokens.spacingVerticalXS,
        width: "100%",
    },
    label: {
        color: tokens.colorNeutralForeground3,
    },
    mono: {
        fontFamily: tokens.fontFamilyMonospace,
    },
});

export function BridgePage() {
    return (
        <Page
            title="Bridge"
            lead={
                <>
                    Public members of an object you register, callable from JavaScript. That's the whole model —
                    the bridge is deny-all by default because it's empty until you fill it.
                </>
            }
        >
            <Properties />
            <SyncMethods />
            <Arrays />
            <Complex />
            <Async />
            <Errors />
            <Push />
        </Page>
    );
}

function Properties() {
    const styles = useStyles();
    const { values, loading, refresh } = useHostProperties(api, ["uptime", "workingSet"]);

    return (
        <Example
            title="Properties"
            description="A property read crosses the bridge too, so it's asynchronous like everything else. useHostProperties reads a set of them in one round of calls rather than one each."
            code={`// C#
public string Uptime => /* ... */;

// TS
const { values, refresh } = useHostProperties(api, ["uptime", "workingSet"]);
<Text>{values.uptime}</Text>`}
            source="npm/react/src/useHostProperties.ts"
        >
            <div className={styles.facts}>
                <Body1 className={styles.label}>uptime</Body1>
                <Body1 className={styles.mono}>{values.uptime ?? "—"}</Body1>
                <Body1 className={styles.label}>workingSet</Body1>
                <Body1 className={styles.mono}>{values.workingSet ?? "—"}</Body1>
            </div>
            <Button onClick={refresh} disabled={loading}>refresh()</Button>
        </Example>
    );
}

function SyncMethods() {
    const ping = useHostCall(() => api.ping());
    const add = useHostCall((a: number, b: number) => api.add(a, b));
    const user = useHostValue(() => api.getEnvironmentVariable("USERNAME"), []);

    return (
        <Example
            title="Methods and arguments"
            description="Arguments convert on the way in and the result on the way out, both checked against the C# signature at compile time by the interface in api.ts."
            code={`// C#
public string Ping() => "pong from .NET";
public int Add(int a, int b) => a + b;
public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

// TS - api.add(2, "forty") wouldn't compile
await api.add(2, 40);`}
            source="src/api.ts"
        >
            <Button onClick={() => void ping.call()}>ping()</Button>
            <Text>{ping.result ?? "—"}</Text>
            <Button onClick={() => void add.call(2, 40)}>add(2, 40)</Button>
            <Text>{add.result ?? "—"}</Text>
            <Text>USERNAME = {user.value ?? "—"}</Text>
        </Example>
    );
}

function Arrays() {
    const primes = useHostCall((n: number) => api.getPrimes(n));

    return (
        <Example
            title="Arrays"
            description={
                <>
                    A flat array crosses as a real JS array — not a proxy, so reading an element is a property
                    access, not a call. A <em>nested</em> array doesn't: the shape arrives and the values come
                    back null. That's a WebView2 bug, and the reason anything structured goes as JSON.
                </>
            }
            code={`// C#
public int[] GetPrimes(int count) => /* ... */;

// TS
const primes = await api.getPrimes(12);   // a real number[]`}
            source="docs/BRIDGE.md"
        >
            <Button onClick={() => void primes.call(12)}>getPrimes(12)</Button>
            <Text>{primes.result ? `[${primes.result.join(", ")}]` : "—"}</Text>
        </Example>
    );
}

function Complex() {
    const styles = useStyles();
    const info = useHostValue(async () => JSON.parse(await api.getProcessInfo()) as ProcessInfo, []);

    return (
        <Example
            title="Complex types: JSON"
            description="Serialized with System.Text.Json's source generator, because reflection-based serialization doesn't survive AOT. Note the payload keeps PascalCase unless you set a naming policy."
            code={`// C#
public string GetProcessInfo() =>
    JsonSerializer.Serialize(info, GalleryJsonContext.Default.GalleryProcessInfo);

// TS
const info: ProcessInfo = JSON.parse(await api.getProcessInfo());`}
            source="Samples/…/GalleryApi.cs"
        >
            <div className={styles.facts}>
                <Body1 className={styles.label}>ProcessId</Body1>
                <Body1 className={styles.mono}>{info.value?.ProcessId ?? "—"}</Body1>
                <Body1 className={styles.label}>Architecture</Body1>
                <Body1 className={styles.mono}>{info.value?.Architecture ?? "—"}</Body1>
                <Body1 className={styles.label}>ManagedHeap</Body1>
                <Body1 className={styles.mono}>
                    {info.value ? `${(info.value.ManagedHeap / (1024 * 1024)).toFixed(1)} MB` : "—"}
                </Body1>
            </div>
            <Button onClick={info.refresh}>re-read</Button>
        </Example>
    );
}

function Async() {
    const [text, setText] = useState("hello from the gallery");
    const echo = useHostCall((value: string) => api.echoAsync(value));

    return (
        <Example
            title="async → Promise"
            description="An async host method is a real Promise on this side. useHostCall owns the pending flag, which is what the spinner is reading."
            code={`// C#
public async Task<string> EchoAsync(string text)
{
    await Task.Delay(600);
    return $".NET echoes: {text}";
}

// TS
const echo = useHostCall((v: string) => api.echoAsync(v));
<Button disabled={echo.pending} onClick={() => void echo.call(text)}>`}
            source="npm/react/src/useHostCall.ts"
        >
            <Input value={text} onChange={(_, d) => setText(d.value)} />
            <Button appearance="primary" disabled={echo.pending} onClick={() => void echo.call(text)}>
                echoAsync()
            </Button>
            {echo.pending && <Spinner size="tiny" />}
            <Text>{echo.result ?? "—"}</Text>
        </Example>
    );
}

function Errors() {
    const fail = useHostCall(() => api.fail());
    const firstLine = fail.error?.message.split("\n", 1)[0];

    return (
        <Example
            title="Exceptions"
            description={
                <>
                    A host method that throws is control flow, not a crash: it arrives as a rejected promise.
                    What you get is the <em>whole</em> .NET exception — message, inner exception, stack, build
                    paths — which is right for a log and wrong for a UI, so show the first line.
                </>
            }
            code={`// C#
public string Fail() => throw new InvalidOperationException("this .NET method always throws");

// TS - the hook catches it into error rather than letting it escape as an unhandled rejection
const fail = useHostCall(() => api.fail());
fail.error?.message.split("\\n", 1)[0]`}
            source="docs/BRIDGE.md"
        >
            <Button onClick={() => void fail.call()}>fail()</Button>
            {fail.error ? (
                <MessageBar intent="error">
                    <MessageBarBody title={fail.error.message}>{firstLine}</MessageBarBody>
                </MessageBar>
            ) : (
                <Text>—</Text>
            )}
        </Example>
    );
}

function Push() {
    const [tick, setTick] = useState<number | null>(null);
    const countdown = useHostCall((seconds: number) => api.countdownAsync(seconds));

    // .NET calls window.galleryTick(n) from inside the host method, once a second
    useEffect(() => {
        window.galleryTick = (n: number) => setTick(n);
        return () => { delete window.galleryTick; };
    }, []);

    return (
        <Example
            title=".NET calling JavaScript"
            description="The other direction: ExecuteScript from inside a host method. The awaits resume on the window's UI thread, so it's safe to call from there."
            code={`// C#
public async Task<int> CountdownAsync(int seconds)
{
    for (var i = seconds; i > 0; i--)
    {
        window.ExecuteScript($"window.galleryTick && window.galleryTick({i});");
        await Task.Delay(1000);
    }
    return seconds;
}

// TS
window.galleryTick = (n) => setTick(n);`}
            source="Samples/…/GalleryApi.cs"
        >
            <Button disabled={countdown.pending} onClick={() => void countdown.call(5)}>
                countdownAsync(5)
            </Button>
            <Text>{tick === null ? "—" : tick === 0 ? "liftoff" : `.NET says ${tick}`}</Text>
        </Example>
    );
}

declare global {
    interface Window {
        galleryTick?: (n: number) => void;
    }
}
