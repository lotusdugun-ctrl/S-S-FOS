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

let meropeCv: HTMLCanvasElement | null = null;

/**
 * Merope, a shade. A tall woman in a long peplos with a mantle drawn over her
 * head, painted opaque into a scratch canvas and laid on the hill as a ghost:
 * translucent, cold, her feet dissolving into the ground mist. As the story
 * goes on she comes back to herself — Act I grey and faint, Act II warmer and
 * more solid, Act III with the starlight of the Pleiad she was. `turned` puts
 * her back to him; `leave` (0-1) walks her away into nothing.
 */
export function drawMerope(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  o: { act: 1 | 2 | 3; turned: boolean; talking: boolean; leave: number; L: NpcLight },
) {
  if (o.leave >= 1) return;
  const W = Math.ceil(70 * s);
  const H = Math.ceil(110 * s);
  const cv = meropeCv ?? (meropeCv = document.createElement("canvas"));
  const dpr = 2;
  if (cv.width !== W * dpr || cv.height !== H * dpr) {
    cv.width = W * dpr;
    cv.height = H * dpr;
  }
  const c = cv.getContext("2d");
  if (!c) return;
  c.setTransform(1, 0, 0, 1, 0, 0);
  c.clearRect(0, 0, cv.width, cv.height);
  c.setTransform(dpr, 0, 0, dpr, 0, 0);
  // her own frame: feet at (cx, base), facing +x
  const cx = W / 2;
  const base = H - 4 * s;
  const t = o.L.t;
  const sway = Math.sin(t * 0.9) * 0.8 * s;
  const breathe = Math.sin(t * 1.4) * 0.4 * s;
  const tone = [
    { cloth: ["#9aa3b4", "#5b6475"], skin: ["#b9bcc4", "#7d818b"], hair: "#3a3d46" },
    { cloth: ["#b7a79a", "#6d5d55"], skin: ["#c9ad98", "#8b6f5e"], hair: "#2e2522" },
    { cloth: ["#e2d6c2", "#8c7a68"], skin: ["#d9b394", "#9a7259"], hair: "#2a1f1a" },
  ][o.act - 1]!;
  c.save();
  if (o.turned) {
    c.translate(cx * 2, 0);
    c.scale(-1, 1);
  }
  const hx = cx + 3 * s + sway;
  const hy = base - 88 * s + breathe;
  // the dress: falls from the shoulders to the ground in long folds
  const dress = c.createLinearGradient(cx - 14 * s, 0, cx + 14 * s, 0);
  dress.addColorStop(0, tone.cloth[1]!);
  dress.addColorStop(0.55, tone.cloth[0]!);
  dress.addColorStop(1, tone.cloth[1]!);
  c.fillStyle = dress;
  c.beginPath();
  c.moveTo(hx - 9 * s, hy + 12 * s);
  c.quadraticCurveTo(cx - 13 * s, base - 40 * s, cx - 17 * s, base);
  c.lineTo(cx + 15 * s, base);
  c.quadraticCurveTo(cx + 12 * s, base - 44 * s, hx + 8 * s, hy + 12 * s);
  c.closePath();
  c.fill();
  // folds
  c.strokeStyle = "rgba(20, 22, 30, 0.28)";
  c.lineWidth = 0.9 * s;
  c.beginPath();
  for (const f of [-9, -4, 1, 6, 11]) {
    c.moveTo(hx + f * 0.45 * s, hy + 20 * s);
    c.quadraticCurveTo(cx + f * s + sway * 0.5, base - 30 * s, cx + f * 1.3 * s, base);
  }
  c.stroke();
  c.strokeStyle = "rgba(255, 255, 255, 0.14)";
  c.beginPath();
  for (const f of [-6, 3, 8]) {
    c.moveTo(hx + f * 0.45 * s + 1 * s, hy + 22 * s);
    c.quadraticCurveTo(cx + f * s + 1 * s, base - 30 * s, cx + f * 1.3 * s + 1 * s, base);
  }
  c.stroke();
  // a girdle under the breast
  c.strokeStyle = "rgba(30, 26, 30, 0.5)";
  c.lineWidth = 1.4 * s;
  c.beginPath();
  c.moveTo(hx - 8 * s, hy + 30 * s);
  c.quadraticCurveTo(hx, hy + 32 * s, hx + 8 * s, hy + 29 * s);
  c.stroke();
  // the arm: one hand at her breast holding the mantle; when she talks it opens
  const open = o.talking ? 0.5 + Math.sin(t * 2.2) * 0.5 : 0;
  c.strokeStyle = tone.skin[1]!;
  c.lineCap = "round";
  c.lineWidth = 3 * s;
  c.beginPath();
  c.moveTo(hx + 3 * s, hy + 16 * s);
  c.quadraticCurveTo(
    hx + 7 * s + open * 3 * s,
    hy + 30 * s,
    hx + 9 * s + open * 6 * s,
    hy + 24 * s - open * 4 * s,
  );
  c.stroke();
  // head: a woman's profile, the mantle over it
  const skin = c.createLinearGradient(hx - 6 * s, hy, hx + 7 * s, hy);
  skin.addColorStop(0, tone.skin[1]!);
  skin.addColorStop(1, tone.skin[0]!);
  c.fillStyle = skin;
  c.beginPath();
  c.moveTo(hx - 4 * s, hy - 6 * s);
  c.quadraticCurveTo(hx + 3 * s, hy - 9 * s, hx + 5.5 * s, hy - 3 * s); // brow
  c.lineTo(hx + 7.4 * s, hy + 1.2 * s); // nose
  c.lineTo(hx + 5.6 * s, hy + 2 * s);
  c.quadraticCurveTo(hx + 6.4 * s, hy + 3.3 * s, hx + 5.6 * s, hy + 4 * s); // lips
  c.quadraticCurveTo(hx + 5.4 * s, hy + 6.4 * s, hx + 2.6 * s, hy + 6.8 * s); // chin
  c.quadraticCurveTo(hx - 2 * s, hy + 6 * s, hx - 3 * s, hy + 11 * s); // neck
  c.lineTo(hx - 6 * s, hy + 11 * s);
  c.quadraticCurveTo(hx - 7 * s, hy - 2 * s, hx - 4 * s, hy - 6 * s);
  c.closePath();
  c.fill();
  // eye: closed and blank at first, open and looking at the end
  c.strokeStyle = "rgba(20, 16, 18, 0.7)";
  c.lineWidth = 0.7 * s;
  c.beginPath();
  if (o.act === 1) {
    c.moveTo(hx + 2 * s, hy - 1 * s);
    c.quadraticCurveTo(hx + 3.4 * s, hy - 0.2 * s, hx + 4.6 * s, hy - 1 * s);
  } else {
    c.ellipse(hx + 3.4 * s, hy - 1 * s, 1.1 * s, 0.55 * s, 0, 0, Math.PI * 2);
  }
  c.stroke();
  // hair, gathered at the nape
  c.fillStyle = tone.hair;
  c.beginPath();
  c.ellipse(hx - 4.5 * s, hy + 1 * s, 3.4 * s, 4 * s, 0.3, 0, Math.PI * 2);
  c.fill();
  // the mantle: over the crown, down the back to the hem
  const mantle = c.createLinearGradient(hx - 12 * s, 0, hx + 6 * s, 0);
  mantle.addColorStop(0, tone.cloth[1]!);
  mantle.addColorStop(1, tone.cloth[0]!);
  c.fillStyle = mantle;
  c.beginPath();
  c.moveTo(hx + 4 * s, hy - 6 * s);
  c.quadraticCurveTo(hx - 1 * s, hy - 13 * s, hx - 8 * s, hy - 6 * s);
  c.quadraticCurveTo(hx - 13 * s, hy + 12 * s, hx - 15 * s + sway, hy + 34 * s);
  c.quadraticCurveTo(cx - 18 * s + sway * 1.5, base - 30 * s, cx - 19 * s + sway * 2, base - 6 * s);
  c.lineTo(cx - 12 * s, base - 8 * s);
  c.quadraticCurveTo(hx - 8 * s, hy + 20 * s, hx - 5 * s, hy + 6 * s);
  c.quadraticCurveTo(hx - 3 * s, hy - 4 * s, hx + 4 * s, hy - 6 * s);
  c.closePath();
  c.fill();
  c.restore();
  // a shade has no feet: she dissolves into the ground mist
  c.save();
  c.globalCompositeOperation = "destination-out";
  const fadeFeet = c.createLinearGradient(0, base - 26 * s, 0, base);
  fadeFeet.addColorStop(0, "rgba(0,0,0,0)");
  fadeFeet.addColorStop(1, "rgba(0,0,0,0.95)");
  c.fillStyle = fadeFeet;
  c.fillRect(0, base - 26 * s, W, 30 * s);
  c.restore();

  // onto the hill, as a ghost
  const alpha = ([0.5, 0.68, 0.85][o.act - 1]! + Math.sin(t * 1.7) * 0.04) * (1 - o.leave);
  const dx = o.leave * -60 * s;
  ctx.save();
  ctx.globalAlpha = Math.max(0, alpha);
  // a cold halo behind her (a gradient: a shadow blur on a canvas this size,
  // every frame, was dear)
  const gx = x + dx + 2 * s;
  const gy = y - 50 * s;
  const halo = ctx.createRadialGradient(gx, gy, 0, gx, gy, 46 * s);
  halo.addColorStop(0, o.act === 3 ? "rgba(255, 236, 200, 0.22)" : "rgba(170, 190, 230, 0.18)");
  halo.addColorStop(1, "rgba(170, 190, 230, 0)");
  ctx.fillStyle = halo;
  ctx.fillRect(gx - 46 * s, gy - 60 * s, 92 * s, 120 * s);
  ctx.drawImage(cv, x - cx + dx, y - base, W, H);
  ctx.restore();
  // a few motes of her drifting off her
  ctx.save();
  for (let i = 0; i < 4; i++) {
    const p = (((t * 0.25 + i * 0.27) % 1) + 1) % 1;
    const a = Math.sin(p * Math.PI) * 0.25 * (1 - o.leave);
    ctx.fillStyle =
      o.act === 3 ? `rgba(255, 236, 200, ${a.toFixed(3)})` : `rgba(190, 205, 240, ${a.toFixed(3)})`;
    ctx.beginPath();
    ctx.arc(x + dx + (i - 1.5) * 6 * s, y - 20 * s - p * 70 * s, 1.1 * s, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.restore();
}

/** the eagle's visit: where it is in its day */
export type EagleVisit = { phase: "away" | "arrive" | "feed" | "leave"; t: number };

/**
 * Prometheus: a titan chained by both wrists to a black cliff beside the path,
 * half-kneeling, his right side torn open. Every day Zeus's eagle comes down
 * on him, plants its talons in his thigh and his shoulder and feeds; he
 * fights the chains and cannot reach it. Then it goes, and the wound closes,
 * for tomorrow.
 */
export function drawPrometheus(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  L: NpcLight,
  eagle: EagleVisit = { phase: "away", t: 0 },
) {
  ctx.save();
  const t = L.t;
  const feeding = eagle.phase === "feed";
  // he writhes while it feeds, and hangs slack after
  const writhe = feeding ? Math.sin(t * 7) * 0.6 + Math.sin(t * 13) * 0.3 : 0;
  const slack = eagle.phase === "leave" || eagle.phase === "away" ? 1 : 0;
  // the cliff
  const rock = ctx.createLinearGradient(x - 10 * s, 0, x + 50 * s, 0);
  rock.addColorStop(0, "#2b2523");
  rock.addColorStop(0.6, "#1d1918");
  rock.addColorStop(1, "#121010");
  ctx.fillStyle = rock;
  ctx.beginPath();
  ctx.moveTo(x - 8 * s, y + 2 * s);
  ctx.lineTo(x - 4 * s, y - 60 * s);
  ctx.lineTo(x + 4 * s, y - 104 * s);
  ctx.lineTo(x + 14 * s, y - 126 * s);
  ctx.lineTo(x + 24 * s, y - 112 * s);
  ctx.lineTo(x + 32 * s, y - 120 * s);
  ctx.lineTo(x + 40 * s, y - 84 * s);
  ctx.lineTo(x + 48 * s, y - 40 * s);
  ctx.lineTo(x + 56 * s, y + 2 * s);
  ctx.closePath();
  ctx.fill();
  ctx.strokeStyle = "rgba(0, 0, 0, 0.45)";
  ctx.lineWidth = 1 * s;
  ctx.beginPath();
  ctx.moveTo(x + 14 * s, y - 118 * s);
  ctx.lineTo(x + 18 * s, y - 80 * s);
  ctx.lineTo(x + 12 * s, y - 50 * s);
  ctx.moveTo(x + 36 * s, y - 70 * s);
  ctx.lineTo(x + 30 * s, y - 30 * s);
  ctx.stroke();
  // his blood, old and new, down the face of the rock
  ctx.strokeStyle = "rgba(90, 10, 8, 0.55)";
  ctx.lineWidth = 1.6 * s;
  ctx.beginPath();
  ctx.moveTo(x + 6 * s, y - 52 * s);
  ctx.quadraticCurveTo(x + 7 * s, y - 30 * s, x + 4 * s, y);
  ctx.stroke();

  // the chains: two iron rings in the rock, long links to the wrists
  const ringL = { x: x + 6 * s, y: y - 98 * s };
  const ringR = { x: x + 34 * s, y: y - 96 * s };
  // his body: half-kneeling at the foot of the cliff, facing left
  const hipX = x + 12 * s;
  const hipY = y - 24 * s;
  const lean = -0.12 + writhe * 0.08 + slack * 0.05;
  const shX = hipX - 4 * s + writhe * 3 * s;
  const shY = hipY - 34 * s + slack * 3 * s;
  const wristL = { x: ringL.x - 6 * s + writhe * 2 * s, y: ringL.y + 26 * s + slack * 6 * s };
  const wristR = { x: ringR.x - 2 * s - writhe * 2 * s, y: ringR.y + 24 * s + slack * 6 * s };
  ctx.strokeStyle = "#4a4d55";
  ctx.lineWidth = 1.3 * s;
  for (const [r, w] of [
    [ringL, wristL],
    [ringR, wristR],
  ] as const) {
    const n = 7;
    for (let i = 0; i < n; i++) {
      const k = (i + 0.5) / n;
      const lx = r.x + (w.x - r.x) * k;
      const ly = r.y + (w.y - r.y) * k + Math.sin(k * Math.PI) * 4 * s * slack;
      ctx.beginPath();
      ctx.ellipse(
        lx,
        ly,
        1.2 * s,
        2 * s,
        Math.atan2(w.y - r.y, w.x - r.x) + Math.PI / 2 + (i % 2) * 1.2,
        0,
        Math.PI * 2,
      );
      ctx.stroke();
    }
    ctx.fillStyle = "#2c2e33";
    ctx.beginPath();
    ctx.arc(r.x, r.y, 2.4 * s, 0, Math.PI * 2);
    ctx.fill();
  }
  const SKN = "#8a5a3e";
  const SKD = "#4e2f20";
  // legs: the near knee down on the rock, the far leg braced
  const leg = (kx: number, ky: number, fx: number, fy: number, w: number, col: string) => {
    ctx.strokeStyle = col;
    ctx.lineCap = "round";
    ctx.lineWidth = w;
    ctx.beginPath();
    ctx.moveTo(hipX, hipY);
    ctx.quadraticCurveTo((hipX + kx) / 2 - 2 * s, (hipY + ky) / 2 - 3 * s, kx, ky);
    ctx.lineTo(fx, fy);
    ctx.stroke();
  };
  leg(hipX - 18 * s, y - 10 * s + writhe * 2 * s, hipX - 30 * s, y - 2 * s, 8 * s, SKD);
  leg(hipX - 12 * s, y - 2 * s, hipX + 6 * s, y - 1 * s, 9 * s, SKN);
  // a torn loincloth
  ctx.fillStyle = "#4a1712";
  ctx.beginPath();
  ctx.moveTo(hipX - 10 * s, hipY - 4 * s);
  ctx.lineTo(hipX + 9 * s, hipY - 5 * s);
  ctx.lineTo(hipX + 6 * s, hipY + 9 * s);
  ctx.lineTo(hipX - 1 * s, hipY + 5 * s);
  ctx.lineTo(hipX - 9 * s, hipY + 10 * s);
  ctx.closePath();
  ctx.fill();
  // torso: a titan's, ribs and muscle, lit from the side
  ctx.save();
  ctx.translate(hipX, hipY - 4 * s);
  ctx.rotate(lean);
  const torso = ctx.createLinearGradient(-14 * s, 0, 14 * s, 0);
  torso.addColorStop(0, "#a8724f");
  torso.addColorStop(0.5, SKN);
  torso.addColorStop(1, SKD);
  ctx.fillStyle = torso;
  ctx.beginPath();
  ctx.moveTo(-10 * s, 0);
  ctx.quadraticCurveTo(-13 * s, -16 * s, -15 * s, -30 * s);
  ctx.quadraticCurveTo(-8 * s, -38 * s, 0, -37 * s);
  ctx.quadraticCurveTo(10 * s, -38 * s, 15 * s, -30 * s);
  ctx.quadraticCurveTo(12 * s, -14 * s, 9 * s, 0);
  ctx.closePath();
  ctx.fill();
  // muscle: pectorals, the ridged belly, ribs straining
  ctx.strokeStyle = "rgba(40, 18, 10, 0.45)";
  ctx.lineWidth = 0.9 * s;
  ctx.beginPath();
  ctx.moveTo(-11 * s, -24 * s);
  ctx.quadraticCurveTo(-5 * s, -20 * s, 0, -24 * s);
  ctx.quadraticCurveTo(5 * s, -20 * s, 11 * s, -24 * s);
  ctx.moveTo(0, -24 * s);
  ctx.lineTo(0, -3 * s);
  for (const yy of [-17, -11, -5]) {
    ctx.moveTo(-5 * s, yy * s);
    ctx.quadraticCurveTo(0, (yy - 1) * s, 5 * s, yy * s);
  }
  for (const yy of [-21, -17, -13]) {
    ctx.moveTo(-13 * s, yy * s);
    ctx.lineTo(-8 * s, (yy + 2) * s);
  }
  ctx.stroke();
  // the wound: under the ribs on his right, open, wet, never healing in daylight
  const wx = 6 * s;
  const wy = -14 * s;
  const open = feeding ? 1 : eagle.phase === "leave" ? 0.9 : 0.55;
  const wg = ctx.createRadialGradient(wx, wy, 0, wx, wy, 6 * s);
  wg.addColorStop(0, "#2a0303");
  wg.addColorStop(0.5, "#6e0c08");
  wg.addColorStop(1, "rgba(120, 20, 14, 0)");
  ctx.fillStyle = wg;
  ctx.beginPath();
  ctx.ellipse(wx, wy, 4.2 * s * open, 6.5 * s * open, 0.4, 0, Math.PI * 2);
  ctx.fill();
  // blood running down to the hip
  ctx.strokeStyle = "rgba(120, 12, 8, 0.8)";
  ctx.lineWidth = 1.2 * s;
  ctx.beginPath();
  ctx.moveTo(wx - 1 * s, wy + 4 * s);
  ctx.quadraticCurveTo(wx + 1 * s, wy + 10 * s, wx - 1 * s, 0);
  ctx.moveTo(wx + 2 * s, wy + 5 * s);
  ctx.quadraticCurveTo(wx + 4 * s, wy + 9 * s, wx + 3 * s, wy + 13 * s);
  ctx.stroke();
  ctx.restore();
  // arms up to the chains
  ctx.strokeStyle = SKN;
  ctx.lineWidth = 6 * s;
  ctx.lineCap = "round";
  ctx.beginPath();
  ctx.moveTo(shX - 10 * s, shY + 2 * s);
  ctx.quadraticCurveTo(shX - 14 * s, shY - 16 * s, wristL.x, wristL.y);
  ctx.stroke();
  ctx.strokeStyle = SKD;
  ctx.beginPath();
  ctx.moveTo(shX + 10 * s, shY + 2 * s);
  ctx.quadraticCurveTo(shX + 16 * s, shY - 14 * s, wristR.x, wristR.y);
  ctx.stroke();
  // iron cuffs
  ctx.fillStyle = "#3a3c42";
  for (const w of [wristL, wristR]) {
    ctx.beginPath();
    ctx.ellipse(w.x, w.y, 3.6 * s, 2.4 * s, 0.3, 0, Math.PI * 2);
    ctx.fill();
  }
  // head: thrown back in pain while it feeds, hanging forward after
  const hx = shX - 2 * s + writhe * 2 * s;
  const hy = shY - 9 * s;
  ctx.save();
  ctx.translate(hx, hy);
  ctx.rotate(feeding ? -0.5 + writhe * 0.2 : 0.35 * slack);
  ctx.fillStyle = SKN;
  ctx.beginPath();
  ctx.ellipse(0, 0, 5.8 * s, 6.8 * s, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(-5 * s, -1 * s);
  ctx.lineTo(-8 * s, 1.5 * s);
  ctx.lineTo(-5 * s, 2.5 * s);
  ctx.fill();
  // grey-black hair and a heavy beard
  ctx.fillStyle = "#2b2726";
  ctx.beginPath();
  ctx.ellipse(1.5 * s, -3 * s, 6.5 * s, 5.5 * s, 0.3, Math.PI * 0.95, Math.PI * 2.2);
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(3 * s, 1 * s);
  ctx.quadraticCurveTo(6 * s, 12 * s, 9 * s, 10 * s);
  ctx.lineTo(5 * s, -2 * s);
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(-4.5 * s, 3 * s);
  ctx.quadraticCurveTo(-3 * s, 13 * s, 2 * s, 12 * s);
  ctx.quadraticCurveTo(4 * s, 6 * s, 2 * s, 2 * s);
  ctx.fill();
  // the mouth open in a cry while it feeds
  if (feeding) {
    ctx.fillStyle = "#1a0605";
    ctx.beginPath();
    ctx.ellipse(
      -3.6 * s,
      4.4 * s,
      1.4 * s,
      1.1 * s + Math.abs(writhe) * 0.8 * s,
      0,
      0,
      Math.PI * 2,
    );
    ctx.fill();
  }
  ctx.fillStyle = "rgba(10, 6, 6, 0.85)";
  ctx.beginPath();
  ctx.ellipse(-2.6 * s, -1.4 * s, 1 * s, 0.5 * s, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();

  // the eagle
  if (eagle.phase !== "away") {
    const wound = { x: hipX + 6 * s, y: hipY - 18 * s };
    let ex = wound.x + 10 * s;
    let ey = wound.y - 8 * s;
    let spread = 0.4;
    let peck = 0;
    if (eagle.phase === "arrive") {
      const k = Math.min(1, eagle.t / 2.2);
      const e = 1 - (1 - k) * (1 - k);
      ex = wound.x + 10 * s + (1 - e) * 220 * s;
      ey = wound.y - 8 * s - (1 - e) * 160 * s;
      spread = 1;
    } else if (eagle.phase === "leave") {
      const k = Math.min(1, eagle.t / 2.2);
      ex = wound.x + 10 * s + k * k * 260 * s;
      ey = wound.y - 8 * s - k * 200 * s;
      spread = 1;
    } else {
      // feeding: the head drives into the wound and tears back
      const c2 = (((eagle.t * 1.6) % 1) + 1) % 1;
      peck = c2 < 0.35 ? c2 / 0.35 : Math.max(0, 1 - (c2 - 0.35) / 0.4);
      spread = 0.45 + Math.sin(eagle.t * 3) * 0.1;
    }
    drawEagle(ctx, ex, ey, s * 1.15, spread, peck, t, eagle.phase === "feed");
    // gore torn loose
    if (eagle.phase === "feed" && peck < 0.2) {
      ctx.fillStyle = "rgba(130, 14, 10, 0.85)";
      for (let i = 0; i < 3; i++) {
        const p = (((t * 2 + i * 0.33) % 1) + 1) % 1;
        ctx.beginPath();
        ctx.arc(wound.x + (i - 1) * 2 * s, wound.y + p * 26 * s, 0.9 * s, 0, Math.PI * 2);
        ctx.fill();
      }
    }
  }
  ctx.restore();
}

/** Zeus's eagle: big, dark, a hooked yellow beak; `spread` 0 folded, 1 open */
function drawEagle(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  spread: number,
  peck: number,
  t: number,
  perched: boolean,
) {
  ctx.save();
  ctx.translate(x, y);
  const flap = perched ? Math.sin(t * 5) * 0.15 : Math.sin(t * 9) * 0.6;
  const body = "#2e231c";
  const wing = "#3a2c22";
  // wings
  ctx.fillStyle = wing;
  for (const side of [-1, 1]) {
    ctx.save();
    ctx.rotate(side * (-0.3 - spread * 0.6 + flap * spread));
    ctx.beginPath();
    ctx.moveTo(0, -2 * s);
    ctx.quadraticCurveTo(
      side * 6 * s,
      -16 * s * spread - 4 * s,
      side * (14 + 18 * spread) * s,
      -10 * s * spread,
    );
    // primaries, fingered
    for (let i = 0; i < 4; i++) {
      ctx.lineTo(side * (14 + 18 * spread - i * 3) * s, (-6 * spread + i * 2.4) * s);
      ctx.lineTo(side * (12 + 16 * spread - i * 3) * s, (-8 * spread + i * 2.4 + 1.2) * s);
    }
    ctx.quadraticCurveTo(side * 6 * s, 6 * s, 0, 3 * s);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }
  // body and tail
  ctx.fillStyle = body;
  ctx.beginPath();
  ctx.ellipse(0, 0, 6 * s, 8.5 * s, -0.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(3 * s, 6 * s);
  ctx.lineTo(9 * s, 15 * s);
  ctx.lineTo(4 * s, 15 * s);
  ctx.closePath();
  ctx.fill();
  // talons
  if (perched) {
    ctx.strokeStyle = "#b8923a";
    ctx.lineWidth = 1 * s;
    ctx.beginPath();
    ctx.moveTo(-2 * s, 7 * s);
    ctx.lineTo(-4 * s, 11 * s);
    ctx.moveTo(1 * s, 8 * s);
    ctx.lineTo(0, 12 * s);
    ctx.stroke();
  }
  // head: lunging down-left into the wound when it pecks
  const hx = -6 * s - peck * 7 * s;
  const hy = -8 * s + peck * 8 * s;
  ctx.fillStyle = "#d9cfc0";
  ctx.beginPath();
  ctx.ellipse(hx, hy, 3.4 * s, 3 * s, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "#c8962c";
  ctx.beginPath();
  ctx.moveTo(hx - 2.6 * s, hy - 0.6 * s);
  ctx.quadraticCurveTo(hx - 6.4 * s, hy - 0.4 * s, hx - 6 * s, hy + 2.6 * s);
  ctx.lineTo(hx - 3 * s, hy + 1.2 * s);
  ctx.closePath();
  ctx.fill();
  if (peck > 0.5) {
    ctx.fillStyle = "rgba(140, 16, 10, 0.9)";
    ctx.beginPath();
    ctx.arc(hx - 5.6 * s, hy + 2.6 * s, 1.1 * s, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.fillStyle = "#111";
  ctx.beginPath();
  ctx.arc(hx - 1 * s, hy - 0.8 * s, 0.6 * s, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();
}

/**
 * Tantalus: starved to the bone, chest-deep in black water under a twisted
 * fig heavy with fruit. When he bends to drink the water sinks away from his
 * cracked lips; when he reaches, the branch lifts. Over and over.
 */
export function drawTantalus(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  L: NpcLight,
) {
  ctx.save();
  const t = L.t;
  const cyc = (((t * 0.22) % 1) + 1) % 1;
  // first he reaches up (the branch lifts), then he bends to drink (the water sinks)
  const reach = cyc < 0.5 ? Math.sin((cyc / 0.5) * Math.PI) : 0;
  const bend = cyc >= 0.5 ? Math.sin(((cyc - 0.5) / 0.5) * Math.PI) : 0;
  const W = 38 * s;
  const surf = y - 7 * s + bend * 6 * s;
  // the fig: a dead-looking trunk, and fruit as ripe as anything in the world
  ctx.strokeStyle = "#2a201b";
  ctx.lineCap = "round";
  ctx.lineWidth = 5 * s;
  ctx.beginPath();
  ctx.moveTo(x + 40 * s, y + 2 * s);
  ctx.bezierCurveTo(x + 46 * s, y - 30 * s, x + 30 * s, y - 50 * s, x + 18 * s, y - 72 * s);
  ctx.stroke();
  ctx.lineWidth = 2.6 * s;
  ctx.beginPath();
  ctx.moveTo(x + 34 * s, y - 40 * s);
  ctx.quadraticCurveTo(x + 50 * s, y - 56 * s, x + 54 * s, y - 70 * s);
  ctx.stroke();
  const by = y - 62 * s - reach * 14 * s;
  ctx.lineWidth = 2 * s;
  ctx.beginPath();
  ctx.moveTo(x + 22 * s, y - 64 * s);
  ctx.quadraticCurveTo(x + 6 * s, by - 6 * s, x - 10 * s, by);
  ctx.stroke();
  const leaf = (lx: number, ly: number, r: number, a: number) => {
    ctx.fillStyle = "#24361f";
    ctx.beginPath();
    ctx.ellipse(lx, ly, r * s, r * 0.55 * s, a, 0, Math.PI * 2);
    ctx.fill();
  };
  leaf(x + 4 * s, by - 7 * s, 7, 0.5);
  leaf(x - 8 * s, by - 4 * s, 6, -0.3);
  leaf(x + 22 * s, y - 76 * s, 8, 0.2);
  leaf(x + 52 * s, y - 74 * s, 7, -0.4);
  leaf(x + 12 * s, y - 82 * s, 7, 0.6);
  for (const [fx, fy] of [
    [-9, 4],
    [-3, 3],
    [-6, 8],
  ] as const) {
    const g = ctx.createRadialGradient(
      x + (fx - 1) * s,
      by + (fy - 1) * s,
      0,
      x + fx * s,
      by + fy * s,
      3 * s,
    );
    g.addColorStop(0, "#b2557a");
    g.addColorStop(1, "#4b1630");
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(x + fx * s, by + fy * s, 2.8 * s, 0, Math.PI * 2);
    ctx.fill();
  }
  // the far half of the pool: black water
  ctx.fillStyle = "#0c1216";
  ctx.beginPath();
  ctx.ellipse(x, surf, W, 9 * s, 0, Math.PI, Math.PI * 2);
  ctx.fill();
  // him: a starved man, every rib showing
  const hx = x - 3 * s - bend * 3 * s;
  const hy = surf - 20 * s + bend * 9 * s;
  const SK = "#9b7a63";
  const SKD = "#5c4334";
  const chest = ctx.createLinearGradient(hx - 9 * s, 0, hx + 9 * s, 0);
  chest.addColorStop(0, "#b59279");
  chest.addColorStop(1, SKD);
  ctx.fillStyle = chest;
  ctx.beginPath();
  ctx.moveTo(hx - 9 * s, surf + 2 * s);
  ctx.quadraticCurveTo(hx - 11 * s, hy + 8 * s, hx - 6 * s, hy + 5 * s);
  ctx.lineTo(hx + 6 * s, hy + 5 * s);
  ctx.quadraticCurveTo(hx + 10 * s, hy + 8 * s, hx + 8 * s, surf + 2 * s);
  ctx.closePath();
  ctx.fill();
  ctx.strokeStyle = "rgba(40, 22, 14, 0.55)";
  ctx.lineWidth = 0.8 * s;
  ctx.beginPath();
  for (let i = 0; i < 4; i++) {
    const ry = hy + 9 * s + i * 3 * s;
    ctx.moveTo(hx - 7 * s, ry);
    ctx.quadraticCurveTo(hx - 2 * s, ry - 1.6 * s, hx, ry + 0.4 * s);
    ctx.moveTo(hx + 1 * s, ry + 0.4 * s);
    ctx.quadraticCurveTo(hx + 4 * s, ry - 1.4 * s, hx + 7 * s, ry);
  }
  // collarbones
  ctx.moveTo(hx - 6 * s, hy + 6 * s);
  ctx.lineTo(hx - 1 * s, hy + 7.5 * s);
  ctx.moveTo(hx + 1 * s, hy + 7.5 * s);
  ctx.lineTo(hx + 6 * s, hy + 6 * s);
  ctx.stroke();
  // the arm, bone-thin, reaching for the fruit (or down to the water)
  ctx.strokeStyle = SK;
  ctx.lineWidth = 2.4 * s;
  ctx.beginPath();
  ctx.moveTo(hx - 6 * s, hy + 7 * s);
  if (bend > 0.05) {
    ctx.quadraticCurveTo(hx - 13 * s, hy + 12 * s, hx - 12 * s, surf + 2 * s);
  } else {
    ctx.quadraticCurveTo(
      hx - 12 * s,
      hy - 8 * s - reach * 4 * s,
      x - 8 * s,
      by + 10 * s + (1 - reach) * 14 * s,
    );
  }
  ctx.stroke();
  // head: hollow cheeks, sunken eyes, lips cracked, hair matted
  const hhx = hx - 1 * s;
  const hhy = hy - 3 * s;
  ctx.save();
  ctx.translate(hhx, hhy);
  ctx.rotate(bend * 0.5 - reach * 0.35);
  ctx.fillStyle = SK;
  ctx.beginPath();
  ctx.moveTo(-1 * s, -6.5 * s);
  ctx.quadraticCurveTo(5 * s, -6 * s, 5 * s, 0);
  ctx.quadraticCurveTo(4 * s, 5 * s, 0, 6.5 * s);
  ctx.quadraticCurveTo(-5 * s, 5 * s, -5 * s, 0);
  ctx.quadraticCurveTo(-5 * s, -6 * s, -1 * s, -6.5 * s);
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(-4.6 * s, -1 * s);
  ctx.lineTo(-7 * s, 1.4 * s);
  ctx.lineTo(-4.6 * s, 2.2 * s);
  ctx.fill();
  ctx.fillStyle = "rgba(30, 16, 10, 0.75)";
  ctx.beginPath();
  ctx.ellipse(-2.4 * s, -1.6 * s, 1.5 * s, 1.1 * s, 0, 0, Math.PI * 2);
  ctx.ellipse(-0.5 * s, 2.6 * s, 1.6 * s, 0.9 * s, 0.3, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "rgba(220, 200, 170, 0.8)";
  ctx.beginPath();
  ctx.arc(-2.7 * s, -1.6 * s, 0.35 * s, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = "#3a1712";
  ctx.lineWidth = 0.8 * s;
  ctx.beginPath();
  ctx.moveTo(-4.4 * s, 3.8 * s);
  ctx.lineTo(-2.4 * s, 4 * s);
  ctx.stroke();
  ctx.fillStyle = "#1c1612";
  ctx.beginPath();
  ctx.moveTo(-3 * s, -5.8 * s);
  ctx.quadraticCurveTo(4 * s, -9 * s, 6 * s, -1 * s);
  ctx.lineTo(6.5 * s, 6 * s);
  ctx.lineTo(4 * s, 3 * s);
  ctx.quadraticCurveTo(3 * s, -3 * s, -3 * s, -5.8 * s);
  ctx.fill();
  ctx.restore();
  // the near half of the water, black and still, over his chest
  const water = ctx.createLinearGradient(0, surf - 8 * s, 0, surf + 9 * s);
  water.addColorStop(0, `rgba(48, 66, 78, ${(0.92 - L.dim * 0.2).toFixed(3)})`);
  water.addColorStop(1, "rgba(8, 12, 16, 0.97)");
  ctx.fillStyle = water;
  ctx.beginPath();
  ctx.ellipse(x, surf, W, 9 * s, 0, 0, Math.PI);
  ctx.fill();
  // a cold glint on the surface, and the ring where the water fled
  ctx.strokeStyle = rgba(L.color, 0.35 * L.strength + 0.08);
  ctx.lineWidth = 0.8 * s;
  ctx.beginPath();
  ctx.ellipse(x, surf, W, 9 * s, 0, Math.PI * 1.15, Math.PI * 1.85);
  ctx.stroke();
  if (bend > 0.1) {
    ctx.strokeStyle = `rgba(150, 170, 180, ${(0.4 * bend).toFixed(3)})`;
    ctx.beginPath();
    ctx.ellipse(hx - 10 * s, surf - 6 * s * bend, 10 * s, 2 * s, 0, 0, Math.PI * 2);
    ctx.stroke();
  }
  // the bank: dead stones round the pool
  ctx.fillStyle = "#231c18";
  ctx.beginPath();
  ctx.ellipse(x, surf + 7 * s, W + 4 * s, 5 * s, 0, 0, Math.PI);
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
    case "lethe": {
      // the water of forgetting, cupped in his hands: still, faintly cold-lit
      const g = ctx.createRadialGradient(x, y - 2 * s, 0, x, y - 2 * s, 9 * s);
      const a = 0.45 + Math.sin(t * 2) * 0.12;
      g.addColorStop(0, `rgba(200, 225, 255, ${a})`);
      g.addColorStop(1, "rgba(160, 190, 240, 0)");
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.arc(x, y - 2 * s, 9 * s, 0, Math.PI * 2);
      ctx.fill();
      break;
    }
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
