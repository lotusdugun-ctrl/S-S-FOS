import { useEffect, useMemo, useRef, useState } from "react";
import { quotesFor } from "@/game/quotes";
import type { LangCode, UIStrings } from "@/i18n";

/**
 * The Book of Wisdom: every aphorism found at a summit, written on its own
 * page. Pages not found yet are there too, face down, so the size of what is
 * still out there is part of the pull. Nothing is bought or ground for: one
 * summit, one page.
 */

const fmt = (s: string, vars: Record<string, string | number>) =>
  s.replace(/\{(\w+)\}/g, (_, k: string) => String(vars[k] ?? ""));

/** a page of parchment, as CSS: warm paper, darker toward the edges, a few foxing spots */
const PAPER = {
  backgroundColor: "#eadcbc",
  backgroundImage: [
    "radial-gradient(circle at 18% 22%, rgba(140, 96, 40, 0.10) 0, transparent 22%)",
    "radial-gradient(circle at 82% 78%, rgba(140, 96, 40, 0.12) 0, transparent 26%)",
    "radial-gradient(circle at 70% 15%, rgba(255, 250, 235, 0.55) 0, transparent 40%)",
    "radial-gradient(ellipse at center, rgba(0,0,0,0) 55%, rgba(110, 72, 30, 0.28) 100%)",
    "linear-gradient(160deg, #f1e6cb 0%, #e6d4ad 55%, #dcc69a 100%)",
  ].join(","),
  boxShadow:
    "0 1px 0 rgba(255,255,255,0.35) inset, 0 0 0 1px rgba(90, 60, 25, 0.35), 0 10px 24px -10px rgba(0,0,0,0.7)",
} as const;

/** the back of a page not found yet: dark, plain, a meander border pressed into it */
const BACK = {
  backgroundColor: "#1b1814",
  backgroundImage: [
    "radial-gradient(ellipse at 50% 40%, rgba(255, 220, 160, 0.06) 0, transparent 70%)",
    "linear-gradient(160deg, #24201a 0%, #15130f 100%)",
  ].join(","),
  boxShadow: "0 0 0 1px rgba(255, 230, 190, 0.08), 0 8px 18px -10px rgba(0,0,0,0.8)",
} as const;

const SERIF = "'Cormorant Garamond', 'EB Garamond', Georgia, 'Times New Roman', serif";

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

/** the Greek key, as a thin strip, for the backs of pages */
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

function Page({
  n,
  text,
  author,
  rtl,
  fresh,
  onOpen,
  t,
}: {
  n: number;
  text: string;
  author: string;
  rtl: boolean;
  fresh: boolean;
  onOpen: () => void;
  t: UIStrings;
}) {
  return (
    <button
      onClick={onOpen}
      className="group relative flex aspect-[3/4] w-full flex-col rounded-[3px] p-3 text-left transition-transform duration-200 hover:-translate-y-0.5 hover:rotate-[-0.6deg] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber-200 sm:p-4"
      style={PAPER}
    >
      <span
        className="text-[0.55rem] tracking-[0.25em] text-[#6b4a22]/80 uppercase"
        style={{ fontFamily: SERIF }}
      >
        № {toRoman(n)}
      </span>
      {fresh && (
        <span className="absolute top-2 right-2 rounded-full bg-[#8a2a1a] px-1.5 py-0.5 text-[0.5rem] tracking-[0.15em] text-[#f6e9cf] uppercase shadow">
          {t.newPage}
        </span>
      )}
      <span
        dir={rtl ? "rtl" : undefined}
        className="mt-2 line-clamp-6 flex-1 text-[0.9rem] leading-snug [overflow-wrap:anywhere] text-[#2b1d0e] italic hyphens-auto"
        style={{ fontFamily: SERIF }}
      >
        “{text}”
      </span>
      <span
        className="mt-2 self-end text-[0.6rem] tracking-[0.2em] text-[#5a3d1c] uppercase"
        style={{ fontFamily: SERIF }}
      >
        — {author}
      </span>
    </button>
  );
}

function Blank({ n, t }: { n: number; t: UIStrings }) {
  return (
    <div
      className="relative flex aspect-[3/4] w-full flex-col items-center justify-center rounded-[3px] text-amber-100/25"
      style={BACK}
      aria-label={t.bookLocked}
    >
      <Meander className="absolute inset-x-3 top-3 h-2" />
      <Meander className="absolute inset-x-3 bottom-3 h-2 rotate-180" />
      <span className="text-lg tracking-[0.2em]" style={{ fontFamily: SERIF }}>
        {toRoman(n)}
      </span>
    </div>
  );
}

export function WisdomBook({
  open,
  onClose,
  lang,
  t,
  unlocked,
  fresh,
}: {
  open: boolean;
  onClose: () => void;
  lang: LangCode;
  t: UIStrings;
  /** indices found, in the order found */
  unlocked: number[];
  /** indices found since the book was last opened */
  fresh: Set<number>;
}) {
  const quotes = useMemo(() => quotesFor(lang), [lang]);
  const total = quotes.length;
  const found = useMemo(() => new Set(unlocked), [unlocked]);
  const [filter, setFilter] = useState<"all" | "found">("all");
  const [reading, setReading] = useState<number | null>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const readingRef = useRef<number | null>(null);
  readingRef.current = reading;
  const rtl = lang === "ar";

  useEffect(() => {
    if (!open) {
      setReading(null);
      return;
    }
    closeRef.current?.focus();
    const key = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      e.stopPropagation();
      if (readingRef.current !== null) setReading(null);
      else onClose();
    };
    window.addEventListener("keydown", key, true);
    return () => window.removeEventListener("keydown", key, true);
  }, [open, onClose]);

  if (!open) return null;

  // found pages in the order they were found, newest first, when filtered;
  // otherwise the whole book in its own order
  const items =
    filter === "found" ? [...unlocked].reverse() : Array.from({ length: total }, (_, i) => i);
  const count = unlocked.filter((i) => i < total).length;
  const q = reading !== null ? quotes[reading] : null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={t.bookTitle}
      lang={lang}
      className="animate-fade-in absolute inset-0 z-40 flex flex-col bg-[#0b0a08]/92 backdrop-blur-sm"
      // stop the game behind from taking these as drags
      onPointerDown={(e) => e.stopPropagation()}
    >
      {/* header */}
      <div className="flex shrink-0 items-start justify-between gap-4 px-5 pt-4 pb-3 sm:px-8 sm:pt-6">
        <div className="min-w-0">
          <h2
            className="text-2xl font-light tracking-[0.12em] text-[#f1e3c2] sm:text-3xl"
            style={{ fontFamily: SERIF }}
          >
            {t.bookTitle}
          </h2>
          <p className="mt-1 text-[0.65rem] tracking-[0.25em] text-[#c9b48c]/80 uppercase">
            {t.bookSub}
          </p>
          <div className="mt-3 flex items-center gap-3">
            <div className="h-px w-40 bg-[#f1e3c2]/15 sm:w-64">
              <div
                className="h-px bg-[#d9a95b]"
                style={{ width: `${(count / Math.max(1, total)) * 100}%` }}
              />
            </div>
            <span className="text-[0.65rem] tracking-[0.2em] text-[#f1e3c2]/85 tabular-nums">
              {fmt(t.bookCount, { n: count, total })}
            </span>
          </div>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <div className="flex overflow-hidden rounded-full border border-[#f1e3c2]/20 text-[0.6rem] tracking-[0.2em] uppercase">
            {(["all", "found"] as const).map((f) => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                className={`px-3 py-1.5 transition-colors ${
                  filter === f
                    ? "bg-[#f1e3c2]/15 text-[#f1e3c2]"
                    : "text-[#f1e3c2]/55 hover:text-[#f1e3c2]"
                }`}
              >
                {f === "all" ? t.filterAll : t.filterFound}
              </button>
            ))}
          </div>
          <button
            ref={closeRef}
            onClick={onClose}
            aria-label={t.closeBtn}
            className="flex h-8 w-8 items-center justify-center rounded-full border border-[#f1e3c2]/25 text-[#f1e3c2]/80 transition-colors hover:bg-[#f1e3c2]/10 hover:text-[#f1e3c2]"
          >
            ✕
          </button>
        </div>
      </div>

      {/* the pages */}
      <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 pb-8 sm:px-8">
        {count === 0 && filter === "found" ? (
          <p
            className="mt-16 text-center text-lg text-[#f1e3c2]/60 italic"
            style={{ fontFamily: SERIF }}
          >
            {t.bookEmpty}
          </p>
        ) : (
          <div className="grid grid-cols-[repeat(auto-fill,minmax(9.5rem,1fr))] gap-3 sm:gap-4">
            {items.map((i) =>
              found.has(i) && quotes[i] ? (
                <Page
                  key={i}
                  n={i + 1}
                  text={quotes[i]!.text}
                  author={quotes[i]!.author}
                  rtl={rtl}
                  fresh={fresh.has(i)}
                  onOpen={() => setReading(i)}
                  t={t}
                />
              ) : (
                <Blank key={i} n={i + 1} t={t} />
              ),
            )}
          </div>
        )}
      </div>

      {/* one page, read up close */}
      {q && reading !== null && (
        <div
          className="animate-fade-in absolute inset-0 z-10 flex items-center justify-center bg-black/70 p-6"
          onClick={() => setReading(null)}
        >
          <figure
            className="relative flex max-h-full w-full max-w-md flex-col rounded-[4px] px-8 py-9 sm:px-10 sm:py-12"
            style={PAPER}
            onClick={(e) => e.stopPropagation()}
          >
            <span
              className="text-center text-[0.65rem] tracking-[0.35em] text-[#6b4a22]/80 uppercase"
              style={{ fontFamily: SERIF }}
            >
              № {toRoman(reading + 1)}
            </span>
            <Meander className="mx-auto mt-3 h-2 w-24 text-[#6b4a22]/50" />
            <blockquote
              dir={rtl ? "rtl" : undefined}
              className="mt-6 overflow-y-auto text-center text-2xl leading-snug text-[#2b1d0e] italic sm:text-3xl"
              style={{ fontFamily: SERIF }}
            >
              “{q.text}”
            </blockquote>
            <figcaption
              className="mt-6 text-center text-xs tracking-[0.3em] text-[#5a3d1c] uppercase"
              style={{ fontFamily: SERIF }}
            >
              — {q.author}
            </figcaption>
            <button
              onClick={() => setReading(null)}
              className="mx-auto mt-8 rounded-full border border-[#5a3d1c]/40 px-5 py-1.5 text-[0.6rem] tracking-[0.3em] text-[#5a3d1c] uppercase transition-colors hover:bg-[#5a3d1c]/10"
            >
              {t.closeBtn}
            </button>
          </figure>
        </div>
      )}
    </div>
  );
}

/** the little page that slides in when a new one is found */
export function NewPageToast({
  index,
  lang,
  t,
  count,
  total,
  onOpen,
}: {
  index: number;
  lang: LangCode;
  t: UIStrings;
  count: number;
  total: number;
  onOpen: () => void;
}) {
  const q = quotesFor(lang)[index];
  if (!q) return null;
  return (
    <button
      onClick={onOpen}
      className="sisyphus-page-in pointer-events-auto absolute top-16 right-4 z-30 flex w-56 items-center gap-3 rounded-[3px] p-3 text-left sm:top-20 sm:right-6 sm:w-64"
      style={PAPER}
    >
      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-[#6b4a22]/40 text-sm text-[#6b4a22]">
        ✦
      </span>
      <span className="min-w-0">
        <span
          className="block text-[0.6rem] tracking-[0.25em] text-[#8a2a1a] uppercase"
          style={{ fontFamily: SERIF }}
        >
          {t.newPage} · № {toRoman(index + 1)}
        </span>
        <span
          className="mt-0.5 block truncate text-sm text-[#2b1d0e] italic"
          style={{ fontFamily: SERIF }}
        >
          {q.author}
        </span>
        <span className="mt-0.5 block text-[0.55rem] tracking-[0.15em] text-[#5a3d1c]/80 uppercase tabular-nums">
          {t.newPageSub} · {count}/{total}
        </span>
      </span>
    </button>
  );
}
