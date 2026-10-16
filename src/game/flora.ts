import { rng } from "./textures";

/**
 * Trees, painted once into sprites and only swayed per frame.
 *
 * The olive trees used to be a stick with three green circles on it. A real
 * old olive on a dry slope is a short, split, twisting trunk — often two or
 * three stems grown apart — carrying a loose, ragged crown of small dark
 * leaf clusters with sky showing through, silver-green where the light
 * catches it. Against a low sun it is mostly silhouette, edged with light.
 * The cypress is the other tree of these hills: a tall dark flame.
 *
 * Sprites are built at screen density with their base at (ox, oy), and the
 * same rim technique as the figures: the silhouette minus itself shifted away
 * from the sun, laid back on in warm light.
 */

export type TreeSprite = { cv: HTMLCanvasElement; ox: number; oy: number; px: number };

function mk(w: number, h: number) {
  const cv = document.createElement("canvas");
  cv.width = Math.max(1, Math.ceil(w));
  cv.height = Math.max(1, Math.ceil(h));
  return cv;
}

/** lay a sunward rim on everything already painted in `cv` */
function rimLight(cv: HTMLCanvasElement, e: number, color: string) {
  const r = mk(cv.width, cv.height);
  const rc = r.getContext("2d");
  const c = cv.getContext("2d");
  if (!rc || !c) return;
  rc.drawImage(cv, 0, 0);
  rc.globalCompositeOperation = "source-in";
  rc.fillStyle = color;
  rc.fillRect(0, 0, r.width, r.height);
  rc.globalCompositeOperation = "destination-out";
  // the sun is to the right of most of the slope and low: rim on the right/top
  rc.drawImage(cv, -e, e * 0.6);
  c.globalCompositeOperation = "source-atop";
  c.drawImage(r, 0, 0);
  c.globalCompositeOperation = "source-over";
}

/** px: sprite pixels per design unit (scale * dpr); seed: which tree */
export function oliveSprite(seed: number, px: number): TreeSprite {
  const r = rng(seed * 7919 + 13);
  const W = 110;
  const H = 96;
  const cv = mk(W * px, H * px);
  const c = cv.getContext("2d");
  const ox = (W / 2) * px;
  const oy = (H - 4) * px;
  if (!c) return { cv, ox, oy, px };
  c.translate(ox, oy);
  c.scale(px, px);
  const bark = "oklch(0.16 0.02 55)";
  const leafDark = "oklch(0.17 0.022 120)";

  // trunk: two or three stems that split low and twist apart
  const stems = 2 + Math.floor(r() * 2);
  const tips: Array<[number, number]> = [];
  c.lineCap = "round";
  c.lineJoin = "round";
  for (let i = 0; i < stems; i++) {
    const dir = (i - (stems - 1) / 2) * 0.55 + (r() - 0.5) * 0.3;
    let x = (r() - 0.5) * 4;
    let y = 0;
    let wdt = 7 - i * 1.2;
    let a = -Math.PI / 2 + dir;
    c.strokeStyle = bark;
    const segs = 5;
    for (let k = 0; k < segs; k++) {
      const len = 7 + r() * 4;
      a += (r() - 0.5) * 0.6;
      const nx = x + Math.cos(a) * len;
      const ny = y + Math.sin(a) * len;
      c.lineWidth = wdt;
      c.beginPath();
      c.moveTo(x, y);
      c.quadraticCurveTo((x + nx) / 2 + (r() - 0.5) * 4, (y + ny) / 2, nx, ny);
      c.stroke();
      // knots and burls on an old trunk
      if (k < 2 && r() < 0.6) {
        c.fillStyle = bark;
        c.beginPath();
        c.ellipse(nx + (r() - 0.5) * 3, ny, wdt * 0.7, wdt * 0.5, r(), 0, Math.PI * 2);
        c.fill();
      }
      x = nx;
      y = ny;
      wdt *= 0.72;
      // branches off the upper trunk
      if (k >= 2) {
        const ba = a + (r() < 0.5 ? -1 : 1) * (0.5 + r() * 0.6);
        const bl = 10 + r() * 14;
        const bx = x + Math.cos(ba) * bl;
        const by = y + Math.sin(ba) * bl;
        c.lineWidth = Math.max(1, wdt * 0.8);
        c.beginPath();
        c.moveTo(x, y);
        c.quadraticCurveTo((x + bx) / 2, (y + by) / 2 - 3, bx, by);
        c.stroke();
        tips.push([bx, by]);
      }
    }
    tips.push([x, y]);
  }

  // crown: many small clusters around the branch tips, ragged, with gaps
  for (const [tx, ty] of tips) {
    const n = 7 + Math.floor(r() * 6);
    for (let j = 0; j < n; j++) {
      const a = r() * Math.PI * 2;
      const d = r() * 12;
      const cx = tx + Math.cos(a) * d * 1.3;
      const cy = ty + Math.sin(a) * d * 0.7 - 3;
      const rx = 3 + r() * 5;
      const ry = 2 + r() * 3;
      c.fillStyle = leafDark;
      c.beginPath();
      c.ellipse(cx, cy, rx, ry, (r() - 0.5) * 0.8, 0, Math.PI * 2);
      c.fill();
      // little leaf points along the cluster edge: olive leaves are narrow
      for (let q = 0; q < 4; q++) {
        const qa = r() * Math.PI * 2;
        c.beginPath();
        c.ellipse(cx + Math.cos(qa) * rx, cy + Math.sin(qa) * ry, 2.4, 0.8, qa, 0, Math.PI * 2);
        c.fill();
      }
    }
  }
  // silver-green where the leaves catch the light (olive undersides)
  c.globalCompositeOperation = "source-atop";
  for (const [tx, ty] of tips) {
    const g = c.createRadialGradient(tx + 6, ty - 8, 0, tx + 6, ty - 8, 16);
    g.addColorStop(0, "rgba(170, 170, 140, 0.13)");
    g.addColorStop(1, "rgba(170, 170, 140, 0)");
    c.fillStyle = g;
    c.fillRect(tx - 20, ty - 30, 44, 44);
  }
  c.globalCompositeOperation = "source-over";
  c.setTransform(1, 0, 0, 1, 0, 0);
  rimLight(cv, Math.max(1, 1.3 * px), "rgba(255, 205, 140, 0.8)");
  return { cv, ox, oy, px };
}

export function cypressSprite(seed: number, px: number): TreeSprite {
  const r = rng(seed * 104729 + 7);
  const W = 30;
  const H = 120;
  const cv = mk(W * px, H * px);
  const c = cv.getContext("2d");
  const ox = (W / 2) * px;
  const oy = (H - 3) * px;
  if (!c) return { cv, ox, oy, px };
  c.translate(ox, oy);
  c.scale(px, px);
  const height = 100 + r() * 14;
  const width = 9 + r() * 3;
  c.fillStyle = "oklch(0.15 0.03 150)";
  // the flame: widest a third of the way up, tapering to a point, with a
  // feathered edge of foliage tufts so it is never a smooth spindle
  c.beginPath();
  c.moveTo(-1.5, 0);
  for (let i = 0; i <= 24; i++) {
    const t = i / 24;
    const prof = Math.sin(Math.min(1, t * 1.6) * Math.PI * 0.5) * (1 - t) ** 0.8;
    c.lineTo(-width * prof - r() * 1.6 * prof, -t * height);
  }
  for (let i = 24; i >= 0; i--) {
    const t = i / 24;
    const prof = Math.sin(Math.min(1, t * 1.6) * Math.PI * 0.5) * (1 - t) ** 0.8;
    c.lineTo(width * prof + r() * 1.6 * prof, -t * height);
  }
  c.closePath();
  c.fill();
  for (let i = 0; i < 40; i++) {
    const t = 0.1 + r() * 0.85;
    const prof = Math.sin(Math.min(1, t * 1.6) * Math.PI * 0.5) * (1 - t) ** 0.8;
    const side = r() < 0.5 ? -1 : 1;
    c.beginPath();
    c.ellipse(
      side * width * prof * (0.8 + r() * 0.3),
      -t * height,
      2 + r() * 2,
      3 + r() * 3,
      side * 0.3,
      0,
      Math.PI * 2,
    );
    c.fill();
  }
  c.fillStyle = "oklch(0.14 0.02 55)";
  c.fillRect(-1.5, -4, 3, 6);
  c.setTransform(1, 0, 0, 1, 0, 0);
  rimLight(cv, Math.max(1, 1.2 * px), "rgba(255, 205, 140, 0.7)");
  return { cv, ox, oy, px };
}

/**
 * A clump of dry grass, live (it moves): a fan of thin curved blades from one
 * root, dark against the light with tips that glow where the sun comes
 * through the seed heads. `wind` is the current lean, in radians.
 */
export function grassClump(
  c: CanvasRenderingContext2D,
  x: number,
  y: number,
  size: number,
  seed: number,
  wind: number,
  dark: string,
  glow: string,
) {
  const r = rng(seed);
  const n = 6 + Math.floor(r() * 6);
  c.lineCap = "round";
  // one path for every blade and one for every seed head: two strokes per
  // clump instead of two per blade
  const heads: number[] = [];
  c.strokeStyle = dark;
  c.lineWidth = Math.max(0.7, size * 0.035);
  c.beginPath();
  for (let i = 0; i < n; i++) {
    const spread = (r() - 0.5) * 1.2;
    const len = size * (0.6 + r() * 0.6);
    const bend = wind * (0.6 + r() * 0.8) + spread * 0.6;
    const tx = x + Math.sin(spread * 0.6 + bend) * len;
    const ty = y - Math.cos(spread * 0.6 + bend * 0.5) * len;
    c.moveTo(x + spread * size * 0.08, y);
    c.quadraticCurveTo(x + Math.sin(spread * 0.4) * len * 0.5, y - len * 0.55, tx, ty);
    if (r() < 0.55) heads.push(x + (tx - x) * 0.82, y + (ty - y) * 0.82, tx, ty);
  }
  c.stroke();
  if (heads.length) {
    c.strokeStyle = glow;
    c.lineWidth = Math.max(0.8, size * 0.045);
    c.beginPath();
    for (let i = 0; i < heads.length; i += 4) {
      c.moveTo(heads[i]!, heads[i + 1]!);
      c.lineTo(heads[i + 2]!, heads[i + 3]!);
    }
    c.stroke();
  }
}
