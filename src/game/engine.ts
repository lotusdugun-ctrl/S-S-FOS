import { GameAudio } from "./audio";
import { drawKeraunos, ZeusPainter, zeusHand } from "./zeus";
import { graniteTexture, groundTexture } from "./textures";
import { drawMoon, paintSky, rgb, skyState, starField, type SkyState } from "./sky";
import { cypressSprite, grassClump, oliveSprite, type TreeSprite } from "./flora";
import { QUOTE_COUNT, quotesFor, type LangCode, type Quote } from "./quotes";
import { vibrate } from "./native";
import { buildCloud, tintCloud, type CloudShape } from "./clouds";
import { Saga, NPC_AT, type SagaEvent, type SagaState } from "./saga";
import {
  drawItem,
  drawMerope,
  drawPrometheus,
  drawTantalus,
  type EagleVisit,
  type NpcLight,
} from "./npcs";
import { EPIGRAPHS, LEVEL_NAMES } from "@/i18n";
import {
  getLevel,
  LEDGE_RUN,
  levelForCycle,
  slopeAt,
  terrainAt,
  type Cloud,
  type Level,
} from "./levels";

export type Phase = "playing" | "summit" | "rolling" | "descent";

export type EngineState = {
  phase: Phase;
  progress: number; // 0..1
  levelName: string;
  epigraph: string;
  cycles: number;
  /** seconds spent climbing in the current cycle (summit to summit, cinematics excluded) */
  climbTime: number;
  /** the finished cycle's climb time, 0 until the first summit */
  lastClimb: number;
  /** fastest climb ever, 0 if none */
  bestClimb: number;
  /** true when lastClimb set a new best */
  newBest: boolean;
  /** a one-line nudge when the player is stuck on something the game can explain */
  hint: Hint | null;
  /** the weather on this climb */
  weather: Weather;
  /** the hill being climbed, by level id */
  levelId: string;
  /** the first climb's lessons have all been seen */
  tutorialDone: boolean;
  /** how the last finished climb went, for the seals; null before the first summit */
  lastRun: RunStats | null;
  /** indices of every aphorism found so far, in the order they were found */
  unlocked: number[];
  /** the aphorism just found at this summit, -1 when it was one already known */
  newQuote: number;
};

export type Hint = "push" | "rhythm" | "breathe" | "kick" | "rest";
export type Weather = "clear" | "rain" | "fog" | "storm";

/** one climb, summed up at its summit */
export type RunStats = {
  /** distinguishes one summit from the next */
  id: number;
  seconds: number;
  slips: number;
  rests: number;
  maxStreak: number;
  night: boolean;
  weather: Weather;
  levelId: string;
  calm: boolean;
};

/**
 * The weather for a cycle: mostly clear, sometimes rain, fog or a storm. Fixed
 * by the cycle number, so a cycle replayed after a reload has the same sky. The
 * first two climbs are always clear — there is enough to learn already.
 */
export function weatherFor(cycles: number): Weather {
  if (cycles < 2) return "clear";
  const r = Math.abs(Math.sin(cycles * 12.9898 + 78.233) * 43758.5453) % 1;
  return r < 0.55 ? "clear" : r < 0.73 ? "rain" : r < 0.88 ? "fog" : "storm";
}

export type EngineInit = {
  /** completed cycles carried over from a previous visit */
  cycles?: number;
  /** fastest climb carried over from a previous visit, in seconds */
  bestClimb?: number;
  /** aphorisms already found on earlier visits */
  unlocked?: number[];
  /** vibration on/off */
  haptics?: boolean;
  /** no breath to run out of: the climb as a meditation */
  calm?: boolean;
  /** the first climb's lessons were seen on an earlier visit */
  tutorialDone?: boolean;
  /** the story so far */
  saga?: SagaState;
};

const PUSH_FORCE = 620;
const GRAVITY = 900;
const FRICTION = 1.6;
const ROLL_GRAVITY = 1500;
/** boulder launch velocity (units/s) when the character kicks it loose */
const KICK_V = 560;
/** character free-run speed (units/s) while the boulder is away */
const KICK_SPRINT = 220;

/*
 * Breath.
 *
 * Holding the key used to be the whole game: a minute of one finger down and
 * nothing to decide. Now pushing costs breath and letting go — bracing — gives
 * it back without giving ground, so the climb is a rhythm of effort and rest.
 * Run out mid-push and his grip goes: the stone slides back until he catches it.
 */
/** breath spent per second of full push on level ground; scales up with the slope */
const BREATH_DRAIN = 0.06;
/** breath recovered per second while braced */
const BREATH_REGEN = 0.3;
/** how long he cannot hold the stone after running out of breath, in seconds */
const SLIP_TIME = 1.3;
/** breath a launch costs; below this he cannot launch at all */
const KICK_COST = 0.3;

/*
 * Heaves. Every fresh press of the push key is a heave, and a heave timed to
 * the beat — the ring closing over his head — lands with real force and gives
 * a little breath back. Mashing lands off the beat and only tires him. Holding
 * the key down still works; it is just the expensive way up.
 */
/** seconds between beats; close to his stride when he is working hard */
const BEAT = 0.75;

/** summit cinematic: when the bolt is thrown, and when the stone starts down */
const ZEUS_STRIKE = 1.9;
/** the ending's summit: the strike comes late, after he has smiled */
const EPIPHANY_STRIKE = 8.5;

/*
 * Time. Every cycle happens at a different hour, and the walk back down is
 * where the hours pass: golden hour, then the blue hour after sunset, the
 * night with its moon, dawn, sunrise, morning, noon, afternoon — and round
 * again to the golden hour the game began in.
 */
const SCHEDULE = [18.2, 18.75, 22.8, 5.95, 6.55, 9.5, 13, 16.4];
/** walking pace on the way down, units/s */
const WALK_SPEED = 255;
const ZEUS_ROLL = 3.0;
const HEAVE = {
  perfect: { window: 0.09, v: 170, breath: 0.03 },
  good: { window: 0.18, v: 95, breath: 0 },
  miss: { window: Infinity, v: 25, breath: -0.05 },
} as const;
type HeaveQuality = keyof typeof HEAVE;
/**
 * Ridge silhouettes from the farthest rank to the nearest. Distance lifts the
 * value, and this close to a setting sun the haze doing the lifting is warm —
 * blue haze belongs to a midday sky, and up against the disc it read as a
 * bruise sitting in front of the light.
 */
const RIDGE_TONES = [
  "oklch(0.46 0.075 44 / 0.45)",
  "oklch(0.32 0.06 26 / 0.66)",
  "oklch(0.2 0.04 30 / 0.85)",
];
/** how much terrain relief the cosmetic ridges are allowed to stand up */
const RIDGE_RELIEF = 0.15;
/** world units covered by one full two-step leg cycle */
const STRIDE_LENGTH = 54;
/** fastest believable leg turnover, in cycles per second */
const MAX_CADENCE = 3.4;

/**
 * How many device pixels the frame is allowed to cost.
 *
 * `devicePixelRatio` on a desktop retina panel is 2, which on a 1920x1080 window
 * is 8.3 million pixels to shade — and this scene shades most of them several
 * times over (sky, ridges, ground, mist, vignette). That is where the frame time
 * was going on big screens. A phone stays at its full ratio because its window is
 * small; a large window drops toward 1 rather than the whole thing dropping
 * frames, which is the trade every game engine makes and the one nobody notices.
 */
const PIXEL_BUDGET = 2_600_000;

/**
 * The cloud bank is the single most expensive thing in the frame: twenty-one
 * cumulus, each a stack of radial gradients covering a good fraction of the
 * screen. It is also the slowest-moving thing in it — six to twenty pixels a
 * second — so it is painted into its own layer at half resolution and only
 * repainted when it has actually drifted. Soft gradients are exactly the content
 * that survives being upscaled; nothing in that layer has an edge to lose.
 */
const CLOUD_RES = 0.5;
/** longest a painted cloud layer may be reused, in seconds */
const CLOUD_MAX_AGE = 0.06;
/** how far the front rank may drift, in pixels, before the layer is repainted */
const CLOUD_MAX_DRIFT = 2;

type Bird = {
  /** world-x offset from the player, drawn with parallax */
  ox: number;
  /** vertical position as a fraction of screen height */
  fy: number;
  /** parallax factor (0 = far away) */
  par: number;
  /** self-drift in world units/s */
  speed: number;
  /** wing-flap animation phase */
  flap: number;
  flapSpeed: number;
  /** gentle path wobble */
  wander: number;
  bob: number;
  bobFreq: number;
  /** relative size */
  size: number;
  /** seconds left in the current glide (wings held still); 0 while flapping */
  glide: number;
  /** seconds until it next stops flapping to glide */
  nextGlide: number;
  /** slow bank, so the flock does not fly on rails */
  bank: number;
};

/** a dust mote hanging in the low sun: screen fractions, parallax depth, drift phase */
type Mote = { fx: number; fy: number; z: number; ph: number };

type Particle = {
  /** world x (near the boulder) */
  wx: number;
  /** world y */
  wy: number;
  vx: number;
  vy: number;
  life: number;
  maxLife: number;
  size: number;
  kind: "dust" | "spark";
};

const DEFAULT_SUN = { xFrac: 0.78, yFrac: 0.6 };

export class SisyphusEngine {
  private ctx: CanvasRenderingContext2D;
  private canvas: HTMLCanvasElement;
  private raf = 0;
  private last = 0;
  private dpr = 1;
  private w = 0;
  private h = 0;

  private level: Level;
  private levelIndex = 0;
  private cycles = 0;

  private x = 40;
  private vx = 0;
  private roll = 0; // boulder rotation
  private phase: Phase = "playing";
  private phaseT = 0;
  private stepT = 0;
  private shake = 0;

  private t = 0; // global clock (background animations)

  private camX = 0;
  private camY = 0;
  private scale = 1;
  private groundY = 0;
  private shakeY = 0;
  private frameDt = 1 / 60;

  /*
   * The camera as a camera operator would hold it. It pushes in slowly while
   * he is losing to the stone, punches in a touch on a perfect heave, closes
   * on him at the summit and lets the moment of the strike play in slow
   * motion, then eases back out as the stone goes. Zoom never drops below 1:
   * the frame the game is played in is the widest shot there is.
   */
  /** false turns the moves off (reduced motion) */
  cinematic = true;
  private zoom = 1;
  private zoomKick = 0;
  private strainS = 0;
  private focusX = -1;
  private focusY = -1;
  private focusTX = 0;
  private focusTY = 0;
  private shakeX = 0;
  /** slow motion: real seconds left, its length, and how slow at its deepest */
  private slowT = 0;
  private slowDur = 1;
  private slowDepth = 0;

  /** pre-painted static sky (gradient + glow + disc), rebuilt only on resize / level change */
  private skyCache: HTMLCanvasElement | null = null;
  private skyCacheKey = "";
  /** half-resolution cloud bank, repainted only when it has drifted (see CLOUD_RES) */
  private cloudCache: HTMLCanvasElement | null = null;
  private cloudCacheKey = "";
  private cloudCacheT = -1;
  private cloudCacheCamX = 0;
  private cloudRes = CLOUD_RES;
  private cloudBlur: HTMLCanvasElement | null = null;
  /** each cloud's volume, built once (see clouds.ts) */
  private cloudShapes = new Map<number, CloudShape>();
  /** clouds still allowed to be built, or recoloured, during this paint */
  private cloudBuildBudget = 0;
  private cloudBudget = 0;
  private cloudCacheHour = -1;
  /** the aphorism, rendered once per quote instead of re-wrapped and re-blurred per frame */
  private quoteCache: HTMLCanvasElement | null = null;
  private quoteCacheKey = "";
  /** the aphorism sprite's size in CSS pixels (the canvas itself is dpr-scaled) */
  private quoteW = 0;
  private quoteH = 0;
  /** where the first line's centre sits inside `quoteCache` */
  private quoteAnchorY = 0;
  /** the mountain's ridge, which is world-space and therefore the same every frame */
  private mountainPts: Array<[number, number]> | null = null;
  private mountainKey = "";
  /** screen-y of the terrain at each 4px column, sampled once and reused by three passes */
  private terrainYs: Float64Array | null = null;
  /** the last state handed to React, so identical frames don't re-render it */
  private lastEmitKey = "";
  /** throttles the friction bus so it isn't handed sixty automation events a second */
  private frictionT = 0;
  /** distance-based walk phase, so the gait follows the feet and not the clock */
  private gait = 0;

  private zeus = { state: "gone" as "gone" | "appear" | "strike" | "fade", t: 0 };
  private zeusPainter = new ZeusPainter();
  /** where the bolt leaves his hand, in screen space, set while he is drawn */
  private boltFrom: { x: number; y: number } | null = null;
  /** world x the man stands at while the stone goes back down without him */
  private summitX = 0;
  /** index of the next ledge the falling stone will drop over (rolling phase) */
  private rollLedge = -1;
  /** 0..1 vertical hop the stone takes when it drops off a ledge */
  private hop = 0;
  private hopV = 0;
  private flash = 0;
  private strainT = 0;

  /** >= 0 while a kick is in flight; -1 when idle */
  private kickT = -1;
  /** character world position (separates from the boulder during a kick) */
  private charX = -24;

  private birds: Bird[] = [];
  private motes: Mote[] = [];
  /** the hour this cycle's climb began at, and the current hour */
  private baseHour = SCHEDULE[0]!;
  private hour = SCHEDULE[0]!;
  /** the walk down: where he is, how fast, and the hour the walk ends at */
  private walkX = 0;
  private walkV = 0;
  private walkFrom = 0;
  private hourFrom = 0;
  private hourTo = 0;
  /** the sky for this frame */
  private st: SkyState = skyState(SCHEDULE[0]!, 0.5);
  private stars: { cv: HTMLCanvasElement; bright: Array<[number, number, number]> } | null = null;
  private starsKey = "";
  private meteor: { x: number; y: number; vx: number; vy: number; t: number } | null = null;
  private meteorT = 8;
  /** the landscape is painted into this so the hour can grade it without touching the sky */
  private landLayer: HTMLCanvasElement | null = null;
  /*
   * Holding the stone. Bracing used to be a still pose — a statue leaning on
   * a rock. A rock on a slope is never still for a man holding it: it creeps
   * back, he feels it, digs in, shoves it home, and it creeps again. These
   * drive that loop visually; the physics underneath is unchanged.
   */
  private holdT = 0;
  /** his breathing, for the sound of it: phase, and which half we are in */
  private breathPh = 0;
  private breathIn = true;
  /** gravel under the stone: time to the next grain */
  private gritT = 0;
  private windT = 0;
  private holdA = 0;
  /** world units the stone has crept back this frame (drawn, not simulated) */
  private creep = 0;
  /** 0..1 how hard he is shoving it back into place right now */
  private holdDrive = 0;
  private chirpT = 0;

  /** false until the start card is dismissed: the scene runs, the man waits */
  private active = false;
  /** 0..1 */
  private breath = 1;
  /** > 0 while he has lost his grip after running out of breath */
  private slipT = 0;
  /** a fresh press is only a heave if the key came back up first */
  private heaveArmed = true;
  /** the last heave, for the ring's flash: quality and seconds since */
  private heaveFx: { q: HeaveQuality; t: number } | null = null;
  /** consecutive perfectly timed heaves; each one adds force, a miss resets it */
  private streak = 0;
  /** the streak shown over his head, kept a moment after it breaks */
  private streakShown = 0;
  private streakT = 99;
  /** seconds since a launch was refused for want of breath, for the red flash */
  private denyT = 99;
  /** world x below which the stone cannot slip back (the last ledge crossed) */
  private checkpoint = 0;
  /** seconds since the last ledge was crossed, for the stele's flare */
  private checkpointT = 99;
  /** smoothed visibility of the breath ring */
  private hudA = 0;
  /** last frame's screen y of his head, so the aphorism can stay clear of him */
  private headY = Infinity;
  private climbT = 0;
  private lastClimb = 0;
  private bestClimb = 0;
  private newBest = false;
  /** seconds spent pushing on a ledge's pitch without getting anywhere */
  private stuckT = 0;
  /** seconds left to show the "rest" hint after a slip or a refused launch */
  private restHintT = 0;
  private restHintsShown = 0;

  /** the weather on this climb (see weatherFor) */
  weather: Weather = "clear";
  /** calm mode: breath never runs out, so he never slips */
  calm = false;
  /** the first climb teaches, one thing at a time */
  private tutorialDone = false;
  private tutStep: "push" | "rhythm" | "free" = "push";
  private tutGood = 0;
  private tutBreatheT = 0;
  /** seconds until the storm's next strike */
  private stormT = 4;
  /** this climb so far, for the seals */
  private runSlips = 0;
  private runRests = 0;
  private runMaxStreak = 0;
  private wasBraced = false;
  private lastRun: RunStats | null = null;
  /** once a launch has carried the stone over a ledge, the player knows how */
  private kickLearned = false;

  /** dust / spark motes kicked up around the boulder */
  private particles: Particle[] = [];

  /** the random aphorism currently written in the sky (assigned at each summit) */
  private currentQuote: Quote | null = null;
  /** index of currentQuote in the deck (so the sky text can follow a language change) */
  private currentQuoteIndex = -1;
  /** shuffled deck of quote indices, reshuffled when emptied */
  private quoteDeck: number[] = [];
  /** every aphorism found so far, in the order found */
  private unlocked: number[] = [];
  /** the one found at the last summit, if it was new */
  private newQuote = -1;
  /** vibration, which some people simply do not want */
  haptics = true;
  /** frozen while a menu is open over the game */
  paused = false;
  /** selected UI / quote language */
  private lang: LangCode = "tr";

  /** -1..1 input from keyboard / pointer drag */
  input = 0;

  audio = new GameAudio();
  onState: (s: EngineState) => void = () => {};
  /** the story: who speaks, what he carries, what he learns (see saga.ts) */
  saga: Saga;
  onSaga: (e: SagaEvent) => void = () => {};
  /** how far the hill is in flower (spring), eased toward the saga's target */
  private bloom = 0;
  /** a change of weather in the middle of a climb fades in */
  private wxFade = 1;
  /** furthest the stone got on this climb (for the wedge) */
  private maxX = 0;
  /** the summit of this climb is the ending: he learns to smile */
  private epiphany = false;
  /** 0-1, how much he is smiling */
  private smile = 0;
  /** the wedge's flash when it catches */
  private wedgeT = 99;
  /** echoes of earlier slips: where, and what he looked like */
  private ghosts: Array<{
    wx: number;
    lv: string;
    cv: HTMLCanvasElement;
    w: number;
    h: number;
    ox: number;
    oy: number;
  }> = [];

  constructor(canvas: HTMLCanvasElement, levelIndex = 0, init: EngineInit = {}) {
    this.canvas = canvas;
    this.cycles = Math.max(0, Math.floor(init.cycles ?? 0));
    this.bestClimb = Math.max(0, init.bestClimb ?? 0);
    const total = QUOTE_COUNT;
    this.unlocked = [...new Set((init.unlocked ?? []).filter((i) => i >= 0 && i < total))];
    this.haptics = init.haptics !== false;
    this.baseHour = SCHEDULE[this.cycles % SCHEDULE.length]!;
    this.hour = this.baseHour;
    // The sky covers every pixel before anything else is drawn, so there is
    // nothing behind the canvas to blend with — and an opaque context lets the
    // compositor skip the per-pixel blend against the page underneath it.
    const c = canvas.getContext("2d", { alpha: false });
    if (!c) throw new Error("Canvas 2D unavailable");
    this.ctx = c;
    // three cycles on each hill, in turn; the first hill is the one the game
    // has always started on
    this.levelIndex = levelIndex + levelForCycle(this.cycles);
    this.level = getLevel(this.levelIndex);
    this.saga = new Saga(init.saga);
    this.saga.onEvent = (e) => this.onSaga(e);
    this.weather = this.saga.weatherFor(this.cycles, weatherFor(this.cycles));
    this.saga.climbStarted();
    this.bloom = this.saga.bloomTarget;
    this.calm = init.calm === true;
    this.tutorialDone = init.tutorialDone === true || this.cycles > 0;

    // distant bird flocks drifting across the sky
    /*
     * Flocks, not confetti. Birds used to be scattered at random across 700
     * units and drawn as a pair of strokes — an "M" from a child's drawing.
     * Now each flock keeps a loose V behind a leader, every bird on its own
     * wingbeat, and they flap, glide, flap, the way large birds actually do.
     */
    const flock = (par: number, speed: number, count: number, fy: number, size: number) => {
      // where the flock starts across the frame, in pixels, through its parallax
      const lead = ((Math.random() - 0.5) * 900) / par;
      const dir = Math.sign(speed) || 1;
      for (let i = 0; i < count; i++) {
        const rank = Math.ceil(i / 2);
        const side = i % 2 ? 1 : -1;
        this.birds.push({
          // spacing in screen pixels, converted back through the flock's parallax
          ox: lead - (dir * rank * (15 + Math.random() * 6) + side * 3) / par,
          fy: fy + side * rank * 0.016 + (Math.random() - 0.5) * 0.006,
          par,
          speed,
          flap: Math.random() * Math.PI * 2,
          flapSpeed: 7 + Math.random() * 2.5,
          wander: Math.random() * Math.PI * 2,
          bob: 1.5 + Math.random() * 2,
          bobFreq: 0.5 + Math.random() * 0.6,
          size: size * (0.85 + Math.random() * 0.3),
          glide: 0,
          nextGlide: 0.6 + Math.random() * 2.5,
          bank: Math.random() * Math.PI * 2,
        });
      }
    };
    flock(0.09, 30, 5, 0.15, 1);
    flock(0.06, -22, 4, 0.22, 0.75);
    flock(0.13, 38, 3, 0.09, 1.25);

    // dust in the light: a few dozen motes that only show near the sun
    for (let i = 0; i < 46; i++) {
      this.motes.push({
        fx: Math.random(),
        fy: 0.25 + Math.random() * 0.6,
        z: 0.3 + Math.random() * 0.7,
        ph: Math.random() * Math.PI * 2,
      });
    }
  }

  // ---------- lifecycle ----------
  start() {
    this.resize();
    this.prebuildClouds();
    this.last = performance.now();
    const loop = (t: number) => {
      const dt = Math.min(0.05, (t - this.last) / 1000);
      this.last = t;
      // the notebook is open: the hill holds its breath, and so does the battery
      if (this.paused) {
        this.raf = requestAnimationFrame(loop);
        return;
      }
      this.update(dt * this.timeScale(dt));
      const r0 = performance.now();
      this.render();
      this.watchFrameCost(performance.now() - r0, t);
      this.raf = requestAnimationFrame(loop);
    };
    this.raf = requestAnimationFrame(loop);
  }

  /** start a stretch of slow motion: `dur` real seconds, `depth` 0-1 */
  private slowMo(dur: number, depth: number) {
    if (!this.cinematic) return;
    this.slowDur = dur;
    this.slowT = dur;
    this.slowDepth = depth;
  }

  /** how fast game time runs this frame: eases into the slow motion and out */
  private timeScale(realDt: number): number {
    if (this.slowT <= 0) return 1;
    this.slowT = Math.max(0, this.slowT - realDt);
    const p = 1 - this.slowT / this.slowDur;
    const env = Math.min(1, p / 0.12, (1 - p) / 0.4);
    const e = env * env * (3 - 2 * env);
    return 1 - this.slowDepth * e;
  }

  stop() {
    cancelAnimationFrame(this.raf);
    this.audio.dispose();
  }

  /** the start card is gone: from here on input moves him and the clock runs */
  activate() {
    this.active = true;
  }

  restart() {
    this.breath = 1;
    this.slipT = 0;
    this.checkpoint = 0;
    this.climbT = 0;
    this.streak = 0;
    this.streakT = 99;
    this.heaveFx = null;
    this.stuckT = 0;
    this.x = 40;
    this.vx = 0;
    this.roll = 0;
    this.input = 0;
    this.phase = "playing";
    this.phaseT = 0;
    this.zeus.state = "gone";
    this.hop = 0;
    this.hopV = 0;
    this.flash = 0;
    this.kickT = -1;
    this.charX = this.x - 64;
    this.cycles += 1;
    this.baseHour = SCHEDULE[this.cycles % SCHEDULE.length]!;
    this.hour = this.baseHour;
    // a new hill every third cycle, and the weather for this one
    this.levelIndex = levelForCycle(this.cycles);
    this.level = getLevel(this.levelIndex);
    this.weather = this.saga.weatherFor(this.cycles, weatherFor(this.cycles));
    this.wxFade = 1;
    this.maxX = 0;
    this.epiphany = false;
    this.eagleDone = false;
    this.mireMet = false;
    this.saga.climbStarted();
    this.particles = [];
    this.runSlips = 0;
    this.runRests = 0;
    this.runMaxStreak = 0;
    this.stormT = 4;
    this.emit();
  }

  /** the moon's phase moves on a little every cycle */
  private moonPhase() {
    return (this.cycles * 0.123 + 0.38) % 1;
  }

  /** Tab: kick the boulder loose up the ramp; it rolls back while the character sprints ahead */
  kick() {
    if (!this.active || this.phase !== "playing" || this.kickT >= 0 || this.slipT > 0) return;
    if (this.breath < KICK_COST) {
      this.denyT = 0;
      this.audio.strain(false);
      this.restHintT = Math.max(this.restHintT, 2.5);
      return;
    }
    this.breath -= KICK_COST;
    this.streak = 0;
    this.haptic(30);
    this.kickT = 0;
    this.vx = KICK_V;
    this.shake = Math.max(this.shake, 0.4);
    this.audio.kick();
  }

  /** set by the interface while a scene plays: the climb stops */
  cutscene = false;
  /** the last scene: at the bottom, his hands on the stone */
  finaleOn = false;
  private finaleT = 0;
  onFinale: () => void = () => {};

  /** she has gone: he walks on down to the stone, and there the story ends */
  meropeGone() {
    this.meropeLeaving = true;
    this.meropeLeave = 0;
  }

  private startFinale() {
    this.finaleOn = true;
    this.finaleT = 0;
    this.phase = "playing";
    this.x = 0;
    this.vx = 0;
    this.charX = -64;
    this.kickT = -1;
    this.weather = "clear";
    this.audio.swell();
    this.emit();
    this.onFinale();
  }

  /** the end card has been closed: the story is over, the climbing is not */
  endFinale() {
    this.finaleOn = false;
    this.saga.complete();
    this.restart();
  }

  /**
   * The Mire of Regret. Once he knows what he did to her, a stretch of the
   * path below her turns to black mud: slow, heavy, sucking at his feet. It
   * is there through the whole of the second act, and gone after.
   */
  private mireSpan(): [number, number] | null {
    if (this.saga.stage !== 2) return null;
    const L = this.level.length;
    return [L * 0.52, L * 0.64];
  }
  private inMire(x: number) {
    const m = this.mireSpan();
    return !!m && x > m[0] && x < m[1];
  }
  private mireMet = false;
  onMire: () => void = () => {};

  /** where a companion's wedge sits on this hill */
  private wedgeX() {
    return this.level.length * 0.48;
  }

  /**
   * The grip went. Despair grows — and if it grows far enough mid-climb, the
   * sky turns with it — and the moment is kept as an echo on the hillside.
   */
  private onSlip() {
    this.saga.slipped();
    const d = this.saga.s.despair;
    if (this.cycles >= 2 && (this.weather === "clear" || this.weather === "fog") && d > 0.55) {
      this.weather = "rain";
      this.wxFade = 0;
    } else if (this.weather === "rain" && d > 0.75) {
      this.weather = "storm";
      this.wxFade = 0.4;
    }
    // the echo: a copy of how he looks right now, left where it happened
    const layers = this.figLayers;
    if (layers.length === 3 && layers[0] && !this.lowQuality) {
      const src = layers[0];
      const cv = document.createElement("canvas");
      cv.width = src.width;
      cv.height = src.height;
      const c = cv.getContext("2d");
      if (c) {
        for (const l of layers) c.drawImage(l, 0, 0);
        c.globalCompositeOperation = "source-in";
        c.fillStyle = "rgb(190, 210, 240)";
        c.fillRect(0, 0, cv.width, cv.height);
        const s = this.scale;
        this.ghosts.push({
          wx: this.x - 64,
          lv: this.level.id,
          cv,
          w: 230 * s,
          h: 190 * s,
          ox: -110 * s,
          oy: -170 * s,
        });
        if (this.ghosts.length > 8) this.ghosts.shift();
      }
    }
  }

  /** pull a quote at random, without repeating until the deck is exhausted */
  private nextQuote(): Quote | null {
    const list = quotesFor(this.lang);
    const all = Array.from({ length: QUOTE_COUNT }, (_, i) => i);
    /*
     * Each summit turns up a page not yet found, while there are any — so the
     * collection always grows. Once every page is found, they come round again
     * at random and the summit is just the words.
     */
    const have = new Set(this.unlocked);
    const fresh = all.filter((i) => !have.has(i));
    if (fresh.length > 0) {
      // the sayings follow his mind: revolt, then the void, then the absurd
      const idx = this.saga.pickQuote(fresh) ?? fresh[0]!;
      this.unlocked.push(idx);
      this.newQuote = idx;
      this.currentQuoteIndex = idx;
      return list[idx] ?? null;
    }
    this.newQuote = -1;
    if (this.quoteDeck.length === 0) {
      const deck = [...all];
      for (let i = deck.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        const a = deck[i]!;
        deck[i] = deck[j]!;
        deck[j] = a;
      }
      this.quoteDeck = deck;
    }
    const idx = this.quoteDeck.pop()!;
    this.currentQuoteIndex = idx;
    return list[idx] ?? null;
  }

  /**
   * A page found away from the hill (the saying of the day). Returns whether it
   * was new.
   */
  grantQuote(idx: number): boolean {
    if (idx < 0 || idx >= QUOTE_COUNT || this.unlocked.includes(idx)) return false;
    this.unlocked.push(idx);
    this.emit();
    return true;
  }

  /** a language's aphorisms have arrived: put the current one into words */
  refreshQuotes() {
    if (this.currentQuoteIndex >= 0) {
      this.currentQuote = quotesFor(this.lang)[this.currentQuoteIndex] ?? null;
    }
    this.emit();
  }

  /** switch the UI and the aphorisms to another language */
  setLanguage(lang: LangCode) {
    if (lang === this.lang) return;
    this.lang = lang;
    // keep the aphorism currently written in the sky in the new language
    if (this.currentQuoteIndex >= 0) {
      this.currentQuote = quotesFor(lang)[this.currentQuoteIndex] ?? null;
    }
    this.emit();
  }

  resize() {
    const r = this.canvas.getBoundingClientRect();
    // A pixel ratio is a quality setting, not a constant. Take the device's, then
    // spend no more than the budget allows: small windows keep the full ratio,
    // large ones give some of it back rather than giving frames back.
    const want = Math.min(2, window.devicePixelRatio || 1);
    const area = Math.max(1, r.width * r.height);
    this.dpr = Math.max(1, Math.min(want, Math.sqrt(PIXEL_BUDGET / area)));
    this.w = r.width;
    this.h = r.height;
    this.canvas.width = Math.floor(r.width * this.dpr);
    this.canvas.height = Math.floor(r.height * this.dpr);
    this.ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
  }

  private progress() {
    return Math.max(0, Math.min(1, this.x / this.level.length));
  }

  private emit() {
    const q = this.currentQuote;
    const progress = this.progress();
    this.lastEmitKey = this.stateKey(progress);
    this.onState({
      phase: this.phase,
      progress,
      levelName: LEVEL_NAMES[this.level.id]?.[this.lang] ?? "",
      epigraph: q ? `${q.text} — ${q.author}` : "",
      cycles: this.cycles,
      climbTime: this.climbT,
      lastClimb: this.lastClimb,
      bestClimb: this.bestClimb,
      newBest: this.newBest,
      hint: this.hint(),
      unlocked: [...this.unlocked],
      newQuote: this.newQuote,
      weather: this.weather,
      levelId: this.level.id,
      tutorialDone: this.tutorialDone,
      lastRun: this.lastRun,
    });
  }

  private hint(): EngineState["hint"] {
    if (!this.active || this.phase !== "playing") return null;
    // the first climb: one lesson at a time, each until it has been done
    if (!this.tutorialDone) {
      if (this.tutBreatheT > 0) return "breathe";
      if (this.tutStep === "push") return "push";
      if (this.tutStep === "rhythm") return "rhythm";
    }
    if (this.restHintT > 0) return "rest";
    if (!this.kickLearned && this.stuckT > 2.5 && this.kickT < 0) return "kick";
    return null;
  }

  private stateKey(progress: number) {
    return `${this.hint()}|${this.phase}|${this.cycles}|${this.lang}|${this.currentQuoteIndex}|${this.unlocked.length}|${Math.round(
      progress * 120,
    )}`;
  }

  /**
   * The HUD was being handed a new state object on every single frame, which put
   * a full React render — and a style write on the progress bar, and the layout
   * that follows one — inside the animation loop. Nothing above the canvas needs
   * to know about a change it cannot show: the bar is about a hundred and fifty
   * pixels wide, so a hundred and twenty steps across it is already finer than
   * the display. Everything that actually reads as a change (phase, cycle,
   * language, which aphorism is up) is in the key, so none of them can be missed.
   */
  private emitIfChanged() {
    if (this.stateKey(this.progress()) === this.lastEmitKey) return;
    this.emit();
  }

  // ---------- simulation ----------
  private update(dt: number) {
    const L = this.level;
    this.t += dt;
    const slope = slopeAt(L, this.x);
    const prevPhase = this.phase;
    this.frameDt = dt;

    // the eagle keeps its own time
    if (this.eagle.phase !== "away") {
      this.eagle.t += dt;
      const e = this.eagle;
      if (e.phase === "arrive" && e.t > 2.2) this.eagle = { phase: "feed", t: 0 };
      else if (e.phase === "feed" && e.t > 7) this.eagle = { phase: "leave", t: 0 };
      else if (e.phase === "leave" && e.t > 2.2) this.eagle = { phase: "away", t: 0 };
    }
    if (this.meropeLeaving && this.meropeLeave < 1)
      this.meropeLeave = Math.min(1, this.meropeLeave + dt / 7);
    // a scene, or the ending: the hill holds still while the story plays
    if (this.cutscene || this.finaleOn) {
      if (this.finaleOn) {
        this.finaleT += dt;
        this.smile = Math.min(1, Math.max(this.smile, (this.finaleT - 2.5) / 3));
      }
      this.input = 0;
      this.updateBirds(dt);
      this.updateParticles(dt);
      return;
    }
    if (!this.active) this.input = 0;
    this.denyT += dt;
    const braced =
      this.active &&
      this.phase === "playing" &&
      this.kickT < 0 &&
      this.slipT <= 0 &&
      Math.abs(this.input) <= 0.15;
    this.holdA += ((braced ? 1 : 0) - this.holdA) * Math.min(1, dt * 3);
    // a rest is a brace begun with breath to win back
    if (braced && !this.wasBraced && this.breath < 0.85) this.runRests++;
    this.wasBraced = braced;
    if (!this.tutorialDone && this.phase === "playing" && this.active) {
      if (this.tutStep === "push" && this.x > 300) this.tutStep = "rhythm";
      else if (this.tutStep === "rhythm" && (this.tutGood >= 3 || this.x > 1200))
        this.tutStep = "free";
      // running low: show how to get it back, until he has braced for a moment
      if (this.breath < 0.35 && this.tutBreatheT <= 0 && !this.calm) this.tutBreatheT = 6;
      if (this.tutBreatheT > 0)
        this.tutBreatheT = braced && this.holdT > 1.2 ? 0 : this.tutBreatheT - dt * 0.2;
    }
    // the storm: lightning somewhere over the hill every few seconds
    if (this.weather === "storm" && this.phase !== "summit") {
      this.stormT -= dt;
      if (this.stormT <= 0) {
        this.stormT = 6 + Math.random() * 9;
        this.flash = Math.max(this.flash, 0.28 + Math.random() * 0.2);
        this.audio.thunder();
      }
    }
    if (braced) this.holdT += dt;
    {
      // one creep-and-catch every few seconds, quicker and bigger as breath goes
      const period = 3.4 - (1 - this.breath) * 1.4;
      const u = (this.holdT % period) / period;
      const amp = (2.2 + (1 - this.breath) * 3.5) * this.holdA;
      const ease = (q: number) => q * q * (3 - 2 * q);
      if (u < 0.72) {
        this.creep = -ease(u / 0.72) * amp;
        this.holdDrive = 0;
      } else {
        const r = (u - 0.72) / 0.28;
        this.creep = -amp * (1 - ease(r));
        this.holdDrive = Math.sin(r * Math.PI) * this.holdA;
      }
      // the shove back kicks a little grit from under the back foot
      if (this.holdDrive > 0.85 && Math.random() < 0.25) this.dustBurst(this.x - 94, 1);
    }
    this.streakT += dt;
    this.updateSoundscape(dt);
    this.checkpointT += dt;
    this.restHintT = Math.max(0, this.restHintT - dt);
    if (this.heaveFx) this.heaveFx.t += dt;

    if (this.phase === "playing") {
      if (this.active) this.climbT += dt;
      if (this.kickT >= 0) {
        this.kickT += dt;
        // boulder sails uphill, then gravity rolls it back down the ramp
        this.vx += (-slope * GRAVITY * L.gravityScale - this.vx * 0.12) * dt;
        this.x += this.vx * dt;
        // unburdened, the character sprints up the hill
        this.charX += KICK_SPRINT * dt;
        this.checkLedges();
        // the boulder rolls back to the character: resume pushing
        if (this.x <= this.charX + 64) {
          this.x = this.charX + 64;
          this.vx = 0;
          this.kickT = -1;
          // whatever key is down now was pressed before the catch: not a heave
          this.heaveArmed = this.input < 0.3;
        }
        this.x = Math.max(0, this.x);
        this.breath = Math.min(1, this.breath + BREATH_REGEN * 0.35 * dt);
      } else {
        this.charX = this.x - 64;
        // a heave is the rising edge of the push, with hysteresis so a joystick
        // wavering around the threshold does not fire a stream of them
        if (this.input < 0.3) this.heaveArmed = true;
        else if (this.heaveArmed && this.input >= 0.5 && this.slipT <= 0) {
          this.heaveArmed = false;
          this.heave();
        }

        let push = 0;
        let gravity = -slope * GRAVITY * L.gravityScale;
        let friction = FRICTION;
        // footing: loose scree and wet ground give less back for each step
        const onScree = !!L.scree && this.x > L.scree[0] && this.x < L.scree[1];
        const ramp = this.difficultyRamp();
        // the Mire of Regret: in the void, a stretch of the path turns to mud
        const inMire = this.inMire(this.x);
        if (inMire && !this.mireMet) {
          this.mireMet = true;
          this.onMire();
        }
        const grip =
          (onScree ? 1 - 0.2 * ramp : 1) *
          (this.weather === "rain" || this.weather === "storm" ? 1 - 0.1 * ramp : 1) *
          (inMire ? 0.72 : 1);
        if (inMire) friction *= 1.9;
        // gusts lean on the stone, as hard as the grass is leaning
        gravity -= this.gustForce();
        if (this.slipT > 0) {
          // grip gone: the stone takes him back down until he catches it
          this.slipT -= dt;
          // Patience (Tantalus's gift): the slip gives back less ground
          friction = FRICTION * (this.saga.has("patience") ? 0.85 : 0.5);
          this.breath = Math.min(1, this.breath + BREATH_REGEN * 0.8 * dt);
        } else if (this.input > 0.15) {
          // Prometheus's pain, carried up: the stone is heavier
          push = this.input * PUSH_FORCE * grip * (this.saga.s.burden === "pro" ? 0.84 : 1);
          // Tantalus's thirst: breath goes faster
          const thirst = this.saga.s.burden === "tan" ? 1.3 : 1;
          if (!this.calm)
            this.breath -= BREATH_DRAIN * thirst * this.input * (0.5 + Math.max(0, slope) * 2) * dt;
          else this.saga.steady(dt);
          if (this.breath > 0.3 && this.vx > 30) this.saga.steady(dt);
          if (this.breath <= 0) {
            this.runSlips++;
            this.breath = 0;
            this.slipT = SLIP_TIME * (this.saga.has("patience") ? 0.7 : 1);
            this.onSlip();
            this.shake = Math.max(this.shake, 0.5);
            this.audio.gasp();
            this.streak = 0;
            this.haptic(70);
            if (this.restHintsShown < 3) {
              this.restHintsShown++;
              this.restHintT = 4;
            }
          }
        } else if (this.input < -0.15) {
          push = this.input * PUSH_FORCE;
        } else {
          // Braced: shoulder in, legs locked. On the ordinary slope that holds the
          // stone where it is; on a ledge's pitch it is too steep to hold, so the
          // only safe place to rest is below one or on top of one.
          // (on scree the stones under it give, and it creeps back)
          gravity *= Math.max(onScree ? 0.22 * ramp : 0, Math.min(1, (slope - 0.6) / 0.5)) * 0.8;
          friction = FRICTION * 4;
          // Fire (Prometheus's gift): breath comes back faster
          this.breath = Math.min(
            1,
            this.breath + BREATH_REGEN * (this.saga.has("fire") ? 1.3 : 1) * dt,
          );
        }
        const a = push + gravity - this.vx * friction;
        this.vx += a * dt;
        this.x += this.vx * dt;
        // the wedge a companion laid: the first time the stone runs back past
        // it, it stops there
        const wedgeX = this.wedgeX();
        if (this.saga.s.wedge && this.maxX > wedgeX + 30 && this.x < wedgeX && this.vx < 0) {
          this.x = wedgeX;
          this.vx = 0;
          this.slipT = 0;
          this.wedgeT = 0;
          this.shake = Math.max(this.shake, 0.45);
          this.audio.impact();
          this.haptic(40);
          this.dustBurst(this.x, 6);
          this.saga.wedgeHeld();
        }
        this.maxX = Math.max(this.maxX, this.x);
        if (this.active) this.saga.ascent(this.x / L.length, this.cycles);
        this.checkLedges();
        const onPitch = (this.level.ledges ?? []).some(
          ([lx]) => this.checkpoint < lx && this.x > lx - LEDGE_RUN - 30 && this.x < lx + 8,
        );
        if (onPitch && this.input > 0.15 && this.vx < 40) this.stuckT += dt;
        else if (!onPitch) this.stuckT = 0;
        if (this.x < 0) {
          this.x = 0;
          this.vx = Math.max(0, this.vx);
        }
        // footsteps + straining while actively pushing forward
        if (this.input > 0.15 && this.vx > 8) {
          this.stepT -= dt * (0.6 + Math.min(1.6, this.vx / 90));
          if (this.stepT <= 0) {
            this.stepT = 0.62;
            this.audio.step();
          }
          this.strainT -= dt;
          if (this.strainT <= 0) {
            this.strainT = 0.85;
            this.audio.strain(this.breath < 0.25);
          }
        }
      }
      if (this.x >= L.length) {
        this.x = L.length;
        this.vx = 0;
        this.kickT = -1;
        this.lastClimb = this.climbT;
        this.newBest = this.bestClimb === 0 || this.climbT < this.bestClimb;
        if (this.newBest) this.bestClimb = this.climbT;
        this.currentQuote = this.nextQuote();
        // every seventh summit the sky carries the player's own words instead
        if (this.ownQuote && this.cycles % 7 === 6) this.currentQuote = this.ownQuote;
        // the story moves on; and once, here, he understands
        this.saga.summit(this.runSlips);
        this.tutorialDone = true;
        this.lastRun = {
          id: this.cycles,
          seconds: this.climbT,
          slips: this.runSlips,
          rests: this.runRests,
          maxStreak: this.runMaxStreak,
          night: this.st.elev < -6,
          weather: this.weather,
          levelId: this.level.id,
          calm: this.calm,
        };
        /*
         * There is no last cycle. This used to stop at fifty and show a "50 / 50
         * cycles complete" card, which is the one thing the myth does not have —
         * a finish. Reaching the summit always sends him back down now, and the
         * counter just keeps going up.
         */
        this.phase = "summit";
        this.phaseT = 0;
        this.zeus.state = "appear";
        this.zeus.t = 0;
        this.summitX = this.x - 64;
        this.audio.toll();
        this.haptic([20, 60, 20]);
        // the summit: the world slows for a breath (the ending takes its time instead)
        if (!this.epiphany) this.slowMo(1.2, 0.5);
      }
    } else if (this.phase === "summit") {
      this.phaseT += dt;
      this.zeus.t += dt;
      this.vx *= 0.9;
      // He takes his time now: the storm gathers, he rises out of it with the
      // bolt drawn back, and only then does he throw.
      // at the ending the god waits a long while, and he looks out and smiles
      const strikeAt = this.epiphany ? EPIPHANY_STRIKE : ZEUS_STRIKE;
      // the ending: the sky stays clear while he looks out, and only then
      // does the storm gather
      if (this.epiphany) {
        if (this.phaseT < EPIPHANY_STRIKE - 2.4) {
          this.zeus.state = "gone";
          this.zeus.t = 0;
        } else if (this.zeus.state === "gone") {
          this.zeus.state = "appear";
          this.zeus.t = 0;
        }
      }
      if (this.epiphany) this.smile = Math.min(1, Math.max(this.smile, (this.phaseT - 2.2) / 2.5));
      if (this.phaseT >= strikeAt && this.zeus.state === "appear") {
        this.zeus.state = "strike";
        this.zeus.t = 0;
        this.flash = 0.7;
        this.shake = Math.max(this.shake, 1);
        // the strike in slow motion, and the camera jolted in toward it
        this.slowMo(1.1, 0.72);
        this.zoomKick = Math.max(this.zoomKick, 0.07);
        this.audio.thunder();
        this.haptic([0, 40, 30, 120]);
        // the stone is hit: sparks and grit thrown off it
        for (let i = 0; i < 26; i++) {
          const a = -Math.PI * (0.1 + Math.random() * 0.8);
          const v = 80 + Math.random() * 220;
          this.particles.push({
            wx: this.x + (Math.random() - 0.5) * 20,
            wy: terrainAt(L, this.x) + 50 + Math.random() * 20,
            vx: Math.cos(a) * v,
            vy: Math.sin(a) * v,
            life: 0,
            maxLife: 0.5 + Math.random() * 0.6,
            size: (1.2 + Math.random() * 2) * this.scale,
            kind: i % 3 ? "spark" : "dust",
          });
        }
      }
      if (this.phaseT > strikeAt + (ZEUS_ROLL - ZEUS_STRIKE)) {
        this.phase = "rolling";
        this.phaseT = 0;
        this.vx = -260;
        this.zeus.state = "fade";
        this.zeus.t = 0;
        this.flash = 0.18;
        const ledges = L.ledges ?? [];
        this.rollLedge = ledges.length - 1;
      }
    } else if (this.phase === "rolling") {
      this.phaseT += dt;
      if (this.zeus.state === "fade") {
        this.zeus.t += dt;
        if (this.zeus.t > 1.4) this.zeus.state = "gone";
      }
      this.vx += -slope * ROLL_GRAVITY * dt - this.vx * 0.25 * dt;
      this.vx = Math.max(this.vx, -1400);
      this.x += this.vx * dt;
      // every ledge it worked for, it now drops off: a jolt, a bounce, a cloud
      const ledges = L.ledges ?? [];
      const drop = ledges[this.rollLedge];
      if (drop && this.x < drop[0] - LEDGE_RUN * 0.5) {
        this.rollLedge--;
        this.hopV = 1.6 + Math.min(1.2, Math.abs(this.vx) / 600);
        this.shake = Math.max(this.shake, 0.55);
        this.audio.impact();
        this.haptic(30);
        this.dustBurst(this.x, 14);
      }
      if (this.x <= 0) {
        this.x = 0;
        if (this.phase === "rolling") this.audio.impact();
        this.shake = 1;
        this.vx = 0;
        this.phase = "descent";
        this.phaseT = 0;
        // the walk down: from where he stood at the top, while the hours pass
        this.walkX = this.summitX;
        this.walkFrom = this.summitX;
        this.walkV = 0;
        this.hourFrom = this.hour;
        let next = SCHEDULE[(this.cycles + 1) % SCHEDULE.length]!;
        while (next <= this.hourFrom) next += 24;
        this.hourTo = next;
      }
    } else if (this.phase === "descent") {
      /*
       * The descent. Camus thought this was the part that mattered: the hour of
       * consciousness, the man walking back down to his stone with nothing in
       * his hands. So the player walks it, at a walking pace, holding any
       * direction; the light changes around him on the way, and when he reaches
       * the stone the next climb begins.
       */
      this.phaseT += dt;
      const want = this.active && Math.abs(this.input) > 0.15 ? WALK_SPEED : 0;
      this.walkV += (want - this.walkV) * Math.min(1, dt * 4);
      this.walkX -= this.walkV * dt;
      this.saga.descent(this.walkX / L.length);
      this.breath = Math.min(1, this.breath + BREATH_REGEN * 0.5 * dt);
      if (this.walkV > 20) {
        this.stepT -= dt * (0.6 + this.walkV / 120);
        if (this.stepT <= 0) {
          this.stepT = 0.62;
          this.audio.step();
        }
      }
      const done = 1 - (this.walkX + 24) / Math.max(1, this.walkFrom + 24);
      this.hour = this.hourFrom + (this.hourTo - this.hourFrom) * Math.max(0, Math.min(1, done));
      if (this.walkX <= -24) {
        // after she has gone: he comes down to his stone, and the story ends there
        if (this.saga.s.finale) this.startFinale();
        else this.restart();
      }
    } else {
      this.phaseT += dt;
    }
    // the climb itself takes a little time too
    if (this.phase === "playing") this.hour = this.baseHour + this.progress() * 0.35;

    this.roll += (this.vx * dt) / 46;
    // the hop: a little ballistic arc in units of the stone's radius
    if (this.hop > 0 || this.hopV > 0) {
      this.hop += this.hopV * dt;
      this.hopV -= 9 * dt;
      if (this.hop <= 0) {
        this.hop = 0;
        this.hopV = this.hopV < -1.2 ? -this.hopV * 0.3 : 0;
      }
    }

    // The gait follows the feet, not the clock and not the boulder. Driving it off
    // `roll` meant the legs blurred at thirty cycles a second while the stone
    // thundered back down the hill; capping the cadence keeps him human.
    const charSpeed =
      this.phase === "descent" ? this.walkV : this.kickT >= 0 ? KICK_SPRINT : Math.abs(this.vx);
    this.gait += Math.min(charSpeed / STRIDE_LENGTH, MAX_CADENCE) * Math.PI * 2 * dt;

    this.shake = Math.max(0, this.shake - dt * 2.2);
    this.flash = Math.max(0, this.flash - dt * 1.6);
    this.bloom += (this.saga.bloomTarget - this.bloom) * Math.min(1, dt * 0.25);
    this.wxFade = Math.min(1, this.wxFade + dt / 5);
    this.wedgeT += dt;
    if (this.saga.s.ended) this.smile = Math.max(this.smile, 0.7);

    this.updateBirds(dt);
    this.updateParticles(dt);
    this.emitParticles();

    // The friction bus is smoothed with a time constant of ~0.1s, so feeding it
    // sixty target changes a second only piles automation events onto the audio
    // thread; a dozen is past the point where the ear can tell.
    this.frictionT -= dt;
    if (this.frictionT <= 0 || prevPhase !== this.phase) {
      this.frictionT = 1 / 15;
      this.audio.updateFriction(Math.min(1, Math.abs(this.vx) / 420), this.phase === "rolling");
    }
    if (prevPhase !== this.phase) this.emit();
    else this.emitIfChanged();
  }

  /** time to the nearest beat, signed: negative = early */
  private beatError(): number {
    const p = this.t % BEAT;
    return p < BEAT / 2 ? p : p - BEAT;
  }

  private heave() {
    const err = Math.abs(this.beatError());
    const q: HeaveQuality =
      err <= HEAVE.perfect.window ? "perfect" : err <= HEAVE.good.window ? "good" : "miss";
    const h = HEAVE[q];
    // a run of perfect heaves builds momentum: +10% per link, up to +30%
    this.streak = q === "perfect" ? this.streak + 1 : 0;
    this.runMaxStreak = Math.max(this.runMaxStreak, this.streak);
    if (q !== "miss") this.tutGood++;
    if (this.streak >= 2) {
      this.streakShown = this.streak;
      this.streakT = 0;
    }
    const bonus = 1 + Math.min(0.3, Math.max(0, this.streak - 1) * 0.1);
    this.vx = Math.max(this.vx, 0) + h.v * bonus;
    if (q === "perfect") this.haptic(this.streak >= 3 ? 18 : 10);
    this.breath = Math.max(0, Math.min(1, this.breath + h.breath));
    this.heaveFx = { q, t: 0 };
    if (q === "perfect" && this.cinematic) this.zoomKick = Math.min(0.05, this.zoomKick + 0.022);
    this.audio.heave(q, this.streak);
    if (q === "perfect") {
      for (let i = 0; i < 6; i++) {
        const wx = this.x - 30 + Math.random() * 12;
        this.particles.push({
          wx,
          wy: terrainAt(this.level, wx) + 4,
          vx: 30 + Math.random() * 50,
          vy: -30 - Math.random() * 40,
          life: 0,
          maxLife: 0.4 + Math.random() * 0.3,
          size: (1.4 + Math.random()) * this.scale,
          kind: "spark",
        });
      }
    }
  }

  /** ledges are one-way: cross a crest and the stone can no longer slip below it */
  private checkLedges() {
    const ledges = this.level.ledges;
    if (ledges) {
      for (const [lx] of ledges) {
        const hold = lx + 8;
        if (this.x > hold && this.checkpoint < hold) {
          this.checkpoint = hold;
          this.checkpointT = 0;
          this.audio.checkpoint();
          this.shake = Math.max(this.shake, 0.35);
          this.haptic([25, 40, 25]);
          this.dustBurst(this.x, 18);
          // gold sparks thrown up off the stele as its flame catches
          for (let i = 0; i < 14; i++) {
            this.particles.push({
              wx: lx + 46 + (Math.random() - 0.5) * 8,
              wy: terrainAt(this.level, lx + 46) + 52,
              vx: (Math.random() - 0.5) * 90,
              vy: -60 - Math.random() * 90,
              life: 0,
              maxLife: 0.7 + Math.random() * 0.6,
              size: (1.2 + Math.random() * 1.4) * this.scale,
              kind: "spark",
            });
          }
          this.stuckT = 0;
          if (this.kickT >= 0) this.kickLearned = true;
        }
      }
    }
    if (this.x < this.checkpoint) {
      this.x = this.checkpoint;
      if (this.vx < 0) this.vx = 0;
    }
  }

  /**
   * The sounds that follow the body and the ground rather than events: his
   * breathing, which quickens and roughens as he works and as his breath runs
   * low; gravel cracking under the stone in proportion to its speed; and the
   * wind, which rises with the same gusts that lean the grass over.
   */
  private updateSoundscape(dt: number) {
    if (!this.active) return;
    const playing = this.phase === "playing";
    const pushing = playing && this.input > 0.15 && this.kickT < 0;
    const work = Math.max(
      pushing ? 0.5 + this.input * 0.3 : 0,
      this.kickT >= 0 ? 0.8 : 0,
      this.slipT > 0 ? 1 : 0,
      1 - this.breath,
      this.phase === "summit" ? 0.6 : 0,
    );
    const rate = 1.4 + work * 3.2; // radians/s of the breath cycle
    this.breathPh += rate * dt;
    if (this.breathPh >= Math.PI) {
      this.breathPh -= Math.PI;
      this.breathIn = !this.breathIn;
      if (work > 0.3 && this.phase !== "descent")
        this.audio.breathe(this.breathIn, Math.min(1, work));
    }

    const speed = Math.abs(this.vx);
    if (speed > 12 && this.hop === 0 && this.phase !== "descent") {
      this.gritT -= dt;
      if (this.gritT <= 0) {
        // more grains the faster it goes, each one random
        this.gritT = Math.max(0.025, 0.4 - speed / 500) * (0.4 + Math.random());
        this.audio.grit(Math.min(1, speed / 400));
      }
    }

    this.windT -= dt;
    if (this.windT <= 0) {
      this.windT = 0.25;
      this.audio.setWind(Math.min(1, Math.abs(this.windAt(this.x)) / 0.3));
      this.audio.setRain(this.weather === "storm" ? 1 : this.weather === "rain" ? 0.65 : 0);
    }
  }

  /*
   * Quality that adapts to the device. The detail added for realism — the
   * textured hillside, the layered rim light on the man, dense grass, dust in
   * the light — is cheap on a desktop GPU and can be too much for an old
   * phone. If drawing a frame keeps costing more than about 14 ms, the
   * expensive extras step down (and stay down for the session); nothing that
   * matters to playing is ever removed.
   */
  private lowQuality = false;
  private costSum = 0;
  private costN = 0;
  private costSince = 0;
  private watchFrameCost(ms: number, now: number) {
    if (this.lowQuality || !this.active) return;
    if (this.costSince === 0) this.costSince = now;
    this.costSum += ms;
    this.costN++;
    if (now - this.costSince > 3000) {
      if (this.costSum / this.costN > 14) this.lowQuality = true;
      this.costSum = 0;
      this.costN = 0;
      this.costSince = now;
    }
  }

  /** a short buzz on phones that have one; silently nothing everywhere else */
  private haptic(pattern: number | number[]) {
    if (!this.active || !this.haptics) return;
    vibrate(pattern);
  }

  /** a cloud of grit thrown up from the ground at world x */
  private dustBurst(wx: number, n: number) {
    for (let i = 0; i < n; i++) {
      const a = -Math.PI * Math.random();
      const v = 30 + Math.random() * 110;
      this.particles.push({
        wx: wx + (Math.random() - 0.5) * 50,
        wy: terrainAt(this.level, wx) + 2,
        vx: Math.cos(a) * v,
        vy: Math.sin(a) * v * 0.6,
        life: 0,
        maxLife: 0.6 + Math.random() * 0.7,
        size: (1.6 + Math.random() * 2.6) * this.scale,
        kind: "dust",
      });
    }
  }

  private updateBirds(dt: number) {
    const w = this.w;
    for (const b of this.birds) {
      b.ox += b.speed * dt;
      b.wander += dt * b.bobFreq;
      b.bank += dt * 0.4;
      if (b.glide > 0) {
        b.glide -= dt;
        // wings settle to a shallow dihedral rather than freezing mid-stroke
        const target = Math.PI * 0.62;
        b.flap += (target - (b.flap % (Math.PI * 2))) * Math.min(1, dt * 6);
        if (b.glide <= 0) b.nextGlide = 0.8 + Math.random() * 2.4;
      } else {
        b.flap += dt * b.flapSpeed;
        b.nextGlide -= dt;
        if (b.nextGlide <= 0) b.glide = 0.8 + Math.random() * 2.2;
      }
      const span = w / (this.scale * b.par);
      const px = w * 0.38 + b.ox * this.scale * b.par;
      if (px < -120) b.ox += span;
      else if (px > w + 120) b.ox -= span;
    }
    this.chirpT -= dt;
    if (this.chirpT <= 0) {
      this.chirpT = 5 + Math.random() * 6;
      this.audio.chirp(0.6 + Math.random() * 0.7, Math.random() < 0.45);
    }
  }

  private updateParticles(dt: number) {
    // compacted in place: `filter` was handing the collector a fresh array of up
    // to a hundred and sixty objects every frame, for a list that usually loses
    // one or two members
    let kept = 0;
    for (const p of this.particles) {
      p.life += dt;
      if (p.life >= p.maxLife) continue;
      p.wx += p.vx * dt;
      p.wy += p.vy * dt;
      p.vy += 40 * dt; // gentle gravity settles the dust
      p.vx *= 1 - dt * 1.5;
      this.particles[kept++] = p;
    }
    this.particles.length = kept;
  }

  private emitParticles() {
    if (this.particles.length > 160) this.particles.length = 160;
    const rolling = this.phase === "rolling";
    const pushing = this.phase === "playing" && this.input > 0.15 && Math.abs(this.vx) > 8;
    if (!rolling && !pushing) return;
    const rate = rolling ? 2 : 1;
    for (let i = 0; i < rate; i++) {
      if (Math.random() < 0.6) continue;
      const wx = this.x + (Math.random() - 0.5) * 34;
      this.particles.push({
        wx,
        wy: terrainAt(this.level, wx) + 2,
        vx: (Math.random() - 0.5) * 40 - this.vx * 0.12,
        vy: -Math.random() * 30,
        life: 0,
        maxLife: 0.5 + Math.random() * 0.5,
        size: (1.2 + Math.random() * 1.7) * this.scale,
        kind: Math.random() < 0.2 ? "spark" : "dust",
      });
    }
  }

  private renderParticles() {
    const ctx = this.ctx;
    const { w } = this;
    for (const p of this.particles) {
      const t = p.life / p.maxLife;
      const px = (p.wx - this.camX) * this.scale;
      const py = this.groundY - (p.wy - this.camY) * this.scale * 0.55 + this.shakeY;
      if (px < -20 || px > w + 20 || py < -10 || py > this.h + 10) continue;
      ctx.globalAlpha = (1 - t) * (p.kind === "spark" ? 0.5 : 0.32);
      if (p.kind === "spark") {
        ctx.fillStyle = "oklch(0.9 0.12 75)";
        ctx.beginPath();
        ctx.arc(px, py, p.size * 0.6, 0, Math.PI * 2);
        ctx.fill();
      } else {
        ctx.fillStyle = "oklch(0.52 0.03 60)";
        ctx.beginPath();
        ctx.arc(px, py, p.size, 0, Math.PI * 2);
        ctx.fill();
      }
    }
    ctx.globalAlpha = 1;
  }

  // ---------- rendering ----------
  private render() {
    let ctx = this.ctx;
    const L = this.level;
    const { w, h } = this;
    // no clear: the sky is opaque and covers the frame before anything else lands

    this.scale = Math.min(1, w / 900) * 0.92 + 0.28;
    this.groundY = h * 0.78;
    this.shakeY = this.shake * (Math.random() - 0.5) * 10;

    // camera keeps the action (boulder or kick midpoint) slightly left of centre,
    // smoothed so the rejoin of character and boulder doesn't jump
    const camTargetX =
      this.phase === "descent"
        ? this.walkX + 40
        : this.kickT >= 0
          ? (this.x + this.charX) / 2
          : this.x;
    const desiredCamX = camTargetX - (w * 0.38) / this.scale;
    this.camX += (desiredCamX - this.camX) * Math.min(1, this.frameDt * 12);
    // a camera held by hand, not mounted: a slow, small drift that never settles
    const driftX =
      (Math.sin(this.t * 0.31) * 1.6 + Math.sin(this.t * 0.73 + 1.1) * 0.7) / this.scale;
    this.camX += driftX;
    this.shakeY += Math.sin(this.t * 0.27 + 2) * 1.1 + Math.sin(this.t * 0.61) * 0.5;
    this.camY = terrainAt(L, this.camX);

    const toScreenX = (wx: number) => (wx - this.camX) * this.scale;
    const toScreenY = (wy: number) =>
      this.groundY - (wy - this.camY) * this.scale * 0.55 + this.shakeY;

    // the camera move for this frame (see `cinematic`), applied as one transform
    // over everything in the world; the interface on top is drawn without it
    const z = this.cameraMove();
    const zfx = this.focusX;
    const zfy = this.focusY;
    const dpr = this.dpr;
    const zoomOn = (c: CanvasRenderingContext2D) =>
      c.setTransform(
        dpr * z,
        0,
        0,
        dpr * z,
        dpr * (zfx * (1 - z) + this.shakeX),
        dpr * zfy * (1 - z),
      );
    const zoomOff = (c: CanvasRenderingContext2D) => c.setTransform(dpr, 0, 0, dpr, 0, 0);
    const zoomed = z !== 1 || this.shakeX !== 0;
    if (zoomed) zoomOn(this.ctx);

    this.st = skyState(this.hour, this.moonPhase());
    this.drawSky();
    this.drawCelestial();
    // the moon behind the clouds: they are solid enough now to pass in front of it
    this.drawMoonNow();
    this.drawClouds();
    this.drawOvercast();

    /*
     * The landscape goes into its own layer, so that the hour can grade it —
     * night blue, dawn rose, the flat light of noon — without grading the sky,
     * which is already the colour it should be. On slow devices there is no
     * layer and the grade goes straight over the frame.
     */
    const mainCtx = this.ctx;
    let land: CanvasRenderingContext2D | null = null;
    if (!this.lowQuality) {
      const pw = Math.ceil(w * this.dpr);
      const ph = Math.ceil(h * this.dpr);
      const lc = this.landLayer ?? document.createElement("canvas");
      if (lc.width !== pw || lc.height !== ph) {
        lc.width = pw;
        lc.height = ph;
      }
      this.landLayer = lc;
      land = lc.getContext("2d");
      if (land) {
        land.setTransform(1, 0, 0, 1, 0, 0);
        land.globalCompositeOperation = "source-over";
        land.globalAlpha = 1;
        land.clearRect(0, 0, pw, ph);
        land.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
        if (zoomed) zoomOn(land);
        this.ctx = land;
        ctx = land;
      }
    }
    this.drawMountain();
    this.renderBirds();
    this.drawMist();
    this.drawMotes();

    // Parallax ridges. These are landforms in front of the horizon, so they have
    // to stay under it: at RIDGE_RELIEF 0.4 the nearest rank sampled terrain
    // 2100 units ahead and crested at 0.35h, two hundred pixels above the sun,
    // burying it for most of the climb. No amount of recolouring fixes a mass
    // that is simply standing in front of the light.
    const horizonY = this.st.horizon * h;
    L.ridges.forEach(([amp, freq, off], i) => {
      const p = 0.15 + i * 0.16;
      ctx.beginPath();
      ctx.moveTo(0, h);
      for (let sx = 0; sx <= w; sx += 8) {
        const wx = this.camX * p + sx / this.scale + off;
        const relief =
          (terrainAt(L, wx) * 0.5 + Math.sin(wx * freq) * amp - this.camY * p) *
          this.scale *
          RIDGE_RELIEF;
        const raw = this.groundY - relief - 18 * i;
        const floor = horizonY + (4 + 6 * i) * this.scale;
        // Anything that would breach the skyline gets eased back under it rather
        // than sheared off at it — a hard clamp would leave a ruled flat line
        // along the crests wherever the wave went over.
        const y = raw >= floor ? raw : floor - (floor - raw) * 0.25;
        ctx.lineTo(sx, y);
      }
      ctx.lineTo(w, h);
      ctx.closePath();
      // Atmospheric perspective: the air between you and a ridge drains its
      // warmth and lifts it. Nearest the sun that haze is gold, not blue.
      ctx.fillStyle = RIDGE_TONES[i] ?? RIDGE_TONES[RIDGE_TONES.length - 1]!;
      ctx.fill();
    });

    // Main terrain. The same curve used to be resolved three times over — once
    // for the fill, once for the worn trail, once for the ridgeline — at four
    // pixels a step, so a wide window was paying for fifteen hundred evaluations
    // of a power and two sines to draw one line three times. Sampled once here.
    const tn = Math.floor((w + 4) / 4) + 2;
    if (!this.terrainYs || this.terrainYs.length !== tn) this.terrainYs = new Float64Array(tn);
    const tys = this.terrainYs;
    const tsx = (i: number) => Math.min(-2 + i * 4, w + 2);
    for (let i = 0; i < tn; i++) {
      tys[i] = toScreenY(terrainAt(L, this.camX + tsx(i) / this.scale));
    }

    ctx.beginPath();
    ctx.moveTo(-2, h + 2);
    for (let i = 0; i < tn; i++) ctx.lineTo(tsx(i), tys[i]!);
    ctx.lineTo(w + 2, h + 2);
    ctx.closePath();
    const groundG = ctx.createLinearGradient(0, this.groundY - 120, 0, h);
    // the face goes cool and dark toward us: lit by the sky, not the sun
    groundG.addColorStop(0, "oklch(0.36 0.05 54)");
    groundG.addColorStop(0.45, "oklch(0.28 0.042 48)");
    groundG.addColorStop(1, "oklch(0.17 0.03 300)");
    ctx.fillStyle = groundG;
    ctx.fill();

    // the fill is one flat wash over the biggest area in the frame; this is what
    // turns it into rock. Clipped to the same path, so nothing can spill upward
    // into the sky.
    ctx.save();
    ctx.clip();
    this.drawGroundDetail(toScreenX, toScreenY);
    ctx.restore();

    // lighter dirt trail worn by the endless rolling boulder
    ctx.strokeStyle = "oklch(0.4 0.05 58 / 0.35)";
    ctx.lineWidth = 24 * this.scale;
    ctx.lineCap = "round";
    ctx.beginPath();
    for (let i = 0; i < tn; i++) {
      if (i === 0) ctx.moveTo(tsx(i), tys[i]! - 1);
      else ctx.lineTo(tsx(i), tys[i]! - 1);
    }
    ctx.stroke();

    // sunlit ridgeline
    ctx.strokeStyle = "oklch(0.66 0.06 62 / 0.7)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    for (let i = 0; i < tn; i++) {
      if (i === 0) ctx.moveTo(tsx(i), tys[i]!);
      else ctx.lineTo(tsx(i), tys[i]!);
    }
    ctx.stroke();

    /*
     * Outcrops go on last of the three, and unclipped.
     *
     * Their whole job is to break the skyline — the high-contrast edge the eye
     * reads the hill's shape from — so clipping them to the ground would sand
     * them flat against it. And drawn before the trail, as they were at first,
     * the twenty-four-pixel trail stroke painted straight over the top of them
     * and they simply vanished.
     */
    this.drawOutcrops(toScreenX, toScreenY);

    // dry grass along the skyline, leaning with the wind, seed heads lit
    // from behind
    const grassStep = this.lowQuality ? 36 : 18;
    for (
      let wx = Math.floor(this.camX / grassStep) * grassStep;
      wx < this.camX + w / this.scale + grassStep;
      wx += grassStep
    ) {
      const h1 = this.hash(wx * 1.31);
      if (h1 < 0.38) continue;
      const gwx = wx + (h1 - 0.5) * grassStep;
      grassClump(
        ctx,
        toScreenX(gwx),
        toScreenY(terrainAt(L, gwx)) + 1,
        (5 + h1 * 7) * this.scale,
        Math.floor(wx),
        this.windAt(gwx),
        "oklch(0.2 0.03 60 / 0.9)",
        "rgba(255, 210, 140, 0.75)",
      );
      // spring: when he has climbed well for a while the hill flowers
      if (this.bloom > 0.04) {
        const h2 = this.hash(wx * 2.77 + 0.5);
        if (h2 < this.bloom * 0.75) {
          const fx = toScreenX(gwx) + (h2 - 0.3) * 6 * this.scale;
          const fy = toScreenY(terrainAt(L, gwx)) - (4 + h1 * 6) * this.scale;
          const pal = [
            "oklch(0.6 0.22 28)",
            "oklch(0.86 0.15 95)",
            "oklch(0.95 0.02 90)",
            "oklch(0.62 0.14 310)",
          ];
          ctx.fillStyle = pal[Math.floor(this.hash(wx * 5.3) * pal.length)]!;
          const r = (1.3 + h2 * 1.4) * this.scale * Math.min(1, this.bloom * 1.6);
          ctx.beginPath();
          ctx.arc(fx, fy, r, 0, Math.PI * 2);
          ctx.fill();
        }
      }
    }

    // summit marker
    const sumX = toScreenX(L.length);
    if (sumX > -60 && sumX < w + 60) {
      const sy = toScreenY(terrainAt(L, L.length));
      ctx.strokeStyle = "oklch(0.78 0.11 70 / 0.65)";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(sumX, sy);
      ctx.lineTo(sumX, sy - 70 * this.scale);
      ctx.stroke();
    }

    // Mediterranean vegetation + scattered stones on the trail
    this.drawStones(toScreenX, toScreenY);
    this.drawOliveTrees(toScreenX, toScreenY);
    this.drawSteles(toScreenX, toScreenY);
    this.drawEchoes(toScreenX, toScreenY);
    this.drawNpcs(toScreenX, toScreenY);
    this.drawWedge(toScreenX, toScreenY);

    // the storm and the god come in over everything behind the man
    this.drawZeus(toScreenX, toScreenY);

    // boulder + figure
    const R = 34 * this.scale;
    const sx = this.x + this.creep;
    const bx = toScreenX(sx);
    const by = toScreenY(terrainAt(L, sx)) - this.hop * R * 1.4;
    const ang = slopeAt(L, this.x) * 0.5;

    // once the stone is gone he stays where it left him, at the top
    const left = this.phase === "rolling" || this.phase === "descent";
    const fwx =
      this.phase === "descent"
        ? this.walkX
        : left
          ? this.summitX
          : this.kickT >= 0
            ? this.charX
            : sx - 64;
    // long shadows thrown toward us by the low sun behind the hill
    if (this.zeus.state === "gone") {
      this.drawLongShadow(
        toScreenX(fwx),
        toScreenY(terrainAt(L, fwx)),
        9 * this.scale,
        120 * this.scale,
      );
      this.drawLongShadow(bx, by, R * 0.9, 170 * this.scale);
    }
    // figure stands downhill of the boulder, feet on the real terrain
    const fxs = toScreenX(fwx);
    // in the mire his feet sink
    const fys = toScreenY(terrainAt(L, fwx)) + (this.inMire(fwx + 40) ? 3.5 * this.scale : 0);
    // what the camera holds on next frame: him, a little above his shoulders,
    // and at the summit high enough to keep the god in the frame
    this.focusTX = fxs + (this.phase === "summit" ? 60 * this.scale : 20 * this.scale);
    this.focusTY =
      this.phase === "summit"
        ? this.epiphany
          ? fys - 92 * this.scale
          : fys - 150 * this.scale
        : fys - 70 * this.scale;
    if (this.epiphany && this.phase === "summit") this.focusTX = fxs + 8 * this.scale;
    // a scene: frame the two of them; the ending: his face
    const other = this.speaker && this.speaker !== "sis" ? this.npcPos[this.speaker] : undefined;
    const lastOther = other ?? this.npcPos["merope"];
    if (this.cutscene && lastOther) {
      this.focusTX = (fxs + lastOther.x) / 2;
      this.focusTY = Math.min(fys - 70 * this.scale, lastOther.y);
    }
    if (this.finaleOn) {
      this.focusTX = fxs + 10 * this.scale;
      this.focusTY = fys - 88 * this.scale;
    }
    const figure = () =>
      this.drawFigureLit(fxs, fys, this.scale, left ? slopeAt(L, fwx) * 0.5 : ang, bx, by - R, R);
    // once the stone is rolling back it passes in front of him, on the near
    // side of the path, instead of through his body
    if (left) figure();
    this.drawContactShadow(bx, by, R, ang);
    this.drawBoulder(bx, by - R, R, ang);
    this.drawWhispers(bx, by - R, R);
    if (!left) figure();
    // what she asked for, in his hand on the way down
    const item = this.saga.s.carrying;
    if (item && this.phase === "descent") {
      drawItem(ctx, fxs - 8 * this.scale, fys - 28 * this.scale, this.scale, item, this.t);
    }
    this.headY = fys - 84 * this.scale;
    this.drawBolt(bx, by - R);
    this.renderParticles();
    this.drawForeground();

    // the hour's grade over the landscape, then the landscape over the sky
    const st = this.st;
    if (land && this.landLayer) {
      if (st.gradeA > 0.005) {
        land.save();
        land.globalCompositeOperation = "source-atop";
        land.fillStyle = rgb(st.grade, st.gradeA);
        land.fillRect(0, 0, w, h);
        land.restore();
      }
      // spring warms the hill; winter frosts it
      if (this.bloom > 0.02) {
        land.save();
        land.globalCompositeOperation = "source-atop";
        land.fillStyle = `rgba(255, 196, 120, ${(0.07 * this.bloom).toFixed(3)})`;
        land.fillRect(0, 0, w, h);
        land.restore();
      }
      if (this.saga.winter && (this.weather === "rain" || this.weather === "storm")) {
        land.save();
        land.globalCompositeOperation = "source-atop";
        land.fillStyle = `rgba(222, 232, 248, ${(0.16 * this.wxFade).toFixed(3)})`;
        land.fillRect(0, 0, w, h);
        land.restore();
      }
      // wet ground under a grey sky: everything a little darker and colder
      const wet = this.weather === "storm" ? 0.26 : this.weather === "rain" ? 0.18 : 0;
      if (wet > 0) {
        land.save();
        land.globalCompositeOperation = "source-atop";
        land.fillStyle = `rgba(28, 34, 46, ${wet})`;
        land.fillRect(0, 0, w, h);
        land.restore();
      }
      this.ctx = mainCtx;
      ctx = mainCtx;
      if (zoomed) zoomOff(ctx);
      ctx.drawImage(this.landLayer, 0, 0, w, h);
    } else if (st.gradeA > 0.005) {
      ctx.fillStyle = rgb(st.grade, st.gradeA * 0.85);
      ctx.fillRect(0, 0, w, h);
    }
    if (zoomed) zoomOff(ctx);
    this.drawWeather();
    // Tantalus's thirst: the edges of the world dry out and shimmer
    if (this.saga.s.burden === "tan" && this.phase === "playing") {
      const a = 0.16 + Math.sin(this.t * 1.7) * 0.05;
      const hv = ctx.createRadialGradient(w / 2, h / 2, h * 0.35, w / 2, h / 2, h * 0.9);
      hv.addColorStop(0, "rgba(200, 120, 40, 0)");
      hv.addColorStop(1, `rgba(200, 120, 40, ${a.toFixed(3)})`);
      ctx.fillStyle = hv;
      ctx.fillRect(0, 0, w, h);
    }
    this.drawAphorism();
    // the ring is interface, but it has to sit over him where the camera put him
    const ringY = fys - 112 * this.scale;
    if (!this.cutscene && !this.finaleOn)
      this.drawBreathRing(zfx + (fxs - zfx) * z + this.shakeX, zfy + (ringY - zfy) * z);

    // vignette: the scene sinks into the level's own fog colour, not into black.
    // Black would drain the gold straight back out of the corners.
    const fog = ctx.createRadialGradient(w / 2, h * 0.55, h * 0.2, w / 2, h * 0.55, h * 0.95);
    fog.addColorStop(0, "rgba(0,0,0,0)");
    fog.addColorStop(1, L.fog);
    ctx.save();
    ctx.globalAlpha = 0.34;
    ctx.fillStyle = fog;
    ctx.fillRect(0, 0, w, h);
    ctx.restore();

    // lightning flash
    if (this.flash > 0) {
      ctx.fillStyle = `rgba(255,250,235,${this.flash * 0.55})`;
      ctx.fillRect(0, 0, w, h);
    }
    this.camX -= driftX;
  }

  /**
   * Where the camera wants to be this frame, eased toward smoothly: returns the
   * zoom, and moves the focus point it zooms about.
   */
  private cameraMove(): number {
    const dt = this.frameDt;
    const { w, h } = this;
    if (this.focusX < 0) {
      this.focusX = this.focusTX || w * 0.4;
      this.focusY = this.focusTY || h * 0.6;
    }
    // how badly he is losing: pushing and getting nowhere, slipping, or spent
    let strain = 0;
    if (this.phase === "playing" && this.kickT < 0) {
      const stuck =
        Math.max(0, Math.min(1, this.input)) * (1 - Math.min(1, Math.abs(this.vx) / 95));
      const spent = this.calm ? 0 : Math.max(0, Math.min(1, (0.4 - this.breath) / 0.4));
      strain = Math.max(stuck * 0.75, this.slipT > 0 ? 1 : 0, spent * 0.8);
    }
    // the push-in is slow: a camera creeping closer, never a snap
    this.strainS += (strain - this.strainS) * Math.min(1, dt * 0.9);
    const target = this.finaleOn
      ? 1 + 1.4 * Math.min(1, this.finaleT / 4)
      : this.cutscene
        ? 1.45
        : this.phase === "playing"
          ? 1 + 0.15 * this.strainS
          : this.phase === "summit"
            ? this.epiphany
              ? 1 +
                1.5 *
                  Math.min(1, Math.max(0, (this.phaseT - 0.6) / 3)) *
                  Math.max(0, Math.min(1, (EPIPHANY_STRIKE - 0.4 - this.phaseT) / 1.2))
              : 1 + 0.17 * Math.min(1, this.phaseT / 1.4)
            : this.phase === "rolling"
              ? 1 + 0.12 * Math.max(0, 1 - this.phaseT / 1.6)
              : 1;
    const k = Math.min(1, dt * (this.phase === "summit" ? 1.6 : 1.1));
    this.zoom += (target - this.zoom) * k;
    this.zoomKick *= Math.exp(-dt * 4.5);
    this.focusX += (this.focusTX - this.focusX) * Math.min(1, dt * 3);
    this.focusY += (this.focusTY - this.focusY) * Math.min(1, dt * 3);
    this.focusX = Math.max(0, Math.min(w, this.focusX));
    this.focusY = Math.max(0, Math.min(h, this.focusY));
    // the stone's impacts shake the frame sideways as well as up and down
    this.shakeX = this.cinematic ? this.shake * (Math.random() - 0.5) * 9 : 0;
    if (!this.cinematic) return 1;
    return Math.max(1, this.zoom + this.zoomKick);
  }

  /** the sun in screen space */
  private sunScreen(): { x: number; y: number } {
    return { x: this.w * this.st.sunX, y: this.h * this.st.sunY };
  }

  /**
   * The light on the scene, in screen space: the sun, or by night the moon.
   * Shadows, rim lights and the lit band on the hill all take their direction,
   * colour and strength from this.
   */
  private lightScreen() {
    const l = this.st.light;
    return { x: this.w * l.x, y: this.h * l.y, color: l.color, strength: l.strength, elev: l.elev };
  }

  /**
   * The sky's gradient, glow, afterglow and disc depend only on the hour and
   * the viewport. Painted into an offscreen canvas and only repainted when the
   * hour has moved on enough to show, or the window changes.
   */
  private skyLayer(): HTMLCanvasElement | null {
    const { w, h } = this;
    const key = `${w}x${h}@${this.dpr}:${Math.round(this.st.hour * 60)}:${this.scale.toFixed(2)}`;
    if (this.skyCacheKey === key && this.skyCache) return this.skyCache;
    const cv = this.skyCache ?? document.createElement("canvas");
    // the sky is all gradient: half resolution loses nothing and halves the cost
    const res = Math.min(1, this.dpr) * 0.75;
    cv.width = Math.max(1, Math.ceil(w * res));
    cv.height = Math.max(1, Math.ceil(h * res));
    const c = cv.getContext("2d");
    if (!c) return null;
    c.setTransform(res, 0, 0, res, 0, 0);
    paintSky(c, w, h, this.st, this.scale);
    this.skyCache = cv;
    this.skyCacheKey = key;
    return cv;
  }

  /**
   * The sky is now the gradient and the disc, and nothing else. The god-rays and
   * the heat shimmer were the last two things the sun was throwing across it —
   * and the shimmer was four horizontal strokes running the full width, which is
   * a ruled line however faintly you draw it.
   */
  /**
   * Stars, the Milky Way, the moon, and now and then a meteor — all only as
   * dark as the sky really is. The brightest stars twinkle; the faint ones
   * just sit there, as they do.
   */
  private drawCelestial() {
    const ctx = this.ctx;
    const { w, h } = this;
    const st = this.st;
    if (st.stars > 0.01) {
      const key = `${Math.round(w)}x${Math.round(h)}@${this.dpr}`;
      if (!this.stars || this.starsKey !== key) {
        this.stars = starField(w, h, Math.min(2, this.dpr));
        this.starsKey = key;
      }
      ctx.save();
      ctx.globalAlpha = st.stars;
      ctx.drawImage(this.stars.cv, 0, 0, w, h);
      ctx.globalCompositeOperation = "lighter";
      for (let i = 0; i < this.stars.bright.length; i += 1) {
        const [x, y, mag] = this.stars.bright[i]!;
        const tw = 0.5 + 0.5 * Math.sin(this.t * (2 + (i % 7)) + i * 1.7);
        ctx.globalAlpha = st.stars * mag * tw * 0.6;
        ctx.fillStyle = "rgb(235, 240, 255)";
        ctx.beginPath();
        ctx.arc(x, y, 0.6 + mag * 1.1, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();

      // a meteor every so often on a dark night
      this.meteorT -= this.frameDt;
      if (!this.meteor && this.meteorT <= 0 && st.stars > 0.7) {
        this.meteorT = 12 + Math.random() * 20;
        this.meteor = {
          x: w * (0.2 + Math.random() * 0.6),
          y: h * (0.05 + Math.random() * 0.2),
          vx: (Math.random() < 0.5 ? -1 : 1) * w * (0.5 + Math.random() * 0.3),
          vy: h * (0.25 + Math.random() * 0.2),
          t: 0,
        };
      }
      const m = this.meteor;
      if (m) {
        m.t += this.frameDt;
        const life = 0.7;
        const k = m.t / life;
        if (k >= 1) this.meteor = null;
        else {
          const x = m.x + m.vx * m.t;
          const y = m.y + m.vy * m.t;
          const g = ctx.createLinearGradient(x, y, x - m.vx * 0.12, y - m.vy * 0.12);
          const a = Math.sin(k * Math.PI) * st.stars;
          g.addColorStop(0, `rgba(255, 255, 255, ${a.toFixed(3)})`);
          g.addColorStop(1, "rgba(200, 220, 255, 0)");
          ctx.strokeStyle = g;
          ctx.lineWidth = 1.4;
          ctx.lineCap = "round";
          ctx.beginPath();
          ctx.moveTo(x, y);
          ctx.lineTo(x - m.vx * 0.12, y - m.vy * 0.12);
          ctx.stroke();
        }
      }
    }
  }

  /** the moon: drawn before the cloud bank, which can pass in front of it */
  private drawMoonNow() {
    const st = this.st;
    if (st.moon.vis > 0.01) {
      drawMoon(
        this.ctx,
        st.moon.x * this.w,
        st.moon.y * this.h,
        Math.max(9, 13 * this.scale),
        this.moonPhase(),
        st.moon.vis,
      );
    }
  }

  private drawSky() {
    const ctx = this.ctx;
    const { w, h } = this;
    const cached = this.skyLayer();
    if (cached) ctx.drawImage(cached, 0, 0, w, h);
    else {
      // nothing else clears the frame, so the fallback has to cover it
      ctx.fillStyle = this.level.sky[0]?.[1] ?? "#0a0c10";
      ctx.fillRect(0, 0, w, h);
    }
  }

  /** the current cycle's aphorism, written into the sky to the left of the sun */
  /** off while the new page is being shown as its own moment over the scene */
  showAphorism = true;
  /** accessibility: a larger, heavier breath ring that does not rely on colour */
  boldRing = false;
  /** the player's own saying, written into the sky every seventh summit */
  ownQuote: Quote | null = null;
  /** chapters of the book completed, whose keepsakes stand on the hills */
  chaptersDone: number[] = [];

  private drawAphorism() {
    if (!this.showAphorism || this.finaleOn || this.cutscene) return;
    // no aphorism in the background until the first cycle is completed
    if (this.phase === "playing" && this.cycles === 0) return;
    // the continue card sits right where the block is written; it comes back
    // the moment the next climb starts
    if (this.phase === "summit") return;
    const q = this.currentQuote;
    if (!q) return;
    const ctx = this.ctx;
    const { w, h } = this;
    // a fixed place in the upper-left sky, whatever the hour moves the sun to
    const cx = w * 0.22;
    const sunY = h * 0.58;
    // The mountain is drawn over this, so keep the text in open sky however low
    // the level hangs its sun. Sitting a little lower than before, too: dark
    // lettering needs the brighter part of the ramp under it, and the sky gains
    // about four hundredths of lightness over that distance.
    const cy = Math.min(sunY, h * 0.4);
    const sprite = this.quoteSprite(q.text, q.author);
    if (!sprite) return;
    // It used to be pinned to the sun's height, which on most screens put the
    // block squarely over the man at the summit — the one moment everyone is
    // looking at him. It still prefers that spot, but rises to stay clear of his
    // head, and never higher than the HUD along the top edge.
    let top = cy - this.quoteAnchorY;
    const clearance = this.headY - 18;
    if (top + this.quoteH > clearance) top = clearance - this.quoteH;
    top = Math.max(44, top);
    ctx.drawImage(
      sprite,
      Math.round(cx - this.quoteW / 2),
      Math.round(top),
      this.quoteW,
      this.quoteH,
    );
  }

  /**
   * The aphorism, rendered once and then blitted.
   *
   * It used to be laid out from scratch on every frame — a `measureText` per word
   * to find the wrap, then a `fillText` per line under a shadow blur, which is one
   * of the slowest things a 2D context does. None of it changes between summits:
   * the words only change when the quote or the language does, and the layout only
   * when the window resizes.
   */
  private quoteSprite(text: string, author: string): HTMLCanvasElement | null {
    const { w, h } = this;
    const maxW = w * 0.4;
    const fs = Math.max(22, Math.min(44, w * 0.045, h * 0.065));
    const lineH = Math.round(fs * 1.35);
    const dpr = this.dpr;
    // dark ink on a bright sky; by night, pale lettering like starlight
    const pale = this.st.elev < -2;
    const key = `${text}\0${author}\0${Math.round(w)}x${Math.round(h)}@${dpr.toFixed(2)}:${pale}`;
    if (this.quoteCacheKey === key && this.quoteCache) return this.quoteCache;

    const bodyFont = `italic ${fs}px Georgia, 'Times New Roman', serif`;
    const authorFont = `italic ${Math.round(fs * 0.72)}px Georgia, 'Times New Roman', serif`;

    // measured on the live context, which already has the fonts loaded
    const m = this.ctx;
    m.save();
    m.font = bodyFont;

    // word wrap; scripts without spaces (CJK) wrap per character
    const spaced = text.includes(" ");
    const words = spaced ? text.split(" ") : Array.from(text);
    const lines: string[] = [];
    let cur = "";
    for (const word of words) {
      const test = cur ? `${cur}${spaced ? " " : ""}${word}` : word;
      if (cur && m.measureText(test).width > maxW) {
        lines.push(cur);
        cur = word;
      } else {
        cur = test;
      }
    }
    if (cur) lines.push(cur);

    let widest = 0;
    for (const line of lines) widest = Math.max(widest, m.measureText(line).width);
    m.font = authorFont;
    widest = Math.max(widest, m.measureText(author).width);
    m.restore();

    // room for the halo, and for whatever a serif italic hangs past its metrics
    const pad = Math.ceil(fs * 0.6);
    const anchorY = pad + lineH / 2;
    const authorY = anchorY + lines.length * lineH + lineH * 0.4;
    // Painted at the screen's real pixel density. It used to be painted at one
    // canvas pixel per CSS pixel and then stretched onto a 2x or 3x phone
    // screen, which is exactly why the lettering looked out of focus.
    const cssW = Math.max(1, Math.ceil(widest) + pad * 2);
    const cssH = Math.max(1, Math.ceil(authorY + fs + pad));
    const cv = this.quoteCache ?? document.createElement("canvas");
    cv.width = Math.ceil(cssW * dpr);
    cv.height = Math.ceil(cssH * dpr);
    this.quoteW = cssW;
    this.quoteH = cssH;
    const c = cv.getContext("2d");
    if (!c) return null;
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.clearRect(0, 0, cv.width, cv.height);
    c.setTransform(dpr, 0, 0, dpr, 0, 0);
    c.textAlign = "center";
    c.textBaseline = "middle";
    const mid = cssW / 2;

    // Pale text on a pale sky is why this could not be read: nothing in this
    // frame is darker than L 0.6, so the only value left to write with is a dark
    // Ink, not light: held translucent so it still reads as written into the sky
    // rather than printed onto it, and haloed rather than drop-shadowed, because
    // once the letters are the dark thing the separation has to come from
    // something brighter than they are. Dropping the block to 0.4h buys about
    // four hundredths of sky lightness to sit against, which is most of what
    // makes dark lettering work on a ramp this low.
    //
    // The halo is its own pass now, and the letters go down on top of it with
    // no blur at all. Drawn in one pass with a shadow, the blur was applied to
    // the glyphs themselves and softened every edge.
    const paintLines = () => {
      c.font = bodyFont;
      lines.forEach((line, i) => c.fillText(line, mid, anchorY + i * lineH));
      c.font = authorFont;
      c.fillText(author, mid, authorY);
    };
    c.save();
    c.shadowColor = pale ? "rgba(0, 0, 10, 0.6)" : "oklch(0.92 0.06 78 / 0.55)";
    c.shadowBlur = 7 * dpr;
    c.fillStyle = pale ? "rgba(0, 0, 10, 0.3)" : "oklch(0.92 0.06 78 / 0.35)";
    paintLines();
    c.restore();
    c.font = bodyFont;
    c.fillStyle = pale ? "rgba(235, 238, 250, 0.92)" : "oklch(0.14 0.03 40 / 0.94)";
    lines.forEach((line, i) => c.fillText(line, mid, anchorY + i * lineH));
    c.font = authorFont;
    c.fillStyle = pale ? "rgba(210, 215, 235, 0.8)" : "oklch(0.2 0.04 42 / 0.85)";
    c.fillText(author, mid, authorY);

    this.quoteCache = cv;
    this.quoteCacheKey = key;
    // the caller places the block by its centre, as it did when it drew the
    // lines itself — so the anchor is the middle of the stack, not of line one
    this.quoteAnchorY = anchorY + ((lines.length - 1) * lineH) / 2;
    return cv;
  }

  /**
   * Three parallax layers of cumulus drifting above the sun, lit from beneath —
   * held in a half-resolution layer of their own and repainted only when they
   * have moved. See CLOUD_RES: this is where the frame time was going.
   */
  private drawClouds() {
    const { w, h } = this;
    // a little finer on capable devices, so the softening pass below can stay
    // small on screen; the slow path keeps the old half resolution
    this.cloudRes = this.lowQuality ? CLOUD_RES : 0.75;
    const key = `${Math.ceil(w)}x${Math.ceil(h)}@${this.cloudRes}:${this.level.id}`;
    let cv = this.cloudCache;
    let stale = false;

    if (!cv || this.cloudCacheKey !== key) {
      cv = cv ?? document.createElement("canvas");
      cv.width = Math.max(1, Math.ceil(w * this.cloudRes));
      cv.height = Math.max(1, Math.ceil(h * this.cloudRes));
      this.cloudCache = cv;
      this.cloudCacheKey = key;
      stale = true;
    } else {
      // the front rank has the strongest parallax, so it is the one that decides
      // whether the bank has drifted far enough to be worth repainting
      const drift = Math.abs(this.camX - this.cloudCacheCamX) * 0.085 * this.scale;
      stale =
        this.t - this.cloudCacheT > CLOUD_MAX_AGE ||
        drift > CLOUD_MAX_DRIFT ||
        Math.abs(this.st.hour - this.cloudCacheHour) > 0.02;
    }

    if (stale) {
      const c = cv.getContext("2d");
      if (c) {
        c.setTransform(1, 0, 0, 1, 0, 0);
        c.clearRect(0, 0, cv.width, cv.height);
        // painted in CSS pixels and scaled down on the way in, so every
        // coordinate below stays the one the scene reasons in
        c.setTransform(this.cloudRes, 0, 0, this.cloudRes, 0, 0);
        this.paintClouds(c);
        /*
         * Chrome dithers every gradient with a 4x4 ordered pattern. Dozens of
         * faint cloud gradients stack that pattern in step, and the layer is
         * then blown up 2x: on screen it was a grid of little squares over the
         * whole sky. One small blur on the finished layer melts it away.
         */
        const bl = this.cloudBlur ?? (this.cloudBlur = document.createElement("canvas"));
        if (bl.width !== cv.width || bl.height !== cv.height) {
          bl.width = cv.width;
          bl.height = cv.height;
        }
        const bc = bl.getContext("2d");
        if (bc) {
          bc.setTransform(1, 0, 0, 1, 0, 0);
          bc.clearRect(0, 0, bl.width, bl.height);
          bc.filter = this.lowQuality ? "blur(1.3px)" : "blur(0.7px)";
          bc.drawImage(cv, 0, 0);
          bc.filter = "none";
        }
        this.cloudCacheT = this.t;
        this.cloudCacheHour = this.st.hour;
        this.cloudCacheCamX = this.camX;
      }
    }

    this.ctx.drawImage(this.cloudBlur ?? cv, 0, 0, w, h);
    this.drawHorizonStreaks();
  }

  private paintClouds(ctx: CanvasRenderingContext2D) {
    const { w, h } = this;
    const t = this.t;
    // back layer slowest & most translucent, front layer fastest & most solid
    const layers: Array<{
      parallax: number;
      speed: number;
      alpha: number;
      minW: number;
      maxW: number;
      count: number;
      yTop: number;
      ySpan: number;
      /** 0 = far haze, 1 = the rank nearest the camera */
      depth: number;
    }> = [
      {
        parallax: 0.018,
        speed: 6,
        alpha: 0.42,
        minW: 0.26,
        maxW: 0.44,
        count: 9,
        yTop: 0.04,
        ySpan: 0.22,
        depth: 0,
      },
      {
        parallax: 0.045,
        speed: 12,
        alpha: 0.64,
        minW: 0.38,
        maxW: 0.6,
        count: 7,
        yTop: 0.09,
        ySpan: 0.26,
        depth: 0.5,
      },
      {
        parallax: 0.085,
        speed: 20,
        alpha: 0.9,
        minW: 0.55,
        maxW: 0.85,
        count: 5,
        yTop: 0.15,
        ySpan: 0.3,
        depth: 1,
      },
    ];
    let seed = 0;
    // building a volume is the costly part: a few per paint, nearest first
    this.cloudBuildBudget = 1;
    this.cloudBudget = 3;
    for (const L of layers) {
      for (let i = 0; i < L.count; i++) {
        const k = seed++;
        const cw = w * (L.minW + this.hash(k * 3.1) * (L.maxW - L.minW));
        // the drawn width: sheets far off spread wide, a near cumulus is a heap
        const dw = cw * (L.depth < 0.25 ? 0.95 : L.depth < 0.75 ? 0.62 : 0.55);
        const wrap = w + dw + 120;
        const off = this.hash(k * 5.3) * wrap;
        const x =
          ((((off - this.camX * L.parallax + t * L.speed) % wrap) + wrap) % wrap) - dw / 2 - 60;
        const y = h * (L.yTop + this.hash(k * 7.7) * L.ySpan) + Math.sin(t * 0.09 + k * 2.1) * 4;
        this.drawVolumeCloud(ctx, x, y, dw, L.alpha, k, L.depth);
      }
    }
  }

  private buildCloudShape(seed: number, depth: number): CloudShape {
    const px = (this.lowQuality ? 0.6 : 1) * (depth < 0.25 ? 300 : depth < 0.75 ? 340 : 400);
    const shape = buildCloud(seed + 1, depth < 0.25 ? "strato" : "cumulus", px);
    this.cloudShapes.set(seed, shape);
    return shape;
  }

  /**
   * Build the cloud volumes in the background, one per tick, while the start
   * card is still up — so the first climb never waits on them. The ranks are
   * 9 far, 7 middle, 5 near (see paintClouds); the near ones go first.
   */
  private prebuildClouds() {
    const order = Array.from({ length: 21 }, (_, i) => 20 - i);
    const next = () => {
      const seed = order.shift();
      if (seed === undefined) return;
      if (!this.cloudShapes.has(seed))
        this.buildCloudShape(seed, seed < 9 ? 0 : seed < 16 ? 0.5 : 1);
      later(next);
    };
    // in the browser's idle time where it has one, so a frame is never held up
    const later = (f: () => void) => {
      const w = window as Window & {
        requestIdleCallback?: (cb: () => void, o?: { timeout: number }) => number;
      };
      if (w.requestIdleCallback) w.requestIdleCallback(f, { timeout: 400 });
      else window.setTimeout(f, 16);
    };
    window.setTimeout(next, 200);
  }

  /** one cloud from its built volume, coloured for the hour (see clouds.ts) */
  private drawVolumeCloud(
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    dw: number,
    alpha: number,
    seed: number,
    depth: number,
  ) {
    const day = Math.max(0, Math.min(1, (this.st.elev - 10) / 10));
    // fair-weather days: the heaps are scattered, not a ceiling
    const keep = this.hash(seed * 1.7 + 0.3) < 0.55;
    const show = keep ? 1 : 1 - day;
    // Cloud is opaque: a thinner sky means fewer clouds, not see-through ones
    // (stars showing through a bank gave it away). Cover decides how many of
    // the bank are out; the far ranks keep a little haze.
    const out = Math.max(
      0,
      Math.min(1, (this.st.cover * 1.3 - this.hash(seed * 2.9 + 0.7)) / 0.15),
    );
    const solid = 0.9 + depth * 0.1 + (alpha - 0.64) * 0.1;
    const a = out * solid * show * (1 - day) + (keep ? 0.97 * day : 0);
    if (a < 0.01) return;
    let shape = this.cloudShapes.get(seed);
    if (!shape) {
      if (this.cloudBuildBudget <= 0) return;
      this.cloudBuildBudget--;
      shape = this.buildCloudShape(seed, depth);
    }
    const lit = Math.max(0.12, 1 - Math.abs(x - this.sunScreen().x) / (this.w * 0.8));
    const litQ = Math.round(lit * 4) / 4;
    const key = `${Math.round(this.st.elev * 2)}|${litQ}`;
    if (shape.key !== key && (this.cloudBudget > 0 || !shape.key)) {
      this.cloudBudget--;
      const pal = this.st.cloud;
      // the far ranks fade toward the sky colour: haze eats their contrast
      tintCloud(shape, {
        day,
        lit: litQ,
        crown: pal.crown,
        crownA: pal.crownA,
        mid: pal.mid,
        belly: pal.belly,
        bellyA: pal.bellyA * (0.6 + depth * 0.4),
      });
      shape.key = key;
    }
    const dh = (dw * shape.h) / shape.w;
    // the base sits a little below the cloud's height line
    ctx.save();
    ctx.globalAlpha = Math.min(1, a);
    ctx.drawImage(shape.canvas, x - dw / 2, y - dh * 0.6, dw, dh);
    ctx.restore();
  }

  /**
   * Thin cloud stripes stacked just over the sun. They belong to the cloud pass,
   * not to the sky pass — drawn there the cumulus above simply painted over them.
   */
  private drawHorizonStreaks() {
    const ctx = this.ctx;
    const { x: sunX, y: sunY } = this.sunScreen();
    const s = this.scale;
    const t = this.t;
    // Eight of these used to sit stacked directly over the disc at even spacing
    // and a uniform thickness, which stopped reading as cloud and started reading
    // as a smear. Five now, spread further apart, each a different thickness and
    // length, and none of them parked on the sun itself — that is what makes a
    // thin cirrus band look like weather rather than like blur.
    const low = this.st.elev > -7 && this.st.elev < 14 ? 1 - Math.abs(this.st.elev - 3) / 11 : 0;
    if (low <= 0.02) return;
    ctx.save();
    for (let i = 0; i < 5; i++) {
      const sy = sunY - 132 * s + i * 27 * s + Math.sin(t * 0.05 + i * 1.3) * 4;
      const len = (150 + this.hash(i * 9.1) * 210) * s;
      const thick = (1.4 + this.hash(i * 4.3) * 2.1) * s;
      // the bands nearest the disc catch the most light
      const heat = Math.max(0.2, 1 - Math.abs(sy - sunY) / (190 * s));
      ctx.fillStyle = rgb(this.st.glow, 0.2 * heat * low);
      ctx.beginPath();
      ctx.ellipse(
        // a slow lateral breath; a one-way drift would walk them off the sun forever
        sunX + len * 0.25 + Math.sin(t * 0.03 + i * 0.8) * 26 * s,
        sy,
        len * 0.5,
        thick,
        (this.hash(i + 4) - 0.5) * 0.24,
        0,
        Math.PI * 2,
      );
      ctx.fill();
    }
    ctx.restore();
  }

  private drawMountain() {
    const mt = this.level.mountain;
    if (!mt) return;
    const ctx = this.ctx;
    const { w, h } = this;
    const p = mt.parallax;
    const farX = (wx: number) => (wx - this.camX * p) * this.scale;
    const farY = (wy: number) =>
      this.groundY - (wy - this.camY * p) * this.scale * 0.5 + this.shakeY * 0.4;

    const lx = mt.x - mt.width / 2;
    const rx = mt.x + mt.width / 2;
    const baseY = mt.height * 0.12;
    const p1x = mt.x - mt.width * 0.18;
    const p1y = baseY + mt.height;

    // One continuous, gently undulating ridge (subtle bumps, no harsh spikes).
    // It is defined entirely in world space off a deterministic hash, so it is
    // the same ninety points on every frame — built once per level and then only
    // projected, rather than rebuilt sixty times a second to arrive at the same
    // answer.
    if (!this.mountainPts || this.mountainKey !== this.level.id) {
      const p2x = mt.x + mt.width * 0.22;
      const p2y = baseY + mt.height * 0.66;
      const pts: Array<[number, number]> = [];
      const seg = (
        x0: number,
        y0: number,
        x1: number,
        y1: number,
        n: number,
        amp: number,
        includeStart: boolean,
      ) => {
        const from = includeStart ? 0 : 1;
        for (let i = from; i <= n; i++) {
          const u = i / n;
          const wx = x0 + (x1 - x0) * u;
          const wy = y0 + (y1 - y0) * u;
          const nz = (this.hash2(wx * 0.4, 13) - 0.5) * amp * 2;
          pts.push([wx, wy + nz]);
        }
      };
      seg(lx, baseY, p1x, p1y, 26, mt.height * 0.012, true);
      seg(p1x, p1y, p2x, p2y, 36, mt.height * 0.014, false);
      seg(p2x, p2y, rx, baseY, 26, mt.height * 0.011, false);
      this.mountainPts = pts;
      this.mountainKey = this.level.id;
    }
    const pts = this.mountainPts;

    // the crest (peak ridge) spans pts[27..62]
    const c0 = 27;
    const c1 = 62;
    const tracePts = (from: number, to: number) => {
      for (let i = from; i <= to; i++) ctx.lineTo(farX(pts[i]![0]), farY(pts[i]![1]));
    };

    // body fill, darker toward the base
    ctx.beginPath();
    ctx.moveTo(farX(lx), h + 2);
    ctx.lineTo(farX(lx), farY(baseY));
    tracePts(0, pts.length - 1);
    ctx.lineTo(farX(rx), h + 2);
    ctx.closePath();
    const bodyG = ctx.createLinearGradient(0, farY(p1y), 0, farY(baseY));
    // a true silhouette: no internal shading, or the contrast against the sky goes soft
    bodyG.addColorStop(0, mt.color);
    bodyG.addColorStop(1, mt.color);
    ctx.fillStyle = bodyG;
    ctx.fill();

    // a hairline of light where the ridge cuts the sky — the only thing that
    // separates a black mass from a black mass, and all a silhouette should get
    ctx.strokeStyle = "oklch(0.97 0.1 74 / 0.5)";
    ctx.lineWidth = 1.4;
    ctx.lineJoin = "round";
    ctx.beginPath();
    ctx.moveTo(farX(pts[c0]![0]), farY(pts[c0]![1]));
    tracePts(c0, c1);
    ctx.stroke();

    // atmospheric haze melting the base into the valley
    const baseYp = farY(baseY);
    const haze = ctx.createLinearGradient(0, h, 0, baseYp);
    haze.addColorStop(0, "oklch(0.82 0.09 60 / 0.42)");
    haze.addColorStop(0.6, "oklch(0.82 0.09 60 / 0.08)");
    haze.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = haze;
    ctx.fillRect(0, baseYp - 6, w, h - baseYp + 6);
  }

  /**
   * The plane in front of the action, and the thing that was missing for depth.
   *
   * The scene already had four ranks behind the player — sky, mountain, three
   * cosmetic ridges — and nothing at all in front of him. Everything nearer than
   * the man was the man, so the picture was a cutout on a backdrop however many
   * layers the backdrop had. Depth is read from *relative* motion, and the
   * strongest signal available is something that crosses the frame faster than
   * he does.
   *
   * So: boulders and scrub at one and a half times the camera speed, cropped by
   * the bottom edge, and black. They are black because they are the nearest
   * things to a viewer looking into a low sun — a foreground object is the one
   * thing that genuinely has no light on its face — and because a silhouette
   * with no internal detail is what the eye reads as "close and out of focus"
   * without any blurring being done.
   *
   * They are kept to the bottom eighth of the frame. A foreground that crosses
   * the man is atmospheric for exactly as long as it is not covering the thing
   * you are playing.
   */
  /**
   * The nearest ground, right at the bottom of the frame, scrolling faster
   * than the hill: weathered rocks and big clumps of dry grass, nearly black
   * against the slope, edged with a little light. These used to be smooth
   * black humps that read as nothing in particular.
   */
  private drawForeground() {
    const ctx = this.ctx;
    const { w, h } = this;
    const s = this.scale;
    const scroll = this.camX * 1.5 * s;
    const period = 360 * s;
    const first = Math.floor(scroll / period) - 1;
    for (let i = first; i * period - scroll < w + period; i++) {
      const h1 = this.hash(i * 3.77 + 0.5);
      const h2 = this.hash(i * 7.13 + 2.5);
      const x = i * period - scroll + h1 * period * 0.6;
      if (h1 > 0.3) {
        // a weathered rock: an irregular polygon with a lit top edge
        const wide = (40 + h2 * 70) * s;
        const rise = (22 + h1 * 40) * s;
        const n = 9;
        const pts: Array<[number, number]> = [];
        for (let k = 0; k <= n; k++) {
          const t = k / n;
          const a = Math.PI * (1 - t);
          const jag = 0.78 + this.hash(i * 11.3 + k * 1.7) * 0.32;
          pts.push([x + Math.cos(a) * wide * jag, h + 6 - Math.sin(a) * rise * jag]);
        }
        ctx.fillStyle = "oklch(0.09 0.012 40)";
        ctx.beginPath();
        ctx.moveTo(x - wide * 1.05, h + 8);
        for (const [px, py] of pts) ctx.lineTo(px, py);
        ctx.lineTo(x + wide * 1.05, h + 8);
        ctx.closePath();
        ctx.fill();
        ctx.strokeStyle = "rgba(255, 196, 130, 0.22)";
        ctx.lineWidth = 1.4 * s;
        ctx.lineJoin = "round";
        ctx.beginPath();
        for (let k = 2; k <= n - 1; k++) {
          const [px, py] = pts[k]!;
          if (k === 2) ctx.moveTo(px, py);
          else ctx.lineTo(px, py);
        }
        ctx.stroke();
      }
      // big grass clumps, close enough to see every blade move
      const clumps = 1 + Math.floor(h2 * 3);
      for (let k = 0; k < clumps; k++) {
        const gx = x + (this.hash(i * 5.1 + k) - 0.5) * period * 0.7;
        grassClump(
          ctx,
          gx,
          h + 4,
          (40 + this.hash(i * 2.3 + k) * 40) * s,
          i * 31 + k,
          this.windAt(gx / s + this.camX) * 1.3,
          "oklch(0.08 0.012 40 / 0.95)",
          "rgba(255, 200, 130, 0.16)",
        );
      }
    }
  }

  /**
   * A long shadow on the face of the hill. The sun is low and behind the
   * slope, so anything standing on the skyline throws its shadow down the
   * face toward the viewer and away from the sun's side — soft, widening and
   * fading with distance the way a penumbra does.
   */
  private drawLongShadow(x: number, y: number, half: number, len: number) {
    const ctx = this.ctx;
    const sun = this.lightScreen();
    if (sun.strength < 0.03) return;
    // a low light throws long shadows, a high one short ones
    len *= Math.max(0.3, Math.min(1.35, 1.35 - sun.elev / 45));
    // Away from the sun, and more sideways the further from the sun's line —
    // continuously. This used to pick a side (left of the sun or right of it)
    // with a fixed minimum lean, so the shadow snapped from one side to the
    // other the moment the stone crossed the sun's line, e.g. on a launch.
    // Right under the sun it now points straight down the slope, and swings
    // smoothly through that point.
    let dx = Math.max(-0.95, Math.min(0.95, (x - sun.x) / (this.w * 0.45)));
    let dy = 0.55;
    const dl = Math.hypot(dx, dy);
    dx /= dl;
    dy /= dl;
    const tx = x + dx * len;
    const ty = y + dy * len;
    const px = -dy;
    const py = dx;
    const g = ctx.createLinearGradient(x, y, tx, ty);
    const k = Math.min(1, sun.strength * 1.1);
    g.addColorStop(0, `rgba(14, 8, 12, ${(0.42 * k).toFixed(3)})`);
    g.addColorStop(0.6, `rgba(14, 8, 12, ${(0.16 * k).toFixed(3)})`);
    g.addColorStop(1, "rgba(14, 8, 12, 0)");
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.moveTo(x + px * half, y + py * half * 0.4);
    ctx.quadraticCurveTo(tx + px * half * 1.8, ty + py * half, tx, ty);
    ctx.quadraticCurveTo(tx - px * half * 1.8, ty - py * half, x - px * half, y - py * half * 0.4);
    ctx.closePath();
    ctx.fill();
  }

  private drawContactShadow(bx: number, by: number, R: number, ang: number) {
    const ctx = this.ctx;
    ctx.save();
    ctx.globalAlpha = 0.45;
    ctx.fillStyle = "oklch(0.08 0 0)";
    ctx.beginPath();
    ctx.ellipse(bx, by + 2, R * 0.9, R * 0.22, ang, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  /** granite texture for the stone, painted once at screen density */
  private stoneTex: HTMLCanvasElement | null = null;
  private stoneTexPx = 0;

  /**
   * The stone: a rough granite boulder, backlit.
   *
   * It used to be lit from the front — a bright, glossy ball with flat
   * triangular "facets" — in a scene where everything else, the man included,
   * stands with the sun behind it. A rock between you and a low sun is mostly
   * in its own shadow, with a hard, bright edge where the light grazes the far
   * side. That is what this paints: the real grain of the stone (turning with
   * it), darkened on the face toward you, a rim of light that follows its
   * actual outline on the sun's side, a little warm bounce from the ground
   * underneath, and the dark where it sits in the dirt.
   */
  private drawBoulder(bx: number, by: number, R: number, ang: number) {
    const ctx = this.ctx;
    const s = this.scale;

    // an irregular outline: a few broad lumps, then smaller chips
    const N = 40;
    const pts: Array<[number, number]> = [];
    for (let i = 0; i < N; i++) {
      const a = (i / N) * Math.PI * 2;
      const n =
        Math.sin(a * 2 + 0.6) * 0.045 +
        Math.sin(a * 3 + 1.3) * 0.05 +
        Math.sin(a * 7 + 4.2) * 0.028 +
        Math.sin(a * 13 + 0.7) * 0.016 +
        (this.hash(i * 3.7) - 0.5) * 0.03;
      const r = R * (1 + n);
      pts.push([Math.cos(a) * r, Math.sin(a) * r]);
    }
    const outline = () => {
      ctx.moveTo(pts[0]![0], pts[0]![1]);
      for (let i = 1; i < N; i++) ctx.lineTo(pts[i]![0], pts[i]![1]);
      ctx.closePath();
    };

    const rot = ang + this.roll;
    const cr = Math.cos(rot);
    const sr = Math.sin(rot);
    const toLocal = (vx: number, vy: number): [number, number] => [
      vx * cr + vy * sr,
      -vx * sr + vy * cr,
    ];
    const sun = this.lightScreen();
    const lc = sun.color;
    const ls = Math.min(1, sun.strength * 1.15);
    const sdx = sun.x - bx;
    const sdy = sun.y - by;
    const slen = Math.hypot(sdx, sdy) || 1;
    const [lx, ly] = toLocal(sdx / slen, sdy / slen);
    const [dnx, dny] = toLocal(0, 1);

    // texture at the screen's pixel density, rebuilt only if the size changes
    const want = Math.ceil(R * 2.2 * this.dpr);
    if (!this.stoneTex || Math.abs(this.stoneTexPx - want) > want * 0.25) {
      this.stoneTex = graniteTexture(Math.max(64, Math.min(512, want)));
      this.stoneTexPx = want;
    }

    ctx.save();
    ctx.translate(bx, by);
    ctx.rotate(rot);
    ctx.beginPath();
    outline();
    ctx.save();
    ctx.clip();
    ctx.drawImage(this.stoneTex, -R * 1.1, -R * 1.1, R * 2.2, R * 2.2);

    // the face toward us is in the stone's own shadow; the sun side is not
    const shade = ctx.createLinearGradient(lx * R, ly * R, -lx * R, -ly * R);
    shade.addColorStop(0, "rgba(40, 26, 18, 0)");
    shade.addColorStop(0.35, "rgba(30, 20, 18, 0.28)");
    shade.addColorStop(1, "rgba(16, 12, 16, 0.62)");
    ctx.fillStyle = shade;
    ctx.fillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

    // sky fill from above, cool and faint, so the top reads as a curved surface
    const [ux, uy] = toLocal(0, -1);
    const sky = ctx.createRadialGradient(
      ux * R * 0.7,
      uy * R * 0.7,
      0,
      ux * R * 0.7,
      uy * R * 0.7,
      R * 0.9,
    );
    sky.addColorStop(0, "rgba(170, 160, 200, 0.14)");
    sky.addColorStop(1, "rgba(170, 160, 200, 0)");
    ctx.fillStyle = sky;
    ctx.fillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

    // warm bounce off the sunlit dirt under it
    const bounce = ctx.createRadialGradient(
      dnx * R * 0.9,
      dny * R * 0.9,
      0,
      dnx * R * 0.9,
      dny * R * 0.9,
      R * 0.8,
    );
    bounce.addColorStop(0, "rgba(190, 120, 60, 0.16)");
    bounce.addColorStop(1, "rgba(190, 120, 60, 0)");
    ctx.fillStyle = bounce;
    ctx.fillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

    // where it sits in the dirt: dark
    const ao = ctx.createRadialGradient(
      dnx * R * 0.75,
      dny * R * 0.75,
      R * 0.05,
      dnx * R * 0.75,
      dny * R * 0.75,
      R * 0.6,
    );
    ao.addColorStop(0, "rgba(0,0,0,0.42)");
    ao.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = ao;
    ctx.fillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

    // the glow just inside the lit edge, where light scatters into the grain
    const glow = ctx.createRadialGradient(
      lx * R * 1.05,
      ly * R * 1.05,
      0,
      lx * R * 1.05,
      ly * R * 1.05,
      R * 0.7,
    );
    glow.addColorStop(0, rgb(lc, 0.45 * ls));
    glow.addColorStop(1, "rgba(255, 200, 130, 0)");
    ctx.fillStyle = glow;
    ctx.fillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);
    this.drawStoneMemory(ctx, R);
    ctx.restore();

    // the rim: each edge of the outline lit by how squarely it faces the sun
    ctx.lineCap = "round";
    for (let i = 0; i < N; i++) {
      const [x0, y0] = pts[i]!;
      const [x1, y1] = pts[(i + 1) % N]!;
      const mx = (x0 + x1) / 2;
      const my = (y0 + y1) / 2;
      const ml = Math.hypot(mx, my) || 1;
      const facing = (mx / ml) * lx + (my / ml) * ly;
      if (facing < 0.2) continue;
      const k = (facing - 0.2) / 0.8;
      ctx.strokeStyle = rgb(lc, (0.25 + k * 0.7) * ls);
      ctx.lineWidth = (0.8 + k * 1.8) * s;
      ctx.beginPath();
      ctx.moveTo(x0, y0);
      ctx.lineTo(x1, y1);
      ctx.stroke();
    }
    ctx.restore();
  }

  /**
   * One bird in silhouette, as you see a large bird against a bright sky from
   * below and a little to the side: a small body, and two wings bent at the
   * wrist like a gull's. `e` is wing elevation, -1 (bottom of the downstroke)
   * to 1 (top of the upstroke); the hand lags the arm through the stroke, so
   * the wing bends rather than swinging like a board.
   */
  private drawBird(
    x: number,
    y: number,
    s: number,
    e: number,
    dir: number,
    tilt: number,
    color: string,
  ) {
    const ctx = this.ctx;
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(tilt);
    ctx.scale(dir, 1);
    ctx.fillStyle = color;
    // body, foreshortened, with the head just showing ahead of it
    ctx.beginPath();
    ctx.ellipse(0.3 * s, 0.2 * s, 1.5 * s, 0.75 * s, 0.15, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.arc(1.7 * s, -0.15 * s, 0.55 * s, 0, Math.PI * 2);
    ctx.fill();
    const lag = Math.sin(Math.asin(Math.max(-1, Math.min(1, e))) - 0.5);
    for (const side of [-1, 1]) {
      // the far wing (behind the body) a touch shorter: it is further away
      const k = side < 0 ? 0.9 : 1;
      const wx = side * 3.6 * s * k;
      const wy = -e * 2.4 * s - 0.5 * s;
      const tx = side * 8.2 * s * k;
      const ty = -lag * 4.6 * s + 0.4 * s;
      ctx.beginPath();
      ctx.moveTo(side * 0.6 * s, -0.4 * s);
      // leading edge: arm up to the wrist, then the hand out to the tip
      ctx.quadraticCurveTo(side * 2 * s, wy - 0.4 * s, wx, wy);
      ctx.quadraticCurveTo(side * 6 * s * k, (wy + ty) / 2 - 0.6 * s, tx, ty);
      // trailing edge: back along the primaries and secondaries to the body
      ctx.quadraticCurveTo(side * 5.4 * s * k, (wy + ty) / 2 + 1 * s, wx * 0.85, wy + 1.2 * s);
      ctx.quadraticCurveTo(side * 1.8 * s, 0.9 * s, side * 0.4 * s, 0.7 * s);
      ctx.closePath();
      ctx.fill();
    }
    ctx.restore();
  }

  private renderBirds() {
    const w = this.w;
    const h = this.h;
    // birds roost at night: they thin out through dusk and are gone after dark
    const about = this.st.birds;
    if (about < 0.02) return;
    this.ctx.save();
    this.ctx.globalAlpha = about;
    // against a burning sky a bird is a hard silhouette; the far ones lift a
    // little into the haze
    for (const b of this.birds) {
      const px = w * 0.38 + b.ox * this.scale * b.par;
      if (px < -80 || px > w + 80) continue;
      const py = b.fy * h + Math.sin(b.wander) * b.bob;
      const s = b.size * this.scale * 0.85;
      // a real wingbeat is not a sine: the downstroke is the fast power stroke
      const ph = b.flap % (Math.PI * 2);
      const e =
        b.glide > 0
          ? 0.25
          : ph < Math.PI * 0.8
            ? Math.cos((ph / (Math.PI * 0.8)) * Math.PI)
            : -Math.cos(((ph - Math.PI * 0.8) / (Math.PI * 1.2)) * Math.PI);
      const haze = 1 - b.par * 4;
      const color = `oklch(${(0.17 + haze * 0.14).toFixed(3)} 0.03 40 / ${(0.95 - haze * 0.25).toFixed(3)})`;
      this.drawBird(px, py, s, e, Math.sign(b.speed) || 1, Math.sin(b.bank) * 0.08, color);
    }

    // Zeus's eagle, high over the hill, riding a thermal. It leaves when he comes.
    if (this.zeus.state === "gone" && about > 0.5) this.drawEagle();
    this.ctx.restore();
  }

  /**
   * A golden eagle seen from below, soaring.
   *
   * It was a dark plank with four nicks in each end. What makes an eagle read
   * as an eagle from the ground: wings very long and broad, nearly parallel-
   * edged, bending slightly forward at the wrist; the hand ending in six or
   * seven separate primaries spread like fingers, each slotted from the next;
   * a head and heavy hooked bill that project well ahead of the wings; a
   * fairly long tail, fanned and rounded. Against a low sun the trailing edge
   * — flight feathers, thin and translucent — glows, while the coverts stay
   * dark.
   *
   * It circles on a thermal, so its heading turns as it goes round and it
   * banks into the turn; every so often it gives a few slow, deep wingbeats.
   */
  private drawEagle() {
    const ctx = this.ctx;
    const { w, h } = this;
    const t = this.t * 0.11;
    // position on the circle and the heading of its tangent (the way it flies)
    const cx = w * 0.66 + Math.cos(t) * w * 0.1;
    const cy = h * 0.13 + Math.sin(t) * h * 0.035;
    // heading on the (horizontal) circle it is flying, not on the screen
    const heading = Math.atan2(Math.cos(t), -Math.sin(t)) + Math.PI / 2;
    const u = Math.max(1, this.scale) * 1.15;
    // the bank: the outer wing reads a little longer, the inner shorter
    const bank = Math.sin(t * 2) * 0.12;
    // a short bout of slow wingbeats every ~11 s; otherwise a fixed glide
    const bout = (this.t % 11) / 11;
    const flapping = bout < 0.12;
    const beat = flapping ? Math.sin((bout / 0.12) * Math.PI * 4) : 0;
    const spanK = 1 - Math.abs(beat) * 0.28;

    ctx.save();
    ctx.translate(cx, cy);
    // we see it from far below and to the side, so its circle is foreshortened:
    // flying across the view the wings close up toward a line, flying toward
    // or away they spread to their full span
    ctx.scale(1, 0.5);
    ctx.rotate(heading);
    const dark = "oklch(0.2 0.025 45 / 0.92)";
    const coverts = "oklch(0.26 0.035 55 / 0.92)";

    const wing = (side: number) => {
      const k = spanK * (1 + side * bank);
      const X = (v: number) => side * v * u * k;
      // leading edge: shoulder, forward to the wrist, then the outer hand
      ctx.beginPath();
      ctx.moveTo(X(1.6), -2.2 * u);
      ctx.quadraticCurveTo(X(6), -3.4 * u, X(10.5), -3.9 * u + beat * 1.2 * u);
      ctx.quadraticCurveTo(X(14), -3.4 * u, X(16.5), -2.6 * u);
      // the primaries: seven fingers, slotted, the middle ones longest
      const fingers = 7;
      for (let f = 0; f < fingers; f++) {
        const q = f / (fingers - 1);
        const baseY = -2.6 + q * 4.6;
        const len = 4.2 + Math.sin(q * Math.PI) * 2.2;
        const tipX = 16.5 + len;
        const tipY = baseY - 0.4 + q * 1.2;
        ctx.lineTo(X(16.5 + len * 0.55), (baseY - 0.35) * u);
        ctx.quadraticCurveTo(X(tipX + 0.3), (tipY - 0.2) * u, X(tipX), tipY * u);
        ctx.quadraticCurveTo(
          X(tipX - 0.2),
          (tipY + 0.45) * u,
          X(16.5 + len * 0.5),
          (baseY + 0.55) * u,
        );
        ctx.lineTo(X(16.6), (baseY + 0.7) * u);
      }
      // trailing edge: the bulge of the long secondaries, back in to the body
      ctx.quadraticCurveTo(X(13), 3.4 * u, X(9), 4.2 * u);
      ctx.quadraticCurveTo(X(4.5), 4.8 * u, X(1.4), 3 * u);
      ctx.closePath();
      ctx.fillStyle = dark;
      ctx.fill();
      // the coverts: a darker-warm band along the front of the wing
      ctx.fillStyle = coverts;
      ctx.beginPath();
      ctx.moveTo(X(1.6), -2.2 * u);
      ctx.quadraticCurveTo(X(6), -3.4 * u, X(10.5), -3.9 * u + beat * 1.2 * u);
      ctx.quadraticCurveTo(X(13.5), -3.3 * u, X(15.5), -2.4 * u);
      ctx.quadraticCurveTo(X(10), -0.6 * u, X(1.6), 0.2 * u);
      ctx.closePath();
      ctx.fill();
      // the backlit trailing edge: thin flight feathers with the sun through them
      ctx.strokeStyle = "rgba(255, 196, 130, 0.38)";
      ctx.lineWidth = Math.max(0.8, 0.9 * u);
      ctx.beginPath();
      ctx.moveTo(X(16), 1.9 * u);
      ctx.quadraticCurveTo(X(13), 3.2 * u, X(9), 4 * u);
      ctx.quadraticCurveTo(X(4.5), 4.6 * u, X(1.8), 2.9 * u);
      ctx.stroke();
    };
    wing(-1);
    wing(1);

    // tail: long, fanned, rounded at the end, the tip translucent
    ctx.fillStyle = dark;
    ctx.beginPath();
    ctx.moveTo(-1.3 * u, 4 * u);
    ctx.quadraticCurveTo(-3.6 * u, 8.5 * u, -3.1 * u, 10.2 * u);
    ctx.quadraticCurveTo(0, 11.4 * u, 3.1 * u, 10.2 * u);
    ctx.quadraticCurveTo(3.6 * u, 8.5 * u, 1.3 * u, 4 * u);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "rgba(255, 196, 130, 0.3)";
    ctx.lineWidth = Math.max(0.8, 0.8 * u);
    ctx.beginPath();
    ctx.moveTo(-3 * u, 10 * u);
    ctx.quadraticCurveTo(0, 11.2 * u, 3 * u, 10 * u);
    ctx.stroke();
    // body: heavy chest tapering to the tail
    ctx.fillStyle = dark;
    ctx.beginPath();
    ctx.ellipse(0, 0.6 * u, 2.3 * u, 4.6 * u, 0, 0, Math.PI * 2);
    ctx.fill();
    // head and neck projecting well ahead of the wings, with the hooked bill
    ctx.beginPath();
    ctx.ellipse(0, -4.6 * u, 1.35 * u, 1.9 * u, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.moveTo(-0.55 * u, -6.1 * u);
    ctx.quadraticCurveTo(0, -7.6 * u, 0.55 * u, -6.1 * u);
    ctx.closePath();
    ctx.fill();
    // golden nape catching the light
    ctx.fillStyle = "rgba(220, 160, 90, 0.35)";
    ctx.beginPath();
    ctx.ellipse(0, -3.9 * u, 1.1 * u, 0.9 * u, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  /**
   * Dust hanging in the low sun. Only the motes near the disc catch enough
   * light to show; the rest of the sky stays clean. Drawn additively and
   * tiny, so they read as air rather than as particles.
   */
  private drawMotes() {
    if (this.lowQuality) return;
    const ctx = this.ctx;
    const { w, h } = this;
    const sun = this.sunScreen();
    const storm =
      (this.zeus.state === "gone" ? 1 : 0.2) *
      Math.max(0, Math.min(1, this.st.light.strength)) *
      (this.st.elev < 20 ? 1 : 0.4);
    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    for (const m of this.motes) {
      const drift = this.t * (6 + m.z * 10);
      let x = (m.fx * w + drift - this.camX * this.scale * m.z * 0.15) % (w + 40);
      if (x < -20) x += w + 40;
      x -= 20;
      const y = m.fy * h + Math.sin(this.t * 0.5 + m.ph) * 8 * m.z;
      const d = Math.hypot(x - sun.x, (y - sun.y) * 1.4) / (w * 0.32);
      const a = Math.exp(-d * d) * 0.38 * storm * (0.6 + 0.4 * Math.sin(this.t * 1.7 + m.ph));
      if (a < 0.02) continue;
      ctx.fillStyle = `rgba(255, 236, 196, ${a.toFixed(3)})`;
      ctx.beginPath();
      ctx.arc(x, y, 0.4 + m.z * 0.8, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }

  /** deterministic pseudo-random hash for procedural scatter */
  private hash(n: number): number {
    const x = Math.sin(n * 127.1 + 311.7) * 43758.5453;
    return x - Math.floor(x);
  }

  /** 2D deterministic hash for stable per-position variation */
  private hash2(x: number, y: number): number {
    const n = Math.sin(x * 127.1 + y * 311.7 + 74.7) * 43758.5453;
    return n - Math.floor(n);
  }

  private drawMist() {
    const ctx = this.ctx;
    const { w, h } = this;
    const t = this.t;
    const layers: Array<{ y: number; a: number; hh: number; sp: number }> = [
      { y: h * 0.5, a: 0.1, hh: 40, sp: 0.12 },
      { y: h * 0.58, a: 0.13, hh: 56, sp: 0.09 },
      { y: h * 0.66, a: 0.09, hh: 44, sp: 0.15 },
    ];
    // dawn fills the low ground with mist; by night it is barely there
    const boost = 1 + this.st.mist * 2.4;
    const mc = this.st.ramp[4];
    const mistRGB = `${Math.round(mc[0])},${Math.round(mc[1])},${Math.round(mc[2])}`;
    for (const L of layers) {
      ctx.save();
      ctx.globalAlpha = Math.min(0.5, L.a * boost);
      // vertical falloff keeps the bank edges soft and airy
      const grad = ctx.createLinearGradient(0, L.y - L.hh, 0, L.y + L.hh * 2);
      // valley mist takes the colour of the light falling into it
      grad.addColorStop(0, `rgba(${mistRGB},0)`);
      grad.addColorStop(0.5, `rgba(${mistRGB},1)`);
      grad.addColorStop(1, `rgba(${mistRGB},0)`);
      ctx.fillStyle = grad;
      ctx.beginPath();
      for (let sx = -10; sx <= w + 10; sx += 10) {
        const y = L.y + Math.sin(sx * 0.008 + t * L.sp + L.y * 0.1) * 16;
        if (sx === -10) ctx.moveTo(sx, y);
        else ctx.lineTo(sx, y);
      }
      for (let sx = w + 10; sx >= -10; sx -= 10) {
        const y = L.y + L.hh + Math.sin(sx * 0.006 + t * (L.sp + 0.04) + L.y * 0.1) * 12;
        ctx.lineTo(sx, y);
      }
      ctx.closePath();
      ctx.fill();
      ctx.restore();
    }
  }

  /**
   * What makes the slope rock rather than a brown gradient.
   *
   * The ground is the largest single area in the frame and it was one flat
   * wash. Everything here is drawn in world space and hashed off the world
   * position, so it is nailed to the hillside — texture that slides against the
   * terrain reads as a filter over the picture rather than as the ground having
   * a surface.
   *
   * Three passes, in the order a geologist would name them.
   *
   * Bedding planes: the hill is sedimentary, so it has layers, and layers
   * outcrop parallel to the surface. They are drawn as long dark lines that
   * follow the terrain curve at fixed depths below it, fading out with depth
   * because the lower part of the frame is in shadow and holds no detail.
   *
   * Gullies: water leaves the hill straight down the fall line, not along it,
   * so these are the one thing here running across the bedding. They are what
   * stops the strata reading as contour lines on a map.
   *
   * Grit: a scatter of specks, dense just under the skyline and gone within a
   * hundred pixels of it. Detail belongs where the light is; putting an even
   * field of it over the whole slope would flatten the very gradient that gives
   * the hill its form.
   */
  /** the hillside's surface texture, as a world-anchored repeating pattern */
  private groundPat: CanvasPattern | null = null;
  private groundPatDpr = 0;

  /**
   * The face of the hill.
   *
   * It was one smooth brown gradient with a few ruled lines and vertical
   * strokes on it, which reads as a vector shape rather than ground. Now it is
   * a seamless tile of real-looking dirt — mottled earth, grit, gravel and
   * stones each lit on the sun's side, dry grass litter, cracks in the crust —
   * pinned to the world so it scrolls with the hill. Over that: the light. The
   * low sun only grazes the top of the slope, so a warm band runs just under
   * the skyline and the face drops off into shade below it.
   */
  private drawGroundDetail(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const ctx = this.ctx;
    const L = this.level;
    const s = this.scale;
    const { w, h } = this;
    void toScreenX;

    const TILE = 512;
    if (!this.groundPat || this.groundPatDpr !== this.dpr) {
      const tex = groundTexture(TILE * this.dpr);
      this.groundPat = ctx.createPattern(tex, "repeat");
      this.groundPatDpr = this.dpr;
    }
    const pat = this.groundPat;
    if (pat && !this.lowQuality) {
      const ox = -((this.camX * s) % TILE);
      const oy = (this.groundY + this.camY * s * 0.55 + this.shakeY) % TILE;
      pat.setTransform(new DOMMatrix().translateSelf(ox, oy).scaleSelf(1 / this.dpr, 1 / this.dpr));
      ctx.fillStyle = pat;
      ctx.globalAlpha = 0.9;
      ctx.fillRect(0, 0, w, h);
      ctx.globalAlpha = 1;
    }

    // the light: a warm band under the skyline, then shade falling away below
    const line = () => {
      ctx.beginPath();
      for (let px = -20; px <= w + 20; px += 16) {
        const y = toScreenY(terrainAt(L, this.camX + px / s));
        if (px === -20) ctx.moveTo(px, y);
        else ctx.lineTo(px, y);
      }
    };
    ctx.lineJoin = "round";
    ctx.lineCap = "round";
    for (const [lw, a] of [
      [120, 0.07],
      [30, 0.12],
    ] as const) {
      ctx.strokeStyle = rgb(this.st.light.color, a * Math.min(1, this.st.light.strength * 1.2));
      ctx.lineWidth = lw * s;
      line();
      ctx.stroke();
    }
  }

  /**
   * Rock breaking through the surface, and the largest single gain available.
   *
   * The slope was one smooth curve, and a smooth curve is what makes a hillside
   * look drawn rather than eroded. The skyline is also where all the contrast in
   * the frame is — dark ground against a bright sky — so it is the edge the eye
   * reads the whole shape from, and a notch in it is worth more than any amount
   * of texture painted on the shadowed face below.
   *
   * Each outcrop is a small angular block: bedrock, so it is drawn as straight
   * facets rather than as a rounded stone, and tilted to a bedding angle so it
   * looks like a layer coming through rather than a boulder set down. The lit
   * top edge is the only bright mark, and it is the one that sells it.
   */
  private drawOutcrops(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const ctx = this.ctx;
    const L = this.level;
    const s = this.scale;
    const step = 96;
    const left = this.camX - step;
    const right = this.camX + this.w / s + step;

    for (let wx = Math.floor(left / step) * step; wx < right; wx += step) {
      const h1 = this.hash(wx * 0.83 + 13);
      if (h1 < 0.44) continue;
      const h2 = this.hash(wx * 1.71 + 27);
      const h3 = this.hash(wx * 3.19 + 41);

      const ox = wx + (h1 - 0.5) * step * 0.7;
      const x = toScreenX(ox);
      const y = toScreenY(terrainAt(L, ox));
      const wide = (10 + h2 * 22) * s;
      const rise = (6 + h3 * 17) * s;
      const tilt = (h2 - 0.5) * 0.5;
      // the local slope, so a block sits along the hill instead of on a shelf
      const lean = Math.atan(slopeAt(L, ox) * this.scale * 0.55) * -1;

      ctx.save();
      ctx.translate(x, y + 1);
      ctx.rotate(lean + tilt * 0.35);

      // an irregular, weathered block: jagged top, flat-ish base in the soil
      const n = 9;
      const top: Array<[number, number]> = [];
      for (let k = 0; k <= n; k++) {
        const t = k / n;
        const jag = this.hash(wx * 0.37 + k * 2.3);
        const hump = Math.sin(t * Math.PI) ** 0.6;
        top.push([-wide + t * wide * 2, -rise * hump * (0.7 + jag * 0.4) + (jag - 0.5) * 2 * s]);
      }
      ctx.fillStyle = "oklch(0.2 0.025 48)";
      ctx.beginPath();
      ctx.moveTo(-wide * 1.05, rise * 0.55);
      for (const [px, py] of top) ctx.lineTo(px, py);
      ctx.lineTo(wide * 1.05, rise * 0.55);
      ctx.closePath();
      ctx.fill();
      // a fracture face catching the light, and a dark joint through the block
      ctx.fillStyle = "oklch(0.3 0.03 55 / 0.7)";
      ctx.beginPath();
      ctx.moveTo(top[5]![0], top[5]![1]);
      ctx.lineTo(top[6]![0], top[6]![1]);
      ctx.lineTo(top[7]![0], top[7]![1]);
      ctx.lineTo(top[6]![0] - wide * 0.1, rise * 0.1);
      ctx.closePath();
      ctx.fill();
      ctx.strokeStyle = "oklch(0.1 0.02 45 / 0.6)";
      ctx.lineWidth = Math.max(0.7, 0.9 * s);
      ctx.beginPath();
      ctx.moveTo(top[3]![0], top[3]![1]);
      ctx.lineTo(top[3]![0] + wide * 0.08, rise * 0.2);
      ctx.stroke();

      // the hairline the low sun leaves along the top
      ctx.strokeStyle = "oklch(0.78 0.08 66 / 0.6)";
      ctx.lineWidth = Math.max(0.7, 1.1 * s);
      ctx.lineJoin = "round";
      ctx.beginPath();
      for (let k = 2; k <= n; k++) {
        const [px, py] = top[k]!;
        if (k === 2) ctx.moveTo(px, py);
        else ctx.lineTo(px, py);
      }
      ctx.stroke();
      ctx.restore();
    }
  }

  private drawStones(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const ctx = this.ctx;
    const s = this.scale;
    const step = 44;
    const startWx = Math.floor(this.camX / step) * step;
    const endWx = this.camX + this.w / this.scale;
    for (let wx = startWx; wx < endWx; wx += step) {
      const h1 = this.hash(wx);
      if (h1 < 0.42) continue;
      const px = toScreenX(wx + (h1 - 0.5) * step);
      const py = toScreenY(terrainAt(this.level, wx)) + 1;
      const sz = (0.5 + ((h1 * 7919) % 100) / 100) * 3.4 * s;
      // soft cast shadow on the dirt
      ctx.fillStyle = "rgba(0,0,0,0.18)";
      ctx.beginPath();
      ctx.ellipse(px + sz * 0.25, py + sz * 0.18, sz * 1.05, sz * 0.6, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = `oklch(0.3 0.025 60 / ${0.55 + h1 * 0.3})`;
      ctx.beginPath();
      ctx.ellipse(px, py, sz, sz * 0.62, (h1 - 0.5) * 0.6, 0, Math.PI * 2);
      ctx.fill();
      // sunlit top edge
      ctx.fillStyle = "oklch(0.52 0.03 68 / 0.5)";
      ctx.beginPath();
      ctx.ellipse(px - sz * 0.15, py - sz * 0.3, sz * 0.42, sz * 0.2, 0, 0, Math.PI * 2);
      ctx.fill();
      // moss speck on the shadowed side
      if (h1 > 0.7) {
        ctx.fillStyle = "oklch(0.45 0.05 130 / 0.35)";
        ctx.beginPath();
        ctx.ellipse(px + sz * 0.3, py + sz * 0.2, sz * 0.28, sz * 0.14, 0.5, 0, Math.PI * 2);
        ctx.fill();
      }
    }
  }

  /**
   * A stele at the top of every ledge. Dark stone until the boulder has gone
   * over it; after that a small flame burns on it, so the way back down shows
   * how much of the hill has been won this cycle.
   */
  /** in the void, the stone breathes out pale wisps — old voices, rising */
  private drawWhispers(bx: number, by: number, R: number) {
    if (this.saga.stage < 2 || this.phase !== "playing" || this.lowQuality) return;
    const ctx = this.ctx;
    const s = this.scale;
    ctx.save();
    for (let i = 0; i < 4; i++) {
      const p = (this.t * 0.18 + i * 0.25) % 1;
      const x = bx + (this.hash(i * 4.1) - 0.5) * R * 1.2 + Math.sin(this.t * 0.8 + i) * 6 * s;
      const y = by - R * 0.4 - p * 60 * s;
      const a = Math.sin(p * Math.PI) * (this.saga.stage === 3 ? 0.16 : 0.12);
      const g = ctx.createRadialGradient(x, y, 0, x, y, 9 * s);
      const c = this.saga.stage === 3 ? "255, 226, 170" : "205, 215, 240";
      g.addColorStop(0, `rgba(${c}, ${a.toFixed(3)})`);
      g.addColorStop(1, `rgba(${c}, 0)`);
      ctx.fillStyle = g;
      ctx.fillRect(x - 10 * s, y - 10 * s, 20 * s, 20 * s);
    }
    ctx.restore();
  }

  /** the light the others on the hill are painted in */
  private npcLight(): NpcLight {
    const l = this.lightScreen();
    return {
      color: l.color,
      strength: l.strength,
      t: this.t,
      dim: Math.max(0, Math.min(0.6, (2 - this.st.elev) / 13)),
    };
  }

  /** set by the interface while one of them is speaking */
  speaker: string | null = null;
  /** the eagle's daily visit to Prometheus, and whether it has come this climb */
  private eagle: EagleVisit = { phase: "away", t: 0 };
  private eagleDone = false;
  /** Merope walking away at the end, 0-1 */
  private meropeLeave = 0;
  private meropeLeaving = false;
  /** where the others are on screen this frame, for the camera in a scene */
  private npcPos: Partial<Record<string, { x: number; y: number }>> = {};

  /** Merope, Tantalus in his pool, Prometheus on his rock */
  private drawNpcs(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const L = this.level;
    const s = this.scale;
    const ctx = this.ctx;
    const Lt = this.npcLight();
    const at = (f: number) => {
      const wx = L.length * f;
      const sx = toScreenX(wx);
      return sx > -120 && sx < this.w + 120
        ? { x: sx, y: toScreenY(terrainAt(L, wx)) - 5 * s }
        : null;
    };
    this.npcPos = {};
    const tan = at(NPC_AT.tan);
    if (tan) this.npcPos["tan"] = tan;
    if (tan) drawTantalus(ctx, tan.x, tan.y + 3 * s, s * 1.25, Lt);
    const pro = at(NPC_AT.pro);
    if (pro) this.npcPos["pro"] = { x: pro.x + 30 * s, y: pro.y - 30 * s };
    if (pro) {
      // the eagle comes once a climb, when he is in sight
      if (this.eagle.phase === "away" && !this.eagleDone && this.phase === "playing") {
        this.eagle = { phase: "arrive", t: 0 };
        this.eagleDone = true;
      }
      drawPrometheus(ctx, pro.x + 18 * s, pro.y, s * 1.3, Lt, this.eagle);
    }
    const me = at(NPC_AT.merope);
    if (me) this.npcPos["merope"] = { x: me.x, y: me.y - 40 * s };
    const leaving = this.meropeLeaving ? this.meropeLeave : 0;
    const visible = this.saga.meropeHere || (this.saga.s.finale && leaving < 1);
    if (me && visible) {
      const act = this.saga.stage;
      drawMerope(ctx, me.x, me.y + 4 * s, s, {
        act,
        turned: this.saga.meropeTurned,
        talking: this.speaker === "merope",
        leave: leaving,
        L: Lt,
      });
    }
  }

  /** the echoes of earlier slips on this hill: pale, still, flickering */
  private drawEchoes(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    if (!this.ghosts.length || this.lowQuality) return;
    const ctx = this.ctx;
    const L = this.level;
    ctx.save();
    for (let i = 0; i < this.ghosts.length; i++) {
      const g = this.ghosts[i]!;
      if (g.lv !== L.id) continue;
      const sx = toScreenX(g.wx);
      if (sx < -200 || sx > this.w + 200) continue;
      // fainter the older it is; it fades as he comes close, like a memory
      const near = Math.min(1, Math.abs(toScreenX(this.x - 64) - sx) / (160 * this.scale));
      const fl = 0.75 + Math.sin(this.t * 2.3 + i * 1.7) * 0.25;
      ctx.globalAlpha = (0.08 + 0.1 * ((i + 1) / this.ghosts.length)) * fl * near;
      const sy = toScreenY(terrainAt(L, g.wx));
      ctx.drawImage(g.cv, sx + g.ox, sy + g.oy, g.w, g.h);
    }
    ctx.restore();
  }

  /** the wedge a companion laid in the path */
  private drawWedge(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    if (!this.saga.s.wedge && this.wedgeT > 1.2) return;
    if (this.phase !== "playing") return;
    const wx = this.wedgeX();
    const x = toScreenX(wx);
    if (x < -40 || x > this.w + 40) return;
    const s = this.scale;
    const y = toScreenY(terrainAt(this.level, wx));
    const ctx = this.ctx;
    ctx.save();
    ctx.fillStyle = "oklch(0.38 0.03 50)";
    ctx.beginPath();
    ctx.moveTo(x - 9 * s, y + 1 * s);
    ctx.lineTo(x - 1 * s, y - 9 * s);
    ctx.lineTo(x + 3 * s, y + 1 * s);
    ctx.closePath();
    ctx.fill();
    // a faint ember glow while it waits, a flash when it holds
    const flash = Math.max(0, 1 - this.wedgeT / 1.2);
    const a = (this.saga.s.wedge ? 0.25 + Math.sin(this.t * 2) * 0.1 : 0) + flash * 0.7;
    const g = ctx.createRadialGradient(x - 2 * s, y - 3 * s, 0, x - 2 * s, y - 3 * s, 16 * s);
    g.addColorStop(0, `rgba(255, 190, 110, ${a.toFixed(3)})`);
    g.addColorStop(1, "rgba(255, 190, 110, 0)");
    ctx.fillStyle = g;
    ctx.fillRect(x - 20 * s, y - 20 * s, 40 * s, 40 * s);
    ctx.restore();
  }

  /**
   * The stone remembers. Every summit leaves a scar — every time the god threw
   * it down — so an old stone is a map of its falls. In the void, pale
   * whispers drift up out of the cracks; once he has made the climb his own,
   * the cracks glow from inside. Drawn in the stone's own turning frame, clipped
   * to its outline.
   */
  private drawStoneMemory(ctx: CanvasRenderingContext2D, R: number) {
    const n = Math.min(48, this.cycles);
    const s = this.scale;
    const stage = this.saga.stage;
    const glow = this.saga.s.ended ? 1 : stage === 3 ? 0.55 : 0;
    const pain = this.saga.s.burden === "pro";
    ctx.save();
    ctx.lineCap = "round";
    for (let i = 0; i < n; i++) {
      const a0 = this.hash(i * 7.31 + 0.2) * Math.PI * 2;
      const r0 = R * (0.15 + this.hash(i * 3.17) * 0.75);
      let px = Math.cos(a0) * r0;
      let py = Math.sin(a0) * r0;
      const segs = 2 + Math.floor(this.hash(i * 5.5) * 3);
      // older scars are worn shallow; the newest are sharp
      const age = (n - i) / Math.max(1, n);
      const depth = 0.18 + (1 - age) * 0.32;
      const pts: Array<[number, number]> = [[px, py]];
      let dir = this.hash(i * 9.1) * Math.PI * 2;
      for (let k = 0; k < segs; k++) {
        dir += (this.hash(i * 11 + k) - 0.5) * 1.4;
        const len = R * (0.08 + this.hash(i * 13 + k) * 0.14);
        px += Math.cos(dir) * len;
        py += Math.sin(dir) * len;
        pts.push([px, py]);
      }
      const path = () => {
        ctx.beginPath();
        ctx.moveTo(pts[0]![0], pts[0]![1]);
        for (const [x, y] of pts.slice(1)) ctx.lineTo(x, y);
      };
      path();
      ctx.strokeStyle = `rgba(14, 10, 12, ${depth.toFixed(3)})`;
      ctx.lineWidth = (0.6 + (1 - age) * 0.8) * s;
      ctx.stroke();
      if (glow > 0 || pain) {
        path();
        const pulse = 0.7 + Math.sin(this.t * 1.6 + i) * 0.3;
        ctx.strokeStyle = pain
          ? `rgba(255, 90, 40, ${(0.55 * pulse).toFixed(3)})`
          : `rgba(255, 214, 140, ${(0.5 * glow * pulse).toFixed(3)})`;
        ctx.lineWidth = 0.6 * s;
        ctx.stroke();
      }
    }
    ctx.restore();
  }

  private drawSteles(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const ledges = this.level.ledges;
    if (!ledges) return;
    const ctx = this.ctx;
    const s = this.scale;
    for (const [lx] of ledges) {
      const wx = lx + 46;
      const sx = toScreenX(wx);
      if (sx < -40 || sx > this.w + 40) continue;
      const sy = toScreenY(terrainAt(this.level, wx)) + 3 * s;
      const sw = 10 * s;
      const sh = 50 * s;
      // shaft, slightly tapered, with a capital
      ctx.fillStyle = "oklch(0.24 0.03 48)";
      ctx.beginPath();
      ctx.moveTo(sx - sw / 2, sy);
      ctx.lineTo(sx - sw * 0.4, sy - sh);
      ctx.lineTo(sx + sw * 0.4, sy - sh);
      ctx.lineTo(sx + sw / 2, sy);
      ctx.closePath();
      ctx.fill();
      ctx.fillRect(sx - sw * 0.75, sy - sh - 4 * s, sw * 1.5, 4 * s);
      // sunlit edge
      ctx.fillStyle = "oklch(0.55 0.06 60 / 0.6)";
      ctx.fillRect(sx + sw * 0.28, sy - sh, 1.4 * s, sh);

      const lit = this.checkpoint > lx && this.phase === "playing";
      if (!lit) continue;
      const fy = sy - sh - 6 * s;
      const flare = Math.max(0, 1 - this.checkpointT / 0.8);
      const flick = 1 + Math.sin(this.t * 13 + lx) * 0.12 + Math.sin(this.t * 7.3) * 0.08;
      const r = (14 + flare * 30) * s * flick;
      const glow = ctx.createRadialGradient(sx, fy, 0, sx, fy, r);
      glow.addColorStop(0, `oklch(0.92 0.13 80 / ${0.55 + flare * 0.4})`);
      glow.addColorStop(1, "oklch(0.8 0.14 60 / 0)");
      ctx.fillStyle = glow;
      ctx.beginPath();
      ctx.arc(sx, fy, r, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "oklch(0.95 0.1 85)";
      ctx.beginPath();
      ctx.ellipse(sx, fy - 2 * s * flick, 2.2 * s, 4.2 * s * flick, 0, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  /**
   * The breath ring over his head, and the beat closing in on it.
   *
   * The outer arc is his breath, gold going to ember red as it runs out. A
   * second, fainter circle shrinks onto it once per beat: press as they meet
   * and the heave lands. It fades away whenever there is nothing to say — full
   * breath and no push — so a player standing still sees an empty sky.
   */
  private drawBreathRing(cx: number, cy: number) {
    const ctx = this.ctx;
    const show =
      this.active &&
      this.phase === "playing" &&
      (this.breath < 0.98 || this.input > 0.15 || this.slipT > 0 || this.denyT < 0.6);
    this.hudA += ((show ? 1 : 0) - this.hudA) * Math.min(1, this.frameDt * 6);
    if (this.hudA < 0.02) return;
    // the bold ring: larger, heavier, and told apart by shape as well as by
    // colour (dashed when breath is low, dotted when it has run out), for
    // anyone who cannot easily tell amber from red
    const bold = this.boldRing;
    const lw = bold ? 1.9 : 1;
    const r = Math.max(9, 12 * this.scale) * (bold ? 1.35 : 1);
    ctx.save();
    ctx.globalAlpha = this.hudA;
    ctx.lineCap = "round";

    // track
    ctx.strokeStyle = bold ? "oklch(0.12 0.02 40 / 0.75)" : "oklch(0.2 0.02 40 / 0.45)";
    ctx.lineWidth = 3.2 * lw;
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.stroke();

    // breath
    const low = this.breath < 0.3;
    const deny = this.denyT < 0.6 && Math.sin(this.denyT * 40) > 0;
    ctx.strokeStyle =
      this.slipT > 0 || deny
        ? "oklch(0.62 0.19 28)"
        : low
          ? `oklch(0.7 0.16 40 / ${0.7 + Math.sin(this.t * 12) * 0.3})`
          : "oklch(0.88 0.11 82)";
    if (bold) {
      ctx.strokeStyle =
        this.slipT > 0 || deny ? "#ffffff" : low ? "oklch(0.95 0.02 90)" : "oklch(0.95 0.06 210)";
      ctx.setLineDash(this.slipT > 0 || deny ? [1, 6] : low ? [6, 5] : []);
    }
    ctx.lineWidth = 2.6 * lw;
    if (this.breath > 0.001) {
      ctx.beginPath();
      ctx.arc(cx, cy, r, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * this.breath);
      ctx.stroke();
    }
    ctx.setLineDash([]);
    // launch threshold tick, so it is visible whether a launch is affordable
    const ta = -Math.PI / 2 + Math.PI * 2 * KICK_COST;
    ctx.strokeStyle = "oklch(0.95 0.02 80 / 0.7)";
    ctx.lineWidth = 1.2;
    ctx.beginPath();
    ctx.moveTo(cx + Math.cos(ta) * (r - 3), cy + Math.sin(ta) * (r - 3));
    ctx.lineTo(cx + Math.cos(ta) * (r + 3), cy + Math.sin(ta) * (r + 3));
    ctx.stroke();

    // the beat: a circle closing in onto the ring
    if (this.kickT < 0 && this.slipT <= 0) {
      const toBeat = (BEAT - (this.t % BEAT)) / BEAT; // 1 -> 0 as the beat lands
      const near = 1 - toBeat;
      ctx.strokeStyle = `oklch(0.96 0.03 85 / ${0.15 + near * (bold ? 0.8 : 0.55)})`;
      ctx.lineWidth = 1.4 * lw;
      ctx.beginPath();
      ctx.arc(cx, cy, r + 3 + toBeat * 16, 0, Math.PI * 2);
      ctx.stroke();
    }

    // the last heave, as a ring thrown outward
    const fx = this.heaveFx;
    if (fx && fx.q !== "miss" && fx.t < 0.45) {
      const k = fx.t / 0.45;
      ctx.globalAlpha = this.hudA * (1 - k);
      ctx.strokeStyle = fx.q === "perfect" ? "oklch(0.9 0.14 82)" : "oklch(0.92 0.03 85)";
      ctx.lineWidth = fx.q === "perfect" ? 2.4 : 1.4;
      ctx.beginPath();
      ctx.arc(cx, cy, r + 4 + k * (fx.q === "perfect" ? 22 : 12), 0, Math.PI * 2);
      ctx.stroke();
    }

    // the streak: "×4" beside the ring, swelling as it grows, fading after a break
    if (this.streakShown >= 2 && this.streakT < 1.6) {
      const alive = this.streak >= 2;
      const pop = Math.max(0, 1 - this.streakT / 0.25);
      const fade = alive ? 1 : Math.max(0, 1 - this.streakT / 0.6);
      const size = Math.round(
        (13 + Math.min(8, this.streakShown) * 1.2 + pop * 6) * Math.max(0.8, this.scale),
      );
      ctx.globalAlpha = this.hudA * fade;
      ctx.font = `italic 600 ${size}px Georgia, 'Times New Roman', serif`;
      ctx.textAlign = "left";
      ctx.textBaseline = "middle";
      ctx.shadowColor = "oklch(0.85 0.14 80 / 0.9)";
      ctx.shadowBlur = 10 + pop * 10;
      ctx.fillStyle = this.streakShown >= 5 ? "oklch(0.93 0.13 85)" : "oklch(0.95 0.06 85)";
      ctx.fillText(`×${this.streakShown}`, cx + r + 10, cy - 1);
      ctx.shadowBlur = 0;
    }
    ctx.restore();
  }

  /**
   * The wind. A slow swell with gusts riding on it that travel along the hill
   * (so neighbouring clumps lean in turn, not in unison). Radians of lean.
   */
  /** how much stronger than a fair day the wind is here: the ridge, the storm */
  private windiness(): number {
    const w = this.weather;
    return 1 + (this.level.wind ?? 0) * 1.6 + (w === "storm" ? 1.3 : w === "rain" ? 0.4 : 0);
  }

  /** the push a gust gives the stone, back down the hill (world units/s²) */
  private gustForce(): number {
    const extra = this.windiness() - 1;
    if (extra <= 0) return 0;
    const ramp = 0.35 + 0.65 * this.difficultyRamp();
    return extra * Math.max(0, Math.sin(this.t * 0.23 + 1.3)) ** 3 * 120 * ramp;
  }

  /**
   * How much of the hills' and the weather's bite is felt: a quarter or so on
   * the early cycles, all of it from the tenth on. The new hills and the first
   * storms arrive while a player is still learning the rhythm; they should
   * look the part straight away and only gradually play it.
   */
  private difficultyRamp(): number {
    return Math.min(1, Math.max(0, (this.cycles - 2) / 8));
  }

  private windAt(wx: number): number {
    const t = this.t;
    const swell = Math.sin(t * 0.7 + wx * 0.002) * 0.08;
    const gust =
      Math.max(0, Math.sin(t * 0.23 + 1.3)) ** 3 *
      (0.18 + Math.sin(t * 2.1 - wx * 0.012) * 0.08) *
      this.windiness();
    const flutter = Math.sin(t * 3.3 + wx * 0.05) * 0.04;
    return -(swell + gust + flutter);
  }

  private treeCache = new Map<string, TreeSprite>();

  /**
   * The sky a weather front brings: grey cloud pulled down over the colour of
   * the hour. Rain and storm are overcast; fog only veils.
   */
  private drawOvercast() {
    const k = { clear: 0, rain: 0.8, storm: 0.9, fog: 0.22 }[this.weather] * this.wxFade;
    if (k <= 0) return;
    const ctx = this.ctx;
    const { w, h } = this;
    const night = Math.max(0, Math.min(1, (-this.st.elev - 2) / 10));
    const top = this.weather === "fog" ? [150, 156, 164] : [62, 68, 82];
    const [c0, c1, c2] = top.map((v) => Math.round(v * (1 - night * 0.75))) as [
      number,
      number,
      number,
    ];
    const c = [c0, c1, c2] as const;
    const g = ctx.createLinearGradient(0, 0, 0, h * this.st.horizon);
    g.addColorStop(0, `rgba(${c[0]},${c[1]},${c[2]},${k})`);
    g.addColorStop(1, `rgba(${c[0] + 30},${c[1] + 30},${c[2] + 30},${k * 0.82})`);
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h * this.st.horizon + 2);
    // under a storm, heavy cloud bellies drifting low across the frame
    if (this.weather === "storm" || this.weather === "rain") {
      const n = this.lowQuality ? 4 : 7;
      for (let i = 0; i < n; i++) {
        const cx = ((this.hash(i * 3.7) * (w + 600) + this.t * (14 + i * 3)) % (w + 600)) - 300;
        const cy = h * (0.06 + this.hash(i * 5.1) * 0.22);
        const r = w * (0.18 + this.hash(i * 2.3) * 0.16);
        const cg = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
        cg.addColorStop(0, `rgba(${c[0] - 20},${c[1] - 20},${c[2] - 20},${k * 0.7})`);
        cg.addColorStop(1, `rgba(${c[0] - 20},${c[1] - 20},${c[2] - 20},0)`);
        ctx.fillStyle = cg;
        ctx.fillRect(cx - r, cy - r, r * 2, r * 2);
      }
    }
  }

  /**
   * Rain and fog in front of everything: slanting streaks that lean with the
   * wind, or a veil that swallows the far end of the hill.
   */
  private drawWeather() {
    const ctx = this.ctx;
    const { w, h } = this;
    if (this.weather === "fog") {
      const mc = this.st.ramp[4];
      const g = ctx.createLinearGradient(0, h * 0.25, 0, h);
      const col = `${Math.round(mc[0] * 0.6 + 70)},${Math.round(mc[1] * 0.6 + 72)},${Math.round(mc[2] * 0.6 + 78)}`;
      g.addColorStop(0, `rgba(${col},0.42)`);
      g.addColorStop(0.55, `rgba(${col},0.3)`);
      g.addColorStop(1, `rgba(${col},0.12)`);
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
      return;
    }
    if (this.weather !== "rain" && this.weather !== "storm") return;
    const heavy = this.weather === "storm";
    if (this.saga.winter) {
      // winter: in despair the rain turns to snow, drifting and slow
      const n = Math.round(
        (heavy ? 260 : 170) * (this.lowQuality ? 0.45 : 1) * (w / 1000) * this.wxFade,
      );
      ctx.save();
      ctx.fillStyle = "rgba(240, 245, 255, 0.75)";
      for (let i = 0; i < n; i++) {
        const sp = 50 + this.hash(i * 1.7) * 70;
        const y = ((this.hash(i * 4.3) * (h + 40) + this.t * sp) % (h + 40)) - 20;
        const x =
          ((((this.hash(i * 7.1) * (w + 100) + Math.sin(this.t * 0.7 + i) * 20 - this.t * 30) %
            (w + 100)) +
            w +
            100) %
            (w + 100)) -
          50;
        const r = (0.8 + this.hash(i * 2.9) * 1.6) * this.scale;
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();
      return;
    }
    const n = Math.round(
      (heavy ? 230 : 150) * (this.lowQuality ? 0.45 : 1) * (w / 1000) * this.wxFade,
    );
    const lean = 0.18 + (this.windiness() - 1) * 0.12 + Math.max(0, -this.windAt(this.camX)) * 0.6;
    const t = this.t;
    ctx.save();
    ctx.strokeStyle = heavy ? "rgba(200, 212, 230, 0.32)" : "rgba(200, 212, 230, 0.26)";
    ctx.lineWidth = Math.max(1, this.scale);
    ctx.beginPath();
    for (let i = 0; i < n; i++) {
      const sp = 900 + this.hash(i * 1.7) * 500;
      const len = (14 + this.hash(i * 2.9) * 14) * this.scale;
      const y = ((this.hash(i * 4.3) * (h + 60) + t * sp) % (h + 60)) - 30;
      const x =
        ((((this.hash(i * 7.1) * (w + 200) - y * lean + t * sp * lean * 0.2) % (w + 200)) +
          w +
          200) %
          (w + 200)) -
        100;
      ctx.moveTo(x, y);
      ctx.lineTo(x - len * lean, y + len);
    }
    ctx.stroke();
    ctx.restore();
  }

  /** loose stones strewn over the scree field, so the bad footing shows */
  /** the mud: a black, wet sheen over the trail, pools that catch the sky */
  private drawMire(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const m = this.mireSpan();
    if (!m) return;
    const ctx = this.ctx;
    const s = this.scale;
    const L = this.level;
    const x0 = toScreenX(m[0]);
    const x1 = toScreenX(m[1]);
    if (x1 < -40 || x0 > this.w + 40) return;
    ctx.save();
    // the mud itself, thickest in the middle of the stretch
    ctx.beginPath();
    for (let wx = m[0]; wx <= m[1]; wx += 8) {
      const edge = Math.min(1, (wx - m[0]) / 90, (m[1] - wx) / 90);
      const y = toScreenY(terrainAt(L, wx));
      if (wx === m[0]) ctx.moveTo(toScreenX(wx), y - 1 * s);
      else ctx.lineTo(toScreenX(wx), y - (1 + edge * 2.5 + Math.sin(wx * 0.11) * 0.8) * s);
    }
    for (let wx = m[1]; wx >= m[0]; wx -= 8) {
      const edge = Math.min(1, (wx - m[0]) / 90, (m[1] - wx) / 90);
      ctx.lineTo(toScreenX(wx), toScreenY(terrainAt(L, wx)) + (6 + edge * 10) * s);
    }
    ctx.closePath();
    ctx.fillStyle = "rgba(18, 13, 12, 0.88)";
    ctx.fill();
    // pools: the sky caught in them, and bubbles that rise and burst
    for (let wx = m[0] + 30; wx < m[1] - 20; wx += 46) {
      const h = this.hash(wx * 0.71);
      const px = toScreenX(wx + h * 20);
      if (px < -30 || px > this.w + 30) continue;
      const py = toScreenY(terrainAt(L, wx + h * 20)) + 2 * s;
      const sky = this.st.ramp[3];
      ctx.fillStyle = `rgba(${sky[0] | 0}, ${sky[1] | 0}, ${sky[2] | 0}, 0.28)`;
      ctx.beginPath();
      ctx.ellipse(px, py, (8 + h * 10) * s, (1.4 + h) * s, 0, 0, Math.PI * 2);
      ctx.fill();
      const b = (((this.t * (0.4 + h * 0.5) + h) % 1) + 1) % 1;
      if (b < 0.8) {
        ctx.strokeStyle = `rgba(120, 110, 100, ${(0.5 * (1 - b)).toFixed(3)})`;
        ctx.lineWidth = 0.7 * s;
        ctx.beginPath();
        ctx.arc(
          px + (h - 0.5) * 8 * s,
          py - b * 1.5 * s,
          (0.6 + b * 1.6) * s,
          Math.PI,
          Math.PI * 2,
        );
        ctx.stroke();
      }
    }
    ctx.restore();
  }

  private drawScree(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const sc = this.level.scree;
    if (!sc) return;
    const ctx = this.ctx;
    const s = this.scale;
    ctx.save();
    for (let wx = sc[0]; wx < sc[1]; wx += 6) {
      const px = toScreenX(wx);
      if (px < -20 || px > this.w + 20) continue;
      // thinner at the edges of the field
      const edge = Math.min(1, (wx - sc[0]) / 160, (sc[1] - wx) / 160);
      if (this.hash(wx * 0.37) > 0.2 + edge * 0.75) continue;
      const py = toScreenY(terrainAt(this.level, wx)) + (2 + this.hash(wx * 1.3) * 22) * s;
      const r = (2.4 + this.hash(wx * 2.1) * 4.6) * s;
      const tone = 0.42 + this.hash(wx * 3.3) * 0.2;
      const rot = this.hash(wx) - 0.5;
      ctx.fillStyle = "rgba(14, 9, 6, 0.45)";
      ctx.beginPath();
      ctx.ellipse(px - r * 0.3, py + r * 0.45, r * 1.35, r * 0.6, rot, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = `oklch(${tone.toFixed(3)} 0.018 70)`;
      ctx.beginPath();
      ctx.ellipse(px, py, r * 1.3, r * 0.8, rot, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "rgba(255, 236, 200, 0.28)";
      ctx.beginPath();
      ctx.ellipse(px + r * 0.25, py - r * 0.32, r * 0.65, r * 0.32, rot, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }

  /**
   * Keepsakes of a finished chapter of the book, standing on every hill: a
   * small stele for each chapter near the summit, its numeral cut in it, and
   * for Rome a line of torches up the path, for China paper lanterns.
   */
  private drawKeepsakes(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const done = this.chaptersDone;
    if (!done.length) return;
    const ctx = this.ctx;
    const s = this.scale;
    const L = this.level;
    const night = Math.max(0, Math.min(1, (-this.st.elev + 2) / 10));
    const ground = (wx: number) => toScreenY(terrainAt(L, wx));
    // Rome: torches
    if (done.includes(4)) {
      for (let wx = 300; wx < L.length - 200; wx += 460) {
        const px = toScreenX(wx);
        if (px < -40 || px > this.w + 40) continue;
        const gy = ground(wx) + 2 * s;
        const top = gy - 46 * s;
        ctx.strokeStyle = "oklch(0.24 0.03 50)";
        ctx.lineWidth = 2.6 * s;
        ctx.beginPath();
        ctx.moveTo(px, gy);
        ctx.lineTo(px, top);
        ctx.stroke();
        ctx.fillStyle = "oklch(0.3 0.03 50)";
        ctx.fillRect(px - 4 * s, top - 3 * s, 8 * s, 5 * s);
        const fl = 1 + Math.sin(this.t * 13 + wx) * 0.12 + Math.sin(this.t * 7.3 + wx * 2) * 0.08;
        const glowR = (26 + night * 30) * s * fl;
        const g = ctx.createRadialGradient(px, top - 8 * s, 0, px, top - 8 * s, glowR);
        g.addColorStop(0, `rgba(255, 190, 90, ${0.35 + night * 0.35})`);
        g.addColorStop(1, "rgba(255, 150, 60, 0)");
        ctx.save();
        ctx.globalCompositeOperation = "lighter";
        ctx.fillStyle = g;
        ctx.fillRect(px - glowR, top - 8 * s - glowR, glowR * 2, glowR * 2);
        ctx.restore();
        ctx.fillStyle = "rgba(255, 214, 140, 0.95)";
        ctx.beginPath();
        ctx.ellipse(
          px,
          top - 8 * s,
          3.4 * s,
          7.5 * s * fl,
          Math.sin(this.t * 3 + wx) * 0.15,
          0,
          Math.PI * 2,
        );
        ctx.fill();
      }
    }
    // China: paper lanterns on low posts
    if (done.includes(5)) {
      for (let wx = 520; wx < L.length - 200; wx += 520) {
        const px = toScreenX(wx);
        if (px < -40 || px > this.w + 40) continue;
        const gy = ground(wx) + 2 * s;
        const arm = gy - 52 * s;
        ctx.strokeStyle = "oklch(0.22 0.03 40)";
        ctx.lineWidth = 2 * s;
        ctx.beginPath();
        ctx.moveTo(px, gy);
        ctx.lineTo(px, arm);
        ctx.lineTo(px + 12 * s, arm);
        ctx.stroke();
        const sway = Math.sin(this.t * 1.4 + wx) * 1.5 * s;
        const lx = px + 12 * s + sway;
        const ly = arm + 13 * s;
        const glowR = (22 + night * 26) * s;
        const g = ctx.createRadialGradient(lx, ly, 0, lx, ly, glowR);
        g.addColorStop(0, `rgba(255, 90, 60, ${0.3 + night * 0.35})`);
        g.addColorStop(1, "rgba(255, 60, 40, 0)");
        ctx.save();
        ctx.globalCompositeOperation = "lighter";
        ctx.fillStyle = g;
        ctx.fillRect(lx - glowR, ly - glowR, glowR * 2, glowR * 2);
        ctx.restore();
        ctx.strokeStyle = "rgba(40, 20, 10, 0.8)";
        ctx.lineWidth = 0.8 * s;
        ctx.beginPath();
        ctx.moveTo(px + 12 * s, arm);
        ctx.lineTo(lx, ly - 7 * s);
        ctx.stroke();
        ctx.fillStyle = `oklch(${(0.55 + night * 0.15).toFixed(2)} 0.19 30)`;
        ctx.beginPath();
        ctx.ellipse(lx, ly, 6.5 * s, 8 * s, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = "oklch(0.25 0.04 60)";
        ctx.fillRect(lx - 4 * s, ly - 9 * s, 8 * s, 2 * s);
        ctx.fillRect(lx - 4 * s, ly + 7 * s, 8 * s, 2 * s);
      }
    }
    // a stele for every finished chapter, in a row below the summit
    const sorted = [...done].sort((a, b) => a - b);
    sorted.forEach((ch, i) => {
      const wx = L.length - 360 + i * 26;
      const px = toScreenX(wx);
      if (px < -30 || px > this.w + 30) return;
      const gy = ground(wx) + 3 * s;
      const hh = 26 * s;
      const ww = 12 * s;
      ctx.fillStyle = "oklch(0.36 0.02 70)";
      ctx.beginPath();
      ctx.moveTo(px - ww / 2, gy);
      ctx.lineTo(px - ww / 2, gy - hh + 4 * s);
      ctx.quadraticCurveTo(px, gy - hh - 3 * s, px + ww / 2, gy - hh + 4 * s);
      ctx.lineTo(px + ww / 2, gy);
      ctx.closePath();
      ctx.fill();
      ctx.fillStyle = "rgba(20, 14, 8, 0.7)";
      ctx.font = `600 ${Math.max(6, 7 * s).toFixed(1)}px Georgia, serif`;
      ctx.textAlign = "center";
      const numerals = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];
      ctx.fillText(numerals[ch] ?? "", px, gy - hh * 0.45);
    });
  }

  /** the columns of a fallen temple, standing and broken along the path */
  private drawRuins(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const ruins = this.level.ruins;
    if (!ruins) return;
    const ctx = this.ctx;
    const s = this.scale;
    const light = this.lightScreen();
    const rim = rgb(light.color, 0.35 + light.strength * 0.4);
    for (const [wx, left] of ruins) {
      const px = toScreenX(wx);
      if (px < -60 || px > this.w + 60) continue;
      const base = toScreenY(terrainAt(this.level, wx)) + 4 * s;
      const full = 118 * s;
      const hgt = full * (0.25 + left * 0.75);
      const cw = 15 * s;
      ctx.save();
      // the stylobate block it stands on
      ctx.fillStyle = "oklch(0.3 0.02 60)";
      ctx.fillRect(px - cw * 1.1, base - 6 * s, cw * 2.2, 8 * s);
      // the shaft, tapering a little, its top broken unless it is whole
      const top = base - hgt;
      ctx.beginPath();
      ctx.moveTo(px - cw * 0.78, base - 6 * s);
      ctx.lineTo(px - cw * 0.66, top);
      if (left >= 0.98) {
        ctx.lineTo(px + cw * 0.66, top);
      } else {
        ctx.lineTo(px - cw * 0.2, top - 7 * s * this.hash(wx));
        ctx.lineTo(px + cw * 0.15, top + 5 * s);
        ctx.lineTo(px + cw * 0.66, top - 4 * s * this.hash(wx * 2));
      }
      ctx.lineTo(px + cw * 0.78, base - 6 * s);
      ctx.closePath();
      ctx.fillStyle = "oklch(0.34 0.022 62)";
      ctx.fill();
      // flutes
      ctx.strokeStyle = "rgba(20, 14, 10, 0.35)";
      ctx.lineWidth = Math.max(0.6, 0.8 * s);
      for (let f = -2; f <= 2; f++) {
        ctx.beginPath();
        ctx.moveTo(px + f * cw * 0.26, base - 8 * s);
        ctx.lineTo(px + f * cw * 0.23, top + 6 * s);
        ctx.stroke();
      }
      // the lit edge on the side the light comes from
      const side = light.x > px ? 1 : -1;
      ctx.strokeStyle = rim;
      ctx.lineWidth = 1.4 * s;
      ctx.beginPath();
      ctx.moveTo(px + side * cw * 0.78, base - 6 * s);
      ctx.lineTo(px + side * cw * 0.66, top + 3 * s);
      ctx.stroke();
      // a whole column keeps its capital
      if (left >= 0.98) {
        ctx.fillStyle = "oklch(0.36 0.022 62)";
        ctx.fillRect(px - cw * 1.05, top - 7 * s, cw * 2.1, 7 * s);
        ctx.fillRect(px - cw * 0.85, top - 2 * s, cw * 1.7, 3 * s);
      }
      ctx.restore();
    }
  }

  /** draw a cached tree sprite at its base, skewed by the wind from the root up */
  private drawTree(
    kind: "olive" | "cypress",
    seed: number,
    x: number,
    y: number,
    size: number,
    wx: number,
  ) {
    const ctx = this.ctx;
    const d = this.dpr;
    const px = Math.round(size * d * 8) / 8;
    const key = `${kind}:${seed}:${px}`;
    let sp = this.treeCache.get(key);
    if (!sp) {
      if (this.treeCache.size > 40) this.treeCache.clear();
      sp = kind === "olive" ? oliveSprite(seed, px) : cypressSprite(seed, px);
      this.treeCache.set(key, sp);
    }
    const sway = this.windAt(wx) * (kind === "cypress" ? 0.18 : 0.12);
    ctx.save();
    ctx.translate(x, y);
    ctx.transform(1, 0, sway, 1, 0, 0);
    ctx.drawImage(sp.cv, -sp.ox / d, -sp.oy / d, sp.cv.width / d, sp.cv.height / d);
    ctx.restore();
  }

  private drawOliveTrees(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const s = this.scale;
    this.drawScree(toScreenX, toScreenY);
    this.drawMire(toScreenX, toScreenY);
    this.drawRuins(toScreenX, toScreenY);
    this.drawKeepsakes(toScreenX, toScreenY);
    for (const [wx, sc] of this.level.cypresses ?? []) {
      const px = toScreenX(wx);
      if (px < -60 || px > this.w + 60) continue;
      this.drawTree(
        "cypress",
        Math.round(wx),
        px,
        toScreenY(terrainAt(this.level, wx)) + 2,
        sc * s * 0.95,
        wx,
      );
    }
    for (const [wx, sc] of this.level.trees ?? []) {
      const px = toScreenX(wx);
      if (px < -120 || px > this.w + 120) continue;
      this.drawTree(
        "olive",
        Math.round(wx),
        px,
        toScreenY(terrainAt(this.level, wx)) + 2,
        sc * s * 1.05,
        wx,
      );
    }
  }

  /**
   * The summit cinematic: the sky goes dark, a storm bank rolls in over the
   * peak, and Zeus rises out of it — a colossus, not a man standing beside the
   * stone. See zeus.ts for the figure itself.
   */
  private drawZeus(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const st = this.zeus.state;
    this.boltFrom = null;
    if (st === "gone") return;
    const ctx = this.ctx;
    const { w, h } = this;
    const L = this.level;
    const ease = (x: number) => 1 - Math.pow(1 - Math.max(0, Math.min(1, x)), 3);

    // how present he is: rises in over the first stretch, fades as the stone goes
    let a = 1;
    let rise = 0;
    if (st === "appear") {
      a = ease(this.zeus.t / 1.3);
      rise = 1 - a;
    } else if (st === "fade") {
      a = 1 - ease(this.zeus.t / 1.4);
      rise = -0.25 * (1 - a);
    }

    // the storm: the whole frame drops toward night
    ctx.fillStyle = `rgba(14, 12, 34, ${(0.42 * a).toFixed(3)})`;
    ctx.fillRect(0, 0, w, h);

    // size and place him: about half the frame tall, over and right of the peak
    const k = Math.min((h * 0.46) / 300, (w * 0.45) / 380);
    const sumX = toScreenX(L.length);
    const sumY = toScreenY(terrainAt(L, L.length));
    const cx = Math.max(w * 0.42, Math.min(w * 0.82, sumX + w * 0.3));
    const waist = Math.max(300 * k + h * 0.03, Math.min(sumY - h * 0.04, h * 0.62));
    const breathe = Math.sin(this.t * 1.3) * 2 * k;
    const y = waist + rise * 120 * k + breathe;
    const flashNow = st === "strike" ? Math.max(0, 1 - this.zeus.t / 0.5) : 0;

    // cloud wall rolling in along the top of the frame
    for (let i = 0; i < 9; i++) {
      const px = (i / 8) * w + Math.sin(this.t * 0.4 + i * 1.7) * 18;
      const py = h * 0.02 + (i % 2) * h * 0.05 - (1 - a) * h * 0.2;
      this.zeusPainter.cloud(ctx, px, py, h * (0.13 + (i % 3) * 0.03), a * 0.95, flashNow);
    }

    ctx.save();
    ctx.globalAlpha = a;
    // a faint halo of the god-light behind him
    const halo = ctx.createRadialGradient(cx, y - 200 * k, 0, cx, y - 200 * k, 220 * k);
    halo.addColorStop(0, `rgba(255, 228, 170, ${0.28 + flashNow * 0.4})`);
    halo.addColorStop(1, "rgba(255, 228, 170, 0)");
    ctx.globalCompositeOperation = "lighter";
    ctx.fillStyle = halo;
    ctx.fillRect(cx - 240 * k, y - 430 * k, 480 * k, 460 * k);
    ctx.globalCompositeOperation = "source-over";

    // the body: wind-up until the throw, then the follow-through. The first
    // fraction of a second of the throw cross-fades the two so the arm snaps
    // rather than teleports.
    const kq = Math.round(k * 40) / 40;
    // built at the screen's pixel density, so he is as sharp on a phone as here
    const d = this.dpr;
    const raise = this.zeusPainter.sprite("raise", kq * d);
    const hurl = this.zeusPainter.sprite("hurl", kq * d);
    const blit = (sp: NonNullable<typeof raise>) =>
      ctx.drawImage(sp.cv, cx - sp.ox / d, y - sp.oy / d, sp.cv.width / d, sp.cv.height / d);
    const snap = st === "appear" ? 0 : Math.min(1, this.zeus.t / 0.09);
    if (raise && snap < 1) {
      ctx.globalAlpha = a * (1 - snap);
      blit(raise);
    }
    if (hurl && snap > 0) {
      ctx.globalAlpha = a * snap;
      blit(hurl);
    }
    ctx.globalAlpha = a;

    // eyes kindle as the throw comes, and blaze at it
    const charge = st === "appear" ? ease((this.zeus.t - 0.6) / 1.2) : 1;
    const eyeGlow = 0.25 + charge * 0.5 + flashNow * 0.6;
    ctx.globalCompositeOperation = "lighter";
    for (const ex of [-12, 7]) {
      const gx = cx + ex * kq;
      const gy = y - 205 * kq;
      const g = ctx.createRadialGradient(gx, gy, 0, gx, gy, 9 * kq);
      g.addColorStop(0, `rgba(220, 235, 255, ${eyeGlow.toFixed(3)})`);
      g.addColorStop(1, "rgba(160, 200, 255, 0)");
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.arc(gx, gy, 9 * kq, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalCompositeOperation = "source-over";

    // the bolt in his fist while he winds up, brightening to the throw
    if (st === "appear") {
      const [hx, hy] = zeusHand("raise");
      const flick = 0.85 + Math.sin(this.t * 37) * 0.15;
      drawKeraunos(ctx, cx + hx * kq, y + hy * kq, 110 * kq, -0.18, (0.4 + charge * 0.6) * flick);
    } else if (st === "strike") {
      const [hx, hy] = zeusHand("hurl");
      this.boltFrom = { x: cx + hx * kq, y: y + hy * kq };
    }

    // the storm bank he stands in, over his waist
    for (let i = 0; i < 8; i++) {
      const px = cx + (i - 3.5) * 56 * k + Math.sin(this.t * 0.7 + i) * 6 * k;
      const py = y + (16 + (i % 2) * 10) * k;
      this.zeusPainter.cloud(ctx, px, py, (50 + (i % 3) * 12) * k, 1, flashNow);
    }
    ctx.restore();
  }

  /**
   * The bolt from his hand to the stone. A channel reshaped by Math.random() on
   * every frame is static, not lightning, so the clock is quantised: one
   * channel holds for ~70 ms and then re-strikes, the way a real flash does.
   */
  private drawBolt(tx: number, ty: number) {
    const from = this.boltFrom;
    if (!from || this.zeus.state !== "strike" || this.zeus.t > 0.9) return;
    const ctx = this.ctx;
    const s = this.scale;
    const life = 1 - this.zeus.t / 0.9;
    const strike = Math.floor(this.zeus.t * 14);
    const jitter = (i: number) => this.hash(strike * 31.7 + i * 5.3) - 0.5;
    const power = (0.55 + this.hash(strike * 3.1) * 0.45) * (0.4 + life * 0.6);
    const { x: ox, y: oy } = from;
    const len = Math.hypot(tx - ox, ty - oy);
    const segs = 14;
    const channel: Array<[number, number]> = [[ox, oy]];
    for (let i = 1; i <= segs; i++) {
      const tt = i / segs;
      const spread = len * 0.09 * Math.sin(tt * Math.PI) ** 0.7;
      channel.push([
        ox + (tx - ox) * tt + jitter(i) * spread,
        oy + (ty - oy) * tt + jitter(i + 40) * spread * 0.4,
      ]);
    }
    const trace = () => {
      ctx.beginPath();
      channel.forEach(([px, py], i) => (i === 0 ? ctx.moveTo(px, py) : ctx.lineTo(px, py)));
    };
    ctx.save();
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    ctx.globalCompositeOperation = "lighter";
    ctx.strokeStyle = `rgba(255, 210, 140, ${(0.25 * power).toFixed(3)})`;
    ctx.lineWidth = 22 * s;
    trace();
    ctx.stroke();
    ctx.strokeStyle = `rgba(255, 238, 200, ${(0.6 * power).toFixed(3)})`;
    ctx.lineWidth = 7 * s;
    trace();
    ctx.stroke();
    ctx.globalCompositeOperation = "source-over";
    ctx.strokeStyle = `rgba(255, 255, 255, ${(0.95 * power).toFixed(3)})`;
    ctx.lineWidth = 2.4 * s;
    trace();
    ctx.stroke();
    // forks peeling off the channel and dying out
    ctx.strokeStyle = `rgba(255, 236, 190, ${(0.7 * power).toFixed(3)})`;
    ctx.lineWidth = 1.6 * s;
    ctx.beginPath();
    for (let b = 0; b < 6; b++) {
      const at = 2 + Math.floor(this.hash(strike * 9.7 + b) * (segs - 4));
      let [fx, fy] = channel[at]!;
      ctx.moveTo(fx, fy);
      for (let k = 1; k <= 3; k++) {
        fx += (jitter(b * 7 + k) * 60 + 8) * s * (1 - k * 0.22);
        fy += (jitter(b * 13 + k) * 30 + 10) * s * (1 - k * 0.22);
        ctx.lineTo(fx, fy);
      }
    }
    ctx.stroke();
    // where it lands: a white bloom on the stone
    const g = ctx.createRadialGradient(tx, ty, 0, tx, ty, 70 * s);
    g.addColorStop(0, `rgba(255, 250, 230, ${(0.9 * power).toFixed(3)})`);
    g.addColorStop(0.3, `rgba(255, 220, 160, ${(0.4 * power).toFixed(3)})`);
    g.addColorStop(1, "rgba(255, 210, 150, 0)");
    ctx.globalCompositeOperation = "lighter";
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(tx, ty, 70 * s, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  /** offscreen layers for the man, so his rim light can follow his real outline */
  private figLayers: HTMLCanvasElement[] = [];
  private rimLayer: HTMLCanvasElement | null = null;
  /** set only while drawFigureLit is drawing him: far limbs, body, near arm */
  private figCtxs:
    [CanvasRenderingContext2D, CanvasRenderingContext2D, CanvasRenderingContext2D] | null = null;

  /**
   * The man, with a rim light that is actually his edge.
   *
   * A rim stroke was tried and taken out long ago: hand-fitted curves never
   * sat on the outline of thirty overlapping, moving fills. This does it the
   * way a compositor would. He is painted into three depth layers — the far
   * leg and arm, the body, the near arm — and for each, the silhouette is
   * shifted a pixel or two away from the sun and cut out of itself. What is
   * left is exactly the sunward edge of whatever shape that layer is in this
   * frame, laid back over it in gold. Because the layers are separate, the
   * near arm gets its own edge where it crosses the chest, and throws a soft
   * shadow onto it: the limbs read as round things in front of a body instead
   * of one flat cut-out.
   */
  private drawFigureLit(
    x: number,
    groundY: number,
    scale: number,
    ang: number,
    bcx: number,
    bcy: number,
    R: number,
  ) {
    const main = this.ctx;
    const dpr = this.dpr;
    const s = scale;
    const left = x - 110 * s;
    const top = groundY - 170 * s;
    const bw = 230 * s;
    const bh = 190 * s;
    const pw = Math.max(1, Math.ceil(bw * dpr));
    const ph = Math.max(1, Math.ceil(bh * dpr));
    const mk = (cv: HTMLCanvasElement | null | undefined) => {
      const c = cv ?? document.createElement("canvas");
      if (c.width !== pw || c.height !== ph) {
        c.width = pw;
        c.height = ph;
      }
      return c;
    };
    for (let i = 0; i < 3; i++) this.figLayers[i] = mk(this.figLayers[i]);
    const rim = (this.rimLayer = mk(this.rimLayer));
    const cs = this.figLayers.map((c) => c.getContext("2d"));
    const rc = rim.getContext("2d");
    if (!cs[0] || !cs[1] || !cs[2] || !rc || this.lowQuality) {
      this.drawFigure(x, groundY, scale, ang, bcx, bcy, R, this.kickT >= 0);
      return;
    }
    const ctxs = cs as [
      CanvasRenderingContext2D,
      CanvasRenderingContext2D,
      CanvasRenderingContext2D,
    ];
    for (const c of ctxs) {
      c.setTransform(1, 0, 0, 1, 0, 0);
      c.globalCompositeOperation = "source-over";
      c.clearRect(0, 0, pw, ph);
      c.setTransform(dpr, 0, 0, dpr, -left * dpr, -top * dpr);
    }
    this.figCtxs = ctxs;
    try {
      this.drawFigure(x, groundY, scale, ang, bcx, bcy, R, this.kickT >= 0);
    } finally {
      this.figCtxs = null;
      this.ctx = main;
    }

    // the sun's side, and how strongly it reaches him (the storm takes it away)
    const sun = this.lightScreen();
    // which side of him the rim is on follows the sun smoothly, never flips
    const dx = Math.max(-1, Math.min(1, (x - sun.x) / (this.w * 0.12)));
    const e = Math.max(1.2, 1.5 * s * dpr);
    const storm = this.zeus.state === "gone" ? 0 : this.zeus.state === "fade" ? 0.5 : 1;
    const strength = 0.85 * (1 - storm * 0.7) * Math.min(1, sun.strength * 1.2);

    /** paint `src`'s silhouette in `color` into the scratch layer, minus a shifted copy */
    const edge = (src: HTMLCanvasElement, color: string, ox: number, oy: number) => {
      rc.setTransform(1, 0, 0, 1, 0, 0);
      rc.globalCompositeOperation = "source-over";
      rc.clearRect(0, 0, pw, ph);
      rc.drawImage(src, 0, 0);
      rc.globalCompositeOperation = "source-in";
      rc.fillStyle = color;
      rc.fillRect(0, 0, pw, ph);
      rc.globalCompositeOperation = "destination-out";
      rc.drawImage(src, ox, oy);
    };
    /*
     * Form. He is painted now, not cut out, and a painted body is round: each
     * layer gets a broad soft light on the side facing the sun, a core shadow
     * turning away from it, and warm light bounced up off the ground under him.
     * All three come from the layer's own silhouette, shifted and blurred, so
     * they follow every pose without anyone having to place them.
     */
    const fy0 = groundY - 60 * s;
    let lx = sun.x - x;
    let ly = sun.y - fy0;
    const ln = Math.hypot(lx, ly) || 1;
    lx /= ln;
    ly /= ln;
    const K = 7 * s * dpr;
    const soft = (i: number, color: string, ox: number, oy: number, blur: number) => {
      edge(this.figLayers[i]!, color, ox, oy);
      const c = ctxs[i as 0 | 1 | 2];
      c.setTransform(1, 0, 0, 1, 0, 0);
      c.globalCompositeOperation = "source-atop";
      c.filter = `blur(${blur.toFixed(1)}px)`;
      c.drawImage(rim, 0, 0);
      c.filter = "none";
      c.globalCompositeOperation = "source-over";
    };
    const keyA = 0.5 * strength + 0.08;
    const dim = Math.max(0, Math.min(0.62, (2 - this.st.elev) / 13));
    for (let i = 0; i < 3; i++) {
      const far = i === 0 ? 0.6 : 1;
      // the core shadow first, on the side turned from the light
      soft(i, `rgba(22, 10, 16, ${(0.5 * far).toFixed(3)})`, lx * K * 1.4, ly * K * 1.4, K * 0.7);
      // the lit side, broad and soft
      soft(i, rgb(sun.color, keyA * far), -lx * K, -ly * K, K * 0.55);
      // bounce from the sunlit ground: warm, from below
      soft(i, `rgba(255, 168, 105, ${(0.2 * far * (1 - dim)).toFixed(3)})`, 0, -K * 0.9, K * 0.5);
      // after dark the colour drains out of him, and what is left is moonlit blue
      if (dim > 0.01) {
        const c = ctxs[i as 0 | 1 | 2];
        c.setTransform(1, 0, 0, 1, 0, 0);
        c.globalCompositeOperation = "source-atop";
        c.fillStyle = `rgba(12, 16, 34, ${dim.toFixed(3)})`;
        c.fillRect(0, 0, pw, ph);
        c.globalCompositeOperation = "source-over";
      }
    }

    const rimOnto = (i: number, a: number) => {
      edge(this.figLayers[i]!, rgb(sun.color, strength * a), dx * e, e * 0.55);
      const c = ctxs[i as 0 | 1 | 2];
      c.setTransform(1, 0, 0, 1, 0, 0);
      c.globalCompositeOperation = "source-atop";
      c.drawImage(rim, 0, 0);
      c.globalCompositeOperation = "source-over";
    };
    rimOnto(0, 0.55);
    rimOnto(1, 1);
    rimOnto(2, 1);

    // the near arm's shadow on the body: its silhouette, darkened, nudged away
    // from the sun and clipped to the body layer
    rc.setTransform(1, 0, 0, 1, 0, 0);
    rc.globalCompositeOperation = "source-over";
    rc.clearRect(0, 0, pw, ph);
    rc.drawImage(this.figLayers[2]!, 0, 0);
    rc.globalCompositeOperation = "source-in";
    rc.fillStyle = "rgba(0, 0, 0, 0.55)";
    rc.fillRect(0, 0, pw, ph);
    const body = ctxs[1];
    body.setTransform(1, 0, 0, 1, 0, 0);
    body.globalCompositeOperation = "source-atop";
    body.drawImage(rim, -dx * e * 1.2, e * 0.8);
    body.globalCompositeOperation = "source-over";

    for (const layer of this.figLayers) main.drawImage(layer, left, top, bw, bh);
  }

  private drawFigure(
    x: number,
    groundY: number,
    scale: number,
    ang: number,
    bcx: number,
    bcy: number,
    R: number,
    kicking = false,
  ) {
    // Three depth layers when drawFigureLit provides them — the far limbs, the
    // body, the near arm — so each can get its own rim light and the near arm a
    // shadow on the chest. Without them everything lands on the one context.
    const layers = this.figCtxs;
    let ctx = layers ? layers[1] : this.ctx;
    const layer = (n: 0 | 1 | 2) => {
      if (!layers || layers[n] === ctx) return;
      const m = ctx.getTransform();
      ctx = layers[n];
      ctx.setTransform(m);
    };
    const s = scale;
    /*
     * What he is doing, beyond walking and pushing:
     *  - heave: the half-second after a well-timed press, an explosive drive
     *  - slip: out of breath, the stone winning, being shoved back down
     *  - rest: braced with a shoulder in the stone, breathing
     *  - awe: at the summit, still holding it, looking up at the sky
     *  - left: the stone is gone; he stands at the top and watches it go
     */
    // at the ending he lets go of the stone, stands, and turns to the light
    // and at the very end, at the bottom, he stands beside his stone and looks out
    const lettingGo =
      (this.finaleOn && this.finaleT > 1.2) ||
      (this.epiphany &&
        this.phase === "summit" &&
        this.phaseT > 1.4 &&
        this.phaseT < EPIPHANY_STRIKE - 0.5);
    const left = this.phase === "rolling" || this.phase === "descent" || lettingGo;
    const walking = this.phase === "descent" ? Math.min(1, this.walkV / 100) : 0;
    const awe = this.phase === "summit";
    /*
     * The strike. He used to keep pushing through a bolt of lightning, then
     * flip round on the spot in a single frame while the stone rolled straight
     * through his body. Now he flinches when it hits — recoils, ducks, throws
     * an arm up over his face — staggers as the stone goes, and only then
     * turns to watch it. The stone passes in front of him (see render), not
     * through him.
     */
    const struck = this.zeus.state === "strike" && awe ? Math.min(1, this.zeus.t / 0.12) : 0;
    const stagger = this.phase === "rolling" ? Math.max(0, 1 - this.phaseT / 0.5) : 0;
    const flinch = Math.max(struck, stagger);
    // in a scene on the way down he turns back up the hill, to face her
    const turned = left && stagger === 0 && !(this.cutscene && this.phase === "descent");
    const slip = this.phase === "playing" && this.slipT > 0 ? Math.min(1, this.slipT / 0.4) : 0;
    const fx = this.heaveFx;
    const heave =
      this.phase === "playing" && fx && fx.q !== "miss" && fx.t < 0.35
        ? (1 - fx.t / 0.35) * (fx.q === "perfect" ? 1 : 0.6)
        : 0;
    const resting =
      this.phase === "playing" && !kicking && slip === 0 && Math.abs(this.input) <= 0.15
        ? Math.max(0, 1 - this.breath) * 0.6 + 0.4
        : 0;
    const pushing = this.phase === "playing" && !kicking;
    // holding: the stone giving ground (0..1), and him shoving it back (0..1)
    const giving = resting > 0 ? Math.min(1, -this.creep / 6) : 0;
    const drive = resting > 0 ? this.holdDrive : 0;
    // How hard he is actually travelling, 0..1. Stride, bob, arm swing and the
    // hem of the tunic all scale off this, so standing still looks like standing
    // still instead of marching in place.
    // (once the stone is gone, its speed is not his: he is standing still)
    const speed =
      this.phase === "descent" ? this.walkV : left ? 0 : kicking ? KICK_SPRINT : Math.abs(this.vx);
    const effort = Math.min(1, speed / 130);

    // Load is not the same as effort, and it is the one that shows. Leaning on
    // the stone and getting nowhere is the hardest he ever works: full input
    // against no speed. That is the moment the body has to look like it is
    // losing, so this peaks exactly there and falls away as the stone starts to
    // give.
    const load = Math.max(
      pushing
        ? Math.max(0, Math.min(1, this.input)) * (1 - Math.min(1, Math.abs(this.vx) / 95))
        : 0,
      heave,
      slip,
      awe ? 0.35 : 0,
      // holding is work too: the muscles are never slack, and they spike as
      // he drives the stone back to where it was
      resting > 0 ? 0.4 + giving * 0.25 + drive * 0.6 : 0,
    );
    // Capped lower than it was. Past a certain point the shoulders get far
    // enough ahead of the hips that the spine reads as horizontal, and a
    // horizontal spine with a head out front is a quadruped no matter how well
    // the legs are moving.
    const leanBase = left
      ? 0.12
      : kicking
        ? 0.78
        : slip > 0
          ? 0.85 - slip * 0.5
          : pushing || awe
            ? 0.85 + load * 0.22 + heave * 0.2 + resting * 0.08 + drive * 0.18 - giving * 0.12
            : 0.35;
    // recoiling from the bolt: upright, weight back on the heels
    const lean = leanBase * (1 - flinch) - 0.15 * flinch;
    // he sinks into it: hips drop and the knees take the weight
    const sink = (load * 7 + heave * 4 + resting * 3) * s;
    // where his head points: up at the god, down at the stone going away, bowed
    // over the stone while he gets his breath back
    const headTiltBase = awe
      ? -0.42
      : left
        ? 0.3
        : resting * 0.32 * (1 - drive * 0.7) + giving * 0.1 + slip * 0.2;
    // ducking from the flash
    /*
     * Walking down he is not a man staring at his feet. Every few steps he
     * lifts his head to the sky, now and then looks back over his shoulder at
     * the summit he has just left, and between times lets his gaze drift.
     */
    const pulse = (t: number, period: number, offset: number, dur: number) => {
      const l = (((t - offset) % period) + period) % period;
      if (l >= dur) return 0;
      const e = Math.min(1, l / 0.45, (dur - l) / 0.45);
      return e * e * (3 - 2 * e);
    };
    const descending = this.phase === "descent";
    const lookUp = descending ? pulse(this.phaseT, 7.3, 1.6, 2.4) : 0;
    const glance = descending ? pulse(this.phaseT, 9.7, 4.8, 1.9) : 0;
    const drift = descending ? Math.sin(this.t * 0.8) * 0.06 + Math.sin(this.t * 1.9) * 0.025 : 0;
    const walkTilt = 0.22 + drift - lookUp * 0.72 - glance * 0.3;
    const serene = this.saga.serenity;
    // at the ending he does not look up at the god: he looks out at the world
    const epi = this.epiphany && awe ? Math.min(1, this.phaseT / 1.5) : 0;
    const headTiltCore = headTiltBase * (1 - epi) - 0.04 * epi - (pushing ? 0.1 * serene : 0);
    const headTilt = descending ? walkTilt : headTiltCore * (1 - flinch) + 0.45 * flinch;
    // turning the head about the neck: narrows through the turn, then faces back
    const headTurn = glance > 0 ? Math.cos(Math.min(1, glance * 1.15) * Math.PI) : 1;
    // the fine shake of a muscle held near its limit, too fast to count. Two of
    // them, out of phase, so the two sides of him do not shiver as one object.
    // the further he has come in his mind, the less he fights the stone: the
    // same work, done without the shaking
    const calmK = 1 - serene * 0.6;
    const tremble = Math.sin(this.t * 31) * load * 0.9 * s * calmK;
    const shudder = Math.sin(this.t * 23.4 + 1.7) * load * 0.75 * s * calmK;
    /**
     * Breathing. Shallow and fast when he is straining, deep and slow when he is
     * not, and hard and ragged at a sprint. It drives the chest, the shoulders and
     * how far his mouth is open — one number so they cannot fall out of step.
     */
    const breath = Math.sin(
      this.t * (1.6 + load * 4.2 + (kicking ? 3.4 : 0) + resting * (1 - this.breath) * 3),
    );
    // out of breath shows whether or not he is pushing right now
    const spent = this.phase === "playing" ? Math.max(0, 1 - this.breath * 1.6) : 0;
    const winded = Math.max(load, kicking ? 0.75 : 0, spent, this.slipT > 0 ? 1 : 0);

    const bob = Math.abs(Math.cos(this.gait)) * (kicking ? 3.6 : 4) * s * effort;

    /*
     * A man stands plumb, whatever the ground does. Tilting the whole figure
     * with the slope only worked while he faced uphill into the stone (where it
     * read as a lean into the work); turned round and walking down, the same
     * tilt laid him back against the hill like a man about to fall over. Once
     * the stone is gone he keeps only a trace of the slope, and his feet find
     * the ground instead: the rest of the slope goes into where each foot lands.
     */
    const plumb = left ? 0.82 * (1 - flinch) : 0;
    const bodyAng = ang * (1 - plumb);
    const groundTilt = Math.tan(ang * plumb) * (turned ? -1 : 1);
    ctx.save();
    ctx.translate(x, groundY - bob);
    ctx.rotate(bodyAng);
    // he turns round to watch it go back down
    if (turned) ctx.scale(-1, 1);

    // He stands with the sun above and behind him, so left alone he resolves to
    // a black cutout — which is what a camera would give you, and useless when
    // he is the thing you are meant to be watching. A backlit subject gets fill:
    // here it is bounce off the sunlit ground, warm and coming from below, which
    // is why this runs lighter at his feet than at his shoulders.
    /**
     * He is a silhouette now, and that is the realistic reading rather than a
     * stylistic one: the sun is behind the ridge he is climbing, so what reaches the
     * eye is a shape against a bright sky, not a lit surface. Every interior detail
     * that used to be drawn on him — pectorals, abdominals, the eye, the mouth —
     * was information a backlit body does not give you, and each one flattened him
     * by competing with his outline.
     *
     * So the anatomy has to live entirely in the edge, which is where the work has
     * gone. The only gradient left is a faint lift toward his feet, where light
     * bouncing off the sunlit ground genuinely does reach him.
     */
    /*
     * That was true while he was a silhouette. He is painted now: sun-bronzed
     * skin, with the light, shade and bounce laid over it afterwards by
     * drawFigureLit, and the muscle drawn in, because a hero you are meant to
     * watch for a thousand climbs has to be a body and not a hole in the sky.
     */
    const painted = !!layers; // the drawn-in detail needs the layers to clip to
    const body = ctx.createLinearGradient(0, -104 * s, 0, 0);
    body.addColorStop(0, "oklch(0.5 0.085 50)");
    body.addColorStop(0.62, "oklch(0.47 0.082 47)");
    body.addColorStop(1, "oklch(0.42 0.075 44)");
    /**
     * The limbs on his far side. Barely separated from the near ones — enough that
     * two crossing legs do not fuse into one slab, not so much that he reads as two
     * different colours of man.
     */
    const bodyFar = "oklch(0.35 0.065 42)";
    /** The one thing he is wearing, and the only value break on him. */
    const cloth = "oklch(0.63 0.035 80)";
    const hairC = "oklch(0.17 0.025 40)";
    const leather = "oklch(0.3 0.05 45)";
    /**
     * Muscle and bone drawn on top of a shape: only ever onto what is already
     * painted in that layer (source-atop), so a crease can never stray off his
     * body into the sky.
     */
    const ink = (fn: () => void) => {
      if (!painted) return;
      ctx.save();
      ctx.globalCompositeOperation = "source-atop";
      ctx.lineCap = "round";
      ctx.lineJoin = "round";
      fn();
      ctx.restore();
    };
    const crease = (w0: number, a = 0.3) => {
      ctx.strokeStyle = `rgba(40, 16, 10, ${a})`;
      ctx.lineWidth = w0 * s;
    };
    const sheen = (w0: number, a = 0.22) => {
      ctx.strokeStyle = `rgba(255, 214, 170, ${a})`;
      ctx.lineWidth = w0 * s;
    };

    // soft shadow under feet
    ctx.save();
    ctx.globalAlpha = 0.3;
    ctx.fillStyle = "oklch(0.06 0 0)";
    ctx.beginPath();
    ctx.ellipse(0, -1 * s, 15 * s, 3.5 * s, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();

    // ---- legs: a real gait, one foot planted while the other swings ----
    // Through the stance half the foot stays down and travels backwards under
    // him; since the cycle is driven by distance covered, that plant holds still
    // against the ground instead of skating. Only the swing half lifts.
    /**
     * A limb as a tapered shape with a muscle belly on one side, not a round-capped
     * line of constant width. This is most of the difference between a stick man and
     * a body: a thigh is thick at the hip, swells at the quadriceps and narrows hard
     * above the knee, and a line cannot do any of that.
     *
     * `belly` is signed along +x, so a positive belly bulges forward (quadriceps,
     * biceps) and a negative one bulges back (hamstring, calf, triceps).
     */
    const seg = (
      ax: number,
      ay: number,
      bx: number,
      by: number,
      wa: number,
      wb: number,
      belly: number,
      at: number,
      fill: string | CanvasGradient,
    ) => {
      const dx = bx - ax;
      const dy = by - ay;
      const len = Math.hypot(dx, dy) || 1;
      // normal, flipped so +belly always means "toward the direction he faces"
      let nx = -dy / len;
      let ny = dx / len;
      if (nx < 0) {
        nx = -nx;
        ny = -ny;
      }
      const mx = ax + dx * at;
      const my = ay + dy * at;
      const wm = wa + (wb - wa) * at;
      // A quadratic through a wanted mid point D needs C = 2D − (P0+P2)/2.
      const side = (sgn: number, extra: number) => {
        const p0x = ax + nx * wa * sgn;
        const p0y = ay + ny * wa * sgn;
        const p2x = bx + nx * wb * sgn;
        const p2y = by + ny * wb * sgn;
        const dxp = mx + nx * (wm + extra) * sgn;
        const dyp = my + ny * (wm + extra) * sgn;
        return {
          p0x,
          p0y,
          p2x,
          p2y,
          cx: 2 * dxp - 0.5 * (p0x + p2x),
          cy: 2 * dyp - 0.5 * (p0y + p2y),
        };
      };
      const front = side(1, Math.max(0, belly));
      const back = side(-1, Math.max(0, -belly));
      ctx.fillStyle = fill;
      ctx.beginPath();
      ctx.moveTo(front.p0x, front.p0y);
      ctx.quadraticCurveTo(front.cx, front.cy, front.p2x, front.p2y);
      ctx.lineTo(back.p2x, back.p2y);
      ctx.quadraticCurveTo(back.cx, back.cy, back.p0x, back.p0y);
      ctx.closePath();
      ctx.fill();
      // joints, so the segments meet in a rounded socket instead of a corner
      ctx.beginPath();
      ctx.arc(ax, ay, wa, 0, Math.PI * 2);
      ctx.fill();
      ctx.beginPath();
      ctx.arc(bx, by, wb, 0, Math.PI * 2);
      ctx.fill();
    };

    /**
     * A line along a limb: a muscle's edge, a bone under the skin. `off` is how
     * far from the axis, in limb widths, toward the way he faces (+) or away;
     * `bow` bends it outward like the belly it runs along.
     */
    const along = (
      ax: number,
      ay: number,
      bx: number,
      by: number,
      off: number,
      t0: number,
      t1: number,
      bow: number,
    ) => {
      const dx = bx - ax;
      const dy = by - ay;
      const len = Math.hypot(dx, dy) || 1;
      let nx = -dy / len;
      let ny = dx / len;
      if (nx < 0) {
        nx = -nx;
        ny = -ny;
      }
      const p0x = ax + dx * t0 + nx * off;
      const p0y = ay + dy * t0 + ny * off;
      const p1x = ax + dx * t1 + nx * off;
      const p1y = ay + dy * t1 + ny * off;
      const tm = (t0 + t1) / 2;
      const cx = ax + dx * tm + nx * (off + bow);
      const cy = ay + dy * tm + ny * (off + bow);
      ctx.beginPath();
      ctx.moveTo(p0x, p0y);
      ctx.quadraticCurveTo(cx, cy, p1x, p1y);
      ctx.stroke();
    };

    const stepLen = (kicking ? 18 : 10) * s * effort;
    const liftH = (kicking ? 13 : 4.5) * s * effort;
    // He was 5.3 heads tall — a child's proportion, and the reason he read as a
    // doll however well he moved. The hip and shoulder go up, the head comes
    // down, and he lands near eight heads, which is where a grown man sits.
    // walking down he steps long and straight-legged, not crouched
    const hipY0 = -42 * s + sink - walking * 1.2 * s;
    // the spine's lean from vertical, and the hips going back to balance it: a
    // man pushing hard does not fold over his feet, he lays his whole body out
    // behind his hands
    const theta = left ? 0.04 - flinch * 0.08 : lean * 0.45 + heave * 0.08 - (awe ? 0.12 : 0);
    const HX = -theta * 14 * s;
    const THIGH = 24 * s;
    const SHIN = 22 * s;

    /**
     * The walk. The planted foot travels *backwards* under him while the swinging
     * one comes through in front — that is how a leg drives a body forward, and it
     * was the wrong way round: the foot on the ground used to slide forward with
     * him, which is precisely what walking backwards looks like.
     */
    const walkPose = (theta: number) => {
      const lift = Math.max(0, Math.sin(theta));
      return { fx: -Math.cos(theta) * stepLen, fy: -lift * liftH, lift };
    };

    /**
     * Braced against the stone. At a standstill under full input the walk collapses
     * to both feet under the hip — a man standing to attention — when what a man
     * actually does is split his stance: one leg folded under the weight in front,
     * one driving straight out behind with the heel up and the toes dug in. Nothing
     * else in the figure sells the effort as hard as this does.
     */
    const bracePose = (front: boolean) =>
      front
        ? { fx: (7 - giving * 1.5) * s, fy: 0, lift: 0 }
        : // the back foot skids as the stone pushes him, then digs back in
          { fx: (-30 + giving * 3.5) * s, fy: -2.6 * s, lift: 0 };

    const pose = (theta: number, front: boolean) => {
      // sliding back, the feet drag: the stride runs in reverse and stays low
      const w = walkPose(slip > 0 ? -theta : theta);
      const b = bracePose(front);
      const m = load;
      const fx = w.fx + (b.fx - w.fx) * m;
      return {
        fx,
        // on a slope the foot lands where the ground actually is
        fy: w.fy + (b.fy - w.fy) * m + fx * groundTilt,
        lift: w.lift * (1 - m),
        /** the back heel comes off the ground as he drives through it */
        heel: front ? 0 : m,
      };
    };

    /** Two-bone solve, knee always breaking forward. */
    const knee = (fx: number, fy: number) => {
      const dx = fx - HX;
      const dy = fy - hipY0;
      const raw = Math.hypot(dx, dy) || 1;
      const d = Math.min(raw, THIGH + SHIN - 0.4 * s);
      const ux = dx / raw;
      const uy = dy / raw;
      const a = (THIGH * THIGH - SHIN * SHIN + d * d) / (2 * d);
      const h = Math.sqrt(Math.max(0, THIGH * THIGH - a * a));
      // of the two perpendiculars take the one pointing the way he faces
      const px = -uy >= 0 ? -uy : uy;
      const py = -uy >= 0 ? ux : -ux;
      return { kx: HX + ux * a + px * h, ky: hipY0 + uy * a + py * h };
    };

    const leg = (theta: number, front: boolean, shade: string | CanvasGradient) => {
      const p = pose(theta, front);
      // the quads shake under a load they cannot move
      const jitter = front ? tremble * 0.6 : shudder * 0.6;
      const { kx, ky } = knee(p.fx, p.fy + jitter * 0.2);
      // thigh: heavy at the hip, quadriceps forward, narrow above the knee
      seg(HX, hipY0, kx + jitter, ky, 7.6 * s, 4.6 * s, (2 + load * 1.7) * s, 0.42, shade);
      // shin: calf belly behind, ankle thin
      seg(
        kx + jitter,
        ky,
        p.fx,
        p.fy - 1.6 * s,
        4.4 * s,
        2.5 * s,
        -(1.7 + load * 1.1) * s,
        0.34,
        shade,
      );
      ink(() => {
        const kx2 = kx + jitter;
        // the quadriceps: the long sweep of the outer thigh, and its teardrop
        // above the knee, standing out harder under load
        crease(0.75, 0.24 + load * 0.16);
        along(HX, hipY0, kx2, ky, 1.6 * s, 0.12, 0.86, 1.4 * s);
        along(HX, hipY0, kx2, ky, -2.4 * s, 0.2, 0.9, -1 * s);
        sheen(1.6, 0.16 + load * 0.08);
        along(HX, hipY0, kx2, ky, 3.6 * s, 0.25, 0.7, 1.2 * s);
        // the kneecap
        ctx.fillStyle = "rgba(255, 210, 165, 0.2)";
        ctx.beginPath();
        ctx.ellipse(kx2 + 2.4 * s, ky - 0.6 * s, 1.9 * s, 2.4 * s, 0, 0, Math.PI * 2);
        ctx.fill();
        // the shin bone catches light; the calf splits in two heads behind
        sheen(1, 0.2);
        along(kx2, ky, p.fx, p.fy - 1.6 * s, 2.6 * s, 0.12, 0.8, 0);
        crease(0.7, 0.22 + load * 0.15);
        along(kx2, ky, p.fx, p.fy - 1.6 * s, -1.4 * s, 0.1, 0.6, -1.2 * s);
      });

      // sandal. Toe-down through the swing; heel up and toes gripping when he is
      // driving off it, which is the pose that reads as "pushing" from the ankle up.
      const toeDrop = p.lift * 0.5 + p.heel * 0.62;
      ctx.save();
      ctx.translate(p.fx, p.fy);
      ctx.rotate(toeDrop);
      ctx.fillStyle = shade;
      ctx.beginPath();
      ctx.ellipse(1.5 * s, -1.4 * s, 5.4 * s, 2 * s, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.beginPath();
      ctx.ellipse(-0.5 * s, -3.4 * s, 2.1 * s, 1.9 * s, 0, 0, Math.PI * 2);
      ctx.fill();
      // toes, splayed against the ground when he is driving
      if (p.heel > 0.15) {
        ctx.beginPath();
        ctx.ellipse(5.4 * s, -1 * s, 2.2 * s, 1.2 * s, 0, 0, Math.PI * 2);
        ctx.fill();
      }
      {
        // the sole, and the straps of the sandal over the instep and the ankle
        ctx.fillStyle = leather;
        ctx.beginPath();
        ctx.ellipse(1.6 * s, 0.2 * s, 5.8 * s, 0.9 * s, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = leather;
        ctx.lineWidth = 1.1 * s;
        ctx.beginPath();
        ctx.moveTo(-1.8 * s, -4.6 * s);
        ctx.lineTo(1 * s, -3.6 * s);
        ctx.moveTo(1.6 * s, -3.2 * s);
        ctx.lineTo(3.4 * s, -0.4 * s);
        ctx.moveTo(-2.6 * s, -2.4 * s);
        ctx.lineTo(0.4 * s, -5.6 * s);
        ctx.stroke();
      }
      ctx.restore();
    };

    // far leg first and slightly dimmed, so the two read as depth not as a blur.
    // Under load the far one is the leg driving out behind him.
    layer(0);
    leg(this.gait + Math.PI, false, bodyFar);
    layer(1);
    leg(this.gait, true, body);

    // ---- torso: drawn in the spine's own frame, so a lean is a real lean ----
    /*
     * The trunk used to stay almost upright and slide its shoulders forward,
     * about sixteen degrees at the hardest push — a man leaning on a counter,
     * not one putting his weight through a rock. It now pivots at the hip. At
     * full load the spine is near thirty degrees from vertical, the back leg is
     * a straight line from heel to shoulder, and that diagonal is what reads as
     * effort before any detail does.
     */
    layer(1);
    const hipX = HX;
    const hipY = hipY0;
    const SPINE = 35 * s;
    const sinT = Math.sin(theta);
    const cosT = Math.cos(theta);
    const shX = hipX + sinT * SPINE + tremble;
    const shY = hipY - cosT * SPINE;
    // The chest fills and empties. Shallow and quick under load, slow at rest.
    const chest = (1 + breath * 0.5) * s;
    // the lats flare and the back rounds under a load it cannot move
    const flare = (load * 1.6 + heave * 0.8) * s;

    ctx.save();
    ctx.translate(hipX + tremble * 0.3, hipY);
    ctx.rotate(theta);
    const Ls = SPINE;
    ctx.fillStyle = body;
    ctx.beginPath();
    // back of the neck out along the trapezius
    ctx.moveTo(-2 * s, -Ls - 6 * s);
    ctx.quadraticCurveTo(-8 * s, -Ls - 4 * s, -10.5 * s, -Ls + 1 * s);
    // lat: wide under the armpit, drawn in hard at the waist
    ctx.quadraticCurveTo(-13.5 * s - flare, -Ls * 0.6, -8 * s, -Ls * 0.24);
    // the small of the back, curving into the sacrum
    ctx.quadraticCurveTo(-9.8 * s, -6 * s, -8.5 * s, 2 * s);
    ctx.lineTo(9 * s, 2 * s);
    // hip flexor up into the oblique
    ctx.quadraticCurveTo(10 * s, -6 * s, 8.5 * s, -Ls * 0.32);
    // the belly and the arch of the ribs, breathing
    ctx.quadraticCurveTo(12.5 * s + chest, -Ls * 0.58, 12.5 * s + chest, -Ls + 4 * s);
    // pectoral shelf into the front of the shoulder
    ctx.quadraticCurveTo(12 * s, -Ls - 3 * s, 5 * s, -Ls - 6 * s);
    ctx.closePath();
    ctx.fill();
    // glutes: a rounded mass behind the pelvis that the skirt drapes over
    ctx.beginPath();
    ctx.ellipse(-6.8 * s, -1 * s, 5.6 * s, 7 * s, 0.25, 0, Math.PI * 2);
    ctx.fill();

    // Deltoids. A shoulder is a cap of muscle sitting over the joint, wider than
    // the arm it feeds, and it swells when the arm is loaded.
    const delt = (5 + load * 1.4) * s;
    ctx.beginPath();
    ctx.ellipse(-9 * s, -Ls + 1.5 * s, delt * 0.9, delt, -0.3, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(10 * s, -Ls + 1.5 * s, delt, delt * 1.05, 0.25, 0, Math.PI * 2);
    ctx.fill();
    // trapezius, rising toward the neck — the muscle that shows when a man braces
    ctx.beginPath();
    ctx.moveTo(-8 * s, -Ls - 1 * s);
    ctx.quadraticCurveTo(1 * s, -Ls - (8 + load * 3) * s, 9 * s, -Ls - 1 * s);
    ctx.quadraticCurveTo(1 * s, -Ls + 2 * s, -8 * s, -Ls - 1 * s);
    ctx.fill();

    /*
     * The anatomy of a man who has pushed a stone forever: the shelf of the
     * chest, the ridged belly, the serratus along the ribs, the long groove of
     * the spine between the back muscles and the blade of the scapula. All of
     * it sharpens as the load comes on — the body is a diagram of effort.
     */
    const def = 0.22 + load * 0.2;
    ink(() => {
      // the deltoids, parted from the chest and the back
      crease(0.8, def);
      ctx.beginPath();
      ctx.moveTo(6.5 * s, -Ls - 3 * s);
      ctx.quadraticCurveTo(4.6 * s, -Ls + 2 * s, 7.4 * s, -Ls + 6.8 * s);
      ctx.moveTo(-6 * s, -Ls - 3 * s);
      ctx.quadraticCurveTo(-4.4 * s, -Ls + 2.4 * s, -7.6 * s, -Ls + 6.4 * s);
      ctx.stroke();
      // the lower edge of the pectoral
      crease(1.1, def + 0.06);
      ctx.beginPath();
      ctx.moveTo(4 * s, -Ls + 8.5 * s);
      ctx.quadraticCurveTo(10 * s, -Ls + 11.5 * s, 12.6 * s + chest, -Ls + 6.5 * s);
      ctx.stroke();
      // the belly: the line down the middle and the three bands across it
      crease(0.75, def * 0.85);
      ctx.beginPath();
      ctx.moveTo(10.2 * s + chest * 0.6, -Ls + 12.5 * s);
      ctx.quadraticCurveTo(10.6 * s, -Ls * 0.4, 9 * s, -5 * s);
      for (const yy of [0.56, 0.42, 0.28]) {
        ctx.moveTo(7 * s, -Ls * yy);
        ctx.quadraticCurveTo(
          9.8 * s,
          -Ls * yy - 1.3 * s,
          11.8 * s + chest * 0.5,
          -Ls * yy + 0.3 * s,
        );
      }
      // serratus: the fingers of muscle over the ribs
      for (let i = 0; i < 3; i++) {
        const yy = -Ls * (0.66 - i * 0.08);
        ctx.moveTo(3.4 * s, yy);
        ctx.lineTo(6.2 * s, yy + 1.6 * s);
      }
      // the oblique into the hip
      ctx.moveTo(4.5 * s, -Ls * 0.32);
      ctx.quadraticCurveTo(8 * s, -4 * s, 8.6 * s, 0);
      ctx.stroke();
      // the spine's groove between the long back muscles, and the scapula
      crease(1, def);
      ctx.beginPath();
      ctx.moveTo(-6.4 * s, -Ls + 3 * s);
      ctx.quadraticCurveTo(-8.6 * s - flare * 0.5, -Ls * 0.5, -6.4 * s, -3 * s);
      ctx.moveTo(-2 * s, -Ls + 3 * s);
      ctx.quadraticCurveTo(-5.5 * s, -Ls + 7 * s, -3.4 * s, -Ls + 12 * s);
      ctx.stroke();
      // the light along the top of the back and over the chest
      sheen(2.2, 0.16);
      ctx.beginPath();
      ctx.moveTo(-9 * s, -Ls + 2 * s);
      ctx.quadraticCurveTo(-11 * s - flare, -Ls * 0.62, -8.4 * s, -Ls * 0.3);
      ctx.moveTo(6 * s, -Ls + 4 * s);
      ctx.quadraticCurveTo(9.5 * s, -Ls + 5.5 * s, 11.4 * s + chest, -Ls + 3 * s);
      ctx.stroke();
    });

    /**
     * The only garment: a labourer's skirt knotted at the waist and falling to
     * mid-thigh, a touch lighter than the skin so the waist reads as a break.
     * It belongs to the pelvis, so it tips with it, and its hem answers to the
     * gait, because a hem that never moves is cardboard.
     */
    ctx.fillStyle = cloth;
    const hem = (i: number) => Math.sin(this.gait * 1.0 + i) * 1.8 * s * effort;
    ctx.rotate(-theta * 0.55);
    ctx.beginPath();
    ctx.moveTo(-9.5 * s, -4 * s);
    ctx.lineTo(10 * s, -4 * s);
    ctx.quadraticCurveTo(11.5 * s, 6 * s, 9 * s, 12 * s + hem(0));
    ctx.lineTo(5 * s, 9 * s + hem(1.1));
    ctx.lineTo(1 * s, 13 * s + hem(2.2));
    ctx.lineTo(-3 * s, 9 * s + hem(3.3));
    ctx.lineTo(-8 * s, 12 * s + hem(4.4));
    ctx.quadraticCurveTo(-13 * s, 4 * s, -9.5 * s, -4 * s);
    ctx.closePath();
    ctx.fill();
    // folds in the linen, falling from the belt
    ink(() => {
      ctx.strokeStyle = "rgba(70, 50, 30, 0.32)";
      ctx.lineWidth = 0.9 * s;
      ctx.beginPath();
      for (const fx0 of [-5.5, -1, 3.5, 7.5]) {
        ctx.moveTo(fx0 * s, -2.5 * s);
        ctx.quadraticCurveTo((fx0 + 0.8) * s, 4 * s, (fx0 + (hem(fx0) / s) * 0.3) * s, 10 * s);
      }
      ctx.stroke();
    });
    // a knotted belt
    ctx.fillStyle = leather;
    ctx.fillRect(-9.5 * s, -5 * s, 19.5 * s, 2.4 * s);
    ctx.restore();

    /*
     * Arms. Every pose uses the same two bones now. The braced arm used to be
     * drawn from its shoulder straight to the stone with the elbow placed on
     * the line between them, and the running arm from two short fixed bones —
     * so the moment he launched the stone his arms visibly shrank by a third.
     * One upper arm, one forearm, the same length everywhere, solved for the
     * elbow: an arm cannot change size, only fold.
     */
    const UPPER = 14 * s;
    const FORE = 13 * s;
    const solve = (sx: number, sy: number, tx: number, ty: number) => {
      let dx = tx - sx;
      let dy = ty - sy;
      let d = Math.hypot(dx, dy) || 1;
      const reach = UPPER + FORE - 0.3 * s;
      if (d > reach) {
        tx = sx + (dx / d) * reach;
        ty = sy + (dy / d) * reach;
        dx = tx - sx;
        dy = ty - sy;
        d = reach;
      }
      const a = (UPPER * UPPER - FORE * FORE + d * d) / (2 * d);
      const hh = Math.sqrt(Math.max(0, UPPER * UPPER - a * a));
      const ux = dx / d;
      const uy = dy / d;
      // the elbow breaks downward and back, never up
      let px = -uy;
      let py = ux;
      if (py < 0) {
        px = -px;
        py = -py;
      }
      return { ex: sx + ux * a + px * hh, ey: sy + uy * a + py * hh, hx: tx, hy: ty };
    };
    /** one arm from a shoulder to a hand already solved, with its muscle bellies */
    const drawArm = (
      sx: number,
      sy: number,
      j: { ex: number; ey: number; hx: number; hy: number },
      shade: string | CanvasGradient,
      hand: "press" | "fist" | "relaxed",
    ) => {
      // upper arm: triceps behind, biceps in front, thickening under load
      seg(
        sx,
        sy,
        j.ex,
        j.ey,
        (4.8 + load * 1) * s,
        (3.5 + load * 0.5) * s,
        -(1.6 + load * 1.4) * s,
        0.55,
        shade,
      );
      // forearm: the mass sits up near the elbow, the wrist is bone
      seg(
        j.ex,
        j.ey,
        j.hx,
        j.hy,
        (3.6 + load * 0.6) * s,
        2.2 * s,
        (1.3 + load * 1) * s,
        0.3,
        shade,
      );
      ink(() => {
        // biceps and triceps parted down the upper arm; the forearm's flexors
        crease(0.75, 0.22 + load * 0.18);
        along(sx, sy, j.ex, j.ey, -0.4 * s, 0.22, 0.85, 0.6 * s);
        along(j.ex, j.ey, j.hx, j.hy, 0.6 * s, 0.08, 0.55, 0.8 * s);
        sheen(1.4, 0.16 + load * 0.06);
        along(sx, sy, j.ex, j.ey, 2.4 * s, 0.3, 0.75, 0.8 * s);
      });
      drawHand(j, shade, hand);
    };
    /**
     * Hands with fingers. A hand was an ellipse — at this size a blob that
     * could have been a mitten or a club. Pressed on the stone the fingers now
     * spread across its surface with the thumb braced along the top, the way
     * a hand actually takes weight; at a run they close into fists with the
     * knuckles showing; hanging, they curl loosely. Drawn in the forearm's own
     * frame, +x pointing out of the wrist.
     */
    const drawHand = (
      j: { ex: number; ey: number; hx: number; hy: number },
      shade: string | CanvasGradient,
      hand: "press" | "fist" | "relaxed",
    ) => {
      const finger = (
        x0: number,
        y0: number,
        a: number,
        len: number,
        wdt: number,
        curl: number,
      ) => {
        // two phalanx segments; the tip turns by `curl`
        const x1 = x0 + Math.cos(a) * len * 0.55;
        const y1 = y0 + Math.sin(a) * len * 0.55;
        const x2 = x1 + Math.cos(a + curl) * len * 0.45;
        const y2 = y1 + Math.sin(a + curl) * len * 0.45;
        ctx.lineWidth = wdt;
        ctx.beginPath();
        ctx.moveTo(x0, y0);
        ctx.lineTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();
      };
      ctx.save();
      ctx.fillStyle = shade;
      ctx.strokeStyle = shade;
      ctx.lineCap = "round";
      ctx.lineJoin = "round";
      ctx.translate(j.hx, j.hy);
      ctx.rotate(Math.atan2(j.hy - j.ey, j.hx - j.ex));
      if (hand === "press") {
        // palm flat on the stone, the heel of the hand driving
        ctx.beginPath();
        ctx.ellipse(1.6 * s, 0, 2.6 * s, 2.3 * s, 0, 0, Math.PI * 2);
        ctx.fill();
        // four fingers fanned out across the surface, tips gripping
        const spread = 0.12 + load * 0.08;
        const fl = [2.7, 3.1, 3, 2.4];
        for (let i = 0; i < 4; i++) {
          const off = (i - 1.5) * 1.15 * s;
          const a = (i - 1.5) * spread * 2.2;
          finger(3.5 * s, off, a, fl[i]! * s, 0.95 * s, 0.25 + load * 0.15);
        }
        // the thumb along the top, braced
        finger(1.6 * s, -1.9 * s, -0.95, 2.6 * s, 1.15 * s, 0.35);
      } else if (hand === "fist") {
        ctx.beginPath();
        ctx.ellipse(1 * s, 0, 2.7 * s, 2.4 * s, 0, 0, Math.PI * 2);
        ctx.fill();
        // knuckles: a row of bumps across the front of the fist
        for (let i = 0; i < 4; i++) {
          ctx.beginPath();
          ctx.arc(3.3 * s, (i - 1.5) * 1.05 * s, 0.8 * s, 0, Math.PI * 2);
          ctx.fill();
        }
        // the thumb wrapped across the fingers
        ctx.beginPath();
        ctx.ellipse(2.2 * s, -1.7 * s, 1.5 * s, 0.75 * s, 0.5, 0, Math.PI * 2);
        ctx.fill();
      } else {
        // hanging: palm, then fingers curling in toward it
        ctx.beginPath();
        ctx.ellipse(1.5 * s, 0, 2.3 * s, 1.9 * s, 0, 0, Math.PI * 2);
        ctx.fill();
        for (let i = 0; i < 4; i++) {
          finger(3.3 * s, (i - 1.5) * 0.8 * s, 0.1 + i * 0.05, 2.6 * s, 0.85 * s, 0.9);
        }
        finger(1.4 * s, -1.6 * s, -0.4, 2.2 * s, 1 * s, 0.6);
      }
      ctx.restore();
    };
    // shoulder joints, in world space, from the tilted trunk
    const shoulderAt = (along: number) => ({
      x: shX + cosT * along * s + sinT * 1 * s,
      y: shY + sinT * along * s - cosT * -1 * s,
    });
    const nearSh = shoulderAt(3);
    const farSh = shoulderAt(-1.5);

    if (left) {
      // Arms hanging, shoulders down. He has nothing to hold.
      // hanging, or swinging gently against the legs while he walks down
      const sway = (k: number) =>
        Math.sin(this.t * 0.8 + k) * 0.6 * s + Math.sin(this.gait + k * Math.PI) * walking * 5 * s;
      layer(0);
      drawArm(
        farSh.x,
        farSh.y,
        solve(farSh.x, farSh.y, farSh.x + 4 * s + sway(1), farSh.y + 26 * s),
        bodyFar,
        "relaxed",
      );
      layer(2);
      drawArm(
        nearSh.x,
        nearSh.y,
        solve(
          nearSh.x,
          nearSh.y,
          nearSh.x + (5 + flinch * 2) * s + sway(2),
          nearSh.y + (26 - flinch * 41) * s,
        ),
        body,
        "relaxed",
      );
    } else if (kicking) {
      /*
       * The launch and the run after it. For the first instant the arms are
       * still out in front where the stone left them — the follow-through of a
       * shove — and then they come down into a runner's swing: elbows near a
       * right angle, a short arc at the shoulder, opposite to the legs.
       */
      const release = Math.max(0, 1 - this.kickT / 0.35);
      const swingTo = (theta2: number) => {
        const sw = Math.sin(theta2) * effort;
        // runner: upper arm hangs and swings ±0.35 rad, forearm forward
        const upAng = Math.PI / 2 - 0.35 * sw;
        const foreAng = upAng - 1.35;
        return { upAng, foreAng };
      };
      const arm = (
        sh: { x: number; y: number },
        theta2: number,
        shade: string | CanvasGradient,
      ) => {
        const run = swingTo(theta2);
        // follow-through: pointing forward and a little up, arm nearly straight
        const up = run.upAng * (1 - release) + -0.15 * release;
        const fore = run.foreAng * (1 - release) + -0.2 * release;
        const ex = sh.x + Math.cos(up) * UPPER;
        const ey = sh.y + Math.sin(up) * UPPER;
        drawArm(
          sh.x,
          sh.y,
          { ex, ey, hx: ex + Math.cos(fore) * FORE, hy: ey + Math.sin(fore) * FORE },
          shade,
          "fist",
        );
      };
      // arms counter the legs: near leg is at `gait`, so the near arm is opposite
      layer(0);
      arm(farSh, this.gait, bodyFar);
      layer(2);
      arm(nearSh, this.gait + Math.PI, body);
    } else {
      // ---- arms braced against the boulder ----
      const press1 = { x: bcx - R * 0.87 - x + tremble, y: bcy - R * 0.5 - groundY };
      // flinching, the near hand comes off the stone and up over his face
      const shield = { x: nearSh.x + 7 * s, y: nearSh.y - 15 * s };
      const h1 = {
        x: press1.x + (shield.x - press1.x) * flinch,
        y: press1.y + (shield.y - press1.y) * flinch,
      };
      const h2 = { x: bcx - R * 0.97 - x + tremble, y: bcy - R * 0.05 - groundY };
      const j2 = solve(farSh.x, farSh.y, h2.x, h2.y);
      const j1 = solve(nearSh.x, nearSh.y, h1.x, h1.y);
      // leather wrist bracers on the forearms
      const bracer = (j: { ex: number; ey: number; hx: number; hy: number }) => {
        const bx = j.ex + (j.hx - j.ex) * 0.72;
        const by = j.ey + (j.hy - j.ey) * 0.72;
        const dx = j.hx - j.ex;
        const dy = j.hy - j.ey;
        const len = Math.hypot(dx, dy) || 1;
        const px = -dy / len;
        const py = dx / len;
        const bw = 2.7 * s;
        ctx.strokeStyle = leather;
        ctx.lineWidth = 3.2 * s;
        ctx.beginPath();
        ctx.moveTo(bx + px * bw, by + py * bw);
        ctx.lineTo(bx - px * bw, by - py * bw);
        ctx.stroke();
        ctx.strokeStyle = "oklch(0.42 0.04 55 / 0.5)";
        ctx.lineWidth = 1 * s;
        ctx.beginPath();
        ctx.moveTo(bx + px * bw, by + py * bw);
        ctx.lineTo(bx - px * bw, by - py * bw);
        ctx.stroke();
      };
      // the far arm behind everything, the near one in front of the chest
      layer(0);
      drawArm(farSh.x, farSh.y, j2, bodyFar, "press");
      bracer(j2);
      layer(2);
      drawArm(nearSh.x, nearSh.y, j1, body, flinch > 0.5 ? "relaxed" : "press");
      bracer(j1);
    }

    // ---- neck + head (profile facing the boulder) ----
    layer(1);
    // The head does not follow the spine all the way down: a man pushing keeps
    // his eyes up and forward, so the neck bends back against the lean.
    const nk = theta * 0.55;
    const hx = shX + Math.sin(nk) * 13.5 * s + 2 * s;
    // the head gives back part of the hip bob, and drops toward the stone as
    // the load comes on, because that is what a neck does under weight
    const hy = shY - Math.cos(nk) * 13.5 * s + bob * 0.3 + load * 2.4 * s + shudder * 0.3;
    // A neck is not a rectangle. It leaves the collarbones wide, narrows, and
    // runs up at an angle into the base of the skull; under load it thickens and
    // the sternocleidomastoid stands out of the side of it.
    seg(
      shX + 0.5 * s,
      shY + 1 * s,
      hx - 1.2 * s,
      hy + 5 * s,
      5.6 * s,
      (4.2 + load * 0.5) * s,
      (0.8 + load * 1.2) * s,
      0.5,
      body,
    );
    ink(() => {
      // the sternocleidomastoid, behind the ear down to the collarbone
      crease(0.8, 0.2 + load * 0.2);
      ctx.beginPath();
      ctx.moveTo(hx - 2.5 * s, hy + 4 * s);
      ctx.quadraticCurveTo(shX + 2 * s, shY - 4 * s, shX + 5 * s, shY + 1 * s);
      ctx.stroke();
    });

    // The head was a seventh of his height when it should be nearer an eighth.
    // Scaling the whole group about its own centre fixes the proportion without
    // letting the nose, hair and beard drift off the face.
    ctx.save();
    ctx.translate(hx - 1 * s, hy + 6 * s);
    ctx.rotate(headTilt);
    if (headTurn < 1) {
      const k = Math.sign(headTurn || 1) * Math.max(0.3, Math.abs(headTurn));
      ctx.scale(k, 1);
    }
    ctx.translate(-(hx - 1 * s), -(hy + 6 * s));
    ctx.translate(hx, hy);
    ctx.scale(0.78, 0.78);
    ctx.translate(-hx, -hy);

    /**
     * The skull in profile, as one path: cranium, brow ridge, the set-back of the
     * eye, the nose, the mouth shelf, the chin, and the jaw running back to the ear.
     * Two overlapping circles gave a snowman with a triangle on it, and no amount of
     * good animation survives that.
     */
    ctx.beginPath();
    ctx.moveTo(hx - 6.6 * s, hy - 1 * s); // back of the skull
    ctx.quadraticCurveTo(hx - 6.2 * s, hy - 8.4 * s, hx + 0.6 * s, hy - 8.6 * s); // crown
    ctx.quadraticCurveTo(hx + 5.6 * s, hy - 8.2 * s, hx + 6.4 * s, hy - 3.4 * s); // forehead
    ctx.quadraticCurveTo(hx + 7.4 * s, hy - 2.2 * s, hx + 6.5 * s, hy - 1.2 * s); // brow ridge
    ctx.quadraticCurveTo(hx + 6.2 * s, hy - 0.4 * s, hx + 6.9 * s, hy + 0.2 * s); // eye socket
    ctx.lineTo(hx + 10.2 * s, hy + 2.4 * s); // bridge out to the tip
    ctx.quadraticCurveTo(hx + 10.4 * s, hy + 3.4 * s, hx + 7.6 * s, hy + 3.6 * s); // nostril
    ctx.quadraticCurveTo(hx + 6.6 * s, hy + 4.4 * s, hx + 7.2 * s, hy + 5.4 * s); // upper lip
    ctx.quadraticCurveTo(hx + 7.8 * s, hy + 7.6 * s, hx + 5.2 * s, hy + 8.6 * s); // chin
    ctx.quadraticCurveTo(hx + 0.5 * s, hy + 9.4 * s, hx - 4.4 * s, hy + 5.2 * s); // jaw
    ctx.quadraticCurveTo(hx - 7 * s, hy + 2.4 * s, hx - 6.6 * s, hy - 1 * s); // back to the ear
    ctx.closePath();
    ctx.fill();

    // the face, in profile, now that he is lit
    const facing = headTurn >= 0.95;
    ink(() => {
      if (!facing) return;
      // the ear
      ctx.fillStyle = "rgba(60, 24, 14, 0.35)";
      ctx.beginPath();
      ctx.ellipse(hx - 1.6 * s, hy + 0.6 * s, 1.7 * s, 2.5 * s, 0.15, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = "rgba(255, 205, 160, 0.3)";
      ctx.lineWidth = 0.6 * s;
      ctx.beginPath();
      ctx.arc(hx - 1.6 * s, hy + 0.6 * s, 1.5 * s, -1.4, 1.6);
      ctx.stroke();
      // the eye under a heavy brow, narrowed with the effort
      ctx.fillStyle = "rgba(30, 12, 8, 0.75)";
      ctx.beginPath();
      ctx.ellipse(
        hx + 4.6 * s,
        hy - 0.5 * s,
        1.3 * s,
        (0.75 - load * 0.3) * s,
        0.1,
        0,
        Math.PI * 2,
      );
      ctx.fill();
      // the brow: knotted down at the nose in his anger, smoothed out later
      const anger = (1 - serene) * (0.5 + load * 0.5) * (1 - this.smile);
      ctx.strokeStyle = "rgba(25, 10, 6, 0.7)";
      ctx.lineWidth = 1.1 * s;
      ctx.beginPath();
      ctx.moveTo(hx + 2.8 * s, hy - (2.4 - load * 0.4 + anger * 0.3) * s);
      ctx.quadraticCurveTo(
        hx + 5 * s,
        hy - (3.2 - load * 0.7 - anger * 0.2) * s,
        hx + 6.7 * s,
        hy - (2 - anger * 0.9) * s,
      );
      ctx.stroke();
      // a glint in the eye
      ctx.fillStyle = "rgba(255, 240, 220, 0.6)";
      ctx.beginPath();
      ctx.arc(hx + 5 * s, hy - 0.7 * s, 0.3 * s, 0, Math.PI * 2);
      ctx.fill();
      // cheekbone, nostril and the line of the mouth
      ctx.strokeStyle = "rgba(255, 210, 165, 0.25)";
      ctx.lineWidth = 1.2 * s;
      ctx.beginPath();
      ctx.moveTo(hx + 2.6 * s, hy + 1.6 * s);
      ctx.quadraticCurveTo(hx + 4.6 * s, hy + 1 * s, hx + 6 * s, hy + 2 * s);
      ctx.stroke();
      ctx.strokeStyle = "rgba(40, 14, 8, 0.5)";
      ctx.lineWidth = 0.7 * s;
      ctx.beginPath();
      ctx.moveTo(hx + 8.2 * s, hy + 3 * s);
      ctx.quadraticCurveTo(hx + 7.4 * s, hy + 2.6 * s, hx + 7.2 * s, hy + 3.3 * s);
      // the mouth: a hard line, or — once he has understood — a smile
      const sm = this.smile;
      ctx.moveTo(hx + 7.4 * s, hy + (5.4 - sm * 0.2) * s);
      ctx.quadraticCurveTo(
        hx + 6.4 * s,
        hy + (5.6 + sm * 0.9) * s,
        hx + 5.3 * s,
        hy + (5.6 - sm * 1.1) * s,
      );
      ctx.stroke();
      if (sm > 0.2) {
        // the cheek lifts with it
        ctx.strokeStyle = `rgba(255, 210, 165, ${(0.3 * sm).toFixed(3)})`;
        ctx.lineWidth = 1 * s;
        ctx.beginPath();
        ctx.arc(hx + 4.6 * s, hy + 3.4 * s, 1.4 * s, 3.6, 5.6);
        ctx.stroke();
      }
    });

    // hair: a heavy mass over the crown and down the back of the neck, not a ball
    ctx.fillStyle = hairC;
    ctx.beginPath();
    ctx.moveTo(hx + 6.2 * s, hy - 4 * s);
    ctx.quadraticCurveTo(hx + 4 * s, hy - 10.6 * s, hx - 2 * s, hy - 9.6 * s);
    ctx.quadraticCurveTo(hx - 8.4 * s, hy - 8 * s, hx - 8.6 * s, hy - 0.5 * s);
    ctx.quadraticCurveTo(hx - 9 * s, hy + 5 * s, hx - 5.6 * s, hy + 6.4 * s);
    ctx.quadraticCurveTo(hx - 7.2 * s, hy + 1 * s, hx - 5.4 * s, hy - 3.4 * s);
    ctx.quadraticCurveTo(hx - 2.4 * s, hy - 7.6 * s, hx + 6.2 * s, hy - 4 * s);
    ctx.closePath();
    ctx.fill();
    // Curls. A smooth cap of hair over a smooth skull is a helmet; a man who
    // has spent eternity on a mountain has a mass of curls, and in silhouette
    // that is exactly the edge that shows.
    const curl = (cx0: number, cy0: number, r0: number) => {
      ctx.beginPath();
      ctx.arc(hx + cx0 * s, hy + cy0 * s, r0 * s, 0, Math.PI * 2);
      ctx.fill();
    };
    curl(4.6, -8.6, 1.9);
    curl(1.6, -10.4, 2.1);
    curl(-1.8, -10.6, 2.2);
    curl(-5, -9.2, 2.1);
    curl(-7.6, -6.6, 2);
    curl(-9, -3.2, 1.9);
    curl(-9.2, 0.6, 1.8);
    curl(-8.4, 4.2, 1.7);
    curl(6.6, -6.2, 1.5);
    // thick dark beard
    ctx.beginPath();
    ctx.moveTo(hx + 6.2 * s, hy + 1 * s);
    ctx.quadraticCurveTo(hx + 7 * s, hy + 8 * s, hx + 2 * s, hy + 10 * s);
    ctx.quadraticCurveTo(hx - 2 * s, hy + 10 * s, hx - 3.4 * s, hy + 5 * s);
    ctx.quadraticCurveTo(hx - 3 * s, hy + 3 * s, hx + 1 * s, hy + 4 * s);
    ctx.quadraticCurveTo(hx + 3.5 * s, hy + 4.5 * s, hx + 4.5 * s, hy + 3 * s);
    ctx.closePath();
    ctx.fill();
    curl(4.6, 9.6, 1.5);
    curl(1.6, 10.4, 1.6);
    curl(-1.6, 9.2, 1.5);
    curl(6.2, 7.4, 1.2);

    /**
     * The face is gone — brow, eye and mouth all of it. On a silhouette they were
     * grey scratches inside a dark shape, and the closer they got to being correct
     * the more they read as a smudge.
     *
     * The jaw carries it instead. It drops as he drags a breath in, which is the one
     * facial movement an outline can actually show, and at this distance it is the
     * only one a real body would give you either.
     */
    if (pushing || kicking) {
      ctx.fillStyle = hairC;
      const gape = (1.4 + winded * 1.2 + Math.max(0, breath) * winded * 1.6) * s;
      ctx.beginPath();
      ctx.moveTo(hx + 7.2 * s, hy + 5.4 * s);
      ctx.quadraticCurveTo(hx + 8 * s, hy + (7.6 + gape) * s, hx + 5.2 * s, hy + (8.6 + gape) * s);
      ctx.quadraticCurveTo(hx + 0.5 * s, hy + (9.4 + gape * 0.5) * s, hx - 4.4 * s, hy + 5.2 * s);
      ctx.quadraticCurveTo(hx + 0.5 * s, hy + 9.4 * s, hx + 5.2 * s, hy + 8.6 * s);
      ctx.quadraticCurveTo(hx + 7.8 * s, hy + 7.6 * s, hx + 7.2 * s, hy + 5.4 * s);
      ctx.closePath();
      ctx.fill();
    }

    ctx.restore(); // end of the head group's scale

    /**
     * There is no rim stroke any more, and there should never have been one.
     *
     * A rim light is the edge of a solid form catching the sun. What was drawn here
     * was a set of hand-fitted curves *approximating* where that edge might be —
     * and the body is not one shape, it is thirty overlapping fills whose true
     * outline changes every frame with the pose. The approximation could not track
     * it, so on most frames the gold curve landed a few units inside him: a yellow
     * line painted across a silhouette rather than light along its edge.
     *
     * A soft wash of sky behind him replaced it for one commit, on the theory that
     * a black figure needed help against black ridges. It did not: against a lit
     * sky he separates on his own, and the wash read as a glow clinging to him.
     * Nothing is drawn around him now, which is what a silhouette means.
     */

    /**
     * Sweat. Four drops on a staggered loop, thrown off the brow and the jaw and
     * falling away behind him — the cheapest possible cue and the one that reads
     * fastest, because nothing else on screen falls at that speed. Only when he is
     * genuinely working, and it thins out as the effort does.
     */
    layer(2);
    if (winded > 0.3) {
      ctx.fillStyle = `oklch(0.92 0.03 220 / ${(0.55 * winded).toFixed(3)})`;
      for (let i = 0; i < 4; i++) {
        // deterministic, staggered, and always falling — no per-frame randomness
        const p = (this.t * (0.9 + i * 0.17) + i * 0.41) % 1;
        const sx0 = hx + (3.4 - i * 2.6) * s;
        const sy0 = hy + (i % 2 ? 4 : -3) * s;
        ctx.globalAlpha = (1 - p) * 0.8;
        ctx.beginPath();
        ctx.ellipse(
          sx0 - p * 7 * s,
          sy0 + p * 26 * s,
          0.9 * s,
          1.7 * s + p * 1.2 * s,
          -0.25,
          0,
          Math.PI * 2,
        );
        ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    /**
     * The breath itself. Under a real load he is not silently straining; he is
     * emptying his lungs into the cold, and the plume leaves his mouth on the same
     * cycle the chest is running on.
     */
    if (winded > 0.45 && breath > 0.2) {
      const puff = breath * winded;
      ctx.fillStyle = `oklch(0.95 0.01 250 / ${(0.16 * puff).toFixed(3)})`;
      ctx.beginPath();
      ctx.ellipse(
        hx + (11 + puff * 7) * s,
        hy + 5 * s,
        (4 + puff * 5) * s,
        (2.4 + puff * 2.4) * s,
        -0.2,
        0,
        Math.PI * 2,
      );
      ctx.fill();
    }

    layer(1);
    ctx.restore();
  }
}
