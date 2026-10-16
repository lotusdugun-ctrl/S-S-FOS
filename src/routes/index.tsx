import { createFileRoute } from "@tanstack/react-router";
import { SisyphusGame } from "@/components/SisyphusGame";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "Sisyphus — Kayayı Zirveye Taşı" },
      {
        name: "description",
        content:
          "Sisyphus efsanesinden esinlenen minimal ve atmosferik bir tarayıcı oyunu. Kayayı tepeye it, zirveye ulaş ve döngüyü yeniden başlat.",
      },
      { property: "og:title", content: "Sisyphus — Kayayı Zirveye Taşı" },
      {
        property: "og:description",
        content: "Minimal, felsefi bir sonsuz döngü oyunu. Masaüstü ve mobil tarayıcıda oynanır.",
      },
      { property: "og:type", content: "website" },
      { name: "twitter:card", content: "summary_large_image" },
      { property: "og:image", content: "/icon-512.png" },
      // installable as an app: Android reads the manifest, iOS reads these
      { name: "theme-color", content: "#1d2340" },
      { name: "mobile-web-app-capable", content: "yes" },
      { name: "apple-mobile-web-app-capable", content: "yes" },
      { name: "apple-mobile-web-app-status-bar-style", content: "black-translucent" },
      { name: "apple-mobile-web-app-title", content: "Sisyphus" },
      { name: "application-name", content: "Sisyphus" },
    ],
    links: [
      { rel: "manifest", href: "/manifest.webmanifest" },
      { rel: "apple-touch-icon", href: "/apple-touch-icon.png" },
      { rel: "icon", type: "image/svg+xml", href: "/icon.svg" },
    ],
  }),
  component: Index,
});

function Index() {
  return <SisyphusGame />;
}
