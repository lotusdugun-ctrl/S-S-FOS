import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "@tanstack/react-router";
import { SisyphusEngine, type EngineState } from "@/game/engine";
import { LANGUAGES, T, type LangCode } from "@/i18n";
import {
  browserLang,
  flushNativeSave,
  formatTime,
  loadSave,
  restoreNativeSave,
  weekKey,
  writeSave,
} from "@/game/save";
import { registerServiceWorker, useInstall } from "@/game/pwa";
import { canVibrate, isNative, vibrate, watchApp } from "@/game/native";
import { CHAPTER_COUNT, CHAPTER_OF, QUOTE_COUNT, loadQuotes } from "@/game/quotes";
import { WisdomBook } from "@/components/WisdomBook";
import {
  ChapterToast,
  DailyQuote,
  SealToast,
  SettingsPanel,
  SummitCeremony,
} from "@/components/GameExtras";
import { dailyIndex, todayKey } from "@/game/daily";
import { SEAL_ORDER, sealsEarned, type SealId } from "@/game/seals";
import { EndCard, Epiphany, Prologue, Scene, StoryToast, Subtitle } from "@/components/Story";
import { HillEmblem } from "@/components/Prologue";
import { linesFor, useNotes, type Block } from "@/game/dialogue";
import { ROMAN, STAGE_TEXT, STORY, type Stage } from "@/game/journey";
import type { BurdenId, Speaker } from "@/game/story";
import type { SagaEvent } from "@/game/saga";

const TOTAL_PAGES = QUOTE_COUNT;

/** a closed book, drawn small enough to sit in the HUD */
function BookIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <path
        d="M3 2.5h7.5a2 2 0 0 1 2 2V14H5a2 2 0 0 1-2-2V2.5z"
        stroke="currentColor"
        strokeWidth="1.2"
      />
      <path d="M3 12a2 2 0 0 1 2-2h7.5" stroke="currentColor" strokeWidth="1.2" />
      <path d="M6 5.5h4" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
    </svg>
  );
}

/** settings: a small cog */
function GearIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <circle cx="8" cy="8" r="2.3" stroke="currentColor" strokeWidth="1.2" />
      <path
        d="M8 1.6v1.6M8 12.8v1.6M1.6 8h1.6M12.8 8h1.6M3.5 3.5l1.1 1.1M11.4 11.4l1.1 1.1M3.5 12.5l1.1-1.1M11.4 4.6l1.1-1.1"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinecap="round"
      />
      <circle cx="8" cy="8" r="4.6" stroke="currentColor" strokeWidth="1.2" />
    </svg>
  );
}

function LanguageSelect({ lang, onChange }: { lang: LangCode; onChange: (l: LangCode) => void }) {
  return (
    <select
      value={lang}
      onChange={(e) => onChange(e.target.value as LangCode)}
      className="pointer-events-auto cursor-pointer rounded border border-border/60 bg-background/40 px-2 py-1 text-[0.7rem] tracking-[0.15em] text-muted-foreground backdrop-blur-sm transition-colors outline-none hover:text-foreground"
    >
      {LANGUAGES.map((l) => (
        <option key={l.code} value={l.code}>
          {l.native}
        </option>
      ))}
    </select>
  );
}

/**
 * True while a hand-held device is being held upright.
 *
 * Both halves of the query matter. `orientation: portrait` alone would nag
 * anyone whose desktop browser window happens to be taller than it is wide, and
 * `pointer: coarse` alone would catch a tablet already held sideways. Together
 * they mean "a touch device, held the wrong way round", which is the only case
 * that wants the overlay.
 */
function useMedia(query: string) {
  const [matches, setMatches] = useState(false);
  useEffect(() => {
    const mq = window.matchMedia(query);
    const sync = () => setMatches(mq.matches);
    sync();
    mq.addEventListener("change", sync);
    return () => mq.removeEventListener("change", sync);
  }, [query]);
  return matches;
}

/**
 * Asks for landscape, and does not care very much whether it gets it.
 *
 * Locking orientation is only permitted from fullscreen, and only on browsers
 * that implement it at all — mobile Safari does neither, so on iOS both calls
 * below reject and the rotate overlay is the whole mechanism. Everything is
 * wrapped because a rejected lock must not take the start button down with it.
 */
async function requestLandscape(el: HTMLElement) {
  try {
    if (!document.fullscreenElement) await el.requestFullscreen({ navigationUI: "hide" });
  } catch {
    /* refused or unsupported; the game is perfectly playable windowed */
  }
  try {
    const orientation = screen.orientation as ScreenOrientation & {
      lock?: (o: string) => Promise<void>;
    };
    await orientation.lock?.("landscape");
  } catch {
    /* iOS, or a desktop browser: the overlay covers this case */
  }
}

/** the chapters every page of which has been found */
function chaptersWhole(unlocked: number[]): number[] {
  const per = new Array<number>(CHAPTER_COUNT).fill(0);
  const have = new Array<number>(CHAPTER_COUNT).fill(0);
  CHAPTER_OF.forEach((ch) => (per[ch] = (per[ch] ?? 0) + 1));
  unlocked.forEach((i) => {
    const ch = CHAPTER_OF[i];
    if (ch !== undefined) have[ch] = (have[ch] ?? 0) + 1;
  });
  return per.flatMap((n, ch) => (n > 0 && (have[ch] ?? 0) >= n ? [ch] : []));
}

export function SisyphusGame() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rootRef = useRef<HTMLDivElement>(null);
  const portrait = useMedia("(orientation: portrait) and (pointer: coarse)");
  /**
   * Whether to show the on-screen stick and throw button.
   *
   * These were hidden with Tailwind's `sm:hidden`, which is a *width* breakpoint
   * — and a phone turned sideways is 844 px wide, so the moment the game got the
   * landscape it now asks for, both controls disappeared and it could not be
   * played at all. Touch controls belong to having a touch screen, not to having
   * a narrow one.
   */
  const touch = useMedia("(pointer: coarse)");
  const engineRef = useRef<SisyphusEngine | null>(null);
  const pwa = useInstall();
  const [state, setState] = useState<EngineState>({
    phase: "playing",
    progress: 0,
    levelName: "",
    epigraph: "",
    cycles: 0,
    climbTime: 0,
    lastClimb: 0,
    bestClimb: 0,
    newBest: false,
    hint: null,
    unlocked: [],
    newQuote: -1,
    weather: "clear",
    levelId: "tartarus",
    tutorialDone: true,
    lastRun: null,
  });
  const [started, setStarted] = useState(false);
  const [muted, setMuted] = useState(false);
  const [lang, setLang] = useState<LangCode>("tr");
  const [haptics, setHaptics] = useState(true);
  /** bumps when a language's aphorisms finish loading, so the book redraws */
  const [quotesReady, setQuotesReady] = useState(0);
  const [vibrates, setVibrates] = useState(false);
  const [bookOpen, setBookOpen] = useState(false);
  /** pages found since the book was last opened, marked on their corner */
  const [fresh, setFresh] = useState<Set<number>>(() => new Set());
  /** the page just found, while its moment is on screen */
  const [ceremony, setCeremony] = useState<number | null>(null);
  const toasted = useRef(-1);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [calm, setCalm] = useState(false);
  /** seals earned, and the ones waiting to be announced */
  const [seals, setSeals] = useState<Set<string>>(() => new Set());
  const [sealQueue, setSealQueue] = useState<SealId[]>([]);
  const hillsRef = useRef<string[]>([]);
  const sealsRef = useRef<Set<string>>(new Set());
  /** the saying of the day: its page, and whether it went into the book */
  const [daily] = useState(() => dailyIndex());
  const [dailyAdded, setDailyAdded] = useState(false);
  /** accessibility choices, the player's own saying, the records worth keeping */
  const [prefs, setPrefs] = useState({ bigText: false, boldRing: false, leftHanded: false });
  const [own, setOwn] = useState({ text: "", author: "" });
  const [rec, setRec] = useState({ streak: 0, week: 0 });
  /** chapters already whole, and the one just finished while its note is up */
  const chaptersSeen = useRef<Set<number> | null>(null);
  const [chapterToast, setChapterToast] = useState<number | null>(null);
  /** the story on screen: who is speaking, notes, the prologue, the ending */
  /** what is being said: scenes that stop the climb, and words in passing */
  const [blocks, setBlocks] = useState<Block[]>([]);
  const finaleRef = useRef(false);
  const { note, push: pushNote } = useNotes();
  const [stage, setStage] = useState<Stage>(1);
  const [prologue, setPrologue] = useState(false);
  const [epiphany, setEpiphany] = useState(false);
  const [ended, setEnded] = useState(false);
  const langRef = useRef<LangCode>("tr");
  const joystickBaseRef = useRef<HTMLDivElement>(null);
  const joystickKnobRef = useRef<HTMLDivElement>(null);
  const joyActive = useRef(false);
  const JOY_RADIUS = 36;

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    // what this browser remembers; the language falls back to the browser's own
    // rather than to Turkish for everyone
    const save = loadSave();
    const engine = new SisyphusEngine(canvas, 0, {
      cycles: save.cycles,
      bestClimb: save.bestClimb,
      unlocked: save.quotes,
      haptics: save.haptics,
      calm: save.calm,
      tutorialDone: save.tutorialDone,
      saga: save.saga,
      run: save.run,
    });
    // the camera's moves are motion; respect a request for less of it
    try {
      engine.cinematic = !window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    } catch {
      /* no matchMedia */
    }
    setStage(engine.saga.stage);
    if (!save.saga.prologue) setPrologue(true);
    engine.onSaga = (e: SagaEvent) => {
      // every turn of the story is kept at once
      writeSave({ saga: engine.saga.s });
      switch (e.type) {
        case "talk": {
          const b: Block = { lines: linesFor(e.ref, langRef.current), cine: e.cine };
          if (e.offer) b.offer = e.offer;
          if (e.choice) b.choice = true;
          setBlocks((q) => [...q, b]);
          break;
        }
        case "took":
          pushNote({ kind: "took", item: e.item });
          break;
        case "stage":
          setStage(e.stage);
          pushNote({ kind: "stage", stage: e.stage });
          break;
        case "passive":
          pushNote({ kind: "passive", id: e.id });
          break;
        case "burden":
          pushNote({ kind: "burden", id: e.id });
          break;
        case "wedge":
          pushNote({ kind: "wedge" });
          break;
        case "finale":
          // she walks away once the farewell has been read
          finaleRef.current = true;
          break;
      }
    };
    engine.onMire = () => pushNote({ kind: "mire" });
    // at the stone, at the very end: the words, then the end card
    engine.onFinale = () => {
      setEpiphany(true);
      window.setTimeout(() => setEnded(true), 10000);
    };
    setCalm(save.calm);
    // only seals that still exist (one for climbing in fog went with the fog)
    const known = save.seals.filter((x) => (SEAL_ORDER as string[]).includes(x));
    setSeals(new Set(known));
    sealsRef.current = new Set(known);
    hillsRef.current = save.hills;
    setHaptics(save.haptics);
    setVibrates(canVibrate());
    engineRef.current = engine;
    // for local tooling (playtest bots, store footage); never in a production build
    if (import.meta.env.DEV) (window as unknown as { __engine: unknown }).__engine = engine;
    chaptersSeen.current = new Set(chaptersWhole(save.quotes));
    setPrefs({ bigText: save.bigText, boldRing: save.boldRing, leftHanded: save.leftHanded });
    engine.boldRing = save.boldRing;
    document.documentElement.style.fontSize = save.bigText ? "18px" : "";
    setOwn({ text: save.ownText, author: save.ownAuthor });
    if (save.ownText) engine.ownQuote = { text: save.ownText, author: save.ownAuthor || "—" };
    const wk = weekKey();
    setRec({ streak: save.bestStreak, week: save.weekKey === wk ? save.weekBest : 0 });
    engine.onState = setState;
    const initialLang = save.lang ?? browserLang();
    setLang(initialLang);
    engine.setLanguage(initialLang);
    void loadQuotes(initialLang).then(() => {
      engine.refreshQuotes();
      setQuotesReady((v) => v + 1);
    });
    // the saying of the day goes into the book, once a day
    const today = todayKey();
    const idx = dailyIndex();
    if (save.daily !== today) {
      engine.grantQuote(idx);
      writeSave({ daily: today });
    }
    setDailyAdded(true);
    setMuted(save.muted);
    engine.audio.setMuted(save.muted);
    engine.start();
    // a store app carries its files inside it; the worker is for the website
    if (!isNative()) registerServiceWorker();

    const onResize = () => engine.resize();
    window.addEventListener("resize", onResize);

    // --- keyboard
    const keys = new Set<string>();
    const apply = () => {
      const right =
        keys.has("ArrowRight") ||
        keys.has("KeyD") ||
        keys.has("Space") ||
        keys.has("KeyW") ||
        keys.has("ArrowUp");
      const left = keys.has("ArrowLeft") || keys.has("KeyA");
      engine.input = right ? 1 : left ? -0.6 : 0;
    };
    const down = (e: KeyboardEvent) => {
      if (engine.paused) return;
      keys.add(e.code);
      if (["Space", "ArrowRight", "ArrowLeft", "ArrowUp", "Tab"].includes(e.code))
        e.preventDefault();
      if (e.code === "Tab") engine.kick();
      apply();
    };
    const up = (e: KeyboardEvent) => {
      keys.delete(e.code);
      apply();
    };
    window.addEventListener("keydown", down);
    window.addEventListener("keyup", up);

    // --- pointer (mouse + touch drag)
    let pointerId: number | null = null;
    let startX = 0;
    const pdown = (e: PointerEvent) => {
      pointerId = e.pointerId;
      startX = e.clientX;
      canvas.setPointerCapture(e.pointerId);
      engine.input = 0.65;
    };
    const pmove = (e: PointerEvent) => {
      if (pointerId !== e.pointerId) return;
      const dx = (e.clientX - startX) / 90;
      engine.input = Math.max(-1, Math.min(1, 0.5 + dx));
    };
    const pend = (e: PointerEvent) => {
      if (pointerId !== e.pointerId) return;
      pointerId = null;
      engine.input = 0;
    };
    canvas.addEventListener("pointerdown", pdown);
    canvas.addEventListener("pointermove", pmove);
    canvas.addEventListener("pointerup", pend);
    canvas.addEventListener("pointercancel", pend);

    return () => {
      window.removeEventListener("resize", onResize);
      window.removeEventListener("keydown", down);
      window.removeEventListener("keyup", up);
      canvas.removeEventListener("pointerdown", pdown);
      canvas.removeEventListener("pointermove", pmove);
      canvas.removeEventListener("pointerup", pend);
      canvas.removeEventListener("pointercancel", pend);
      engine.stop();
    };
  }, [pushNote]);

  // Persist progress whenever it moves. Both only change at a summit or a
  // restart, so this is a handful of writes per cycle, not one per frame.
  useEffect(() => {
    if (!started) return;
    writeSave({ cycles: state.cycles, bestClimb: state.bestClimb });
  }, [started, state.cycles, state.bestClimb]);

  // every page found is kept, the moment it is found
  const foundCount = state.unlocked.length;
  useEffect(() => {
    if (foundCount === 0) return;
    writeSave({ quotes: state.unlocked });
    // state.unlocked only changes when foundCount does
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [foundCount]);

  // a new page: mark it, and once Zeus is done and the walk down begins, show it
  useEffect(() => {
    const n = state.newQuote;
    if (n < 0 || toasted.current === n) return;
    if (state.phase !== "descent") return;
    toasted.current = n;
    setFresh((f) => new Set(f).add(n));
    setCeremony(n);
  }, [state.newQuote, state.phase]);

  // the first climb's lessons, once seen, are never shown again
  useEffect(() => {
    if (started && state.tutorialDone) writeSave({ tutorialDone: true });
  }, [started, state.tutorialDone]);

  // seals: worked out at each summit, and whenever the book grows
  const awardSeals = useCallback(
    (run: EngineState["lastRun"], unlocked: number[], summits: number) => {
      const got = sealsEarned({
        summits,
        run,
        unlocked,
        hills: hillsRef.current,
        ended: engineRef.current?.saga.s.ended === true,
      });
      const fresh = got.filter((id) => !sealsRef.current.has(id));
      if (!fresh.length) return;
      fresh.forEach((id) => sealsRef.current.add(id));
      setSeals(new Set(sealsRef.current));
      setSealQueue((q) => [...q, ...fresh]);
      writeSave({ seals: [...sealsRef.current] });
    },
    [],
  );
  const lastRunId = state.lastRun?.id ?? -1;
  useEffect(() => {
    const run = state.lastRun;
    if (!run) return;
    if (!hillsRef.current.includes(run.levelId)) {
      hillsRef.current = [...hillsRef.current, run.levelId];
      writeSave({ hills: hillsRef.current });
    }
    awardSeals(run, state.unlocked, run.id + 1);
    // the records: the longest run of perfect heaves, and this week's best climb
    const save = loadSave();
    const wk = weekKey();
    const prevWeek = save.weekKey === wk ? save.weekBest : 0;
    const week = prevWeek > 0 ? Math.min(prevWeek, run.seconds) : run.seconds;
    const streak = Math.max(save.bestStreak, run.maxStreak);
    writeSave({ bestStreak: streak, weekKey: wk, weekBest: week });
    setRec({ streak, week });
    // once per summit
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lastRunId]);
  useEffect(() => {
    if (foundCount === 0) return;
    awardSeals(null, state.unlocked, state.cycles);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [foundCount]);
  // a chapter made whole leaves its keepsake on the hills
  useEffect(() => {
    const done = chaptersWhole(state.unlocked);
    if (engineRef.current) engineRef.current.chaptersDone = done;
    const seen = chaptersSeen.current;
    if (!seen) return;
    const fresh = done.find((ch) => !seen.has(ch));
    done.forEach((ch) => seen.add(ch));
    if (fresh !== undefined) setChapterToast(fresh);
  }, [foundCount, state.unlocked]);
  useEffect(() => {
    if (chapterToast === null || ceremony !== null || sealQueue.length) return;
    const id = window.setTimeout(() => setChapterToast(null), 5200);
    return () => window.clearTimeout(id);
  }, [chapterToast, ceremony, sealQueue.length]);
  const changePref = useCallback((k: "bigText" | "boldRing" | "leftHanded", v: boolean) => {
    setPrefs((p) => ({ ...p, [k]: v }));
    writeSave({ [k]: v });
    if (k === "bigText") document.documentElement.style.fontSize = v ? "18px" : "";
    if (k === "boldRing" && engineRef.current) engineRef.current.boldRing = v;
  }, []);
  const changeOwn = useCallback((text: string, author: string) => {
    const tx = text.trim().slice(0, 160);
    const au = author.trim().slice(0, 40);
    setOwn({ text: tx, author: au });
    writeSave({ ownText: tx, ownAuthor: au });
    if (engineRef.current) engineRef.current.ownQuote = tx ? { text: tx, author: au || "—" } : null;
  }, []);

  // one seal on screen at a time, a few seconds each
  useEffect(() => {
    if (!sealQueue.length || ceremony !== null || epiphany) return;
    const id = window.setTimeout(() => setSealQueue((q) => q.slice(1)), 4200);
    return () => window.clearTimeout(id);
  }, [sealQueue, ceremony, epiphany]);

  const openBook = useCallback(() => {
    const e = engineRef.current;
    if (e) {
      e.input = 0;
      e.paused = true;
    }
    setCeremony(null);
    setBookOpen(true);
  }, []);

  const closeBook = useCallback(() => {
    const e = engineRef.current;
    if (e) {
      e.input = 0;
      e.paused = false;
    }
    setBookOpen(false);
    setFresh(new Set());
  }, []);

  const openSettings = useCallback(() => {
    const e = engineRef.current;
    if (e) {
      e.input = 0;
      e.paused = true;
    }
    setSettingsOpen(true);
  }, []);
  const closeSettings = useCallback(() => {
    const e = engineRef.current;
    if (e) {
      e.input = 0;
      e.paused = false;
    }
    setSettingsOpen(false);
  }, []);

  const toggleCalm = () => {
    const c = !calm;
    setCalm(c);
    if (engineRef.current) engineRef.current.calm = c;
    writeSave({ calm: c });
  };

  // B opens and closes the book on a keyboard
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if (e.code !== "KeyB" || e.repeat) return;
      if (bookOpen) closeBook();
      else openBook();
    };
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, [bookOpen, openBook, closeBook]);

  // store app: silence in the background, and Android's back button closes the
  // book before it leaves the game
  const bookOpenRef = useRef(false);
  bookOpenRef.current = bookOpen;
  const settingsOpenRef = useRef(false);
  settingsOpenRef.current = settingsOpen;
  /*
   * The climb in progress is kept as it goes, and at once when the page is
   * hidden or closed, so reopening the game picks it up where it was. In the
   * store apps the save is also put back from the app's own copy if the
   * phone cleared the web view's.
   */
  useEffect(() => {
    void restoreNativeSave().then((back) => {
      if (back) window.location.reload();
    });
    const keep = () => {
      const e = engineRef.current;
      if (e) writeSave({ run: e.snapshotRun() });
    };
    const id = window.setInterval(keep, 4000);
    const hide = () => {
      if (document.visibilityState === "hidden") {
        keep();
        flushNativeSave();
      }
    };
    document.addEventListener("visibilitychange", hide);
    window.addEventListener("pagehide", keep);
    return () => {
      window.clearInterval(id);
      document.removeEventListener("visibilitychange", hide);
      window.removeEventListener("pagehide", keep);
    };
  }, []);

  useEffect(
    () =>
      watchApp({
        onPause: () => {
          const e = engineRef.current;
          if (e) {
            e.input = 0;
            e.audio.suspend();
            writeSave({ run: e.snapshotRun() });
          }
          flushNativeSave();
        },
        onResume: () => engineRef.current?.audio.resume(),
        onBack: () => {
          if (settingsOpenRef.current) {
            closeSettings();
            return true;
          }
          if (!bookOpenRef.current) return false;
          closeBook();
          return true;
        },
      }),
    [closeBook, closeSettings],
  );

  const endCeremony = useCallback(() => setCeremony(null), []);

  const toggleHaptics = () => {
    const h = !haptics;
    setHaptics(h);
    if (engineRef.current) engineRef.current.haptics = h;
    writeSave({ haptics: h });
    // a short buzz says it is back on
    if (h) vibrate(25);
  };

  // an exchange has been read: the next one; after the farewell, she goes
  const doneBlock = useCallback(() => {
    setBlocks((q) => {
      const rest = q.slice(1);
      if (!rest.length && finaleRef.current) {
        finaleRef.current = false;
        engineRef.current?.meropeGone();
      }
      return rest;
    });
  }, []);
  const answerBlock = useCallback(
    (yes: boolean) => {
      const e = engineRef.current;
      const b = blocks[0];
      if (e && b?.offer) e.saga.answer(yes);
      else if (e && b?.choice) e.saga.choose(yes);
      if (e) writeSave({ saga: e.saga.s });
      doneBlock();
    },
    [blocks, doneBlock],
  );
  // a scene stops the climb while it plays
  const sceneOn = !!blocks[0]?.cine && ceremony === null;
  useEffect(() => {
    const e = engineRef.current;
    if (!e) return;
    e.cutscene = sceneOn;
    // nothing else in the frame while a scene plays: no saying in the sky
    e.showAphorism = !sceneOn && ceremony === null;
  }, [sceneOn, ceremony]);
  const setSpeaker = useCallback((who: Speaker | null) => {
    if (engineRef.current) engineRef.current.speaker = who;
  }, []);
  const finishPrologue = () => {
    setPrologue(false);
    const e = engineRef.current;
    if (e) {
      e.saga.s.prologue = true;
      writeSave({ saga: e.saga.s });
    }
  };

  /*
   * The music from the first screen, not only once a climb begins. A browser
   * will not make a sound before the player has touched the page, so it is
   * tried at once (the store apps allow it) and again on the first touch, key
   * or click anywhere — the menu, the opening, a language change.
   */
  useEffect(() => {
    const wake = () => {
      const a = engineRef.current?.audio;
      if (!a) return;
      a.start();
      a.resume();
    };
    const id = window.setTimeout(wake, 0);
    const opts = { capture: true, passive: true } as const;
    const events = ["pointerdown", "touchend", "keydown"] as const;
    // every touch: a refused start, or a phone that paused the track, recovers
    for (const ev of events) window.addEventListener(ev, wake, opts);
    return () => {
      window.clearTimeout(id);
      for (const ev of events) window.removeEventListener(ev, wake, opts);
    };
  }, []);

  useEffect(() => {
    langRef.current = lang;
    // screen readers, hyphenation and the script-specific type rules in styles.css
    document.documentElement.lang = lang;
  }, [lang]);

  const begin = () => {
    engineRef.current?.activate();
    engineRef.current?.audio.start();
    engineRef.current?.audio.resume();
    // the click is the only user gesture we get, and both fullscreen and the
    // orientation lock require one — so this has to happen here and nowhere else
    // (a store app is landscape and full screen by its own configuration)
    if (rootRef.current && !isNative()) void requestLandscape(rootRef.current);
    setStarted(true);
  };

  const toggleMute = () => {
    const m = !muted;
    setMuted(m);
    engineRef.current?.audio.setMuted(m);
    writeSave({ muted: m });
  };

  const changeLang = (code: LangCode) => {
    setLang(code);
    engineRef.current?.setLanguage(code);
    writeSave({ lang: code });
    // the aphorisms come in their own file per language; fetched on first use
    void loadQuotes(code).then(() => {
      engineRef.current?.refreshQuotes();
      setQuotesReady((v) => v + 1);
    });
  };

  const t = T[lang];

  const joySet = (clientX: number, clientY: number) => {
    const base = joystickBaseRef.current;
    const engine = engineRef.current;
    if (!base || !engine) return;
    const rect = base.getBoundingClientRect();
    let dx = clientX - (rect.left + rect.width / 2);
    let dy = clientY - (rect.top + rect.height / 2);
    const dist = Math.hypot(dx, dy);
    if (dist > JOY_RADIUS) {
      dx = (dx / dist) * JOY_RADIUS;
      dy = (dy / dist) * JOY_RADIUS;
    }
    if (joystickKnobRef.current) {
      joystickKnobRef.current.style.transform = `translate(${dx}px, ${dy}px)`;
    }
    engine.input = dx / JOY_RADIUS;
  };

  const joyDown = (e: React.PointerEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.currentTarget.setPointerCapture(e.pointerId);
    joyActive.current = true;
    joySet(e.clientX, e.clientY);
  };
  const joyMove = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!joyActive.current) return;
    joySet(e.clientX, e.clientY);
  };
  const joyUp = () => {
    joyActive.current = false;
    if (engineRef.current) engineRef.current.input = 0;
    if (joystickKnobRef.current) {
      joystickKnobRef.current.style.transform = "translate(0px, 0px)";
    }
  };

  return (
    <div ref={rootRef} className="relative h-dvh w-full overflow-hidden bg-background select-none">
      <canvas ref={canvasRef} className="h-full w-full touch-none" />

      {/* HUD */}
      <div className="pointer-events-none absolute inset-x-0 top-0 flex items-start justify-between p-4 sm:p-6">
        <div>
          <p className="text-[0.7rem] tracking-[0.35em] text-muted-foreground uppercase">
            <span className="mr-2 text-foreground/70">
              {STORY[lang].act} {ROMAN[stage - 1]} · {STAGE_TEXT[lang][stage - 1]!.name}
            </span>
            <span className="text-muted-foreground/50">·</span> {state.levelName}
            {state.weather !== "clear" && (
              <span className="ml-2 tracking-[0.25em] text-muted-foreground/70">
                · {state.weather === "rain" ? t.weatherRain : t.weatherStorm}
              </span>
            )}
          </p>
          <div className="mt-2 h-px w-32 bg-border sm:w-48">
            <div
              className="h-px bg-accent transition-[width] duration-150"
              style={{ width: `${state.progress * 100}%` }}
            />
          </div>
        </div>
        <div className="pointer-events-none flex items-center justify-end gap-2">
          <button
            onClick={openBook}
            aria-label={t.bookTitle}
            className={`pointer-events-auto relative flex items-center gap-1.5 rounded border border-border/60 bg-background/40 px-2.5 py-1.5 text-[0.7rem] tracking-[0.15em] text-muted-foreground backdrop-blur-sm transition-colors hover:text-foreground ${
              fresh.size > 0 ? "book-glow" : ""
            }`}
          >
            <BookIcon />
            <span className="tabular-nums">
              {foundCount}/{TOTAL_PAGES}
            </span>
            {fresh.size > 0 && (
              <span className="absolute -top-1 -right-1 h-2 w-2 rounded-full bg-[#d9a95b] shadow-[0_0_6px_#d9a95b]" />
            )}
          </button>
          <button
            onClick={openSettings}
            aria-label={t.settings}
            className="pointer-events-auto flex h-[30px] w-[30px] items-center justify-center rounded border border-border/60 bg-background/40 text-muted-foreground backdrop-blur-sm transition-colors hover:text-foreground"
          >
            <GearIcon />
          </button>
        </div>
      </div>

      <p className="pointer-events-none absolute bottom-5 left-1/2 -translate-x-1/2 text-[0.65rem] tracking-[0.3em] text-muted-foreground/70 uppercase">
        {t.cycle} {state.cycles + 1}
      </p>

      {/* Key shortcuts — same reasoning as the touch controls, inverted. These
          were shown at >= 640 px, which on a phone in landscape put a list of
          keyboard keys directly underneath the joystick. */}
      {!touch && (state.phase === "playing" || state.phase === "rolling") && (
        <div className="pointer-events-none absolute bottom-5 left-4 flex flex-col gap-1.5 text-[0.6rem] tracking-[0.2em] text-muted-foreground/80 uppercase">
          <div className="flex items-center gap-1.5">
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              →
            </kbd>
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              D
            </kbd>
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              {t.keySpace}
            </kbd>
            <span>{t.helpAdvance}</span>
          </div>
          <div className="flex items-center gap-1.5">
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              ←
            </kbd>
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              A
            </kbd>
            <span>{t.helpPushBack}</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="inline-block h-3 w-3 rounded-full border border-foreground/70" />
            <span>{t.helpRhythm}</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="inline-block h-3 w-3 rounded-full border-2 border-accent" />
            <span>{t.helpRest}</span>
          </div>
          <div className="flex items-center gap-1.5">
            <kbd className="rounded border border-border bg-background/40 px-1.5 py-0.5 font-mono text-foreground/90">
              Tab
            </kbd>
            <span>{t.helpThrow}</span>
          </div>
        </div>
      )}

      {/* Mobile joystick */}
      {touch &&
        (state.phase === "playing" || state.phase === "rolling" || state.phase === "descent") && (
          <div
            ref={joystickBaseRef}
            onPointerDown={joyDown}
            onPointerMove={joyMove}
            onPointerUp={joyUp}
            onPointerCancel={joyUp}
            className={`absolute bottom-6 ${prefs.leftHanded ? "right-4" : "left-4"} flex h-24 w-24 touch-none items-center justify-center rounded-full border border-border/60 bg-background/30 backdrop-blur-sm select-none`}
          >
            <div
              ref={joystickKnobRef}
              className="h-10 w-10 rounded-full border border-border bg-accent/70"
              style={{ willChange: "transform" }}
            />
          </div>
        )}

      {/* Mobile throw button */}
      {touch && (state.phase === "playing" || state.phase === "rolling") && (
        <button
          onPointerDown={(e) => {
            e.preventDefault();
            engineRef.current?.kick();
          }}
          className={`absolute ${prefs.leftHanded ? "left-4" : "right-4"} bottom-6 flex h-16 w-16 touch-none items-center justify-center rounded-full border border-border bg-background/50 text-[0.65rem] tracking-[0.15em] text-foreground uppercase backdrop-blur-sm select-none active:scale-95 active:bg-accent`}
        >
          {t.throwBtn}
        </button>
      )}

      {/* Contextual nudge: only when the player is stuck on something we can name */}
      {state.hint && (
        <p
          key={state.hint}
          className="animate-fade-in pointer-events-none absolute top-[18%] left-1/2 w-max max-w-[80vw] -translate-x-1/2 rounded-full bg-background/45 px-4 py-1.5 text-center text-[0.7rem] tracking-[0.2em] text-foreground/90 uppercase backdrop-blur-sm"
        >
          {state.hint === "kick"
            ? t.hintKick.replace("{k}", touch ? t.throwBtn : "Tab")
            : state.hint === "push"
              ? touch
                ? t.hintPushTouch
                : t.hintPush
              : state.hint === "rhythm"
                ? t.hintRhythm
                : state.hint === "breathe"
                  ? t.hintBreathe
                  : t.hintRest}
        </p>
      )}

      {/* The summit used to put the aphorism up here as a card. Zeus has the
          frame to himself now; the words are written into the sky once the
          stone is on its way back down. */}

      {/*
        There was an ending card here — "50 / 50 cycles complete" with the
        epigraph and a restart button. It is gone with the cycle limit: the one
        thing this myth does not have is a finish, and a game about it should not
        hand out a completion screen.
      */}

      {/* The walk down: no card, no button — the hill, the hour changing, the
          words in the sky, and a quiet reminder of how to walk. */}
      {state.phase === "descent" && (
        <div className="animate-fade-in pointer-events-none absolute inset-x-0 bottom-14 flex flex-col items-center gap-2 px-6 text-center">
          <p className="text-[0.7rem] tracking-[0.3em] text-foreground/85 uppercase">
            {touch ? t.walkHintTouch : t.walkHint}
          </p>
          {state.lastClimb > 0 && (
            <p className="text-[0.65rem] tracking-[0.25em] text-muted-foreground/80 uppercase tabular-nums">
              {t.thisClimb} {formatTime(state.lastClimb)}
              <span className="mx-2 opacity-50">·</span>
              {state.newBest ? (
                <span className="text-foreground">{t.newBest}</span>
              ) : (
                <>
                  {t.bestClimb} {formatTime(state.bestClimb)}
                </>
              )}
            </p>
          )}
        </div>
      )}

      {/* Start overlay */}
      {!started && (
        <div className="menu-veil absolute inset-0 flex flex-col items-center justify-center gap-6 overflow-y-auto px-6 py-4 text-center [@media(max-height:500px)]:gap-2.5">
          {/* embers rising through the menu */}
          <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden="true">
            {Array.from({ length: 16 }, (_, i) => (
              <span
                key={i}
                className="menu-ember"
                style={{
                  left: `${(i * 61) % 100}%`,
                  animationDelay: `${(i * 0.83) % 9}s`,
                  animationDuration: `${8 + ((i * 7) % 6)}s`,
                }}
              />
            ))}
          </div>
          <div className="absolute top-4 right-4 z-10 flex items-center gap-2">
            <LanguageSelect lang={lang} onChange={changeLang} />
            <button
              onClick={openSettings}
              aria-label={t.settings}
              className="flex h-[28px] w-[28px] items-center justify-center rounded border border-border/60 bg-background/40 text-muted-foreground hover:text-foreground"
            >
              <GearIcon />
            </button>
          </div>
          <HillEmblem className="menu-rise h-[110px] w-[230px] shrink-0 [@media(max-height:760px)]:hidden" />
          <h1
            className="menu-title -mt-4 text-4xl [@media(max-height:760px)]:mt-0 text-foreground sm:text-7xl [@media(max-height:500px)]:mt-0 [@media(max-height:500px)]:text-3xl"
            aria-label="Sisyphus"
          >
            {Array.from("SISYPHUS").map((ch, i) => (
              <span
                key={i}
                className="prologue-letter"
                style={{ animationDelay: `${150 + i * 90}ms` }}
              >
                {ch}
              </span>
            ))}
          </h1>
          <span className="prologue-line -mt-3 h-px bg-[#d9b46a] [@media(max-height:500px)]:hidden" />
          <p className="menu-rise max-w-sm text-sm leading-relaxed text-muted-foreground [animation-delay:900ms] [@media(max-height:500px)]:max-w-2xl [@media(max-height:500px)]:text-xs">
            {(touch ? t.instructionsTouch : t.instructions).split(/(\{k[123]\})/g).map((part, i) =>
              part === "{k1}" ? (
                <span key={i} className="text-foreground">
                  space / → / D
                </span>
              ) : part === "{k2}" ? (
                <span key={i} className="text-foreground">
                  Tab
                </span>
              ) : part === "{k3}" ? (
                <span key={i} className="text-foreground">
                  {t.throwBtn}
                </span>
              ) : (
                <span key={i}>{part}</span>
              ),
            )}
          </p>
          <p className="menu-rise max-w-md text-xs leading-relaxed text-muted-foreground/80 [animation-delay:1050ms] [@media(max-height:500px)]:max-w-2xl [@media(max-height:500px)]:text-[0.68rem]">
            {t.instructions2}
          </p>
          {quotesReady > 0 && (
            <div className="menu-rise menu-card [animation-delay:1250ms]">
              <DailyQuote index={daily} lang={lang} t={t} added={dailyAdded} />
            </div>
          )}
          {state.cycles > 0 && (
            <p className="text-[0.65rem] tracking-[0.3em] text-muted-foreground uppercase tabular-nums">
              {t.cycle} {state.cycles + 1}
              {state.bestClimb > 0 && (
                <>
                  <span className="mx-2 opacity-50">·</span>
                  {t.bestClimb} {formatTime(state.bestClimb)}
                </>
              )}
            </p>
          )}
          <button
            onClick={begin}
            className="menu-start mt-2 rounded-full border px-10 py-3 text-xs tracking-[0.4em] text-foreground uppercase transition-colors"
          >
            {t.startBtn}
          </button>
          <button
            onClick={openBook}
            className="menu-rise -mt-2 flex [animation-delay:1600ms] items-center gap-2 rounded-full px-5 py-2 text-[0.65rem] tracking-[0.3em] text-muted-foreground uppercase transition-colors hover:text-foreground"
          >
            <BookIcon /> {t.bookTitle}
            <span className="tabular-nums opacity-70">
              {foundCount}/{TOTAL_PAGES}
            </span>
          </button>
          {pwa.canInstall && (
            <button
              onClick={() => void pwa.install()}
              className="animate-fade-in -mt-2 rounded-full px-5 py-2 text-[0.65rem] tracking-[0.3em] text-muted-foreground uppercase underline-offset-4 transition-colors hover:text-foreground hover:underline"
            >
              ↓ {t.installBtn}
            </button>
          )}
          {!pwa.canInstall && pwa.showIosHint && (
            <p className="-mt-2 text-[0.65rem] tracking-[0.15em] text-muted-foreground/80">
              {t.iosInstall}
            </p>
          )}
          <Link
            to="/privacy"
            className="absolute right-4 bottom-3 text-[0.6rem] tracking-[0.25em] text-muted-foreground/60 uppercase transition-colors hover:text-foreground"
          >
            {t.privacy}
          </Link>
        </div>
      )}

      {ceremony !== null && !bookOpen && !epiphany && (
        <SummitCeremony
          key={ceremony}
          index={ceremony}
          lang={lang}
          t={t}
          count={foundCount}
          total={TOTAL_PAGES}
          onOpen={openBook}
          onDone={endCeremony}
        />
      )}

      {sealQueue[0] && ceremony === null && !bookOpen && !settingsOpen && !epiphany && !sceneOn && (
        <SealToast key={sealQueue[0]} id={sealQueue[0]} lang={lang} t={t} />
      )}

      {/* the story: notes, subtitles, the ending */}
      {note && !sealQueue.length && !bookOpen && !settingsOpen && ceremony === null && (
        <StoryToast key={JSON.stringify(note)} note={note} lang={lang} />
      )}
      {started &&
        !bookOpen &&
        !settingsOpen &&
        ceremony === null &&
        !ended &&
        blocks[0] &&
        (blocks[0].cine ? (
          <Scene
            key={JSON.stringify(blocks[0].lines[0])}
            block={blocks[0]}
            lang={lang}
            onDone={doneBlock}
            onAnswer={answerBlock}
            onSpeaker={setSpeaker}
          />
        ) : (
          <Subtitle
            key={JSON.stringify(blocks[0].lines[0])}
            block={blocks[0]}
            lang={lang}
            onDone={doneBlock}
            onSpeaker={setSpeaker}
            touch={touch}
          />
        ))}
      {epiphany && <Epiphany lang={lang} />}
      {ended && (
        <EndCard
          lang={lang}
          stats={[
            [t.cycle, String(state.cycles)],
            [t.bookTitle, `${foundCount}/${TOTAL_PAGES}`],
            [t.sealsTitle, `${seals.size}`],
          ]}
          onContinue={() => {
            setEnded(false);
            setEpiphany(false);
            const e = engineRef.current;
            if (e) {
              e.endFinale();
              writeSave({ saga: e.saga.s });
              awardSeals(null, state.unlocked, state.cycles);
            }
          }}
        />
      )}
      {prologue && <Prologue lang={lang} onDone={finishPrologue} />}

      <SettingsPanel
        open={settingsOpen}
        onClose={closeSettings}
        t={t}
        lang={lang}
        onLang={changeLang}
        muted={muted}
        onMute={toggleMute}
        vibrates={vibrates}
        haptics={haptics}
        onHaptics={toggleHaptics}
        calm={calm}
        onCalm={toggleCalm}
        seals={seals}
        prefs={prefs}
        onPref={changePref}
        own={own}
        onOwn={changeOwn}
        records={{
          best: state.bestClimb,
          week: rec.week,
          streak: rec.streak,
          summits: state.cycles,
          pages: foundCount,
          seals: seals.size,
        }}
      />

      {chapterToast !== null &&
        ceremony === null &&
        !sealQueue.length &&
        !bookOpen &&
        !settingsOpen && <ChapterToast key={chapterToast} ch={chapterToast} lang={lang} t={t} />}

      <WisdomBook
        open={bookOpen}
        onClose={closeBook}
        lang={lang}
        t={t}
        unlocked={state.unlocked}
        fresh={fresh}
        version={quotesReady}
      />

      {/*
        Last in the tree, so it covers every other overlay including the start
        card — on a phone held upright there is nothing worth doing underneath
        it. The scene is a wide slope with the summit off to one side, so in
        portrait the hill is most of the frame and the man is a speck; this is
        the one layout the game genuinely cannot be played in.
      */}
      {portrait && (
        <div className="absolute inset-0 z-50 flex flex-col items-center justify-center gap-6 bg-background px-8 text-center">
          {/* a phone turning: the frame rotates, the label under it does not */}
          <div className="animate-[spin_3s_ease-in-out_infinite] [animation-direction:alternate]">
            <div className="h-20 w-12 rounded-lg border-2 border-foreground/70" />
          </div>
          <p className="text-sm tracking-[0.3em] text-foreground uppercase">{t.rotate}</p>
        </div>
      )}
    </div>
  );
}
