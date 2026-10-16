import { LANGUAGES, type LangCode } from "@/i18n";

/**
 * What the hill remembers between visits: how many times he has been down it,
 * his fastest way up, and the two choices a player should only have to make
 * once. Browser storage can be missing, full or throw outright (private
 * windows, blocked site data), so every access is guarded and the game simply
 * starts fresh when it is not there.
 */
export type SaveData = {
  cycles: number;
  bestClimb: number;
  lang: LangCode | null;
  muted: boolean;
};

const KEY = "sisyphus.save.v1";

const EMPTY: SaveData = { cycles: 0, bestClimb: 0, lang: null, muted: false };

const isLang = (v: unknown): v is LangCode =>
  typeof v === "string" && LANGUAGES.some((l) => l.code === v);

export function loadSave(): SaveData {
  try {
    const raw = window.localStorage.getItem(KEY);
    if (!raw) return { ...EMPTY };
    const d = JSON.parse(raw) as Partial<SaveData>;
    return {
      cycles: Number.isFinite(d.cycles) ? Math.max(0, Math.floor(d.cycles!)) : 0,
      bestClimb: Number.isFinite(d.bestClimb) ? Math.max(0, d.bestClimb!) : 0,
      lang: isLang(d.lang) ? d.lang : null,
      muted: d.muted === true,
    };
  } catch {
    return { ...EMPTY };
  }
}

export function writeSave(patch: Partial<SaveData>) {
  try {
    const next = { ...loadSave(), ...patch };
    window.localStorage.setItem(KEY, JSON.stringify(next));
  } catch {
    /* storage unavailable: the session still plays, it just won't be remembered */
  }
}

/** the first of the browser's preferred languages the game speaks, else English */
export function browserLang(): LangCode {
  try {
    const prefs = navigator.languages?.length ? navigator.languages : [navigator.language];
    for (const p of prefs) {
      const base = p?.toLowerCase().split("-")[0];
      if (isLang(base)) return base;
    }
  } catch {
    /* no navigator (SSR) */
  }
  return "en";
}

/** 83.4 -> "1:23" */
export function formatTime(sec: number): string {
  const s = Math.max(0, Math.round(sec));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
}
