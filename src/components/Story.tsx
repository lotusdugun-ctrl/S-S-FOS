import { useEffect, useRef, useState } from "react";
import type { Line, StoryNote } from "@/game/dialogue";
import type { LangCode } from "@/i18n";
import { ROMAN, STAGE_TEXT, STORY } from "@/game/journey";
import { storyFor, type BurdenId, type Speaker } from "@/game/story";

/**
 * The story on screen: the prologue, the subtitles of whoever is speaking on
 * the hill (with the choice when someone offers a burden), the small notes
 * that mark what happened — an item taken, a stage reached, a gift earned —
 * the words that come at the ending, and the end card.
 */

const SERIF = "'Cormorant Garamond', 'EB Garamond', Garamond, Georgia, serif";
const CREAM = "#f1e4c4";
const GOLD = "#c9a15a";

const COLOR: Record<Speaker, string> = {
  ione: "#e7a99a",
  lykos: "#a9d08e",
  sis: GOLD,
  pro: "#ef8a5a",
  tan: "#8fb7e0",
};

/** the opening: three cards out of the dark, before the first climb */
export function Prologue({ lang, onDone }: { lang: LangCode; onDone: () => void }) {
  const S = STORY[lang];
  const [i, setI] = useState(0);
  const n = S.prologue.length;
  useEffect(() => {
    if (i >= n) return;
    const id = window.setTimeout(() => setI((v) => v + 1), 4200);
    return () => window.clearTimeout(id);
  }, [i, n]);
  return (
    <div
      className="absolute inset-0 z-[45] flex flex-col items-center justify-center bg-black px-8 text-center"
      onClick={() => (i < n ? setI((v) => v + 1) : onDone())}
      role="dialog"
      aria-modal="true"
    >
      {i < n ? (
        <p
          key={i}
          className="sisyphus-page-in max-w-2xl text-[1.5rem] leading-relaxed italic sm:text-[1.9rem]"
          style={{ fontFamily: SERIF, color: CREAM }}
        >
          {S.prologue[i]}
        </p>
      ) : (
        <div className="sisyphus-page-in flex flex-col items-center gap-8">
          <p
            className="text-[2.4rem] tracking-[0.45em] sm:text-[3.2rem]"
            style={{ fontFamily: SERIF, color: CREAM, fontWeight: 600 }}
          >
            SISYPHUS
          </p>
          <button
            onClick={(e) => {
              e.stopPropagation();
              onDone();
            }}
            className="rounded-full border px-8 py-2.5 text-[0.75rem] tracking-[0.35em] uppercase"
            style={{ fontFamily: SERIF, color: CREAM, borderColor: "rgba(241,228,196,0.4)" }}
          >
            {S.begin}
          </button>
        </div>
      )}
      {i < n && (
        <button
          onClick={(e) => {
            e.stopPropagation();
            onDone();
          }}
          className="absolute right-6 bottom-6 text-[0.65rem] tracking-[0.3em] uppercase opacity-60 hover:opacity-100"
          style={{ fontFamily: SERIF, color: CREAM }}
        >
          {S.skip}
        </button>
      )}
    </div>
  );
}

/**
 * Subtitles. One line at a time, timed to its length, a tap to move on. When
 * a burden is offered, the two answers wait under the last line of the offer.
 */
export function Dialogue({
  queue,
  lang,
  offer,
  onAnswer,
  onShift,
  onSpeaker,
}: {
  queue: Line[];
  lang: LangCode;
  offer: BurdenId | null;
  onAnswer: (accept: boolean) => void;
  onShift: () => void;
  onSpeaker: (who: Speaker | null) => void;
}) {
  const st = storyFor(lang);
  const line = queue[0];
  const waiting = !!offer && queue.length === 1;
  useEffect(() => {
    onSpeaker(line?.who ?? null);
  }, [line, onSpeaker]);
  useEffect(() => {
    if (!line) return;
    // an offer left unanswered is a refusal, after a while
    const ms = waiting ? 11000 : 2400 + line.text.length * 48;
    const id = window.setTimeout(() => (waiting ? onAnswer(false) : onShift()), ms);
    return () => window.clearTimeout(id);
  }, [line, waiting, onShift, onAnswer]);
  if (!line) return null;
  return (
    <div className="pointer-events-none absolute inset-x-0 bottom-[13%] z-30 flex justify-center px-6">
      <div
        key={line.text}
        className="sisyphus-page-in pointer-events-auto max-w-2xl rounded-[4px] px-5 py-3 text-center"
        style={{ background: "rgba(10, 8, 14, 0.62)", backdropFilter: "blur(6px)" }}
        onClick={() => !waiting && onShift()}
      >
        <span
          className="block text-[0.6rem] tracking-[0.32em] uppercase"
          style={{ fontFamily: SERIF, fontWeight: 600, color: COLOR[line.who] }}
        >
          {st.names[line.who]}
        </span>
        <span
          className="mt-1 block text-[1.08rem] leading-snug italic sm:text-[1.2rem]"
          style={{ fontFamily: SERIF, color: CREAM }}
        >
          {line.text}
        </span>
        {waiting && offer && (
          <span className="mt-3 flex flex-col items-center gap-2">
            <span className="text-[0.75rem] not-italic" style={{ fontFamily: SERIF, color: GOLD }}>
              {st.burdens[offer].name} — {st.burdens[offer].desc}
            </span>
            <span className="flex gap-3">
              <button
                onClick={() => onAnswer(true)}
                className="rounded-full border px-5 py-1.5 text-[0.7rem] tracking-[0.25em] uppercase"
                style={{ fontFamily: SERIF, color: CREAM, borderColor: GOLD }}
              >
                {st.accept}
              </button>
              <button
                onClick={() => onAnswer(false)}
                className="rounded-full border px-5 py-1.5 text-[0.7rem] tracking-[0.25em] uppercase opacity-80"
                style={{ fontFamily: SERIF, color: CREAM, borderColor: "rgba(241,228,196,0.35)" }}
              >
                {st.decline}
              </button>
            </span>
          </span>
        )}
      </div>
    </div>
  );
}

/** a short note at the top of the screen */
export function StoryToast({ note, lang }: { note: StoryNote; lang: LangCode }) {
  const st = storyFor(lang);
  const S = STORY[lang];
  let head = "";
  let body = "";
  let sub = "";
  switch (note.kind) {
    case "took":
      head = st.took;
      body = st.items[note.item];
      break;
    case "stage":
      head = `${S.newStage} · ${S.act} ${ROMAN[note.stage - 1]}`;
      body = STAGE_TEXT[lang][note.stage - 1]!.name;
      sub = STAGE_TEXT[lang][note.stage - 1]!.desc;
      break;
    case "passive":
      head = "✦";
      body = st.passives[note.id].name;
      sub = st.passives[note.id].desc;
      break;
    case "burden":
      head = st.burdens[note.id].name;
      body = st.burdens[note.id].desc;
      break;
    case "wedge":
      body = st.wedgeHeld;
      break;
  }
  return (
    <div
      className="sisyphus-page-in pointer-events-none absolute top-16 left-1/2 z-30 w-max max-w-[85vw] -translate-x-1/2 rounded-[3px] px-5 py-2.5 text-center"
      style={{ background: "rgba(10, 8, 14, 0.6)", backdropFilter: "blur(6px)" }}
    >
      {head && (
        <span
          className="block text-[0.58rem] tracking-[0.3em] uppercase"
          style={{ fontFamily: SERIF, fontWeight: 600, color: GOLD }}
        >
          {head}
        </span>
      )}
      <span
        className="block text-[1.1rem]"
        style={{ fontFamily: SERIF, fontWeight: 600, color: CREAM }}
      >
        {body}
      </span>
      {sub && (
        <span
          className="block text-[0.78rem] italic"
          style={{ fontFamily: SERIF, color: "#d8c8a4" }}
        >
          {sub}
        </span>
      )}
    </div>
  );
}

/** the words at the ending's summit, over the close-up of his face */
export function Epiphany({ lang }: { lang: LangCode }) {
  const S = STORY[lang];
  return (
    <div className="pointer-events-none absolute inset-x-0 top-[14%] z-30 flex justify-center px-6">
      <div className="epiphany-in text-center">
        <p
          className="text-[1.6rem] italic sm:text-[2.1rem]"
          style={{ fontFamily: SERIF, color: CREAM, textShadow: "0 2px 18px rgba(0,0,0,0.75)" }}
        >
          {S.camus}
        </p>
        <p
          className="mt-2 text-[0.7rem] tracking-[0.35em] uppercase"
          style={{ fontFamily: SERIF, color: GOLD, textShadow: "0 2px 12px rgba(0,0,0,0.8)" }}
        >
          {S.camusBy}
        </p>
      </div>
    </div>
  );
}

/** the end card: the story is over, the hill is not */
export function EndCard({
  lang,
  stats,
  onContinue,
}: {
  lang: LangCode;
  stats: Array<[string, string]>;
  onContinue: () => void;
}) {
  const S = STORY[lang];
  const ref = useRef<HTMLButtonElement>(null);
  useEffect(() => ref.current?.focus(), []);
  return (
    <div
      className="animate-fade-in absolute inset-0 z-[45] flex flex-col items-center justify-center gap-6 bg-black/85 px-8 text-center"
      role="dialog"
      aria-modal="true"
    >
      <p
        className="text-[2.6rem] tracking-[0.4em] sm:text-[3.4rem]"
        style={{ fontFamily: SERIF, color: CREAM, fontWeight: 600 }}
      >
        {S.end.toUpperCase()}
      </p>
      <p className="max-w-xl text-[1.3rem] italic" style={{ fontFamily: SERIF, color: CREAM }}>
        “{S.camus}”
      </p>
      <p
        className="max-w-xl text-[0.95rem] italic opacity-80"
        style={{ fontFamily: SERIF, color: "#d8c8a4" }}
      >
        {S.endNote}
      </p>
      <div className="mt-2 flex flex-wrap justify-center gap-x-8 gap-y-2">
        {stats.map(([k, v]) => (
          <span key={k} className="flex flex-col">
            <span
              className="text-[0.58rem] tracking-[0.28em] uppercase"
              style={{ fontFamily: SERIF, color: GOLD }}
            >
              {k}
            </span>
            <span className="text-[1.3rem]" style={{ fontFamily: SERIF, color: CREAM }}>
              {v}
            </span>
          </span>
        ))}
      </div>
      <button
        ref={ref}
        onClick={onContinue}
        className="mt-4 rounded-full border px-8 py-2.5 text-[0.75rem] tracking-[0.35em] uppercase"
        style={{ fontFamily: SERIF, color: CREAM, borderColor: GOLD }}
      >
        {S.continue}
      </button>
    </div>
  );
}
