import { createFileRoute, Link } from "@tanstack/react-router";

/**
 * The privacy policy the app stores ask for. Short because there is little to
 * say: the game collects nothing. Written in Turkish and English; the store
 * listings link here (https://s-s-fos.vercel.app/privacy).
 */
export const Route = createFileRoute("/privacy")({
  head: () => ({
    meta: [
      { title: "Sisyphus — Gizlilik Politikası / Privacy Policy" },
      { name: "description", content: "Sisyphus collects no personal data." },
      { name: "robots", content: "index" },
    ],
  }),
  component: Privacy,
});

const UPDATED = "18.10.2026";

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="mt-6">
      <h3 className="text-sm font-semibold tracking-wide text-foreground">{title}</h3>
      <div className="mt-1.5 space-y-2 text-sm leading-relaxed text-muted-foreground">
        {children}
      </div>
    </section>
  );
}

function Privacy() {
  return (
    <main className="min-h-dvh w-full overflow-y-auto bg-background px-6 py-10 text-foreground select-text">
      <div className="mx-auto max-w-2xl">
        <Link
          to="/"
          className="text-[0.7rem] tracking-[0.3em] text-muted-foreground uppercase hover:text-foreground"
        >
          ← Sisyphus
        </Link>

        <h1 className="mt-6 text-2xl font-light tracking-[0.15em] uppercase">
          Gizlilik Politikası
        </h1>
        <p className="mt-1 text-xs text-muted-foreground">Son güncelleme: {UPDATED}</p>

        <Section title="Kısaca">
          <p>
            Sisyphus hiçbir kişisel veri toplamaz, saklamaz ya da paylaşmaz. Hesap, reklam, analiz
            aracı veya takip yoktur.
          </p>
        </Section>
        <Section title="Cihazınızda tutulanlar">
          <p>
            Oyun ilerlemeniz (döngü sayısı, en iyi süre, Bilgelik Defteri'nde bulduğunuz sayfalar)
            ve ayarlarınız (dil, ses, titreşim) yalnızca kendi cihazınızda, oyunun yerel deposunda
            tutulur. Bu bilgiler hiçbir sunucuya gönderilmez ve uygulamayı sildiğinizde ya da
            tarayıcı verilerini temizlediğinizde silinir.
          </p>
        </Section>
        <Section title="İnternet bağlantısı">
          <p>
            Mağaza uygulaması tüm dosyalarını içinde taşır ve internet olmadan çalışır. Web
            sürümünde sayfa, barındırma sağlayıcımızın (Vercel) sunucularından yüklenir; bu
            sağlayıcı, her web sitesinde olduğu gibi, teknik sunucu kayıtları tutabilir. Biz bu
            kayıtları kişileri tanımak için kullanmayız.
          </p>
        </Section>
        <Section title="İzinler">
          <p>
            Uygulama yalnızca titreşim (dokunsal geri bildirim) kullanır; bunu oyun içindeki ayardan
            kapatabilirsiniz. Kamera, mikrofon, konum, rehber ya da dosyalarınıza erişmez.
          </p>
        </Section>
        <Section title="Çocuklar">
          <p>Oyun her yaşa uygundur ve hiçbir kullanıcıdan, çocuklar dahil, veri toplamaz.</p>
        </Section>
        <Section title="İletişim ve değişiklikler">
          <p>
            Sorularınız için uygulama mağazasındaki geliştirici iletişim adresinden bize
            ulaşabilirsiniz. Bu politika değişirse güncel hali bu sayfada yayımlanır.
          </p>
        </Section>

        <hr className="my-10 border-border" />

        <h2 className="text-2xl font-light tracking-[0.15em] uppercase">Privacy Policy</h2>
        <p className="mt-1 text-xs text-muted-foreground">Last updated: {UPDATED}</p>

        <Section title="In short">
          <p>
            Sisyphus does not collect, store or share any personal data. There are no accounts, ads,
            analytics or tracking.
          </p>
        </Section>
        <Section title="What stays on your device">
          <p>
            Your progress (cycles, best time, the pages you have found in the Book of Wisdom) and
            your settings (language, sound, vibration) are kept only on your own device, in the
            game's local storage. They are never sent to any server, and they are deleted when you
            uninstall the app or clear your browser data.
          </p>
        </Section>
        <Section title="Internet connection">
          <p>
            The store app carries all of its files and works offline. The web version is loaded from
            our hosting provider's (Vercel) servers, which, like any website host, may keep
            technical server logs. We do not use them to identify anyone.
          </p>
        </Section>
        <Section title="Permissions">
          <p>
            The app only uses vibration (haptic feedback), which can be switched off in the game. It
            does not access your camera, microphone, location, contacts or files.
          </p>
        </Section>
        <Section title="Children">
          <p>
            The game is suitable for all ages and collects no data from anyone, children included.
          </p>
        </Section>
        <Section title="Contact and changes">
          <p>
            For questions, reach us at the developer contact address on the app's store page. If
            this policy changes, the current version is published on this page.
          </p>
        </Section>
      </div>
    </main>
  );
}
