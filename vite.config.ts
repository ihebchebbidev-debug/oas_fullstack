import { defineConfig } from "vite";
import react from "@vitejs/plugin-react-swc";
import { componentTagger } from "lovable-tagger";
import path from "path";

export default defineConfig(({ mode }) => ({
  // Absolute base for web/PWA so deep links like /mobile/login resolve assets
  // correctly; relative base only for the Electron desktop build (file://).
  base: mode === "electron" ? "./" : "/",
  server: {
    host: "::",
    port: 8080,
    // No /api/oas proxy: the OAS frontend calls https://api.flowentra.app
    // directly (src/oas/api/client.ts), cross-origin, in every environment.
  },
  plugins: [react(), mode === "development" && componentTagger()].filter(Boolean),
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
    dedupe: ["react", "react-dom", "react/jsx-runtime"],
  },
  build: {
    target: "es2022",
    cssTarget: "chrome80",
    chunkSizeWarningLimit: 1200,
    minify: "esbuild",
    sourcemap: false,
    cssCodeSplit: true,
    assetsInlineLimit: 4096,
  },
  optimizeDeps: {
    esbuildOptions: { target: "es2022" },
    include: ["react", "react-dom", "lucide-react", "@ionic/react"],
  },
}));
