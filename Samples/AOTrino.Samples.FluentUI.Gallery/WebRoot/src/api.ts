import { host } from "@aotrino/client";

// the JS-visible surface of GalleryApi.cs. names cross the bridge case-insensitively, so camelCase here
// matches the PascalCase members in C#.
export interface GalleryApi {
    framework: string;
    aotrinoVersion: string;
    webView2Version: string;
    uptime: string;
    workingSet: string;

    ping(): string;
    add(a: number, b: number): number;
    getEnvironmentVariable(name: string): string | null;
    getUserName(): string;
    getPrimes(count: number): number[];
    getProcessInfo(): string;

    echoAsync(text: string): Promise<string>;
    countdownAsync(seconds: number): Promise<number>;
    fail(): string;

    openExternal(url: string): boolean;
    collectGarbage(): string;
    setBackdrop(type: "mica" | "acrylic" | "tabbed" | "none"): boolean;
    quit(): void;
}

// AOTrino ships SystemInfo; this window chose to register it (MainWindow.RegisterHostObjects)
export interface SystemApi {
    getInfo(): string;
}

// what GalleryApi.GetProcessInfo() serializes. System.Text.Json's source generator keeps PascalCase,
// so the payload does too.
export interface ProcessInfo {
    ProcessId: number;
    ProcessorCount: number;
    Architecture: string;
    WorkingSet: number;
    ManagedHeap: number;
    Collections: number;
}

export const api = host<GalleryApi>("gallery");
export const system = host<SystemApi>("system");
