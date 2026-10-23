/**
 * The saga's state and rules, apart from any drawing.
 *
 * The engine tells it what happens on the hill — a climb starts, the stone
 * passes someone, he slips, he reaches the top, he walks back past someone —
 * and it decides what that means for the story: who speaks, what he carries,
 * which burden he bears, what the sky should be like, when it ends. What it
 * decides goes out as events for the interface to show, and as a plain state
 * object for the save.
 */
import { STAGE_OF, type Stage } from "./journey";
import {
  COMPANIONS,
  EPISODES,
  type BurdenId,
  type CompanionId,
  type ItemId,
  type Meeting,
  type PassiveId,
} from "./story";
import type { Weather } from "./engine";

export type SagaState = {
  /** 2: the Merope story (an earlier version told a different one) */
  v: number;
  /** one per summit, one more for a climb without a slip */
  insight: number;
  /** 0-1: how close he is to despair; slips raise it, clean climbs lower it */
  despair: number;
  /** climbs in a row without a slip */
  clean: number;
  /** summits ever (the saga's own count) */
  summits: number;
  /** Merope's next episode, and the summit count when the last one happened */
  ep: number;
  epAt: number;
  /** what she has asked him to bring, and what he is bringing down */
  asked: ItemId | null;
  carrying: ItemId | null;
  /** she has asked for the water, and he has said "not yet" this many times */
  notYet: number;
  waiting: boolean;
  /** how many times each companion has spoken to him */
  meet: Record<CompanionId, number>;
  /** whose burden he has carried to the top */
  carried: Record<CompanionId, boolean>;
  /** the burden on this climb */
  burden: CompanionId | null;
  passives: PassiveId[];
  /** a wedge waiting for the next climb (from whom), and whether it is laid */
  wedgeNext: CompanionId | null;
  wedge: boolean;
  /** she has gone; the last scene at the stone is still to come */
  finale: boolean;
  /** the story has reached its end; he smiles from here on */
  ended: boolean;
  /** the prologue has been seen */
  prologue: boolean;
};

export const SAGA_START: SagaState = {
  v: 2,
  insight: 0,
  despair: 0,
  clean: 0,
  summits: 0,
  ep: 0,
  epAt: -99,
  asked: null,
  carrying: null,
  notYet: 0,
  waiting: false,
  meet: { pro: 0, tan: 0 },
  carried: { pro: false, tan: false },
  burden: null,
  passives: [],
  wedgeNext: null,
  wedge: false,
  finale: false,
  ended: false,
  prologue: false,
};

const ITEMS: ItemId[] = ["stars", "wind", "spring", "lethe"];

/** sanitise a saved saga */
export function readSaga(v: unknown): SagaState {
  const d = (typeof v === "object" && v ? v : {}) as Partial<SagaState>;
  const num = (x: unknown, lo: number, hi: number, dflt = lo) =>
    typeof x === "number" && Number.isFinite(x) ? Math.max(lo, Math.min(hi, x)) : dflt;
  const comp = (x: unknown): CompanionId | null => (x === "pro" || x === "tan" ? x : null);
  const item = (x: unknown): ItemId | null => (ITEMS.includes(x as ItemId) ? (x as ItemId) : null);
  // a save from the earlier story: keep what he earned, start Merope's from the beginning
  const fresh = d.v !== 2;
  return {
    v: 2,
    insight: num(d.insight, 0, 1e6),
    despair: num(d.despair, 0, 1),
    clean: num(d.clean, 0, 1e6),
    summits: num(d.summits, 0, 1e7),
    ep: fresh ? 0 : Math.floor(num(d.ep, 0, EPISODES.length)),
    epAt: fresh ? -99 : num(d.epAt, -99, 1e7, -99),
    asked: fresh ? null : item(d.asked),
    carrying: fresh ? null : item(d.carrying),
    notYet: fresh ? 0 : num(d.notYet, 0, 1e6),
    waiting: !fresh && d.waiting === true,
    meet: fresh
      ? { pro: 0, tan: 0 }
      : { pro: num(d.meet?.pro, 0, 1e6), tan: num(d.meet?.tan, 0, 1e6) },
    carried: { pro: d.carried?.pro === true, tan: d.carried?.tan === true },
    burden: null,
    passives: Array.isArray(d.passives)
      ? d.passives.filter((p): p is PassiveId => p === "fire" || p === "patience")
      : [],
    wedgeNext: comp(d.wedgeNext),
    wedge: false,
    finale: !fresh && d.finale === true,
    ended: !fresh && d.ended === true,
    // the prologue is new too: show it once more
    prologue: !fresh && d.prologue === true,
  };
}

/** a line reference the interface turns into words in the player's language */
export type TalkRef =
  | { kind: "ep"; ep: number }
  | { kind: "again" }
  | { kind: "notyet"; n: number }
  | { kind: "filler"; act: 1 | 2 | 3; n: number }
  | { kind: "turned"; n: number }
  | { kind: "meet"; who: CompanionId; m: number }
  | { kind: "idle"; who: CompanionId; n: number }
  | { kind: "reply"; who: CompanionId; accepted: boolean };

export type SagaEvent =
  | {
      type: "talk";
      ref: TalkRef;
      /** a scene: the climb stops while it plays */
      cine: boolean;
      /** a burden is offered at the end of it */
      offer?: BurdenId;
      /** the last line asks: bring the water now, or not yet */
      choice?: boolean;
    }
  | { type: "took"; item: ItemId }
  | { type: "stage"; stage: Stage }
  | { type: "passive"; id: PassiveId }
  | { type: "burden"; id: BurdenId }
  | { type: "wedge" }
  | { type: "finale" };

/** where they are on the hill, as a fraction of its length */
export const NPC_AT = { tan: 0.22, merope: 0.45, pro: 0.7 } as const;

export class Saga {
  s: SagaState;
  onEvent: ((e: SagaEvent) => void) | null = null;
  /** who has already spoken on this climb / this descent */
  private spoke = new Set<string>();
  /** an offer waiting for an answer */
  pendingOffer: CompanionId | null = null;
  private fillN = 0;

  constructor(state?: SagaState) {
    this.s = state
      ? { ...state }
      : { ...SAGA_START, meet: { pro: 0, tan: 0 }, carried: { pro: false, tan: false } };
  }

  /** the stage of his mind is the act of the story he is in */
  get stage(): Stage {
    if (this.s.ended || this.s.finale) return 3;
    return EPISODES[Math.min(this.s.ep, EPISODES.length - 1)]!.act;
  }

  /** how serene he has become, 0-1: it shows in how he holds himself */
  get serenity(): number {
    if (this.s.ended) return 1;
    return [0, 0.4, 0.75][this.stage - 1]!;
  }

  /** Merope stands with her back to him between the midpoint and Act III */
  get meropeTurned(): boolean {
    return this.s.ep === 8 || this.s.ep === 9;
  }

  /** she is there to be seen: until she walks away at the end */
  get meropeHere(): boolean {
    return !this.s.finale && !this.s.ended;
  }

  has(p: PassiveId) {
    return this.s.passives.includes(p);
  }

  private emit(e: SagaEvent) {
    this.onEvent?.(e);
  }

  private meetingFor(who: CompanionId, summits: number): { m: number; mt: Meeting } | null {
    const c = COMPANIONS[who];
    const m = this.s.meet[who];
    const mt = c.meetings[m];
    if (!mt) return null;
    if (mt.after && summits < mt.after) return null;
    if (mt.minEp && this.s.ep < mt.minEp) return null;
    // the story has moved on past this one: what he would have said is stale
    const nx = c.meetings[m + 1];
    if (mt.minEp && nx?.minEp && this.s.ep >= nx.minEp && !(nx.after && summits < nx.after)) {
      this.s.meet[who]++;
      return this.meetingFor(who, summits);
    }
    if (mt.gift && !this.s.carried[who]) {
      // he refused the burden: there is nothing to thank him for
      this.s.meet[who]++;
      return this.meetingFor(who, summits);
    }
    return { m, mt };
  }

  /** the weather this climb should have, given how he is */
  weatherFor(cycles: number, fallback: Weather): Weather {
    const d = this.s.despair;
    if (cycles < 2) return "clear";
    // the last climb of the story is under a clear sky, whatever came before
    if (EPISODES[this.s.ep]?.finale && this.s.carrying !== null) return "clear";
    if (d > 0.6) return "storm";
    if (d > 0.4) return "rain";
    return "clear";
  }

  /** deep despair turns the storm to snow: winter */
  get winter(): boolean {
    return this.s.despair > 0.75;
  }

  /** spring: how far the hill is in flower, from a run of clean climbs */
  get bloomTarget(): number {
    if (this.s.despair > 0.3) return 0;
    const base = Math.min(1, this.s.clean / 3) * (1 - this.s.despair * 2);
    return this.s.ended ? Math.max(0.6, base) : base;
  }

  // ---------- the hill's events ----------

  climbStarted() {
    this.spoke.clear();
    this.s.burden = null;
    this.s.wedge = this.s.wedgeNext !== null;
    this.s.wedgeNext = null;
  }

  /** the stone has reached `frac` of the hill on the way up */
  ascent(frac: number, cycles: number) {
    if (frac >= NPC_AT.tan - 0.05 && cycles >= 2 && cycles % 2 === 0) this.companion("tan");
    if (frac >= NPC_AT.pro - 0.05 && cycles >= 3 && cycles % 2 === 1) this.companion("pro");
  }

  private companion(who: CompanionId) {
    if (this.spoke.has(who)) return;
    this.spoke.add(who);
    const next = this.meetingFor(who, this.s.summits);
    if (!next) {
      this.emit({ type: "talk", ref: { kind: "idle", who, n: this.fillN++ % 3 }, cine: false });
      return;
    }
    this.s.meet[who]++;
    const { m, mt } = next;
    if (mt.gift) this.s.wedgeNext = who;
    const e: SagaEvent = { type: "talk", ref: { kind: "meet", who, m }, cine: true };
    if (mt.offer && !this.s.burden) {
      this.pendingOffer = who;
      e.offer = mt.offer;
    }
    this.emit(e);
  }

  /** the player's answer to a burden */
  answer(accept: boolean) {
    const who = this.pendingOffer;
    if (!who) return;
    this.pendingOffer = null;
    if (accept) {
      this.s.burden = who;
      this.emit({ type: "burden", id: COMPANIONS[who].burden });
    }
    this.emit({ type: "talk", ref: { kind: "reply", who, accepted: accept }, cine: false });
  }

  /** the player's answer to her request for the water */
  choose(bring: boolean) {
    if (bring) {
      this.s.waiting = false;
      this.s.asked = "lethe";
      return;
    }
    this.s.waiting = true;
    this.emit({
      type: "talk",
      ref: { kind: "notyet", n: Math.min(2, this.s.notYet) },
      cine: true,
    });
    this.s.notYet++;
  }

  slipped() {
    this.s.despair = Math.min(1, this.s.despair + 0.14);
    this.s.clean = 0;
  }

  steady(dt: number) {
    this.s.despair = Math.max(0, this.s.despair - 0.004 * dt);
  }

  wedgeHeld() {
    this.s.wedge = false;
    this.emit({ type: "wedge" });
  }

  /** he reached the top */
  summit(slips: number) {
    this.s.summits++;
    this.s.insight += 1 + (slips === 0 ? 1 : 0);
    if (slips === 0) {
      this.s.clean++;
      this.s.despair = Math.max(0, this.s.despair - 0.25);
    } else this.s.despair = Math.max(0, this.s.despair - 0.05);
    const b = this.s.burden;
    if (b) {
      this.s.carried[b] = true;
      const p = COMPANIONS[b].passive;
      if (!this.s.passives.includes(p)) {
        this.s.passives.push(p);
        this.emit({ type: "passive", id: p });
      }
      this.s.burden = null;
    }
    // what she asked for, from the top
    if (this.s.asked && !this.s.ended) {
      this.s.carrying = this.s.asked;
      this.s.asked = null;
      this.emit({ type: "took", item: this.s.carrying });
    }
  }

  /** walking down, at `frac` of the hill: he passes her on the way */
  descent(frac: number) {
    // he stops a few steps before her, so he can face her as they talk
    if (frac > NPC_AT.merope + 0.06 || this.spoke.has("merope")) return;
    if (!this.meropeHere) return;
    this.spoke.add("merope");
    const before = this.stage;
    const next = EPISODES[this.s.ep];
    // she asked for the water, and he said not yet: she asks again
    if (this.s.waiting) {
      this.emit({ type: "talk", ref: { kind: "again" }, cine: true, choice: true });
      return;
    }
    const ready =
      !!next &&
      this.s.summits - this.s.epAt >= (next.gap ?? 2) &&
      (!next.brings || this.s.carrying === next.brings);
    if (!ready) {
      const ref: TalkRef = this.meropeTurned
        ? { kind: "turned", n: this.fillN++ % 3 }
        : { kind: "filler", act: this.stage, n: this.fillN++ % 3 };
      this.emit({ type: "talk", ref, cine: false });
      return;
    }
    const ep = this.s.ep;
    const e: SagaEvent = { type: "talk", ref: { kind: "ep", ep }, cine: next.cine };
    if (next.choice) e.choice = true;
    this.emit(e);
    if (next.brings) this.s.carrying = null;
    if (next.asks && !next.choice) this.s.asked = next.asks;
    this.s.ep++;
    this.s.epAt = this.s.summits;
    if (next.finale) {
      this.s.finale = true;
      this.emit({ type: "finale" });
    } else if (this.stage > before) this.emit({ type: "stage", stage: this.stage });
  }

  /** the last scene at the stone has played: the story is over */
  complete() {
    this.s.finale = false;
    this.s.ended = true;
  }

  /** pick a summit's saying from the stage he is in, among those not yet found */
  pickQuote(fresh: number[]): number | null {
    if (!fresh.length) return null;
    const st = this.stage;
    const mine = fresh.filter((i) => (STAGE_OF[i] ?? 3) === st);
    const pool = mine.length ? mine : fresh;
    return pool[Math.floor(Math.random() * pool.length)]!;
  }
}
