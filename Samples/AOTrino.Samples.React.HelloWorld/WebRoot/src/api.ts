import { host } from "@aotrino/client";

// the JS-visible surface of DemoApi.cs. 
// member names cross the WebView2 bridge case -insensitively, so camelCase here matches the PascalCase members in C#.
// this interface is the whole point of @aotrino/client: without it, chrome.webview.hostObjects.dotnet is untyped and nothing checks these calls against the .NET side.
export interface DemoApi {
    machineName: string;
    framework: string;
    aotrinoVersion: string;
    webView2Version: string;
    ping(): string;
    add(a: number, b: number): number;
    echoAsync(text: string): Promise<string>;
    quit(): void;
}

// "dotnet" is the name MainWindow.RegisterHostObjects registered, and the client's default.
// safe at module scope: outside AOTrino this is a proxy that only throws if something calls it.
export const api = host<DemoApi>();
