# Devam notu (yeni sohbet için) — 29.10.2026

## Durum
- Oyun Unity 6 (6000.6.7f1) ile kullanıcının bilgisayarında açılıyor ve çalışıyor:
  ~63 FPS, hata yok; menü, HUD, hikâye sahneleri, prolog, 9 dil (Arapça/Hintçe/Bengalce
  şekillendirme dahil) doğrulandı.
- Proje: `S-S-FOS/unity` (repo `lotusdugun-ctrl/S-S-FOS`, dal `master`). Kullanıcının kopyası:
  `C:\Users\caner\OneDrive\Desktop\Sisifos Guncelle\S-S-FOS\unity`.
- Güncellemeler format-patch olarak `Sisifos Guncelle` klasörüne konur; kullanıcı
  GUNCELLE.bat ile uygular ve gönderir. Buradan push yapılmaz.
- Son gönderilen: 0048. Bu notla birlikte 0049 hazırlandı (aşağıda).

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

## Yarım kalan iş (kullanıcının son isteği)
1. Sisifos'un hareketleri: inişteki "zıplama" bulundu ve 0049'da düzeltildi (yürürken kalça
   artık basılan ayaktan değil, kalçanın altındaki zeminden + ters sarkaç salınımından
   hesaplanıyor; yokuşta adım kısalıyor: `strideFor(mode, v, slope)`). Kalça izi
   -52→-32 testereden -41..-43 yumuşak salınıma indi. GÖRSEL OLARAK Unity'de henüz
   denenmedi → otomatik pilotla iniş görüntüsü al (cycles kaydı + push, zirve sonrası iniş).
   Sonra: itme/koşu modlarına da aynı yumuşatma; diz/kol/gövde biyomekaniği.
2. Unity olanaklarıyla görsel iyileştirme: post-process (bloom, güneş huzmeleri, sis,
   film greni) `UnityBackend` ekran RT'si üzerinde; yağmur için GPU parçacıkları;
   NPC'lere (Merope, Prometheus, Tantalos) drawFigureLit benzeri ışık + nefes/göz kırpma/rüzgâr.
3. Tam oyun turu otomatik pilotla: zirve, Zeus, iniş, Bilgelik Defteri, ayarlar.
4. Android: kullanıcıya Unity Hub → Installs → 6000.6.7f1 → Add modules → Android Build
   Support (+OpenJDK, SDK&NDK) kurdurulacak; sonra otomatik pilota "build" eylemi
   (BuildPipeline.BuildPlayer) eklenip APK alınacak. Keystore yalnızca kullanıcının
   `Magaza/ANAHTAR-GIZLI` klasöründe; asla commit edilmez.
5. Küçük: HUD'da "ÇAKIL YAMACI" içinde M ile A arasında fazladan boşluk.
