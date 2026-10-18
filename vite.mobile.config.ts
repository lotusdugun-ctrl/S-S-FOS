// Build for the native apps (Capacitor). The store apps carry the game inside
// them rather than loading the website, so this produces a plain static bundle:
// no server, the single page prerendered as an SPA shell into dist-mobile/.
//   bun run build:mobile
import { defineConfig } from "@lovable.dev/vite-tanstack-config";

export default defineConfig({
  nitro: false,
  tanstackStart: {
    server: { entry: "server" },
    spa: {
      enabled: true,
      prerender: { outputPath: "/index.html", crawlLinks: false },
    },
  },
  vite: {
    define: { "import.meta.env.VITE_NATIVE": JSON.stringify("1") },
    // the prerender step runs a preview server; bind it to IPv4 so it also
    // works on machines without IPv6
    preview: { host: "127.0.0.1" },
  },
});
