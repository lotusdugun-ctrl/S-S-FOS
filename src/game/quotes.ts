/**
 * A thousand aphorisms, in nine languages.
 *
 * The texts are about a megabyte across all the languages, so each language
 * lives in its own JSON file and is fetched only when it is chosen — a player
 * only ever downloads the one they read. The author list is shared and small,
 * and is all the game needs to know how many pages the Book of Wisdom has.
 *
 * Index order is permanent: saved progress refers to pages by index, so new
 * aphorisms are only ever appended.
 */
import AUTHORS from "./quotes-data/authors.json";
import CHAPTERS from "./quotes-data/chapters.json";

export type LangCode = "tr" | "en" | "zh" | "hi" | "es" | "fr" | "ar" | "bn" | "pt";

export interface Quote {
  text: string;
  author: string;
}

type LangFile = { texts: string[]; names: Record<string, string> };

export const QUOTE_COUNT: number = (AUTHORS as string[]).length;

/** which chapter of the Book of Wisdom each aphorism belongs to (0-based) */
export const CHAPTER_OF: readonly number[] = CHAPTERS as number[];
export const CHAPTER_COUNT = 12;

const files = import.meta.glob<LangFile>(
  ["./quotes-data/*.json", "!./quotes-data/authors.json", "!./quotes-data/chapters.json"],
  { import: "default" },
);

const cache: Partial<Record<LangCode, Quote[]>> = {};
const pending: Partial<Record<LangCode, Promise<Quote[]>>> = {};

/** fetch one language's aphorisms (once); resolves to the list */
export function loadQuotes(lang: LangCode): Promise<Quote[]> {
  const have = cache[lang];
  if (have) return Promise.resolve(have);
  const loader = files[`./quotes-data/${lang}.json`];
  if (!loader) return Promise.resolve([]);
  pending[lang] ??= loader().then((d) => {
    const list = d.texts.map((text, i) => {
      const key = (AUTHORS as string[])[i] ?? "";
      return { text, author: d.names[key] ?? key };
    });
    cache[lang] = list;
    return list;
  });
  return pending[lang]!;
}

/** the aphorisms for a language if they are loaded, else an empty list */
export function quotesFor(lang: LangCode): Quote[] {
  return cache[lang] ?? [];
}
