import { useEffect, useRef, useState } from "react";
import type { Block, StoryNote } from "@/game/dialogue";
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
  merope: "#b9c8ea",
  sis: GOLD,
  pro: "#ef8a5a",
  tan: "#8fb7e0",
  narr: "#bbb",
};

export { Prologue } from "./Prologue";

/**
 * A scene. The climb has stopped; black bars close in over the top and
 * bottom of the frame, and the lines come one at a time, at the player's own
 * pace — a tap, a click, or the space bar. Nothing is missed. When a burden is
 * offered, or she asks for the water, the answers wait under the last line.
 */
export function Scene({
  block,
  lang,
  onDone,
  onAnswer,
  onSpeaker,
}: {
  block: Block;
  lang: LangCode;
  onDone: () => void;
  onAnswer: (yes: boolean) => void;
  onSpeaker: (who: Speaker | null) => void;
}) {
  const st = storyFor(lang);
  const [i, setI] = useState(0);
  const line = block.lines[i];
  const last = i >= block.lines.length - 1;
  const asking = last && (!!block.offer || !!block.choice);
  useEffect(() => {
    onSpeaker(line?.who ?? null);
  }, [line, onSpeaker]);
  useEffect(() => {
    const k = (e: KeyboardEvent) => {
      if (asking) return;
      if (e.key === " " || e.key === "Enter" || e.key === "ArrowRight") {
        e.preventDefault();
        e.stopPropagation();
        if (last) onDone();
        else setI((v) => v + 1);
      }
    };
    window.addEventListener("keydown", k, true);
    return () => window.removeEventListener("keydown", k, true);
  }, [asking, last, onDone]);
  if (!line) return null;
  const narr = line.who === "narr";
  const next = () => {
    if (asking) return;
    if (last) onDone();
    else setI((v) => v + 1);
  };
  return (
    <div className="absolute inset-0 z-[35]" onClick={next} role="dialog" aria-modal="true">
      {/* the frame narrows */}
      <div className="cine-bar-top absolute inset-x-0 top-0 h-[12%] bg-black" />
      <div className="cine-bar-bottom absolute inset-x-0 bottom-0 flex h-[30%] min-h-[150px] flex-col items-center justify-center bg-black px-6">
        <div key={i} className="sisyphus-page-in max-w-3xl text-center">
          {!narr && (
            <span
              className="block text-[0.66rem] tracking-[0.36em] uppercase"
              style={{ fontFamily: SERIF, fontWeight: 600, color: COLOR[line.who] }}
            >
              {st.names[line.who]}
            </span>
          )}
          <span
            className={`mt-1.5 block leading-snug ${narr ? "text-[1rem] italic opacity-75" : "text-[1.22rem] sm:text-[1.45rem]"}`}
            style={{ fontFamily: SERIF, color: narr ? "#cfc6b4" : CREAM }}
          >
            {line.text}
          </span>
          {asking ? (
            <span
              className="mt-4 flex flex-col items-center gap-2"
              onClick={(e) => e.stopPropagation()}
            >
              {block.offer && (
                <span className="text-[0.8rem]" style={{ fontFamily: SERIF, color: GOLD }}>
                  {st.burdens[block.offer].name} — {st.burdens[block.offer].desc}
                </span>
              )}
              <span className="flex gap-3">
                <button
                  onClick={() => onAnswer(true)}
                  className="rounded-full border px-6 py-2 text-[0.72rem] tracking-[0.25em] uppercase"
                  style={{ fontFamily: SERIF, color: CREAM, borderColor: GOLD }}
                >
                  {block.offer ? st.accept : st.bring}
                </button>
                <button
                  onClick={() => onAnswer(false)}
                  className="rounded-full border px-6 py-2 text-[0.72rem] tracking-[0.25em] uppercase opacity-80"
                  style={{ fontFamily: SERIF, color: CREAM, borderColor: "rgba(241,228,196,0.35)" }}
                >
                  {block.offer ? st.decline : st.notYet}
                </button>
              </span>
            </span>
          ) : (
            <span
              className="mt-3 block text-[0.6rem] tracking-[0.3em] uppercase opacity-40"
              style={{ fontFamily: SERIF, color: CREAM }}
            >
              {st.next} ›
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

/**
 * A word in passing, while he walks or climbs: one line at a time, slow
 * enough to read with the hands busy, a tap to move on.
 */
export function Subtitle({
  block,
  lang,
  onDone,
  onSpeaker,
}: {
  block: Block;
  lang: LangCode;
  onDone: () => void;
  onSpeaker: (who: Speaker | null) => void;
}) {
  const st = storyFor(lang);
  const [i, setI] = useState(0);
  const line = block.lines[i];
  useEffect(() => {
    onSpeaker(line?.who ?? null);
  }, [line, onSpeaker]);
  useEffect(() => {
    if (!line) return;
    const id = window.setTimeout(
      () => (i >= block.lines.length - 1 ? onDone() : setI((v) => v + 1)),
      3200 + line.text.length * 60,
    );
    return () => window.clearTimeout(id);
  }, [line, i, block, onDone]);
  if (!line) return null;
  return (
    <div className="pointer-events-none absolute inset-x-0 bottom-[14%] z-30 flex justify-center px-6">
      <div
        key={i}
        className="sisyphus-page-in pointer-events-auto max-w-2xl rounded-[4px] px-5 py-3 text-center"
        style={{ background: "rgba(8, 7, 12, 0.7)", backdropFilter: "blur(6px)" }}
        onClick={() => (i >= block.lines.length - 1 ? onDone() : setI((v) => v + 1))}
      >
        {line.who !== "narr" && (
          <span
            className="block text-[0.6rem] tracking-[0.32em] uppercase"
            style={{ fontFamily: SERIF, fontWeight: 600, color: COLOR[line.who] }}
          >
            {st.names[line.who]}
          </span>
        )}
        <span
          className="mt-1 block text-[1.08rem] leading-snug italic sm:text-[1.2rem]"
          style={{ fontFamily: SERIF, color: CREAM }}
        >
          {line.text}
        </span>
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
    case "mire":
      body = S.mire;
      sub = S.mireDesc;
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
