import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import "@fontsource/cormorant-garamond/400.css";
import "@fontsource/cormorant-garamond/400-italic.css";
import "@fontsource/cormorant-garamond/500-italic.css";
import "@fontsource/cormorant-garamond/600.css";
import { CHAPTER_COUNT, CHAPTER_OF, QUOTE_COUNT, quotesFor } from "@/game/quotes";
import type { LangCode, UIStrings } from "@/i18n";
import { BOOK_TEXT } from "@/game/bookText";
import { ShareButton } from "./GameExtras";

/**
 * The Book of Wisdom.
 *
 * A thousand pages do not fit on a wall of cards, and should not: this is laid
 * out as a book. Twelve chapters, by age and school — the first sages, Athens,
 * Rome, China, Anatolia, the moderns — each a run of numbered entries on
 * parchment, two pages to a spread on a wide screen and one on a phone.
 * Entries not found yet are left as blank ruled lines with their number, so
 * every chapter shows how much of it is still out on the hill.
 */

const SERIF = "'Cormorant Garamond', 'EB Garamond', Garamond, Georgia, 'Times New Roman', serif";

const INK = "#2a1c0f";
const SEPIA = "#6b4a22";
const GOLD = "#c9a15a";
const WAX = "#8e2b1e";

const fmt = (s: string, vars: Record<string, string | number>) =>
  s.replace(/\{(\w+)\}/g, (_, k: string) => String(vars[k] ?? ""));

function toRoman(n: number) {
  const map: Array<[number, string]> = [
    [1000, "M"],
    [900, "CM"],
    [500, "D"],
    [400, "CD"],
    [100, "C"],
    [90, "XC"],
    [50, "L"],
    [40, "XL"],
    [10, "X"],
    [9, "IX"],
    [5, "V"],
    [4, "IV"],
    [1, "I"],
  ];
  let out = "";
  for (const [v, r] of map) {
    while (n >= v) {
      out += r;
      n -= v;
    }
  }
  return out;
}

/** parchment: warm laid paper, darker and foxed toward the edges */
const PAPER: React.CSSProperties = {
  backgroundColor: "#ecdfc2",
  backgroundImage: [
    // laid lines, barely there
    "repeating-linear-gradient(0deg, rgba(120, 85, 40, 0.035) 0 1px, transparent 1px 4px)",
    "radial-gradient(circle at 12% 18%, rgba(150, 100, 40, 0.10) 0, transparent 18%)",
    "radial-gradient(circle at 88% 84%, rgba(150, 100, 40, 0.12) 0, transparent 22%)",
    "radial-gradient(circle at 72% 30%, rgba(255, 252, 240, 0.5) 0, transparent 45%)",
    "radial-gradient(ellipse at center, rgba(0,0,0,0) 58%, rgba(120, 78, 30, 0.30) 100%)",
    "linear-gradient(170deg, #f3e9d1 0%, #e9dbb9 55%, #e0cda4 100%)",
  ].join(","),
};

/** a small engraved ornament: a rule with a lozenge at its centre */
function Rule({ className = "", color = SEPIA }: { className?: string; color?: string }) {
  return (
    <svg className={className} viewBox="0 0 120 8" aria-hidden="true">
      <line x1="0" y1="4" x2="50" y2="4" stroke={color} strokeWidth="0.6" opacity="0.6" />
      <line x1="70" y1="4" x2="120" y2="4" stroke={color} strokeWidth="0.6" opacity="0.6" />
      <path d="M60 0.8 L63.2 4 L60 7.2 L56.8 4 Z" fill={color} opacity="0.75" />
      <circle cx="52.5" cy="4" r="0.9" fill={color} opacity="0.6" />
      <circle cx="67.5" cy="4" r="0.9" fill={color} opacity="0.6" />
    </svg>
  );
}

/** the Greek key, pressed in gold into the leather */
function Meander({ className = "" }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 40 8" preserveAspectRatio="none" aria-hidden="true">
      <path
        d="M0 7 H6 V1 H14 V5 H10 V3 M10 7 H20 V1 H28 V5 H24 V3 M24 7 H34 V1 H40"
        fill="none"
        stroke="currentColor"
        strokeWidth="0.8"
      />
    </svg>
  );
}

/** a laurel-and-seal mark for a finished chapter */
function Seal({ size = 14 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" aria-hidden="true">
      <circle cx="8" cy="8" r="7" fill={WAX} />
      <circle cx="8" cy="8" r="5.2" fill="none" stroke="#e8c9a0" strokeWidth="0.6" opacity="0.7" />
      <path
        d="M8 4.2 L9.1 6.9 L12 7.1 L9.8 8.9 L10.5 11.7 L8 10.2 L5.5 11.7 L6.2 8.9 L4 7.1 L6.9 6.9 Z"
        fill="#e8c9a0"
        opacity="0.85"
      />
    </svg>
  );
}

type Entry = { index: number; text?: string; author?: string };

function EntryRow({
  e,
  rtl,
  fresh,
  onOpen,
  compact,
}: {
  e: Entry;
  rtl: boolean;
  fresh: boolean;
  onOpen: (i: number) => void;
  compact: boolean;
}) {
  const found = e.text !== undefined;
  return (
    <li className="relative flex gap-3" style={{ minHeight: compact ? 54 : 70 }}>
      <span
        className="w-8 shrink-0 pt-[3px] text-right text-[0.78rem] tabular-nums"
        style={{ fontFamily: SERIF, color: found ? SEPIA : "rgba(107,74,34,0.45)" }}
      >
        {e.index + 1}
      </span>
      {found ? (
        <button
          onClick={() => onOpen(e.index)}
          className="group min-w-0 flex-1 text-left outline-none"
          dir={rtl ? "rtl" : undefined}
        >
          <span
            className={`block leading-snug italic transition-colors group-hover:text-black group-focus-visible:underline ${
              compact
                ? "line-clamp-2 text-[0.98rem]"
                : "line-clamp-3 text-[1.08rem] xl:text-[1.2rem]"
            }`}
            style={{ fontFamily: SERIF, color: INK, fontWeight: 500 }}
          >
            {e.text}
          </span>
          <span
            className="mt-0.5 block text-right text-[0.62rem] tracking-[0.22em] uppercase"
            style={{ fontFamily: SERIF, color: SEPIA, fontWeight: 600 }}
          >
            {e.author}
          </span>
          {fresh && (
            <span
              className="absolute top-1 -left-1 h-2 w-2 rounded-full"
              style={{ background: WAX, boxShadow: "0 0 0 2px rgba(142,43,30,0.2)" }}
            />
          )}
        </button>
      ) : (
        // not found yet: the ruled lines the words will go on
        <span
          className="flex min-w-0 flex-1 flex-col justify-center gap-[9px] pt-1"
          aria-hidden="true"
        >
          <span className="block h-px w-full" style={{ background: "rgba(107,74,34,0.18)" }} />
          <span className="block h-px w-[72%]" style={{ background: "rgba(107,74,34,0.14)" }} />
          {!compact && (
            <span
              className="block h-px w-[38%] self-end"
              style={{ background: "rgba(107,74,34,0.12)" }}
            />
          )}
        </span>
      )}
    </li>
  );
}

function Leaf({
  side,
  head,
  folio,
  entries,
  rtl,
  fresh,
  onOpen,
  compact,
  turnKey,
  dir,
}: {
  side: "left" | "right" | "single";
  head: string;
  folio: number;
  entries: Entry[];
  rtl: boolean;
  fresh: Set<number>;
  onOpen: (i: number) => void;
  compact: boolean;
  turnKey: string;
  dir: 1 | -1;
}) {
  // the binding casts its shadow into the page from the spine side
  const gutter =
    side === "left"
      ? "linear-gradient(to left, rgba(60,35,10,0.32), rgba(60,35,10,0.08) 7%, transparent 16%)"
      : side === "right"
        ? "linear-gradient(to right, rgba(60,35,10,0.32), rgba(60,35,10,0.08) 7%, transparent 16%)"
        : "none";
  return (
    <section
      key={turnKey}
      className={`relative flex h-full min-w-0 flex-1 flex-col overflow-hidden ${
        dir > 0 ? "book-turn-next" : "book-turn-prev"
      } ${side === "left" ? "rounded-l-[6px]" : side === "right" ? "rounded-r-[6px]" : "rounded-[6px]"}`}
      style={{ ...PAPER, transformOrigin: side === "left" ? "right center" : "left center" }}
    >
      <div className="pointer-events-none absolute inset-0" style={{ background: gutter }} />
      <div
        className={`relative flex h-full flex-col ${compact ? "px-5 pt-3 pb-2" : "px-8 pt-6 pb-4"}`}
      >
        <header className="text-center">
          <p
            className="truncate text-[0.62rem] tracking-[0.32em] uppercase"
            style={{ fontFamily: SERIF, color: SEPIA, fontWeight: 600 }}
          >
            {head}
          </p>
          <Rule className={`mx-auto ${compact ? "mt-1 h-1.5 w-20" : "mt-2 h-2 w-28"}`} />
        </header>
        <ol className={`mt-3 flex flex-1 flex-col ${compact ? "gap-2" : "gap-4"}`}>
          {entries.map((e) => (
            <EntryRow
              key={e.index}
              e={e}
              rtl={rtl}
              fresh={fresh.has(e.index)}
              onOpen={onOpen}
              compact={compact}
            />
          ))}
        </ol>
        <footer
          className="text-center text-[0.7rem] tabular-nums"
          style={{ fontFamily: SERIF, color: SEPIA }}
        >
          — {folio} —
        </footer>
      </div>
    </section>
  );
}

export function WisdomBook({
  open,
  onClose,
  lang,
  t,
  unlocked,
  fresh,
  version,
}: {
  open: boolean;
  onClose: () => void;
  lang: LangCode;
  t: UIStrings;
  /** indices found, in the order found */
  unlocked: number[];
  /** indices found since the book was last opened */
  fresh: Set<number>;
  /** bumped when this language's file has finished loading */
  version: number;
}) {
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const quotes = useMemo(() => quotesFor(lang), [lang, version]);
  const bt = BOOK_TEXT[lang];
  const rtl = lang === "ar";
  const found = useMemo(() => new Set(unlocked.filter((i) => i < QUOTE_COUNT)), [unlocked]);
  const [chapter, setChapter] = useState(0);
  const [spread, setSpread] = useState(0);
  const [dir, setDir] = useState<1 | -1>(1);
  const [onlyFound, setOnlyFound] = useState(false);
  const [reading, setReading] = useState<number | null>(null);
  const readingRef = useRef<number | null>(null);
  const stageRef = useRef<HTMLDivElement>(null);
  const [stage, setStage] = useState({ w: 900, h: 600 });
  const closeRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    readingRef.current = reading;
  }, [reading]);

  // chapter contents, in index order
  const byChapter = useMemo(() => {
    const out: number[][] = Array.from({ length: CHAPTER_COUNT }, () => []);
    for (let i = 0; i < QUOTE_COUNT; i++) out[CHAPTER_OF[i] ?? 0]!.push(i);
    return out;
  }, []);
  const chapterFound = useMemo(
    () => byChapter.map((list) => list.filter((i) => found.has(i)).length),
    [byChapter, found],
  );

  // open on the chapter of the newest page, so a fresh find is right there
  useEffect(() => {
    if (!open) {
      setReading(null);
      return;
    }
    const last = unlocked[unlocked.length - 1];
    if (last !== undefined && fresh.has(last)) {
      const c = CHAPTER_OF[last] ?? 0;
      setChapter(c);
      setOnlyFound(false);
      setSpread(-1); // resolved to the right spread once the layout is known
    }
    closeRef.current?.focus();
    // only when the book is opened
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // how many entries fit on a page, from the space the book actually has
  useEffect(() => {
    if (!open) return;
    const el = stageRef.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => {
      if (!e) return;
      setStage({ w: e.contentRect.width, h: e.contentRect.height });
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, [open]);

  const compact = stage.h < 380;
  const two = stage.w >= 760;
  const rowH = compact ? 70 : 92;
  const perPage = Math.max(2, Math.floor((stage.h - (compact ? 112 : 140)) / rowH));
  const perSpread = perPage * (two ? 2 : 1);

  const list = useMemo(
    () => (onlyFound ? byChapter[chapter]!.filter((i) => found.has(i)) : byChapter[chapter]!),
    [byChapter, chapter, onlyFound, found],
  );
  const spreads = Math.max(1, Math.ceil(list.length / perSpread));

  // land on the newest page's spread when the book opens on it
  useEffect(() => {
    if (spread !== -1) return;
    const last = unlocked[unlocked.length - 1];
    const pos = last === undefined ? 0 : list.indexOf(last);
    setSpread(pos < 0 ? 0 : Math.floor(pos / perSpread));
  }, [spread, unlocked, list, perSpread]);

  const at = Math.min(Math.max(0, spread), spreads - 1);

  const go = useCallback(
    (d: 1 | -1) => {
      setDir(d);
      setSpread((s) => {
        const cur = Math.min(Math.max(0, s), spreads - 1);
        const nx = cur + d;
        if (nx >= 0 && nx < spreads) return nx;
        // past the last spread: on to the next chapter, and back the same way
        if (d > 0 && chapter < CHAPTER_COUNT - 1) {
          setChapter(chapter + 1);
          return 0;
        }
        if (d < 0 && chapter > 0) {
          setChapter(chapter - 1);
          return 10_000; // clamped to that chapter's last spread
        }
        return cur;
      });
    },
    [spreads, chapter],
  );

  useEffect(() => {
    if (!open) return;
    const key = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        if (readingRef.current !== null) setReading(null);
        else onClose();
      } else if (readingRef.current === null && (e.key === "ArrowRight" || e.key === "ArrowLeft")) {
        e.stopPropagation();
        e.preventDefault();
        go((e.key === "ArrowRight") !== rtl ? 1 : -1);
      }
    };
    window.addEventListener("keydown", key, true);
    return () => window.removeEventListener("keydown", key, true);
  }, [open, onClose, go, rtl]);

  // swipe to turn
  const swipe = useRef<{ x: number; y: number } | null>(null);

  if (!open) return null;

  const total = QUOTE_COUNT;
  const count = found.size;
  const entryOf = (i: number): Entry =>
    found.has(i) && quotes[i]
      ? { index: i, text: quotes[i]!.text, author: quotes[i]!.author }
      : { index: i };
  const first = at * perSpread;
  const pageA = list.slice(first, first + perPage).map(entryOf);
  const pageB = two ? list.slice(first + perPage, first + perSpread).map(entryOf) : [];
  const head = `${toRoman(chapter + 1)} · ${bt.chapters[chapter]}`;
  const turnKey = `${lang}:${chapter}:${at}:${onlyFound}:${perPage}`;
  const q = reading !== null ? quotes[reading] : null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={t.bookTitle}
      lang={lang}
      className="animate-fade-in absolute inset-0 z-40 flex flex-col text-[#efe3c6]"
      style={{
        background: "radial-gradient(ellipse at 50% 40%, #221a12 0%, #120e0a 55%, #080605 100%)",
      }}
      onPointerDown={(e) => e.stopPropagation()}
    >
      {/* header */}
      <div
        className={`flex shrink-0 items-center gap-4 ${compact ? "px-4 pt-2 pb-1" : "px-6 pt-5 pb-3 sm:px-10"}`}
      >
        <div className="min-w-0 flex-1">
          <h2
            className={`truncate tracking-[0.18em] uppercase ${compact ? "text-lg" : "text-2xl sm:text-[1.9rem]"}`}
            style={{ fontFamily: SERIF, fontWeight: 600, color: "#f1e4c4" }}
          >
            {t.bookTitle}
          </h2>
          {!compact && (
            <p
              className="mt-0.5 text-sm italic"
              style={{ fontFamily: SERIF, color: "rgba(241,228,196,0.6)" }}
            >
              {t.bookSub}
            </p>
          )}
        </div>
        <div className="hidden shrink-0 flex-col items-end gap-1.5 sm:flex">
          <span
            className="text-sm tabular-nums"
            style={{ fontFamily: SERIF, color: "#f1e4c4", letterSpacing: "0.12em" }}
          >
            {fmt(t.bookCount, { n: count, total })}
          </span>
          <span className="block h-[2px] w-44 overflow-hidden rounded-full bg-white/10">
            <span
              className="block h-full"
              style={{
                width: `${(count / total) * 100}%`,
                background: `linear-gradient(90deg, #8a6a30, ${GOLD}, #f0d49a)`,
              }}
            />
          </span>
        </div>
        <button
          onClick={() => setOnlyFound((v) => !v)}
          aria-pressed={onlyFound}
          className="shrink-0 rounded-full border px-3 py-1 text-[0.6rem] tracking-[0.22em] uppercase transition-colors"
          style={{
            borderColor: onlyFound ? GOLD : "rgba(241,228,196,0.2)",
            color: onlyFound ? "#f1e4c4" : "rgba(241,228,196,0.6)",
            background: onlyFound ? "rgba(201,161,90,0.15)" : "transparent",
          }}
        >
          {onlyFound ? t.filterFound : t.filterAll}
        </button>
        <button
          ref={closeRef}
          onClick={onClose}
          aria-label={t.closeBtn}
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-[#f1e4c4]/25 text-[#f1e4c4]/80 transition-colors hover:border-[#f1e4c4]/50 hover:text-[#f1e4c4]"
        >
          <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true">
            <path d="M1 1 L11 11 M11 1 L1 11" stroke="currentColor" strokeWidth="1.4" />
          </svg>
        </button>
      </div>

      <div
        className={`flex min-h-0 flex-1 ${compact ? "gap-3 px-3 pb-2" : "gap-6 px-6 pb-6 sm:px-10"}`}
      >
        {/* contents */}
        <nav
          aria-label={bt.contents}
          className={`flex shrink-0 flex-col overflow-y-auto overscroll-contain ${compact ? "w-44" : "w-60"}`}
        >
          {!compact && (
            <p
              className="mb-2 text-[0.62rem] tracking-[0.32em] uppercase"
              style={{ fontFamily: SERIF, fontWeight: 600, color: GOLD }}
            >
              {bt.contents}
            </p>
          )}
          <ol className="flex flex-col">
            {bt.chapters.map((name, c) => {
              const n = chapterFound[c]!;
              const of = byChapter[c]!.length;
              const done = n === of;
              const active = c === chapter;
              return (
                <li key={c}>
                  <button
                    onClick={() => {
                      setDir(c > chapter ? 1 : -1);
                      setChapter(c);
                      setSpread(0);
                    }}
                    className={`group relative flex w-full items-baseline gap-2 border-l-2 text-left transition-colors ${
                      compact ? "py-1 pr-1 pl-2" : "py-1.5 pr-2 pl-3"
                    }`}
                    style={{
                      borderColor: active ? GOLD : "transparent",
                      background: active
                        ? "linear-gradient(90deg, rgba(201,161,90,0.14), transparent)"
                        : "transparent",
                    }}
                  >
                    <span
                      className="w-7 shrink-0 text-[0.7rem]"
                      style={{ fontFamily: SERIF, color: active ? GOLD : "rgba(241,228,196,0.4)" }}
                    >
                      {toRoman(c + 1)}
                    </span>
                    <span
                      className={`min-w-0 flex-1 truncate ${compact ? "text-[0.85rem]" : "text-[0.98rem]"}`}
                      style={{
                        fontFamily: SERIF,
                        fontWeight: active ? 600 : 400,
                        color: active ? "#f6ead0" : "rgba(241,228,196,0.72)",
                      }}
                    >
                      {name}
                    </span>
                    {done ? (
                      <span title={bt.complete} className="shrink-0 self-center">
                        <Seal size={compact ? 12 : 14} />
                      </span>
                    ) : (
                      <span
                        className="shrink-0 text-[0.62rem] tabular-nums"
                        style={{ color: "rgba(241,228,196,0.45)" }}
                      >
                        {n}/{of}
                      </span>
                    )}
                    <span
                      className="absolute right-2 bottom-0.5 left-12 h-px overflow-hidden"
                      style={{ background: "rgba(241,228,196,0.06)" }}
                    >
                      <span
                        className="block h-full"
                        style={{ width: `${(n / of) * 100}%`, background: "rgba(201,161,90,0.55)" }}
                      />
                    </span>
                  </button>
                </li>
              );
            })}
          </ol>
        </nav>

        {/* the open book */}
        <div className="relative flex min-w-0 flex-1 flex-col">
          <div
            ref={stageRef}
            className="relative min-h-0 flex-1"
            onPointerDown={(e) => (swipe.current = { x: e.clientX, y: e.clientY })}
            onPointerUp={(e) => {
              const s = swipe.current;
              swipe.current = null;
              if (!s) return;
              const dx = e.clientX - s.x;
              if (Math.abs(dx) > 50 && Math.abs(dx) > Math.abs(e.clientY - s.y) * 1.5)
                go(dx < 0 !== rtl ? 1 : -1);
            }}
          >
            {/* the board: leather, a stack of page edges, then the leaves */}
            <div
              className="absolute inset-0 rounded-[10px]"
              style={{
                background: "linear-gradient(160deg, #4a2f1c, #2e1c10 60%, #24160c)",
                boxShadow:
                  "0 30px 60px -20px rgba(0,0,0,0.85), inset 0 0 0 1px rgba(255,220,170,0.08), inset 0 0 40px rgba(0,0,0,0.5)",
              }}
            >
              <Meander className="absolute inset-x-6 top-1 h-1.5 text-[#c9a15a]/35" />
              <Meander className="absolute inset-x-6 bottom-1 h-1.5 rotate-180 text-[#c9a15a]/35" />
            </div>
            <div
              className={`absolute flex ${compact ? "inset-x-2 inset-y-2.5" : "inset-x-3 inset-y-3.5"}`}
              style={{
                // page edges: a few lines of paper showing under the leaves
                boxShadow:
                  "0 1px 0 #d9c8a2, 0 2px 0 #cdb98f, 0 3px 0 #d7c49c, 0 4px 0 #c7b285, 0 5px 6px rgba(0,0,0,0.45)",
                borderRadius: 6,
                perspective: 1600,
              }}
            >
              <Leaf
                side={two ? "left" : "single"}
                head={head}
                folio={at * (two ? 2 : 1) + 1}
                entries={pageA}
                rtl={rtl}
                fresh={fresh}
                onOpen={setReading}
                compact={compact}
                turnKey={`a:${turnKey}`}
                dir={dir}
              />
              {two && (
                <>
                  <div
                    className="w-px shrink-0"
                    style={{ background: "linear-gradient(#8a6a40, #5a4024, #8a6a40)" }}
                  />
                  <Leaf
                    side="right"
                    head={head}
                    folio={at * 2 + 2}
                    entries={pageB}
                    rtl={rtl}
                    fresh={fresh}
                    onOpen={setReading}
                    compact={compact}
                    turnKey={`b:${turnKey}`}
                    dir={dir}
                  />
                </>
              )}
            </div>

            {/* turning */}
            <button
              onClick={() => go(-1)}
              aria-label={bt.prev}
              disabled={at === 0 && chapter === 0}
              className="absolute top-1/2 -left-1 z-10 flex h-10 w-10 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full border border-[#c9a15a]/40 bg-[#1a120b]/90 text-[#e9d3a4] shadow-lg transition hover:border-[#c9a15a] disabled:opacity-25"
            >
              <svg width="10" height="14" viewBox="0 0 10 14" aria-hidden="true">
                <path d="M8 1 L2 7 L8 13" fill="none" stroke="currentColor" strokeWidth="1.5" />
              </svg>
            </button>
            <button
              onClick={() => go(1)}
              aria-label={bt.next}
              disabled={at >= spreads - 1 && chapter === CHAPTER_COUNT - 1}
              className="absolute top-1/2 -right-1 z-10 flex h-10 w-10 translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full border border-[#c9a15a]/40 bg-[#1a120b]/90 text-[#e9d3a4] shadow-lg transition hover:border-[#c9a15a] disabled:opacity-25"
            >
              <svg width="10" height="14" viewBox="0 0 10 14" aria-hidden="true">
                <path d="M2 1 L8 7 L2 13" fill="none" stroke="currentColor" strokeWidth="1.5" />
              </svg>
            </button>
          </div>
          {!compact && (
            <p
              className="mt-3 text-center text-[0.7rem] tracking-[0.3em] uppercase tabular-nums"
              style={{ fontFamily: SERIF, color: "rgba(241,228,196,0.5)" }}
            >
              {toRoman(chapter + 1)} · {at + 1} / {spreads}
            </p>
          )}
        </div>
      </div>

      {/* one page, read up close */}
      {q && reading !== null && (
        <div
          className="animate-fade-in absolute inset-0 z-20 flex items-center justify-center bg-black/75 p-6 backdrop-blur-[2px]"
          onClick={() => setReading(null)}
        >
          <figure
            className="book-reveal relative flex max-h-full w-full max-w-lg flex-col overflow-y-auto rounded-[4px] px-8 py-8 sm:px-12 sm:py-12"
            style={{
              ...PAPER,
              boxShadow:
                "0 40px 80px -20px rgba(0,0,0,0.9), inset 0 0 0 1px rgba(107,74,34,0.25), inset 0 0 0 6px rgba(236,223,194,1), inset 0 0 0 7px rgba(107,74,34,0.35)",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <span
              className="text-center text-[0.65rem] tracking-[0.35em] uppercase"
              style={{ fontFamily: SERIF, color: SEPIA, fontWeight: 600 }}
            >
              {toRoman((CHAPTER_OF[reading] ?? 0) + 1)} · {bt.chapters[CHAPTER_OF[reading] ?? 0]}
            </span>
            <Rule className="mx-auto mt-3 h-2.5 w-32" />
            <blockquote
              dir={rtl ? "rtl" : undefined}
              className="mt-6 text-center text-[1.65rem] leading-snug italic sm:text-[2rem]"
              style={{ fontFamily: SERIF, color: INK, fontWeight: 500 }}
            >
              {q.text}
            </blockquote>
            <figcaption
              className="mt-6 text-center text-[0.8rem] tracking-[0.3em] uppercase"
              style={{ fontFamily: SERIF, color: SEPIA, fontWeight: 600 }}
            >
              {q.author}
            </figcaption>
            <span
              className="mt-2 text-center text-[0.7rem] tabular-nums"
              style={{ fontFamily: SERIF, color: "rgba(107,74,34,0.7)" }}
            >
              № {reading + 1}
            </span>
            <div className="mt-7 flex items-center justify-center gap-3">
              <ShareButton
                index={reading}
                lang={lang}
                t={t}
                className="px-5 hover:bg-[#6b4a22]/10"
                style={{ borderColor: "rgba(107,74,34,0.4)", color: SEPIA }}
              />
              <button
                onClick={() => setReading(null)}
                className="rounded-full border px-6 py-1.5 text-[0.62rem] tracking-[0.3em] uppercase transition-colors hover:bg-[#6b4a22]/10"
                style={{
                  borderColor: "rgba(107,74,34,0.4)",
                  color: SEPIA,
                  fontFamily: SERIF,
                  fontWeight: 600,
                }}
              >
                {t.closeBtn}
              </button>
            </div>
          </figure>
        </div>
      )}
    </div>
  );
}
