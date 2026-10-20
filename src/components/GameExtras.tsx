import { useEffect, useState } from "react";
import { Link } from "@tanstack/react-router";
import { CHAPTER_OF, quotesFor } from "@/game/quotes";
import { SEAL_ORDER, SEAL_TEXT, type SealId } from "@/game/seals";
import { renderShareCard, shareCard } from "@/game/share";
import { LANGUAGES, type LangCode, type UIStrings } from "@/i18n";
import { CHAPTER_NAMES } from "@/game/bookText";

/**
 * The pieces around the game that are not the game: the settings panel, the
 * summit's moment with a new page, the seals, the saying of the day, and
 * sharing a saying as a picture.
 */

const SERIF = "'Cormorant Garamond', 'EB Garamond', Garamond, Georgia, serif";
const INK = "#2a1c0f";
const SEPIA = "#6b4a22";
const GOLD = "#c9a15a";
const WAX = "#8e2b1e";

const PAPER: React.CSSProperties = {
  backgroundColor: "#ecdfc2",
  backgroundImage: [
    "radial-gradient(circle at 72% 30%, rgba(255, 252, 240, 0.5) 0, transparent 45%)",
    "radial-gradient(ellipse at center, rgba(0,0,0,0) 58%, rgba(120, 78, 30, 0.28) 100%)",
    "linear-gradient(170deg, #f3e9d1 0%, #e9dbb9 55%, #e0cda4 100%)",
  ].join(","),
};

/** a wax seal; dull and unpressed until earned */
export function SealMark({ earned, size = 34 }: { earned: boolean; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true">
      <path
        d="M16 1.5c2 0 2.6 1.6 4.3 2.1 1.8.5 3.3-.4 4.7.8 1.3 1.2.8 2.9 1.5 4.6.7 1.6 2.3 2.4 2.3 4.4s-1.6 2.6-2 4.3c-.5 1.8.4 3.3-.8 4.7-1.2 1.3-2.9.8-4.6 1.5-1.6.7-2.4 2.3-4.4 2.3s-2.7-1.6-4.4-2.2c-1.8-.5-3.3.4-4.6-.8-1.3-1.2-.8-2.9-1.5-4.6C3.8 17.9 2.2 17.2 2.2 15.2s1.6-2.7 2.1-4.4c.5-1.8-.4-3.3.8-4.6C6.3 4.9 8 5.4 9.7 4.7 11.3 4 12 1.5 16 1.5z"
        fill={earned ? WAX : "rgba(241,228,196,0.08)"}
        stroke={earned ? "rgba(0,0,0,0.25)" : "rgba(241,228,196,0.25)"}
        strokeWidth="0.8"
      />
      <circle
        cx="16"
        cy="15.6"
        r="8.2"
        fill="none"
        stroke={earned ? "#e8c9a0" : "rgba(241,228,196,0.2)"}
        strokeWidth="0.7"
        opacity={earned ? 0.8 : 1}
      />
      <path
        d="M16 10.2l1.6 3.4 3.7.4-2.8 2.5.8 3.6-3.3-1.9-3.3 1.9.8-3.6-2.8-2.5 3.7-.4z"
        fill={earned ? "#e8c9a0" : "rgba(241,228,196,0.18)"}
      />
    </svg>
  );
}

function ShareIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <path d="M8 1.5v9M4.5 5L8 1.5 11.5 5" stroke="currentColor" strokeWidth="1.3" />
      <path d="M3 8.5v5h10v-5" stroke="currentColor" strokeWidth="1.3" />
    </svg>
  );
}

/** a button that turns a saying into a picture and shares it */
export function ShareButton({
  index,
  lang,
  t,
  className = "",
  style,
}: {
  index: number;
  lang: LangCode;
  t: UIStrings;
  className?: string;
  style?: React.CSSProperties;
}) {
  const [busy, setBusy] = useState(false);
  const go = async (e: React.MouseEvent) => {
    e.stopPropagation();
    const q = quotesFor(lang)[index];
    if (!q || busy) return;
    setBusy(true);
    try {
      const chapter = CHAPTER_NAMES[lang][CHAPTER_OF[index] ?? 0] ?? "";
      const blob = await renderShareCard({
        text: q.text,
        author: q.author,
        chapter,
        rtl: lang === "ar",
      });
      await shareCard(blob, `${q.text} — ${q.author}`);
    } catch {
      /* the share sheet was dismissed or is unavailable */
    } finally {
      setBusy(false);
    }
  };
  return (
    <button
      onClick={(e) => void go(e)}
      disabled={busy}
      className={`inline-flex items-center gap-1.5 rounded-full border px-4 py-1.5 text-[0.62rem] tracking-[0.28em] uppercase transition-colors disabled:opacity-50 ${className}`}
      style={{ fontFamily: SERIF, fontWeight: 600, ...style }}
    >
      <ShareIcon /> {t.share}
    </button>
  );
}

/**
 * The moment a page is found. When the walk down begins the new saying stands
 * in the middle of the frame, large, for a few breaths — then it folds away
 * toward the book in the corner, where it has been filed.
 */
export function SummitCeremony({
  index,
  lang,
  t,
  count,
  total,
  onOpen,
  onDone,
}: {
  index: number;
  lang: LangCode;
  t: UIStrings;
  count: number;
  total: number;
  onOpen: () => void;
  onDone: () => void;
}) {
  const [leaving, setLeaving] = useState(false);
  useEffect(() => {
    const a = window.setTimeout(() => setLeaving(true), 4200);
    const b = window.setTimeout(onDone, 5000);
    return () => {
      window.clearTimeout(a);
      window.clearTimeout(b);
    };
  }, [onDone]);
  const q = quotesFor(lang)[index];
  if (!q) return null;
  const chapter = CHAPTER_NAMES[lang][CHAPTER_OF[index] ?? 0];
  return (
    <div
      className={`pointer-events-none absolute inset-0 z-30 flex items-center justify-center px-8 ${
        leaving ? "ceremony-leave" : "ceremony-enter"
      }`}
      style={{
        background: "radial-gradient(ellipse at center, rgba(8,6,10,0.62) 0%, rgba(8,6,10,0) 70%)",
      }}
    >
      <div
        className="pointer-events-auto max-w-2xl cursor-pointer text-center"
        onClick={onOpen}
        role="button"
        dir={lang === "ar" ? "rtl" : undefined}
      >
        <p
          className="text-[0.65rem] tracking-[0.4em] uppercase"
          style={{ fontFamily: SERIF, fontWeight: 600, color: GOLD }}
        >
          {t.newPage} · № {index + 1} · {chapter}
        </p>
        <p
          className="mt-4 text-[1.7rem] leading-snug text-[#f6ead0] italic sm:text-[2.3rem] [@media(max-height:500px)]:text-[1.45rem]"
          style={{ fontFamily: SERIF, fontWeight: 500, textShadow: "0 2px 18px rgba(0,0,0,0.65)" }}
        >
          {q.text}
        </p>
        <p
          className="mt-4 text-[0.78rem] tracking-[0.32em] uppercase"
          style={{ fontFamily: SERIF, fontWeight: 600, color: "#e9d3a4" }}
        >
          {q.author}
        </p>
        <p className="mt-3 text-[0.6rem] tracking-[0.25em] text-[#e9d3a4]/60 uppercase tabular-nums">
          {t.newPageSub} · {count}/{total}
        </p>
        <div className="mt-4 flex justify-center">
          <ShareButton
            index={index}
            lang={lang}
            t={t}
            className="border-[#e9d3a4]/40 text-[#e9d3a4] hover:bg-white/10"
          />
        </div>
      </div>
    </div>
  );
}

/** a seal just pressed: drops in at the top of the frame */
export function SealToast({ id, lang, t }: { id: SealId; lang: LangCode; t: UIStrings }) {
  const s = SEAL_TEXT[id][lang];
  return (
    <div
      className="sisyphus-page-in pointer-events-none absolute top-16 left-1/2 z-30 flex -translate-x-1/2 items-center gap-3 rounded-[3px] px-4 py-2.5"
      style={{
        ...PAPER,
        boxShadow: "0 18px 30px -12px rgba(0,0,0,0.75), inset 0 0 0 1px rgba(107,74,34,0.3)",
      }}
    >
      <SealMark earned size={34} />
      <span>
        <span
          className="block text-[0.58rem] tracking-[0.28em] uppercase"
          style={{ fontFamily: SERIF, fontWeight: 600, color: WAX }}
        >
          {t.sealEarned}
        </span>
        <span
          className="block text-[1.05rem]"
          style={{ fontFamily: SERIF, fontWeight: 600, color: INK }}
        >
          {s.name}
        </span>
        <span className="block text-[0.72rem] italic" style={{ fontFamily: SERIF, color: SEPIA }}>
          {s.desc}
        </span>
      </span>
    </div>
  );
}

/** every seal, earned or still to win */
export function SealsGrid({ earned, lang }: { earned: Set<string>; lang: LangCode }) {
  return (
    <div className="grid grid-cols-[repeat(auto-fill,minmax(13rem,1fr))] gap-3">
      {SEAL_ORDER.map((id) => {
        const on = earned.has(id);
        const s = SEAL_TEXT[id][lang];
        return (
          <div
            key={id}
            className="flex items-center gap-3 rounded-[4px] border px-3 py-2.5"
            style={{
              borderColor: on ? "rgba(201,161,90,0.45)" : "rgba(241,228,196,0.1)",
              background: on ? "rgba(201,161,90,0.08)" : "transparent",
            }}
          >
            <SealMark earned={on} size={38} />
            <span className="min-w-0">
              <span
                className="block text-[1rem]"
                style={{
                  fontFamily: SERIF,
                  fontWeight: 600,
                  color: on ? "#f6ead0" : "rgba(241,228,196,0.55)",
                }}
              >
                {s.name}
              </span>
              <span
                className="block text-[0.78rem] leading-snug italic"
                style={{
                  fontFamily: SERIF,
                  color: on ? "rgba(241,228,196,0.75)" : "rgba(241,228,196,0.4)",
                }}
              >
                {s.desc}
              </span>
            </span>
          </div>
        );
      })}
    </div>
  );
}

/** the saying of the day, as a small page on the start screen */
export function DailyQuote({
  index,
  lang,
  t,
  added,
}: {
  index: number;
  lang: LangCode;
  t: UIStrings;
  added: boolean;
}) {
  const q = quotesFor(lang)[index];
  if (!q) return null;
  return (
    <div
      className="animate-fade-in relative w-full max-w-md rounded-[3px] px-6 py-4 text-center [animation-delay:250ms] [animation-fill-mode:backwards] [@media(max-height:500px)]:py-2.5"
      style={{
        ...PAPER,
        boxShadow: "0 16px 30px -14px rgba(0,0,0,0.8), inset 0 0 0 1px rgba(107,74,34,0.3)",
      }}
      dir={lang === "ar" ? "rtl" : undefined}
    >
      <p
        className="text-[0.58rem] tracking-[0.35em] uppercase"
        style={{ fontFamily: SERIF, fontWeight: 600, color: WAX }}
      >
        {t.dailyTitle}
      </p>
      <p
        className="mt-1.5 text-[1.12rem] leading-snug italic [@media(max-height:500px)]:text-[0.98rem]"
        style={{ fontFamily: SERIF, fontWeight: 500, color: INK }}
      >
        {q.text}
      </p>
      <p
        className="mt-1 text-[0.62rem] tracking-[0.25em] uppercase"
        style={{ fontFamily: SERIF, fontWeight: 600, color: SEPIA }}
      >
        {q.author}
      </p>
      <div className="mt-2 flex items-center justify-center gap-3">
        {added && (
          <span
            className="text-[0.58rem] tracking-[0.15em] italic"
            style={{ fontFamily: SERIF, color: SEPIA }}
          >
            ✓ {t.dailyAdded}
          </span>
        )}
        <ShareButton
          index={index}
          lang={lang}
          t={t}
          className="px-3 py-1 hover:bg-[#6b4a22]/10"
          style={{ borderColor: "rgba(107,74,34,0.4)", color: SEPIA }}
        />
      </div>
    </div>
  );
}

function Toggle({ on, onChange, label }: { on: boolean; onChange: () => void; label: string }) {
  return (
    <button
      role="switch"
      aria-checked={on}
      aria-label={label}
      onClick={onChange}
      className="relative h-6 w-11 shrink-0 rounded-full border transition-colors"
      style={{
        borderColor: on ? GOLD : "rgba(241,228,196,0.25)",
        background: on ? "rgba(201,161,90,0.35)" : "transparent",
      }}
    >
      <span
        className="absolute top-0.5 h-[18px] w-[18px] rounded-full transition-all"
        style={{ left: on ? 22 : 2, background: on ? "#f1e4c4" : "rgba(241,228,196,0.5)" }}
      />
    </button>
  );
}

/** the game's settings, in one place instead of strung across the top edge */
export function SettingsPanel({
  open,
  onClose,
  t,
  lang,
  onLang,
  muted,
  onMute,
  vibrates,
  haptics,
  onHaptics,
  calm,
  onCalm,
  seals,
}: {
  open: boolean;
  onClose: () => void;
  t: UIStrings;
  lang: LangCode;
  onLang: (l: LangCode) => void;
  muted: boolean;
  onMute: () => void;
  vibrates: boolean;
  haptics: boolean;
  onHaptics: () => void;
  calm: boolean;
  onCalm: () => void;
  seals: Set<string>;
}) {
  useEffect(() => {
    if (!open) return;
    const k = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        onClose();
      }
    };
    window.addEventListener("keydown", k, true);
    return () => window.removeEventListener("keydown", k, true);
  }, [open, onClose]);
  if (!open) return null;
  const row = "flex items-center justify-between gap-6 border-b border-[#f1e4c4]/10 py-3";
  const label = "text-[1.02rem]";
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={t.settings}
      className="animate-fade-in absolute inset-0 z-40 flex justify-end bg-black/55"
      onClick={onClose}
      onPointerDown={(e) => e.stopPropagation()}
    >
      <div
        className="flex h-full w-full max-w-md flex-col overflow-y-auto px-6 py-5 text-[#efe3c6] sm:px-8"
        style={{
          background: "linear-gradient(170deg, #1d1712, #0e0b09)",
          boxShadow: "-20px 0 40px rgba(0,0,0,0.6)",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between">
          <h2
            className="text-xl tracking-[0.2em] uppercase"
            style={{ fontFamily: SERIF, fontWeight: 600 }}
          >
            {t.settings}
          </h2>
          <button
            onClick={onClose}
            aria-label={t.closeBtn}
            className="flex h-9 w-9 items-center justify-center rounded-full border border-[#f1e4c4]/25 text-[#f1e4c4]/80 hover:text-[#f1e4c4]"
          >
            <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true">
              <path d="M1 1 L11 11 M11 1 L1 11" stroke="currentColor" strokeWidth="1.4" />
            </svg>
          </button>
        </div>
        <div className="mt-3" style={{ fontFamily: SERIF }}>
          <div className={row}>
            <span className={label}>{t.language}</span>
            <select
              value={lang}
              onChange={(e) => onLang(e.target.value as LangCode)}
              className="rounded border border-[#f1e4c4]/25 bg-transparent px-2 py-1 text-sm text-[#efe3c6] outline-none"
            >
              {LANGUAGES.map((l) => (
                <option key={l.code} value={l.code} className="bg-[#1d1712]">
                  {l.native}
                </option>
              ))}
            </select>
          </div>
          <div className={row}>
            <span className={label}>{t.sound}</span>
            <Toggle on={!muted} onChange={onMute} label={t.sound} />
          </div>
          {vibrates && (
            <div className={row}>
              <span className={label}>{t.vibration}</span>
              <Toggle on={haptics} onChange={onHaptics} label={t.vibration} />
            </div>
          )}
          <div className={row}>
            <span>
              <span className={`block ${label}`}>{t.calmMode}</span>
              <span className="block text-[0.82rem] text-[#f1e4c4]/55 italic">{t.calmDesc}</span>
            </span>
            <Toggle on={calm} onChange={onCalm} label={t.calmMode} />
          </div>
        </div>
        <h3
          className="mt-7 mb-3 text-[0.65rem] tracking-[0.32em] uppercase"
          style={{ fontFamily: SERIF, fontWeight: 600, color: GOLD }}
        >
          {t.sealsTitle} · {seals.size}/{SEAL_ORDER.length}
        </h3>
        <SealsGrid earned={seals} lang={lang} />
        <Link
          to="/privacy"
          className="mt-8 self-start text-[0.62rem] tracking-[0.25em] text-[#f1e4c4]/50 uppercase hover:text-[#f1e4c4]"
        >
          {t.privacy}
        </Link>
      </div>
    </div>
  );
}
