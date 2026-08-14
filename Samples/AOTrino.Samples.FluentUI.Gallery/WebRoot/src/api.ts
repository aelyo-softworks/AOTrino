import { host } from "@aotrino/client";

// the JS-visible surface of GalleryApi.cs. names cross the bridge case-insensitively, so camelCase here
// matches the PascalCase members in C#.
export interface GalleryApi {
    framework: string;
    aotrinoVersion: string;
    webView2Version: string;
    uptime: string;
    workingSet: string;

    rowCount: number;

    ping(): string;
    add(a: number, b: number): number;
    getEnvironmentVariable(name: string): string | null;
    getUserName(): string;
    getPrimes(count: number): number[];
    getProcessInfo(): string;

    // one page of the virtual table, as JSON (see GalleryRowPage)
    getRowsAsync(offset: number, count: number): Promise<string>;

    echoAsync(text: string): Promise<string>;
    countdownAsync(seconds: number): Promise<number>;
    fail(): string;

    openExternal(url: string): boolean;
    collectGarbage(): string;
    setBackdrop(type: "mica" | "acrylic" | "tabbed" | "none"): boolean;
    quit(): void;

    // the Packages page: the .NET/NuGet side as JSON (see PackageInfo), and a registry lookup for the latest version
    getPackages(): string;
    getLatestVersionAsync(ecosystem: string, id: string): Promise<string>;
}

// PackageInfo.cs on the .NET side, and the shape Vite bakes into __NPM_PACKAGES__ for the npm side.
// System.Text.Json's source generator keeps PascalCase, so the npm half matches it deliberately.
export interface Package {
    Ecosystem: string;   // "dotnet" | "nuget" | "npm"
    Name: string;
    Current: string;
    Declared: string | null;
    RegistryId: string | null;
}

// the frontend packages, baked into the bundle at build time by the npmPackages() plugin in vite.config.ts.
// the runtime has no package.json, so this is the only way the page knows what it was built against.
declare const __NPM_PACKAGES__: Package[];
export const npmPackages: Package[] = typeof __NPM_PACKAGES__ !== "undefined" ? __NPM_PACKAGES__ : [];

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

// GalleryRow.cs / GalleryRowPage.cs
export interface Row {
    Index: number;
    Name: string;
    Kind: string;
    Size: number;
    Modified: string;
}

export interface RowPage {
    Offset: number;
    Total: number;
    Rows: Row[];
}

export const api = host<GalleryApi>("gallery");
export const system = host<SystemApi>("system");
