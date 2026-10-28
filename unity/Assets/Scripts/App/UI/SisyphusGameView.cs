// Port of src/components/SisyphusGame.tsx: the game screen. The engine draws into the screen
// surface (shown by GameRoot's camera); this VisualElement is the React tree over it — the HUD,
// the start card, the touch controls, the story's overlays, the settings and the book — and all
// of the wiring: the save, the seals, the saga's events, audio, haptics, keyboard and touch input,
// the app's lifecycle and Android's back button.
//
// React → C#: useState fields live on this class; setState marks the view dirty and Render()
// brings the tree up to date once a frame. useEffect bodies run where their dependencies change
// (OnState for the engine's state, the setters otherwise). Timers are UiKit tweens bound to an
// element, so they die with it, like a cleared timeout.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sisifos.Game;
using Sisifos.Paint;
using UnityEngine;
using UnityEngine.UIElements;
using LineCap = Sisifos.Paint.LineCap;
using LineJoin = Sisifos.Paint.LineJoin;

namespace Sisifos.App.UI
{
    public sealed class SisyphusGameView : VisualElement, IFrameDrawer
    {
        static int TOTAL_PAGES { get { return Quotes.QUOTE_COUNT; } }
        const float JOY_RADIUS = 36;

        // ------------------------------------------------------------ state (useState / useRef)

        readonly SisyphusEngine engine;
        readonly AudioHost audioHost;
        EngineState state = new EngineState
        {
            phase = "playing",
            progress = 0,
            levelName = "",
            epigraph = "",
            cycles = 0,
            climbTime = 0,
            lastClimb = 0,
            bestClimb = 0,
            newBest = false,
            hint = null,
            unlocked = new List<int>(),
            newQuote = -1,
            weather = "clear",
            levelId = "tartarus",
            tutorialDone = true,
            lastRun = null,
        };
        bool started;
        bool muted;
        string lang = "tr";
        bool haptics = true;
        /// <summary>bumps when a language's aphorisms finish loading, so the book redraws</summary>
        int quotesReady;
        bool vibrates;
        bool bookOpen;
        /// <summary>pages found since the book was last opened, marked on their corner</summary>
        HashSet<int> fresh = new HashSet<int>();
        /// <summary>the page just found, while its moment is on screen (-1: none)</summary>
        int ceremony = -1;
        double toasted = -1;
        bool settingsOpen;
        bool calm;
        /// <summary>seals earned, and the ones waiting to be announced</summary>
        HashSet<string> seals = new HashSet<string>();
        readonly List<string> sealQueue = new List<string>();
        int sealQueueVer;
        List<string> hillsRef = new List<string>();
        readonly HashSet<string> sealsRef = new HashSet<string>();
        /// <summary>the saying of the day: its page, and whether it went into the book</summary>
        readonly int daily = Daily.dailyIndex();
        bool dailyAdded;
        bool bigText, boldRing, leftHanded;
        string ownText = "", ownAuthor = "";
        double recStreak, recWeek;
        /// <summary>chapters already whole, and the one just finished while its note is up</summary>
        HashSet<int> chaptersSeen;
        int chapterToast = -1;
        /// <summary>what is being said: scenes that stop the climb, and words in passing</summary>
        readonly List<Block> blocks = new List<Block>();
        bool finaleRef;
        readonly Dialogue.Notes notes = Dialogue.useNotes();
        int stage = 1;
        bool prologue;
        bool epiphany;
        bool ended;
        bool asleep;
        string langRef = "tr";

        bool dirty = true;
        bool touch, portrait;

        // ------------------------------------------------------------ the tree

        readonly VisualElement canvasLayer, baseLayer, z30, z35, z40, z45, z50, z70;
        readonly VisualElement slotCeremony, slotSeal, slotStoryToast, slotSubtitle, slotEpiphany, slotChapter;
        readonly VisualElement slotScene, slotSettings, slotBook, slotEnd, slotPrologue;

        public SisyphusGameView(Surface screen, AudioHost host)
        {
            audioHost = host;
#if UNITY_EDITOR
            AutopilotRunner.begin = () => { if (!started) Begin(); };
            AutopilotRunner.kick = () => engine?.kick();
            AutopilotRunner.lang = ChangeLang;
#endif
            name = "SisyphusGame";
            UiKit.Fill(this);
            pickingMode = PickingMode.Ignore;
            style.overflow = Overflow.Hidden;

            VisualElement Layer(string n)
            {
                var l = new VisualElement { name = n, pickingMode = PickingMode.Ignore };
                UiKit.Fill(l);
                Add(l);
                return l;
            }
            VisualElement Slot(VisualElement parent, string n)
            {
                var s = new VisualElement { name = n, pickingMode = PickingMode.Ignore };
                UiKit.Fill(s);
                parent.Add(s);
                return s;
            }

            // <canvas className="h-full w-full touch-none" />: takes the drag that walks him
            canvasLayer = new VisualElement { name = "canvas", pickingMode = PickingMode.Position };
            UiKit.Fill(canvasLayer);
            Add(canvasLayer);
            baseLayer = Layer("base");
            z30 = Layer("z30");
            slotCeremony = Slot(z30, "ceremony");
            slotSeal = Slot(z30, "seal");
            slotStoryToast = Slot(z30, "storyToast");
            slotSubtitle = Slot(z30, "subtitle");
            slotEpiphany = Slot(z30, "epiphany");
            slotChapter = Slot(z30, "chapter");
            z35 = Layer("z35");
            slotScene = Slot(z35, "scene");
            z40 = Layer("z40");
            slotSettings = Slot(z40, "settings");
            slotBook = Slot(z40, "book");
            z45 = Layer("z45");
            slotEnd = Slot(z45, "end");
            slotPrologue = Slot(z45, "prologue");
            z50 = Layer("z50");
            z70 = Layer("z70");

            touch = UiKit.Touch;
            portrait = touch && UiKit.viewH > UiKit.viewW;

            // ---- the engine (the first useEffect)
            // what this device remembers; the language falls back to the device's own
            if (Save.restoreNativeSave()) { /* the store copy was newer: loadSave below reads it */ }
            var save = Save.loadSave();
            engine = new SisyphusEngine(screen, 0, new EngineInit
            {
                cycles = save.cycles,
                bestClimb = save.bestClimb,
                unlocked = save.quotes,
                haptics = save.haptics,
                calm = save.calm,
                tutorialDone = save.tutorialDone,
                saga = save.saga,
                run = save.run,
            });
            // PORT: prefers-reduced-motion has no counterpart on the phones; the camera moves
            engine.cinematic = true;
            audioHost?.Attach(engine.audio);
            stage = engine.saga.stage;
            if (!save.saga.prologue) prologue = true;
            engine.onSaga = OnSaga;
            engine.onMire = () => PushNote(new StoryNote { kind = "mire" });
            // at the stone, at the very end: the words, then the end card
            engine.onFinale = () =>
            {
                epiphany = true;
                dirty = true;
                UiKit.After(this, 10000, () => { ended = true; dirty = true; });
            };
            calm = save.calm;
            // only seals that still exist (one for climbing in fog went with the fog)
            var known = new List<string>();
            foreach (var x in save.seals) if (Array.IndexOf(Seals.SEAL_ORDER, x) >= 0) known.Add(x);
            seals = new HashSet<string>(known);
            foreach (var x in known) sealsRef.Add(x);
            hillsRef = new List<string>(save.hills);
            haptics = save.haptics;
            vibrates = Native.canVibrate();
            chaptersSeen = new HashSet<int>(ChaptersWhole(save.quotes));
            bigText = save.bigText; boldRing = save.boldRing; leftHanded = save.leftHanded;
            engine.boldRing = save.boldRing;
            UiKit.SetRootFontSize(save.bigText ? 18 : 16);
            ownText = save.ownText ?? ""; ownAuthor = save.ownAuthor ?? "";
            if (!string.IsNullOrEmpty(save.ownText)) engine.ownQuote = new Quote { text = save.ownText, author = string.IsNullOrEmpty(save.ownAuthor) ? "—" : save.ownAuthor };
            string wk = Save.weekKey();
            recStreak = save.bestStreak;
            recWeek = save.weekKey == wk ? save.weekBest : 0;
            engine.onState = OnState;
            string initialLang = save.lang ?? Save.browserLang();
            lang = initialLang;
            langRef = lang;
            UiKit.UseLanguage(lang);
            engine.setLanguage(initialLang);
            Story.loadStory(initialLang);
            quotesReady++;
            Quotes.loadQuotes(initialLang);
            engine.refreshQuotes();
            quotesReady++;
            // the saying of the day goes into the book, once a day
            string today = Daily.todayKey();
            int idx = Daily.dailyIndex();
            if (save.daily != today)
            {
                engine.grantQuote(idx);
                Save.writeSave(d => d.daily = today);
            }
            dailyAdded = true;
            muted = save.muted;
            engine.audio.setMuted(save.muted);
            ResizeEngine();
            engine.start();

            // ---- pointer on the canvas (mouse + touch drag)
            canvasLayer.RegisterCallback<PointerDownEvent>(PDown);
            canvasLayer.RegisterCallback<PointerMoveEvent>(PMove);
            canvasLayer.RegisterCallback<PointerUpEvent>(PEnd);
            canvasLayer.RegisterCallback<PointerCancelEvent>(PEnd);

            // ---- the climb in progress is kept as it goes
            keepTimer = 4000;

            Native.watchApp(new AppHandlers
            {
                onPause = () =>
                {
                    engine.input = 0;
                    engine.audio.suspend();
                    Save.writeSave(d => d.run = engine.snapshotRun());
                    Save.flushNativeSave();
                },
                onResume = () => engine.audio.resume(),
                onBack = () =>
                {
                    if (settingsOpen)
                    {
                        CloseSettings();
                        return true;
                    }
                    if (!bookOpen) return false;
                    CloseBook();
                    return true;
                },
            });

            // the music from the first screen: a store app may play at once (no veil). Every later
            // touch or key recovers a track the phone paused.
            RegisterCallback<PointerDownEvent>(e => Touched(), TrickleDown.TrickleDown);
            UiKit.After(this, 0, () =>
            {
                if (!Native.isNative() && !hasBeenActive)
                {
                    asleep = true;
                    dirty = true;
                    return;
                }
                Wake();
                // the platform may still have said no: then wait for a touch after all
                UiKit.After(this, 900, () =>
                {
                    var a = engine.audio;
                    if (!Native.isNative() && a != null && !a.muted && !a.playing) { asleep = true; dirty = true; }
                });
            });

            UiKit.changed += OnMedia;
            BuildStatic();
            Render();
        }

        public SisyphusEngine Engine { get { return engine; } }

        // ------------------------------------------------------------ engine host

        double keepTimer;
        bool hasBeenActive;

        void ResizeEngine()
        {
            engine.hostW = UiKit.viewW;
            engine.hostH = UiKit.viewH;
            engine.hostDpr = UiKit.dpr;
        }

        /// <summary>window "resize"</summary>
        public void OnResize()
        {
            ResizeEngine();
            engine.resize();
        }

        public void Draw(Surface screen, double nowMs)
        {
            engine.frame(nowMs);
        }

        /// <summary>once a frame, before the panel updates: input, timers, re-render</summary>
        public void Tick(double dtMs)
        {
#if UNITY_EDITOR
            if (AutopilotRunner.input.HasValue && engine != null) engine.input = AutopilotRunner.input.Value;
#endif
            PollKeys();
            var before = notes.note;
            notes.tick(dtMs);
            if (notes.note != before) dirty = true;
            keepTimer -= dtMs;
            if (keepTimer <= 0)
            {
                keepTimer += 4000;
                Save.writeSave(d => d.run = engine.snapshotRun());
            }
            bool t = UiKit.Touch;
            bool p = t && UiKit.viewH > UiKit.viewW;
            if (t != touch || p != portrait) { touch = t; portrait = p; dirty = true; }
            if (dirty) Render();
        }

        /// <summary>the app is going away or to the background: keep the climb</summary>
        public void Keep()
        {
            Save.writeSave(d => d.run = engine.snapshotRun());
            Save.flushNativeSave();
        }

        public void Stop()
        {
            UiKit.changed -= OnMedia;
            engine.stop();
        }

        void OnMedia()
        {
            mediaDirty = true;
            dirty = true;
        }

        bool mediaDirty;

        // ------------------------------------------------------------ the engine's state

        void OnState(EngineState s)
        {
            var prev = state;
            state = s;
            dirty = true;
            int foundCount = s.unlocked.Count;
            int prevFound = prev.unlocked != null ? prev.unlocked.Count : 0;

            // Persist progress whenever it moves (a summit or a restart)
            if (started && (s.cycles != prev.cycles || s.bestClimb != prev.bestClimb))
                Save.writeSave(d => { d.cycles = s.cycles; d.bestClimb = s.bestClimb; });

            // every page found is kept, the moment it is found
            if (foundCount != prevFound && foundCount != 0)
            {
                var q = new List<int>(s.unlocked);
                Save.writeSave(d => d.quotes = q);
            }

            // a new page: mark it, and once Zeus is done and the walk down begins, show it
            if ((s.newQuote != prev.newQuote || s.phase != prev.phase) && s.newQuote >= 0 && toasted != s.newQuote && s.phase == "descent")
            {
                toasted = s.newQuote;
                fresh = new HashSet<int>(fresh) { (int)s.newQuote };
                SetCeremony((int)s.newQuote);
            }

            // the first climb's lessons, once seen, are never shown again
            if (started && s.tutorialDone && !prev.tutorialDone) Save.writeSave(d => d.tutorialDone = true);

            // seals at each summit, and the records
            double runId = s.lastRun != null ? s.lastRun.id : -1;
            double prevRunId = prev.lastRun != null ? prev.lastRun.id : -1;
            if (runId != prevRunId && s.lastRun != null)
            {
                var run = s.lastRun;
                if (!hillsRef.Contains(run.levelId))
                {
                    hillsRef = new List<string>(hillsRef) { run.levelId };
                    var hills = new List<string>(hillsRef);
                    Save.writeSave(d => d.hills = hills);
                }
                AwardSeals(run, s.unlocked, run.id + 1);
                // the records: the longest run of perfect heaves, and this week's best climb
                var save = Save.loadSave();
                string wk = Save.weekKey();
                double prevWeek = save.weekKey == wk ? save.weekBest : 0;
                double week = prevWeek > 0 ? Math.Min(prevWeek, run.seconds) : run.seconds;
                double streak = Math.Max(save.bestStreak, run.maxStreak);
                Save.writeSave(d => { d.bestStreak = streak; d.weekKey = wk; d.weekBest = week; });
                recStreak = streak;
                recWeek = week;
            }
            if (foundCount != prevFound && foundCount != 0) AwardSeals(null, s.unlocked, s.cycles);

            // a chapter made whole leaves its keepsake on the hills
            if (foundCount != prevFound || !ReferenceEquals(s.unlocked, prev.unlocked))
            {
                var done = ChaptersWhole(s.unlocked);
                var dd = new List<double>();
                foreach (var c in done) dd.Add(c);
                engine.chaptersDone = dd;
                if (chaptersSeen != null)
                {
                    int fr = -1;
                    foreach (var ch in done) if (!chaptersSeen.Contains(ch)) { fr = ch; break; }
                    foreach (var ch in done) chaptersSeen.Add(ch);
                    if (fr >= 0) chapterToast = fr;
                }
            }
        }

        /// <summary>the chapters every page of which has been found</summary>
        static List<int> ChaptersWhole(IList<int> unlocked)
        {
            int n = Quotes.CHAPTER_COUNT;
            var per = new int[n];
            var have = new int[n];
            var of = Quotes.CHAPTER_OF;
            foreach (var ch in of) per[ch]++;
            foreach (var i in unlocked)
                if (i >= 0 && i < of.Length) have[of[i]]++;
            var r = new List<int>();
            for (int ch = 0; ch < n; ch++) if (per[ch] > 0 && have[ch] >= per[ch]) r.Add(ch);
            return r;
        }

        // seals: worked out at each summit, and whenever the book grows
        void AwardSeals(RunStats run, IList<int> unlocked, double summits)
        {
            var got = Seals.sealsEarned(new SealContext
            {
                summits = summits,
                run = run,
                unlocked = unlocked,
                hills = hillsRef,
                ended = engine.saga.s.ended,
            });
            var fr = new List<string>();
            foreach (var id in got) if (!sealsRef.Contains(id)) fr.Add(id);
            if (fr.Count == 0) return;
            foreach (var id in fr) sealsRef.Add(id);
            seals = new HashSet<string>(sealsRef);
            sealQueue.AddRange(fr);
            sealQueueVer++;
            var all = new List<string>(sealsRef);
            Save.writeSave(d => d.seals = all);
            dirty = true;
        }

        void OnSaga(SagaEvent e)
        {
            // every turn of the story is kept at once
            var st = engine.saga.s;
            Save.writeSave(d => d.saga = st);
            switch (e.type)
            {
                case "talk":
                    {
                        var b = new Block { lines = Dialogue.linesFor(e.@ref, langRef), cine = e.cine };
                        if (e.offer != null) b.offer = e.offer;
                        if (e.choice) b.choice = true;
                        var label = Dialogue.labelFor(e.@ref);
                        if (label != null && e.cine) b.label = label;
                        blocks.Add(b);
                        break;
                    }
                case "took":
                    PushNote(new StoryNote { kind = "took", item = e.item });
                    break;
                case "stage":
                    stage = e.stage;
                    PushNote(new StoryNote { kind = "stage", stage = e.stage });
                    break;
                case "passive":
                    PushNote(new StoryNote { kind = "passive", id = e.id });
                    break;
                case "burden":
                    PushNote(new StoryNote { kind = "burden", id = e.id });
                    break;
                case "wedge":
                    PushNote(new StoryNote { kind = "wedge" });
                    break;
                case "finale":
                    // she walks away once the farewell has been read
                    finaleRef = true;
                    break;
            }
            dirty = true;
        }

        void PushNote(StoryNote n)
        {
            notes.push(n);
            dirty = true;
        }

        // ------------------------------------------------------------ actions

        void SetCeremony(int c)
        {
            ceremony = c;
            dirty = true;
        }

        void ChangePref(string k, bool v)
        {
            if (k == "bigText") bigText = v;
            if (k == "boldRing") boldRing = v;
            if (k == "leftHanded") leftHanded = v;
            Save.writeSave(d =>
            {
                if (k == "bigText") d.bigText = v;
                if (k == "boldRing") d.boldRing = v;
                if (k == "leftHanded") d.leftHanded = v;
            });
            if (k == "bigText") UiKit.SetRootFontSize(v ? 18 : 16);
            if (k == "boldRing") engine.boldRing = v;
            dirty = true;
        }

        void ChangeOwn(string text, string author)
        {
            string tx = Trunc(text.Trim(), 160);
            string au = Trunc(author.Trim(), 40);
            ownText = tx;
            ownAuthor = au;
            Save.writeSave(d => { d.ownText = tx; d.ownAuthor = au; });
            engine.ownQuote = tx != "" ? new Quote { text = tx, author = au != "" ? au : "—" } : null;
            dirty = true;
        }

        static string Trunc(string s, int n) { return s.Length > n ? s.Substring(0, n) : s; }

        void OpenBook()
        {
            engine.input = 0;
            engine.paused = true;
            ceremony = -1;
            bookOpen = true;
            dirty = true;
        }

        void CloseBook()
        {
            engine.input = 0;
            engine.paused = false;
            bookOpen = false;
            fresh = new HashSet<int>();
            dirty = true;
        }

        void OpenSettings()
        {
            engine.input = 0;
            engine.paused = true;
            settingsOpen = true;
            dirty = true;
        }

        void CloseSettings()
        {
            engine.input = 0;
            engine.paused = false;
            settingsOpen = false;
            dirty = true;
        }

        void ToggleCalm()
        {
            bool c = !calm;
            calm = c;
            engine.calm = c;
            Save.writeSave(d => d.calm = c);
            dirty = true;
        }

        void ToggleHaptics()
        {
            bool h = !haptics;
            haptics = h;
            engine.haptics = h;
            Save.writeSave(d => d.haptics = h);
            // a short buzz says it is back on
            if (h) Native.vibrate(25);
            dirty = true;
        }

        // an exchange has been read: the next one; after the farewell, she goes
        void DoneBlock()
        {
            if (blocks.Count > 0) blocks.RemoveAt(0);
            if (blocks.Count == 0 && finaleRef)
            {
                finaleRef = false;
                engine.meropeGone();
            }
            dirty = true;
        }

        void AnswerBlock(bool yes)
        {
            var b = blocks.Count > 0 ? blocks[0] : null;
            if (b != null && b.offer != null) engine.saga.answer(yes);
            else if (b != null && b.choice) engine.saga.choose(yes);
            var st = engine.saga.s;
            Save.writeSave(d => d.saga = st);
            DoneBlock();
        }

        void SetSpeaker(string who) { engine.speaker = who; }

        void FinishPrologue()
        {
            prologue = false;
            engine.saga.s.prologue = true;
            var st = engine.saga.s;
            Save.writeSave(d => d.saga = st);
            dirty = true;
        }

        void Wake()
        {
            var a = engine.audio;
            if (a == null) return;
            a.start();
            a.resume();
        }

        void Touched()
        {
            hasBeenActive = true;
            Wake();
            // the veil goes once the touch is over, so the tap does not fall through onto the menu
            if (asleep) UiKit.After(this, 250, () => { asleep = false; dirty = true; });
        }

        void Begin()
        {
            engine.activate();
            engine.audio.start();
            engine.audio.resume();
            // (a store app is landscape and full screen by its own configuration)
            started = true;
            dirty = true;
        }

        void ToggleMute()
        {
            bool m = !muted;
            muted = m;
            engine.audio.setMuted(m);
            Save.writeSave(d => d.muted = m);
            dirty = true;
        }

        void ChangeLang(string code)
        {
            lang = code;
            langRef = code;
            UiKit.UseLanguage(code);
            engine.setLanguage(code);
            Save.writeSave(d => d.lang = code);
            // the aphorisms come in their own file per language
            Story.loadStory(code);
            quotesReady++;
            Quotes.loadQuotes(code);
            engine.refreshQuotes();
            quotesReady++;
            dirty = true;
        }

        // ------------------------------------------------------------ input

        readonly HashSet<string> keys = new HashSet<string>();
        int pointerId = -1;
        float startX;

        static readonly (KeyCode key, string code)[] KEYMAP =
        {
            (KeyCode.RightArrow, "ArrowRight"), (KeyCode.LeftArrow, "ArrowLeft"), (KeyCode.UpArrow, "ArrowUp"),
            (KeyCode.D, "KeyD"), (KeyCode.A, "KeyA"), (KeyCode.W, "KeyW"), (KeyCode.Space, "Space"),
            (KeyCode.Tab, "Tab"), (KeyCode.B, "KeyB"),
        };

        void PollKeys()
        {
            foreach (var (key, code) in KEYMAP)
            {
                if (Input.GetKeyDown(key)) KeyDown(code);
                if (Input.GetKeyUp(key)) KeyUp(code);
            }
        }

        void ApplyKeys()
        {
            bool right = keys.Contains("ArrowRight") || keys.Contains("KeyD") || keys.Contains("Space") || keys.Contains("KeyW") || keys.Contains("ArrowUp");
            bool left = keys.Contains("ArrowLeft") || keys.Contains("KeyA");
            engine.input = right ? 1 : left ? -0.6 : 0;
#if UNITY_EDITOR
            if (AutopilotRunner.input.HasValue) engine.input = AutopilotRunner.input.Value;
#endif
        }

        void KeyDown(string code)
        {
            Touched();
            // PORT: the web's window listener also saw keys typed into the settings' text boxes
            // (a "b" in your own saying opened the book); here the text box keeps them
            if (UiKit.typing) return;
            // B opens and closes the book on a keyboard
            if (code == "KeyB")
            {
                if (bookOpen) CloseBook();
                else OpenBook();
            }
            if (engine.paused) return;
            keys.Add(code);
            if (code == "Tab") engine.kick();
            ApplyKeys();
        }

        void KeyUp(string code)
        {
            keys.Remove(code);
            ApplyKeys();
        }

        void PDown(PointerDownEvent e)
        {
            pointerId = e.pointerId;
            startX = e.position.x;
            canvasLayer.CapturePointer(e.pointerId);
            engine.input = 0.65;
        }

        void PMove(PointerMoveEvent e)
        {
            if (pointerId != e.pointerId) return;
            double dx = (e.position.x - startX) / 90.0;
            engine.input = Math.Max(-1, Math.Min(1, 0.5 + dx));
        }

        void PEnd<T>(T e) where T : PointerEventBase<T>, new()
        {
            if (pointerId != e.pointerId) return;
            canvasLayer.ReleasePointer(e.pointerId);
            pointerId = -1;
            engine.input = 0;
        }

        // ------------------------------------------------------------ the static tree: HUD, controls

        VisualElement hud, hudLeft, hudText, progressTrack, progressFill, hudRight, bookBtn, bookDot, gearBtn;
        Label bookCount;
        CanvasBox bookGlow;
        UiKit.TweenHandle bookGlowLoop;
        Label cycleLabel;
        VisualElement keyHelp, joystick, knob, throwBtn, hint, descent, menu, portraitEl, wake;
        string hudKey, hintKey, descentKey, keyHelpKey, cycleKey;
        bool joyActive;

        void BuildStatic()
        {
            // ---- HUD
            hud = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
            hud.style.position = Position.Absolute;
            hud.style.left = 0; hud.style.right = 0; hud.style.top = 0;
            hud.style.flexDirection = FlexDirection.Row;
            hud.style.alignItems = Align.FlexStart;
            hud.style.justifyContent = Justify.SpaceBetween;
            baseLayer.Add(hud);
            hudLeft = new VisualElement { pickingMode = PickingMode.Ignore };
            hudLeft.style.flexShrink = 1;
            hud.Add(hudLeft);
            hudText = new VisualElement { pickingMode = PickingMode.Ignore };
            hudText.style.flexDirection = FlexDirection.Row;
            hudText.style.flexWrap = Wrap.Wrap;
            hudLeft.Add(hudText);
            progressTrack = new VisualElement { pickingMode = PickingMode.Ignore };
            progressTrack.style.marginTop = 8;
            progressTrack.style.height = 1;
            progressTrack.style.backgroundColor = UiKit.Border;
            hudLeft.Add(progressTrack);
            progressFill = new VisualElement { pickingMode = PickingMode.Ignore };
            progressFill.style.height = 1;
            progressFill.style.backgroundColor = UiKit.Accent;
            progressFill.style.width = Length.Percent(0);
            progressTrack.Add(progressFill);

            hudRight = new VisualElement { pickingMode = PickingMode.Ignore };
            hudRight.style.flexDirection = FlexDirection.Row;
            hudRight.style.alignItems = Align.Center;
            hudRight.style.justifyContent = Justify.FlexEnd;
            hud.Add(hudRight);

            // the book: relative flex items-center gap-1.5 rounded border border-border/60 bg-background/40 px-2.5 py-1.5
            bookGlow = new CanvasBox((c, w, h) =>
            {
                // book-glow: box-shadow 0 0 14px 2px rgba(217,169,91,0.55) at its height
                double k = glowK;
                if (k <= 0.001) return;
                UiKit.BoxShadow(c, w, h, 4, 0, 0, 14 * k, 2 * k, Col.Rgba(217, 169, 91, 0.55 * k));
            }, 24);
            bookBtn = bookGlow;
            bookBtn.style.flexDirection = FlexDirection.Row;
            bookBtn.style.alignItems = Align.Center;
            UiKit.Radius(bookBtn, 4);
            UiKit.BorderW(bookBtn, 1);
            UiKit.BorderC(bookBtn, UiKit.A(UiKit.Border, 0.6));
            bookBtn.style.backgroundColor = UiKit.A(UiKit.Background, 0.4);
            UiKit.Pad(bookBtn, 10, 6);
            var bi = Icons.Book(UiKit.MutedFg);
            bookBtn.Add(bi);
            bookCount = UiKit.Text("", UiKit.SANS, 400, false, 11.2f, UiKit.MutedFg, 0.15);
            bookCount.style.marginLeft = 6;
            bookBtn.Add(bookCount);
            GX.HoverText(bookBtn, hv => { var col = hv ? UiKit.Foreground : UiKit.MutedFg; bookCount.style.color = col; bi.color = col; });
            GX.OnClick(bookBtn, OpenBook);
            bookDot = new CanvasBox((c, w, h) =>
            {
                c.ShadowColor = Col.Parse("#d9a95b");
                var m = c.GetTransform();
                c.ShadowBlur = 6 * Math.Sqrt(Math.Abs(m.a * m.d));
                c.FillStyle = "#d9a95b";
                c.BeginPath();
                c.Arc(w / 2, h / 2, w / 2, 0, Math.PI * 2);
                c.Fill();
            }, 8) { pickingMode = PickingMode.Ignore };
            bookDot.style.position = Position.Absolute;
            bookDot.style.top = -4 - 1;
            bookDot.style.right = -4 - 1;
            bookDot.style.width = 8;
            bookDot.style.height = 8;
            bookBtn.Add(bookDot);
            hudRight.Add(bookBtn);

            gearBtn = IconButton(30, Icons.Gear(UiKit.MutedFg), OpenSettings);
            gearBtn.style.marginLeft = 8;
            hudRight.Add(gearBtn);

            // ---- cycle, bottom centre
            cycleLabel = UiKit.Text("", UiKit.SANS, 400, false, UiKit.Rem(0.65), UiKit.A(UiKit.MutedFg, 0.7), 0.3);
            var cycleWrap = new VisualElement { pickingMode = PickingMode.Ignore };
            cycleWrap.style.position = Position.Absolute;
            cycleWrap.style.left = 0; cycleWrap.style.right = 0; cycleWrap.style.bottom = 20;
            cycleWrap.style.alignItems = Align.Center;
            cycleWrap.Add(cycleLabel);
            baseLayer.Add(cycleWrap);

            // ---- the key list (no touch screen)
            keyHelp = new VisualElement { pickingMode = PickingMode.Ignore };
            keyHelp.style.position = Position.Absolute;
            keyHelp.style.bottom = 20;
            keyHelp.style.left = 16;
            baseLayer.Add(keyHelp);

            // ---- the joystick
            joystick = new VisualElement { name = "joystick", pickingMode = PickingMode.Position };
            joystick.style.position = Position.Absolute;
            joystick.style.bottom = 24;
            joystick.style.width = 96;
            joystick.style.height = 96;
            joystick.style.alignItems = Align.Center;
            joystick.style.justifyContent = Justify.Center;
            UiKit.Radius(joystick, 48);
            UiKit.BorderW(joystick, 1);
            UiKit.BorderC(joystick, UiKit.A(UiKit.Border, 0.6));
            joystick.style.backgroundColor = UiKit.A(UiKit.Background, 0.3);
            knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.style.width = 40;
            knob.style.height = 40;
            UiKit.Radius(knob, 20);
            UiKit.BorderW(knob, 1);
            UiKit.BorderC(knob, UiKit.Border);
            knob.style.backgroundColor = UiKit.A(UiKit.Accent, 0.7);
            knob.usageHints = UsageHints.DynamicTransform;
            joystick.Add(knob);
            joystick.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                joystick.CapturePointer(e.pointerId);
                joyActive = true;
                JoySet(e.position);
            });
            joystick.RegisterCallback<PointerMoveEvent>(e => { if (joyActive) JoySet(e.position); });
            joystick.RegisterCallback<PointerUpEvent>(e => { joystick.ReleasePointer(e.pointerId); JoyUp(); });
            joystick.RegisterCallback<PointerCancelEvent>(e => JoyUp());
            baseLayer.Add(joystick);

            // ---- the throw button
            throwBtn = new VisualElement { name = "throw", pickingMode = PickingMode.Position };
            throwBtn.style.position = Position.Absolute;
            throwBtn.style.bottom = 24;
            throwBtn.style.width = 64;
            throwBtn.style.height = 64;
            throwBtn.style.alignItems = Align.Center;
            throwBtn.style.justifyContent = Justify.Center;
            UiKit.Radius(throwBtn, 32);
            UiKit.BorderW(throwBtn, 1);
            UiKit.BorderC(throwBtn, UiKit.Border);
            throwBtn.style.backgroundColor = UiKit.A(UiKit.Background, 0.5);
            throwBtn.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                engine.kick();
                // active:scale-95 active:bg-accent
                throwBtn.style.scale = new Scale(new Vector2(0.95f, 0.95f));
                throwBtn.style.backgroundColor = UiKit.Accent;
                throwBtn.CapturePointer(e.pointerId);
            });
            void ThrowUp(int id)
            {
                throwBtn.ReleasePointer(id);
                throwBtn.style.scale = new Scale(new Vector2(1, 1));
                throwBtn.style.backgroundColor = UiKit.A(UiKit.Background, 0.5);
            }
            throwBtn.RegisterCallback<PointerUpEvent>(e => ThrowUp(e.pointerId));
            throwBtn.RegisterCallback<PointerCancelEvent>(e => ThrowUp(e.pointerId));
            baseLayer.Add(throwBtn);

            // ---- the hint
            hint = new VisualElement { pickingMode = PickingMode.Ignore };
            hint.style.position = Position.Absolute;
            hint.style.left = 0; hint.style.right = 0;
            hint.style.top = Length.Percent(18);
            hint.style.alignItems = Align.Center;
            baseLayer.Add(hint);

            // ---- the walk down
            descent = new VisualElement { pickingMode = PickingMode.Ignore };
            descent.style.position = Position.Absolute;
            descent.style.left = 0; descent.style.right = 0; descent.style.bottom = 56;
            descent.style.alignItems = Align.Center;
            descent.style.paddingLeft = 24; descent.style.paddingRight = 24;
            baseLayer.Add(descent);

            if (!started) BuildMenu();
        }

        VisualElement IconButton(float size, SvgIcon icon, Action onClick)
        {
            // flex items-center justify-center rounded border border-border/60 bg-background/40 text-muted-foreground
            var b = new VisualElement();
            b.style.width = size;
            b.style.height = size;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            UiKit.Radius(b, 4);
            UiKit.BorderW(b, 1);
            UiKit.BorderC(b, UiKit.A(UiKit.Border, 0.6));
            b.style.backgroundColor = UiKit.A(UiKit.Background, 0.4);
            b.Add(icon);
            GX.HoverText(b, hv => icon.color = hv ? UiKit.Foreground : UiKit.MutedFg);
            GX.OnClick(b, onClick);
            return b;
        }

        double glowK;

        void JoySet(Vector3 pos)
        {
            var r = joystick.worldBound;
            float dx = pos.x - (r.x + r.width / 2);
            float dy = pos.y - (r.y + r.height / 2);
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dist > JOY_RADIUS)
            {
                dx = dx / dist * JOY_RADIUS;
                dy = dy / dist * JOY_RADIUS;
            }
            knob.style.translate = new Translate(dx, dy);
            engine.input = dx / JOY_RADIUS;
        }

        void JoyUp()
        {
            joyActive = false;
            engine.input = 0;
            knob.style.translate = new Translate(0, 0);
        }

        // ------------------------------------------------------------ the start card

        VisualElement menuCol, menuEmblem, menuTitle, menuLine, menuP1, menuP2, menuDailyWrap, menuCycle, menuStart, menuBookBtn, menuTopRight, menuPrivacy;
        Label menuP1Label, menuP2Label, menuCycleLabel, menuStartLabel, menuBookTitle, menuBookCount, menuPrivacyLabel;
        readonly List<Label> titleLetters = new List<Label>();
        LanguageSelect menuSelect;
        CanvasBox menuStartBox, menuSweep;
        double startGlow, sweepPos = 1.2, titleShadowBlur, titleShadowA;
        string menuDailyKey, menuTextKey;
        float lineW;

        void BuildMenu()
        {
            menu = new VisualElement { name = "menu", pickingMode = PickingMode.Position };
            UiKit.Fill(menu);
            menu.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            baseLayer.Add(menu);

            // .menu-veil: radial over linear, fading in over 1.2s
            var veil = new CanvasBox((c, w, h) =>
            {
                var lg = c.CreateLinearGradient(0, 0, 0, h);
                lg.AddColorStop(0, "rgba(0,0,0,0.3)");
                lg.AddColorStop(1, "rgba(0,0,0,0.55)");
                c.FillStyle = lg;
                c.FillRect(0, 0, w, h);
                UiKit.CssRadial(c, w, h, false, w * 0.5, h * 0.45, (0, Col.Rgba(8, 8, 14, 0.62)), (0.7, Col.Rgba(6, 6, 10, 0.88)), (1, Col.Rgba(6, 6, 10, 0.88)));
            }) { pickingMode = PickingMode.Ignore };
            UiKit.Fill(veil);
            menu.Add(veil);
            UiKit.Tween(menu, 1200, 0, UiKit.EaseOut, k => menu.style.opacity = (float)k);

            // embers rising through the menu
            var embers = new VisualElement { pickingMode = PickingMode.Ignore };
            UiKit.Fill(embers);
            embers.style.overflow = Overflow.Hidden;
            menu.Add(embers);
            for (int i = 0; i < 16; i++)
            {
                var em = new CanvasBox((c, w, h) =>
                {
                    c.ShadowColor = Col.Rgba(255, 140, 60, 0.8);
                    var m = c.GetTransform();
                    c.ShadowBlur = 6 * Math.Sqrt(Math.Abs(m.a * m.d));
                    c.FillStyle = Col.Rgba(255, 170, 90, 0.8);
                    c.BeginPath();
                    c.Arc(w / 2, h / 2, w / 2, 0, Math.PI * 2);
                    c.Fill();
                }, 8) { pickingMode = PickingMode.Ignore };
                em.style.position = Position.Absolute;
                em.style.bottom = -10;
                em.style.left = Length.Percent((i * 61) % 100);
                em.style.width = 3;
                em.style.height = 3;
                em.style.opacity = 0;
                em.usageHints = UsageHints.DynamicTransform;
                embers.Add(em);
                double delay = (i * 0.83) % 9 * 1000;
                double dur = (8 + ((i * 7) % 6)) * 1000;
                UiKit.Loop(em, dur, delay, ph =>
                {
                    em.style.translate = new Translate((float)(30 * ph), (float)(-UiKit.Vh(105) * ph));
                    em.style.opacity = (float)(ph < 0.15 ? 0.8 * ph / 0.15 : 0.8 * (1 - (ph - 0.15) / 0.85));
                });
            }

            menuCol = new VisualElement { pickingMode = PickingMode.Ignore };
            UiKit.Fill(menuCol);
            menuCol.style.flexDirection = FlexDirection.Column;
            menuCol.style.alignItems = Align.Center;
            menuCol.style.justifyContent = Justify.Center;
            menuCol.style.overflow = Overflow.Hidden;
            UiKit.Pad(menuCol, 24, 16);
            menu.Add(menuCol);

            // top right: the language and the cog
            menuTopRight = new VisualElement();
            menuTopRight.style.position = Position.Absolute;
            menuTopRight.style.flexDirection = FlexDirection.Row;
            menuTopRight.style.alignItems = Align.Center;
            menu.Add(menuTopRight);

            // the emblem (the hill in miniature)
            menuEmblem = new HillEmblemView();
            menuEmblem.style.width = 230;
            menuEmblem.style.height = 110;
            menuEmblem.style.flexShrink = 0;
            MenuRise(menuEmblem, 1100, 0);
            menuCol.Add(menuEmblem);

            // SISYPHUS, a letter at a time
            menuTitle = new VisualElement { pickingMode = PickingMode.Ignore };
            menuTitle.style.flexDirection = FlexDirection.Row;
            menuTitle.style.flexShrink = 0;
            string word = "SISYPHUS";
            var letterEase = UiKit.Soft;
            for (int i = 0; i < word.Length; i++)
            {
                var l = UiKit.Text(word[i].ToString(), UiKit.CINZEL, 700, false, 36, UiKit.C("#f1e4c4"), 0, null, false, 0);
                l.style.flexShrink = 0;
                titleLetters.Add(l);
                menuTitle.Add(l);
                // prologue-letter: 1.6s cubic-bezier(0.2, 0.7, 0.2, 1), 150 + i·90 ms
                UiKit.Tween(l, 1600, 150 + i * 90, UiKit.Linear, t =>
                {
                    double op = UiKit.Seg(t, letterEase, (0, 0), (0.6, 1), (1, 1));
                    double tr = letterEase(t);
                    double blur = UiKit.Seg(t, letterEase, (0, 0), (0.6, 32), (1, 18));
                    double a = UiKit.Seg(t, letterEase, (0, 0), (0.6, 0.9), (1, 0.35));
                    float size = l.style.fontSize.value.value;
                    l.style.opacity = (float)op;
                    UiKit.Xf(l, 0, (float)(-0.2 * size * (1 - tr)), (float)(1.25 - 0.25 * tr));
                    l.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = (float)blur, color = new Color(217 / 255f, 180 / 255f, 106 / 255f, (float)a) };
                });
            }
            menuCol.Add(menuTitle);

            // the gold rule under the name
            menuLine = new VisualElement { pickingMode = PickingMode.Ignore };
            menuLine.style.height = 1;
            menuLine.style.flexShrink = 0;
            menuLine.style.backgroundColor = UiKit.C("#d9b46a");
            UiKit.Tween(menuLine, 1800, 1100, UiKit.Soft, k =>
            {
                lineW = (float)k;
                menuLine.style.width = (float)(Math.Min(UiKit.Rem(22), UiKit.Vw(60)) * k);
                menuLine.style.opacity = (float)(0.8 * k);
            });
            menuCol.Add(menuLine);

            menuP1 = new VisualElement { pickingMode = PickingMode.Ignore };
            menuP1Label = new Label { pickingMode = PickingMode.Ignore };
            UiKit.Reset(menuP1Label);
            menuP1.Add(menuP1Label);
            MenuRise(menuP1, 1100, 900);
            menuCol.Add(menuP1);

            menuP2 = new VisualElement { pickingMode = PickingMode.Ignore };
            menuP2Label = new Label { pickingMode = PickingMode.Ignore };
            UiKit.Reset(menuP2Label);
            menuP2.Add(menuP2Label);
            MenuRise(menuP2, 1100, 1050);
            menuCol.Add(menuP2);

            menuDailyWrap = new VisualElement();
            menuDailyWrap.style.flexShrink = 0;
            MenuRise(menuDailyWrap, 1100, 1250);
            menuCol.Add(menuDailyWrap);

            menuCycle = new VisualElement { pickingMode = PickingMode.Ignore };
            menuCycleLabel = new Label { pickingMode = PickingMode.Ignore };
            UiKit.Reset(menuCycleLabel);
            menuCycle.Add(menuCycleLabel);
            menuCol.Add(menuCycle);

            // .menu-start: Cinzel, a gold rim, rising then glowing
            menuStartBox = new CanvasBox((c, w, h) =>
            {
                double k = startGlow;
                if (k > 0.001) UiKit.BoxShadow(c, w, h, h / 2, 0, 0, 26, 0, Col.Rgba(217, 180, 106, 0.35 * k));
            }, 40);
            menuStart = menuStartBox;
            menuStart.style.flexShrink = 0;
            UiKit.Radius(menuStart, 9999);
            UiKit.BorderW(menuStart, 1);
            UiKit.BorderC(menuStart, UiKit.C("rgba(217,180,106,0.6)"));
            GX.Hover(menuStart, new Color(0, 0, 0, 0.3f), UiKit.C("rgba(217,180,106,0.16)"));
            menuStartLabel = new Label { pickingMode = PickingMode.Ignore };
            UiKit.Reset(menuStartLabel);
            menuStart.Add(menuStartLabel);
            GX.OnClick(menuStart, Begin);
            MenuRise(menuStart, 1000, 1450);
            UiKit.Loop(menuStart, 3000, 2600, ph =>
            {
                double k = UiKit.EaseInOut(ph < 0.5 ? ph * 2 : (1 - ph) * 2);
                startGlow = k;
                menuStartBox.Redraw();
            });
            menuCol.Add(menuStart);

            // the book, under the start
            menuBookBtn = new VisualElement();
            menuBookBtn.style.flexDirection = FlexDirection.Row;
            menuBookBtn.style.alignItems = Align.Center;
            menuBookBtn.style.flexShrink = 0;
            UiKit.Radius(menuBookBtn, 9999);
            UiKit.Pad(menuBookBtn, 20, 8);
            var mbi = Icons.Book(UiKit.MutedFg);
            menuBookBtn.Add(mbi);
            menuBookTitle = new Label { pickingMode = PickingMode.Ignore };
            UiKit.Reset(menuBookTitle);
            menuBookTitle.style.marginLeft = 8;
            menuBookBtn.Add(menuBookTitle);
            menuBookCount = new Label { pickingMode = PickingMode.Ignore };
            UiKit.Reset(menuBookCount);
            menuBookCount.style.marginLeft = 8;
            menuBookCount.style.opacity = 0.7f;
            menuBookBtn.Add(menuBookCount);
            GX.HoverText(menuBookBtn, hv =>
            {
                var col = hv ? UiKit.Foreground : UiKit.MutedFg;
                mbi.color = col;
                menuBookTitle.style.color = col;
                menuBookCount.style.color = col;
            });
            GX.OnClick(menuBookBtn, OpenBook);
            MenuRise(menuBookBtn, 1100, 1600);
            menuCol.Add(menuBookBtn);

            // privacy, bottom right
            menuPrivacy = new VisualElement();
            menuPrivacy.style.position = Position.Absolute;
            menuPrivacyLabel = new Label { pickingMode = PickingMode.Ignore };
            UiKit.Reset(menuPrivacyLabel);
            menuPrivacy.Add(menuPrivacyLabel);
            GX.HoverText(menuPrivacy, hv => menuPrivacyLabel.style.color = hv ? UiKit.Foreground : UiKit.A(UiKit.MutedFg, 0.6));
            GX.OnClick(menuPrivacy, Privacy.Open);
            menu.Add(menuPrivacy);

            menuTextKey = null;
            menuDailyKey = null;
            mediaDirty = true;
        }

        /// <summary>.menu-rise: up 14px out of a blur (the blur is dropped), 1.1s cubic-bezier(0.2, 0.7, 0.2, 1)</summary>
        static void MenuRise(VisualElement e, double dur, double delay)
        {
            UiKit.Tween(e, dur, delay, UiKit.Soft, k =>
            {
                e.style.opacity = (float)k;
                e.style.translate = new Translate(0, (float)(14 * (1 - k)));
            });
        }

        static string Hex(Color c)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255), g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255), b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255), a = Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255);
            return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2") + a.ToString("X2");
        }

        static string Esc(string s) { return s.IndexOf('<') >= 0 ? s.Replace("<", "<noparse><</noparse>") : s; }

        static void Style(Label l, string family, int weight, bool italic, float size, Color color, double tracking, string lang, double lh)
        {
            UiKit.SetFont(l, family, weight, italic);
            l.style.fontSize = size;
            l.style.color = color;
            UiKit.Tracking(l, tracking, lang);
            UiKit.Leading(l, family, size, lh);
        }

        /// <summary>the start card's sizes, from the media queries ([@media(max-height:…)], sm:)</summary>
        void LayoutMenu(UIStrings t)
        {
            bool low = UiKit.MaxHeight(500), mid = UiKit.MaxHeight(760), sm = UiKit.Sm;
            float gap = low ? 10 : 24;

            menuTopRight.style.top = 16 + UiKit.safeTop;
            menuTopRight.style.right = 16 + UiKit.safeRight;
            menuPrivacy.style.right = 16 + UiKit.safeRight;
            menuPrivacy.style.bottom = 12 + UiKit.safeBottom;

            menuEmblem.style.display = mid ? DisplayStyle.None : DisplayStyle.Flex;
            menuLine.style.display = low ? DisplayStyle.None : DisplayStyle.Flex;
            menuLine.style.width = (float)(Math.Min(UiKit.Rem(22), UiKit.Vw(60)) * lineW);

            // h1: text-4xl, sm:text-7xl, max-height 500: text-3xl
            float ts = low ? UiKit.Rem(1.875) : sm ? UiKit.Rem(4.5) : UiKit.Rem(2.25);
            double tlh = low ? 1.2 : sm ? 1.0 : 1.111;
            foreach (var l in titleLetters)
            {
                l.style.fontSize = ts;
                UiKit.SetSpacing(l, (float)(0.3 * ts));
            }
            menuTitle.style.marginRight = (float)(-0.3 * ts);
            float titleTrim = (float)((tlh - UiKit.NaturalLine(UiKit.CINZEL)) * ts / 2);

            // the instructions
            float p1s = low ? UiKit.Rem(0.75) : UiKit.Rem(0.875);
            Style(menuP1Label, UiKit.SANS, 400, false, p1s, UiKit.MutedFg, 0, lang, 1.625);
            menuP1Label.style.unityTextAlign = TextAnchor.UpperCenter;
            menuP1.style.maxWidth = low ? UiKit.Rem(42) : UiKit.Rem(24);
            float p2s = low ? UiKit.Rem(0.68) : UiKit.Rem(0.75);
            Style(menuP2Label, UiKit.SANS, 400, false, p2s, UiKit.A(UiKit.MutedFg, 0.8), 0, lang, 1.625);
            menuP2Label.style.unityTextAlign = TextAnchor.UpperCenter;
            menuP2.style.maxWidth = low ? UiKit.Rem(42) : UiKit.Rem(28);

            Style(menuCycleLabel, UiKit.SANS, 400, false, UiKit.Rem(0.65), UiKit.MutedFg, 0.3, lang, 1.5);
            menuCycleLabel.style.unityTextAlign = TextAnchor.UpperCenter;

            // the start: px-10 py-3 text-xs tracking-[0.4em]
            UiKit.Pad(menuStart, 40, 12);
            Style(menuStartLabel, UiKit.CINZEL, 400, false, UiKit.Rem(0.75), UiKit.Foreground, 0.4, lang, 1.0 / 0.75);
            Style(menuBookTitle, UiKit.SANS, 400, false, UiKit.Rem(0.65), UiKit.MutedFg, 0.3, lang, 1.5);
            Style(menuBookCount, UiKit.SANS, 400, false, UiKit.Rem(0.65), UiKit.MutedFg, 0.3, lang, 1.5);
            Style(menuPrivacyLabel, UiKit.SANS, 400, false, UiKit.Rem(0.6), UiKit.A(UiKit.MutedFg, 0.6), 0.25, lang, 1.5);

            // the column's gap, with each child's own margin on top of it
            var order = new[] { menuEmblem, menuTitle, menuLine, menuP1, menuP2, menuDailyWrap, menuCycle, menuStart, menuBookBtn };
            bool first = true;
            foreach (var e in order)
            {
                if (e.style.display.value == DisplayStyle.None) continue;
                float own = 0;
                if (e == menuTitle) own = mid ? 0 : -16;
                if (e == menuLine) own = -12;
                if (e == menuStart) own = 8;
                if (e == menuBookBtn) own = -8;
                float extraTop = e == menuTitle ? titleTrim : 0;
                e.style.marginTop = (first ? 0 : gap) + own + extraTop;
                e.style.marginBottom = e == menuTitle ? titleTrim : 0;
                first = false;
            }
        }

        void SyncMenu(UIStrings t)
        {
            if (menu == null) return;
            string key = lang + "|" + touch + "|" + state.cycles + "|" + state.bestClimb + "|" + state.unlocked.Count + "|" + UiKit.rootFontSize;
            if (mediaDirty || key != menuTextKey)
            {
                LayoutMenu(t);
                // the instructions, with the keys set in the foreground colour
                var sb = new StringBuilder();
                string fg = Hex(UiKit.Foreground);
                string src = touch ? t.instructionsTouch : t.instructions;
                int i = 0;
                while (i < src.Length)
                {
                    int j = src.IndexOf("{k", i, StringComparison.Ordinal);
                    if (j < 0 || j + 3 >= src.Length || src[j + 3] != '}' || src[j + 2] < '1' || src[j + 2] > '3')
                    {
                        sb.Append(Esc(j < 0 ? src.Substring(i) : src.Substring(i, j + 2 - i)));
                        if (j < 0) break;
                        i = j + 2;
                        continue;
                    }
                    sb.Append(Esc(src.Substring(i, j - i)));
                    string k = src[j + 2] == '1' ? "space / → / D" : src[j + 2] == '2' ? "Tab" : t.throwBtn;
                    sb.Append("<color=").Append(fg).Append('>').Append(Esc(k)).Append("</color>");
                    i = j + 4;
                }
                float p1s = menuP1Label.style.fontSize.value.value;
                menuP1Label.enableRichText = true;
                menuP1Label.text = "<line-height=" + (1.625).ToString(CultureInfo.InvariantCulture) + "em>" + sb;
                UiKit.SetText(menuP2Label, t.instructions2, 1.625);

                menuCycle.style.display = state.cycles > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (state.cycles > 0)
                {
                    string s = UiKit.Upper(t.cycle, lang) + " " + Js.str(state.cycles + 1);
                    if (state.bestClimb > 0)
                        s += "<alpha=#80><space=8px>·<space=8px><alpha=#FF>" + UiKit.Upper(t.bestClimb, lang) + " " + Save.formatTime(state.bestClimb);
                    menuCycleLabel.enableRichText = true;
                    menuCycleLabel.text = s;
                }
                UiKit.SetText(menuStartLabel, UiKit.Upper(t.startBtn, lang), 1.333);
                UiKit.SetText(menuBookTitle, UiKit.Upper(t.bookTitle, lang), 1.5);
                UiKit.SetText(menuBookCount, state.unlocked.Count + "/" + TOTAL_PAGES, 1.5);
                UiKit.SetText(menuPrivacyLabel, UiKit.Upper(t.privacy, lang), 1.5);

                if (menuSelect == null)
                {
                    menuSelect = new LanguageSelect(lang, ChangeLang, false);
                    menuTopRight.Add(menuSelect);
                    var g = IconButton(28, Icons.Gear(UiKit.MutedFg), OpenSettings);
                    g.style.marginLeft = 8;
                    menuTopRight.Add(g);
                }
                else menuSelect.SetLang(lang);
                menuTextKey = key;
            }

            // the saying of the day (re-rendered in place, as React does)
            string dkey = lang + "|" + quotesReady + "|" + dailyAdded + "|" + UiKit.rootFontSize + "|" + UiKit.MaxHeight(500);
            if (dkey != menuDailyKey)
            {
                menuDailyWrap.Clear();
                menuDailyWrap.style.display = quotesReady > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (quotesReady > 0)
                {
                    var dq = new DailyQuote(daily, lang, I18n.T[lang], dailyAdded);
                    menuDailyWrap.Add(dq);
                    if (dq.card != null && menuDailyKey == null) Sweep(dq.card);
                }
                menuDailyKey = dkey;
            }
        }

        /// <summary>.menu-card::after: a light passing over the card once it has arrived (2.2s, 1.6s ease-in-out)</summary>
        void Sweep(CanvasBox card)
        {
            menuSweep = new CanvasBox((c, w, h) =>
            {
                // background: linear-gradient(105deg, transparent 35%, rgba(255,245,220,0.35) 50%, transparent 65%),
                // 250% wide, at background-position sweepPos (120% → −40%).
                // PORT: mix-blend-mode: soft-light becomes a plain, fainter light.
                double bw = w * 2.5;
                double x0 = (w - bw) * sweepPos;
                c.Save();
                c.Translate(x0, 0);
                UiKit.CssLinear(105, bw, h, out double ax, out double ay, out double bx, out double by);
                var g = c.CreateLinearGradient(ax, ay, bx, by);
                g.AddColorStop(0.35, Col.Rgba(255, 245, 220, 0));
                g.AddColorStop(0.5, Col.Rgba(255, 245, 220, 0.18));
                g.AddColorStop(0.65, Col.Rgba(255, 245, 220, 0));
                c.FillStyle = g;
                c.FillRect(-x0, 0, w, h);
                c.Restore();
            }) { pickingMode = PickingMode.Ignore };
            UiKit.Fill(menuSweep);
            card.Add(menuSweep);
            UiKit.Tween(menuSweep, 1600, 2200, UiKit.EaseInOut, k =>
            {
                sweepPos = 1.2 + (-0.4 - 1.2) * k;
                menuSweep.Redraw();
            });
        }

        // ------------------------------------------------------------ render

        UiKit.TweenHandle chapterTimer, sealTimer;
        string chapterTimerKey, sealTimerKey;
        string sceneKey, noteKey;
        VisualElement sceneEl;
        SceneView sceneView;
        SubtitleView subtitleView;
        int ceremonyShown = -2;
        string sealShown, endShownKey;
        int chapterShown = -2;
        EpiphanyView epiphanyView;
        EndCardView endView;
        PrologueView prologueView;
        SettingsPanel settingsPanel;
        WisdomBookView book;

        void Render()
        {
            dirty = false;
            var t = I18n.T[lang];
            int foundCount = state.unlocked.Count;

            // ---- effects that hang on state other than the engine's
            // a chapter toast leaves after 5.2s, once nothing else is on screen
            string ck = chapterToast + "|" + ceremony + "|" + sealQueue.Count;
            if (ck != chapterTimerKey)
            {
                chapterTimerKey = ck;
                chapterTimer?.Stop();
                chapterTimer = null;
                if (chapterToast >= 0 && ceremony < 0 && sealQueue.Count == 0)
                    chapterTimer = UiKit.After(this, 5200, () => { chapterToast = -1; dirty = true; });
            }
            // one seal on screen at a time, a few seconds each
            string sk = sealQueueVer + "|" + sealQueue.Count + "|" + ceremony + "|" + epiphany;
            if (sk != sealTimerKey)
            {
                sealTimerKey = sk;
                sealTimer?.Stop();
                sealTimer = null;
                if (sealQueue.Count > 0 && ceremony < 0 && !epiphany)
                    sealTimer = UiKit.After(this, 4200, () => { if (sealQueue.Count > 0) sealQueue.RemoveAt(0); sealQueueVer++; dirty = true; });
            }
            // a scene stops the climb while it plays
            bool sceneOn = blocks.Count > 0 && blocks[0].cine && ceremony < 0;
            engine.cutscene = sceneOn;
            // nothing else in the frame while a scene plays: no saying in the sky
            engine.showAphorism = !sceneOn && ceremony < 0;
            if (mediaDirty) OnResize();

            // ---- HUD
            float pad = UiKit.Sm ? 24 : 16;
            hud.style.paddingLeft = pad + UiKit.safeLeft;
            hud.style.paddingRight = pad + UiKit.safeRight;
            hud.style.paddingTop = pad + UiKit.safeTop;
            string hk = lang + "|" + stage + "|" + state.levelName + "|" + state.weather + "|" + UiKit.rootFontSize + "|" + UiKit.Sm;
            if (hk != hudKey)
            {
                hudKey = hk;
                hudText.Clear();
                float fs = UiKit.Rem(0.7);
                var story = Journey.STORY[lang];
                int si = Math.Max(0, Math.Min(2, stage - 1));
                var a = UiKit.Text(story.act + " " + Journey.ROMAN[si] + " · " + Journey.STAGE_TEXT[lang][si].name, UiKit.SANS, 400, false, fs, UiKit.A(UiKit.Foreground, 0.7), 0.35, lang, true);
                a.style.marginRight = 8;
                hudText.Add(a);
                hudText.Add(UiKit.Text("·", UiKit.SANS, 400, false, fs, UiKit.A(UiKit.MutedFg, 0.5), 0.35, lang));
                hudText.Add(UiKit.Text(" " + state.levelName, UiKit.SANS, 400, false, fs, UiKit.MutedFg, 0.35, lang, true));
                if (state.weather != "clear")
                {
                    var w = UiKit.Text("· " + (state.weather == "rain" ? t.weatherRain : t.weatherStorm), UiKit.SANS, 400, false, fs, UiKit.A(UiKit.MutedFg, 0.7), 0.25, lang, true);
                    w.style.marginLeft = 8;
                    hudText.Add(w);
                }
                progressTrack.style.width = UiKit.Sm ? 192 : 128;
                bookCount.style.fontSize = fs;
                UiKit.Tracking(bookCount, 0.15, lang);
                cycleLabel.style.fontSize = UiKit.Rem(0.65);
                UiKit.Tracking(cycleLabel, 0.3, lang);
            }
            progressFill.style.width = Length.Percent((float)(state.progress * 100));
            UiKit.SetText(bookCount, foundCount + "/" + TOTAL_PAGES, 1.5);
            bool glow = fresh.Count > 0;
            bookDot.style.display = glow ? DisplayStyle.Flex : DisplayStyle.None;
            if (glow && bookGlowLoop == null)
                bookGlowLoop = UiKit.Loop(bookBtn, 2200, 0, ph => { glowK = UiKit.EaseInOut(ph < 0.5 ? ph * 2 : (1 - ph) * 2); bookGlow.Redraw(); });
            else if (!glow && bookGlowLoop != null) { bookGlowLoop.Stop(); bookGlowLoop = null; glowK = 0; bookGlow.Redraw(); }

            string cyk = lang + "|" + state.cycles;
            if (cyk != cycleKey) { cycleKey = cyk; UiKit.SetText(cycleLabel, UiKit.Upper(t.cycle, lang) + " " + Js.str(state.cycles + 1), 1.5); }
            cycleLabel.parent.style.bottom = 20 + UiKit.safeBottom;

            // ---- key list (no touch screen)
            bool playing = state.phase == "playing" || state.phase == "rolling";
            keyHelp.style.display = !touch && playing ? DisplayStyle.Flex : DisplayStyle.None;
            keyHelp.style.left = 16 + UiKit.safeLeft;
            keyHelp.style.bottom = 20 + UiKit.safeBottom;
            string kk = lang + "|" + UiKit.rootFontSize;
            if (!touch && playing && kk != keyHelpKey) { keyHelpKey = kk; BuildKeyHelp(t); }

            // ---- joystick and throw
            bool joyOn = touch && (playing || state.phase == "descent");
            joystick.style.display = joyOn ? DisplayStyle.Flex : DisplayStyle.None;
            if (!joyOn && joyActive) JoyUp();
            joystick.style.left = leftHanded ? StyleKeyword.Auto : (StyleLength)(16 + UiKit.safeLeft);
            joystick.style.right = leftHanded ? (StyleLength)(16 + UiKit.safeRight) : StyleKeyword.Auto;
            joystick.style.bottom = 24 + UiKit.safeBottom;
            throwBtn.style.display = touch && playing ? DisplayStyle.Flex : DisplayStyle.None;
            throwBtn.style.left = leftHanded ? (StyleLength)(16 + UiKit.safeLeft) : StyleKeyword.Auto;
            throwBtn.style.right = leftHanded ? StyleKeyword.Auto : (StyleLength)(16 + UiKit.safeRight);
            throwBtn.style.bottom = 24 + UiKit.safeBottom;
            string thk = lang + "|" + UiKit.rootFontSize;
            if ((string)throwBtn.userData != thk)
            {
                throwBtn.userData = thk;
                throwBtn.Clear();
                throwBtn.Add(UiKit.Text(t.throwBtn, UiKit.SANS, 400, false, UiKit.Rem(0.65), UiKit.Foreground, 0.15, lang, true));
            }

            // ---- the hint (keyed: a new hint is a new element)
            string hik = state.hint == null ? null : state.hint + "|" + lang + "|" + touch + "|" + UiKit.rootFontSize;
            if (hik != hintKey)
            {
                hintKey = hik;
                hint.Clear();
                if (state.hint != null)
                {
                    string s = state.hint == "kick" ? t.hintKick.Replace("{k}", touch ? t.throwBtn : "Tab")
                        : state.hint == "push" ? (touch ? t.hintPushTouch : t.hintPush)
                        : state.hint == "rhythm" ? t.hintRhythm
                        : state.hint == "breathe" ? t.hintBreathe
                        : t.hintRest;
                    var pill = new VisualElement { pickingMode = PickingMode.Ignore };
                    pill.style.maxWidth = UiKit.Vw(80);
                    UiKit.Radius(pill, 9999);
                    pill.style.backgroundColor = UiKit.A(UiKit.Background, 0.45);
                    UiKit.Pad(pill, 16, 6);
                    var l = UiKit.Text(s, UiKit.SANS, 400, false, UiKit.Rem(0.7), UiKit.A(UiKit.Foreground, 0.9), 0.2, lang, true);
                    l.style.unityTextAlign = TextAnchor.UpperCenter;
                    pill.Add(l);
                    hint.Add(pill);
                }
            }

            // ---- the walk down
            string dk = state.phase == "descent" ? lang + "|" + touch + "|" + state.lastClimb + "|" + state.bestClimb + "|" + state.newBest + "|" + UiKit.rootFontSize : null;
            if (dk != descentKey)
            {
                descentKey = dk;
                descent.Clear();
                if (dk != null)
                {
                    var a = UiKit.Text(touch ? t.walkHintTouch : t.walkHint, UiKit.SANS, 400, false, UiKit.Rem(0.7), UiKit.A(UiKit.Foreground, 0.85), 0.3, lang, true);
                    a.style.unityTextAlign = TextAnchor.UpperCenter;
                    descent.Add(a);
                    if (state.lastClimb > 0)
                    {
                        string s = Esc(UiKit.Upper(t.thisClimb, lang)) + " " + Save.formatTime(state.lastClimb)
                            + "<alpha=#80><space=8px>·<space=8px><alpha=#FF>"
                            + (state.newBest ? "<color=" + Hex(UiKit.Foreground) + ">" + Esc(UiKit.Upper(t.newBest, lang)) + "</color>"
                                : Esc(UiKit.Upper(t.bestClimb, lang)) + " " + Save.formatTime(state.bestClimb));
                        var b = UiKit.Text("", UiKit.SANS, 400, false, UiKit.Rem(0.65), UiKit.A(UiKit.MutedFg, 0.8), 0.25, lang);
                        b.text = s;
                        b.style.unityTextAlign = TextAnchor.UpperCenter;
                        b.style.marginTop = 8;
                        descent.Add(b);
                    }
                }
            }
            descent.style.bottom = 56 + UiKit.safeBottom;

            // ---- the start card
            if (started && menu != null)
            {
                menu.RemoveFromHierarchy();
                menu = null;
                menuSelect = null;
            }
            if (menu != null) SyncMenu(t);

            // ---- the summit's moment
            int cer = ceremony >= 0 && !bookOpen && !epiphany ? ceremony : -1;
            if (cer != ceremonyShown)
            {
                ceremonyShown = cer;
                slotCeremony.Clear();
                if (cer >= 0)
                {
                    int c = cer;
                    slotCeremony.Add(new SummitCeremony(c, lang, t, foundCount, TOTAL_PAGES, OpenBook, () => { if (ceremony == c) { ceremony = -1; dirty = true; } }));
                }
            }

            // ---- a seal
            string seal = sealQueue.Count > 0 && ceremony < 0 && !bookOpen && !settingsOpen && !epiphany && !sceneOn ? sealQueue[0] + "|" + lang : null;
            if (seal != sealShown)
            {
                sealShown = seal;
                slotSeal.Clear();
                if (seal != null) slotSeal.Add(new SealToast(sealQueue[0], lang, t));
            }

            // ---- the story: notes, subtitles, the ending
            var note = notes.note;
            string nk = note != null && sealQueue.Count == 0 && !bookOpen && !settingsOpen && ceremony < 0
                ? note.GetHashCode() + "|" + note.kind + "|" + note.item + "|" + note.stage + "|" + note.id + "|" + lang : null;
            if (nk != noteKey)
            {
                noteKey = nk;
                slotStoryToast.Clear();
                if (nk != null) slotStoryToast.Add(new StoryToastView(new StoryToastProps { note = note, lang = lang }));
            }

            var b0 = blocks.Count > 0 ? blocks[0] : null;
            bool showBlock = started && !bookOpen && !settingsOpen && ceremony < 0 && !ended && b0 != null;
            string bk = showBlock ? (b0.cine ? "S|" : "s|") + (b0.lines.Count > 0 ? b0.lines[0].who + "|" + b0.lines[0].text : "") : null;
            if (bk != sceneKey)
            {
                sceneKey = bk;
                slotScene.Clear();
                slotSubtitle.Clear();
                sceneView = null;
                subtitleView = null;
                if (showBlock)
                {
                    if (b0.cine)
                    {
                        sceneView = new SceneView(new SceneProps { block = b0, lang = lang, onDone = DoneBlock, onAnswer = AnswerBlock, onSpeaker = SetSpeaker });
                        slotScene.Add(sceneView);
                    }
                    else
                    {
                        subtitleView = new SubtitleView(new SubtitleProps { block = b0, lang = lang, onDone = DoneBlock, onSpeaker = SetSpeaker, touch = touch });
                        slotSubtitle.Add(subtitleView);
                    }
                }
            }
            else if (showBlock)
            {
                if (sceneView != null) sceneView.Update(new SceneProps { block = b0, lang = lang, onDone = DoneBlock, onAnswer = AnswerBlock, onSpeaker = SetSpeaker });
                if (subtitleView != null) subtitleView.Update(new SubtitleProps { block = b0, lang = lang, onDone = DoneBlock, onSpeaker = SetSpeaker, touch = touch });
            }

            if (epiphany && epiphanyView == null) { epiphanyView = new EpiphanyView(new EpiphanyProps { lang = lang }); slotEpiphany.Add(epiphanyView); }
            else if (!epiphany && epiphanyView != null) { epiphanyView.RemoveFromHierarchy(); epiphanyView = null; }
            else if (epiphanyView != null) epiphanyView.Update(new EpiphanyProps { lang = lang });

            if (ended)
            {
                var props = new EndCardProps
                {
                    lang = lang,
                    stats = new[]
                    {
                        (t.cycle, Js.str(state.cycles)),
                        (t.bookTitle, foundCount + "/" + TOTAL_PAGES),
                        (t.sealsTitle, seals.Count.ToString(CultureInfo.InvariantCulture)),
                    },
                    onContinue = () =>
                    {
                        ended = false;
                        epiphany = false;
                        engine.endFinale();
                        var st = engine.saga.s;
                        Save.writeSave(d => d.saga = st);
                        AwardSeals(null, state.unlocked, state.cycles);
                        dirty = true;
                    },
                };
                if (endView == null) { endView = new EndCardView(props); slotEnd.Add(endView); }
                else endView.Update(props);
            }
            else if (endView != null) { endView.RemoveFromHierarchy(); endView = null; }

            if (prologue && prologueView == null) { prologueView = new PrologueView(new PrologueProps { lang = lang, onDone = FinishPrologue }); slotPrologue.Add(prologueView); }
            else if (!prologue && prologueView != null) { prologueView.RemoveFromHierarchy(); prologueView = null; }
            else if (prologueView != null) prologueView.Update(new PrologueProps { lang = lang, onDone = FinishPrologue });

            // ---- the wake veil (the web's first touch; never on a store app)
            if (asleep && wake == null) BuildWake(t);
            else if (!asleep && wake != null) { wake.RemoveFromHierarchy(); wake = null; }

            // ---- settings
            if (settingsPanel == null) { settingsPanel = new SettingsPanel(); slotSettings.Add(settingsPanel); }
            settingsPanel.Update(new SettingsPanel.Props
            {
                open = settingsOpen,
                onClose = CloseSettings,
                t = t,
                lang = lang,
                onLang = ChangeLang,
                muted = muted,
                onMute = ToggleMute,
                vibrates = vibrates,
                haptics = haptics,
                onHaptics = ToggleHaptics,
                calm = calm,
                onCalm = ToggleCalm,
                seals = seals,
                bigText = bigText,
                boldRing = boldRing,
                leftHanded = leftHanded,
                onPref = ChangePref,
                ownText = ownText,
                ownAuthor = ownAuthor,
                onOwn = ChangeOwn,
                records = new Records { best = state.bestClimb, week = recWeek, streak = recStreak, summits = state.cycles, pages = foundCount, seals = seals.Count },
                reload = Reload,
            });

            // ---- a chapter made whole
            int chs = chapterToast >= 0 && ceremony < 0 && sealQueue.Count == 0 && !bookOpen && !settingsOpen ? chapterToast : -1;
            if (chs != chapterShown)
            {
                chapterShown = chs;
                slotChapter.Clear();
                if (chs >= 0) slotChapter.Add(new ChapterToast(chs, lang, t));
            }

            // ---- the book
            var bp = new WisdomBookProps { open = bookOpen, onClose = CloseBook, lang = lang, t = t, unlocked = state.unlocked, fresh = fresh, version = quotesReady };
            if (book == null) { book = new WisdomBookView(bp); slotBook.Add(book); }
            else book.Update(bp);

            // ---- portrait: the one layout the game cannot be played in
            if (portrait && portraitEl == null) BuildPortrait(t);
            else if (!portrait && portraitEl != null) { portraitEl.RemoveFromHierarchy(); portraitEl = null; }

            mediaDirty = false;
        }

        void BuildKeyHelp(UIStrings t)
        {
            keyHelp.Clear();
            float fs = UiKit.Rem(0.6);
            Color txt = UiKit.A(UiKit.MutedFg, 0.8);
            VisualElement Kbd(string s)
            {
                var k = new VisualElement { pickingMode = PickingMode.Ignore };
                UiKit.Radius(k, 4);
                UiKit.BorderW(k, 1);
                UiKit.BorderC(k, UiKit.Border);
                k.style.backgroundColor = UiKit.A(UiKit.Background, 0.4);
                UiKit.Pad(k, 6, 2);
                k.Add(UiKit.Text(s, UiKit.MONO, 400, false, fs, UiKit.A(UiKit.Foreground, 0.9), 0.2, lang, true));
                return k;
            }
            VisualElement Ring(bool accent)
            {
                var r = new VisualElement { pickingMode = PickingMode.Ignore };
                r.style.width = 12; r.style.height = 12;
                UiKit.Radius(r, 6);
                UiKit.BorderW(r, accent ? 2 : 1);
                UiKit.BorderC(r, accent ? UiKit.Accent : UiKit.A(UiKit.Foreground, 0.7));
                return r;
            }
            VisualElement Row(params VisualElement[] items)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                foreach (var i in items) row.Add(i);
                GX.Gap(row, 6);
                return row;
            }
            Label L(string s) { return UiKit.Text(s, UiKit.SANS, 400, false, fs, txt, 0.2, lang, true); }
            keyHelp.Add(Row(Kbd("→"), Kbd("D"), Kbd(t.keySpace), L(t.helpAdvance)));
            keyHelp.Add(Row(Kbd("←"), Kbd("A"), L(t.helpPushBack)));
            keyHelp.Add(Row(Ring(false), L(t.helpRhythm)));
            keyHelp.Add(Row(Ring(true), L(t.helpRest)));
            keyHelp.Add(Row(Kbd("Tab"), L(t.helpThrow)));
            GX.Gap(keyHelp, 6, true);
        }

        void BuildWake(UIStrings t)
        {
            wake = new VisualElement { pickingMode = PickingMode.Position };
            UiKit.Fill(wake);
            wake.style.backgroundColor = new Color(0, 0, 0, 0.75f);
            wake.style.alignItems = Align.Center;
            wake.style.justifyContent = Justify.Center;
            wake.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            var l = UiKit.Text(t.touchToBegin, UiKit.CINZEL, 400, false, UiKit.Rem(0.95), UiKit.C("#e9d9b0"), 0.35, lang, true);
            wake.Add(l);
            UiKit.Tween(wake, 1200, 0, UiKit.EaseOut, k => wake.style.opacity = (float)k);
            UiKit.Loop(l, 2800, 0, ph => l.style.opacity = (float)(0.35 + 0.6 * UiKit.EaseInOut(ph < 0.5 ? ph * 2 : (1 - ph) * 2)));
            z70.Add(wake);
        }

        void BuildPortrait(UIStrings t)
        {
            portraitEl = new VisualElement { pickingMode = PickingMode.Position };
            UiKit.Fill(portraitEl);
            portraitEl.style.backgroundColor = UiKit.Background;
            portraitEl.style.alignItems = Align.Center;
            portraitEl.style.justifyContent = Justify.Center;
            UiKit.Pad(portraitEl, 32, 0);
            // a phone turning: the frame rotates, the label under it does not
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.style.width = 48;
            frame.style.height = 80;
            UiKit.Radius(frame, 8);
            UiKit.BorderW(frame, 2);
            UiKit.BorderC(frame, UiKit.A(UiKit.Foreground, 0.7));
            portraitEl.Add(frame);
            // animate-[spin_3s_ease-in-out_infinite] alternate: 0 → 360°, then back
            UiKit.Loop(frame, 6000, 0, ph =>
            {
                double u = ph < 0.5 ? ph * 2 : (1 - ph) * 2;
                frame.style.rotate = new Rotate(new Angle((float)(360 * UiKit.EaseInOut(u)), AngleUnit.Degree));
            });
            var l = UiKit.Text(t.rotate, UiKit.SANS, 400, false, UiKit.Rem(0.875), UiKit.Foreground, 0.3, lang, true, 1.25 / 0.875);
            l.style.marginTop = 24;
            l.style.unityTextAlign = TextAnchor.UpperCenter;
            portraitEl.Add(l);
            z50.Add(portraitEl);
        }

        /// <summary>Backup's window.location.reload(): the host starts the game screen again from the save.</summary>
        public static Action reloadGame;

        void Reload() { reloadGame?.Invoke(); }
    }
}
