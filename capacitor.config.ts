import type { CapacitorConfig } from "@capacitor/cli";

/**
 * The store apps. The game ships inside them as a static bundle (see
 * vite.mobile.config.ts), so it starts with no connection and is a real app
 * rather than a window onto the website.
 */
const config: CapacitorConfig = {
  appId: "com.lotusdugun.sisyphus",
  appName: "Sisyphus",
  webDir: "dist/client",
  backgroundColor: "#1d2340",
  android: {
    backgroundColor: "#1d2340",
  },
  ios: {
    backgroundColor: "#1d2340",
    contentInset: "never",
    // the game draws edge to edge; no rubber-band scrolling of the page
    scrollEnabled: false,
  },
};

export default config;
