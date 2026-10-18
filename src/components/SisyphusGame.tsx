import { useCallback, useEffect, useRef, useState } from "react";
import { SisyphusEngine, type EngineState } from "@/game/engine";
import { LANGUAGES, T, type LangCode } from "@/i18n";
import { browserLang, formatTime, loadSave, writeSave } from "@/game/save";
import { registerServiceWorker, useInstall } from "@/game/pwa";
import { QUOTE_COUNT, loadQuotes } from "@/game/quotes";
import { NewPageToast, WisdomBook } from "@/components/WisdomBook";

const TOTAL_PAGES = QUOTE_COUNT;
const canVibrate = () => typeof navigator !== "undefined" && "vibrate" in navigator;

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
  /** the page just found, while its little card is on screen */
  const [toast, setToast] = useState<number | null>(null);
  const toasted = useRef(-1);
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
    });
    setHaptics(save.haptics);
    setVibrates(canVibrate());
    engineRef.current = engine;
    engine.onState = setState;
    const initialLang = save.lang ?? browserLang();
    setLang(initialLang);
    engine.setLanguage(initialLang);
    void loadQuotes(initialLang).then(() => {
      engine.refreshQuotes();
      setQuotesReady((v) => v + 1);
    });
    setMuted(save.muted);
    engine.audio.setMuted(save.muted);
    engine.start();
    registerServiceWorker();

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
  }, []);

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
    setToast(n);
    const id = window.setTimeout(() => setToast(null), 7000);
    return () => window.clearTimeout(id);
  }, [state.newQuote, state.phase]);

  const openBook = useCallback(() => {
    const e = engineRef.current;
    if (e) {
      e.input = 0;
      e.paused = true;
    }
    setToast(null);
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

  const toggleHaptics = () => {
    const h = !haptics;
    setHaptics(h);
    if (engineRef.current) engineRef.current.haptics = h;
    writeSave({ haptics: h });
    // a short buzz says it is back on
    if (h) {
      try {
        navigator.vibrate?.(25);
      } catch {
        /* not allowed */
      }
    }
  };

  const begin = () => {
    engineRef.current?.activate();
    engineRef.current?.audio.start();
    engineRef.current?.audio.resume();
    // the click is the only user gesture we get, and both fullscreen and the
    // orientation lock require one — so this has to happen here and nowhere else
    if (rootRef.current) void requestLandscape(rootRef.current);
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
            {state.levelName}
          </p>
          <div className="mt-2 h-px w-32 bg-border sm:w-48">
            <div
              className="h-px bg-accent transition-[width] duration-150"
              style={{ width: `${state.progress * 100}%` }}
            />
          </div>
        </div>
        <div className="pointer-events-none flex items-center justify-end gap-3">
          <button
            onClick={openBook}
            aria-label={t.bookTitle}
            className="pointer-events-auto relative flex items-center gap-1.5 rounded border border-border/60 bg-background/40 px-2 py-1 text-[0.7rem] tracking-[0.15em] text-muted-foreground backdrop-blur-sm transition-colors hover:text-foreground"
          >
            <BookIcon />
            <span className="tabular-nums">
              {foundCount}/{TOTAL_PAGES}
            </span>
            {fresh.size > 0 && (
              <span className="absolute -top-1 -right-1 h-2 w-2 rounded-full bg-[#d9a95b] shadow-[0_0_6px_#d9a95b]" />
            )}
          </button>
          <LanguageSelect lang={lang} onChange={changeLang} />
          <button
            onClick={toggleMute}
            className="pointer-events-auto text-[0.7rem] tracking-[0.25em] text-muted-foreground uppercase transition-colors hover:text-foreground"
          >
            {muted ? t.muteOff : t.muteOn}
          </button>
          {vibrates && touch && (
            <button
              onClick={toggleHaptics}
              className="pointer-events-auto text-[0.7rem] tracking-[0.25em] text-muted-foreground uppercase transition-colors hover:text-foreground"
            >
              {haptics ? t.hapticsOn : t.hapticsOff}
            </button>
          )}
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
            className="absolute bottom-6 left-4 flex h-24 w-24 touch-none items-center justify-center rounded-full border border-border/60 bg-background/30 backdrop-blur-sm select-none"
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
          className="absolute right-4 bottom-6 flex h-16 w-16 touch-none items-center justify-center rounded-full border border-border bg-background/50 text-[0.65rem] tracking-[0.15em] text-foreground uppercase backdrop-blur-sm select-none active:scale-95 active:bg-accent"
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
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-6 overflow-y-auto bg-background/85 px-6 py-4 text-center [@media(max-height:500px)]:gap-2.5">
          <div className="absolute top-4 right-4">
            <LanguageSelect lang={lang} onChange={changeLang} />
          </div>
          <div className="relative h-[100px] w-[160px] shrink-0 [@media(max-height:500px)]:hidden">
            <div className="absolute bottom-0 left-[10px] h-[84px] w-[140px] rounded-t-full border border-border/30 bg-gradient-to-t from-accent/20 via-accent/10 to-transparent" />
            <div className="absolute left-2 bottom-[39px] h-8 w-8 rounded-full border border-border/40 bg-[radial-gradient(circle_at_30%_30%,oklch(0.82_0.02_80),oklch(0.52_0.02_55)_70%)] shadow-lg sisyphus-boulder" />
          </div>
          <h1 className="animate-fade-in text-4xl font-light tracking-[0.22em] text-foreground uppercase sm:text-7xl sm:tracking-[0.4em] [@media(max-height:500px)]:text-3xl [@media(max-height:500px)]:tracking-[0.3em]">
            Sisyphus
          </h1>
          <p className="animate-fade-in max-w-sm text-sm leading-relaxed text-muted-foreground [animation-delay:150ms] [animation-fill-mode:backwards] [@media(max-height:500px)]:max-w-2xl [@media(max-height:500px)]:text-xs">
            {t.instructions.split(/(\{k[123]\})/g).map((part, i) =>
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
          <p className="animate-fade-in max-w-md text-xs leading-relaxed text-muted-foreground/80 [animation-delay:200ms] [animation-fill-mode:backwards] [@media(max-height:500px)]:max-w-2xl [@media(max-height:500px)]:text-[0.68rem]">
            {t.instructions2}
          </p>
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
            className="animate-fade-in mt-2 rounded-full border border-border px-8 py-3 text-xs tracking-[0.35em] text-foreground uppercase transition-colors hover:bg-accent [animation-delay:300ms] [animation-fill-mode:backwards]"
          >
            {t.startBtn}
          </button>
          <button
            onClick={openBook}
            className="animate-fade-in -mt-2 flex items-center gap-2 rounded-full px-5 py-2 text-[0.65rem] tracking-[0.3em] text-muted-foreground uppercase transition-colors hover:text-foreground"
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
        </div>
      )}

      {toast !== null && !bookOpen && (
        <NewPageToast
          key={toast}
          index={toast}
          lang={lang}
          t={t}
          count={foundCount}
          total={TOTAL_PAGES}
          onOpen={openBook}
        />
      )}

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
