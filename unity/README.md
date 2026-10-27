# Sisyphus — Unity 6 (Android + iOS)

Open the folder in Unity 6000.0 and press Play: `GameRoot` boots itself (no scene setup needed)
and starts the real game — the engine (`Assets/Scripts/Game`) draws into the screen surface, the
UI Toolkit panel (`Assets/Scripts/App/UI`) sits on top of it. See `PORTING.md` for the port rules.

## Türkçe: hızlı başlangıç

1. Unity Hub'ı aç → **Installs** → Unity **6000.0 LTS** sürümünü kur (Android Build Support ve iOS Build Support modülleriyle).
2. Unity Hub → **Projects** → **Add → Add project from disk** → reponun içindeki `unity` klasörünü seç.
3. Proje ilk açılışta birkaç dakika içe aktarır (Library klasörü oluşturulur). Sürüm uyarısı çıkarsa kurduğun 6000.0.x sürümüyle açmayı onayla.
4. `Assets/Scenes/Main.unity` sahnesini açıp **Play**'e bas. Oyun kendiliğinden başlar.
5. Telefona almak için: **File → Build Profiles** → Android (veya iOS) → **Switch Platform** → **Build**.
6. Console'da kırmızı hata görürsen ekran görüntüsünü ya da hata metnini bana gönder, düzelteyim.

## How the pieces connect

- `App/GameRoot.cs` — the host MonoBehaviour: drawing backend, data, storage, the camera that
  shows the screen surface, safe area / devicePixelRatio, app pause/resume, Android's back button
  (`Escape`). Every frame: `Haptics.Tick` → `UiKit.Tick` (tweens/timers) → `SisyphusGameView.Tick`
  (keyboard, notes, re-render) → `engine.frame` → `UiKit.DrawCanvases` → `Gfx.Flush` →
  `UiKit.PresentCanvases`.
- `App/UI/UiKit.cs` — the panel (created in code; 1 UI unit = 1 CSS px, scale = devicePixelRatio),
  fonts, CSS colours, tweens (CSS keyframes), `CanvasBox` (gradients, box-shadows and SVG icons
  painted with `Sisifos.Paint.Ctx`), `ScrollBox`.
- `App/UI/SisyphusGameView.cs` — SisyphusGame.tsx; `GameExtras.cs`, `Reveal.cs`, `Share.cs`;
  `StoryView.cs`, `PrologueView.cs`, `WisdomBookView.cs` — the story and the book.
- `App/NativeHost.cs` — haptics (Android Vibrator / iOS `UIImpactFeedbackGenerator`) and the audio
  host (a clip-less AudioSource rendering the synthesized effects, a streamed AudioSource playing
  `Resources/Audio/music.mp3`, each through `OnAudioFilterRead` into `GameAudio`).
- `Plugins/iOS/SisifosNative.mm` — haptics and the share sheet on iOS.

## Arabic, Hindi, Bengali, Chinese (no manual step)

UI Toolkit shapes complex scripts only with its *Advanced Text Generator*, which Unity 6.0 turns
on from **Project Settings › UI Toolkit › Enable Advanced Text** and which needs ICU data that only
the editor can put into a build (attached to a PanelSettings asset). `App/Editor/SisifosUiSetup.cs`
does this automatically whenever scripts load in the editor: it enables the setting and keeps
`Assets/Resources/UI/SisifosPanel.asset` with the ICU data (and Unity's default runtime theme,
`Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss`) assigned. At runtime `UiKit.Init`
loads that asset and switches the generator on for the player (by reflection — Unity 6.0 only sets
that switch in the editor; `Assets/link.xml` keeps it through stripping). Missing glyphs come from
the OS fonts (Noto on Android, Geeza Pro / Kohinoor / PingFang on iOS).

If the asset is missing (the editor never ran the script), the menu item **Sisifos › Set up UI
text (ICU data)** does the same; without it the player logs a warning and Arabic/Indic text is not
shaped.

## Things to check in the editor

- Arabic/Hindi/Bengali shaping in the menu, settings and the book (switch the language in Settings).
- `<line-height=…em>` rich-text tags on multi-line labels give CSS line spacing; check that the
  advanced generator honours them (instructions on the start card, the saying of the day).
- Canvas-painted backgrounds (`CanvasBox`): paper cards, shadows, icons appear upright and with
  clean edges (premultiplied → straight alpha through `Resources/Shaders/SisifosUnpremul.shader`).
- The settings text boxes: caret, selection and the on-screen keyboard.
- Share: the saying card opens the share sheet (Android intent / iOS activity sheet).
- Reference screenshots of the web build for comparison: `/tmp/claude-0/ui1/ref/` (make them again
  with `node /tmp/claude-0/ui1/ref.cjs` against `npx vite --port 5199`).
