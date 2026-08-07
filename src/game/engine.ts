import { GameAudio } from "./audio";
import { quotesFor, type LangCode, type Quote } from "./quotes";
import { EPIGRAPHS, LEVEL_NAMES } from "@/i18n";
import {
  getLevel,
  slopeAt,
  terrainAt,
  type Cloud,
  type Level,
} from "./levels";

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

const DEFAULT_CLOUDS: Cloud[] = [];

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
    this.shake = Math.max(0, this.shake - dt * 2.2);
    this.flash = Math.max(0, this.flash - dt * 1.6);

    this.updateBirds(dt);
    this.updateParticles(dt);
    this.emitParticles();

    this.audio.updateFriction(
      Math.min(1, Math.abs(this.vx) / 420),
      this.phase === "rolling",
    );
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
    const pushing =
      this.phase === "playing" && this.input > 0.15 && Math.abs(this.vx) > 8;
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
      const py =
        this.groundY - (p.wy - this.camY) * this.scale * 0.55 + this.shakeY;
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

    // sky
    const sky = ctx.createLinearGradient(0, 0, 0, h);
    sky.addColorStop(0, L.sky[0]);
    sky.addColorStop(1, L.sky[1]);
    ctx.fillStyle = sky;
    ctx.fillRect(0, 0, w, h);

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

    this.drawStormSky();
    this.drawAphorism();
    this.drawClouds();
    this.drawMountain();
    this.renderBirds();
    this.drawMist();

    // parallax ridges
    L.ridges.forEach(([amp, freq, off], i) => {
      const p = 0.15 + i * 0.16;
      ctx.beginPath();
      ctx.moveTo(0, h);
      for (let sx = 0; sx <= w; sx += 8) {
        const wx = this.camX * p + sx / this.scale + off;
        const y =
          this.groundY -
          (terrainAt(L, wx) * 0.5 + Math.sin(wx * freq) * amp - this.camY * p) *
            this.scale *
            0.4 -
          40 * i;
        ctx.lineTo(sx, y);
      }
      ctx.lineTo(w, h);
      ctx.closePath();
      ctx.fillStyle = `oklch(${0.4 + i * 0.03} 0.03 250 / ${0.42 - i * 0.09})`;
      ctx.fill();
    });

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
    groundG.addColorStop(0, "oklch(0.38 0.05 56)");
    groundG.addColorStop(0.45, "oklch(0.32 0.045 50)");
    groundG.addColorStop(1, "oklch(0.24 0.035 44)");
    ctx.fillStyle = groundG;
    ctx.fill();

    // lighter dirt trail worn by the endless rolling boulder
    ctx.strokeStyle = "oklch(0.52 0.06 58 / 0.4)";
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
    for (
      let wx = Math.floor(this.camX / 22) * 22;
      wx < this.camX + w / this.scale;
      wx += 22
    ) {
      const h1 = this.hash(wx * 1.31);
      if (h1 < 0.42) continue;
      const gx = toScreenX(wx + (h1 - 0.5) * 22);
      const gy = toScreenY(terrainAt(L, wx));
      const tu = (2 + h1 * 3.4) * this.scale;
      ctx.strokeStyle =
        h1 < 0.72
          ? "oklch(0.45 0.05 130 / 0.6)"
          : "oklch(0.5 0.06 60 / 0.6)";
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

    // vignette / fog
    const fog = ctx.createRadialGradient(
      w / 2,
      h * 0.55,
      h * 0.2,
      w / 2,
      h * 0.55,
      h * 0.95,
    );
    fog.addColorStop(0, "rgba(0,0,0,0)");
    fog.addColorStop(1, "rgba(0,0,0,0.55)");
    ctx.fillStyle = fog;
    ctx.fillRect(0, 0, w, h);

    // lightning flash
    if (this.flash > 0) {
      ctx.fillStyle = `rgba(255,250,235,${this.flash * 0.55})`;
      ctx.fillRect(0, 0, w, h);
    }
  }

  private drawStormSky() {
    const ctx = this.ctx;
    const { w, h } = this;
    const sun = this.level.sun ?? DEFAULT_SUN;
    const sunX = w * sun.xFrac;
    const sunY = h * sun.yFrac;
    const t = this.t;
    const s = this.scale;

    // faint stars wheeling far above the storm deck
    for (let i = 0; i < 46; i++) {
      const hx = this.hash(i * 1.7) * w;
      const hy = this.hash(i * 3.1) * h * 0.3;
      if (Math.hypot(hx - sunX, hy - sunY) < w * 0.22) continue;
      const tw = Math.sin(t * 0.5 + i * 2.3);
      ctx.globalAlpha = 0.16 + 0.18 * tw;
      ctx.fillStyle = "oklch(0.95 0.01 260)";
      ctx.fillRect(hx, hy, 1.2, 1.2);
    }
    ctx.globalAlpha = 1;

    // warm golden glow radiating from the break in the storm clouds
    const glow = ctx.createRadialGradient(sunX, sunY, 0, sunX, sunY, w * 0.55);
    glow.addColorStop(0, "oklch(0.9 0.12 75 / 0.55)");
    glow.addColorStop(0.35, "oklch(0.84 0.1 70 / 0.22)");
    glow.addColorStop(1, "oklch(0.8 0.08 65 / 0)");
    ctx.fillStyle = glow;
    ctx.fillRect(0, 0, w, h);

    // layered corona spilling out of the break
    const sunR = 26 * s;
    const corona = ctx.createRadialGradient(sunX, sunY, sunR * 0.4, sunX, sunY, sunR * 3.1);
    corona.addColorStop(0, "oklch(0.97 0.08 80 / 0.9)");
    corona.addColorStop(0.35, "oklch(0.9 0.11 70 / 0.4)");
    corona.addColorStop(1, "oklch(0.85 0.1 68 / 0)");
    ctx.fillStyle = corona;
    ctx.beginPath();
    ctx.arc(sunX, sunY, sunR * 3.1, 0, Math.PI * 2);
    ctx.fill();

    // blazing sun disc with a white-hot core
    const disc = ctx.createRadialGradient(sunX, sunY, sunR * 0.05, sunX, sunY, sunR);
    disc.addColorStop(0, "oklch(0.99 0.04 78)");
    disc.addColorStop(0.5, "oklch(0.95 0.09 72)");
    disc.addColorStop(0.82, "oklch(0.88 0.12 64)");
    disc.addColorStop(1, "oklch(0.78 0.14 58)");
    ctx.fillStyle = disc;
    ctx.beginPath();
    ctx.arc(sunX, sunY, sunR, 0, Math.PI * 2);
    ctx.fill();

    // thin shreds of stratus tearing away from the break
    ctx.fillStyle = "oklch(0.55 0.02 250 / 0.3)";
    for (let i = 0; i < 9; i++) {
      const sy = sunY - 78 * s + (i - 4) * 19 * s + Math.sin(t * 0.18 + i * 1.3) * 8;
      const sx = sunX + 70 * s;
      const len = (150 + this.hash(i * 9.1) * 180) * s;
      ctx.beginPath();
      ctx.ellipse(
        sx + len * 0.5,
        sy,
        len * 0.5,
        (3 + this.hash(i * 4.4) * 3) * s,
        (this.hash(i) - 0.5) * 0.22,
        0,
        Math.PI * 2,
      );
      ctx.fill();
    }

    // dark storm cloud banks drifting, split open around the golden break
    ctx.fillStyle = "oklch(0.23 0.03 262 / 0.92)";
    const bankW = 300 * s;
    const bankH = 46 * s;
    for (let i = -3; i <= 4; i++) {
      const cx = ((i * bankW * 1.75 + t * 8) % (w + bankW * 2.4)) - bankW * 1.2;
      if (Math.abs(cx - sunX) < bankW * 1.25) continue;
      const cy = h * 0.12 + ((i * 37 + 20) % 11) + Math.sin(t * 0.22 + i) * 5;
      ctx.beginPath();
      ctx.moveTo(cx - bankW, cy);
      ctx.quadraticCurveTo(cx - bankW * 0.7, cy - bankH * 2.1, cx - bankW * 0.35, cy - bankH);
      ctx.quadraticCurveTo(cx, cy - bankH * 2.4, cx + bankW * 0.35, cy - bankH);
      ctx.quadraticCurveTo(cx + bankW * 0.7, cy - bankH * 2, cx + bankW, cy);
      ctx.lineTo(cx + bankW, cy + bankH * 0.9);
      ctx.lineTo(cx - bankW, cy + bankH * 0.9);
      ctx.closePath();
      ctx.fill();
    }

    // volumetric god rays, twice over for a hotter core beam
    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    for (let pass = 0; pass < 2; pass++) {
      ctx.fillStyle =
        pass === 0 ? "oklch(0.84 0.1 74 / 0.05)" : "oklch(0.9 0.1 75 / 0.08)";
      const count = pass === 0 ? 9 : 5;
      for (let i = 0; i < count; i++) {
        const base = 1.05 + (i / count) * 1.7 + Math.sin(t * 0.25 + i * 1.7) * 0.05;
        const a0 = base + (pass === 1 ? 0.03 : 0);
        const a1 = base + (pass === 1 ? 0.09 : 0.07);
        const len = w * 1.8;
        ctx.beginPath();
        ctx.moveTo(sunX, sunY);
        ctx.lineTo(sunX + Math.cos(a0) * len, sunY + Math.sin(a0) * len);
        ctx.lineTo(sunX + Math.cos(a1) * len, sunY + Math.sin(a1) * len);
        ctx.closePath();
        ctx.fill();
      }
    }
    ctx.restore();

    // soft warm veil spilling out of the break over the valley
    const veil = ctx.createLinearGradient(0, h * 0.28, 0, h * 0.85);
    veil.addColorStop(0, "rgba(255,200,130,0)");
    veil.addColorStop(0.5, "rgba(255,190,120,0.11)");
    veil.addColorStop(1, "rgba(255,180,110,0)");
    ctx.fillStyle = veil;
    ctx.fillRect(0, h * 0.28, w, h * 0.57);

    // dusty horizon haze rising over the far valley
    const haze = ctx.createLinearGradient(0, h * 0.55, 0, h);
    haze.addColorStop(0, "rgba(255,195,130,0)");
    haze.addColorStop(0.45, "rgba(255,185,125,0.1)");
    haze.addColorStop(1, "rgba(235,175,120,0.06)");
    ctx.fillStyle = haze;
    ctx.fillRect(0, h * 0.55, w, h * 0.45);
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

    ctx.shadowColor = "rgba(10,12,18,0.7)";
    ctx.shadowBlur = 6;
    ctx.fillStyle = "oklch(0.96 0.05 85 / 0.5)";
    const startY = sunY - ((lines.length - 1) * lineH) / 2;
    lines.forEach((line, i) => ctx.fillText(line, cx, startY + i * lineH));

    ctx.font = `italic ${Math.round(fs * 0.72)}px Georgia, 'Times New Roman', serif`;
    ctx.fillStyle = "oklch(0.88 0.08 85 / 0.42)";
    ctx.fillText(q.author, cx, startY + lines.length * lineH + lineH * 0.4);
    ctx.shadowBlur = 0;
  }

  private drawClouds() {
    const ctx = this.ctx;
    const { w, h } = this;
    const clouds = this.level.clouds ?? DEFAULT_CLOUDS;
    const wrap = w + 320;
    const sun = this.level.sun ?? DEFAULT_SUN;
    const sunX = w * sun.xFrac;

    // screen bounds of the distant mountain silhouette, so clouds drifting
    // directly behind it are skipped (they'd read as a white layer behind the peak)
    const mt = this.level.mountain;
    let mtX0 = -Infinity;
    let mtX1 = Infinity;
    let mtPeakY = -Infinity;
    if (mt) {
      const p = mt.parallax;
      const lx = (mt.x - mt.width / 2 - this.camX * p) * this.scale;
      const rx = (mt.x + mt.width / 2 - this.camX * p) * this.scale;
      mtX0 = Math.min(lx, rx) - 50;
      mtX1 = Math.max(lx, rx) + 50;
      const peakWy = mt.height * 0.12 + mt.height;
      mtPeakY =
        this.groundY - (peakWy - this.camY * p) * this.scale * 0.5 + this.shakeY * 0.4;
    }

    for (const c of clouds) {
      const x = (((c.offset - this.camX * c.parallax + this.t * c.speed) % wrap) + wrap) % wrap - 160;
      const y = h * c.yFrac + Math.sin(this.t * 0.1 + c.offset * 0.01) * 6;
      // skip clouds sitting behind the mountain body
      if (mt && x > mtX0 && x < mtX1 && y > mtPeakY - 36) continue;
      const s = c.scale * this.scale;
      this.drawCloud(ctx, x, y, s, c.alpha, sunX);
    }
  }

  private cloudSilhouette(ctx: CanvasRenderingContext2D, x: number, y: number, s: number) {
    ctx.beginPath();
    ctx.moveTo(x - 58 * s, y + 4 * s);
    ctx.ellipse(x, y + 5 * s, 58 * s, 15 * s, 0, 0, Math.PI * 2);
    const lobes: Array<[number, number, number]> = [
      [-50, 4, 24],
      [-34, -8, 26],
      [-16, -20, 25],
      [2, -26, 27],
      [20, -19, 26],
      [36, -7, 25],
      [50, 5, 20],
    ];
    for (const [dx, dy, r] of lobes) {
      const lx = x + dx * s;
      const ly = y + dy * s;
      ctx.moveTo(lx + r * s, ly);
      ctx.arc(lx, ly, r * s, 0, Math.PI * 2);
    }
  }

  private drawCloud(ctx: CanvasRenderingContext2D, x: number, y: number, s: number, alpha: number, sunX: number) {
    ctx.save();

    // soft outer halo so the cloud edges look airy instead of hard
    ctx.globalAlpha = alpha;
    ctx.fillStyle = "oklch(0.88 0.015 238)";
    this.cloudSilhouette(ctx, x, y, s * 1.62);
    ctx.globalAlpha = alpha * 0.16;
    ctx.fill();
    ctx.globalAlpha = alpha * 0.24;
    this.cloudSilhouette(ctx, x, y, s * 1.42);
    ctx.fill();
    ctx.globalAlpha = alpha;

    // top-lit fluff
    this.cloudSilhouette(ctx, x, y, s * 1.28);
    ctx.fillStyle = "oklch(0.92 0.015 235)";
    ctx.fill();
    ctx.globalAlpha = alpha * 0.3;

    this.cloudSilhouette(ctx, x, y, s);
    const body = ctx.createLinearGradient(0, y - 34 * s, 0, y + 18 * s);
    body.addColorStop(0, "oklch(0.7 0.03 245)");
    body.addColorStop(0.4, "oklch(0.58 0.035 246)");
    body.addColorStop(0.8, "oklch(0.47 0.04 248)");
    body.addColorStop(1, "oklch(0.4 0.05 250)");
    ctx.fillStyle = body;
    ctx.fill();

    ctx.save();
    ctx.globalCompositeOperation = "source-atop";
    const toSun = sunX >= x ? 1 : -1;
    this.cloudSilhouette(ctx, x + toSun * 3.5 * s, y - 2 * s, s * 1.04);
    const rim = ctx.createLinearGradient(x - toSun * 55 * s, 0, x + toSun * 60 * s, 0);
    rim.addColorStop(0, "oklch(0.9 0.1 72 / 0)");
    rim.addColorStop(1, "oklch(0.92 0.1 74 / 0.55)");
    ctx.fillStyle = rim;
    ctx.fill();

    this.cloudSilhouette(ctx, x, y + 3 * s, s * 0.98);
    const shade = ctx.createLinearGradient(0, y - 2 * s, 0, y + 18 * s);
    shade.addColorStop(0, "oklch(0 0 0 / 0)");
    shade.addColorStop(0.7, "oklch(0.12 0.05 265 / 0.28)");
    shade.addColorStop(1, "oklch(0.14 0.06 268 / 0.45)");
    ctx.fillStyle = shade;
    ctx.fill();
    ctx.restore();

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
    bodyG.addColorStop(0, mt.color);
    bodyG.addColorStop(1, "oklch(0.19 0.025 262)");
    ctx.fillStyle = bodyG;
    ctx.fill();

    // gentle side shading on the shadowed face
    const shade = ctx.createLinearGradient(farX(p1x), farY(p1y), farX(rx), farY(baseY));
    shade.addColorStop(0, "rgba(0,0,0,0)");
    shade.addColorStop(1, "rgba(0,0,0,0.35)");
    ctx.fillStyle = shade;
    ctx.beginPath();
    ctx.moveTo(farX(p1x), farY(p1y));
    ctx.lineTo(farX(p2x), farY(p2y));
    ctx.lineTo(farX(rx), farY(baseY));
    ctx.lineTo(farX(p1x), farY(baseY));
    ctx.closePath();
    ctx.fill();

    // warm light rim along the lit ridge
    ctx.strokeStyle = "oklch(0.85 0.09 68 / 0.5)";
    ctx.lineWidth = 2.4;
    ctx.lineJoin = "round";
    ctx.beginPath();
    ctx.moveTo(farX(pts[c0]![0]), farY(pts[c0]![1]));
    tracePts(pts.slice(c0, c1 + 1));
    ctx.stroke();

    // atmospheric haze melting the base into the valley
    const baseYp = farY(baseY);
    const haze = ctx.createLinearGradient(0, h, 0, baseYp);
    haze.addColorStop(0, "oklch(0.5 0.05 60 / 0.5)");
    haze.addColorStop(0.6, "oklch(0.5 0.05 60 / 0.1)");
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

    ctx.save();
    ctx.translate(bx, by);
    ctx.rotate(ang);
    ctx.rotate(this.roll);

    // base rock fill, warm-lit from the top-left
    const g = ctx.createLinearGradient(-R, -R, R * 0.7, R * 0.6);
    g.addColorStop(0, "oklch(0.68 0.025 70)");
    g.addColorStop(0.45, "oklch(0.52 0.022 74)");
    g.addColorStop(1, "oklch(0.34 0.02 72)");
    ctx.fillStyle = g;
    ctx.beginPath();
    outline();
    ctx.fill();

    ctx.save();
    ctx.beginPath();
    outline();
    ctx.clip();

    // soft ambient shading (sun from upper-left)
    const soft = ctx.createRadialGradient(-R * 0.35, -R * 0.42, R * 0.1, 0, 0, R * 1.15);
    soft.addColorStop(0, "oklch(0.78 0.035 60 / 0.55)");
    soft.addColorStop(0.45, "oklch(0.6 0.03 65 / 0.22)");
    soft.addColorStop(0.85, "oklch(0.26 0.025 70 / 0)");
    ctx.fillStyle = soft;
    ctx.fillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

    // ground ambient occlusion where it meets the dirt
    const ao = ctx.createRadialGradient(0, R * 0.55, R * 0.05, 0, R * 0.55, R * 0.75);
    ao.addColorStop(0, "rgba(0,0,0,0.28)");
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
    facet(
      [-2.1, -1.55, -1.0, -1.7],
      [0.32, 0.78, 0.62, 0.36],
      "oklch(0.78 0.03 58 / 0.4)",
    );
    facet(
      [-0.55, 0.05, 0.55, -0.25],
      [0.4, 0.8, 0.6, 0.34],
      "oklch(0.6 0.03 78 / 0.3)",
    );
    facet(
      [1.7, 2.2, 2.9, 2.15],
      [0.3, 0.72, 0.55, 0.28],
      "oklch(0.2 0.02 80 / 0.45)",
    );
    facet(
      [-3.0, -2.5, -2.1, -2.6],
      [0.4, 0.7, 0.5, 0.3],
      "oklch(0.66 0.02 68 / 0.35)",
    );

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
      ctx.strokeStyle = "oklch(0.3 0.02 260 / 0.75)";
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
      grad.addColorStop(0, "rgba(201,205,214,0)");
      grad.addColorStop(0.5, "rgba(201,205,214,1)");
      grad.addColorStop(1, "rgba(201,205,214,0)");
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

  private drawStones(
    toScreenX: (wx: number) => number,
    toScreenY: (wy: number) => number,
  ) {
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

  private drawOliveTrees(
    toScreenX: (wx: number) => number,
    toScreenY: (wy: number) => number,
  ) {
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

  private renderZeus(
    toScreenX: (wx: number) => number,
    toScreenY: (wy: number) => number,
  ) {
    const ctx = this.ctx;
    const st = this.zeus.state;
    if (st === "gone") return;
    const L = this.level;
    const zx = toScreenX(L.length + 44);
    const zy = toScreenY(terrainAt(L, L.length + 44));
    const s = this.scale;
    const appear = st === "appear" ? Math.min(1, this.zeus.t / 0.8) : 1;

    // divine aura
    ctx.save();
    ctx.globalAlpha = 0.55 * appear;
    const aura = ctx.createRadialGradient(zx, zy - 80 * s, 0, zx, zy - 80 * s, 150 * s);
    aura.addColorStop(0, "rgba(255,235,200,0.55)");
    aura.addColorStop(0.5, "rgba(255,205,140,0.2)");
    aura.addColorStop(1, "rgba(255,205,140,0)");
    ctx.fillStyle = aura;
    ctx.fillRect(zx - 170 * s, zy - 240 * s, 340 * s, 300 * s);
    ctx.restore();

    ctx.save();
    ctx.translate(zx, zy);
    ctx.globalAlpha = appear;
    const robe = "oklch(0.87 0.02 85)";
    const shade = "oklch(0.6 0.02 85)";
    const fold = "oklch(0.68 0.03 88 / 0.55)";
    const gold = "oklch(0.82 0.12 85)";
    const sway = Math.sin(this.t * 1.2) * 3 * s;

    // flowing robe
    ctx.fillStyle = robe;
    ctx.beginPath();
    ctx.moveTo(-14 * s, -95 * s);
    ctx.quadraticCurveTo(-40 * s, -20 * s, -30 * s + sway, 0);
    ctx.lineTo(30 * s + sway, 0);
    ctx.quadraticCurveTo(40 * s, -20 * s, 14 * s, -95 * s);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = shade;
    ctx.beginPath();
    ctx.moveTo(-14 * s, -95 * s);
    ctx.quadraticCurveTo(-40 * s, -20 * s, -30 * s + sway, 0);
    ctx.lineTo(-2 * s, 0);
    ctx.lineTo(4 * s, -95 * s);
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
    ctx.fillStyle = "oklch(0.85 0.015 85)";
    ctx.beginPath();
    ctx.arc(0, hy, 11 * s, 0, Math.PI * 2);
    ctx.fill();
    // cheek shade
    ctx.fillStyle = "oklch(0.72 0.02 85 / 0.35)";
    ctx.beginPath();
    ctx.arc(2.5 * s, hy + 2 * s, 8.5 * s, -Math.PI / 2, Math.PI / 2);
    ctx.fill();
    // brow + eye
    ctx.strokeStyle = "oklch(0.3 0.01 260)";
    ctx.lineWidth = 1.4 * s;
    ctx.beginPath();
    ctx.moveTo(-5 * s, hy - 3 * s);
    ctx.quadraticCurveTo(0, -6 * s, 5.5 * s, hy - 3.5 * s);
    ctx.stroke();
    ctx.fillStyle = "oklch(0.9 0.01 260)";
    ctx.beginPath();
    ctx.arc(3.6 * s, hy - 1.2 * s, 1.3 * s, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "oklch(0.14 0.01 260)";
    ctx.beginPath();
    ctx.arc(3.9 * s, hy - 1.2 * s, 0.6 * s, 0, Math.PI * 2);
    ctx.fill();
    // nose
    ctx.strokeStyle = "oklch(0.7 0.015 85 / 0.75)";
    ctx.lineWidth = 1.3 * s;
    ctx.beginPath();
    ctx.moveTo(0.5 * s, hy - 3 * s);
    ctx.lineTo(2.2 * s, hy + 1.5 * s);
    ctx.quadraticCurveTo(1 * s, hy + 3 * s, -1 * s, hy + 3.2 * s);
    ctx.stroke();

    // layered beard with strands
    ctx.fillStyle = "oklch(0.88 0.015 80)";
    ctx.beginPath();
    ctx.moveTo(-6.5 * s, hy + 4 * s);
    ctx.quadraticCurveTo(-8 * s, hy + 17 * s, -3 * s, hy + 23 * s);
    ctx.quadraticCurveTo(0, hy + 29 * s, 3 * s, hy + 23 * s);
    ctx.quadraticCurveTo(8 * s, hy + 17 * s, 6.5 * s, hy + 4 * s);
    ctx.quadraticCurveTo(0, hy + 11 * s, -6.5 * s, hy + 4 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "oklch(0.68 0.02 80 / 0.6)";
    ctx.lineWidth = 1.2 * s;
    ctx.beginPath();
    for (let i = -3; i <= 3; i++) {
      ctx.moveTo(i * 1.6 * s, hy + 6 * s);
      ctx.lineTo(i * 1.5 * s, hy + (22 - Math.abs(i) * 2.2) * s);
    }
    ctx.stroke();

    // laurel crown: green leaves + gold band
    ctx.fillStyle = "oklch(0.72 0.12 120)";
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

    // ---- arms + lightning ----
    if (st === "strike") {
      // raised right arm hurling the bolt
      ctx.strokeStyle = "oklch(0.88 0.02 85)";
      ctx.lineWidth = 5.5 * s;
      ctx.lineCap = "round";
      ctx.beginPath();
      ctx.moveTo(11 * s, -92 * s);
      ctx.lineTo(22 * s, -120 * s);
      ctx.lineTo(28 * s, -134 * s);
      ctx.stroke();
      // gold bracer
      ctx.strokeStyle = gold;
      ctx.lineWidth = 4.6 * s;
      ctx.beginPath();
      ctx.moveTo(20 * s, -116 * s);
      ctx.lineTo(23 * s, -122 * s);
      ctx.stroke();

      const bx = toScreenX(this.x) - zx;
      const by = toScreenY(terrainAt(L, this.x)) - zy;
      const segs = 8;
      ctx.shadowColor = "oklch(0.85 0.16 70)";
      ctx.shadowBlur = 18;
      ctx.strokeStyle = "oklch(0.95 0.05 95)";
      ctx.lineWidth = 3.2 * s;
      ctx.beginPath();
      ctx.moveTo(28 * s, -134 * s);
      for (let i = 1; i <= segs; i++) {
        const t = i / segs;
        const jx = (Math.random() - 0.5) * 24 * s * (1 - t * 0.5);
        ctx.lineTo(28 * s + (bx - 28 * s) * t + jx, -134 * s + (by + 134 * s) * t);
      }
      ctx.stroke();
      // bright core
      ctx.strokeStyle = "rgba(255,255,255,0.95)";
      ctx.lineWidth = 1.3 * s;
      ctx.stroke();
      ctx.shadowBlur = 0;

      // forking branches off the main bolt
      ctx.strokeStyle = "oklch(0.85 0.2 75 / 0.8)";
      ctx.lineWidth = 1.6 * s;
      ctx.beginPath();
      for (let b = 0; b < 3; b++) {
        const t = 0.25 + b * 0.2;
        ctx.moveTo(28 * s + (bx - 28 * s) * t, -134 * s + (by + 134 * s) * t);
        ctx.lineTo(
          28 * s + (bx - 28 * s) * (t + 0.14) + (Math.random() - 0.5) * 20 * s,
          -134 * s + (by + 134 * s) * (t + 0.14) - 16 * s,
        );
      }
      ctx.stroke();

      // thunderclouds churning around his head
      ctx.fillStyle = `rgba(35,35,50,${0.55 * appear})`;
      for (let c = 0; c < 5; c++) {
        const cx = (c - 2) * 22 * s + Math.sin(this.t * 0.9 + c) * 4 * s;
        const cy = -118 * s - (c % 2 === 0 ? 7 : 0) * s;
        ctx.beginPath();
        ctx.arc(cx, cy, (13 + (c % 3) * 5) * s, 0, Math.PI * 2);
        ctx.fill();
      }

      // crackling sparks at the fingertips
      ctx.fillStyle = "rgba(255,240,190,0.95)";
      for (let sp = 0; sp < 6; sp++) {
        const a = this.t * 22 + sp * 1.1;
        ctx.beginPath();
        ctx.arc(
          28 * s + Math.cos(a) * (9 + sp) * s,
          -134 * s + Math.sin(a) * (9 + sp) * s,
          1.4 * s,
          0,
          Math.PI * 2,
        );
        ctx.fill();
      }
    } else {
      // standing: one arm across the chest, one at his side
      ctx.strokeStyle = "oklch(0.88 0.02 85)";
      ctx.lineWidth = 5.5 * s;
      ctx.lineCap = "round";
      ctx.beginPath();
      ctx.moveTo(10 * s, -94 * s);
      ctx.lineTo(20 * s, -78 * s);
      ctx.lineTo(16 * s, -60 * s);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(-11 * s, -94 * s);
      ctx.lineTo(-26 * s, -80 * s);
      ctx.lineTo(-20 * s, -60 * s);
      ctx.stroke();
      // gold bracer on the crossed arm
      ctx.strokeStyle = gold;
      ctx.lineWidth = 4.6 * s;
      ctx.beginPath();
      ctx.moveTo(18 * s, -80 * s);
      ctx.lineTo(22 * s, -76 * s);
      ctx.stroke();
    }

    // soft inner glow over the whole figure
    ctx.fillStyle = `rgba(255,240,200,${0.08 * appear})`;
    ctx.beginPath();
    ctx.arc(0, -70 * s, 60 * s, 0, Math.PI * 2);
    ctx.fill();

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
    const lean = kicking ? 0.45 : pushing ? 0.85 : 0.35;
    // sprint cycle starts from a neutral stance and matches the actual run cadence
    const cyc = kicking ? Math.sin(this.kickT * 13) : Math.sin(this.roll * 2);
    const bob = pushing
      ? Math.abs(Math.cos(this.roll * 2)) * 4 * s
      : kicking
        ? Math.abs(Math.cos(this.kickT * 13)) * 1.6 * s
        : 0;

    ctx.save();
    ctx.translate(x, groundY - bob);
    ctx.rotate(ang);

    const body = "oklch(0.12 0.012 268)";
    const cloth = "oklch(0.34 0.04 62)";
    const line = "oklch(0.34 0.02 260 / 0.55)";
    const rim = "oklch(0.84 0.1 60 / 0.5)";

    // soft shadow under feet
    ctx.save();
    ctx.globalAlpha = 0.3;
    ctx.fillStyle = "oklch(0.06 0 0)";
    ctx.beginPath();
    ctx.ellipse(0, -1 * s, 15 * s, 3.5 * s, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();

    // ---- muscular legs (lunge stance, walking/running cycle) ----
    const stride = (kicking ? 6 * s : 3 * s) * cyc;
    const leg = (dir: 1 | -1, st: number) => {
      ctx.fillStyle = body;
      ctx.beginPath();
      ctx.moveTo(dir * 2 * s, -33 * s); // hip front
      ctx.lineTo(dir * (9 * s + st), -17 * s); // knee front
      ctx.lineTo(dir * (12 * s + st), -1 * s); // toe
      ctx.lineTo(dir * (4 * s + st), -1 * s); // heel
      ctx.lineTo(dir * (3 * s + st), -16 * s); // knee back
      ctx.lineTo(dir * -3 * s, -35 * s); // hip back
      ctx.closePath();
      ctx.fill();
      // calf muscle
      ctx.beginPath();
      ctx.arc(dir * (4.5 * s + st), -9 * s, 3.4 * s, 0, Math.PI * 2);
      ctx.fill();
      // knee cap
      ctx.beginPath();
      ctx.arc(dir * (6 * s + st), -16.5 * s, 2.4 * s, 0, Math.PI * 2);
      ctx.fill();
    };
    leg(1, stride);
    leg(-1, -stride);

    // leather sandals (ankle strap + sole on each foot)
    ctx.fillStyle = "oklch(0.18 0.02 40)";
    for (const d of [1, -1]) {
      const st = d * stride;
      ctx.beginPath();
      ctx.ellipse(d * (8 * s + st), -1.4 * s, 5 * s, 2 * s, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.beginPath();
      ctx.ellipse(d * (6 * s + st), -3.4 * s, 2 * s, 1.8 * s, 0, 0, Math.PI * 2);
      ctx.fill();
    }

    // ---- torso (leaning into the boulder) ----
    const hipX = 0;
    const hipY = -34 * s;
    const shX = 9 * s * lean;
    const shY = -60 * s;
    ctx.fillStyle = body;
    ctx.beginPath();
    ctx.moveTo(shX - 8 * s, shY + 1 * s);
    ctx.lineTo(hipX - 6 * s, hipY + 1 * s);
    ctx.lineTo(hipX + 8 * s, hipY + 1 * s);
    ctx.lineTo(shX + 9 * s, shY + 2 * s);
    ctx.closePath();
    ctx.fill();
    // deltoid bumps
    ctx.beginPath();
    ctx.arc(shX - 8 * s, shY + 1 * s, 4.6 * s, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.arc(shX + 9 * s, shY + 1 * s, 4.6 * s, 0, Math.PI * 2);
    ctx.fill();

    // ---- worn linen tunic (sleeveless), frayed at the hem ----
    ctx.fillStyle = "oklch(0.46 0.05 64 / 0.92)";
    ctx.beginPath();
    ctx.moveTo(shX - 8 * s, shY + 1 * s);
    ctx.lineTo(hipX - 7 * s, hipY + 3 * s);
    ctx.lineTo(hipX - 5 * s, hipY + 7 * s);
    ctx.lineTo(hipX - 2 * s, hipY + 4 * s);
    ctx.lineTo(hipX + 1 * s, hipY + 8 * s);
    ctx.lineTo(hipX + 4 * s, hipY + 5 * s);
    ctx.lineTo(hipX + 6 * s, hipY + 8 * s);
    ctx.lineTo(hipX + 8 * s, hipY + 3 * s);
    ctx.lineTo(shX + 9 * s, shY + 2 * s);
    ctx.closePath();
    ctx.fill();
    // fold creases
    ctx.strokeStyle = "oklch(0.3 0.045 62 / 0.8)";
    ctx.lineWidth = 1 * s;
    ctx.beginPath();
    ctx.moveTo(shX + 1 * s, shY + 3 * s);
    ctx.lineTo(hipX + 1 * s, hipY + 4 * s);
    ctx.moveTo(shX + 4 * s, shY + 3 * s);
    ctx.lineTo(hipX + 4 * s, hipY + 5 * s);
    ctx.stroke();
    // leather belt across the waist
    ctx.fillStyle = "oklch(0.15 0.015 42)";
    ctx.fillRect(hipX - 7 * s, hipY - 1 * s, 16 * s, 2.6 * s);
    ctx.fillStyle = "oklch(0.42 0.05 58 / 0.7)";
    ctx.fillRect(hipX - 7 * s, hipY - 1 * s, 16 * s, 0.7 * s);
    // v-neck opening showing the bare chest
    ctx.fillStyle = body;
    ctx.beginPath();
    ctx.moveTo(shX - 4 * s, shY + 2 * s);
    ctx.quadraticCurveTo(shX + 1 * s, shY - 1 * s, shX + 6 * s, shY + 2 * s);
    ctx.quadraticCurveTo(shX + 4 * s, shY + 4 * s, shX - 1 * s, shY + 4 * s);
    ctx.closePath();
    ctx.fill();

    // pec + abs definition (muscles showing through the worn linen)
    ctx.strokeStyle = line;
    ctx.lineWidth = 1.4 * s;
    ctx.beginPath();
    ctx.moveTo(shX - 4 * s, shY + 4 * s);
    ctx.quadraticCurveTo(shX + 2 * s, shY + 5 * s, shX + 6 * s, shY + 3 * s);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(shX + 2 * s, shY + 6 * s);
    ctx.lineTo(shX + 3 * s, hipY + 1 * s);
    ctx.moveTo(shX + 5 * s, shY + 5 * s);
    ctx.lineTo(shX + 5.5 * s, hipY + 1 * s);
    ctx.stroke();

    // tunic drape flaps at the hip
    const sway = Math.sin(this.t * 1.8) * 1.5 * s;
    ctx.fillStyle = cloth;
    // back flap billowing behind him
    ctx.beginPath();
    ctx.moveTo(hipX - 6 * s, hipY + 1 * s);
    ctx.quadraticCurveTo(hipX - 14 * s, hipY + sway, hipX - 17 * s, hipY + 5 * s + sway);
    ctx.quadraticCurveTo(hipX - 10 * s, hipY + 4 * s, hipX - 6 * s, hipY + 2 * s);
    ctx.closePath();
    ctx.fill();
    // front drape
    ctx.beginPath();
    ctx.moveTo(hipX + 8 * s, hipY);
    ctx.quadraticCurveTo(hipX + 12 * s, hipY + 1 * s, hipX + 10 * s, hipY + 5 * s);
    ctx.quadraticCurveTo(hipX + 6 * s, hipY + 3 * s, hipX + 6 * s, hipY - 2 * s);
    ctx.closePath();
    ctx.fill();

    if (kicking) {
      // arms held out in a steady, fixed-length reach toward the boulder
      const sx = shX + 3 * s;
      const sy = shY + 1 * s;
      // stable reach direction: forward and slightly up the slope (not locked to
      // the fast-moving boulder, which would make the arms appear to shrink/stretch)
      const ux = 0.97;
      const uy = -0.24;
      const reach = 17 * s;
      const tremble = Math.sin(this.kickT * 5) * 0.9 * s;
      for (const [side, off] of [
        [-1, -2.4 * s],
        [1, 2.4 * s],
      ] as const) {
        const hx = sx + ux * reach - uy * off + tremble * side;
        const hy = sy + uy * reach + ux * off;
        const ex = sx + ux * reach * 0.55 - uy * off * 0.6;
        const ey = sy + uy * reach * 0.55 + ux * off * 0.6;
        ctx.strokeStyle = body;
        ctx.lineCap = "round";
        // upper arm
        ctx.lineWidth = 5.6 * s;
        ctx.beginPath();
        ctx.moveTo(sx, sy);
        ctx.lineTo(ex, ey);
        ctx.stroke();
        // forearm
        ctx.lineWidth = 4.4 * s;
        ctx.beginPath();
        ctx.moveTo(ex, ey);
        ctx.lineTo(hx, hy);
        ctx.stroke();
        // reaching hand
        ctx.fillStyle = body;
        ctx.beginPath();
        ctx.arc(hx, hy, 2.8 * s, 0, Math.PI * 2);
        ctx.fill();
      }
    } else {
      // ---- arms braced against the boulder ----
      const shoulder = { x: shX + 3 * s, y: shY + 1 * s };
      const h1 = { x: bcx - R * 0.87 - x, y: bcy - R * 0.5 - groundY };
      const h2 = { x: bcx - R * 0.97 - x, y: bcy - R * 0.05 - groundY };
      const arm = (hand: { x: number; y: number }) => {
        const mid = { x: (shoulder.x + hand.x) / 2, y: (shoulder.y + hand.y) / 2 };
        const elbow = { x: mid.x - 2 * s, y: mid.y + 2 * s };
        ctx.strokeStyle = body;
        ctx.lineCap = "round";
        // upper arm (thick)
        ctx.lineWidth = 6.8 * s;
        ctx.beginPath();
        ctx.moveTo(shoulder.x, shoulder.y);
        ctx.quadraticCurveTo(elbow.x, elbow.y, hand.x, hand.y);
        ctx.stroke();
        // forearm (slightly thinner)
        ctx.lineWidth = 5 * s;
        ctx.beginPath();
        ctx.moveTo(mid.x, mid.y);
        ctx.quadraticCurveTo(elbow.x, elbow.y, hand.x, hand.y);
        ctx.stroke();
        // hand palm
        ctx.fillStyle = body;
        ctx.beginPath();
        ctx.arc(hand.x, hand.y, 3.4 * s, 0, Math.PI * 2);
        ctx.fill();
      };
      arm(h1);
      arm(h2);

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
    ctx.fillStyle = body;
    ctx.beginPath();
    ctx.moveTo(shX - 3 * s, shY + 2 * s);
    ctx.lineTo(shX + 5 * s, shY + 2 * s);
    ctx.lineTo(shX + 5 * s, shY - 5 * s);
    ctx.lineTo(shX - 3 * s, shY - 5 * s);
    ctx.closePath();
    ctx.fill();

    const hx = shX + 4 * s;
    const hy = shY - 12 * s;
    // head
    ctx.beginPath();
    ctx.arc(hx, hy, 7.5 * s, 0, Math.PI * 2);
    ctx.fill();
    // hair mass at the back
    ctx.beginPath();
    ctx.arc(hx - 3.2 * s, hy - 0.3 * s, 6.6 * s, 0, Math.PI * 2);
    ctx.fill();
    // nose
    ctx.beginPath();
    ctx.moveTo(hx + 6.5 * s, hy - 1 * s);
    ctx.lineTo(hx + 10 * s, hy + 1.5 * s);
    ctx.lineTo(hx + 6.8 * s, hy + 3 * s);
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

    // brow + straining mouth
    ctx.strokeStyle = line;
    ctx.lineWidth = 1.3 * s;
    ctx.beginPath();
    ctx.moveTo(hx + 1 * s, hy - 2 * s);
    ctx.lineTo(hx + 5 * s, hy - 2.5 * s);
    ctx.stroke();
    if (pushing) {
      ctx.beginPath();
      ctx.arc(hx + 2 * s, hy + 2 * s, 2.4 * s, Math.PI * 0.15, Math.PI * 0.85);
      ctx.stroke();
    }

    // warm rim light along the back (setting sun)
    ctx.strokeStyle = rim;
    ctx.lineWidth = 1.8 * s;
    ctx.beginPath();
    ctx.moveTo(shX - 8 * s, shY + 1 * s);
    ctx.quadraticCurveTo(shX - 5 * s, shY - 9 * s, shX - 2 * s, shY - 18 * s);
    ctx.stroke();

    ctx.restore();
  }
}
