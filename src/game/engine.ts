import { GameAudio } from "./audio";
import { QUOTES, type Quote } from "./quotes";
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

const DEFAULT_SUN = { xFrac: 0.78, yFrac: 0.6 };

const DEFAULT_CLOUDS: Cloud[] = [
  { offset: 300, yFrac: 0.1, scale: 1.3, speed: 12, parallax: 0.05, alpha: 0.45 },
  { offset: 1200, yFrac: 0.18, scale: 1.1, speed: 9, parallax: 0.07, alpha: 0.4 },
  { offset: 2100, yFrac: 0.08, scale: 1.6, speed: 15, parallax: 0.04, alpha: 0.5 },
];

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

  /** prometheus scene: eagle loop phase + wind streak seed */
  private prom = { phase: Math.random() * 20, wind: Math.random() * 40 };

  /** the random aphorism currently written in the sky (assigned at each summit) */
  private currentQuote: Quote | null = null;
  /** shuffled deck of quote indices, reshuffled when emptied */
  private quoteDeck: number[] = [];

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
    if (this.quoteDeck.length === 0) {
      const deck = QUOTES.map((_, i) => i);
      for (let i = deck.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        const a = deck[i]!;
        deck[i] = deck[j]!;
        deck[j] = a;
      }
      this.quoteDeck = deck;
    }
    return QUOTES[this.quoteDeck.pop()!]!;
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
      levelName: this.level.name,
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
    this.drawPrometheus();
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
    groundG.addColorStop(0, "oklch(0.36 0.045 58)");
    groundG.addColorStop(1, "oklch(0.26 0.035 45)");
    ctx.fillStyle = groundG;
    ctx.fill();
    ctx.strokeStyle = "oklch(0.62 0.05 60 / 0.55)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    for (let sx = -2; sx <= w + 2; sx += 4) {
      const wx = this.camX + sx / this.scale;
      const y = toScreenY(terrainAt(L, wx));
      if (sx === -2) ctx.moveTo(sx, y);
      else ctx.lineTo(sx, y);
    }
    ctx.stroke();

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

    // warm golden glow radiating from the break in the storm clouds
    const glow = ctx.createRadialGradient(sunX, sunY, 0, sunX, sunY, w * 0.55);
    glow.addColorStop(0, "oklch(0.9 0.12 75 / 0.55)");
    glow.addColorStop(0.35, "oklch(0.84 0.1 70 / 0.22)");
    glow.addColorStop(1, "oklch(0.8 0.08 65 / 0)");
    ctx.fillStyle = glow;
    ctx.fillRect(0, 0, w, h);

    // sun disc blazing through the break
    const sunR = 26 * s;
    const disc = ctx.createRadialGradient(sunX, sunY, sunR * 0.1, sunX, sunY, sunR);
    disc.addColorStop(0, "oklch(0.97 0.08 78)");
    disc.addColorStop(0.6, "oklch(0.92 0.11 70)");
    disc.addColorStop(1, "oklch(0.82 0.14 60)");
    ctx.fillStyle = disc;
    ctx.beginPath();
    ctx.arc(sunX, sunY, sunR, 0, Math.PI * 2);
    ctx.fill();

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

    // volumetric god rays streaming down toward the trail and the boulder
    ctx.fillStyle = "oklch(0.88 0.1 74 / 0.1)";
    for (let i = 0; i < 7; i++) {
      const a0 = 1.05 + (i / 7) * 1.7 + Math.sin(t * 0.25 + i * 1.7) * 0.04;
      const a1 = a0 + 0.07;
      const len = w * 1.7;
      ctx.beginPath();
      ctx.moveTo(sunX, sunY);
      ctx.lineTo(sunX + Math.cos(a0) * len, sunY + Math.sin(a0) * len);
      ctx.lineTo(sunX + Math.cos(a1) * len, sunY + Math.sin(a1) * len);
      ctx.closePath();
      ctx.fill();
    }

    // soft warm veil spilling out of the break over the valley
    const veil = ctx.createLinearGradient(0, h * 0.3, 0, h * 0.8);
    veil.addColorStop(0, "rgba(255,200,130,0)");
    veil.addColorStop(0.5, "rgba(255,190,120,0.1)");
    veil.addColorStop(1, "rgba(255,180,110,0)");
    ctx.fillStyle = veil;
    ctx.fillRect(0, h * 0.3, w, h * 0.5);
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

    const words = q.text.split(" ");
    const lines: string[] = [];
    let cur = "";
    for (const word of words) {
      const test = cur ? `${cur} ${word}` : word;
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
    for (const c of clouds) {
      const x = (((c.offset - this.camX * c.parallax + this.t * c.speed) % wrap) + wrap) % wrap - 160;
      const y = h * c.yFrac + Math.sin(this.t * 0.1 + c.offset * 0.01) * 6;
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
    ctx.globalAlpha = alpha;

    this.cloudSilhouette(ctx, x, y, s * 1.28);
    ctx.fillStyle = "oklch(0.9 0.015 235)";
    ctx.fill();
    ctx.globalAlpha = alpha * 0.3;

    this.cloudSilhouette(ctx, x, y, s);
    const body = ctx.createLinearGradient(0, y - 34 * s, 0, y + 16 * s);
    body.addColorStop(0, "oklch(0.68 0.03 245)");
    body.addColorStop(0.45, "oklch(0.57 0.035 246)");
    body.addColorStop(1, "oklch(0.45 0.045 250)");
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
    const shade = ctx.createLinearGradient(0, y - 2 * s, 0, y + 16 * s);
    shade.addColorStop(0, "oklch(0 0 0 / 0)");
    shade.addColorStop(1, "oklch(0.12 0.05 265 / 0.4)");
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

    ctx.beginPath();
    ctx.moveTo(farX(lx), h + 2);
    ctx.lineTo(farX(lx), farY(baseY));
    ctx.lineTo(farX(p1x), farY(p1y));
    ctx.lineTo(farX(p2x), farY(p2y));
    ctx.lineTo(farX(rx), farY(baseY));
    ctx.lineTo(farX(rx), h + 2);
    ctx.closePath();
    ctx.fillStyle = mt.color;
    ctx.fill();

    // side shading
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

    // warm light rim on the peaks facing the cloud break
    ctx.strokeStyle = "oklch(0.82 0.09 68 / 0.45)";
    ctx.lineWidth = 3;
    ctx.lineJoin = "round";
    ctx.beginPath();
    ctx.moveTo(farX(p1x), farY(p1y));
    ctx.lineTo(farX(p2x), farY(p2y));
    ctx.stroke();

    if (mt.snow) {
      const sw = mt.width * 0.16;
      ctx.fillStyle = "oklch(0.94 0.015 80 / 0.55)";
      ctx.beginPath();
      ctx.moveTo(farX(p1x), farY(p1y));
      ctx.lineTo(farX(p1x - sw * 0.6), farY(p1y - mt.height * 0.14));
      ctx.lineTo(farX(p1x + sw * 0.7), farY(p1y - mt.height * 0.05));
      ctx.closePath();
      ctx.fill();
    }
  }

  /** Prometheus chained to the distant summit, with Zeus's eagle looping in to peck */
  private drawPrometheus() {
    const mt = this.level.mountain;
    if (!mt) return;
    const ctx = this.ctx;
    const { w } = this;
    const p = mt.parallax;
    const s = this.scale;
    const camX = this.camX;
    const camY = this.camY;
    const farX = (wx: number) => (wx - camX * p) * s;
    const farY = (wy: number) =>
      this.groundY - (wy - camY * p) * s * 0.5 + this.shakeY * 0.4;

    // the mountain's left summit, where the crag rises
    const ax = mt.x - mt.width * 0.18;
    const ay = mt.height * 0.12 + mt.height;
    const px = farX(ax);
    const py = farY(ay) - 6 * s;
    if (px < -360 || px > w + 360) return;

    const U = Math.max(0.5, s * 1.0);
    const t = this.t;
    const sun = this.level.sun ?? DEFAULT_SUN;
    const toSun = sun.xFrac >= 0.5 ? 1 : -1;

    // eagle flight path: [dx, dy, size, flap] in units of U (closed Catmull-Rom loop)
    const E: Array<[number, number, number, number]> = [
      [340, -250, 0.5, 0.4],
      [250, -210, 0.72, 0.5],
      [165, -155, 0.95, 0.3],
      [95, -100, 1.08, 0.18],
      [45, -48, 1.2, 0.12],
      [8, -24, 1.32, 0.1],
      [-20, -30, 1.28, 0.65],
      [-95, -90, 1.02, 0.85],
      [-210, -165, 0.72, 0.75],
      [-320, -205, 0.48, 0.4],
    ];
    const n = E.length;
    const sample = (u: number) => {
      const seg = u * n;
      const fi = Math.floor(seg);
      const i = fi % n;
      const f = seg - fi;
      const p0 = E[(i - 1 + n) % n]!;
      const p1 = E[i]!;
      const p2 = E[(i + 1) % n]!;
      const p3 = E[(i + 2) % n]!;
      const cr = (a: number, b: number, c: number, d: number) =>
        0.5 *
        (2 * b +
          (-a + c) * f +
          (2 * a - 5 * b + 4 * c - d) * f * f +
          (-a + 3 * b - 3 * c + d) * f * f * f);
      return [
        cr(p0[0], p1[0], p2[0], p3[0]),
        cr(p0[1], p1[1], p2[1], p3[1]),
        cr(p0[2], p1[2], p2[2], p3[2]),
        cr(p0[3], p1[3], p2[3], p3[3]),
      ] as const;
    };

    const per = 9;
    const u = ((((t + this.prom.phase) % per) + per) % per) / per;
    const ex = sample(u);
    const en = sample((u + 0.012) % 1);
    const heading = Math.atan2(en[1] - ex[1], en[0] - ex[0]);
    const peck = Math.exp(-Math.pow(u - 0.556, 2) * 2600);

    ctx.save();
    ctx.translate(px, py);
    const sway = Math.sin(t * 0.55 + this.prom.phase) * 0.05;

    // thin mist clinging to the summit
    const mist = ctx.createRadialGradient(0, -22 * U, 2, 0, -22 * U, 70 * U);
    mist.addColorStop(0, "oklch(0.9 0.02 240 / 0.15)");
    mist.addColorStop(1, "oklch(0.9 0.02 240 / 0)");
    ctx.fillStyle = mist;
    ctx.fillRect(-80 * U, -100 * U, 160 * U, 110 * U);

    // rocky crag
    ctx.beginPath();
    ctx.moveTo(-15 * U, 2 * U);
    ctx.lineTo(-11 * U, -20 * U);
    ctx.lineTo(-6 * U, -34 * U);
    ctx.lineTo(-2 * U, -44 * U);
    ctx.lineTo(4 * U, -38 * U);
    ctx.lineTo(10 * U, -24 * U);
    ctx.lineTo(15 * U, -8 * U);
    ctx.lineTo(14 * U, 2 * U);
    ctx.closePath();
    ctx.fillStyle = "oklch(0.23 0.03 275)";
    ctx.fill();
    ctx.strokeStyle = "oklch(0.85 0.08 70 / 0.5)";
    ctx.lineWidth = 1.2;
    ctx.lineJoin = "round";
    ctx.beginPath();
    ctx.moveTo(-11 * U, -20 * U);
    ctx.lineTo(-6 * U, -34 * U);
    ctx.lineTo(-2 * U, -44 * U);
    ctx.lineTo(4 * U, -38 * U);
    ctx.stroke();

    // the bound figure — a clear human silhouette straining against the chains
    const Lw = Math.max(1.4, U * 0.72);
    const bodyC = "oklch(0.34 0.035 286)";
    ctx.save();
    ctx.translate(-peck * 2.5, 0);
    ctx.rotate(sway);
    ctx.lineCap = "round";
    ctx.lineJoin = "round";

    // legs
    ctx.strokeStyle = bodyC;
    ctx.lineWidth = Lw;
    ctx.beginPath();
    ctx.moveTo(2.2 * U, -17 * U);
    ctx.lineTo(0.4 * U, -1 * U);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(5 * U, -17 * U);
    ctx.lineTo(6.8 * U, -2 * U);
    ctx.stroke();

    // torso — thick capsule from hips to shoulders
    ctx.lineWidth = Lw * 1.55;
    ctx.beginPath();
    ctx.moveTo(3.6 * U, -17 * U);
    ctx.lineTo(3.6 * U, -37 * U);
    ctx.stroke();

    // arms raised overhead, taut against the chains
    ctx.lineWidth = Lw;
    ctx.beginPath();
    ctx.moveTo(1.6 * U, -36 * U);
    ctx.lineTo(-4.5 * U, -45 * U);
    ctx.lineTo(-11 * U, -54 * U);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(5.6 * U, -36 * U);
    ctx.lineTo(9.5 * U, -42 * U);
    ctx.lineTo(13.5 * U, -48 * U);
    ctx.stroke();

    // neck + head
    ctx.lineWidth = U * 0.9;
    ctx.beginPath();
    ctx.moveTo(3.6 * U, -37 * U);
    ctx.lineTo(3.6 * U, -42 * U);
    ctx.stroke();
    ctx.fillStyle = bodyC;
    ctx.beginPath();
    ctx.arc(3.4 * U, -44.5 * U, 4.5 * U, 0, Math.PI * 2);
    ctx.fill();

    // beard
    ctx.beginPath();
    ctx.moveTo(-0.2 * U, -42 * U);
    ctx.quadraticCurveTo(3.4 * U, -35.5 * U, 6.6 * U, -42.5 * U);
    ctx.quadraticCurveTo(3.4 * U, -31.5 * U, -0.2 * U, -42 * U);
    ctx.fill();

    // hair streaming with the wind
    ctx.strokeStyle = "oklch(0.24 0.02 286)";
    ctx.lineWidth = Math.max(1, U * 0.5);
    ctx.beginPath();
    ctx.moveTo(2.8 * U, -49 * U);
    ctx.quadraticCurveTo(5.5 * U, -50.5 * U, 7 * U, -49 * U);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(3.4 * U, -49.5 * U);
    ctx.quadraticCurveTo(6.5 * U, -52 * U, 8.2 * U, -50.5 * U);
    ctx.stroke();

    // loincloth
    ctx.fillStyle = bodyC;
    ctx.beginPath();
    ctx.moveTo(0.6 * U, -18 * U);
    ctx.lineTo(4.4 * U, -18.5 * U);
    ctx.lineTo(6.2 * U, -10 * U);
    ctx.lineTo(1.2 * U, -10 * U);
    ctx.closePath();
    ctx.fill();

    // warm rim light on the sun-facing side
    ctx.strokeStyle = `oklch(0.92 0.09 68 / ${toSun > 0 ? 0.75 : 0.5})`;
    ctx.lineWidth = Math.max(0.8, U * 0.32);
    ctx.beginPath();
    ctx.moveTo(5.6 * U, -36 * U);
    ctx.lineTo(9.5 * U, -42 * U);
    ctx.lineTo(13.5 * U, -48 * U);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(5 * U, -17 * U);
    ctx.lineTo(6.8 * U, -2 * U);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(6.6 * U, -17 * U);
    ctx.lineTo(5.6 * U, -36 * U);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(3.4 * U, -44.5 * U, 4.5 * U, -0.85, 0.5);
    ctx.stroke();

    // the eternal wound on his right side
    const wound = 1 + peck * 2 + Math.sin(t * 2.2 + 1) * 0.15;
    const wg = ctx.createRadialGradient(5.5 * U, -23 * U, 0.3, 5.5 * U, -23 * U, 9 * U * wound);
    wg.addColorStop(0, "oklch(0.65 0.2 25 / 0.65)");
    wg.addColorStop(1, "oklch(0.65 0.2 25 / 0)");
    ctx.fillStyle = wg;
    ctx.fillRect(-5 * U, -33 * U, 20 * U, 24 * U);
    ctx.fillStyle = "oklch(0.5 0.22 24 / 0.95)";
    ctx.beginPath();
    ctx.arc(5.5 * U, -23 * U, 1.9 * U, 0, Math.PI * 2);
    ctx.fill();

    // smoke wisps rising from the wound
    ctx.strokeStyle = "oklch(0.82 0.02 250 / 0.28)";
    ctx.lineWidth = 1;
    for (let i = 0; i < 2; i++) {
      const st = (t * 0.4 + i * 0.5 + this.prom.wind * 0.1) % 1;
      const sx = 5.5 * U + Math.sin(t * 1.1 + i * 2.7) * 1.6 * U;
      const sy = -23 * U - st * 13 * U;
      ctx.globalAlpha = 0.3 * (1 - st);
      ctx.beginPath();
      ctx.moveTo(sx, -21 * U);
      ctx.quadraticCurveTo(sx + Math.sin(t * 0.8 + i * 3) * 2.5 * U, sy + 4 * U, sx + Math.sin(t * 1.5 + i) * 3.5 * U, sy);
      ctx.stroke();
    }
    ctx.globalAlpha = 1;
    ctx.restore();

    // chains wrapping the wrists to the crag
    ctx.strokeStyle = "oklch(0.6 0.02 280 / 0.95)";
    ctx.lineWidth = Math.max(1, U * 0.34);
    ctx.lineCap = "round";
    ctx.beginPath();
    ctx.moveTo(-12 * U, -52 * U);
    ctx.lineTo(-10 * U, -49 * U);
    ctx.lineTo(-8.5 * U, -48 * U);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(-8.5 * U, -48 * U);
    ctx.lineTo(-7 * U, -45 * U);
    ctx.lineTo(-6 * U, -44 * U);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(13 * U, -46 * U);
    ctx.lineTo(11 * U, -41 * U);
    ctx.lineTo(9 * U, -37 * U);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(9 * U, -37 * U);
    ctx.lineTo(8 * U, -34 * U);
    ctx.lineTo(7 * U, -32 * U);
    ctx.stroke();

    // ---- Zeus's eagle ----
    const sz = 36 * U * ex[2];
    if (sz > 2.5) {
      const k = sz * 0.17;
      const flapI = ex[3];
      const wingFreq = 6 + 6 * flapI;
      const wingUp = Math.sin(t * wingFreq) * (0.25 + 0.6 * flapI);
      const fillC = "oklch(0.2 0.025 272 / 0.98)";
      const rimC = "oklch(0.9 0.09 68 / 0.6)";
      ctx.save();
      ctx.translate(ex[0] * U, ex[1] * U);
      ctx.rotate(heading);

      const wing = (side: 1 | -1, tone: number) => {
        ctx.fillStyle = tone < 0.5 ? fillC : "oklch(0.26 0.02 272 / 0.98)";
        const W = 5.8 * k;
        const tipX = 2.1 * k;
        const rootX = -0.9 * k;
        const tipY = side * W + wingUp * 1.4 * k;
        ctx.beginPath();
        ctx.moveTo(rootX, side * 0.3 * k);
        // leading edge
        ctx.quadraticCurveTo(0.3 * k, side * W * 0.62 + wingUp * 1.4 * k, tipX, tipY);
        // feathered wingtip: finger feathers along the trailing edge
        const F = 4;
        for (let f = 0; f < F; f++) {
          const t1 = (f + 0.45) / F;
          const t2 = (f + 1) / F;
          ctx.lineTo(tipX - t1 * 3.4 * k, tipY - side * t1 * 2.0 * k + side * 0.55 * k);
          ctx.lineTo(tipX - t2 * 3.4 * k, tipY - side * t2 * 2.0 * k);
        }
        // inner edge back to the body
        ctx.quadraticCurveTo(-0.1 * k, side * 0.7 * k, rootX, side * 0.35 * k);
        ctx.closePath();
        ctx.fill();
        // warm glint along the leading edge
        ctx.strokeStyle = rimC;
        ctx.lineWidth = Math.max(0.7, k * 0.12);
        ctx.beginPath();
        ctx.moveTo(rootX, side * 0.3 * k);
        ctx.quadraticCurveTo(0.3 * k, side * W * 0.62 + wingUp * 1.4 * k, tipX, tipY);
        ctx.stroke();
      };

      // far wing
      wing(-1, 0.4);
      // tail
      ctx.fillStyle = fillC;
      ctx.beginPath();
      ctx.moveTo(-2.6 * k, -0.9 * k);
      ctx.lineTo(-6.4 * k, -0.5 * k);
      ctx.lineTo(-5.6 * k, 0);
      ctx.lineTo(-6.4 * k, 0.5 * k);
      ctx.lineTo(-2.6 * k, 0.9 * k);
      ctx.closePath();
      ctx.fill();
      // body
      ctx.beginPath();
      ctx.ellipse(0, 0, 3.4 * k, 1.5 * k, 0, 0, Math.PI * 2);
      ctx.fill();
      // near wing
      wing(1, 0.7);
      // head + hooked beak
      ctx.fillStyle = fillC;
      ctx.beginPath();
      ctx.arc(3.3 * k, -0.7 * k, 1.55 * k, 0, Math.PI * 2);
      ctx.fill();
      ctx.beginPath();
      ctx.moveTo(3.5 * k, -0.7 * k);
      ctx.quadraticCurveTo(5.4 * k + peck * 0.9 * k, -0.3 * k, 4.7 * k + peck * 0.95 * k, 0.85 * k);
      ctx.quadraticCurveTo(4.0 * k, 1.05 * k, 3.5 * k, 0.6 * k);
      ctx.closePath();
      ctx.fill();
      // eye glint
      ctx.fillStyle = "oklch(0.95 0.02 70 / 0.9)";
      ctx.beginPath();
      ctx.arc(4.05 * k, -0.85 * k, 0.28 * k, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }

    // strike flash on the wound as the eagle pecks
    if (peck > 0.2) {
      ctx.save();
      ctx.globalAlpha = Math.min(1, peck * 1.6);
      const pg = ctx.createRadialGradient(5.5 * U, -23 * U, 0.2, 5.5 * U, -23 * U, 7 * U);
      pg.addColorStop(0, "oklch(0.75 0.22 30)");
      pg.addColorStop(1, "oklch(0.75 0.22 30 / 0)");
      ctx.fillStyle = pg;
      ctx.fillRect(-4 * U, -31 * U, 19 * U, 17 * U);
      ctx.restore();
    }

    // drifting wind streaks
    ctx.save();
    ctx.globalAlpha = 0.13;
    ctx.strokeStyle = "oklch(0.95 0.01 230)";
    ctx.lineWidth = 1;
    for (let i = 0; i < 4; i++) {
      const seed = this.prom.wind + i * 7.13;
      const wt = ((t * 0.055 + seed) % 1 + 1) % 1;
      const wx0 = -70 * U + wt * 150 * U;
      const wy0 = -52 * U + ((seed * 13.7) % 1) * 48 * U + Math.sin(t * 0.9 + seed) * 3;
      ctx.beginPath();
      ctx.moveTo(wx0, wy0);
      ctx.lineTo(wx0 + 26 * U, wy0 - 8 * U);
      ctx.stroke();
    }
    ctx.restore();

    ctx.restore();
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

  private drawMist() {
    const ctx = this.ctx;
    const { w, h } = this;
    const t = this.t;
    const layers: Array<{ y: number; a: number; hh: number; sp: number }> = [
      { y: h * 0.52, a: 0.13, hh: 34, sp: 0.12 },
      { y: h * 0.6, a: 0.16, hh: 48, sp: 0.09 },
    ];
    for (const L of layers) {
      ctx.save();
      ctx.globalAlpha = L.a;
      ctx.fillStyle = "#c9cdd6";
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
      ctx.fillStyle = `oklch(0.3 0.025 60 / ${0.55 + h1 * 0.3})`;
      ctx.beginPath();
      ctx.ellipse(px, py, sz, sz * 0.62, (h1 - 0.5) * 0.6, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "oklch(0.5 0.03 70 / 0.35)";
      ctx.beginPath();
      ctx.ellipse(px - sz * 0.15, py - sz * 0.3, sz * 0.4, sz * 0.2, 0, 0, Math.PI * 2);
      ctx.fill();
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
    const lean = kicking ? 0.3 : pushing ? 0.85 : 0.35;
    const cyc = kicking ? Math.sin(this.t * 16) : Math.sin(this.roll * 2);
    const bob = pushing
      ? Math.abs(Math.cos(this.roll * 2)) * 4 * s
      : kicking
        ? Math.abs(Math.cos(this.t * 16)) * 5 * s
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
      // sprinting: arms pumping opposite the legs
      ctx.lineCap = "round";
      for (const d of [1, -1] as const) {
        const swing = cyc * d;
        const sx = shX + 3 * s;
        const sy = shY + 1 * s;
        const ex = sx + 2 * s + swing * 6 * s;
        const ey = sy + 6 * s;
        const hx = ex + swing * 7 * s;
        const hy = ey + 3.5 * s;
        ctx.strokeStyle = body;
        ctx.lineWidth = 5.2 * s;
        ctx.beginPath();
        ctx.moveTo(sx, sy);
        ctx.lineTo(ex, ey);
        ctx.stroke();
        ctx.lineWidth = 3.8 * s;
        ctx.beginPath();
        ctx.moveTo(ex, ey);
        ctx.lineTo(hx, hy);
        ctx.stroke();
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
