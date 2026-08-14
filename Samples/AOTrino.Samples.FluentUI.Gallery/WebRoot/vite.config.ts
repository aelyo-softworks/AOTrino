import fs from "fs";
import path from "path";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import type { Plugin } from "vite";

// bakes the frontend package list into the bundle: the declared range from this WebRoot's package.json, and the version
// actually resolved from the workspace lockfile. the Packages page reads it as __NPM_PACKAGES__, the runtime never has
// package.json, only what the build put in the bundle. the @aotrino/* workspace refs are skipped: they aren't published.
function npmPackages(): Plugin {
    const here = import.meta.dirname;
    const workspace = "Samples/AOTrino.Samples.FluentUI.Gallery/WebRoot";
    return {
        name: "aotrino-npm-packages",
        config() {
            const pkg = JSON.parse(fs.readFileSync(path.join(here, "package.json"), "utf8"));
            const ranges: Record<string, string> = { ...pkg.dependencies, ...pkg.devDependencies };

            // resolved versions come from the workspace root lockfile: hoisted deps live under node_modules/<name>,
            // a version pinned to this workspace lives under <workspace>/node_modules/<name>.
            let lock: { packages?: Record<string, { version?: string }> } = {};
            try { lock = JSON.parse(fs.readFileSync(path.resolve(here, "../../../package-lock.json"), "utf8")); }
            catch { /* no lockfile: resolved versions stay empty, the page just shows the declared range */ }
            const resolved = (name: string) =>
                lock.packages?.[`${workspace}/node_modules/${name}`]?.version ??
                lock.packages?.[`node_modules/${name}`]?.version ?? "";

            const list = Object.keys(ranges)
                .filter(name => !name.startsWith("@aotrino/") && !/^(\*|file:|workspace:)/.test(ranges[name]))
                .sort((a, b) => a.localeCompare(b))
                .map(name => ({
                    Ecosystem: "npm",
                    Name: name,
                    Current: resolved(name),
                    Declared: ranges[name],
                    RegistryId: name,
                }));

            return { define: { __NPM_PACKAGES__: JSON.stringify(list) } };
        },
    };
}

export default defineConfig({
    // the WebRoot is served from the window's own virtual host (see MainWindow.VirtualHostName), and
    // relative URLs work there as well as they do from disk
    base: "./",
    plugins: [react(), npmPackages()],
    resolve: {
        alias: {
            "@": path.resolve(import.meta.dirname, "./src"),
        },
    },
    build: {
        // WebRoot\dist is what Directory.Build.targets embeds into the executable
        outDir: "dist",
        // the gallery shows most of Fluent, so it imports most of Fluent: ~1 MB, ~280 kB gzipped. Vite's
        // 500 kB default warns about download time on the web; this bundle ships inside the .exe and is
        // served from the app's own virtual host, so it never crosses a network. a real app imports a
        // fraction of it - FluentUI.HelloWorld lands at ~570 kB with the same library available.
        chunkSizeWarningLimit: 1500,
        rollupOptions: {
            output: {
                // stable, hash-free names: the embedded resource list stays diffable across builds
                entryFileNames: "assets/[name].js",
                chunkFileNames: "assets/[name].js",
                assetFileNames: "assets/[name].[ext]",
            },
        },
    },
});
