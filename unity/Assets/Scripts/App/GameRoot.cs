using System;
using Sisifos.App.UI;
using Sisifos.Game;
using Sisifos.Paint;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sisifos.App
{
    /// <summary>What draws a frame into the screen surface (the engine, once it is ported).</summary>
    public interface IFrameDrawer
    {
        /// <param name="screen">The screen surface, device pixels (Screen.width × Screen.height).</param>
        /// <param name="nowMs">performance.now() for the frame.</param>
        void Draw(Surface screen, double nowMs);
    }

    /// <summary>Insets of the safe area from each screen edge, in device pixels (y down).</summary>
    public struct Insets
    {
        public double left, top, right, bottom;
    }

    /// <summary>
    /// The Unity host: wires the pure-C# game to Unity (drawing backend, data files, storage,
    /// main-thread work, app lifecycle), keeps the screen surface at the screen's size, draws a
    /// frame every Update and shows it on the camera. Created at startup when the scene has none.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot instance;

        /// <summary>Draws each frame. Null draws the backend test scene.</summary>
        public static IFrameDrawer drawer;

        /// <summary>The screen surface (what is drawn here is shown).</summary>
        public static Surface screen;

        /// <summary>Safe area insets (notches, home indicator), device pixels.</summary>
        public static Insets safeInsets;

        /// <summary>Device pixels per CSS pixel, as the browser's devicePixelRatio would be.</summary>
        public static double devicePixelRatio = 1;

        /// <summary>The capacitor build's backgroundColor (#1d2340), shown before the first frame.</summary>
        static readonly Color background = new Color(0x1d / 255f, 0x23 / 255f, 0x40 / 255f, 1);

        UnityBackend backend;
        PlayerPrefsStore store;
        Camera cam;
        CommandBuffer present;
        /// <summary>bloom, light shafts, mist, rain and grain on the way to the screen (null: not supported)</summary>
        PostFx post;
        RenderTexture presented;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("GameRoot");
            go.AddComponent<GameRoot>();
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            MainThread.Init();

            QualitySettings.vSyncCount = 0;
            // the browser drew at the display's rate (requestAnimationFrame)
            double hz = Screen.currentResolution.refreshRateRatio.value;
            // 60 at most: at 90 or 120 a phone heats up within minutes and then throttles
            // itself hard, which showed as freezes well into a climb
            Application.targetFrameRate = hz >= 30 ? Math.Min(60, (int)Math.Round(hz)) : 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            // the store apps are landscape only (sensorLandscape / UISupportedInterfaceOrientations)
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Input.multiTouchEnabled = true;

            Data.loader = LoadData;
            store = new PlayerPrefsStore();
            Store.impl = store;
            Native.native = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            Native.minimizeApp = () =>
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    activity.Call<bool>("moveTaskToBack", true);
            };
#endif
            Haptics.Init();
            Save.preferredLanguages = PreferredLanguages;

            backend = new UnityBackend();
            Gfx.backend = backend;
            screen = new Surface(Math.Max(1, Screen.width), Math.Max(1, Screen.height), true, "screen") { isScreen = true };
            UpdateMetrics();
            SetupCamera();

            // the game: the engine draws into the screen surface, the UI Toolkit panel sits over it
            UiKit.textureOf = backend.TextureOf;
            UiKit.Init(gameObject);
            // ads (store app only; a no-op without the plugin): consent, then the SDK
            if (Native.native) Ads.Init();
            audioHost = AudioHost.Create(transform);
            SisyphusGameView.reloadGame = () => reload = true;
            StartGame();
#if UNITY_EDITOR
            AutopilotRunner.MaybeStart(gameObject);
#endif
        }

        /// <summary>the running engine, for tools</summary>
        public static Sisifos.Game.SisyphusEngine CurrentEngine
        {
            get { return instance != null && instance.game != null ? instance.game.Engine : null; }
        }

        SisyphusGameView game;
        AudioHost audioHost;
        bool reload;

        void StartGame()
        {
            game = new SisyphusGameView(screen, audioHost);
            if (game.Engine != null) game.Engine.gpuWeather = post != null;
            UiKit.root.Add(game);
            drawer = game;
        }

        /// <summary>window.location.reload() after a backup was restored: the game screen again, from the save</summary>
        void ReloadGame()
        {
            reload = false;
            if (game != null)
            {
                game.Stop();
                game.Engine.audio.dispose();
                game.RemoveFromHierarchy();
            }
            game = null;
            drawer = null;
            StartGame();
        }

        /// <summary>navigator.languages: the device's language as a code the game knows, or nothing</summary>
        static string[] PreferredLanguages()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Turkish: return new[] { "tr" };
                case SystemLanguage.English: return new[] { "en" };
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return new[] { "zh" };
                case SystemLanguage.Hindi: return new[] { "hi" };
                case SystemLanguage.Spanish: return new[] { "es" };
                case SystemLanguage.French: return new[] { "fr" };
                case SystemLanguage.Arabic: return new[] { "ar" };
                case SystemLanguage.Portuguese: return new[] { "pt" };
            }
            // Bengali has no SystemLanguage: ask the culture
            try { return new[] { System.Globalization.CultureInfo.CurrentUICulture.Name }; }
            catch (Exception) { return new string[0]; }
        }

        static string LoadData(string path)
        {
            // Resources is main-thread only; Work jobs reach it through MainThread
            return MainThread.Invoke(() =>
            {
                var ta = Resources.Load<TextAsset>("Data/" + path);
                if (ta == null) return null;
                string s = ta.text;
                Resources.UnloadAsset(ta);
                return s;
            });
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Camera");
                go.transform.SetParent(transform, false);
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.cullingMask = 0;
            cam.orthographic = true;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.useOcclusionCulling = false;
            present = new CommandBuffer { name = "Sisifos.Present" };
            post = PostFx.Create();
            cam.AddCommandBuffer(CameraEvent.AfterEverything, present);
        }

        void UpdateMetrics()
        {
            var sa = Screen.safeArea; // pixels, origin bottom-left
            int w = Screen.width, h = Screen.height;
            safeInsets = new Insets
            {
                left = Math.Max(0, sa.xMin),
                right = Math.Max(0, w - sa.xMax),
                top = Math.Max(0, h - sa.yMax),
                bottom = Math.Max(0, sa.yMin),
            };
            float dpi = Screen.dpi;
#if UNITY_IOS && !UNITY_EDITOR
            const float cssDpi = 163f; // iOS points
#else
            const float cssDpi = 160f; // Android dp
#endif
            devicePixelRatio = dpi > 0 ? Math.Max(1.0, Math.Round(dpi / cssDpi * 4) / 4) : 1;
            float k = (float)devicePixelRatio;
            UiKit.SetMetrics(k, w / k, h / k, (float)(safeInsets.left / k), (float)(safeInsets.top / k), (float)(safeInsets.right / k), (float)(safeInsets.bottom / k));
        }

        int lastW, lastH;
        Rect lastSafe;
        double lastNow;

        void Update()
        {
            // the editor recompiled while playing: everything built in Awake is gone, and
            // every frame threw. Nothing to draw with; stop playing to start again.
            if (screen == null || backend == null) return;
            MainThread.Pump();
            Work.Pump();
            if (Input.GetKeyDown(KeyCode.Escape)) Native.backButton();
            if (reload) ReloadGame();

            int w = Math.Max(1, Screen.width), h = Math.Max(1, Screen.height);
            if (w != lastW || h != lastH || Screen.safeArea != lastSafe)
            {
                lastW = w; lastH = h; lastSafe = Screen.safeArea;
                UpdateMetrics();
                // the engine sizes the screen surface itself (resize(): CSS size × its own pixel ratio)
                if (game == null) screen.SetSize(w, h);
            }

            double now = Js.now();
            double dt = lastNow > 0 ? now - lastNow : 0;
            lastNow = now;
            Haptics.Tick();
            UiKit.Tick();
            game?.Tick(dt);
            if (drawer != null) drawer.Draw(screen, now);
            else DrawTest(screen, now);
            UiKit.DrawCanvases();
            Gfx.Flush();
            UiKit.PresentCanvases();

            var rt = backend.TextureOf(screen);
            if (post != null) post.Present(present, rt, game?.Engine);
            else if (rt != presented)
            {
                presented = rt;
                present.Clear();
                if (rt != null) present.Blit(rt, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget));
            }
        }

        static void DrawTest(Surface s, double now)
        {
            var c = s.GetContext();
            c.SetTransform(1, 0, 0, 1, 0, 0);
            c.FillStyle = "#1d2340";
            c.FillRect(0, 0, s.width, s.height);
            double k = Math.Min(s.width / 400.0, s.height / 420.0);
            c.Translate((s.width - 400 * k) / 2, (s.height - 420 * k) / 2);
            c.Scale(k, k);
            TestScene.Draw(c);
            TestScene.DrawText(c, now / 1000);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                // the handlers keep the climb in progress first, then the store is written out
                Native.appPaused();
                store?.Flush();
            }
            else Native.appResumed();
        }

        void OnApplicationQuit()
        {
            game?.Keep();
            store?.Flush();
        }

        void OnDestroy()
        {
            if (instance != this) return;
            if (cam != null && present != null) cam.RemoveCommandBuffer(CameraEvent.AfterEverything, present);
            present?.Release();
            post?.Dispose();
            if (Gfx.backend == backend) Gfx.backend = null;
            backend?.Dispose();
            instance = null;
        }
    }
}
