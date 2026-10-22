import { useEffect, useRef, useState } from "react";
import type { Block, StoryNote } from "@/game/dialogue";
import type { LangCode } from "@/i18n";
import { ROMAN, STAGE_TEXT, STORY } from "@/game/journey";
import { storyFor, type BurdenId, type Speaker } from "@/game/story";
import { Reveal } from "./Reveal";
import { revealTime } from "./reveal-time";

/**
 * The story on screen: the prologue, the subtitles of whoever is speaking on
 * the hill (with the choice when someone offers a burden), the small notes
 * that mark what happened — an item taken, a stage reached, a gift earned —
 * the words that come at the ending, and the end card.
 */

const SERIF = "'Marcellus', 'Cormorant Garamond', Georgia, serif";
const TITLE = "'Cinzel', 'Cormorant Garamond', Georgia, serif";
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
  // whether the line has finished coming in; a tap before that shows it whole
  const [shown, setShown] = useState(false);
  const line = block.lines[i];
  const last = i >= block.lines.length - 1;
  const asking = last && (!!block.offer || !!block.choice);
  useEffect(() => {
    onSpeaker(line?.who ?? null);
  }, [line, onSpeaker]);
  useEffect(() => {
    setShown(false);
    if (!line) return;
    const id = window.setTimeout(() => setShown(true), revealTime(line.text, 42) + 250);
    return () => window.clearTimeout(id);
  }, [line]);
  const next = () => {
    if (!shown) {
      setShown(true);
      return;
    }
    if (asking) return;
    if (last) onDone();
    else setI((v) => v + 1);
  };
  const nextRef = useRef(next);
  nextRef.current = next;
  useEffect(() => {
    const k = (e: KeyboardEvent) => {
      if (e.key === " " || e.key === "Enter" || e.key === "ArrowRight") {
        e.preventDefault();
        e.stopPropagation();
        nextRef.current();
      }
    };
    window.addEventListener("keydown", k, true);
    return () => window.removeEventListener("keydown", k, true);
  }, []);
  if (!line) return null;
  const narr = line.who === "narr";
  const tint = COLOR[line.who];
  return (
    <div className="absolute inset-0 z-[35]" onClick={next} role="dialog" aria-modal="true">
      {/* the frame narrows */}
      <div className="cine-bar-top absolute inset-x-0 top-0 h-[12%] bg-black" />
      <div className="cine-bar-bottom absolute inset-x-0 bottom-0 flex h-[30%] min-h-[150px] flex-col items-center justify-center bg-black px-6">
        {/* a line of the speaker's colour along the edge of the frame */}
        <span
          key={`edge-${line.who}`}
          className="scene-edge absolute inset-x-0 top-0 h-px"
          style={{
            background: `linear-gradient(90deg, transparent, ${narr ? "#777" : tint}, transparent)`,
          }}
        />
        <div key={i} className="max-w-3xl text-center">
          {!narr && (
            <span
              className="scene-name flex items-center justify-center gap-3 text-[0.7rem] uppercase"
              style={{ fontFamily: TITLE, fontWeight: 700, color: tint }}
            >
              <span className="h-px w-6 bg-current opacity-50" />
              {st.names[line.who]}
              <span className="h-px w-6 bg-current opacity-50" />
            </span>
          )}
          <Reveal
            text={line.text}
            step={42}
            delay={narr ? 100 : 260}
            instant={shown}
            style={{ fontFamily: SERIF, color: narr ? "#cfc6b4" : CREAM }}
            className={`mt-2 block leading-snug ${narr ? "text-[1.02rem] italic opacity-80" : "text-[1.22rem] sm:text-[1.45rem]"}`}
          />
          {asking && shown ? (
            <span
              className="mt-4 flex flex-col items-center gap-2"
              onClick={(e) => e.stopPropagation()}
            >
              {block.offer && (
                <span
                  className="scene-choice text-[0.8rem]"
                  style={{ fontFamily: SERIF, color: GOLD }}
                >
                  {st.burdens[block.offer].name} — {st.burdens[block.offer].desc}
                </span>
              )}
              <span className="flex gap-3">
                <button
                  onClick={() => onAnswer(true)}
                  className="scene-choice scene-btn rounded-full border px-6 py-2 text-[0.72rem] tracking-[0.25em] uppercase"
                  style={{
                    fontFamily: TITLE,
                    color: CREAM,
                    borderColor: GOLD,
                    animationDelay: "120ms",
                  }}
                >
                  {block.offer ? st.accept : st.bring}
                </button>
                <button
                  onClick={() => onAnswer(false)}
                  className="scene-choice scene-btn rounded-full border px-6 py-2 text-[0.72rem] tracking-[0.25em] uppercase opacity-80"
                  style={{
                    fontFamily: TITLE,
                    color: CREAM,
                    borderColor: "rgba(241,228,196,0.35)",
                    animationDelay: "240ms",
                  }}
                >
                  {block.offer ? st.decline : st.notYet}
                </button>
              </span>
            </span>
          ) : (
            <span
              className={`mt-3 block text-[0.6rem] tracking-[0.3em] uppercase ${shown ? "scene-next" : "opacity-0"}`}
              style={{ fontFamily: TITLE, color: CREAM }}
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
        className="subtitle-in pointer-events-auto max-w-2xl rounded-[4px] px-5 py-3 text-center"
        style={{
          background: "linear-gradient(180deg, rgba(8, 7, 12, 0.55), rgba(8, 7, 12, 0.78))",
          backdropFilter: "blur(6px)",
          borderTop: `1px solid ${line.who === "narr" ? "rgba(255,255,255,0.12)" : COLOR[line.who]}55`,
        }}
        onClick={() => (i >= block.lines.length - 1 ? onDone() : setI((v) => v + 1))}
      >
        {line.who !== "narr" && (
          <span
            className="block text-[0.6rem] tracking-[0.32em] uppercase"
            style={{ fontFamily: TITLE, fontWeight: 700, color: COLOR[line.who] }}
          >
            {st.names[line.who]}
          </span>
        )}
        <span
          className="mt-1 block text-[1.08rem] leading-snug sm:text-[1.2rem]"
          style={{ fontFamily: SERIF, color: CREAM }}
        >
          <Reveal text={line.text} step={55} delay={150} />
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
      className="toast-in pointer-events-none absolute top-16 left-1/2 z-30 w-max max-w-[85vw] -translate-x-1/2 rounded-[3px] px-5 py-2.5 text-center"
      style={{
        background: "rgba(10, 8, 14, 0.62)",
        backdropFilter: "blur(6px)",
        boxShadow: "0 0 0 1px rgba(217,180,106,0.18), 0 8px 30px rgba(0,0,0,0.4)",
      }}
    >
      {head && (
        <span
          className="block text-[0.58rem] tracking-[0.3em] uppercase"
          style={{ fontFamily: TITLE, fontWeight: 700, color: GOLD }}
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
    <div className="pointer-events-none absolute inset-x-0 top-[12%] z-30 flex justify-center px-6">
      {/* light breaking over him as the words come */}
      <div className="epiphany-light absolute top-[-30%] left-1/2 h-[90vh] w-[90vw] -translate-x-1/2" />
      <div className="relative text-center">
        <p
          className="text-[1.6rem] sm:text-[2.3rem]"
          style={{ fontFamily: SERIF, color: CREAM, textShadow: "0 2px 18px rgba(0,0,0,0.75)" }}
        >
          <Reveal text={S.camus} step={260} delay={3000} />
        </p>
        <span className="epiphany-rule mx-auto mt-3 block h-px bg-[#d9b46a]" />
        <p
          className="epiphany-by mt-3 text-[0.72rem] tracking-[0.4em] uppercase"
          style={{ fontFamily: TITLE, color: GOLD, textShadow: "0 2px 12px rgba(0,0,0,0.8)" }}
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
  useEffect(() => {
    const id = window.setTimeout(() => ref.current?.focus(), 3200);
    return () => window.clearTimeout(id);
  }, []);
  const title = S.end.toUpperCase();
  return (
    <div
      className="menu-veil absolute inset-0 z-[45] flex flex-col items-center justify-center gap-6 overflow-hidden px-8 text-center"
      role="dialog"
      aria-modal="true"
    >
      <div className="endcard-glow pointer-events-none absolute inset-0" aria-hidden="true" />
      <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden="true">
        {Array.from({ length: 18 }, (_, i) => (
          <span
            key={i}
            className="menu-ember"
            style={{
              left: `${(i * 53) % 100}%`,
              animationDelay: `${(i * 0.7) % 8}s`,
              animationDuration: `${9 + ((i * 5) % 6)}s`,
            }}
          />
        ))}
      </div>
      <p
        className="relative text-[2.4rem] tracking-[0.4em] sm:text-[3.4rem]"
        style={{ fontFamily: TITLE, color: CREAM, fontWeight: 700, marginRight: "-0.4em" }}
        aria-label={title}
      >
        {Array.from(title).map((ch, i) => (
          <span
            key={i}
            className="prologue-letter"
            style={{ animationDelay: `${200 + i * 120}ms` }}
          >
            {ch}
          </span>
        ))}
      </p>
      <span className="prologue-line relative -mt-3 h-px bg-[#d9b46a]" />
      <p className="relative max-w-xl text-[1.35rem]" style={{ fontFamily: SERIF, color: CREAM }}>
        <Reveal text={`“${S.camus}”`} step={140} delay={1500} />
      </p>
      <p
        className="menu-rise relative max-w-xl text-[0.98rem] opacity-80 [animation-delay:2.6s]"
        style={{ fontFamily: SERIF, color: "#d8c8a4" }}
      >
        {S.endNote}
      </p>
      <div className="relative mt-2 flex flex-wrap justify-center gap-x-8 gap-y-2">
        {stats.map(([k, v], i) => (
          <span
            key={k}
            className="menu-rise flex flex-col"
            style={{ animationDelay: `${3000 + i * 180}ms` }}
          >
            <span
              className="text-[0.58rem] tracking-[0.28em] uppercase"
              style={{ fontFamily: TITLE, color: GOLD }}
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
        className="menu-start relative mt-4 rounded-full border px-8 py-2.5 text-[0.75rem] tracking-[0.35em] uppercase [animation-delay:3.6s,4.6s]"
        style={{ fontFamily: TITLE, color: CREAM, borderColor: GOLD }}
      >
        {S.continue}
      </button>
    </div>
  );
}
