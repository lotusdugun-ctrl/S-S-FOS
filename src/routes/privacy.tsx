import { createFileRoute, Link } from "@tanstack/react-router";

/**
 * The privacy policy the app stores ask for. The game itself collects nothing;
 * the store app shows ads through Google AdMob, which is what most of this is
 * about. Written in Turkish and English; the store listings link here
 * (https://s-s-fos.vercel.app/privacy).
 */
export const Route = createFileRoute("/privacy")({
  head: () => ({
    meta: [
      { title: "Sisyphus — Gizlilik Politikası / Privacy Policy" },
      { name: "description", content: "What Sisyphus keeps, and the ads in the store app." },
      { name: "robots", content: "index" },
    ],
  }),
  component: Privacy,
});

const UPDATED = "29.10.2026";

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
            Sisyphus'un kendisi hiçbir kişisel veri toplamaz, saklamaz ya da paylaşmaz; hesap ve
            analiz aracı yoktur. Mağaza uygulaması, Google AdMob aracılığıyla reklam gösterir
            (aşağıda). Web sürümünde reklam yoktur.
          </p>
        </Section>
        <Section title="Reklamlar (mağaza uygulaması)">
          <p>
            Uygulama ana menünün altında küçük bir reklam şeridi gösterir; oyun sırasında reklam
            çıkmaz. Nefesiniz tükendiğinde isterseniz bir ödüllü reklam izleyerek nefesinizi
            doldurabilirsiniz; bu tamamen isteğe bağlıdır.
          </p>
          <p>
            Reklamları Google (AdMob) sunar. Google, reklam göstermek, ölçmek ve kötüye kullanımı
            önlemek için cihazınızın reklam kimliğini, IP adresini ve kaba konum, cihaz modeli
            gibi teknik bilgileri işleyebilir. Avrupa Ekonomik Alanı, Birleşik Krallık ve İsviçre'de
            ilk açılışta izniniz sorulur; kişiselleştirilmiş reklamları reddedebilir, kararınızı
            Ayarlar &gt; Diğer &gt; "Reklam gizlilik seçenekleri"nden değiştirebilirsiniz. Reklam
            kimliğinizi cihaz ayarlarından sıfırlayabilir ya da kişiselleştirmeyi kapatabilirsiniz.
            Ayrıntılar:{" "}
            <a className="underline" href="https://policies.google.com/technologies/partner-sites">
              Google ortak siteleri ve uygulamaları
            </a>
            .
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
            Uygulama titreşim (dokunsal geri bildirim; oyun içindeki ayardan kapatılabilir) ve
            reklamlar için internet ile reklam kimliği iznini kullanır. Kamera, mikrofon, konum,
            rehber ya da dosyalarınıza erişmez.
          </p>
        </Section>
        <Section title="Çocuklar">
          <p>
            Oyun çocuklara yönelik değildir; biz hiçbir kullanıcıdan, çocuklar dahil, veri
            toplamayız.
          </p>
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
            Sisyphus itself does not collect, store or share any personal data; there are no
            accounts or analytics. The store app shows ads through Google AdMob (below). The web
            version has no ads.
          </p>
        </Section>
        <Section title="Ads (store app)">
          <p>
            The app shows a small banner under the main menu; no ads appear while you play. When
            your breath runs out you may choose to watch a rewarded ad to refill it; this is
            entirely optional.
          </p>
          <p>
            Ads are served by Google (AdMob). To show and measure ads and to prevent abuse, Google
            may process your device's advertising ID, IP address and technical information such as
            approximate location and device model. In the European Economic Area, the UK and
            Switzerland you are asked for consent on first launch; you can refuse personalised ads
            and change your choice under Settings &gt; More &gt; "Ad privacy options". You can
            reset your advertising ID or turn off personalisation in your device settings.
            Details:{" "}
            <a className="underline" href="https://policies.google.com/technologies/partner-sites">
              How Google uses information from sites or apps that use its services
            </a>
            .
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
            The app uses vibration (haptic feedback; can be switched off in the game) and, for the
            ads, internet access and the advertising ID permission. It does not access your camera,
            microphone, location, contacts or files.
          </p>
        </Section>
        <Section title="Children">
          <p>
            The game is not directed at children; we collect no data from anyone, children
            included.
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
