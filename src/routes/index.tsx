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
        content:
          "Minimal, felsefi bir sonsuz döngü oyunu. Masaüstü ve mobil tarayıcıda oynanır.",
      },
      { property: "og:type", content: "website" },
      { name: "twitter:card", content: "summary_large_image" },
    ],
  }),
  component: Index,
});

function Index() {
  return <SisyphusGame />;
}
