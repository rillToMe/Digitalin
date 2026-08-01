import { defineConfig } from "vite";
import path from "path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { visualizer } from "rollup-plugin-visualizer";

function figmaAssetResolver() {
  return {
    name: "figma-asset-resolver",
    resolveId(id: string) {
      if (id.startsWith("figma:asset/")) {
        const filename = id.replace("figma:asset/", "");
        return path.resolve(__dirname, "src/assets", filename);
      }
    },
  };
}

export default defineConfig({
  plugins: [
    figmaAssetResolver(),

    react(),

    tailwindcss(),

    visualizer({
      filename: "stats.html",
      open: true,
      gzipSize: true,
      brotliSize: true,
    }),
  ],

  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },

  assetsInclude: ["**/*.svg", "**/*.csv"],

  server: {
    host: true,
    port: 5173,
    strictPort: true,
  },

  build: {
    target: "es2022",
    sourcemap: false,
    cssCodeSplit: true,
    reportCompressedSize: true,
    chunkSizeWarningLimit: 1000,

    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes("node_modules")) return;

          if (id.includes("react-router")) {
            return "router";
          }

          if (id.includes("@tanstack")) {
            return "query";
          }

          if (id.includes("lucide-react")) {
            return "icons";
          }

          if (id.includes("axios")) {
            return "network";
          }

          if (id.includes("zustand")) {
            return "state";
          }

          if (
            id.includes("@react-oauth") ||
            id.includes("google")
          ) {
            return "google";
          }

          return "vendor";
        },
      },
    },
    
  },
});