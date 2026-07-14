import { host } from "@aotrino/client";

// the JS-visible surface of DashboardApi.cs. member names cross the bridge case-insensitively, so
// camelCase here matches the PascalCase members in C#.
export interface DashboardApi {
    // the machine and the versions: fixed, read once
    machineName: string;
    userName: string;
    operatingSystem: string;
    architecture: string;
    processorCount: number;
    framework: string;
    aotrinoVersion: string;
    webView2Version: string;
    processId: number;

    // the process: these move between reads
    uptime: string;
    workingSet: string;
    managedHeap: string;
    collections: number;
    threadCount: number;

    analyzeAsync(text: string): Promise<string>;
    collectGarbage(): string;
    fail(): string;
    quit(): void;
}

export const api = host<DashboardApi>();
