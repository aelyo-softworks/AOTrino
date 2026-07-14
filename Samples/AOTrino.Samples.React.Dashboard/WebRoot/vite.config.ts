import path from "path";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
    // AOTrino extracts the embedded WebRoot to disk and navigates to it through file://,
    // so every asset URL the build emits has to be relative
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
