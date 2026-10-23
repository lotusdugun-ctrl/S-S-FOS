import { LANGUAGES, type LangCode } from "@/i18n";
import { readSaga, SAGA_START, type SagaState } from "./saga";
import { isNative } from "./native";

/**
 * The climb in progress, so that closing the game in the middle of one does
 * not send him back to the foot of the hill: where the stone was, how far it
 * had been worked up, or how far down the way back he had walked.
 */
export type RunSave = {
  /** the cycle and hill it belongs to: anything else is stale */
  cycles: number;
  lv: string;
  phase: "playing" | "descent";
  x: number;
  checkpoint: number;
  maxX: number;
  climbT: number;
  breath: number;
  weather: string;
  /** on the way down */
  walkX: number;
  walkFrom: number;
  hourFrom: number;
  hourTo: number;
  summitX: number;
};

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
  /** vibration on touch devices */
  haptics: boolean;
  /** aphorisms found, by index, in the order they were found */
  quotes: number[];
  /** calm mode: breath never runs out */
  calm: boolean;
  /** the first climb's lessons have been seen */
  tutorialDone: boolean;
  /** seals earned */
  seals: string[];
  /** hills summited, by level id */
  hills: string[];
  /** the last day (YYYY-MM-DD) the saying of the day was added to the book */
  daily: string;
  /** the player's own saying, and the name to sign it with */
  ownText: string;
  ownAuthor: string;
  /** accessibility: larger interface text, the bold breath ring, controls swapped */
  bigText: boolean;
  boldRing: boolean;
  leftHanded: boolean;
  /** records: longest run of perfect heaves, and the best climb of this week */
  bestStreak: number;
  weekKey: string;
  weekBest: number;
  /** the story so far: Ione, the others, the stages of his mind */
  saga: SagaState;
  /** the climb in progress, if the game was closed during one */
  run: RunSave | null;
  /** when this was written (ms), to tell the newer of two copies */
  savedAt: number;
};

const KEY = "sisyphus.save.v1";

const EMPTY: SaveData = {
  cycles: 0,
  bestClimb: 0,
  lang: null,
  muted: false,
  haptics: true,
  quotes: [],
  calm: false,
  tutorialDone: false,
  seals: [],
  hills: [],
  daily: "",
  ownText: "",
  ownAuthor: "",
  bigText: false,
  boldRing: false,
  leftHanded: false,
  bestStreak: 0,
  weekKey: "",
  weekBest: 0,
  saga: SAGA_START,
  run: null,
  savedAt: 0,
};

const isLang = (v: unknown): v is LangCode =>
  typeof v === "string" && LANGUAGES.some((l) => l.code === v);

export function loadSave(): SaveData {
  try {
    const raw = window.localStorage.getItem(KEY);
    if (!raw) return { ...EMPTY, saga: readSaga(null) };
    return parseSave(raw);
  } catch {
    return { ...EMPTY };
  }
}

const num = (v: unknown, d = 0) => (typeof v === "number" && Number.isFinite(v) ? v : d);

function readRun(v: unknown): RunSave | null {
  if (!v || typeof v !== "object") return null;
  const r = v as Partial<RunSave>;
  if (typeof r.lv !== "string" || (r.phase !== "playing" && r.phase !== "descent")) return null;
  return {
    cycles: Math.max(0, Math.floor(num(r.cycles))),
    lv: r.lv,
    phase: r.phase,
    x: num(r.x, 40),
    checkpoint: num(r.checkpoint),
    maxX: num(r.maxX),
    climbT: Math.max(0, num(r.climbT)),
    breath: Math.max(0, Math.min(1, num(r.breath, 1))),
    weather: typeof r.weather === "string" ? r.weather : "clear",
    walkX: num(r.walkX),
    walkFrom: num(r.walkFrom),
    hourFrom: num(r.hourFrom),
    hourTo: num(r.hourTo),
    summitX: num(r.summitX),
  };
}

function parseSave(raw: string): SaveData {
  try {
    const d = JSON.parse(raw) as Partial<SaveData>;
    return {
      cycles: Number.isFinite(d.cycles) ? Math.max(0, Math.floor(d.cycles!)) : 0,
      bestClimb: Number.isFinite(d.bestClimb) ? Math.max(0, d.bestClimb!) : 0,
      lang: isLang(d.lang) ? d.lang : null,
      muted: d.muted === true,
      haptics: d.haptics !== false,
      quotes: Array.isArray(d.quotes)
        ? d.quotes.filter((n): n is number => Number.isInteger(n) && n >= 0)
        : [],
      calm: d.calm === true,
      tutorialDone: d.tutorialDone === true,
      seals: Array.isArray(d.seals)
        ? d.seals.filter((x): x is string => typeof x === "string")
        : [],
      hills: Array.isArray(d.hills)
        ? d.hills.filter((x): x is string => typeof x === "string")
        : [],
      daily: typeof d.daily === "string" ? d.daily : "",
      ownText: typeof d.ownText === "string" ? d.ownText.slice(0, 160) : "",
      ownAuthor: typeof d.ownAuthor === "string" ? d.ownAuthor.slice(0, 40) : "",
      bigText: d.bigText === true,
      boldRing: d.boldRing === true,
      leftHanded: d.leftHanded === true,
      bestStreak: Number.isFinite(d.bestStreak) ? Math.max(0, Math.floor(d.bestStreak!)) : 0,
      weekKey: typeof d.weekKey === "string" ? d.weekKey : "",
      weekBest: Number.isFinite(d.weekBest) ? Math.max(0, d.weekBest!) : 0,
      saga: readSaga(d.saga),
      run: readRun(d.run),
      savedAt: num(d.savedAt),
    };
  } catch {
    return { ...EMPTY };
  }
}

export function writeSave(patch: Partial<SaveData>) {
  try {
    const next = { ...loadSave(), ...patch, savedAt: Date.now() };
    const json = JSON.stringify(next);
    window.localStorage.setItem(KEY, json);
    mirrorNative(json);
  } catch {
    /* storage unavailable: the session still plays, it just won't be remembered */
  }
}

/*
 * In the store apps the save is also kept in the app's own preferences. A
 * phone may clear a web view's storage when it runs short of space (iOS in
 * particular); the app's preferences it keeps. Written a moment after the
 * last change, so a burst of saves is one write.
 */
type PrefsMod = typeof import("@capacitor/preferences");
let prefs: Promise<PrefsMod> | null = null;
const loadPrefs = () => (prefs ??= import("@capacitor/preferences"));
let mirrorTimer = 0;
let mirrorJson = "";
function mirrorNative(json: string) {
  if (!isNative()) return;
  mirrorJson = json;
  window.clearTimeout(mirrorTimer);
  mirrorTimer = window.setTimeout(() => {
    void loadPrefs()
      .then(({ Preferences }) => Preferences.set({ key: KEY, value: mirrorJson }))
      .catch(() => {});
  }, 400);
}

/** write any pending copy at once (the app is going to the background) */
export function flushNativeSave() {
  if (!isNative() || !mirrorJson) return;
  window.clearTimeout(mirrorTimer);
  void loadPrefs()
    .then(({ Preferences }) => Preferences.set({ key: KEY, value: mirrorJson }))
    .catch(() => {});
}

/**
 * At start, in the store apps: if the app's own copy is newer than what the
 * web view kept (or the web view kept nothing), put it back. Resolves true
 * when it did — the page should then load again from it.
 */
export async function restoreNativeSave(): Promise<boolean> {
  if (!isNative()) return false;
  try {
    const { Preferences } = await loadPrefs();
    const { value } = await Preferences.get({ key: KEY });
    if (!value) {
      // first run of this version: keep a copy of what there is
      const cur = window.localStorage.getItem(KEY);
      if (cur) await Preferences.set({ key: KEY, value: cur });
      return false;
    }
    const kept = parseSave(value);
    const cur = window.localStorage.getItem(KEY);
    const here = cur ? parseSave(cur) : null;
    if (here && here.savedAt >= kept.savedAt) return false;
    window.localStorage.setItem(KEY, value);
    return true;
  } catch {
    return false;
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

/**
 * A backup of everything above as one line of text, to carry the book and the
 * seals to a new phone: copied, shared, pasted back in. Nothing leaves the
 * device unless the player sends it somewhere.
 */
const CODE_PREFIX = "SISYPHUS1:";

export function exportSave(): string {
  const json = JSON.stringify(loadSave());
  const bytes = new TextEncoder().encode(json);
  let bin = "";
  bytes.forEach((b) => (bin += String.fromCharCode(b)));
  return CODE_PREFIX + btoa(bin);
}

/** restore from a backup code; false if it is not one */
export function importSave(code: string): boolean {
  try {
    const raw = code.trim().replace(/\s+/g, "");
    if (!raw.startsWith(CODE_PREFIX)) return false;
    const bin = atob(raw.slice(CODE_PREFIX.length));
    const bytes = Uint8Array.from(bin, (c) => c.charCodeAt(0));
    const data = JSON.parse(new TextDecoder().decode(bytes)) as Partial<SaveData>;
    if (typeof data !== "object" || data === null || !Array.isArray(data.quotes)) return false;
    window.localStorage.setItem(KEY, JSON.stringify(data));
    return true;
  } catch {
    return false;
  }
}

/** the ISO week a date falls in, as "2026-W43": the weekly record resets on Mondays */
export function weekKey(d = new Date()): string {
  const t = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
  const day = t.getUTCDay() || 7;
  t.setUTCDate(t.getUTCDate() + 4 - day);
  const y = t.getUTCFullYear();
  const wk = Math.ceil(((t.getTime() - Date.UTC(y, 0, 1)) / 86_400_000 + 1) / 7);
  return `${y}-W${String(wk).padStart(2, "0")}`;
}
