# Devam notu (yeni sohbet için) — 29.10.2026 (akşam güncellemesi)

## Durum
- Oyun Unity 6 (6000.6.7f1) ile kullanıcının bilgisayarında açılıyor ve çalışıyor:
  ~63 FPS, hata yok; menü, HUD, hikâye sahneleri, prolog, 9 dil (Arapça/Hintçe/Bengalce
  şekillendirme dahil) doğrulandı.
- Proje: `S-S-FOS/unity` (repo `lotusdugun-ctrl/S-S-FOS`, dal `master`). Kullanıcının kopyası:
  `C:\Users\caner\OneDrive\Desktop\Sisifos Guncelle\S-S-FOS\unity`.
- Güncellemeler format-patch olarak `Sisifos Guncelle` klasörüne konur; kullanıcı
  GUNCELLE.bat ile uygular ve gönderir. Buradan push yapılmaz.
- Son gönderilen: 0048. Hazır, gönderilmeyi bekleyen: 0049–0052 (kullanıcı GUNCELLE.bat ile gönderir).
  0050: RenderTexture uyarısı. 0051: HUD harf aralığı ("YAM ACI"). 0052: iniş yürüyüşü.

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
1. Hareket: itme/koşu modlarına aynı yumuşatma; diz/kol/gövde biyomekaniği. İnişte çift
   destek anında dizler hâlâ belirgin bükük (Lb, eğimde 6*s'ye kadar alçaltma) — istenirse azalt.
2. Unity olanaklarıyla görsel iyileştirme: post-process (bloom, güneş huzmeleri, sis,
   film greni) `UnityBackend` ekran RT'si üzerinde; yağmur için GPU parçacıkları;
   NPC'lere (Merope, Prometheus, Tantalos) drawFigureLit benzeri ışık + nefes/göz kırpma/rüzgâr.
3. Tam oyun turu otomatik pilotla: zirve, Zeus, iniş, Bilgelik Defteri, ayarlar.
4. Android: kullanıcıya Unity Hub → Installs → 6000.6.7f1 → Add modules → Android Build
   Support (+OpenJDK, SDK&NDK) kurdurulacak; sonra otomatik pilota "build" eylemi
   (BuildPipeline.BuildPlayer) eklenip APK alınacak. Keystore yalnızca kullanıcının
   `Magaza/ANAHTAR-GIZLI` klasöründe; asla commit edilmez.
