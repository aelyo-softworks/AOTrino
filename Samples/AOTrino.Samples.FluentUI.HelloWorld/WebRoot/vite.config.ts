import path from "path";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
    // the WebRoot is served from the window's own virtual host (see MainWindow.VirtualHostName), and
    // relative URLs work there as well as they do from disk
    base: "./",
    plugins: [react()],
    resolve: {
        alias: {
            "@": path.resolve(__dirname, "./src"),
        },
    },
    build: {
        // WebRoot\dist is what Directory.Build.targets embeds into the executable
        outDir: "dist",
        // Fluent is a component kit and weighs ~570 kB here. Vite's 500 kB default is a warning about
        // download time on the web; this bundle ships inside the .exe and is served from the app's own
        // host, so it never crosses a network.
        chunkSizeWarningLimit: 1000,
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
