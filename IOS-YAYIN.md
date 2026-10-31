# Sisyphus: Endless Climb — iOS'ta App Store'a yayın rehberi

Bu rehber, oyunu GitHub'dan alıp bir Mac'te derleyecek ve App Store'a gönderecek kişi içindir.
Oyun Unity 6 ile yazıldı. Android sürümü Google Play'de; iOS sürümü aynı koddan çıkar, kodda
iOS için ayrıca bir şey yazmaya gerek yok.

## 0. Gerekenler

| Ne | Ayrıntı |
|---|---|
| Mac | Apple Silicon ya da Intel, macOS 14 veya üstü |
| Xcode | 16 veya üstü (App Store'dan) |
| Unity Hub + **Unity 6000.6.7f1** | Hub > Installs > Install Editor > Archive'dan bu sürüm; **iOS Build Support** modülüyle |
| CocoaPods | Terminal: `brew install cocoapods` (ya da `sudo gem install cocoapods`) — reklam kütüphanesi bununla gelir |
| Apple Developer Program üyeliği | Yıllık 99 $. Uygulama, mağazada bu hesabın adıyla görünür. |
| GitHub erişimi | `https://github.com/lotusdugun-ctrl/S-S-FOS` (repo sahibi seni ekler) |

## 1. Projeyi al ve aç

```bash
git clone https://github.com/lotusdugun-ctrl/S-S-FOS.git
```

1. Unity Hub > **Add > Add project from disk** > `S-S-FOS/unity` klasörünü seç.
2. Unity 6000.6.7f1 ile aç. İlk açılış birkaç dakika sürer (paketler ve reklam eklentisi iner).
3. Bir "External Dependency Manager / Resolver" sorusu çıkarsa **Enable / Resolve** de.

## 2. iOS'a geç ve ayarları kontrol et

1. **File > Build Profiles > iOS > Switch Platform**.
2. **Edit > Project Settings > Player > iOS**:
   - *Bundle Identifier*: `com.lotusdugun.sisyphus` (hazır; değiştirme — Android'le aynı kimlik)
   - *Version*: `1.0.1`, *Build*: `1` (her yeni yüklemede Build'i bir artır)
   - *Signing Team ID*: Apple Developer hesabındaki Team ID (developer.apple.com > Membership)
   - *Automatically Sign*: açık
   - *Target minimum iOS Version*: 13.0 (hazır)
3. Uygulama simgesi ve AdMob kimlikleri proje açılırken kendiliğinden yazılır
   (`Assets/Editor/LogExport/AdsSetup.cs`, iOS AdMob App ID: `ca-app-pub-5494310464478030~5899516284`).
   Kontrol: **Assets > Google Mobile Ads > Settings** penceresinde iOS App ID dolu olmalı.

## 3. Xcode projesini üret ve arşivle

1. **File > Build Profiles > iOS > Build** > boş bir klasör seç (ör. `~/SisyphusXcode`).
2. Bitince Terminal'de o klasöre gir ve (gerekirse) `pod install` çalıştır.
3. **`Unity-iPhone.xcworkspace`** dosyasını aç (`.xcodeproj` değil — reklam kütüphanesi workspace'te).
4. Xcode > *Unity-iPhone* target > **Signing & Capabilities**: Team'i seç, "Automatically manage signing" açık.
5. Üstten cihaz olarak **Any iOS Device (arm64)** seç > **Product > Archive**.
6. Organizer açılınca **Distribute App > App Store Connect > Upload**.

`Info.plist`'e "şifreleme kullanmıyor" (`ITSAppUsesNonExemptEncryption = NO`) bilgisini
`Assets/Editor/LogExport/IosPostBuild.cs` yazar; yüklemede ihracat sorusu çıkmaz.

## 4. App Store Connect'te uygulamayı oluştur

[appstoreconnect.apple.com](https://appstoreconnect.apple.com) > **Apps > + > New App**

| Alan | Değer |
|---|---|
| Platform | iOS |
| Name | `Sisyphus: Endless Climb` |
| Primary language | English (U.S.) |
| Bundle ID | `com.lotusdugun.sisyphus` (Xcode'dan ilk yüklemede listede belirir; yoksa developer.apple.com > Identifiers'dan ekle) |
| SKU | `sisyphus-endless-climb` |
| Category | Games > Casual (ikincil: Adventure) |
| Price | Free |

### Mağaza metinleri
`store/` klasöründe:
- `store/listing-en.txt` — İngilizce başlık, kısa açıklama, uzun açıklama
- `store/apple-en.txt` — Apple'a özel alanlar (Subtitle 30 karakter, Keywords 100 karakter,
  Promotional Text, Review Notes)
- `store/translations/` — diğer diller (App Store'da istersen aynı metinleri yerelleştirmelere yapıştır)

### Ekran görüntüleri
`store/graphics/` klasöründe hazır:
- `ios-iphone-6.9/` — 2868 × 1320 (yatay), App Store'un zorunlu iPhone boyutu
- `ios-ipad-13/` — 2752 × 2064 (yatay), iPad için

### Tanıtım videosu (App Preview, isteğe bağlı)
`store/video/` içindeki film, App Preview için 1920 × 886'ya kırpılıp en fazla 30 sn olmalı;
şimdilik atlanabilir.

### App Privacy (Uygulama Gizliliği)
Oyun kendisi veri toplamaz; **Google AdMob** reklamları şunları toplar:
- **Identifiers > Device ID** — Third-Party Advertising, Analytics — *Linked to user: No*, *Tracking: No*
- **Usage Data > Product Interaction** — Third-Party Advertising, Analytics — No / No
- **Diagnostics > Crash Data, Performance Data** — Analytics — No / No
- **Location > Coarse Location** — Third-Party Advertising — No / No

Oyun ATT (izleme izni) penceresi göstermez; bu yüzden "Data Used to Track You" boş kalır.
Gizlilik politikası: `https://s-s-fos.vercel.app/privacy`

### Age Rating
Tüm sorulara **None / No**. ("Cartoon or Fantasy Violence": None — yalnızca Zeus'un yıldırımı, kansız.)
Sonuç 4+ civarı çıkar. Anket reklamla ilgili bir soru sorarsa cevap **Yes** (oyunda AdMob reklamı var).

### App Review Information
- Sign-in required: **No**
- Notes: `store/apple-en.txt` içindeki "REVIEW NOTES" metni.
- Contact: repo sahibinin bilgileri (lotusdugun@gmail.com)

## 5. TestFlight ve gönderme

1. Yükleme işlendikten sonra (10–30 dk) **TestFlight** sekmesinde build görünür; kendi iPhone'unda dene.
2. **App Store** sekmesi > sürüm 1.0 > Build bölümünden yüklenen build'i seç.
3. **Add for Review > Submit**.

## 6. Yayından sonra

- AdMob > Apps > iOS uygulamasını **App Store'daki uygulamaya bağla** (store'da yayınlandıktan sonra).
- `app-ads.txt` zaten `https://s-s-fos.vercel.app/app-ads.txt` adresinde; App Store'daki
  "Marketing URL"/"Support URL" olarak `https://s-s-fos.vercel.app` girilmeli ki AdMob doğrulasın.

## Sorun çıkarsa

- **"No such module GoogleMobileAds" / pod hatası**: derleme klasöründe `pod install`, sonra `.xcworkspace`'i aç.
- **İmza hatası**: Xcode > Settings > Accounts'ta Apple ID ekli mi; Team seçili mi.
- **Unity'de iOS Build butonu pasif**: Hub > Installs > 6000.6.7f1 > Add modules > iOS Build Support.
