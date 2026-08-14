import { GameAudio } from "./audio";
import { quotesFor, type LangCode, type Quote } from "./quotes";
import { EPIGRAPHS, LEVEL_NAMES } from "@/i18n";
import { getLevel, slopeAt, terrainAt, type Cloud, type Level } from "./levels";

export type Phase = "playing" | "summit" | "rolling" | "restart" | "done";

export type EngineState = {
  phase: Phase;
  progress: number; // 0..1
  levelName: string;
  epigraph: string;
  cycles: number;
};

const PUSH_FORCE = 620;
const GRAVITY = 900;
const FRICTION = 1.6;
const ROLL_GRAVITY = 1500;
const MAX_CYCLES = 50;
/** boulder launch velocity (units/s) when the character kicks it loose */
const KICK_V = 560;
/** character free-run speed (units/s) while the boulder is away */
const KICK_SPRINT = 220;
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
};

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

  /** pre-painted static sky (gradient + glow + disc), rebuilt only on resize / level change */
  private skyCache: HTMLCanvasElement | null = null;
  private skyCacheKey = "";
  /** distance-based walk phase, so the gait follows the feet and not the clock */
  private gait = 0;

  private zeus = { state: "gone" as "gone" | "appear" | "strike", t: 0 };
  private flash = 0;
  private strainT = 0;

  /** >= 0 while a kick is in flight; -1 when idle */
  private kickT = -1;
  /** character world position (separates from the boulder during a kick) */
  private charX = -24;

  private birds: Bird[] = [];
  private chirpT = 0;

  /** dust / spark motes kicked up around the boulder */
  private particles: Particle[] = [];

  /** the random aphorism currently written in the sky (assigned at each summit) */
  private currentQuote: Quote | null = null;
  /** index of currentQuote in the deck (so the sky text can follow a language change) */
  private currentQuoteIndex = -1;
  /** shuffled deck of quote indices, reshuffled when emptied */
  private quoteDeck: number[] = [];
  /** selected UI / quote language */
  private lang: LangCode = "tr";

  /** -1..1 input from keyboard / pointer drag */
  input = 0;

  audio = new GameAudio();
  onState: (s: EngineState) => void = () => {};

  constructor(canvas: HTMLCanvasElement, levelIndex = 0) {
    this.canvas = canvas;
    const c = canvas.getContext("2d");
    if (!c) throw new Error("Canvas 2D unavailable");
    this.ctx = c;
    this.levelIndex = levelIndex;
    this.level = getLevel(levelIndex);

    // distant bird flocks drifting across the sky
    const flock = (
      par: number,
      speed: number,
      count: number,
      yLo: number,
      yHi: number,
      size: number,
    ) => {
      for (let i = 0; i < count; i++) {
        this.birds.push({
          ox: (Math.random() - 0.5) * 700,
          fy: yLo + Math.random() * (yHi - yLo),
          par,
          speed,
          flap: Math.random() * Math.PI * 2,
          flapSpeed: 5 + Math.random() * 3,
          wander: Math.random() * Math.PI * 2,
          bob: 2 + Math.random() * 3,
          bobFreq: 0.8 + Math.random() * 1.2,
          size: size * (0.7 + Math.random() * 0.6),
        });
      }
    };
    flock(0.09, 30, 3, 0.08, 0.22, 1);
    flock(0.06, -24, 3, 0.14, 0.3, 0.8);
    flock(0.13, 40, 3, 0.05, 0.16, 1.3);
  }

  // ---------- lifecycle ----------
  start() {
    this.resize();
    this.last = performance.now();
    const loop = (t: number) => {
      const dt = Math.min(0.05, (t - this.last) / 1000);
      this.last = t;
      this.update(dt);
      this.render();
      this.raf = requestAnimationFrame(loop);
    };
    this.raf = requestAnimationFrame(loop);
  }

  stop() {
    cancelAnimationFrame(this.raf);
    this.audio.dispose();
  }

  restart() {
    this.x = 40;
    this.vx = 0;
    this.roll = 0;
    this.input = 0;
    this.phase = "playing";
    this.phaseT = 0;
    this.zeus.state = "gone";
    this.flash = 0;
    this.kickT = -1;
    this.charX = this.x - 64;
    this.cycles += 1;
    this.emit();
  }

  /** Tab: kick the boulder loose up the ramp; it rolls back while the character sprints ahead */
  kick() {
    if (this.phase !== "playing" || this.kickT >= 0) return;
    this.kickT = 0;
    this.vx = KICK_V;
    this.shake = Math.max(this.shake, 0.4);
    this.audio.kick();
  }

  /** pull a quote at random, without repeating until the deck is exhausted */
  private nextQuote(): Quote {
    const list = quotesFor(this.lang);
    if (this.quoteDeck.length === 0) {
      const deck = list.map((_, i) => i);
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
    return list[idx]!;
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
    this.dpr = Math.min(2, window.devicePixelRatio || 1);
    const r = this.canvas.getBoundingClientRect();
    this.w = r.width;
    this.h = r.height;
    this.canvas.width = Math.floor(r.width * this.dpr);
    this.canvas.height = Math.floor(r.height * this.dpr);
    this.ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
  }

  private emit() {
    const q = this.currentQuote;
    this.onState({
      phase: this.phase,
      progress: Math.max(0, Math.min(1, this.x / this.level.length)),
      levelName: LEVEL_NAMES[this.lang],
      epigraph: q ? `${q.text} — ${q.author}` : "",
      cycles: this.cycles,
    });
  }

  // ---------- simulation ----------
  private update(dt: number) {
    const L = this.level;
    this.t += dt;
    const slope = slopeAt(L, this.x);
    const prevPhase = this.phase;
    this.frameDt = dt;

    if (this.phase === "playing") {
      if (this.kickT >= 0) {
        this.kickT += dt;
        // boulder sails uphill, then gravity rolls it back down the ramp
        this.vx += (-slope * GRAVITY * L.gravityScale - this.vx * 0.12) * dt;
        this.x += this.vx * dt;
        // unburdened, the character sprints up the hill
        this.charX += KICK_SPRINT * dt;
        // the boulder rolls back to the character: resume pushing
        if (this.x <= this.charX + 64) {
          this.x = this.charX + 64;
          this.vx = 0;
          this.kickT = -1;
        }
        this.x = Math.max(0, this.x);
      } else {
        this.charX = this.x - 64;
        const push = this.input * PUSH_FORCE;
        const gravity = -slope * GRAVITY * L.gravityScale;
        const a = push + gravity - this.vx * FRICTION;
        this.vx += a * dt;
        this.x += this.vx * dt;
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
            this.audio.strain();
          }
        }
      }
      if (this.x >= L.length) {
        this.x = L.length;
        this.vx = 0;
        this.kickT = -1;
        this.currentQuote = this.nextQuote();
        if (this.cycles >= MAX_CYCLES - 1) {
          this.phase = "done";
          this.phaseT = 0;
          this.zeus.state = "appear";
          this.zeus.t = 0;
          this.audio.toll();
        } else {
          this.phase = "summit";
          this.phaseT = 0;
          this.zeus.state = "appear";
          this.zeus.t = 0;
          this.audio.toll();
        }
      }
    } else if (this.phase === "summit") {
      this.phaseT += dt;
      this.zeus.t += dt;
      this.vx *= 0.9;
      if (this.phaseT >= 1.4 && this.zeus.state === "appear") {
        this.zeus.state = "strike";
        this.flash = 0.55;
        this.shake = Math.max(this.shake, 0.8);
        this.audio.thunder();
      }
      if (this.phaseT > 2.1) {
        this.phase = "rolling";
        this.phaseT = 0;
        this.vx = -260;
        this.zeus.state = "gone";
        this.flash = 0.18;
      }
    } else if (this.phase === "rolling") {
      this.phaseT += dt;
      this.vx += -slope * ROLL_GRAVITY * dt - this.vx * 0.25 * dt;
      this.vx = Math.max(this.vx, -1400);
      this.x += this.vx * dt;
      if (this.x <= 0) {
        this.x = 0;
        if (this.phase === "rolling") this.audio.impact();
        this.shake = 1;
        this.vx = 0;
        this.phase = "restart";
        this.phaseT = 0;
      }
    } else {
      this.phaseT += dt;
    }

    this.roll += (this.vx * dt) / 46;

    // The gait follows the feet, not the clock and not the boulder. Driving it off
    // `roll` meant the legs blurred at thirty cycles a second while the stone
    // thundered back down the hill; capping the cadence keeps him human.
    const charSpeed = this.kickT >= 0 ? KICK_SPRINT : Math.abs(this.vx);
    this.gait += Math.min(charSpeed / STRIDE_LENGTH, MAX_CADENCE) * Math.PI * 2 * dt;

    this.shake = Math.max(0, this.shake - dt * 2.2);
    this.flash = Math.max(0, this.flash - dt * 1.6);

    this.updateBirds(dt);
    this.updateParticles(dt);
    this.emitParticles();

    this.audio.updateFriction(Math.min(1, Math.abs(this.vx) / 420), this.phase === "rolling");
    if (prevPhase !== this.phase) this.emit();
    else if (this.phase === "playing") this.emit();
  }

  private updateBirds(dt: number) {
    const w = this.w;
    for (const b of this.birds) {
      b.ox += b.speed * dt;
      b.flap += dt * b.flapSpeed;
      b.wander += dt * b.bobFreq;
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
    for (const p of this.particles) {
      p.life += dt;
      p.wx += p.vx * dt;
      p.wy += p.vy * dt;
      p.vy += 40 * dt; // gentle gravity settles the dust
      p.vx *= 1 - dt * 1.5;
    }
    this.particles = this.particles.filter((p) => p.life < p.maxLife);
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
    const ctx = this.ctx;
    const L = this.level;
    const { w, h } = this;
    ctx.clearRect(0, 0, w, h);

    this.scale = Math.min(1, w / 900) * 0.92 + 0.28;
    this.groundY = h * 0.78;
    this.shakeY = this.shake * (Math.random() - 0.5) * 10;

    // camera keeps the action (boulder or kick midpoint) slightly left of centre,
    // smoothed so the rejoin of character and boulder doesn't jump
    const camTargetX = this.kickT >= 0 ? (this.x + this.charX) / 2 : this.x;
    const desiredCamX = camTargetX - (w * 0.38) / this.scale;
    this.camX += (desiredCamX - this.camX) * Math.min(1, this.frameDt * 12);
    this.camY = terrainAt(L, this.camX);

    const toScreenX = (wx: number) => (wx - this.camX) * this.scale;
    const toScreenY = (wy: number) =>
      this.groundY - (wy - this.camY) * this.scale * 0.55 + this.shakeY;

    this.drawSky();
    this.drawClouds();
    this.drawMountain();
    this.renderBirds();
    this.drawMist();

    // Parallax ridges. These are landforms in front of the horizon, so they have
    // to stay under it: at RIDGE_RELIEF 0.4 the nearest rank sampled terrain
    // 2100 units ahead and crested at 0.35h, two hundred pixels above the sun,
    // burying it for most of the climb. No amount of recolouring fixes a mass
    // that is simply standing in front of the light.
    const horizonY = this.sunScreen().y;
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

    this.drawAphorism();

    // main terrain
    ctx.beginPath();
    ctx.moveTo(-2, h + 2);
    for (let sx = -2; sx <= w + 2; sx += 4) {
      const wx = this.camX + sx / this.scale;
      ctx.lineTo(sx, toScreenY(terrainAt(L, wx)));
    }
    ctx.lineTo(w + 2, h + 2);
    ctx.closePath();
    const groundG = ctx.createLinearGradient(0, this.groundY - 120, 0, h);
    groundG.addColorStop(0, "oklch(0.36 0.05 54)");
    groundG.addColorStop(0.45, "oklch(0.3 0.045 50)");
    groundG.addColorStop(1, "oklch(0.22 0.035 44)");
    ctx.fillStyle = groundG;
    ctx.fill();

    // lighter dirt trail worn by the endless rolling boulder
    ctx.strokeStyle = "oklch(0.4 0.05 58 / 0.35)";
    ctx.lineWidth = 24 * this.scale;
    ctx.lineCap = "round";
    ctx.beginPath();
    for (let sx = -2; sx <= w + 2; sx += 4) {
      const wx = this.camX + sx / this.scale;
      const y = toScreenY(terrainAt(L, wx));
      if (sx === -2) ctx.moveTo(sx, y - 1);
      else ctx.lineTo(sx, y - 1);
    }
    ctx.stroke();

    // sunlit ridgeline
    ctx.strokeStyle = "oklch(0.66 0.06 62 / 0.7)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    for (let sx = -2; sx <= w + 2; sx += 4) {
      const wx = this.camX + sx / this.scale;
      const y = toScreenY(terrainAt(L, wx));
      if (sx === -2) ctx.moveTo(sx, y);
      else ctx.lineTo(sx, y);
    }
    ctx.stroke();

    // sparse scrub / grass tufts breaking the silhouette
    for (let wx = Math.floor(this.camX / 22) * 22; wx < this.camX + w / this.scale; wx += 22) {
      const h1 = this.hash(wx * 1.31);
      if (h1 < 0.42) continue;
      const gx = toScreenX(wx + (h1 - 0.5) * 22);
      const gy = toScreenY(terrainAt(L, wx));
      const tu = (2 + h1 * 3.4) * this.scale;
      ctx.strokeStyle = h1 < 0.72 ? "oklch(0.45 0.05 130 / 0.6)" : "oklch(0.5 0.06 60 / 0.6)";
      ctx.lineWidth = 1.2 * this.scale;
      ctx.beginPath();
      ctx.moveTo(gx, gy);
      ctx.lineTo(gx - tu * 0.5, gy - tu * 2.2);
      ctx.moveTo(gx, gy);
      ctx.lineTo(gx + tu * 0.4, gy - tu * 1.9);
      ctx.moveTo(gx, gy);
      ctx.lineTo(gx + tu * 0.1, gy - tu * 2.6);
      ctx.stroke();
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

    // boulder + figure
    const bx = toScreenX(this.x);
    const by = toScreenY(terrainAt(L, this.x));
    const R = 34 * this.scale;
    const ang = slopeAt(L, this.x) * 0.5;

    this.drawContactShadow(bx, by, R, ang);
    this.drawBoulder(bx, by - R, R, ang);

    // figure stands downhill of the boulder, feet on the real terrain
    const fwx = this.kickT >= 0 ? this.charX : this.x - 64;
    const fxs = toScreenX(fwx);
    const fys = toScreenY(terrainAt(L, fwx));
    this.drawFigure(fxs, fys, this.scale, ang, bx, by - R, R, this.kickT >= 0);
    this.renderZeus(toScreenX, toScreenY);
    this.renderParticles();

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
  }

  /** the sun in screen space: the one light every other draw call is lit by */
  private sunScreen(): { x: number; y: number } {
    const sun = this.level.sun ?? DEFAULT_SUN;
    return { x: this.w * sun.xFrac, y: this.h * sun.yFrac };
  }

  /**
   * The sky's gradient, glow and disc never change between frames — only the
   * viewport and the level do. Paint them once into an offscreen canvas and blit
   * that instead of rebuilding three full-screen gradients sixty times a second.
   */
  private skyLayer(): HTMLCanvasElement | null {
    const { w, h } = this;
    const { x: sunX, y: sunY } = this.sunScreen();
    const key = `${w}x${h}:${sunX.toFixed(1)},${sunY.toFixed(1)}:${this.level.id}`;
    if (this.skyCacheKey === key && this.skyCache) return this.skyCache;

    const cv = this.skyCache ?? document.createElement("canvas");
    cv.width = Math.max(1, Math.ceil(w));
    cv.height = Math.max(1, Math.ceil(h));
    const c = cv.getContext("2d");
    if (!c) return null;
    c.clearRect(0, 0, w, h);

    // the level owns the palette; the engine only decides how it is painted
    const sky = c.createLinearGradient(0, 0, 0, h);
    for (const [stop, color] of this.level.sky) sky.addColorStop(stop, color);
    c.fillStyle = sky;
    c.fillRect(0, 0, w, h);

    // Everything the sun used to throw across the sky is gone: the wide glow,
    // the atmosphere band, the waterline pool and the anamorphic flare. The pool
    // and the flare were the visible seams — both were drawn with a *horizontal*
    // gradient into a fillRect, so they faded out sideways but had hard cut top
    // and bottom edges, which is precisely how you get a ruled line across the
    // frame. What is left is the disc and the sky it sits in.
    //
    // The disc used to be clipped at sunY to fake a half-set sun. That worked
    // only while a band of horizon haze sat on the same line to cut it against.
    // With the band gone and the ridges pushed below it, the clip was slicing a
    // circle in half against nothing at all — a bite taken out of open sky. A
    // horizon has to be something you can see, so either the ridges cut the sun
    // or nothing does. The disc is whole now and sits above them.
    //
    // It had also been given its own falloff — filled out to 1.35x its radius,
    // going
    // transparent from 0.78 onward — which meant a fifth of what you saw was
    // gradient rather than sun. That is what made it look smeared. A low sun has
    // an edge; haze reddens it and flattens it, but you can still see where it
    // stops. So the fill runs opaque to the rim and only the last three percent
    // softens, which is anti-aliasing rather than glow.
    const sunR = 40 * this.scale;
    const disc = c.createRadialGradient(sunX, sunY, sunR * 0.05, sunX, sunY, sunR);
    disc.addColorStop(0, "oklch(0.99 0.03 86)");
    disc.addColorStop(0.6, "oklch(0.97 0.05 80)");
    disc.addColorStop(0.9, "oklch(0.93 0.085 74)");
    disc.addColorStop(0.97, "oklch(0.9 0.105 68)");
    disc.addColorStop(1, "oklch(0.89 0.105 66 / 0.82)");
    c.fillStyle = disc;
    c.beginPath();
    c.arc(sunX, sunY, sunR, 0, Math.PI * 2);
    c.fill();

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
  private drawSky() {
    const ctx = this.ctx;
    const { w, h } = this;
    const cached = this.skyLayer();
    if (cached) ctx.drawImage(cached, 0, 0, w, h);
  }

  /** the current cycle's aphorism, written into the sky to the left of the sun */
  private drawAphorism() {
    if (this.phase === "done") return;
    // no aphorism in the background until the first cycle is completed
    if (this.phase === "playing" && this.cycles === 0) return;
    const q = this.currentQuote;
    if (!q) return;
    const ctx = this.ctx;
    const { w, h } = this;
    const sun = this.level.sun ?? DEFAULT_SUN;
    const sunX = w * sun.xFrac;
    const sunY = h * sun.yFrac;

    const cx = sunX - w * 0.3;
    // The mountain is drawn over this, so keep the text in open sky however low
    // the level hangs its sun. Sitting a little lower than before, too: dark
    // lettering needs the brighter part of the ramp under it, and the sky gains
    // about four hundredths of lightness over that distance.
    const cy = Math.min(sunY, h * 0.4);
    const maxW = w * 0.42;
    const fs = Math.max(30, Math.min(52, w * 0.06));
    const lineH = Math.round(fs * 1.35);

    ctx.font = `italic ${fs}px Georgia, 'Times New Roman', serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";

    // word wrap; scripts without spaces (CJK) wrap per character
    const spaced = q.text.includes(" ");
    const words = spaced ? q.text.split(" ") : Array.from(q.text);
    const lines: string[] = [];
    let cur = "";
    for (const word of words) {
      const test = cur ? `${cur}${spaced ? " " : ""}${word}` : word;
      if (cur && ctx.measureText(test).width > maxW) {
        lines.push(cur);
        cur = word;
      } else {
        cur = test;
      }
    }
    if (cur) lines.push(cur);

    // Pale text on a pale sky is why this could not be read: nothing in this
    // frame is darker than L 0.6, so the only value left to write with is a dark
    // Ink, not light: held translucent so it still reads as written into the sky
    // rather than printed onto it, and haloed rather than drop-shadowed, because
    // once the letters are the dark thing the separation has to come from
    // something brighter than they are. Dropping the block to 0.4h buys about
    // four hundredths of sky lightness to sit against, which is most of what
    // makes dark lettering work on a ramp this low.
    ctx.shadowColor = "oklch(0.9 0.06 76 / 0.5)";
    ctx.shadowBlur = 9;
    ctx.fillStyle = "oklch(0.15 0.035 40 / 0.88)";
    const startY = cy - ((lines.length - 1) * lineH) / 2;
    lines.forEach((line, i) => ctx.fillText(line, cx, startY + i * lineH));

    ctx.font = `italic ${Math.round(fs * 0.72)}px Georgia, 'Times New Roman', serif`;
    ctx.fillStyle = "oklch(0.21 0.04 42 / 0.75)";
    ctx.fillText(q.author, cx, startY + lines.length * lineH + lineH * 0.4);
    ctx.shadowBlur = 0;
  }

  /** three parallax layers of cumulus drifting above the sun, lit from beneath */
  private drawClouds() {
    const ctx = this.ctx;
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
    const wrap = w + 480;
    let seed = 0;
    for (const L of layers) {
      for (let i = 0; i < L.count; i++) {
        const k = seed++;
        const off = this.hash(k * 5.3) * (w + 600) - 300;
        const x = ((((off - this.camX * L.parallax + t * L.speed) % wrap) + wrap) % wrap) - 240;
        const y = h * (L.yTop + this.hash(k * 7.7) * L.ySpan) + Math.sin(t * 0.09 + k * 2.1) * 4;
        const cw = w * (L.minW + this.hash(k * 3.1) * (L.maxW - L.minW));
        this.drawSoftCloud(ctx, x, y, cw, L.alpha, k, L.depth);
      }
    }
    this.drawHorizonStreaks();
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
    ctx.save();
    for (let i = 0; i < 5; i++) {
      const sy = sunY - 132 * s + i * 27 * s + Math.sin(t * 0.05 + i * 1.3) * 4;
      const len = (150 + this.hash(i * 9.1) * 210) * s;
      const thick = (1.4 + this.hash(i * 4.3) * 2.1) * s;
      // the bands nearest the disc catch the most light
      const heat = Math.max(0.2, 1 - Math.abs(sy - sunY) / (190 * s));
      ctx.fillStyle = `oklch(0.94 0.05 76 / ${(0.2 * heat).toFixed(3)})`;
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

  /**
   * One cumulus at golden hour: crown in shadow, underside molten where it faces
   * the sun. `depth` is how near the rank is — distance both simplifies a cloud's
   * silhouette and drains its contrast until it is barely a tint on the sky, and
   * that falloff is what gives the sky its sense of going somewhere.
   */
  private drawSoftCloud(
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    cw: number,
    alpha: number,
    seed: number,
    depth: number,
  ) {
    // stable per-cloud lobes so the shape never shimmers while drifting; the
    // nearer ranks get more of them, since only up close do you read the detail
    const lobes: Array<{ dx: number; dy: number; r: number }> = [];
    const n = 5 + Math.round(depth * 4);
    for (let i = 0; i < n; i++) {
      const r = (0.24 + this.hash(seed * 13.7 + i * 7.31) * 0.3) * cw;
      const dx = (this.hash(seed * 3.31 + i * 11.9) - 0.5) * cw * 1.2;
      const dy = (this.hash(seed * 5.91 + i * 3.7) - 0.5) * r * 1.4;
      lobes.push({ dx, dy, r });
    }

    // the sun is low and to one side: clouds above it burn on the underside,
    // and the further a cloud drifts from that column the cooler it stays
    const lit = Math.max(0.12, 1 - Math.abs(x - this.sunScreen().x) / (this.w * 0.8));
    // Haze eats contrast with distance, and lifts the shadows toward the sky.
    //
    // The crowns used to sit at 0.46-0.63 while the sky behind them at that
    // altitude runs 0.22-0.47 — so a cloud was lighter than the sky it hung in,
    // which is the wrong way round for cloud lit from underneath and why these
    // read as pale smudges rather than as shapes. The top of a backlit cloud is
    // the part in shadow. It goes darker than its sky, and the fire stays on the
    // underside where the light actually is.
    const contrast = 0.42 + depth * 0.58;
    const shadowL = (0.28 - depth * 0.06).toFixed(3);
    const shadowC = (0.03 + depth * 0.035).toFixed(3);

    const crown = y - cw * 0.12;
    const belly = y + cw * 0.1;
    ctx.save();

    // shadowed crown: cool violet mass that reads as cloud against a burning sky
    const mass = ctx.createRadialGradient(x, crown, cw * 0.08, x, crown, cw * 0.62);
    mass.addColorStop(0, `oklch(${shadowL} ${shadowC} 292 / ${(alpha * 0.9).toFixed(3)})`);
    mass.addColorStop(0.6, `oklch(${shadowL} ${shadowC} 320 / ${(alpha * 0.45).toFixed(3)})`);
    mass.addColorStop(1, `oklch(${shadowL} ${shadowC} 335 / 0)`);
    ctx.fillStyle = mass;
    ctx.beginPath();
    ctx.arc(x, crown, cw * 0.62, 0, Math.PI * 2);
    ctx.fill();

    // dusty rose midtone where the shadow rolls into the light
    const midA = (alpha * 0.62 * contrast).toFixed(3);
    const mid = ctx.createRadialGradient(x, y + cw * 0.02, cw * 0.06, x, y + cw * 0.02, cw * 0.56);
    mid.addColorStop(0, `oklch(0.7 ${(0.07 * contrast).toFixed(3)} 28 / ${midA})`);
    mid.addColorStop(1, "oklch(0.7 0.07 28 / 0)");
    ctx.fillStyle = mid;
    ctx.beginPath();
    ctx.arc(x, y + cw * 0.02, cw * 0.56, 0, Math.PI * 2);
    ctx.fill();

    // molten underside: the lobes hanging lowest catch the most light
    const fire = alpha * lit * contrast;
    for (const { dx, dy, r } of lobes) {
      const g = ctx.createRadialGradient(
        x + dx,
        belly + dy + r * 0.2,
        r * 0.1,
        x + dx,
        belly + dy,
        r,
      );
      g.addColorStop(0, `oklch(0.97 0.09 82 / ${fire.toFixed(3)})`);
      g.addColorStop(0.5, `oklch(0.88 0.12 66 / ${(fire * 0.55).toFixed(3)})`);
      g.addColorStop(1, "oklch(0.8 0.12 52 / 0)");
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.arc(x + dx, belly + dy, r, 0, Math.PI * 2);
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
    const p2x = mt.x + mt.width * 0.22;
    const p2y = baseY + mt.height * 0.66;

    // one continuous, gently undulating ridge (subtle bumps, no harsh spikes)
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

    // the crest (peak ridge) spans pts[27..62]
    const c0 = 27;
    const c1 = 62;
    const tracePts = (a: Array<[number, number]>) => {
      for (const [wx, wy] of a) ctx.lineTo(farX(wx), farY(wy));
    };

    // body fill, darker toward the base
    ctx.beginPath();
    ctx.moveTo(farX(lx), h + 2);
    ctx.lineTo(farX(lx), farY(baseY));
    tracePts(pts);
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
    tracePts(pts.slice(c0, c1 + 1));
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

  private drawBoulder(bx: number, by: number, R: number, ang: number) {
    const ctx = this.ctx;
    const s = this.scale;

    // irregular rocky outline (local space, rotates with the stone)
    const N = 26;
    const pts: Array<[number, number]> = [];
    for (let i = 0; i < N; i++) {
      const a = (i / N) * Math.PI * 2;
      const n =
        Math.sin(a * 3 + 1.3) * 0.05 +
        Math.sin(a * 7 + 4.2) * 0.032 +
        Math.sin(a * 13 + 0.7) * 0.018;
      const r = R * (1 + n);
      pts.push([Math.cos(a) * r, Math.sin(a) * r]);
    }
    const outline = () => {
      ctx.moveTo(pts[0]![0], pts[0]![1]);
      for (let i = 1; i < N; i++) ctx.lineTo(pts[i]![0], pts[i]![1]);
      ctx.closePath();
    };

    const rot = ang + this.roll;

    // Everything below is drawn in the stone's own turning frame, so the cracks
    // and facets ride around with it. The light must not: a rolling rock keeps
    // its lit face toward the sun. Rotate the light vectors backwards by the same
    // amount and the highlight stays pinned while the surface spins underneath.
    const cr = Math.cos(rot);
    const sr = Math.sin(rot);
    const toLocal = (vx: number, vy: number): [number, number] => [
      vx * cr + vy * sr,
      -vx * sr + vy * cr,
    ];
    const sun = this.sunScreen();
    const sdx = sun.x - bx;
    const sdy = sun.y - by;
    const slen = Math.hypot(sdx, sdy) || 1;
    const [lx, ly] = toLocal(sdx / slen, sdy / slen);
    const [dnx, dny] = toLocal(0, 1);

    ctx.save();
    ctx.translate(bx, by);
    ctx.rotate(rot);

    // base rock fill: lit limb toward the sun, falling off to the far side
    const g = ctx.createLinearGradient(lx * R, ly * R, -lx * R * 0.95, -ly * R * 0.95);
    g.addColorStop(0, "oklch(0.72 0.045 68)");
    g.addColorStop(0.45, "oklch(0.5 0.03 72)");
    g.addColorStop(1, "oklch(0.3 0.022 74)");
    ctx.fillStyle = g;
    ctx.beginPath();
    outline();
    ctx.fill();

    ctx.save();
    ctx.beginPath();
    outline();
    ctx.clip();

    // warm bounce gathering on the sun-facing shoulder
    const soft = ctx.createRadialGradient(lx * R * 0.5, ly * R * 0.5, R * 0.1, 0, 0, R * 1.15);
    soft.addColorStop(0, "oklch(0.82 0.06 62 / 0.55)");
    soft.addColorStop(0.45, "oklch(0.62 0.045 66 / 0.22)");
    soft.addColorStop(0.85, "oklch(0.26 0.025 70 / 0)");
    ctx.fillStyle = soft;
    ctx.fillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

    // a sun this low grazes the stone: a hard bright edge on the limb facing it
    const rimG = ctx.createRadialGradient(lx * R * 1.15, ly * R * 1.15, R * 0.05, 0, 0, R * 1.3);
    rimG.addColorStop(0, "oklch(0.97 0.1 74 / 0.85)");
    rimG.addColorStop(0.28, "oklch(0.9 0.11 68 / 0.3)");
    rimG.addColorStop(0.6, "oklch(0.85 0.1 64 / 0)");
    ctx.fillStyle = rimG;
    ctx.fillRect(-R * 1.3, -R * 1.3, R * 2.6, R * 2.6);

    // ground ambient occlusion where it meets the dirt (stays down, never spins)
    const ao = ctx.createRadialGradient(
      dnx * R * 0.55,
      dny * R * 0.55,
      R * 0.05,
      dnx * R * 0.55,
      dny * R * 0.55,
      R * 0.75,
    );
    ao.addColorStop(0, "rgba(0,0,0,0.32)");
    ao.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = ao;
    ctx.fillRect(-R, -R, R * 2, R * 2);

    // facets: flat planes catching light
    const facet = (aa: number[], rr: number[], cc: string) => {
      ctx.fillStyle = cc;
      ctx.beginPath();
      for (let k = 0; k < aa.length; k++) {
        const x = Math.cos(aa[k]!) * rr[k]! * R;
        const y = Math.sin(aa[k]!) * rr[k]! * R;
        if (k === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      }
      ctx.closePath();
      ctx.fill();
    };
    facet([-2.1, -1.55, -1.0, -1.7], [0.32, 0.78, 0.62, 0.36], "oklch(0.78 0.03 58 / 0.4)");
    facet([-0.55, 0.05, 0.55, -0.25], [0.4, 0.8, 0.6, 0.34], "oklch(0.6 0.03 78 / 0.3)");
    facet([1.7, 2.2, 2.9, 2.15], [0.3, 0.72, 0.55, 0.28], "oklch(0.2 0.02 80 / 0.45)");
    facet([-3.0, -2.5, -2.1, -2.6], [0.4, 0.7, 0.5, 0.3], "oklch(0.66 0.02 68 / 0.35)");

    // cracks: dark jagged lines
    const crack = (cr: Array<[number, number]>, w: number) => {
      ctx.strokeStyle = "oklch(0.16 0.015 75 / 0.75)";
      ctx.lineWidth = w;
      ctx.lineCap = "round";
      ctx.lineJoin = "round";
      ctx.beginPath();
      cr.forEach(([px, py], k) => {
        if (k === 0) ctx.moveTo(px * R, py * R);
        else ctx.lineTo(px * R, py * R);
      });
      ctx.stroke();
    };
    crack(
      [
        [-0.12, 0.5],
        [-0.02, 0.32],
        [0.06, 0.28],
        [0.18, 0.1],
        [0.12, -0.05],
        [0.3, -0.2],
      ],
      2.2 * s,
    );
    crack(
      [
        [-0.02, 0.32],
        [-0.22, 0.14],
        [-0.32, -0.02],
        [-0.2, -0.18],
      ],
      1.5 * s,
    );

    // pockmarks / tiny pits
    ctx.fillStyle = "oklch(0.22 0.015 75 / 0.5)";
    for (let i = 0; i < 14; i++) {
      const a = i * 2.4;
      const r = R * (0.2 + ((i * 7) % 5) * 0.14);
      ctx.beginPath();
      ctx.arc(Math.cos(a) * r, Math.sin(a) * r, R * (0.03 + (i % 3) * 0.02), 0, Math.PI * 2);
      ctx.fill();
    }
    // bright mineral specks
    ctx.fillStyle = "oklch(0.85 0.03 60 / 0.6)";
    for (let i = 0; i < 5; i++) {
      const a = -1.8 + i * 0.5;
      const r = R * (0.45 + (i % 2) * 0.2);
      ctx.beginPath();
      ctx.arc(Math.cos(a) * r, Math.sin(a) * r, R * 0.025, 0, Math.PI * 2);
      ctx.fill();
    }

    // weathered dust settling at the base
    const dust = ctx.createRadialGradient(0, R * 0.55, R * 0.08, 0, R * 0.55, R * 0.85);
    dust.addColorStop(0, "oklch(0.16 0.02 60 / 0.5)");
    dust.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = dust;
    ctx.fillRect(-R, -R, R * 2, R * 2);

    // lichen patches (muted mossy green)
    const lichenBlob = (lx: number, ly: number, lr: number) => {
      ctx.fillStyle = "oklch(0.5 0.07 130 / 0.42)";
      ctx.beginPath();
      ctx.arc(lx * R, ly * R, lr * R, 0, Math.PI * 2);
      ctx.arc((lx + lr * 0.6) * R, (ly - lr * 0.4) * R, lr * R * 0.8, 0, Math.PI * 2);
      ctx.arc((lx - lr * 0.5) * R, (ly + lr * 0.5) * R, lr * R * 0.7, 0, Math.PI * 2);
      ctx.fill();
    };
    lichenBlob(0.18, -0.3, 0.34);
    lichenBlob(-0.45, 0.25, 0.4);
    lichenBlob(0.55, 0.42, 0.22);
    // darker moss speckles
    ctx.fillStyle = "oklch(0.32 0.05 135 / 0.5)";
    for (let i = 0; i < 8; i++) {
      const a = 1.2 + i * 0.9;
      const r = R * (0.25 + (i % 4) * 0.18);
      ctx.beginPath();
      ctx.arc(Math.cos(a) * r, Math.sin(a) * r, R * 0.035, 0, Math.PI * 2);
      ctx.fill();
    }

    // warm rim light from the setting sun (upper-left)
    ctx.strokeStyle = "oklch(0.85 0.1 60 / 0.55)";
    ctx.lineWidth = 2.6 * s;
    ctx.beginPath();
    ctx.arc(0, 0, R * 0.96, -Math.PI * 0.85, -Math.PI * 0.08);
    ctx.stroke();

    ctx.restore();
    ctx.restore();
  }

  private renderBirds() {
    const ctx = this.ctx;
    const w = this.w;
    const h = this.h;
    for (const b of this.birds) {
      const px = w * 0.38 + b.ox * this.scale * b.par;
      if (px < -80 || px > w + 80) continue;
      const py = b.fy * h + Math.sin(b.wander) * b.bob;
      const s = b.size * this.scale;
      const flap = Math.sin(b.flap);
      ctx.save();
      ctx.translate(px, py);
      // against a burning sky a bird is a hard silhouette, not a soft grey smudge
      ctx.strokeStyle = "oklch(0.16 0.03 40 / 0.92)";
      ctx.lineWidth = Math.max(1, 1.1 * s);
      ctx.lineCap = "round";
      ctx.beginPath();
      ctx.moveTo(-4.5 * s, 0);
      ctx.quadraticCurveTo(-1.5 * s, -5 * s * flap, 1.2 * s, 0);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(4.5 * s, 0);
      ctx.quadraticCurveTo(1.5 * s, -5 * s * flap, -1.2 * s, 0);
      ctx.stroke();
      ctx.restore();
    }
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
    for (const L of layers) {
      ctx.save();
      ctx.globalAlpha = L.a;
      // vertical falloff keeps the bank edges soft and airy
      const grad = ctx.createLinearGradient(0, L.y - L.hh, 0, L.y + L.hh * 2);
      // valley mist takes the colour of the light falling into it
      grad.addColorStop(0, "rgba(236,206,178,0)");
      grad.addColorStop(0.5, "rgba(236,206,178,1)");
      grad.addColorStop(1, "rgba(236,206,178,0)");
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

  private drawOliveTrees(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const ctx = this.ctx;
    const s = this.scale;
    const trees = this.level.trees ?? [];
    for (const [wx, sc] of trees) {
      const px = toScreenX(wx);
      if (px < -180 || px > this.w + 180) continue;
      const py = toScreenY(terrainAt(this.level, wx));
      const hh = 34 * sc * s;
      ctx.save();
      ctx.translate(px, py);
      // gnarled, twisted trunk
      ctx.strokeStyle = "oklch(0.16 0.02 60)";
      ctx.lineCap = "round";
      ctx.lineWidth = 4.5 * sc * s;
      ctx.beginPath();
      ctx.moveTo(0, 0);
      ctx.quadraticCurveTo(-2 * sc * s, -hh * 0.45, 2 * sc * s, -hh * 0.75);
      ctx.quadraticCurveTo(4 * sc * s, -hh * 0.95, 6 * sc * s, -hh);
      ctx.stroke();
      // branches
      ctx.lineWidth = 2.6 * sc * s;
      ctx.beginPath();
      ctx.moveTo(2 * sc * s, -hh * 0.75);
      ctx.quadraticCurveTo(-6 * sc * s, -hh * 0.9, -10 * sc * s, -hh * 1.05);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(4 * sc * s, -hh * 0.9);
      ctx.quadraticCurveTo(10 * sc * s, -hh * 0.95, 12 * sc * s, -hh * 1.1);
      ctx.stroke();
      // irregular olive canopy
      ctx.fillStyle = "oklch(0.14 0.025 145 / 0.85)";
      ctx.beginPath();
      ctx.arc(6 * sc * s, -hh * 1.05, 9 * sc * s, 0, Math.PI * 2);
      ctx.arc(-10 * sc * s, -hh * 1.02, 7 * sc * s, 0, Math.PI * 2);
      ctx.arc(1 * sc * s, -hh * 1.18, 8 * sc * s, 0, Math.PI * 2);
      ctx.fill();
      // golden rim on the sunlit side
      ctx.strokeStyle = "oklch(0.75 0.08 70 / 0.35)";
      ctx.lineWidth = 1.2 * sc * s;
      ctx.beginPath();
      ctx.arc(6 * sc * s, -hh * 1.05, 9 * sc * s, Math.PI * 0.9, Math.PI * 1.8);
      ctx.stroke();
      ctx.restore();
    }
  }

  private renderZeus(toScreenX: (wx: number) => number, toScreenY: (wy: number) => number) {
    const ctx = this.ctx;
    const st = this.zeus.state;
    if (st === "gone") return;
    const L = this.level;
    const zx = toScreenX(L.length + 44);
    const zy = toScreenY(terrainAt(L, L.length + 44));
    const s = this.scale;
    const appear = st === "appear" ? Math.min(1, this.zeus.t / 0.8) : 1;

    // divine aura, additive so it burns through whatever it stands against
    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    // With the sun's glare stripped out of the sky, this was suddenly the widest
    // soft glow in the frame — the exact thing that was reading as blur. Tight
    // and faint now: enough to say he is lit from within, not a second sun.
    ctx.globalAlpha = 0.28 * appear;
    const aura = ctx.createRadialGradient(zx, zy - 80 * s, 0, zx, zy - 80 * s, 92 * s);
    aura.addColorStop(0, "oklch(0.95 0.06 88 / 0.34)");
    aura.addColorStop(0.5, "oklch(0.88 0.1 74 / 0.1)");
    aura.addColorStop(1, "oklch(0.84 0.1 68 / 0)");
    ctx.fillStyle = aura;
    ctx.fillRect(zx - 170 * s, zy - 240 * s, 340 * s, 300 * s);
    ctx.restore();

    ctx.save();
    // he does not stand, he hangs: a slow drift keeps him from reading as a statue
    ctx.translate(zx, zy + Math.sin(this.t * 0.6) * 2.5 * s);
    ctx.globalAlpha = appear;
    // lit by the same low sun as everything else — the shadow side goes violet
    // with the sky rather than staying a neutral grey
    const fold = "oklch(0.3 0.04 296 / 0.42)";
    const gold = "oklch(0.72 0.12 82)";
    const sway = Math.sin(this.t * 1.2) * 3 * s;

    // The robe was a pale panel and a dark panel meeting at a straight seam down
    // his front, which is what a paper cutout looks like, not a body. One fill
    // with the light turning across it instead — the sun is off to his right, so
    // that is the side that catches, and the shadow side goes violet with the sky
    // rather than to neutral grey. Values sit where the rest of the scene sits
    // now; at 0.9 he was brighter than anything but the disc.
    const robeG = ctx.createLinearGradient(-32 * s, 0, 34 * s, 0);
    robeG.addColorStop(0, "oklch(0.34 0.045 292)");
    robeG.addColorStop(0.3, "oklch(0.46 0.04 300)");
    robeG.addColorStop(0.58, "oklch(0.63 0.035 320)");
    robeG.addColorStop(0.82, "oklch(0.75 0.05 82)");
    robeG.addColorStop(1, "oklch(0.71 0.06 76)");
    ctx.fillStyle = robeG;
    ctx.beginPath();
    ctx.moveTo(-14 * s, -95 * s);
    ctx.quadraticCurveTo(-40 * s, -20 * s, -30 * s + sway, 0);
    ctx.lineTo(30 * s + sway, 0);
    ctx.quadraticCurveTo(40 * s, -20 * s, 14 * s, -95 * s);
    ctx.closePath();
    ctx.fill();

    // drapery folds
    ctx.strokeStyle = fold;
    ctx.lineWidth = 1.6 * s;
    ctx.beginPath();
    for (let i = 0; i < 5; i++) {
      const fx = -24 + i * 11;
      ctx.moveTo(fx * s, -94 * s);
      ctx.quadraticCurveTo((fx - 5) * s, -52 * s, (fx + 3) * s + sway * 0.4, 0);
    }
    ctx.stroke();
    // gold himation belt
    ctx.strokeStyle = gold;
    ctx.lineWidth = 2.4 * s;
    ctx.beginPath();
    ctx.moveTo(-13 * s, -67 * s);
    ctx.quadraticCurveTo(0, -63 * s + sway * 0.3, 13 * s, -67 * s);
    ctx.stroke();
    // collar / himation edge over the shoulder
    ctx.strokeStyle = fold;
    ctx.lineWidth = 2 * s;
    ctx.beginPath();
    ctx.moveTo(-12.5 * s, -92 * s);
    ctx.quadraticCurveTo(0, -87 * s, 12.5 * s, -92 * s);
    ctx.stroke();

    // ---- head with face ----
    const hy = -112 * s;
    // He was five and a half heads tall, which is a child's proportion and part
    // of why he never looked like a man. Shrinking the head about its own centre
    // takes him to roughly six and a half without moving anything else, and
    // scaling the whole group means the features cannot drift off the face.
    ctx.save();
    ctx.translate(0, hy);
    ctx.scale(0.86, 0.86);
    ctx.translate(0, -hy);

    // the face turns from lit to shadow across its own width, like the robe
    const faceG = ctx.createLinearGradient(-11 * s, hy, 11 * s, hy);
    faceG.addColorStop(0, "oklch(0.42 0.045 292)");
    faceG.addColorStop(0.45, "oklch(0.63 0.03 85)");
    faceG.addColorStop(1, "oklch(0.74 0.05 82)");
    ctx.fillStyle = faceG;
    ctx.beginPath();
    ctx.arc(0, hy, 11 * s, 0, Math.PI * 2);
    ctx.fill();
    // cheek shade
    ctx.fillStyle = "oklch(0.46 0.04 290 / 0.28)";
    ctx.beginPath();
    ctx.arc(2.5 * s, hy + 2 * s, 8.5 * s, -Math.PI / 2, Math.PI / 2);
    ctx.fill();
    // brow + eye
    ctx.strokeStyle = "oklch(0.22 0.015 292)";
    ctx.lineWidth = 1.4 * s;
    ctx.beginPath();
    ctx.moveTo(-5 * s, hy - 3 * s);
    ctx.quadraticCurveTo(0, -6 * s, 5.5 * s, hy - 3.5 * s);
    ctx.stroke();
    ctx.fillStyle = "oklch(0.78 0.012 84)";
    ctx.beginPath();
    ctx.arc(3.6 * s, hy - 1.2 * s, 1.3 * s, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "oklch(0.14 0.01 260)";
    ctx.beginPath();
    ctx.arc(3.9 * s, hy - 1.2 * s, 0.6 * s, 0, Math.PI * 2);
    ctx.fill();
    // nose
    ctx.strokeStyle = "oklch(0.48 0.02 84 / 0.7)";
    ctx.lineWidth = 1.3 * s;
    ctx.beginPath();
    ctx.moveTo(0.5 * s, hy - 3 * s);
    ctx.lineTo(2.2 * s, hy + 1.5 * s);
    ctx.quadraticCurveTo(1 * s, hy + 3 * s, -1 * s, hy + 3.2 * s);
    ctx.stroke();

    // layered beard with strands
    ctx.fillStyle = "oklch(0.68 0.02 78)";
    ctx.beginPath();
    ctx.moveTo(-6.5 * s, hy + 4 * s);
    ctx.quadraticCurveTo(-8 * s, hy + 17 * s, -3 * s, hy + 23 * s);
    ctx.quadraticCurveTo(0, hy + 29 * s, 3 * s, hy + 23 * s);
    ctx.quadraticCurveTo(8 * s, hy + 17 * s, 6.5 * s, hy + 4 * s);
    ctx.quadraticCurveTo(0, hy + 11 * s, -6.5 * s, hy + 4 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "oklch(0.5 0.025 78 / 0.6)";
    ctx.lineWidth = 1.2 * s;
    ctx.beginPath();
    for (let i = -3; i <= 3; i++) {
      ctx.moveTo(i * 1.6 * s, hy + 6 * s);
      ctx.lineTo(i * 1.5 * s, hy + (22 - Math.abs(i) * 2.2) * s);
    }
    ctx.stroke();

    // laurel crown: green leaves + gold band
    ctx.fillStyle = "oklch(0.5 0.09 122)";
    for (let i = -2; i <= 2; i++) {
      const ang = -Math.PI / 2 + i * 0.3;
      ctx.save();
      ctx.translate(i * 3.4 * s, hy - 8.5 * s);
      ctx.rotate(ang);
      ctx.beginPath();
      ctx.ellipse(0, -3.4 * s, 1.6 * s, 3.6 * s, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
    ctx.strokeStyle = gold;
    ctx.lineWidth = 1.8 * s;
    ctx.beginPath();
    ctx.moveTo(-8.5 * s, hy - 8 * s);
    ctx.quadraticCurveTo(0, hy - 12 * s, 8.5 * s, hy - 8 * s);
    ctx.stroke();
    ctx.restore(); // end of the head group's scale

    // An arm drawn as one stroke of constant width is a pipe. Real ones carry
    // their mass at the shoulder and lose it toward the wrist, so these go down
    // in two segments, the forearm thinner than the upper arm, with the elbow
    // joint filled in so the step between them does not show.
    const limb = (pts: Array<[number, number]>, w0: number, w1: number, tone: string) => {
      ctx.strokeStyle = tone;
      ctx.lineCap = "round";
      for (let i = 0; i < pts.length - 1; i++) {
        const a = pts[i]!;
        const b = pts[i + 1]!;
        ctx.lineWidth = (w0 + (w1 - w0) * (i / Math.max(1, pts.length - 2))) * s;
        ctx.beginPath();
        ctx.moveTo(a[0] * s, a[1] * s);
        ctx.lineTo(b[0] * s, b[1] * s);
        ctx.stroke();
      }
    };

    // ---- arms + lightning ----
    if (st === "strike") {
      // raised right arm hurling the bolt
      limb(
        [
          [11, -92],
          [22, -120],
          [28, -134],
        ],
        6.4,
        4.2,
        "oklch(0.66 0.04 84)",
      );
      // gold bracer
      ctx.strokeStyle = gold;
      ctx.lineWidth = 4.6 * s;
      ctx.beginPath();
      ctx.moveTo(20 * s, -116 * s);
      ctx.lineTo(23 * s, -122 * s);
      ctx.stroke();

      const bx = toScreenX(this.x) - zx;
      const by = toScreenY(terrainAt(L, this.x)) - zy;
      const ox = 28 * s;
      const oy = -134 * s;

      // A bolt reshaped by Math.random() on every frame is not lightning, it is
      // static: sixty different bolts a second. Quantise the clock instead so one
      // channel holds for ~70ms and then re-strikes, the way a real flash does.
      const strike = Math.floor(this.zeus.t * 14);
      const jitter = (i: number) => this.hash(strike * 31.7 + i * 5.3) - 0.5;
      // each re-strike has its own brightness; some are barely there
      const power = 0.45 + this.hash(strike * 3.1) * 0.55;

      const segs = 9;
      const channel: Array<[number, number]> = [[ox, oy]];
      for (let i = 1; i <= segs; i++) {
        const tt = i / segs;
        // the channel wanders most in the middle and converges on the target
        const spread = 26 * s * Math.sin(tt * Math.PI) ** 0.7;
        channel.push([ox + (bx - ox) * tt + jitter(i) * spread, oy + (by - oy) * tt]);
      }
      const traceChannel = () => {
        ctx.beginPath();
        channel.forEach(([px, py], i) => (i === 0 ? ctx.moveTo(px, py) : ctx.lineTo(px, py)));
      };

      ctx.lineCap = "round";
      ctx.lineJoin = "round";
      // three passes: a wide glow, the hot channel, then a white core inside it
      ctx.strokeStyle = `oklch(0.8 0.16 72 / ${(0.3 * power).toFixed(3)})`;
      ctx.lineWidth = 11 * s;
      traceChannel();
      ctx.stroke();
      ctx.shadowColor = "oklch(0.85 0.16 70)";
      ctx.shadowBlur = 18;
      ctx.strokeStyle = `oklch(0.95 0.05 95 / ${power.toFixed(3)})`;
      ctx.lineWidth = 3.2 * s;
      traceChannel();
      ctx.stroke();
      ctx.strokeStyle = `rgba(255,255,255,${(0.95 * power).toFixed(3)})`;
      ctx.lineWidth = 1.3 * s;
      traceChannel();
      ctx.stroke();
      ctx.shadowBlur = 0;

      // forks peeling off the channel and dying out, never reaching the ground
      ctx.strokeStyle = `oklch(0.85 0.2 75 / ${(0.75 * power).toFixed(3)})`;
      ctx.lineWidth = 1.6 * s;
      ctx.beginPath();
      for (let b = 0; b < 4; b++) {
        const at = 1 + Math.floor(this.hash(strike * 9.7 + b) * (segs - 2));
        const [px, py] = channel[at]!;
        let fx = px;
        let fy = py;
        ctx.moveTo(fx, fy);
        // two or three ragged steps, each shorter than the last
        for (let k = 1; k <= 3; k++) {
          fx += (jitter(b * 7 + k) * 26 + 10) * s * (1 - k * 0.25);
          fy += (jitter(b * 13 + k) * 14 - 12) * s * (1 - k * 0.25);
          ctx.lineTo(fx, fy);
        }
      }
      ctx.stroke();

      // thundercloud churning over him: soft masses, underlit by the bolt
      for (let c = 0; c < 6; c++) {
        const cx = (c - 2.5) * 21 * s + Math.sin(this.t * 0.9 + c) * 4 * s;
        const cy = -120 * s - (c % 2 === 0 ? 8 : 0) * s;
        const cr = (16 + (c % 3) * 6) * s;
        const g = ctx.createRadialGradient(cx, cy - cr * 0.2, cr * 0.1, cx, cy, cr);
        g.addColorStop(0, `oklch(0.28 0.03 285 / ${(0.72 * appear).toFixed(3)})`);
        g.addColorStop(0.65, `oklch(0.32 0.04 290 / ${(0.4 * appear).toFixed(3)})`);
        g.addColorStop(1, "oklch(0.36 0.04 295 / 0)");
        ctx.fillStyle = g;
        ctx.beginPath();
        ctx.arc(cx, cy, cr, 0, Math.PI * 2);
        ctx.fill();
        // the flash catching the underside of the mass
        const u = ctx.createRadialGradient(cx, cy + cr * 0.45, cr * 0.05, cx, cy + cr * 0.45, cr);
        u.addColorStop(0, `oklch(0.9 0.12 78 / ${(0.3 * power * appear).toFixed(3)})`);
        u.addColorStop(1, "oklch(0.85 0.12 70 / 0)");
        ctx.fillStyle = u;
        ctx.beginPath();
        ctx.arc(cx, cy + cr * 0.45, cr, 0, Math.PI * 2);
        ctx.fill();
      }

      // sparks scattering off the fist rather than orbiting it on rails
      ctx.fillStyle = `rgba(255,240,190,${(0.95 * power).toFixed(3)})`;
      for (let sp = 0; sp < 7; sp++) {
        const a = this.hash(strike * 17.3 + sp * 2.7) * Math.PI * 2;
        const d = (5 + this.hash(strike * 5.9 + sp) * 16) * s;
        ctx.beginPath();
        ctx.arc(
          ox + Math.cos(a) * d,
          oy + Math.sin(a) * d,
          (0.8 + jitter(sp) * 0.6) * s,
          0,
          Math.PI * 2,
        );
        ctx.fill();
      }
    } else {
      // standing: one arm across the chest, one at his side. The far arm is
      // darker — it is on his shadow side, and the depth reads better for it.
      limb(
        [
          [-11, -94],
          [-26, -80],
          [-20, -60],
        ],
        6.2,
        4,
        "oklch(0.4 0.04 292)",
      );
      limb(
        [
          [10, -94],
          [20, -78],
          [16, -60],
        ],
        6.4,
        4.2,
        "oklch(0.66 0.04 84)",
      );
      // gold bracer on the crossed arm
      ctx.strokeStyle = gold;
      ctx.lineWidth = 4.6 * s;
      ctx.beginPath();
      ctx.moveTo(18 * s, -80 * s);
      ctx.lineTo(22 * s, -76 * s);
      ctx.stroke();
    }

    // A flat disc of light laid over the whole figure — hard-edged, and it washed
    // the shading back out of everything underneath it. Gone.

    ctx.restore();
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
    const ctx = this.ctx;
    const s = scale;
    const pushing = this.phase === "playing" && !kicking;
    // How hard he is actually travelling, 0..1. Stride, bob, arm swing and the
    // hem of the tunic all scale off this, so standing still looks like standing
    // still instead of marching in place.
    const speed = kicking ? KICK_SPRINT : Math.abs(this.vx);
    const effort = Math.min(1, speed / 130);

    // Load is not the same as effort, and it is the one that shows. Leaning on
    // the stone and getting nowhere is the hardest he ever works: full input
    // against no speed. That is the moment the body has to look like it is
    // losing, so this peaks exactly there and falls away as the stone starts to
    // give.
    const load = pushing
      ? Math.max(0, Math.min(1, this.input)) * (1 - Math.min(1, Math.abs(this.vx) / 95))
      : 0;
    // Capped lower than it was. Past a certain point the shoulders get far
    // enough ahead of the hips that the spine reads as horizontal, and a
    // horizontal spine with a head out front is a quadruped no matter how well
    // the legs are moving.
    const lean = kicking ? 0.78 : pushing ? 0.85 + load * 0.22 : 0.35;
    // he sinks into it: hips drop and the knees take the weight
    const sink = load * 7 * s;
    // the fine shake of a muscle held near its limit, too fast to count. Two of
    // them, out of phase, so the two sides of him do not shiver as one object.
    const tremble = Math.sin(this.t * 31) * load * 0.9 * s;
    const shudder = Math.sin(this.t * 23.4 + 1.7) * load * 0.75 * s;
    /**
     * Breathing. Shallow and fast when he is straining, deep and slow when he is
     * not, and hard and ragged at a sprint. It drives the chest, the shoulders and
     * how far his mouth is open — one number so they cannot fall out of step.
     */
    const breath = Math.sin(this.t * (1.6 + load * 4.2 + (kicking ? 3.4 : 0)));
    const winded = Math.max(load, kicking ? 0.75 : 0);

    const bob = Math.abs(Math.cos(this.gait)) * (kicking ? 3.6 : 4) * s * effort;

    ctx.save();
    ctx.translate(x, groundY - bob);
    ctx.rotate(ang);

    // He stands with the sun above and behind him, so left alone he resolves to
    // a black cutout — which is what a camera would give you, and useless when
    // he is the thing you are meant to be watching. A backlit subject gets fill:
    // here it is bounce off the sunlit ground, warm and coming from below, which
    // is why this runs lighter at his feet than at his shoulders.
    const body = ctx.createLinearGradient(0, -78 * s, 0, 0);
    body.addColorStop(0, "oklch(0.34 0.038 42)");
    body.addColorStop(0.55, "oklch(0.42 0.045 46)");
    body.addColorStop(1, "oklch(0.52 0.062 54)");
    /** the limbs on his far side, kept darker so the near ones read in front */
    const bodyFar = "oklch(0.27 0.032 42)";
    const cloth = "oklch(0.54 0.06 60)";
    const line = "oklch(0.2 0.035 42 / 0.5)";
    const rim = "oklch(0.93 0.12 68 / 0.75)";

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

    const stepLen = (kicking ? 18 : 10) * s * effort;
    const liftH = (kicking ? 13 : 4.5) * s * effort;
    // He was 5.3 heads tall — a child's proportion, and the reason he read as a
    // doll however well he moved. The hip and shoulder go up, the head comes
    // down, and he lands near eight heads, which is where a grown man sits.
    const hipY0 = -42 * s + sink;
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
      front ? { fx: 12 * s, fy: 0, lift: 0 } : { fx: -21 * s, fy: -2.2 * s, lift: 0 };

    const pose = (theta: number, front: boolean) => {
      const w = walkPose(theta);
      const b = bracePose(front);
      const m = load;
      return {
        fx: w.fx + (b.fx - w.fx) * m,
        fy: w.fy + (b.fy - w.fy) * m,
        lift: w.lift * (1 - m),
        /** the back heel comes off the ground as he drives through it */
        heel: front ? 0 : m,
      };
    };

    /** Two-bone solve, knee always breaking forward. */
    const knee = (fx: number, fy: number) => {
      const dx = fx - 0;
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
      return { kx: ux * a + px * h, ky: hipY0 + uy * a + py * h };
    };

    const leg = (theta: number, front: boolean, shade: string | CanvasGradient) => {
      const p = pose(theta, front);
      // the quads shake under a load they cannot move
      const jitter = front ? tremble * 0.6 : shudder * 0.6;
      const { kx, ky } = knee(p.fx, p.fy + jitter * 0.2);
      // thigh: heavy at the hip, quadriceps forward, narrow above the knee
      seg(0, hipY0, kx + jitter, ky, 7.4 * s, 4.6 * s, (1.9 + load * 1.5) * s, 0.42, shade);
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

      // sandal. Toe-down through the swing; heel up and toes gripping when he is
      // driving off it, which is the pose that reads as "pushing" from the ankle up.
      const toeDrop = p.lift * 0.5 + p.heel * 0.62;
      ctx.save();
      ctx.translate(p.fx, p.fy);
      ctx.rotate(toeDrop);
      ctx.fillStyle = "oklch(0.16 0.02 40)";
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
      ctx.restore();
    };

    // far leg first and slightly dimmed, so the two read as depth not as a blur.
    // Under load the far one is the leg driving out behind him.
    leg(this.gait + Math.PI, false, bodyFar);
    leg(this.gait, true, body);

    // ---- torso (leaning into the boulder) ----
    const hipX = 0;
    const hipY = hipY0;
    // Under load the spine does not just tip: it flattens. The shoulders run out
    // ahead of the hips while dropping less than they do, which is the shape of a
    // man putting his whole mass behind his hands.
    const shX = 9 * s * lean + tremble;
    const shY = -76 * s + sink * 0.55;
    // The chest fills and empties. Shallow and quick under load, slow at rest.
    const chest = (1 + breath * 0.5) * s;

    /**
     * The trunk in one path, with the landmarks a body actually has: trapezius
     * running out to the shoulder, the lat sweeping down and in, the waist pinched
     * above the belt, the pelvis flaring again below it. The old version was a
     * quadrilateral with two circles stuck on the top corners.
     */
    const midY = (shY + hipY) / 2;
    ctx.fillStyle = body;
    ctx.beginPath();
    // back of the neck out along the trapezius
    ctx.moveTo(shX - 2 * s, shY - 6 * s);
    ctx.quadraticCurveTo(shX - 8 * s, shY - 4 * s, shX - 10.5 * s, shY + 1 * s);
    // lat: wide under the armpit, drawn in hard at the waist
    ctx.quadraticCurveTo(shX - 12 * s, midY - 2 * s, hipX - 7.5 * s, midY + 6 * s);
    // the small of the back
    ctx.quadraticCurveTo(hipX - 8 * s, hipY - 4 * s, hipX - 7.5 * s, hipY + 2 * s);
    // across the pelvis
    ctx.lineTo(hipX + 9 * s, hipY + 2 * s);
    // hip flexor up into the oblique
    ctx.quadraticCurveTo(hipX + 10 * s, hipY - 6 * s, hipX + 8.5 * s, midY + 5 * s);
    // the belly and the arch of the ribs, breathing
    ctx.quadraticCurveTo(shX + 12 * s + chest, midY - 1 * s, shX + 12.5 * s + chest, shY + 4 * s);
    // pectoral shelf into the front of the shoulder
    ctx.quadraticCurveTo(shX + 12 * s, shY - 3 * s, shX + 5 * s, shY - 6 * s);
    ctx.closePath();
    ctx.fill();

    // Deltoids. A shoulder is a cap of muscle sitting over the joint, wider than
    // the arm it feeds, and it swells when the arm is loaded.
    const delt = (4.9 + load * 1.1) * s;
    ctx.beginPath();
    ctx.ellipse(shX - 9 * s, shY + 1.5 * s, delt * 0.9, delt, -0.3, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(shX + 10 * s, shY + 1.5 * s, delt, delt * 1.05, 0.25, 0, Math.PI * 2);
    ctx.fill();
    // trapezius, rising toward the neck — the muscle that shows when a man braces
    ctx.beginPath();
    ctx.moveTo(shX - 8 * s, shY - 1 * s);
    ctx.quadraticCurveTo(shX + 1 * s, shY - (8 + load * 2.5) * s, shX + 9 * s, shY - 1 * s);
    ctx.quadraticCurveTo(shX + 1 * s, shY + 2 * s, shX - 8 * s, shY - 1 * s);
    ctx.fill();

    /**
     * The exomis: a labourer's garment, belted at the waist and slung over the far
     * shoulder only, leaving the near side of the chest bare. A sleeveless tunic
     * covered the whole trunk, which meant every bit of anatomy underneath was doing
     * no work at all — and a man whose body you cannot see cannot look like a man
     * straining. The cloth turns with the same light the skin does.
     */
    const linen = ctx.createLinearGradient(shX - 10 * s, 0, shX + 13 * s, 0);
    linen.addColorStop(0, "oklch(0.4 0.04 58 / 0.96)");
    linen.addColorStop(0.45, "oklch(0.6 0.06 64 / 0.96)");
    linen.addColorStop(1, "oklch(0.76 0.07 70 / 0.96)");

    // the strap, over the far shoulder and across the back
    ctx.fillStyle = "oklch(0.46 0.05 60 / 0.96)";
    ctx.beginPath();
    ctx.moveTo(shX - 11 * s, shY + 1 * s);
    ctx.quadraticCurveTo(shX - 6 * s, shY + 8 * s, hipX - 3 * s, hipY - 2 * s);
    ctx.lineTo(hipX - 7.5 * s, hipY - 1 * s);
    ctx.quadraticCurveTo(shX - 10 * s, shY + 10 * s, shX - 13 * s, shY + 2 * s);
    ctx.closePath();
    ctx.fill();

    // the skirt, hanging from the belt to mid-thigh, hem frayed and moving
    ctx.fillStyle = linen;
    const hem = (i: number) => Math.sin(this.gait * 1.0 + i) * 1.8 * s * effort;
    ctx.beginPath();
    ctx.moveTo(hipX - 8 * s, hipY - 3 * s);
    ctx.lineTo(hipX + 9.5 * s, hipY - 3 * s);
    ctx.quadraticCurveTo(hipX + 11 * s, hipY + 6 * s, hipX + 9 * s, hipY + 12 * s + hem(0));
    ctx.lineTo(hipX + 5 * s, hipY + 9 * s + hem(1.1));
    ctx.lineTo(hipX + 1 * s, hipY + 13 * s + hem(2.2));
    ctx.lineTo(hipX - 3 * s, hipY + 9 * s + hem(3.3));
    ctx.lineTo(hipX - 7 * s, hipY + 12 * s + hem(4.4));
    ctx.quadraticCurveTo(hipX - 10 * s, hipY + 5 * s, hipX - 8 * s, hipY - 3 * s);
    ctx.closePath();
    ctx.fill();
    // Fold creases, gathered at the belt and fanning down — linen does not hang flat.
    ctx.strokeStyle = "oklch(0.33 0.045 58 / 0.55)";
    ctx.lineWidth = 1 * s;
    ctx.lineCap = "round";
    for (let i = 0; i < 4; i++) {
      const topX = hipX + (-5 + i * 4) * s;
      ctx.beginPath();
      ctx.moveTo(topX, hipY - 1 * s);
      ctx.quadraticCurveTo(topX + 1.2 * s, hipY + 5 * s, topX + 0.6 * s, hipY + 10 * s + hem(i));
      ctx.stroke();
    }
    // leather belt across the waist
    ctx.fillStyle = "oklch(0.15 0.015 42)";
    ctx.fillRect(hipX - 8 * s, hipY - 4 * s, 18 * s, 3 * s);
    ctx.fillStyle = "oklch(0.42 0.05 58 / 0.7)";
    ctx.fillRect(hipX - 8 * s, hipY - 4 * s, 18 * s, 0.8 * s);

    /**
     * The bare trunk. Under load every one of these deepens: a pectoral under
     * tension has a hard lower edge, the serratus shows along the ribs, and the
     * abdominal wall braces before the arms ever move.
     */
    const cut = (0.45 + load * 0.5).toFixed(3);
    ctx.strokeStyle = `oklch(0.2 0.035 42 / ${cut})`;
    ctx.lineWidth = (1.3 + load * 0.4) * s;
    // the lower edge of the pectoral, running from the sternum out to the armpit
    ctx.beginPath();
    ctx.moveTo(shX + 2 * s, shY + 8 * s);
    ctx.quadraticCurveTo(shX + 8 * s, shY + 9 * s, shX + 11 * s, shY + 4 * s);
    ctx.stroke();
    // sternum, splitting the two halves of the chest
    ctx.lineWidth = 1 * s;
    ctx.beginPath();
    ctx.moveTo(shX + 3.4 * s, shY + 2 * s);
    ctx.lineTo(shX + 3 * s, shY + 9 * s);
    ctx.stroke();
    // linea alba down the middle of the belly
    ctx.beginPath();
    ctx.moveTo(shX + 3 * s, shY + 10 * s);
    ctx.quadraticCurveTo(hipX + 3 * s, midY + 4 * s, hipX + 2 * s, hipY - 5 * s);
    ctx.stroke();
    // three rows of abdominals, tightening with the load
    ctx.lineWidth = (0.9 + load * 0.4) * s;
    for (let i = 0; i < 3; i++) {
      const ay = shY + (12 + i * 5.5) * s;
      if (ay > hipY - 5 * s) break;
      ctx.beginPath();
      ctx.moveTo(shX + (0.5 - i * 0.4) * s, ay);
      ctx.quadraticCurveTo(shX + 4 * s, ay + 1.2 * s, shX + (8 - i * 0.7) * s, ay - 0.6 * s);
      ctx.stroke();
    }
    // serratus, only when he is really working
    if (load > 0.3) {
      ctx.strokeStyle = `oklch(0.2 0.035 42 / ${(0.3 * load).toFixed(3)})`;
      ctx.lineWidth = 0.8 * s;
      for (let i = 0; i < 3; i++) {
        const ay = shY + (9 + i * 3.2) * s;
        ctx.beginPath();
        ctx.moveTo(shX + (9.5 - i * 0.5) * s, ay);
        ctx.lineTo(shX + (11.5 - i * 0.6) * s, ay - 2 * s);
        ctx.stroke();
      }
    }

    // The back flap is gone. It hung off the hip, reached seventeen units behind
    // him and swung on the gait — which is a tail, and once the eye has found a
    // tail on a leaning body with the head thrown forward it stops seeing a man
    // and starts seeing a horse. Nothing else about him needed to change for
    // that reading to take hold, and nothing else needs to change to break it.
    //
    // The front drape stays: it hangs down off the belt where cloth actually
    // hangs, and reads as a tunic rather than as an appendage.
    const sway = (Math.sin(this.gait - 0.6) * 1.6 * effort + Math.sin(this.t * 1.4) * 0.5) * s;
    ctx.fillStyle = cloth;
    ctx.beginPath();
    ctx.moveTo(hipX + 8 * s, hipY);
    ctx.quadraticCurveTo(hipX + 12 * s, hipY + 1 * s + sway, hipX + 10 * s, hipY + 5 * s + sway);
    ctx.quadraticCurveTo(hipX + 6 * s, hipY + 3 * s, hipX + 6 * s, hipY - 2 * s);
    ctx.closePath();
    ctx.fill();

    if (kicking) {
      // Running, the arms drive against the legs — right arm forward as the left
      // leg swings through. Held rigidly out in front (as they were) a sprint
      // reads as sleepwalking.
      const sx = shX + 3 * s;
      const sy = shY + 1 * s;
      /**
       * A sprint is work, and it has to look like it even with no stone in front of
       * him. The arm drives from a locked, high elbow — hand up past the ribs at the
       * front of the swing, elbow thrown well behind him at the back — and the whole
       * limb keeps its muscle: biceps forward on the drive, triceps behind on the
       * recovery.
       */
      const swingArm = (theta: number, shade: string | CanvasGradient, depth: number) => {
        const sw = Math.sin(theta) * effort;
        const handX = sx + (4 + 11 * sw) * s + depth;
        const handY = sy + (15 - 8 * sw) * s;
        // the elbow stays high and tucked, and swings behind him on the recovery
        const elX = sx + (-1 + 5 * sw) * s + depth;
        const elY = sy + (10 - 1.5 * sw) * s;
        seg(sx + depth, sy, elX, elY, 4.6 * s, 3.4 * s, -1.5 * s, 0.55, shade);
        seg(elX, elY, handX, handY, 3.4 * s, 2.2 * s, 1.2 * s, 0.35, shade);
        // fist, closed at a run
        ctx.fillStyle = shade;
        ctx.beginPath();
        ctx.ellipse(handX, handY, 3 * s, 2.5 * s, 0.4, 0, Math.PI * 2);
        ctx.fill();
      };
      // arms counter the legs: near leg is at `gait`, so the near arm is opposite
      swingArm(this.gait, bodyFar, -2.4 * s);
      swingArm(this.gait + Math.PI, body, 2.4 * s);
    } else {
      // ---- arms braced against the boulder ----
      const shoulder = { x: shX + 3 * s, y: shY + 1 * s };
      const h1 = { x: bcx - R * 0.87 - x + tremble, y: bcy - R * 0.5 - groundY };
      const h2 = { x: bcx - R * 0.97 - x + tremble, y: bcy - R * 0.05 - groundY };
      /**
       * A braced arm is a strut. The elbow only just breaks — it straightens as the
       * load rises, because a bent arm cannot transmit force into a rock — and the
       * muscle bellies swell against it: triceps behind the upper arm, the forearm
       * heavy at the elbow and thin at the wrist.
       */
      const arm = (hand: { x: number; y: number }, shade: string | CanvasGradient) => {
        const dx = hand.x - shoulder.x;
        const dy = hand.y - shoulder.y;
        // the straighter he holds it, the harder he is pushing
        const bend = (1 - load) * 3.4 * s + 1.1 * s;
        const elbow = {
          x: shoulder.x + dx * 0.52 - bend * 0.5 + tremble * 0.5,
          y: shoulder.y + dy * 0.52 + bend,
        };
        // upper arm: triceps behind, thickening as the load comes on
        seg(
          shoulder.x,
          shoulder.y,
          elbow.x,
          elbow.y,
          (4.6 + load * 0.9) * s,
          (3.5 + load * 0.5) * s,
          -(1.5 + load * 1.3) * s,
          0.55,
          shade,
        );
        // forearm: the mass sits up near the elbow, the wrist is bone
        seg(
          elbow.x,
          elbow.y,
          hand.x,
          hand.y,
          (3.5 + load * 0.5) * s,
          2.2 * s,
          (1.3 + load * 0.9) * s,
          0.3,
          shade,
        );
        // the heel of the hand, flattened against the stone rather than a ball
        ctx.save();
        ctx.fillStyle = shade;
        ctx.translate(hand.x, hand.y);
        ctx.rotate(Math.atan2(dy, dx));
        ctx.beginPath();
        ctx.ellipse(0.6 * s, 0, 3.6 * s, 2.7 * s, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();

        // Cords standing out of the forearm. Only under real load, and they fade
        // in with it rather than sitting there permanently.
        if (load > 0.25) {
          ctx.strokeStyle = `oklch(0.2 0.035 42 / ${(0.42 * load).toFixed(3)})`;
          ctx.lineWidth = 0.8 * s;
          ctx.beginPath();
          ctx.moveTo(elbow.x + 1 * s, elbow.y + 0.5 * s);
          ctx.quadraticCurveTo(
            (elbow.x + hand.x) / 2 + 1.4 * s,
            (elbow.y + hand.y) / 2,
            hand.x - 1 * s,
            hand.y,
          );
          ctx.stroke();
        }
      };
      // the far arm first and dimmer, so the two do not merge into one slab
      arm(h2, bodyFar);
      arm(h1, body);

      // leather wrist bracers on the forearms
      const bracer = (hand: { x: number; y: number }) => {
        const mid = { x: (shoulder.x + hand.x) / 2, y: (shoulder.y + hand.y) / 2 };
        const bx = mid.x + (hand.x - mid.x) * 0.7;
        const by = mid.y + (hand.y - mid.y) * 0.7;
        const dx = hand.x - mid.x;
        const dy = hand.y - mid.y;
        const len = Math.hypot(dx, dy) || 1;
        const px = -dy / len;
        const py = dx / len;
        const bw = 2.6 * s;
        ctx.strokeStyle = "oklch(0.18 0.015 42)";
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
      bracer(h1);
      bracer(h2);
    }

    // ---- neck + head (profile facing the boulder) ----
    // A neck is not a rectangle. It leaves the collarbones wide, narrows, and runs
    // up at an angle into the base of the skull; under load it thickens and the
    // sternocleidomastoid stands out of the side of it.
    ctx.fillStyle = body;
    ctx.beginPath();
    ctx.moveTo(shX - 4 * s, shY + 1 * s);
    ctx.quadraticCurveTo(shX - 2.6 * s, shY - 5 * s, shX - 0.5 * s, shY - 10 * s);
    ctx.lineTo(shX + (5.6 + load * 0.6) * s, shY - 9 * s);
    ctx.quadraticCurveTo(shX + (5.4 + load * 0.8) * s, shY - 4 * s, shX + 6 * s, shY + 1 * s);
    ctx.closePath();
    ctx.fill();
    // the cords that stand out of a neck under real load
    if (load > 0.05) {
      ctx.strokeStyle = `oklch(0.2 0.035 42 / ${(0.5 * load).toFixed(3)})`;
      ctx.lineWidth = 0.9 * s;
      ctx.beginPath();
      ctx.moveTo(shX + 0.5 * s, shY + 1 * s);
      ctx.lineTo(shX + 2.4 * s, shY - 6 * s);
      ctx.moveTo(shX + 3 * s, shY + 1 * s);
      ctx.lineTo(shX + 4 * s, shY - 6 * s);
      ctx.stroke();
    }

    // pulled back over the shoulders; thrown forward it was the last thing
    // completing the horse
    const hx = shX + 2.2 * s;
    // the head gives back part of the hip bob — runners hold their eyes steady
    // while the pelvis rides up and down under them — and it drops toward the
    // stone as the load comes on, because that is what a neck does under weight
    const hy = shY - 13.5 * s + bob * 0.3 + load * 3.2 * s + shudder * 0.3;

    // The head was a seventh of his height when it should be nearer an eighth.
    // Scaling the whole group about its own centre fixes the proportion without
    // letting the nose, hair and beard drift off the face.
    ctx.save();
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

    // ear, set where the jaw meets the skull
    ctx.beginPath();
    ctx.ellipse(hx - 2.6 * s, hy + 1.4 * s, 1.5 * s, 2.2 * s, -0.2, 0, Math.PI * 2);
    ctx.fill();

    // hair: a heavy mass over the crown and down the back of the neck, not a ball
    ctx.beginPath();
    ctx.moveTo(hx + 6.2 * s, hy - 4 * s);
    ctx.quadraticCurveTo(hx + 4 * s, hy - 10.6 * s, hx - 2 * s, hy - 9.6 * s);
    ctx.quadraticCurveTo(hx - 8.4 * s, hy - 8 * s, hx - 8.6 * s, hy - 0.5 * s);
    ctx.quadraticCurveTo(hx - 9 * s, hy + 5 * s, hx - 5.6 * s, hy + 6.4 * s);
    ctx.quadraticCurveTo(hx - 7.2 * s, hy + 1 * s, hx - 5.4 * s, hy - 3.4 * s);
    ctx.quadraticCurveTo(hx - 2.4 * s, hy - 7.6 * s, hx + 6.2 * s, hy - 4 * s);
    ctx.closePath();
    ctx.fill();
    // thick dark beard
    ctx.beginPath();
    ctx.moveTo(hx + 6.2 * s, hy + 1 * s);
    ctx.quadraticCurveTo(hx + 7 * s, hy + 8 * s, hx + 2 * s, hy + 10 * s);
    ctx.quadraticCurveTo(hx - 2 * s, hy + 10 * s, hx - 3.4 * s, hy + 5 * s);
    ctx.quadraticCurveTo(hx - 3 * s, hy + 3 * s, hx + 1 * s, hy + 4 * s);
    ctx.quadraticCurveTo(hx + 3.5 * s, hy + 4.5 * s, hx + 4.5 * s, hy + 3 * s);
    ctx.closePath();
    ctx.fill();

    // Brow and mouth. Both tighten with the load rather than sitting at one
    // fixed grimace: the brow drives down over the eye and the mouth opens.
    ctx.strokeStyle = line;
    ctx.lineWidth = (1.3 + winded * 0.5) * s;
    ctx.beginPath();
    ctx.moveTo(hx + 2.2 * s, hy - 1.6 * s + winded * 1.3 * s);
    ctx.lineTo(hx + 6 * s, hy - 2.3 * s);
    ctx.stroke();
    // the eye, screwed shut as the effort peaks
    ctx.lineWidth = 1 * s;
    ctx.beginPath();
    ctx.moveTo(hx + 3.6 * s, hy + 0.4 * s);
    ctx.quadraticCurveTo(hx + 5.4 * s, hy + (0.9 - winded * 0.8) * s, hx + 6.6 * s, hy + 0.6 * s);
    ctx.stroke();
    // Mouth. Open, and opening further on every breath he drags in — a fixed
    // grimace reads as a mask, a mouth that moves reads as a man.
    if (pushing || kicking) {
      const gape = (1.6 + winded * 1.6 + Math.max(0, breath) * winded * 1.4) * s;
      ctx.fillStyle = "oklch(0.12 0.02 40)";
      ctx.beginPath();
      ctx.ellipse(hx + 6 * s, hy + 5.2 * s, 2.1 * s, gape * 0.6, -0.25, 0, Math.PI * 2);
      ctx.fill();
    }

    ctx.restore(); // end of the head group's scale

    // The sun is low and ahead of him, so it catches his front, not his back —
    // chest, brow and the leading thigh. `k` flips the whole thing if a level
    // ever hangs its sun on the other side of him.
    const k = this.sunScreen().x >= x ? 1 : -1;
    ctx.strokeStyle = rim;
    ctx.lineCap = "round";
    // brow and cheek
    ctx.lineWidth = 1.6 * s;
    ctx.beginPath();
    ctx.moveTo(hx + k * 2 * s, hy - 7 * s);
    ctx.quadraticCurveTo(hx + k * 7.5 * s, hy - 3 * s, hx + k * 7 * s, hy + 2 * s);
    ctx.stroke();
    // the lit edge running down chest and belly
    ctx.lineWidth = 2 * s;
    ctx.beginPath();
    ctx.moveTo(shX + k * 11 * s, shY + 1 * s);
    ctx.quadraticCurveTo(shX + k * 13 * s, shY + 14 * s, hipX + k * 9 * s, hipY - 1 * s);
    ctx.stroke();
    // a dim bounce down his shaded side keeps him off the black ridges behind
    ctx.strokeStyle = "oklch(0.6 0.05 55 / 0.28)";
    ctx.lineWidth = 1.5 * s;
    ctx.beginPath();
    ctx.moveTo(shX - k * 9 * s, shY + 1 * s);
    ctx.quadraticCurveTo(shX - k * 7 * s, shY - 10 * s, shX - k * 2 * s, shY - 20 * s);
    ctx.stroke();

    /**
     * Sweat. Four drops on a staggered loop, thrown off the brow and the jaw and
     * falling away behind him — the cheapest possible cue and the one that reads
     * fastest, because nothing else on screen falls at that speed. Only when he is
     * genuinely working, and it thins out as the effort does.
     */
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

    ctx.restore();
  }
}
