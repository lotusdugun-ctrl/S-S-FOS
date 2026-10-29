# Devam notu (yeni sohbet için) — 29.10.2026 (akşam güncellemesi)

## Durum
- Oyun Unity 6 (6000.6.7f1) ile kullanıcının bilgisayarında açılıyor ve çalışıyor:
  ~63 FPS, hata yok; menü, HUD, hikâye sahneleri, prolog, 9 dil (Arapça/Hintçe/Bengalce
  şekillendirme dahil) doğrulandı.
- Proje: `S-S-FOS/unity` (repo `lotusdugun-ctrl/S-S-FOS`, dal `master`). Kullanıcının kopyası:
  `C:\Users\caner\OneDrive\Desktop\Sisifos Guncelle\S-S-FOS\unity`.
- Güncellemeler format-patch olarak `Sisifos Guncelle` klasörüne konur; kullanıcı
  GUNCELLE.bat ile uygular ve gönderir. Buradan push yapılmaz.
- Son gönderilen: 0048. Yamaları kullanıcı GUNCELLE.bat ile gönderir.
  0050: RenderTexture uyarısı. 0051: HUD harf aralığı ("YAM ACI"). 0052: iniş yürüyüşü.
- 0049–0052 gönderildi (7ef6127). 0053 gönderildi (8e467f7). Bekleyen: 0054 (görsel efektler).

## Uzaktan test: otomatik pilot
- Unity açıkken `unity/Logs/autopilot.json` (senaryo) yazılır, sonra `Logs/autopilot-go.txt`
  bırakılır → editör yeniler, derler, Play'e girer, senaryoyu oynatır, sonuçları
  `Logs/autopilot/` (shot_*.png, report.txt) içine yazar, Play'den çıkar.
- Eylemler: begin, push, back, release, keys, kick, lang:<kod>, shot:<ad>, stats, stop,
  fresh (kayıtsız ilk açılış), save:<json>, restore (kullanıcının kaydı), key:<KeyCode>.
- Editör günlüğü: `Logs/Editor-copy.log` (her 3 sn kopyalanır).
- Unity, kullanıcı başka bir projeyi açarsa çalışmaz (bir kez AntikViewer açıktı).

## Derleme kontrolü (Unity olmadan)
- Saf C# + görüntü testi: `unity/Headless` → `dotnet build -c Release -o /tmp/hl`
  (nuget.config kaynakları kapatır). Modlar: `core`, `play`, `gait <descent|push> <dir> w h warm frames level`.
- Unity API taklitleri: `unity/Tools/UnityStubs/check{EDITOR,ANDROID,IOS}` → `dotnet build`.
  (dotnet: `apt-get install dotnet-sdk-8.0`)

## Yapılanlar (29.10 akşam, hepsi Unity'de otomatik pilotla doğrulandı)
- 0049 iniş zıplaması: Unity'de doğrulandı, kalça düzgün.
- 0052 iniş: yürüyüş hızı eğime göre (`walkSpeedFor`, düzde 100, eğim 0.5'te ~80 birim/sn;
  eskiden sabit 125 ve saniyede 4,7 adım = koşar adım). Adım ~2,5/sn. İniş ~40 sn.
  Ön ayak eğimde daha yakına basıyor (`centre`), yere basacak ayağın uzanma sınırı salınımın
  sonunda yavaşça devreye giriyor → her adımda 3 birimlik kalça düşüşü kalktı.
  Ayak sesi artık her ayak yere bastığında (gait π geçişi).
- 0051 HUD: Unity standart metin üreticisi, IgnoreSpacingAdjustments bayraklı kerning
  çiftlerinde harf aralığını siliyordu. UiKit.KeepSpacingThroughKerning bayrağı temizliyor
  (yansıma ile, FontAsset.m_FontFeatureTable → m_GlyphPairAdjustmentRecordLookup).
- 0050 Bilgelik Defteri kapanırken "Releasing render texture that is set to be active" uyarısı.
- Not: GaitTest artık her karede cutscene=false yapıyor (Merope sahnesi onu yokuşa çeviriyordu).
- Otomatik pilot: hikâye sahnesi iniş konumuna göre tetiklenir (tartarus walkX≈1630'da
  Merope); yürüyüş testi için walkX≈2950 kullan.

## Yarım kalan iş
1. Hareket: 0053 ile itme/koşu kalçası yumuşadı (itmede her adımda 7 birimlik zıplama
   vardı; koşuda uçuşta sıçrama). Kalan: diz/kol/gövde biyomekaniği; inişte çift destek
   anında dizler hâlâ belirgin bükük (Lb) — istenirse azalt. GaitTest modları: descent, push, run.
2. Görsel efektler (0054): PostFx.cs + Resources/Shaders/SisifosPost.shader — bloom, güneş
   huzmeleri (parlak kısımların güneşe doğru radyal bulanıklığı), ufuk boyunca sis, GPU yağmuru
   (3 derinlik; engine.gpuWeather=true iken drawWeather yağmuru çizmez, kar CPU'da), film greni.
   Motor parametreleri: SisyphusEngine.postFx() (Engine.PostFx.cs). lowQuality'de hepsi kapalı.
   NPC'ler zaten paintLit ile ışıklıydı; artık npcLight(x,y) her birinin kendi konumundan güneşe,
   Zeus fırtınası/yüksek güneş kuralları adamınkiyle aynı. Göz kırpma (Figures.blink), Merope'nin
   pelerini rüzgârla (FigLight.wind). Kalan: huzmeler ince, istenirse _Sun.z çarpanı artırılır.
3. Tam oyun turu yapıldı (0055): zirve → Zeus → yuvarlanma → iniş, mühür, bilgelik sayfası,
   Bilgelik Defteri, ayarlar, İngilizce; hata 0. Yeni eylemler: book, settings (aç/kapa).
   Dikkat: Unity penceresi odakta değilken editör çok seyrek güncellenir (bir turda ısınma
   293 sn sürdü). Kullanıcı Preferences > General > Interaction Mode = No Throttling yapabilir.
4. Android: kullanıcıya Unity Hub → Installs → 6000.6.7f1 → Add modules → Android Build
   Support (+OpenJDK, SDK&NDK) kurdurulacak. Derleme eylemi hazır (0055): Logs/autopilot-build.txt
   içine "apk" ya da "aab" yaz → Builds/Sisifos.apk|aab, sonuç Logs/autopilot/build.txt
   ("no-android-module" = modül kurulu değil). Anahtar yoksa Unity'nin debug imzası kullanılır. Keystore yalnızca kullanıcının
   `Magaza/ANAHTAR-GIZLI` klasöründe; asla commit edilmez.
