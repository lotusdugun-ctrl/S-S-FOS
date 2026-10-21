import { useCallback, useEffect, useState } from "react";
import type { LangCode } from "@/i18n";
import type { Stage } from "./journey";
import {
  COMPANIONS,
  EPISODES,
  storyFor,
  type BurdenId,
  type ItemId,
  type PassiveId,
  type Speaker,
} from "./story";
import type { TalkRef } from "./saga";

export type StoryNote =
  | { kind: "took"; item: ItemId }
  | { kind: "stage"; stage: Stage }
  | { kind: "passive"; id: PassiveId }
  | { kind: "burden"; id: BurdenId }
  | { kind: "wedge" };

export type Line = { who: Speaker; text: string };

/** the words for a reference, in a language */
export function linesFor(ref: TalkRef, lang: LangCode): Line[] {
  const st = storyFor(lang);
  const zip = (who: Speaker[], texts: string[]) =>
    texts.map((text, i) => ({ who: who[i] ?? "sis", text }));
  switch (ref.kind) {
    case "ask":
      return zip(EPISODES[ref.ep]?.ask ?? [], st.episodes[ref.ep]?.ask ?? []);
    case "give":
      return zip(EPISODES[ref.ep]?.give ?? [], st.episodes[ref.ep]?.give ?? []);
    case "filler":
      return [{ who: "ione", text: st.fillers[ref.act - 1]?.[ref.n] ?? "" }];
    case "after":
      return [{ who: "lykos", text: st.after[ref.n % st.after.length] ?? "" }];
    case "meet":
      return zip(
        COMPANIONS[ref.who].meetings[ref.m]?.who ?? [],
        st.companions[ref.who].meetings[ref.m] ?? [],
      );
    case "idle":
      return [{ who: ref.who, text: st.companions[ref.who].idle[ref.n % 3] ?? "" }];
    case "reply":
      return [
        {
          who: ref.who,
          text: ref.accepted ? st.companions[ref.who].accepted : st.companions[ref.who].declined,
        },
      ];
  }
}

/** a queue of notes, one on screen at a time */
export function useNotes() {
  const [notes, setNotes] = useState<StoryNote[]>([]);
  const push = useCallback((n: StoryNote) => setNotes((q) => [...q, n]), []);
  useEffect(() => {
    if (!notes.length) return;
    const id = window.setTimeout(() => setNotes((q) => q.slice(1)), 4600);
    return () => window.clearTimeout(id);
  }, [notes]);
  return { note: notes[0] ?? null, push };
}
