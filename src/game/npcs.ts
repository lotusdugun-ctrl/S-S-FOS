/**
 * The others on the hill, painted in the same hand as Sisyphus but smaller
 * and simpler: they are scenery he passes, not figures to be played.
 *
 * Every function draws with its feet (or its seat) at (x, y), facing left —
 * down the slope, toward where he comes from — and `s` is the scene's scale.
 */
import type { RGB } from "./sky";

export type NpcLight = { color: RGB; strength: number; t: number; dim: number };

const rgba = (c: RGB, a: number) => `rgba(${c[0] | 0}, ${c[1] | 0}, ${c[2] | 0}, ${a.toFixed(3)})`;

/** a soft rim of sunlight along one side of whatever was just pathed */
function rim(ctx: CanvasRenderingContext2D, L: NpcLight, w: number) {
  if (L.strength < 0.05) return;
  ctx.save();
  ctx.strokeStyle = rgba(L.color, 0.22 * L.strength);
  ctx.lineWidth = w;
  ctx.stroke();
  ctx.restore();
}

const SKIN = "oklch(0.52 0.08 50)";
const SKIN_OLD = "oklch(0.5 0.06 45)";

function head(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  r: number,
  hair: string,
  skin: string,
  hairStyle: "short" | "veil" | "grey" | "curls",
) {
  ctx.fillStyle = skin;
  ctx.beginPath();
  ctx.ellipse(x, y, r * 0.9, r, 0, 0, Math.PI * 2);
  ctx.fill();
  // the nose, toward the left
  ctx.beginPath();
  ctx.moveTo(x - r * 0.8, y - r * 0.1);
  ctx.lineTo(x - r * 1.15, y + r * 0.25);
  ctx.lineTo(x - r * 0.8, y + r * 0.35);
  ctx.fill();
  ctx.fillStyle = hair;
  ctx.beginPath();
  if (hairStyle === "veil") {
    ctx.moveTo(x - r * 0.75, y - r * 0.6);
    ctx.quadraticCurveTo(x, y - r * 1.6, x + r * 1.1, y - r * 0.2);
    ctx.quadraticCurveTo(x + r * 1.5, y + r * 1.6, x + r * 0.9, y + r * 2.4);
    ctx.lineTo(x + r * 0.2, y + r * 1.2);
    ctx.quadraticCurveTo(x + r * 0.5, y - r * 0.2, x - r * 0.75, y - r * 0.6);
  } else {
    ctx.ellipse(x + r * 0.2, y - r * 0.35, r * 0.95, r * 0.75, 0.2, Math.PI, Math.PI * 2.15);
    if (hairStyle === "curls" || hairStyle === "grey") {
      for (let i = 0; i < 5; i++) {
        ctx.moveTo(x + r * (0.9 - i * 0.05), y - r * (0.5 - i * 0.35));
        ctx.arc(x + r * 0.75, y - r * (0.5 - i * 0.32), r * 0.32, 0, Math.PI * 2);
      }
    }
  }
  ctx.fill();
  // the eye
  ctx.fillStyle = "rgba(25, 12, 8, 0.8)";
  ctx.beginPath();
  ctx.arc(x - r * 0.45, y - r * 0.05, r * 0.11, 0, Math.PI * 2);
  ctx.fill();
}

/** a rock to sit on */
function seat(ctx: CanvasRenderingContext2D, x: number, y: number, s: number, L: NpcLight) {
  ctx.fillStyle = "oklch(0.33 0.025 50)";
  ctx.beginPath();
  ctx.moveTo(x - 14 * s, y);
  ctx.quadraticCurveTo(x - 15 * s, y - 13 * s, x - 2 * s, y - 14 * s);
  ctx.quadraticCurveTo(x + 13 * s, y - 14 * s, x + 15 * s, y);
  ctx.closePath();
  ctx.fill();
}

/**
 * Ione at her age: 1 a girl of eight, 2 a woman in a veil, 3 an old woman with
 * a stick. `talking` lifts her head and moves a hand.
 */
export function drawIone(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  age: 1 | 2 | 3,
  talking: boolean,
  L: NpcLight,
) {
  ctx.save();
  seat(ctx, x + 4 * s, y, s, L);
  const k = (age === 1 ? 0.62 : age === 2 ? 0.9 : 0.86) * 1.25;
  const sy = y - 13 * s; // the seat
  const bob = Math.sin(L.t * 1.3) * 0.4 * s;
  const lean = age === 3 ? 4 * s : 0;
  const dress =
    age === 1 ? "oklch(0.6 0.09 70)" : age === 2 ? "oklch(0.45 0.1 35)" : "oklch(0.32 0.03 260)";
  // legs, down the front of the rock
  ctx.fillStyle = dress;
  ctx.beginPath();
  ctx.moveTo(x - 2 * s, sy);
  ctx.quadraticCurveTo(x - 10 * s * k, sy + 2 * s, x - 11 * s * k, y - 1 * s);
  ctx.lineTo(x - 4 * s * k, y - 1 * s);
  ctx.quadraticCurveTo(x - 2 * s, sy + 6 * s, x + 6 * s * k, sy + 2 * s);
  ctx.fill();
  // body
  ctx.beginPath();
  ctx.moveTo(x - 4 * s * k, sy + 1 * s);
  ctx.quadraticCurveTo(
    x - 7 * s * k - lean,
    sy - 14 * s * k,
    x - 2 * s * k - lean,
    sy - 22 * s * k + bob,
  );
  ctx.lineTo(x + 5 * s * k - lean * 0.5, sy - 21 * s * k + bob);
  ctx.quadraticCurveTo(x + 9 * s * k, sy - 10 * s * k, x + 7 * s * k, sy + 1 * s);
  ctx.closePath();
  ctx.fill();
  rim(ctx, L, 1.1 * s);
  // arm: on the knee, or raised a little as she talks
  const wave = talking ? Math.sin(L.t * 3) * 2 * s : 0;
  ctx.strokeStyle = age === 3 ? SKIN_OLD : SKIN;
  ctx.lineWidth = 2.6 * s * k;
  ctx.lineCap = "round";
  ctx.beginPath();
  ctx.moveTo(x - 1 * s * k - lean, sy - 18 * s * k + bob);
  ctx.quadraticCurveTo(
    x - 8 * s * k - lean,
    sy - 10 * s * k,
    x - 10 * s * k,
    sy - (talking ? 10 : 2) * s * k + wave,
  );
  ctx.stroke();
  if (age === 3) {
    // her stick
    ctx.strokeStyle = "oklch(0.3 0.04 50)";
    ctx.lineWidth = 1.4 * s;
    ctx.beginPath();
    ctx.moveTo(x - 10 * s * k, sy - 3 * s * k);
    ctx.lineTo(x - 14 * s * k, y);
    ctx.stroke();
  }
  // head
  const hx = x + 1 * s * k - lean;
  const hy = sy - 27 * s * k + bob - (talking ? 0.6 * s : 0);
  head(
    ctx,
    hx,
    hy,
    5.2 * s * k * (age === 1 ? 1.2 : 1),
    age === 3 ? "oklch(0.78 0.01 80)" : age === 2 ? "oklch(0.4 0.04 250)" : "oklch(0.2 0.03 40)",
    age === 3 ? SKIN_OLD : SKIN,
    age === 2 ? "veil" : age === 3 ? "grey" : "short",
  );
  ctx.restore();
}

/** Lykos, her grandson: a small boy standing beside the rock */
export function drawLykos(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  L: NpcLight,
) {
  ctx.save();
  const k = 0.55;
  const bob = Math.abs(Math.sin(L.t * 2.1)) * 0.6 * s;
  ctx.strokeStyle = SKIN;
  ctx.lineCap = "round";
  ctx.lineWidth = 3 * s * k;
  ctx.beginPath();
  ctx.moveTo(x - 2 * s * k, y - 22 * s * k);
  ctx.lineTo(x - 3 * s * k, y);
  ctx.moveTo(x + 2 * s * k, y - 22 * s * k);
  ctx.lineTo(x + 3 * s * k, y);
  ctx.stroke();
  ctx.fillStyle = "oklch(0.62 0.07 140)";
  ctx.beginPath();
  ctx.moveTo(x - 6 * s * k, y - 18 * s * k);
  ctx.lineTo(x - 5 * s * k, y - 42 * s * k - bob);
  ctx.lineTo(x + 5 * s * k, y - 42 * s * k - bob);
  ctx.lineTo(x + 6 * s * k, y - 18 * s * k);
  ctx.closePath();
  ctx.fill();
  rim(ctx, L, 1 * s);
  head(ctx, x, y - 49 * s * k - bob, 6.5 * s * k, "oklch(0.25 0.04 45)", SKIN, "curls");
  ctx.restore();
}

/**
 * Prometheus, chained by both wrists to a spire of rock, on his knees. Now and
 * then he hauls at the chains.
 */
export function drawPrometheus(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  L: NpcLight,
) {
  ctx.save();
  // the spire
  const rock = ctx.createLinearGradient(x - 4 * s, 0, x + 26 * s, 0);
  rock.addColorStop(0, "oklch(0.3 0.02 40)");
  rock.addColorStop(1, "oklch(0.22 0.02 40)");
  ctx.fillStyle = rock;
  ctx.beginPath();
  ctx.moveTo(x - 4 * s, y);
  ctx.lineTo(x + 1 * s, y - 52 * s);
  ctx.lineTo(x + 5 * s, y - 72 * s);
  ctx.lineTo(x + 12 * s, y - 78 * s);
  ctx.lineTo(x + 17 * s, y - 64 * s);
  ctx.lineTo(x + 21 * s, y - 50 * s);
  ctx.lineTo(x + 28 * s, y);
  ctx.closePath();
  ctx.fill();
  // cracks in the rock
  ctx.strokeStyle = "rgba(0, 0, 0, 0.3)";
  ctx.lineWidth = 0.8 * s;
  ctx.beginPath();
  ctx.moveTo(x + 8 * s, y - 70 * s);
  ctx.lineTo(x + 11 * s, y - 50 * s);
  ctx.lineTo(x + 7 * s, y - 30 * s);
  ctx.moveTo(x + 19 * s, y - 40 * s);
  ctx.lineTo(x + 15 * s, y - 20 * s);
  ctx.stroke();
  const haul = Math.max(0, Math.sin(L.t * 0.7)) ** 6;
  const kx = x - 6 * s;
  const ky = y - 1 * s;
  // kneeling legs
  ctx.fillStyle = SKIN;
  ctx.beginPath();
  ctx.ellipse(kx - 2 * s, ky - 4 * s, 9 * s, 4 * s, -0.2, 0, Math.PI * 2);
  ctx.fill();
  // torso, leaning out from the rock, arms up and back to the chains
  const sx = kx - 4 * s - haul * 2 * s;
  const sy = ky - 30 * s + haul * 1.5 * s;
  ctx.beginPath();
  ctx.moveTo(kx - 6 * s, ky - 5 * s);
  ctx.quadraticCurveTo(kx - 10 * s, ky - 18 * s, sx - 6 * s, sy);
  ctx.lineTo(sx + 7 * s, sy - 1 * s);
  ctx.quadraticCurveTo(kx + 6 * s, ky - 16 * s, kx + 4 * s, ky - 5 * s);
  ctx.closePath();
  ctx.fill();
  rim(ctx, L, 1.2 * s);
  // a red rag of a cloak at the hip
  ctx.fillStyle = "oklch(0.42 0.13 28)";
  ctx.beginPath();
  ctx.moveTo(kx - 8 * s, ky - 9 * s);
  ctx.quadraticCurveTo(kx, ky - 13 * s, kx + 6 * s, ky - 8 * s);
  ctx.lineTo(kx + 9 * s, ky + 1 * s);
  ctx.lineTo(kx - 10 * s, ky);
  ctx.closePath();
  ctx.fill();
  // arms to the chains on the rock
  const wr = [
    [x + 6 * s, y - 62 * s],
    [x + 18 * s, y - 56 * s],
  ];
  ctx.strokeStyle = SKIN;
  ctx.lineWidth = 3.6 * s;
  ctx.lineCap = "round";
  ctx.beginPath();
  ctx.moveTo(sx - 4 * s, sy + 1 * s);
  ctx.quadraticCurveTo(sx - 4 * s, sy - 14 * s, wr[0]![0]! - 3 * s, wr[0]![1]! + 6 * s);
  ctx.moveTo(sx + 5 * s, sy);
  ctx.quadraticCurveTo(sx + 12 * s, sy - 10 * s, wr[1]![0]! - 3 * s, wr[1]![1]! + 6 * s);
  ctx.stroke();
  // chains: a few links each, iron grey with a glint
  ctx.strokeStyle = "oklch(0.55 0.01 250)";
  ctx.lineWidth = 1 * s;
  for (const [cx, cy] of wr) {
    for (let i = 0; i < 3; i++) {
      ctx.beginPath();
      ctx.ellipse(
        cx! - 2 * s + i * 0.6 * s,
        cy! + 5 * s - i * 2.6 * s,
        1.1 * s,
        1.6 * s,
        0.3,
        0,
        Math.PI * 2,
      );
      ctx.stroke();
    }
  }
  // head thrown back, a heavy beard
  head(ctx, sx - 1 * s, sy - 7 * s, 5.6 * s, "oklch(0.22 0.03 40)", SKIN, "curls");
  ctx.fillStyle = "oklch(0.22 0.03 40)";
  ctx.beginPath();
  ctx.ellipse(sx - 2 * s, sy - 1.5 * s, 4 * s, 4.5 * s, 0.3, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();
}

/**
 * Tantalus, chest-deep in a pool under a fig tree. The water drops away from
 * him and the branch lifts out of reach, over and over.
 */
export function drawTantalus(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  L: NpcLight,
) {
  ctx.save();
  const cyc = (((L.t * 0.3) % 1) + 1) % 1;
  // he reaches, and the branch lifts; he bends, and the water sinks
  const reach = Math.max(0, Math.sin(cyc * Math.PI * 2));
  const bend = Math.max(0, -Math.sin(cyc * Math.PI * 2));
  const W = 30 * s;
  const surf = y - 6 * s + bend * 3 * s;
  // the fig tree, leaning over the pool
  ctx.strokeStyle = "oklch(0.32 0.04 50)";
  ctx.lineCap = "round";
  ctx.lineWidth = 3.4 * s;
  ctx.beginPath();
  ctx.moveTo(x + 30 * s, y);
  ctx.quadraticCurveTo(x + 30 * s, y - 38 * s, x + 12 * s, y - 58 * s);
  ctx.stroke();
  const by = y - 54 * s - reach * 9 * s;
  ctx.lineWidth = 1.8 * s;
  ctx.beginPath();
  ctx.moveTo(x + 16 * s, y - 54 * s);
  ctx.quadraticCurveTo(x + 4 * s, by - 6 * s, x - 8 * s, by);
  ctx.stroke();
  ctx.fillStyle = "oklch(0.38 0.08 140)";
  for (const [lx, ly, r] of [
    [x + 6 * s, by - 8 * s, 6],
    [x - 6 * s, by - 5 * s, 5],
    [x + 18 * s, y - 62 * s, 7],
    [x + 26 * s, y - 52 * s, 6],
    [x + 8 * s, y - 66 * s, 6],
  ] as const) {
    ctx.beginPath();
    ctx.ellipse(lx, ly, r * s, r * 0.6 * s, 0.4, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.fillStyle = "oklch(0.42 0.13 330)";
  for (const fx of [-7, -2]) {
    ctx.beginPath();
    ctx.arc(x + fx * s, by + 3 * s, 2.3 * s, 0, Math.PI * 2);
    ctx.fill();
  }
  // the far half of the pool
  ctx.fillStyle = rgba([60, 90, 120], 0.9);
  ctx.beginPath();
  ctx.ellipse(x, surf, W, 8 * s, 0, Math.PI, Math.PI * 2);
  ctx.fill();
  // him: chest-deep, head up, one arm toward the fruit
  const hx = x - 2 * s;
  const hy = surf - 16 * s + bend * 4 * s;
  ctx.fillStyle = SKIN;
  ctx.beginPath();
  ctx.moveTo(hx - 9 * s, surf + 2 * s);
  ctx.quadraticCurveTo(hx - 10 * s, hy + 6 * s, hx - 4 * s, hy + 4 * s);
  ctx.lineTo(hx + 5 * s, hy + 4 * s);
  ctx.quadraticCurveTo(hx + 10 * s, hy + 6 * s, hx + 9 * s, surf + 2 * s);
  ctx.closePath();
  ctx.fill();
  rim(ctx, L, 1 * s);
  ctx.strokeStyle = SKIN;
  ctx.lineWidth = 3 * s;
  ctx.beginPath();
  ctx.moveTo(hx - 6 * s, hy + 6 * s);
  ctx.quadraticCurveTo(
    hx - 10 * s,
    hy - 6 * s - reach * 4 * s,
    x - 6 * s,
    by + 8 * s + (1 - reach) * 10 * s,
  );
  ctx.stroke();
  head(ctx, hx, hy - 2 * s, 5.4 * s, "oklch(0.22 0.03 40)", SKIN, "curls");
  // the near half of the water, over his chest
  const water = ctx.createLinearGradient(0, surf - 8 * s, 0, surf + 8 * s);
  water.addColorStop(0, rgba([130, 170, 200], 0.82 - L.dim * 0.3));
  water.addColorStop(1, rgba([40, 62, 92], 0.92));
  ctx.fillStyle = water;
  ctx.beginPath();
  ctx.ellipse(x, surf, W, 8 * s, 0, 0, Math.PI);
  ctx.fill();
  // ripples spreading from him
  ctx.strokeStyle = rgba([225, 238, 248], 0.35 * (1 - L.dim));
  ctx.lineWidth = 0.8 * s;
  for (let i = 0; i < 2; i++) {
    const r = Math.max(0.5, ((((L.t * 0.5 + i * 0.5) % 1) + 1) % 1) * 22 * s);
    ctx.beginPath();
    ctx.ellipse(hx, surf, r, r * 0.25, 0, 0, Math.PI * 2);
    ctx.stroke();
  }
  // the bank: a lip of stones round the pool
  ctx.fillStyle = "oklch(0.3 0.025 50)";
  ctx.beginPath();
  ctx.ellipse(x, surf + 6 * s, W + 3 * s, 5 * s, 0, 0, Math.PI);
  ctx.fill();
  ctx.restore();
}

/** a little thing in his hand on the way down */
export function drawItem(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  item: string,
  t: number,
) {
  ctx.save();
  switch (item) {
    case "feather":
      ctx.fillStyle = "oklch(0.35 0.03 60)";
      ctx.beginPath();
      ctx.ellipse(x, y - 4 * s, 1.6 * s, 5 * s, 0.4, 0, Math.PI * 2);
      ctx.fill();
      break;
    case "thyme":
    case "poppy":
      ctx.strokeStyle = "oklch(0.45 0.1 140)";
      ctx.lineWidth = 0.8 * s;
      ctx.beginPath();
      ctx.moveTo(x, y);
      ctx.lineTo(x - 1 * s, y - 7 * s);
      ctx.stroke();
      ctx.fillStyle = item === "poppy" ? "oklch(0.58 0.22 28)" : "oklch(0.7 0.08 310)";
      ctx.beginPath();
      ctx.arc(x - 1 * s, y - 8 * s, (item === "poppy" ? 2.4 : 1.4) * s, 0, Math.PI * 2);
      ctx.fill();
      break;
    case "snow":
      ctx.fillStyle = "rgba(240, 246, 255, 0.95)";
      ctx.beginPath();
      ctx.arc(x, y - 2 * s, 2.6 * s, 0, Math.PI * 2);
      ctx.fill();
      break;
    case "light":
    case "understanding": {
      const g = ctx.createRadialGradient(x, y - 2 * s, 0, x, y - 2 * s, 8 * s);
      const a = 0.5 + Math.sin(t * 3) * 0.15;
      g.addColorStop(0, `rgba(255, 236, 190, ${a})`);
      g.addColorStop(1, "rgba(255, 220, 160, 0)");
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.arc(x, y - 2 * s, 8 * s, 0, Math.PI * 2);
      ctx.fill();
      break;
    }
    case "stone":
    case "pebble":
    case "shard":
      ctx.fillStyle = "oklch(0.4 0.01 60)";
      ctx.beginPath();
      ctx.ellipse(x, y - 1.6 * s, 2.4 * s, 1.8 * s, 0.3, 0, Math.PI * 2);
      ctx.fill();
      break;
    default:
      // words, an answer, a look at the sea: nothing in the hand
      break;
  }
  ctx.restore();
}
