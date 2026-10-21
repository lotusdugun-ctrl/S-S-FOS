/**
 * The saga's state and rules, apart from any drawing.
 *
 * The engine tells it what happens on the hill — a climb starts, the stone
 * passes someone, he slips, he reaches the top, he walks back past someone —
 * and it decides what that means for the story: who speaks, what he carries,
 * which burden he bears, what the sky should be like, when he understands.
 * What it decides goes out as events for the interface to show, and as a
 * plain state object for the save.
 */
import { stageFor, STAGE_OF, type Stage } from "./journey";
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
  /** one per summit, one more for a climb without a slip */
  insight: number;
  /** 0-1: how close he is to despair; slips raise it, clean climbs lower it */
  despair: number;
  /** climbs in a row without a slip */
  clean: number;
  /** Ione's next episode */
  ep: number;
  /** what he is bringing down for her */
  carrying: ItemId | null;
  /** how many times each companion has spoken to him */
  meet: Record<CompanionId, number>;
  /** whose burden he has carried to the top */
  carried: Record<CompanionId, boolean>;
  /** the burden on this climb, and whose it is */
  burden: CompanionId | null;
  passives: PassiveId[];
  /** a wedge waiting for the next climb (from whom), and whether it is laid */
  wedgeNext: CompanionId | null;
  wedge: boolean;
  /** the story has reached its end; he smiles from here on */
  ended: boolean;
  /** the prologue has been seen */
  prologue: boolean;
};

export const SAGA_START: SagaState = {
  insight: 0,
  despair: 0,
  clean: 0,
  ep: 0,
  carrying: null,
  meet: { pro: 0, tan: 0 },
  carried: { pro: false, tan: false },
  burden: null,
  passives: [],
  wedgeNext: null,
  wedge: false,
  ended: false,
  prologue: false,
};

/** sanitise a saved saga */
export function readSaga(v: unknown): SagaState {
  const d = (typeof v === "object" && v ? v : {}) as Partial<SagaState>;
  const num = (x: unknown, lo: number, hi: number) =>
    typeof x === "number" && Number.isFinite(x) ? Math.max(lo, Math.min(hi, x)) : lo;
  const comp = (x: unknown): CompanionId | null => (x === "pro" || x === "tan" ? x : null);
  return {
    insight: num(d.insight, 0, 1e6),
    despair: num(d.despair, 0, 1),
    clean: num(d.clean, 0, 1e6),
    ep: Math.floor(num(d.ep, 0, EPISODES.length)),
    carrying: null,
    meet: { pro: num(d.meet?.pro, 0, 1e6), tan: num(d.meet?.tan, 0, 1e6) },
    carried: { pro: d.carried?.pro === true, tan: d.carried?.tan === true },
    burden: null,
    passives: Array.isArray(d.passives)
      ? d.passives.filter((p): p is PassiveId => p === "fire" || p === "patience")
      : [],
    wedgeNext: comp(d.wedgeNext),
    wedge: false,
    ended: d.ended === true,
    prologue: d.prologue === true,
  };
}

/** a line reference the interface turns into words in the player's language */
export type TalkRef =
  | { kind: "ask" | "give"; ep: number }
  | { kind: "filler"; act: 1 | 2 | 3; n: number }
  | { kind: "after"; n: number }
  | { kind: "meet"; who: CompanionId; m: number }
  | { kind: "idle"; who: CompanionId; n: number }
  | { kind: "reply"; who: CompanionId; accepted: boolean };

export type SagaEvent =
  | { type: "talk"; ref: TalkRef; offer?: BurdenId }
  | { type: "took"; item: ItemId }
  | { type: "stage"; stage: Stage }
  | { type: "passive"; id: PassiveId }
  | { type: "burden"; id: BurdenId }
  | { type: "wedge" }
  | { type: "epiphany" }
  | { type: "end" };

/** where the three of them sit, as a fraction of the hill */
export const NPC_AT = { tan: 0.22, ione: 0.45, pro: 0.7 } as const;

export class Saga {
  s: SagaState;
  onEvent: ((e: SagaEvent) => void) | null = null;
  /** who has already spoken on this climb / this descent */
  private spoke = new Set<string>();
  /** an offer waiting for an answer */
  pendingOffer: CompanionId | null = null;
  /** the summit of this climb is the ending */
  endingNow = false;
  /** filler rotation */
  private fillN = 0;

  constructor(state?: SagaState) {
    this.s = state
      ? { ...state }
      : { ...SAGA_START, meet: { pro: 0, tan: 0 }, carried: { pro: false, tan: false } };
  }

  get stage(): Stage {
    return stageFor(this.s.insight);
  }

  /** how serene he has become, 0-1: it shows in how he holds himself */
  get serenity(): number {
    if (this.s.ended) return 1;
    return [0, 0.4, 0.75][this.stage - 1]!;
  }

  has(p: PassiveId) {
    return this.s.passives.includes(p);
  }

  private emit(e: SagaEvent) {
    this.onEvent?.(e);
  }

  /** the summits he must have before the next stage of a companion's talk */
  private meetingFor(who: CompanionId, summits: number): { m: number; mt: Meeting } | null {
    const c = COMPANIONS[who];
    const m = this.s.meet[who];
    const mt = c.meetings[m];
    if (!mt) return null;
    if (mt.after && summits < mt.after) return null;
    if (mt.gift && !this.s.carried[who]) {
      // he refused the burden: skip the thanks
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
    if (!this.s.ended && EPISODES[this.s.ep]?.ending) return "clear";
    if (d > 0.6) return "storm";
    if (d > 0.4) return "rain";
    if (d > 0.25) return "fog";
    // content: mostly fair, with the odd misty morning for variety
    return fallback === "fog" ? "fog" : "clear";
  }

  /** deep despair turns the storm to snow: winter */
  get winter(): boolean {
    return this.s.despair > 0.75;
  }

  /** spring: how far the hill is in flower, from a run of clean climbs */
  get bloomTarget(): number {
    if (this.s.despair > 0.3) return 0;
    return Math.min(1, this.s.clean / 3) * (1 - this.s.despair * 2);
  }

  // ---------- the hill's events ----------

  climbStarted() {
    this.spoke.clear();
    this.s.burden = null;
    // a promised wedge is laid on this climb
    this.s.wedge = this.s.wedgeNext !== null;
    this.s.wedgeNext = null;
  }

  /** the stone has reached `frac` of the hill on the way up */
  ascent(frac: number, cycles: number) {
    if (frac >= NPC_AT.tan - 0.02 && cycles >= 2 && cycles % 2 === 0) this.companion("tan", cycles);
    if (frac >= NPC_AT.pro - 0.02 && cycles >= 3 && cycles % 2 === 1) this.companion("pro", cycles);
    if (frac >= NPC_AT.ione - 0.02 && !this.spoke.has("ione")) {
      this.spoke.add("ione");
      if (this.s.ended) {
        this.emit({ type: "talk", ref: { kind: "after", n: this.fillN++ % 4 } });
        return;
      }
      const ep = EPISODES[this.s.ep];
      if (ep && ep.act <= this.stage) {
        this.emit({ type: "talk", ref: { kind: "ask", ep: this.s.ep } });
        this.s.carrying = null;
        this.pendingItem = ep.item;
      } else {
        const act = (EPISODES[Math.min(this.s.ep, EPISODES.length - 1)]?.act ?? 3) as 1 | 2 | 3;
        this.emit({
          type: "talk",
          ref: { kind: "filler", act: Math.max(1, act - 1) as 1 | 2 | 3, n: this.fillN++ % 3 },
        });
      }
    }
  }

  private pendingItem: ItemId | null = null;

  private companion(who: CompanionId, summits: number) {
    if (this.spoke.has(who)) return;
    this.spoke.add(who);
    const next = this.meetingFor(who, summits);
    if (!next) {
      this.emit({ type: "talk", ref: { kind: "idle", who, n: this.fillN++ % 3 } });
      return;
    }
    this.s.meet[who]++;
    const { m, mt } = next;
    if (mt.gift) this.s.wedgeNext = who;
    if (mt.offer && !this.s.burden) {
      this.pendingOffer = who;
      this.emit({ type: "talk", ref: { kind: "meet", who, m }, offer: mt.offer });
    } else {
      this.emit({ type: "talk", ref: { kind: "meet", who, m } });
    }
  }

  /** the player's answer to an offer */
  answer(accept: boolean) {
    const who = this.pendingOffer;
    if (!who) return;
    this.pendingOffer = null;
    if (accept) {
      this.s.burden = who;
      this.emit({ type: "burden", id: COMPANIONS[who].burden });
    }
    this.emit({ type: "talk", ref: { kind: "reply", who, accepted: accept } });
  }

  /** the grip went: the stone took him back */
  slipped() {
    this.s.despair = Math.min(1, this.s.despair + 0.14);
    this.s.clean = 0;
  }

  /** a stretch of good climbing: despair eases */
  steady(dt: number) {
    this.s.despair = Math.max(0, this.s.despair - 0.004 * dt);
  }

  /** the wedge caught the stone */
  wedgeHeld() {
    this.s.wedge = false;
    this.emit({ type: "wedge" });
  }

  /** he reached the top. Returns true when this summit is the ending. */
  summit(slips: number): boolean {
    const before = this.stage;
    this.s.insight += 1 + (slips === 0 ? 1 : 0);
    if (slips === 0) {
      this.s.clean++;
      this.s.despair = Math.max(0, this.s.despair - 0.25);
    } else this.s.despair = Math.max(0, this.s.despair - 0.05);
    // a burden carried all the way up
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
    if (this.pendingItem) {
      this.s.carrying = this.pendingItem;
      this.emit({ type: "took", item: this.pendingItem });
      this.pendingItem = null;
    }
    const after = this.stage;
    if (after > before) this.emit({ type: "stage", stage: after });
    this.endingNow = !this.s.ended && this.s.carrying === "understanding";
    if (this.endingNow) this.emit({ type: "epiphany" });
    return this.endingNow;
  }

  /** walking down, at `frac` of the hill */
  descent(frac: number) {
    if (frac > NPC_AT.ione + 0.01 || this.spoke.has("ione-down")) return;
    if (!this.s.carrying) return;
    this.spoke.add("ione-down");
    const ep = this.s.ep;
    this.emit({ type: "talk", ref: { kind: "give", ep } });
    this.s.carrying = null;
    this.s.ep++;
    if (EPISODES[ep]?.ending) {
      this.s.ended = true;
      this.endingNow = false;
      this.emit({ type: "end" });
    }
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
