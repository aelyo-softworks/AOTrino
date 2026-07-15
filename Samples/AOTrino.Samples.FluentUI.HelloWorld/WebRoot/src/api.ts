import { host } from "@aotrino/client";

// the JS-visible surface of FluentApi.cs. member names cross the bridge case-insensitively, so camelCase
// here matches the PascalCase members in C#.
export interface FluentApi {
    // one call the page can ask anything of: no value about the user or the machine is baked into the bundle
    getEnvironmentVariable(name: string): string | null;
    framework: string;
    aotrinoVersion: string;
    webView2Version: string;
    greetAsync(name: string): Promise<string>;
    quit(): void;
}

// the environment variables this app reads. Windows sets both; they're named here so the page says what it
// wants rather than C# deciding for it.
export const userNameVariable = "USERNAME";
export const machineNameVariable = "COMPUTERNAME";

export const api = host<FluentApi>();
