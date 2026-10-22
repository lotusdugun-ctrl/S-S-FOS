/**
 * The others on the hill, painted properly.
 *
 * Each figure is drawn flat into a scratch canvas of its own — base colours,
 * then the anatomy and the cloth drawn in — and then lit the way Sisyphus is:
 * a broad soft light on the side facing the sun, a core shadow turning away,
 * warm light bounced up from the ground, and a fine rim along the sunward
 * edge, all worked out from the figure's own silhouette so they follow every
 * pose. Only then does it go onto the hill.
 *
 * Every figure faces left (down the slope, toward where Sisyphus comes from)
 * with its origin on the ground under it, and `s` is the scene's scale.
 */
import type { RGB } from "./sky";

export type FigLight = {
  color: RGB;
  strength: number;
  /** unit vector toward the light, in screen space */
  lx: number;
  ly: number;
  t: number;
  /** after dark: how much the colour has drained out (0-0.6) */
  dim: number;
};

const R = 2; // scratch resolution, per CSS pixel

const pool = new Map<string, HTMLCanvasElement>();
function scratch(key: string, w: number, h: number) {
  let c = pool.get(key);
  if (!c) {
    c = document.createElement("canvas");
    pool.set(key, c);
  }
  if (c.width !== w || c.height !== h) {
    c.width = w;
    c.height = h;
  }
  return c;
}

const rgba = (c: RGB, a: number) =>
  `rgba(${Math.round(c[0])}, ${Math.round(c[1])}, ${Math.round(c[2])}, ${a.toFixed(3)})`;
const mix = (a: RGB, b: RGB, k: number): RGB => [
  a[0] + (b[0] - a[0]) * k,
  a[1] + (b[1] - a[1]) * k,
  a[2] + (b[2] - a[2]) * k,
];

/**
 * Paint a figure. `draw` draws it flat, in a frame whose origin is the
 * figure's ground point; `box` is the frame's extent around that origin,
 * in scene units (already multiplied by s).
 */
function paintLit(
  ctx: CanvasRenderingContext2D,
  key: string,
  x: number,
  y: number,
  box: { l: number; t: number; r: number; b: number },
  L: FigLight,
  s: number,
  draw: (c: CanvasRenderingContext2D) => void,
  alpha = 1,
) {
  const W = Math.max(1, Math.ceil((box.r - box.l) * R));
  const H = Math.max(1, Math.ceil((box.b - box.t) * R));
  const cv = scratch(key, W, H);
  const c = cv.getContext("2d");
  if (!c) return;
  c.setTransform(1, 0, 0, 1, 0, 0);
  c.globalCompositeOperation = "source-over";
  c.globalAlpha = 1;
  c.clearRect(0, 0, W, H);
  c.setTransform(R, 0, 0, R, -box.l * R, -box.t * R);
  c.lineCap = "round";
  c.lineJoin = "round";
  draw(c);

  // the light, at a quarter of the size and stretched back over (the stretch
  // is the softness)
  const lw = Math.max(1, Math.ceil(W / 4));
  const lh = Math.max(1, Math.ceil(H / 4));
  const lo = scratch("lo:" + key, lw, lh);
  const lc = lo.getContext("2d");
  const pass = (color: string, ox: number, oy: number) => {
    if (!lc) return;
    const k = lw / W;
    lc.setTransform(1, 0, 0, 1, 0, 0);
    lc.globalCompositeOperation = "source-over";
    lc.clearRect(0, 0, lw, lh);
    lc.drawImage(cv, 0, 0, lw, lh);
    lc.globalCompositeOperation = "source-in";
    lc.fillStyle = color;
    lc.fillRect(0, 0, lw, lh);
    lc.globalCompositeOperation = "destination-out";
    lc.drawImage(cv, ox * k, oy * k, lw, lh);
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.globalCompositeOperation = "source-atop";
    c.drawImage(lo, 0, 0, W, H);
  };
  const K = 6 * s * R;
  const warm: RGB = mix(L.color, [255, 196, 150], 0.55);
  const st = L.strength;
  pass(
    `rgba(18, 10, 14, ${(0.55 * (1 - L.dim * 0.4)).toFixed(3)})`,
    L.lx * K * 1.3,
    L.ly * K * 1.3,
  );
  pass(rgba(warm, 0.3 * st + 0.05), -L.lx * K, -L.ly * K);
  pass(`rgba(255, 168, 110, ${(0.16 * (1 - L.dim)).toFixed(3)})`, 0, -K * 0.8);
  // after dark the colour drains to moonlit blue
  if (L.dim > 0.01) {
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.globalCompositeOperation = "source-atop";
    c.fillStyle = `rgba(12, 16, 34, ${L.dim.toFixed(3)})`;
    c.fillRect(0, 0, W, H);
  }
  // the rim: the sunward edge, thin and warm
  if (st > 0.05) {
    const rim = scratch("rim:" + key, W, H);
    const rc = rim.getContext("2d");
    if (rc) {
      const e = Math.max(1.2, 1.3 * s * R);
      rc.setTransform(1, 0, 0, 1, 0, 0);
      rc.globalCompositeOperation = "source-over";
      rc.clearRect(0, 0, W, H);
      rc.drawImage(cv, 0, 0);
      rc.globalCompositeOperation = "source-in";
      rc.fillStyle = rgba(warm, 0.6 * st);
      rc.fillRect(0, 0, W, H);
      rc.globalCompositeOperation = "destination-out";
      rc.drawImage(cv, -L.lx * e, -L.ly * e);
      c.setTransform(1, 0, 0, 1, 0, 0);
      c.globalCompositeOperation = "source-atop";
      c.drawImage(rim, 0, 0);
    }
  }
  c.globalCompositeOperation = "source-over";
  ctx.save();
  ctx.globalAlpha *= alpha;
  ctx.drawImage(cv, x + box.l, y + box.t, box.r - box.l, box.b - box.t);
  ctx.restore();
}

/**
 * A limb: tapered, with a muscle belly on one side (+ toward the way the
 * figure faces, which is -x here). Joints round.
 */
function limb(
  c: CanvasRenderingContext2D,
  ax: number,
  ay: number,
  bx: number,
  by: number,
  wa: number,
  wb: number,
  belly: number,
  at: number,
  fill: string | CanvasGradient,
) {
  const dx = bx - ax;
  const dy = by - ay;
  const len = Math.hypot(dx, dy) || 1;
  let nx = -dy / len;
  let ny = dx / len;
  if (nx > 0) {
    nx = -nx;
    ny = -ny;
  }
  const mx = ax + dx * at;
  const my = ay + dy * at;
  const wm = wa + (wb - wa) * at;
  const side = (sg: number, extra: number) => {
    const p0x = ax + nx * wa * sg;
    const p0y = ay + ny * wa * sg;
    const p2x = bx + nx * wb * sg;
    const p2y = by + ny * wb * sg;
    const dpx = mx + nx * (wm + extra) * sg;
    const dpy = my + ny * (wm + extra) * sg;
    return { p0x, p0y, p2x, p2y, cx: 2 * dpx - 0.5 * (p0x + p2x), cy: 2 * dpy - 0.5 * (p0y + p2y) };
  };
  const f = side(1, Math.max(0, belly));
  const k = side(-1, Math.max(0, -belly));
  c.fillStyle = fill;
  c.beginPath();
  c.moveTo(f.p0x, f.p0y);
  c.quadraticCurveTo(f.cx, f.cy, f.p2x, f.p2y);
  c.lineTo(k.p2x, k.p2y);
  c.quadraticCurveTo(k.cx, k.cy, k.p0x, k.p0y);
  c.closePath();
  c.fill();
  c.beginPath();
  c.arc(ax, ay, wa, 0, Math.PI * 2);
  c.arc(bx, by, wb, 0, Math.PI * 2);
  c.fill();
}

/** a line along a limb, offset to one side and bowed — a muscle's edge */
function along(
  c: CanvasRenderingContext2D,
  ax: number,
  ay: number,
  bx: number,
  by: number,
  off: number,
  t0: number,
  t1: number,
  bow: number,
) {
  const dx = bx - ax;
  const dy = by - ay;
  const len = Math.hypot(dx, dy) || 1;
  let nx = -dy / len;
  let ny = dx / len;
  if (nx > 0) {
    nx = -nx;
    ny = -ny;
  }
  const tm = (t0 + t1) / 2;
  c.beginPath();
  c.moveTo(ax + dx * t0 + nx * off, ay + dy * t0 + ny * off);
  c.quadraticCurveTo(
    ax + dx * tm + nx * (off + bow),
    ay + dy * tm + ny * (off + bow),
    ax + dx * t1 + nx * off,
    ay + dy * t1 + ny * off,
  );
  c.stroke();
}

const ink = (c: CanvasRenderingContext2D, a: number, w: number) => {
  c.strokeStyle = `rgba(36, 14, 8, ${a})`;
  c.lineWidth = w;
};
const shine = (c: CanvasRenderingContext2D, a: number, w: number) => {
  c.strokeStyle = `rgba(255, 220, 185, ${a})`;
  c.lineWidth = w;
};
/** draw only onto what is already there */
const atop = (c: CanvasRenderingContext2D, fn: () => void) => {
  c.save();
  c.globalCompositeOperation = "source-atop";
  fn();
  c.restore();
};

/** a head in profile, facing left, as one path: skull, brow, nose, lips, chin */
function profile(
  c: CanvasRenderingContext2D,
  x: number,
  y: number,
  r: number,
  fill: string,
  open = 0,
) {
  c.fillStyle = fill;
  c.beginPath();
  c.moveTo(x + 0.95 * r, y - 0.1 * r); // back of the skull
  c.quadraticCurveTo(x + 0.95 * r, y - 1.05 * r, x + 0.05 * r, y - 1.08 * r); // crown
  c.quadraticCurveTo(x - 0.72 * r, y - 1.0 * r, x - 0.82 * r, y - 0.42 * r); // forehead
  c.quadraticCurveTo(x - 0.96 * r, y - 0.3 * r, x - 0.86 * r, y - 0.18 * r); // brow ridge
  c.lineTo(x - 0.88 * r, y - 0.08 * r); // eye socket
  c.lineTo(x - 1.28 * r, y + 0.3 * r); // nose
  c.quadraticCurveTo(x - 1.3 * r, y + 0.44 * r, x - 0.98 * r, y + 0.46 * r); // nostril
  c.quadraticCurveTo(x - 0.9 * r, y + 0.56 * r, x - 0.96 * r, y + 0.66 * r); // upper lip
  c.lineTo(x - 0.9 * r, y + 0.72 * r + open * 0.3 * r);
  c.quadraticCurveTo(
    x - 0.98 * r,
    y + 0.82 * r + open * 0.3 * r,
    x - 0.88 * r,
    y + 0.92 * r + open * 0.32 * r,
  ); // lower lip
  c.quadraticCurveTo(
    x - 0.94 * r,
    y + 1.18 * r + open * 0.32 * r,
    x - 0.62 * r,
    y + 1.2 * r + open * 0.32 * r,
  ); // chin
  c.quadraticCurveTo(x - 0.1 * r, y + 1.18 * r + open * 0.2 * r, x + 0.42 * r, y + 0.62 * r); // jaw
  c.quadraticCurveTo(x + 0.62 * r, y + 0.4 * r, x + 0.95 * r, y - 0.1 * r);
  c.closePath();
  c.fill();
}

/** the eye, ear and brow on a profile; `pain` knots the brow, `shut` closes the eye */
function face(
  c: CanvasRenderingContext2D,
  x: number,
  y: number,
  r: number,
  o: { pain?: number; shut?: number; open?: number; ear?: boolean },
) {
  const pain = o.pain ?? 0;
  atop(c, () => {
    // the socket's shadow
    c.fillStyle = "rgba(40, 18, 12, 0.35)";
    c.beginPath();
    c.ellipse(x - 0.62 * r, y - 0.08 * r, 0.26 * r, 0.18 * r, 0, 0, Math.PI * 2);
    c.fill();
    // the eye
    if ((o.shut ?? 0) > 0.5) {
      ink(c, 0.75, 0.09 * r);
      c.beginPath();
      c.moveTo(x - 0.8 * r, y - 0.04 * r);
      c.quadraticCurveTo(x - 0.64 * r, y + 0.06 * r, x - 0.48 * r, y - 0.04 * r);
      c.stroke();
    } else {
      c.fillStyle = "rgba(235, 225, 210, 0.9)";
      c.beginPath();
      c.ellipse(x - 0.66 * r, y - 0.06 * r, 0.14 * r, 0.08 * r, 0, 0, Math.PI * 2);
      c.fill();
      c.fillStyle = "#1a0f0a";
      c.beginPath();
      c.arc(x - 0.74 * r, y - 0.06 * r, 0.065 * r, 0, Math.PI * 2);
      c.fill();
      ink(c, 0.8, 0.07 * r);
      c.beginPath();
      c.moveTo(x - 0.82 * r, y - 0.13 * r);
      c.quadraticCurveTo(x - 0.66 * r, y - 0.2 * r, x - 0.5 * r, y - 0.1 * r);
      c.stroke();
    }
    // the brow
    ink(c, 0.85, 0.12 * r);
    c.beginPath();
    c.moveTo(x - 0.88 * r, y - (0.32 - pain * 0.12) * r);
    c.quadraticCurveTo(
      x - 0.66 * r,
      y - (0.42 + pain * 0.1) * r,
      x - 0.42 * r,
      y - (0.32 - pain * 0.05) * r,
    );
    c.stroke();
    if (pain > 0.3) {
      // the furrow between the brows, the creases at the eye
      ink(c, 0.4 * pain, 0.05 * r);
      c.beginPath();
      c.moveTo(x - 0.76 * r, y - 0.52 * r);
      c.lineTo(x - 0.7 * r, y - 0.36 * r);
      c.moveTo(x - 0.46 * r, y + 0.02 * r);
      c.lineTo(x - 0.36 * r, y + 0.1 * r);
      c.stroke();
    }
    // nostril and the line of the cheek
    ink(c, 0.4, 0.06 * r);
    c.beginPath();
    c.moveTo(x - 1.05 * r, y + 0.4 * r);
    c.quadraticCurveTo(x - 0.98 * r, y + 0.32 * r, x - 0.92 * r, y + 0.4 * r);
    c.moveTo(x - 0.86 * r, y + 0.5 * r);
    c.quadraticCurveTo(x - 0.62 * r, y + 0.42 * r, x - 0.5 * r, y + 0.7 * r);
    c.stroke();
    if ((o.open ?? 0) > 0.2) {
      c.fillStyle = "#1b0907";
      c.beginPath();
      c.ellipse(
        x - 0.9 * r,
        y + 0.78 * r + (o.open ?? 0) * 0.16 * r,
        0.1 * r,
        0.06 * r + (o.open ?? 0) * 0.14 * r,
        0,
        0,
        Math.PI * 2,
      );
      c.fill();
    }
    if (o.ear !== false) {
      c.fillStyle = "rgba(70, 30, 20, 0.35)";
      c.beginPath();
      c.ellipse(x + 0.18 * r, y + 0.06 * r, 0.16 * r, 0.26 * r, 0.15, 0, Math.PI * 2);
      c.fill();
    }
  });
}

/** hair as a mass with strands combed through it */
function strands(
  c: CanvasRenderingContext2D,
  pts: Array<[number, number, number, number]>,
  col: string,
  w: number,
) {
  c.strokeStyle = col;
  c.lineWidth = w;
  c.beginPath();
  for (const [x0, y0, x1, y1] of pts) {
    c.moveTo(x0, y0);
    c.quadraticCurveTo((x0 + x1) / 2 + (y1 - y0) * 0.15, (y0 + y1) / 2, x1, y1);
  }
  c.stroke();
}

// =====================================================================
//  Prometheus and Zeus's eagle
// =====================================================================

export type EagleVisit = { phase: "away" | "arrive" | "feed" | "leave"; t: number };

/** a small deterministic hash */
function hh(i: number, k: number) {
  const v = Math.sin(i * 127.1 + k * 311.7) * 43758.5453;
  return v - Math.floor(v);
}

/**
 * The crag he is chained to, drawn straight onto the hill behind him: a
 * fractured column of dark rock, every block cut by the light into a lit
 * face and a face in shadow, bedded in horizontal strata, scarred, with a
 * crust of lichen, and the old blood of every day down the front of it.
 */
function cliff(ctx: CanvasRenderingContext2D, x: number, y: number, s: number, L: FigLight) {
  const lit = Math.max(0.15, 0.5 - L.lx * 0.5) * (0.4 + 0.6 * L.strength) * (1 - L.dim * 0.7);
  const outline: Array<[number, number]> = [
    [-24, 4],
    [-21, -22],
    [-24, -38],
    [-17, -60],
    [-15, -84],
    [-8, -98],
    [-6, -118],
    [2, -134],
    [9, -142],
    [13, -152],
    [19, -146],
    [25, -150],
    [31, -138],
    [36, -122],
    [41, -116],
    [44, -92],
    [50, -74],
    [53, -48],
    [58, -30],
    [62, 4],
  ];
  const P = (q: [number, number]) => [x + q[0] * s, y + q[1] * s] as const;
  ctx.save();
  ctx.beginPath();
  outline.forEach((q, i) => (i ? ctx.lineTo(...P(q)) : ctx.moveTo(...P(q))));
  ctx.closePath();
  const base = ctx.createLinearGradient(0, y - 150 * s, 0, y);
  base.addColorStop(0, "#3a3230");
  base.addColorStop(1, "#231d1c");
  ctx.fillStyle = base;
  ctx.fill();
  ctx.clip();
  // the rock breaks in long vertical shards, as columnar basalt does: each
  // shard a lit face and a face in shadow, each a little different
  let bx = x - 28 * s;
  let i = 0;
  while (bx < x + 64 * s) {
    const bw = (9 + hh(i, 1) * 13) * s;
    const top = y - (150 - (Math.abs(bx - x - 18 * s) / s) * 1.1 - hh(i, 2) * 30) * s;
    const ridge = bx + bw * (0.35 + hh(i, 3) * 0.3);
    const litLeft = L.lx < 0;
    // a shard may break across partway down
    const brk = top + (30 + hh(i, 4) * 60) * s;
    for (const [y0, y1, k] of [
      [y + 4 * s, brk, 0],
      [brk - 1.5 * s, top, 1],
    ] as const) {
      const v = 0.75 + hh(i, 5 + k) * 0.5;
      const sh = (k ? 1.5 : -1) * s;
      ctx.fillStyle = `rgba(${150 + lit * 70}, ${122 + lit * 50}, ${104 + lit * 30}, ${((0.06 + lit * 0.2) * v).toFixed(3)})`;
      ctx.beginPath();
      if (litLeft) {
        ctx.moveTo(bx, y0);
        ctx.lineTo(bx + sh, y1);
        ctx.lineTo(ridge + sh, y1);
        ctx.lineTo(ridge, y0);
      } else {
        ctx.moveTo(ridge, y0);
        ctx.lineTo(ridge + sh, y1);
        ctx.lineTo(bx + bw + sh, y1);
        ctx.lineTo(bx + bw, y0);
      }
      ctx.closePath();
      ctx.fill();
      ctx.fillStyle = `rgba(6, 3, 4, ${(0.22 * v).toFixed(3)})`;
      ctx.beginPath();
      if (litLeft) {
        ctx.moveTo(ridge, y0);
        ctx.lineTo(ridge + sh, y1);
        ctx.lineTo(bx + bw + sh, y1);
        ctx.lineTo(bx + bw, y0);
      } else {
        ctx.moveTo(bx, y0);
        ctx.lineTo(bx + sh, y1);
        ctx.lineTo(ridge + sh, y1);
        ctx.lineTo(ridge, y0);
      }
      ctx.closePath();
      ctx.fill();
    }
    // the break, and the joint to the next shard
    ctx.strokeStyle = "rgba(8, 4, 4, 0.55)";
    ctx.lineWidth = 0.8 * s;
    ctx.beginPath();
    ctx.moveTo(bx, brk);
    ctx.lineTo(bx + bw * 0.5, brk + (hh(i, 8) - 0.5) * 4 * s);
    ctx.lineTo(bx + bw, brk + (hh(i, 9) - 0.5) * 3 * s);
    ctx.moveTo(bx + bw, y + 4 * s);
    ctx.lineTo(bx + bw + 1 * s, top);
    ctx.stroke();
    bx += bw;
    i++;
  }
  // the whole far side of the crag turned away from the light
  const sideG = ctx.createLinearGradient(x - 24 * s, 0, x + 62 * s, 0);
  const away = L.lx < 0 ? [0, 0.62] : [0.62, 0];
  sideG.addColorStop(0, `rgba(8, 4, 6, ${away[0]})`);
  sideG.addColorStop(0.45, "rgba(8, 4, 6, 0.18)");
  sideG.addColorStop(1, `rgba(8, 4, 6, ${away[1]})`);
  ctx.fillStyle = sideG;
  ctx.fillRect(x - 30 * s, y - 160 * s, 100 * s, 170 * s);
  // fissures
  ctx.strokeStyle = "rgba(0, 0, 0, 0.6)";
  ctx.lineWidth = 1 * s;
  ctx.beginPath();
  for (const [x0, y0, n] of [
    [4, -130, 5],
    [32, -124, 6],
    [-12, -74, 3],
    [20, -60, 4],
  ] as const) {
    let px = x + x0 * s;
    let py = y + y0 * s;
    ctx.moveTo(px, py);
    for (let k = 0; k < n; k++) {
      px += (hh(x0, k) - 0.5) * 8 * s;
      py += (8 + hh(y0, k) * 6) * s;
      ctx.lineTo(px, py);
    }
  }
  ctx.stroke();
  // lichen: crusts of rust and grey-green on the lit, wet-free faces
  for (let i = 0; i < 26; i++) {
    const lx = x + (-18 + hh(i, 7) * 70) * s;
    const ly = y - hh(i, 9) * 140 * s;
    ctx.fillStyle = i % 3 === 0 ? "rgba(150, 110, 40, 0.28)" : "rgba(120, 130, 100, 0.22)";
    ctx.beginPath();
    ctx.ellipse(
      lx,
      ly,
      (1 + hh(i, 3) * 2.4) * s,
      (0.8 + hh(i, 4) * 1.4) * s,
      hh(i, 5) * 3,
      0,
      Math.PI * 2,
    );
    ctx.fill();
  }
  // old blood down the face of the rock, dried black over older rust
  ctx.strokeStyle = "rgba(70, 14, 8, 0.45)";
  ctx.lineWidth = 3.4 * s;
  ctx.beginPath();
  ctx.moveTo(x - 6 * s, y - 56 * s);
  ctx.quadraticCurveTo(x - 3 * s, y - 26 * s, x - 7 * s, y);
  ctx.stroke();
  ctx.strokeStyle = "rgba(90, 8, 6, 0.7)";
  ctx.lineWidth = 1.6 * s;
  ctx.beginPath();
  ctx.moveTo(x - 6 * s, y - 50 * s);
  ctx.quadraticCurveTo(x - 4 * s, y - 24 * s, x - 7 * s, y);
  ctx.moveTo(x + 4 * s, y - 44 * s);
  ctx.quadraticCurveTo(x + 6 * s, y - 20 * s, x + 3 * s, y);
  ctx.stroke();
  ctx.restore();
  // a crisp lit edge where the crag meets the sky on the light's side
  ctx.save();
  ctx.strokeStyle = `rgba(240, 206, 170, ${(0.28 * lit).toFixed(3)})`;
  ctx.lineWidth = 1 * s;
  ctx.beginPath();
  const edge = L.lx < 0 ? outline.slice(0, 11) : outline.slice(10);
  edge.forEach((q, i) => (i ? ctx.lineTo(...P(q)) : ctx.moveTo(...P(q))));
  ctx.stroke();
  ctx.restore();
}

/**
 * Prometheus: a titan on one knee at the foot of the cliff, both wrists
 * chained high above him so his whole body hangs open to the bird. While it
 * feeds he throws his head back and the muscles stand out of him; after, he
 * hangs slack.
 */
export function drawPrometheus(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  L: FigLight,
  eagle: EagleVisit = { phase: "away", t: 0 },
) {
  const t = L.t;
  const feeding = eagle.phase === "feed";
  const writhe = feeding
    ? Math.sin(t * 6.5) * 0.7 + Math.sin(t * 11) * 0.3
    : Math.sin(t * 0.8) * 0.08;
  const slack = feeding ? 0 : 1;
  cliff(ctx, x + 10 * s, y, s, L);
  // the rings in the rock
  const ringN = { x: x - 6 * s, y: y - 118 * s };
  const ringF = { x: x + 26 * s, y: y - 124 * s };
  paintLit(ctx, "prometheus", x, y, { l: -70 * s, t: -140 * s, r: 60 * s, b: 6 * s }, L, s, (c) => {
    const SK: RGB = [150, 98, 70];
    const skin = (k: number) => rgba(mix(SK, [60, 34, 24], k), 1);
    // pose
    const hip = { x: 2 * s, y: -27 * s + slack * 1.5 * s };
    const sh = { x: -2 * s + writhe * 1.6 * s, y: -64 * s + slack * 3 * s };
    const shN = { x: sh.x - 7 * s, y: sh.y + 1 * s };
    const shF = { x: sh.x + 7 * s, y: sh.y - 1 * s };
    const wrN = {
      x: ringN.x - x - 4 * s + writhe * 1.5 * s,
      y: ringN.y - y + 22 * s + slack * 5 * s,
    };
    const wrF = {
      x: ringF.x - x - 2 * s - writhe * 1.5 * s,
      y: ringF.y - y + 22 * s + slack * 5 * s,
    };
    const elbow = (a: { x: number; y: number }, b: { x: number; y: number }, out: number) => ({
      x: (a.x + b.x) / 2 + out * s,
      y: (a.y + b.y) / 2 + 2 * s,
    });
    const elN = elbow(shN, wrN, -6);
    const elF = elbow(shF, wrF, 6);

    // chains first, behind everything: forged links, alternately seen
    // face-on and edge-on, sagging between the ring and the cuff
    for (const [r, w] of [
      [ringN, wrN],
      [ringF, wrF],
    ] as const) {
      const rx = r.x - x;
      const ry = r.y - y;
      const n = 11;
      const pts: Array<[number, number]> = [];
      for (let i = 0; i <= n; i++) {
        const k = i / n;
        pts.push([
          rx + (w.x - rx) * k,
          ry + (w.y - ry) * k + Math.sin(k * Math.PI) * 4 * s * slack,
        ]);
      }
      for (let i = 0; i < n; i++) {
        const [ax, ay] = pts[i]!;
        const [bx, by] = pts[i + 1]!;
        const a = Math.atan2(by - ay, bx - ax);
        const mx = (ax + bx) / 2;
        const my = (ay + by) / 2;
        const half = Math.hypot(bx - ax, by - ay) * 0.62;
        c.save();
        c.translate(mx, my);
        c.rotate(a);
        if (i % 2 === 0) {
          c.strokeStyle = "#151619";
          c.lineWidth = 1.2 * s;
          c.beginPath();
          c.ellipse(0, 0, half, 1.5 * s, 0, 0, Math.PI * 2);
          c.stroke();
          c.strokeStyle = "rgba(150, 140, 130, 0.22)";
          c.lineWidth = 0.35 * s;
          c.beginPath();
          c.ellipse(0, -0.3 * s, half * 0.9, 1.2 * s, 0, Math.PI * 1.1, Math.PI * 1.9);
          c.stroke();
        } else {
          c.fillStyle = "#121315";
          c.beginPath();
          c.roundRect(-half, -0.65 * s, half * 2, 1.3 * s, 0.6 * s);
          c.fill();
        }
        c.restore();
      }
      // the ring, hammered into the rock with an iron staple
      c.strokeStyle = "#232428";
      c.lineWidth = 1.4 * s;
      c.beginPath();
      c.arc(rx, ry, 2.6 * s, 0, Math.PI * 2);
      c.stroke();
      c.fillStyle = "#16171a";
      c.fillRect(rx - 1.6 * s, ry - 4.4 * s, 3.2 * s, 2.4 * s);
      c.fillStyle = "rgba(120, 50, 20, 0.45)";
      c.beginPath();
      c.ellipse(rx, ry + 5 * s, 1.2 * s, 4 * s, 0, 0, Math.PI * 2);
      c.fill();
    }

    // the far arm and leg, a shade darker
    limb(c, shF.x, shF.y, elF.x, elF.y, 4.6 * s, 3.6 * s, 1.6 * s, 0.45, skin(0.35));
    limb(c, elF.x, elF.y, wrF.x, wrF.y, 3.6 * s, 2.4 * s, 1.2 * s, 0.3, skin(0.35));
    const kneeF = { x: -14 * s, y: -30 * s + writhe * 1.2 * s };
    const footF = { x: -15 * s, y: -1 * s };
    limb(c, hip.x + 2 * s, hip.y, kneeF.x, kneeF.y, 7 * s, 4.4 * s, 2.6 * s, 0.4, skin(0.3));
    limb(c, kneeF.x, kneeF.y, footF.x, footF.y, 4.4 * s, 2.6 * s, -2 * s, 0.32, skin(0.3));
    c.fillStyle = skin(0.35);
    c.beginPath();
    c.ellipse(footF.x - 3 * s, footF.y - 0.5 * s, 5 * s, 1.8 * s, 0, 0, Math.PI * 2);
    c.fill();

    // the near leg: kneeling, the shin flat along the ground behind him
    const kneeN = { x: -6 * s, y: -4 * s };
    const footN = { x: 16 * s, y: -2.4 * s };
    limb(c, hip.x, hip.y, kneeN.x, kneeN.y, 7.6 * s, 4.6 * s, 2.8 * s, 0.42, skin(0.08));
    limb(c, kneeN.x, kneeN.y, footN.x, footN.y, 4.4 * s, 2.4 * s, -2.2 * s, 0.35, skin(0.12));

    // the torso: a titan's, ribs pulled open by the raised arms
    const torso = c.createLinearGradient(sh.x - 14 * s, 0, sh.x + 14 * s, 0);
    torso.addColorStop(0, skin(-0.1));
    torso.addColorStop(0.6, skin(0.05));
    torso.addColorStop(1, skin(0.35));
    c.fillStyle = torso;
    c.beginPath();
    c.moveTo(hip.x - 9 * s, hip.y + 2 * s);
    c.quadraticCurveTo(hip.x - 12 * s, hip.y - 14 * s, sh.x - 13 * s, sh.y + 12 * s); // flank, chest
    c.quadraticCurveTo(sh.x - 15 * s, sh.y + 2 * s, shN.x - 2 * s, shN.y - 3 * s); // pectoral to armpit
    c.quadraticCurveTo(sh.x, sh.y - 6 * s, shF.x + 3 * s, shF.y - 2 * s); // trapezius
    c.quadraticCurveTo(sh.x + 14 * s, sh.y + 12 * s, hip.x + 10 * s, hip.y - 6 * s); // lat, back
    c.quadraticCurveTo(hip.x + 11 * s, hip.y, hip.x + 8 * s, hip.y + 3 * s);
    c.closePath();
    c.fill();
    // anatomy
    atop(c, () => {
      ink(c, 0.45, 0.9 * s);
      // pectoral's lower edge
      c.beginPath();
      c.moveTo(sh.x - 13 * s, sh.y + 10 * s);
      c.quadraticCurveTo(sh.x - 6 * s, sh.y + 14 * s, sh.x + 1 * s, sh.y + 9 * s);
      c.stroke();
      // the ribs, standing out of a body stretched by its arms
      ink(c, 0.32, 0.7 * s);
      for (let i = 0; i < 4; i++) {
        const ry = sh.y + 15 * s + i * 3.4 * s;
        c.beginPath();
        c.moveTo(sh.x - 12 * s + i * 0.6 * s, ry);
        c.quadraticCurveTo(sh.x - 5 * s, ry - 2 * s, sh.x + 2 * s, ry + 1.5 * s);
        c.stroke();
      }
      // the belly: a long line and the bands across it
      ink(c, 0.38, 0.8 * s);
      c.beginPath();
      c.moveTo(sh.x - 6 * s, sh.y + 13 * s);
      c.quadraticCurveTo(hip.x - 8 * s, hip.y - 10 * s, hip.x - 5 * s, hip.y);
      for (const k of [0.25, 0.5, 0.75]) {
        const by = sh.y + 13 * s + (hip.y - sh.y - 13 * s) * k;
        c.moveTo(sh.x - 11 * s + k * 1 * s, by);
        c.quadraticCurveTo(sh.x - 7 * s, by - 1.4 * s, sh.x - 2 * s, by + 0.6 * s);
      }
      // the oblique into the hip
      c.moveTo(sh.x + 2 * s, sh.y + 20 * s);
      c.quadraticCurveTo(hip.x - 2 * s, hip.y - 8 * s, hip.x - 8 * s, hip.y + 1 * s);
      c.stroke();
      // serratus
      ink(c, 0.35, 0.7 * s);
      c.beginPath();
      for (let i = 0; i < 3; i++) {
        c.moveTo(sh.x - 13 * s, sh.y + 8 * s + i * 3 * s);
        c.lineTo(sh.x - 9 * s, sh.y + 10 * s + i * 3 * s);
      }
      c.stroke();
      // light on the chest and along the flank
      shine(c, 0.22, 2 * s);
      c.beginPath();
      c.moveTo(sh.x - 12 * s, sh.y + 4 * s);
      c.quadraticCurveTo(sh.x - 8 * s, sh.y + 2 * s, sh.x - 3 * s, sh.y + 4 * s);
      c.stroke();
    });

    // the wound under the ribs on his near side: torn, wet, never closing
    const wd = { x: sh.x - 4 * s, y: sh.y + 22 * s };
    const open = feeding ? 1 : eagle.phase === "leave" ? 0.9 : 0.6;
    const wg = c.createRadialGradient(wd.x, wd.y, 0, wd.x, wd.y, 7 * s);
    wg.addColorStop(0, "#1c0202");
    wg.addColorStop(0.45, "#5e0906");
    wg.addColorStop(0.8, "rgba(120, 18, 12, 0.85)");
    wg.addColorStop(1, "rgba(130, 30, 20, 0)");
    c.fillStyle = wg;
    c.beginPath();
    c.ellipse(wd.x, wd.y, 5 * s * open, 7.5 * s * open, 0.35, 0, Math.PI * 2);
    c.fill();
    // torn edges
    c.strokeStyle = "rgba(170, 70, 60, 0.7)";
    c.lineWidth = 0.6 * s;
    c.beginPath();
    for (let i = 0; i < 7; i++) {
      const a = (i / 7) * Math.PI * 2;
      c.moveTo(wd.x + Math.cos(a) * 3.6 * s * open, wd.y + Math.sin(a) * 5.4 * s * open);
      c.lineTo(wd.x + Math.cos(a + 0.2) * 5 * s * open, wd.y + Math.sin(a + 0.2) * 7 * s * open);
    }
    c.stroke();
    // blood down the belly and the thigh
    c.strokeStyle = "rgba(115, 10, 6, 0.9)";
    c.lineWidth = 1.3 * s;
    c.beginPath();
    c.moveTo(wd.x - 1 * s, wd.y + 5 * s);
    c.bezierCurveTo(wd.x + 1 * s, wd.y + 12 * s, wd.x - 2 * s, hip.y - 4 * s, wd.x, hip.y + 6 * s);
    c.moveTo(wd.x + 3 * s, wd.y + 6 * s);
    c.quadraticCurveTo(wd.x + 5 * s, wd.y + 12 * s, wd.x + 4 * s, wd.y + 16 * s);
    c.stroke();

    // a torn loincloth
    c.fillStyle = "#4d1712";
    c.beginPath();
    c.moveTo(hip.x - 10 * s, hip.y - 4 * s);
    c.quadraticCurveTo(hip.x, hip.y - 6 * s, hip.x + 10 * s, hip.y - 5 * s);
    c.lineTo(hip.x + 8 * s, hip.y + 8 * s);
    c.lineTo(hip.x + 3 * s, hip.y + 5 * s);
    c.lineTo(hip.x - 1 * s, hip.y + 11 * s);
    c.lineTo(hip.x - 5 * s, hip.y + 6 * s);
    c.lineTo(hip.x - 11 * s, hip.y + 9 * s);
    c.closePath();
    c.fill();
    atop(c, () => {
      c.strokeStyle = "rgba(0, 0, 0, 0.35)";
      c.lineWidth = 0.7 * s;
      c.beginPath();
      for (const fx of [-6, -1, 4]) {
        c.moveTo(hip.x + fx * s, hip.y - 4 * s);
        c.lineTo(hip.x + (fx + 1) * s, hip.y + 7 * s);
      }
      c.stroke();
    });

    // the near arm, up to the chain: deltoid, biceps, forearm, a fist on the links
    limb(c, shN.x, shN.y, elN.x, elN.y, 5.2 * s, 3.8 * s, 2 * s, 0.45, skin(0));
    limb(c, elN.x, elN.y, wrN.x, wrN.y, 3.8 * s, 2.5 * s, 1.4 * s, 0.3, skin(0.02));
    atop(c, () => {
      ink(c, 0.35, 0.7 * s);
      along(c, shN.x, shN.y, elN.x, elN.y, 0, 0.2, 0.85, 1 * s);
      along(c, elN.x, elN.y, wrN.x, wrN.y, 0.6 * s, 0.1, 0.6, 0.8 * s);
      shine(c, 0.2, 1.4 * s);
      along(c, shN.x, shN.y, elN.x, elN.y, 2.6 * s, 0.25, 0.75, 1 * s);
    });
    for (const w of [wrN, wrF]) {
      c.fillStyle = skin(0.1);
      c.beginPath();
      c.ellipse(w.x, w.y - 2 * s, 3 * s, 2.6 * s, 0, 0, Math.PI * 2);
      c.fill();
      // the iron cuff
      c.fillStyle = "#2f3136";
      c.beginPath();
      c.ellipse(w.x, w.y + 1.5 * s, 3.8 * s, 2.2 * s, 0.2, 0, Math.PI * 2);
      c.fill();
      c.strokeStyle = "rgba(200, 200, 210, 0.25)";
      c.lineWidth = 0.6 * s;
      c.stroke();
    }

    // the neck, straining
    const hd = {
      x: sh.x - 4 * s + writhe * 1.5 * s,
      y: sh.y - 12 * s + slack * 2 * s,
    };
    limb(c, sh.x - 1 * s, sh.y, hd.x + 1 * s, hd.y + 4 * s, 5 * s, 3.8 * s, 1 * s, 0.5, skin(0.05));
    // the head: thrown back in a scream while it feeds, hung forward after
    c.save();
    c.translate(hd.x, hd.y);
    c.rotate(feeding ? -0.45 + writhe * 0.15 : 0.35 * slack);
    const r = 5.6 * s;
    // hair, long and matted, falling behind
    c.fillStyle = "#231c19";
    c.beginPath();
    c.moveTo(-0.6 * r, -1.05 * r);
    c.quadraticCurveTo(0.8 * r, -1.5 * r, 1.4 * r, -0.4 * r);
    c.quadraticCurveTo(1.9 * r, 0.8 * r, 1.5 * r, 2.2 * r);
    c.lineTo(0.6 * r, 1.2 * r);
    c.quadraticCurveTo(0.2 * r, -0.2 * r, -0.6 * r, -1.05 * r);
    c.fill();
    profile(c, 0, 0, r, skin(0), feeding ? 0.9 + writhe * 0.3 : 0.1);
    face(c, 0, 0, r, { pain: feeding ? 1 : 0.6, shut: feeding ? 1 : 0, open: feeding ? 0.9 : 0.1 });
    // the beard: heavy, curling, over the jaw and down the throat
    c.fillStyle = "#2a211d";
    c.beginPath();
    c.moveTo(-0.92 * r, 0.62 * r);
    c.quadraticCurveTo(-1.2 * r, 1.6 * r, -0.4 * r, 2.1 * r);
    c.quadraticCurveTo(0.2 * r, 1.9 * r, 0.6 * r, 0.7 * r);
    c.quadraticCurveTo(0.2 * r, 0.9 * r, -0.4 * r, 0.6 * r);
    c.closePath();
    c.fill();
    // hair over the brow
    c.beginPath();
    c.moveTo(-0.8 * r, -0.5 * r);
    c.quadraticCurveTo(-0.6 * r, -1.2 * r, 0.3 * r, -1.15 * r);
    c.quadraticCurveTo(-0.2 * r, -0.9 * r, -0.8 * r, -0.5 * r);
    c.fill();
    strands(
      c,
      [
        [-0.3 * r, -1.1 * r, 1.2 * r, 0.6 * r],
        [0.2 * r, -1.2 * r, 1.6 * r, 1.4 * r],
        [0.6 * r, -0.9 * r, 1.4 * r, 2 * r],
        [-0.8 * r, 0.9 * r, -0.5 * r, 1.9 * r],
        [-0.4 * r, 0.9 * r, -0.1 * r, 1.8 * r],
      ],
      "rgba(120, 100, 90, 0.35)",
      0.4 * s,
    );
    c.restore();

    // the bird
    if (eagle.phase !== "away") {
      const perch = { x: hip.x + 4 * s, y: hip.y - 8 * s };
      let ex = perch.x;
      let ey = perch.y;
      let pose: EaglePose = { spread: 0.5, flap: 0, peck: 0, perched: true };
      if (eagle.phase === "arrive") {
        const k = Math.min(1, eagle.t / 2.2);
        const e = 1 - (1 - k) * (1 - k);
        ex = perch.x + (1 - e) * 240 * s;
        ey = perch.y - (1 - e) * 170 * s;
        pose = { spread: 1, flap: Math.sin(t * 7), peck: 0, perched: k > 0.9 };
      } else if (eagle.phase === "leave") {
        const k = Math.min(1, eagle.t / 2.2);
        ex = perch.x + k * k * 280 * s;
        ey = perch.y - k * 210 * s;
        pose = { spread: 1, flap: Math.sin(t * 8), peck: 0, perched: false };
      } else {
        const cy = (((eagle.t * 1.4) % 1) + 1) % 1;
        const peck = cy < 0.3 ? cy / 0.3 : Math.max(0, 1 - (cy - 0.3) / 0.45);
        pose = { spread: 0.45 + Math.sin(eagle.t * 2.3) * 0.12, flap: 0, peck, perched: true };
      }
      drawEagle(c, ex, ey, s * 1.4, pose);
      if (feeding && pose.peck < 0.25) {
        c.fillStyle = "rgba(125, 12, 8, 0.9)";
        for (let i = 0; i < 3; i++) {
          const p = (((t * 2.2 + i * 0.33) % 1) + 1) % 1;
          c.beginPath();
          c.arc(wd.x + (i - 1) * 2 * s, wd.y + p * 24 * s, 0.9 * s, 0, Math.PI * 2);
          c.fill();
        }
      }
    }
  });
}

/** one feather: a long tapered vane with its shaft, pointing along `ang` */
function feather(
  c: CanvasRenderingContext2D,
  x: number,
  y: number,
  ang: number,
  len: number,
  wid: number,
  fill: string,
  edge: string,
  bend = 0.12,
  notch = false,
) {
  c.save();
  c.translate(x, y);
  c.rotate(ang);
  c.fillStyle = fill;
  c.beginPath();
  c.moveTo(0, -wid * 0.35);
  c.quadraticCurveTo(
    len * 0.45,
    -wid * 0.62 - len * bend * 0.3,
    len * 0.86,
    -wid * (notch ? 0.18 : 0.38) - len * bend * 0.4,
  );
  c.quadraticCurveTo(len * 1.02, -len * bend * 0.45, len * 0.97, wid * 0.12 - len * bend * 0.4);
  c.quadraticCurveTo(len * 0.5, wid * 0.62 - len * bend * 0.2, 0, wid * 0.35);
  c.closePath();
  c.fill();
  c.strokeStyle = edge;
  c.lineWidth = Math.max(0.25, wid * 0.09);
  c.stroke();
  // the shaft, pale, a little off-centre toward the leading edge
  c.strokeStyle = "rgba(196, 170, 130, 0.35)";
  c.lineWidth = Math.max(0.2, wid * 0.07);
  c.beginPath();
  c.moveTo(0, -wid * 0.08);
  c.quadraticCurveTo(len * 0.5, -wid * 0.12 - len * bend * 0.28, len * 0.9, -len * bend * 0.42);
  c.stroke();
  c.restore();
}

type EaglePose = { spread: number; flap: number; peck: number; perched: boolean };

const EAGLE = {
  flight: "#1f150e",
  flightEdge: "rgba(8, 5, 3, 0.7)",
  covert: "#4b321e",
  covertPale: "#8a6a42",
  body: "#3a2717",
  chest: "#56391f",
  gold: "#b98a45",
  goldDeep: "#8c6430",
};

/**
 * One wing, built feather by feather in its own frame: the leading edge runs
 * along +x from the shoulder (0,0) to the tip, and the feathers trail toward
 * +y. Ten secondaries hang from the arm; seven primaries fan from the hand,
 * slotted apart at their tips like fingers; over their roots, three ranks of
 * coverts, the middle rank with the pale tawny bar of a golden eagle.
 */
function eagleWing(
  c: CanvasRenderingContext2D,
  span: number,
  chord: number,
  open: number,
  far: boolean,
) {
  const f = (col: string) => (far ? shade(col, 0.72) : col);
  const wrist = span * 0.46;
  // primaries: from the hand, fanning from trailing to pointing out along the span
  for (let i = 6; i >= 0; i--) {
    const u = wrist + (span * 0.2 * i) / 6;
    const ang = 1.35 - (i / 6) * (1.05 + open * 0.3);
    const len = span * (0.34 + i * 0.035) * (0.9 + open * 0.15);
    feather(
      c,
      u,
      -span * 0.04 * (i / 6),
      ang,
      len,
      chord * 0.24,
      f(EAGLE.flight),
      EAGLE.flightEdge,
      0.1,
      i > 2,
    );
  }
  // secondaries: a row hanging from the arm, slanting back toward the body
  for (let i = 0; i < 10; i++) {
    const u = (wrist * (i + 0.5)) / 10;
    const len = chord * (1 - Math.abs(i - 4.5) * 0.015);
    feather(c, u, 0, 1.75 - i * 0.012, len, chord * 0.26, f(EAGLE.flight), EAGLE.flightEdge, 0.06);
  }
  // greater coverts
  for (let i = 0; i < 12; i++) {
    const u = ((wrist + span * 0.08) * (i + 0.5)) / 12;
    feather(
      c,
      u,
      -chord * 0.04,
      1.7,
      chord * 0.5,
      chord * 0.22,
      f(EAGLE.covert),
      "rgba(20, 12, 6, 0.6)",
      0.05,
    );
  }
  // median coverts: the pale bar
  for (let i = 0; i < 11; i++) {
    const u = (wrist * (i + 0.6)) / 11;
    feather(
      c,
      u,
      -chord * 0.1,
      1.65,
      chord * 0.32,
      chord * 0.2,
      f(EAGLE.covertPale),
      "rgba(40, 26, 12, 0.55)",
      0.04,
    );
  }
  // lesser coverts: small scallops along the leading edge
  for (let i = 0; i < 12; i++) {
    const u = (wrist * (i + 0.5)) / 12;
    feather(
      c,
      u,
      -chord * 0.16,
      1.6,
      chord * 0.2,
      chord * 0.18,
      f(EAGLE.covert),
      "rgba(20, 12, 6, 0.5)",
      0.02,
    );
  }
  // primary coverts over the hand
  for (let i = 0; i < 5; i++) {
    const u = wrist + (span * 0.16 * i) / 5;
    feather(
      c,
      u,
      -chord * 0.1,
      1.2 - i * 0.12,
      chord * 0.42,
      chord * 0.2,
      f(EAGLE.covert),
      "rgba(20, 12, 6, 0.55)",
      0.05,
    );
  }
  // the leading edge itself: a smooth, dark, rounded bone-line
  c.strokeStyle = f("#2a1b10");
  c.lineWidth = chord * 0.16;
  c.lineCap = "round";
  c.beginPath();
  c.moveTo(0, -chord * 0.12);
  c.quadraticCurveTo(wrist * 0.6, -chord * 0.26, wrist + span * 0.1, -chord * 0.12);
  c.stroke();
}

function shade(hex: string, k: number) {
  const n = parseInt(hex.slice(1), 16);
  const r = ((n >> 16) & 255) * k;
  const g = ((n >> 8) & 255) * k;
  const b = (n & 255) * k;
  return `rgb(${r | 0}, ${g | 0}, ${b | 0})`;
}

/**
 * A golden eagle, seen from the side, facing left, feet at (x, y). On its
 * prey it mantles: both wings raised and arched over its back, the flight
 * feathers hanging like a cloak, tail fanned down for balance, head driven
 * down to tear. In the air its wings open wide with the primaries splayed.
 */
function drawEagle(c: CanvasRenderingContext2D, x: number, y: number, s: number, p: EaglePose) {
  c.save();
  c.translate(x, y);
  c.lineJoin = "round";
  const perched = p.perched;
  // the body's anchor points
  const back = { x: 5 * s, y: (perched ? -22 : -14) * s };
  const chest = { x: -5 * s, y: (perched ? -15 : -12) * s };
  const rump = { x: 14 * s, y: (perched ? -12 : -12) * s };
  const wingAt = { x: 3 * s, y: back.y + 2 * s };

  // ---- the far wing, raised behind
  c.save();
  c.translate(wingAt.x + 1.5 * s, wingAt.y - 1 * s);
  c.rotate(perched ? -1.05 - p.spread * 0.25 : -1.45 + p.flap * 0.9);
  eagleWing(c, (perched ? 30 : 44) * s, (perched ? 13 : 14) * s, perched ? p.spread : 1, true);
  c.restore();

  // ---- the tail: twelve broad feathers, fanned, banded dark at the tips
  const tailAng = perched ? 0.62 : 0.1;
  for (let i = 0; i < 9; i++) {
    const a = tailAng + (i - 4) * (perched ? 0.1 : 0.06);
    feather(
      c,
      rump.x - 2 * s,
      rump.y,
      a,
      14 * s,
      5 * s,
      i % 2 ? "#241810" : "#2b1d13",
      "rgba(10, 6, 4, 0.6)",
      0.02,
    );
  }
  c.strokeStyle = "rgba(14, 9, 6, 0.55)";
  c.lineWidth = 2.2 * s;
  c.beginPath();
  c.arc(rump.x - 2 * s, rump.y, 12 * s, tailAng - 0.42, tailAng + 0.42);
  c.stroke();

  // ---- legs: shaggy feathered trousers, then bare yellow toes and black talons
  if (perched) {
    c.fillStyle = EAGLE.body;
    c.beginPath();
    c.moveTo(-2 * s, -15 * s);
    c.quadraticCurveTo(-6 * s, -8 * s, -3 * s, -3 * s);
    // the ragged hem of the trousers
    for (let i = 0; i < 5; i++) c.lineTo((-2 + i * 1.6) * s, (i % 2 ? -4.2 : -2.6) * s);
    c.quadraticCurveTo(7 * s, -9 * s, 6 * s, -16 * s);
    c.closePath();
    c.fill();
    c.strokeStyle = "rgba(150, 110, 70, 0.3)";
    c.lineWidth = 0.45 * s;
    c.beginPath();
    for (let i = 0; i < 6; i++) {
      c.moveTo((-3 + i * 1.6) * s, -12 * s + (i % 2) * s);
      c.lineTo((-3.6 + i * 1.6) * s, -4.6 * s);
    }
    c.stroke();
    // toes, scaled and yellow, gripping
    c.strokeStyle = "#d1a634";
    c.lineCap = "round";
    c.lineWidth = 1.5 * s;
    c.beginPath();
    c.moveTo(0, -3 * s);
    c.quadraticCurveTo(-3 * s, -2 * s, -5 * s, 0);
    c.moveTo(0.5 * s, -3 * s);
    c.quadraticCurveTo(-1 * s, -1 * s, -1.8 * s, 0.8 * s);
    c.moveTo(1.5 * s, -3 * s);
    c.quadraticCurveTo(3.5 * s, -2 * s, 4 * s, 0.4 * s);
    c.stroke();
    c.strokeStyle = "rgba(120, 80, 20, 0.6)";
    c.lineWidth = 0.3 * s;
    c.beginPath();
    for (let i = 0; i < 4; i++) {
      c.moveTo((-1 - i * 1) * s, (-2.6 + i * 0.5) * s);
      c.lineTo((-1.4 - i * 1) * s, (-1.6 + i * 0.5) * s);
    }
    c.stroke();
    // talons: long, black, curved, sunk into him
    c.strokeStyle = "#0e0b0a";
    c.lineWidth = 0.9 * s;
    c.beginPath();
    c.moveTo(-5 * s, 0);
    c.quadraticCurveTo(-6.6 * s, 0.6 * s, -6.2 * s, 2.4 * s);
    c.moveTo(-1.8 * s, 0.8 * s);
    c.quadraticCurveTo(-2.8 * s, 1.8 * s, -2.2 * s, 3 * s);
    c.moveTo(4 * s, 0.4 * s);
    c.quadraticCurveTo(5.2 * s, 1.4 * s, 4.4 * s, 2.8 * s);
    c.stroke();
  } else {
    // in the air: legs drawn up, talons forward and open
    c.fillStyle = EAGLE.body;
    c.beginPath();
    c.ellipse(2 * s, -8 * s, 4 * s, 3 * s, 0.4, 0, Math.PI * 2);
    c.fill();
    c.strokeStyle = "#d1a634";
    c.lineWidth = 1.4 * s;
    c.beginPath();
    c.moveTo(0, -6 * s);
    c.lineTo(-4 * s, -3 * s);
    c.stroke();
    c.strokeStyle = "#0e0b0a";
    c.lineWidth = 0.8 * s;
    c.beginPath();
    c.moveTo(-4 * s, -3 * s);
    c.quadraticCurveTo(-6 * s, -2 * s, -5.6 * s, 0);
    c.stroke();
  }

  // ---- the body: a deep, heavy chest, the back dark
  const bg = c.createLinearGradient(0, back.y - 2 * s, 0, chest.y + 8 * s);
  bg.addColorStop(0, "#2a1c11");
  bg.addColorStop(0.5, EAGLE.body);
  bg.addColorStop(1, EAGLE.chest);
  c.fillStyle = bg;
  c.beginPath();
  c.moveTo(chest.x - 2 * s, chest.y - 4 * s);
  c.quadraticCurveTo(back.x - 8 * s, back.y - 2 * s, back.x, back.y);
  c.quadraticCurveTo(rump.x + 1 * s, back.y + 3 * s, rump.x + 1 * s, rump.y);
  c.quadraticCurveTo(rump.x - 2 * s, rump.y + 5 * s, 6 * s, -11 * s);
  c.quadraticCurveTo(-1 * s, -6 * s, chest.x - 1 * s, chest.y + 5 * s);
  c.quadraticCurveTo(chest.x - 5 * s, chest.y + 1 * s, chest.x - 2 * s, chest.y - 4 * s);
  c.closePath();
  c.fill();
  // breast and flank feathering: short overlapping scallops, streaked
  atop(c, () => {
    c.lineWidth = 0.45 * s;
    for (let r = 0; r < 4; r++) {
      for (let i = 0; i < 5; i++) {
        const fx = chest.x + (i * 3.2 + (r % 2) * 1.6) * s;
        const fy = chest.y - 1 * s + r * 2.6 * s + i * 0.6 * s;
        c.strokeStyle = "rgba(160, 118, 72, 0.32)";
        c.beginPath();
        c.moveTo(fx - 1.5 * s, fy);
        c.quadraticCurveTo(fx, fy + 1.4 * s, fx + 1.5 * s, fy);
        c.stroke();
        c.strokeStyle = "rgba(14, 8, 4, 0.35)";
        c.beginPath();
        c.moveTo(fx, fy - 0.6 * s);
        c.lineTo(fx + 0.2 * s, fy + 0.8 * s);
        c.stroke();
      }
    }
  });

  // ---- neck and head: down into the prey while it feeds
  const hx = (perched ? -11 : -13) * s - p.peck * 2.5 * s;
  const hy = (perched ? -14 : -15) * s + p.peck * 5 * s;
  const ha = perched ? 0.35 + p.peck * 0.35 : 0;
  c.fillStyle = EAGLE.body;
  c.beginPath();
  c.moveTo(chest.x + 1 * s, back.y + 1 * s);
  c.quadraticCurveTo(hx + 3 * s, hy - 5 * s, hx + 1 * s, hy - 3.2 * s);
  c.lineTo(hx + 1.6 * s, hy + 3 * s);
  c.quadraticCurveTo(chest.x - 2 * s, chest.y + 1 * s, chest.x - 1 * s, chest.y + 3 * s);
  c.closePath();
  c.fill();
  // golden hackles on the nape: pointed lanceolate feathers, not a cap
  for (let i = 0; i < 9; i++) {
    const k = i / 8;
    const bx = hx + 1 * s + k * (back.x - 2 * s - hx);
    const by = hy - 3 * s + k * (back.y + 1 * s - hy + 3 * s) + Math.sin(k * 3) * s;
    feather(
      c,
      bx,
      by,
      0.5 + k * 0.3,
      (3.6 - k * 1.2) * s,
      1.4 * s,
      i % 2 ? EAGLE.gold : EAGLE.goldDeep,
      "rgba(60, 36, 12, 0.45)",
      0.08,
    );
  }
  c.save();
  c.translate(hx, hy);
  c.rotate(-ha);
  // the skull: flat-crowned, small for the body
  c.fillStyle = "#3d2a19";
  c.beginPath();
  c.moveTo(3.4 * s, 0.8 * s);
  c.quadraticCurveTo(3.2 * s, -3.2 * s, -0.4 * s, -3.4 * s);
  c.quadraticCurveTo(-3 * s, -3.2 * s, -3.6 * s, -1.2 * s);
  c.lineTo(-3.4 * s, 1.4 * s);
  c.quadraticCurveTo(-1 * s, 3.2 * s, 2.4 * s, 2.8 * s);
  c.closePath();
  c.fill();
  // golden crown and nape
  c.fillStyle = EAGLE.gold;
  c.beginPath();
  c.moveTo(-0.6 * s, -3.3 * s);
  c.quadraticCurveTo(2.8 * s, -3.4 * s, 3.4 * s, 0.2 * s);
  c.quadraticCurveTo(2 * s, -1.4 * s, -0.2 * s, -2.2 * s);
  c.closePath();
  c.fill();
  // the beak: grey-horn at the base, black at the hook, a deep gape
  const bk = c.createLinearGradient(-3.6 * s, 0, -8.6 * s, 0);
  bk.addColorStop(0, "#6e675e");
  bk.addColorStop(0.55, "#3c3631");
  bk.addColorStop(1, "#121010");
  c.fillStyle = bk;
  c.beginPath();
  c.moveTo(-3.4 * s, -1.6 * s);
  c.quadraticCurveTo(-7.2 * s, -2.4 * s, -8.6 * s, 0.2 * s); // the culmen, arched
  c.quadraticCurveTo(-9 * s, 2.2 * s, -7.8 * s, 3.2 * s); // the hook
  c.quadraticCurveTo(-7.9 * s, 2 * s, -7.2 * s, 1.6 * s);
  c.lineTo(-3.4 * s, 1.5 * s); // lower mandible
  c.closePath();
  c.fill();
  // the cere and gape, yellow
  c.fillStyle = "#d9ae3a";
  c.beginPath();
  c.moveTo(-3.3 * s, -1.6 * s);
  c.quadraticCurveTo(-4.8 * s, -1.9 * s, -5.2 * s, -0.8 * s);
  c.lineTo(-5 * s, 0.2 * s);
  c.lineTo(-3.4 * s, 0.4 * s);
  c.closePath();
  c.fill();
  c.strokeStyle = "#cf9f2e";
  c.lineWidth = 0.5 * s;
  c.beginPath();
  c.moveTo(-2.6 * s, 1.2 * s);
  c.lineTo(-7 * s, 1.5 * s);
  c.stroke();
  // nostril
  c.fillStyle = "#3a2a10";
  c.beginPath();
  c.ellipse(-4.4 * s, -0.9 * s, 0.4 * s, 0.25 * s, 0, 0, Math.PI * 2);
  c.fill();
  // the eye under its shelf of brow: amber, hard, with a dark pupil
  c.fillStyle = "#c78d22";
  c.beginPath();
  c.arc(-1.6 * s, -1 * s, 0.95 * s, 0, Math.PI * 2);
  c.fill();
  c.fillStyle = "#070505";
  c.beginPath();
  c.arc(-1.85 * s, -1 * s, 0.5 * s, 0, Math.PI * 2);
  c.fill();
  c.fillStyle = "rgba(255, 240, 210, 0.7)";
  c.beginPath();
  c.arc(-2 * s, -1.3 * s, 0.16 * s, 0, Math.PI * 2);
  c.fill();
  c.fillStyle = "#1e140c";
  c.beginPath();
  c.moveTo(-3.6 * s, -1.8 * s);
  c.quadraticCurveTo(-1.6 * s, -3 * s, 0.4 * s, -1.9 * s);
  c.quadraticCurveTo(-1.4 * s, -2.1 * s, -3.6 * s, -1.8 * s);
  c.fill();
  if (perched && p.peck > 0.35) {
    c.fillStyle = "rgba(130, 12, 8, 0.95)";
    c.beginPath();
    c.ellipse(-8.2 * s, 2.6 * s, 1.1 * s, 0.8 * s, 0.4, 0, Math.PI * 2);
    c.fill();
    c.strokeStyle = "rgba(110, 10, 6, 0.9)";
    c.lineWidth = 0.6 * s;
    c.beginPath();
    c.moveTo(-8 * s, 3 * s);
    c.quadraticCurveTo(-8.6 * s, 5 * s, -9.4 * s, 6 * s);
    c.stroke();
  }
  c.restore();

  // ---- the near wing, arched over the back like a cloak
  c.save();
  c.translate(wingAt.x, wingAt.y);
  c.rotate(perched ? -0.42 - p.spread * 0.22 : -1.25 + p.flap * 0.9);
  eagleWing(c, (perched ? 32 : 46) * s, (perched ? 14 : 15) * s, perched ? p.spread : 1, false);
  c.restore();
  c.restore();
}

// =====================================================================
//  Merope
// =====================================================================

/**
 * Merope, a shade: a tall woman in a long chiton with a mantle over her head,
 * painted with all her folds, then laid on the hill as a ghost — translucent,
 * her feet lost in the ground mist. Her colour comes back to her act by act.
 */
export function drawMerope(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  o: { act: 1 | 2 | 3; turned: boolean; talking: boolean; leave: number; L: FigLight },
) {
  if (o.leave >= 1) return;
  const L = o.L;
  const t = L.t;
  const tone = [
    {
      cloth: [150, 160, 180] as RGB,
      mantle: [96, 106, 124] as RGB,
      skin: [180, 184, 194] as RGB,
      hair: "#383b44",
    },
    {
      cloth: [182, 166, 152] as RGB,
      mantle: [112, 96, 90] as RGB,
      skin: [206, 172, 150] as RGB,
      hair: "#2b221f",
    },
    {
      cloth: [226, 214, 194] as RGB,
      mantle: [140, 120, 104] as RGB,
      skin: [222, 184, 152] as RGB,
      hair: "#261c17",
    },
  ][o.act - 1]!;
  const alpha = ([0.6, 0.76, 0.9][o.act - 1]! + Math.sin(t * 1.7) * 0.03) * (1 - o.leave);
  const dx = o.leave * -70 * s;
  // a cold halo behind her
  const gx = x + dx;
  const gy = y - 48 * s;
  const halo = ctx.createRadialGradient(gx, gy, 0, gx, gy, 50 * s);
  halo.addColorStop(0, o.act === 3 ? "rgba(255, 236, 200, 0.2)" : "rgba(170, 190, 230, 0.16)");
  halo.addColorStop(1, "rgba(170, 190, 230, 0)");
  ctx.fillStyle = halo;
  ctx.fillRect(gx - 50 * s, gy - 60 * s, 100 * s, 120 * s);

  paintLit(
    ctx,
    "merope",
    x + dx,
    y,
    { l: -30 * s, t: -96 * s, r: 30 * s, b: 4 * s },
    L,
    s,
    (c) => {
      if (o.turned) c.scale(-1, 1);
      const sway = Math.sin(t * 0.9) * 0.6 * s;
      const br = Math.sin(t * 1.3) * 0.3 * s;
      const hx = -1 * s + sway;
      const hy = -80 * s + br;
      const col = (k: RGB, d: number) => rgba(mix(k, [20, 20, 30], d), 1);
      const P = (px: number, py: number) => [hx + px * s, hy + py * s] as const;
      // ---- the mantle's back fall, behind everything: from the crown down her back
      const mBack = c.createLinearGradient(hx, 0, hx + 16 * s, 0);
      mBack.addColorStop(0, col(tone.mantle, 0.15));
      mBack.addColorStop(1, col(tone.mantle, 0.5));
      c.fillStyle = mBack;
      c.beginPath();
      c.moveTo(...P(-2, -6));
      c.quadraticCurveTo(...P(8, -8), ...P(9, 4));
      c.quadraticCurveTo(...P(14 + sway / s, 40), 13 * s + sway * 1.4, -14 * s);
      c.quadraticCurveTo(8 * s, -10 * s, 4 * s, -16 * s);
      c.lineTo(...P(4, 14));
      c.closePath();
      c.fill();
      // ---- the chiton: a woman's body under fine cloth
      const dress = c.createLinearGradient(hx - 10 * s, 0, hx + 12 * s, 0);
      dress.addColorStop(0, col(tone.cloth, 0.02));
      dress.addColorStop(0.55, col(tone.cloth, 0.16));
      dress.addColorStop(1, col(tone.cloth, 0.42));
      c.fillStyle = dress;
      c.beginPath();
      c.moveTo(...P(-1.6, 10)); // throat
      c.quadraticCurveTo(...P(-4, 12.5), ...P(-6.4, 14.5)); // sloping front shoulder
      c.quadraticCurveTo(...P(-7.6, 17), ...P(-8.6, 21)); // over the breast
      c.quadraticCurveTo(...P(-9, 25), ...P(-6.2, 27)); // under it
      c.quadraticCurveTo(...P(-5, 30), ...P(-5.4, 32)); // the waist
      c.quadraticCurveTo(...P(-7.6, 37), ...P(-8, 43)); // the hip
      c.quadraticCurveTo(...P(-10, 56), -10 * s + sway * 0.4, -26 * s); // the thigh, the knee forward
      c.quadraticCurveTo(-11 * s, -12 * s, -13 * s + sway * 0.6, 0); // to the hem
      // the hem: scalloped where the folds break on the ground
      for (let i = 1; i <= 8; i++)
        c.lineTo(-13 * s + i * 3.1 * s + sway * 0.6 * (1 - i / 8), (i % 2 ? -1.4 : 0) * s);
      c.quadraticCurveTo(10 * s, -20 * s, ...P(8.4, 42)); // the back of the leg up to the hip
      c.quadraticCurveTo(...P(6.4, 34), ...P(5.6, 31)); // the small of the back
      c.quadraticCurveTo(...P(7.4, 22), ...P(7, 15)); // the back
      c.quadraticCurveTo(...P(5, 12), ...P(2, 10)); // the back shoulder to the nape
      c.closePath();
      c.fill();
      atop(c, () => {
        // folds from the girdle to the ground, following the leg forward
        for (let i = 0; i < 8; i++) {
          const f = -7 + i * 2;
          const x0 = hx + f * 0.7 * s;
          const y0 = hy + 34 * s;
          const kx = (f * 1.05 - 2) * s + sway * 0.3;
          const ex = (f * 1.55 + 0.5) * s + sway * 0.5;
          ink(c, 0.22 + (i % 3) * 0.06, (0.7 + (i % 2) * 0.4) * s);
          c.beginPath();
          c.moveTo(x0, y0);
          c.bezierCurveTo(x0 - 1 * s, hy + 50 * s, kx, -26 * s, ex, 0);
          c.stroke();
          shine(c, 0.12, 0.9 * s);
          c.beginPath();
          c.moveTo(x0 + 1 * s, y0);
          c.bezierCurveTo(x0, hy + 50 * s, kx + 1.1 * s, -26 * s, ex + 1.2 * s, 0);
          c.stroke();
        }
        // the light along the forward thigh, where the cloth lies on the leg
        shine(c, 0.22, 2.2 * s);
        c.beginPath();
        c.moveTo(...P(-6, 40));
        c.quadraticCurveTo(...P(-8.2, 54), -8 * s, -24 * s);
        c.stroke();
        // catenary folds over the breast, gathered at the girdle
        ink(c, 0.26, 0.6 * s);
        c.beginPath();
        for (let i = 0; i < 4; i++) {
          c.moveTo(...P(-6 + i * 0.6, 15 + i * 3));
          c.quadraticCurveTo(...P(-1, 18 + i * 3.4), ...P(5, 14 + i * 3.6));
        }
        c.stroke();
        // the girdle, crossed
        c.strokeStyle = "rgba(60, 40, 30, 0.6)";
        c.lineWidth = 1.2 * s;
        c.beginPath();
        c.moveTo(...P(-5.6, 31.5));
        c.quadraticCurveTo(...P(0, 33.5), ...P(6, 30.5));
        c.stroke();
        // the blouse of cloth over the girdle
        c.fillStyle = "rgba(255, 255, 255, 0.08)";
        c.beginPath();
        c.ellipse(...P(0, 30), 6 * s, 1.6 * s, 0.05, 0, Math.PI * 2);
        c.fill();
      });
      // ---- the mantle across the body: from the back shoulder over the front hip
      const mFront = c.createLinearGradient(...P(-8, 30), ...P(8, 16));
      mFront.addColorStop(0, col(tone.mantle, 0.02));
      mFront.addColorStop(1, col(tone.mantle, 0.35));
      c.fillStyle = mFront;
      c.beginPath();
      c.moveTo(...P(7.4, 14));
      c.quadraticCurveTo(...P(4, 24), ...P(-6.4, 33));
      c.quadraticCurveTo(...P(-9.6, 40), ...P(-9.2, 52)); // the end hanging over the forearm
      c.lineTo(...P(-6.2, 53));
      c.quadraticCurveTo(...P(-5, 44), ...P(2, 42));
      c.quadraticCurveTo(...P(8, 40), ...P(8.6, 36));
      c.closePath();
      c.fill();
      atop(c, () => {
        // U-shaped folds hanging across the swathe
        ink(c, 0.28, 0.75 * s);
        c.beginPath();
        for (let i = 0; i < 4; i++) {
          c.moveTo(...P(6.6 - i * 0.2, 18 + i * 5));
          c.quadraticCurveTo(...P(0 - i * 1.4, 32 + i * 3.4), ...P(-6 - i * 0.6, 35 + i * 3.6));
        }
        c.stroke();
        shine(c, 0.14, 0.7 * s);
        c.beginPath();
        for (let i = 0; i < 3; i++) {
          c.moveTo(...P(6.4 - i * 0.2, 20.4 + i * 5));
          c.quadraticCurveTo(
            ...P(-0.6 - i * 1.4, 34 + i * 3.4),
            ...P(-6.4 - i * 0.6, 37 + i * 3.6),
          );
        }
        c.stroke();
      });
      // ---- the arm: the hand holding the mantle's edge at her breast; when she
      // speaks it opens toward him
      const open = o.talking ? 0.5 + Math.sin(t * 2.1) * 0.5 : 0;
      const sh = { x: hx - 5.4 * s, y: hy + 15 * s };
      const el = { x: hx - 7.6 * s, y: hy + 29 * s };
      const hand = { x: hx - 5 * s - open * 7 * s, y: hy + 20 * s - open * 1 * s };
      limb(c, sh.x, sh.y, el.x, el.y, 2.4 * s, 1.8 * s, 0.5 * s, 0.4, col(tone.cloth, 0.12));
      limb(c, el.x, el.y, hand.x, hand.y, 1.6 * s, 1.1 * s, 0.4 * s, 0.4, col(tone.skin, 0.06));
      c.fillStyle = col(tone.skin, 0.02);
      c.beginPath();
      c.ellipse(hand.x, hand.y, 1.6 * s, 1.2 * s, 0.4, 0, Math.PI * 2);
      c.fill();
      ink(c, 0.3, 0.35 * s);
      c.beginPath();
      c.moveTo(hand.x - 1.2 * s, hand.y - 0.2 * s);
      c.lineTo(hand.x + 0.4 * s, hand.y + 0.4 * s);
      c.stroke();
      // ---- the neck and head
      limb(
        c,
        hx + 0.4 * s,
        hy + 11 * s,
        hx - 0.3 * s,
        hy + 4 * s,
        2 * s,
        1.8 * s,
        0,
        0.5,
        col(tone.skin, 0.12),
      );
      const r = 5.4 * s;
      profile(c, hx, hy, r, col(tone.skin, 0.02), 0);
      face(c, hx, hy, r, { shut: o.act === 1 ? 1 : 0, pain: o.act === 2 ? 0.4 : 0 });
      atop(c, () => {
        c.fillStyle = "rgba(150, 70, 70, 0.35)";
        c.beginPath();
        c.ellipse(hx - 0.94 * r, hy + 0.74 * r, 0.12 * r, 0.1 * r, 0, 0, Math.PI * 2);
        c.fill();
        // the soft shadow under the jaw
        c.fillStyle = "rgba(40, 30, 40, 0.25)";
        c.beginPath();
        c.ellipse(hx - 0.1 * r, hy + 1.2 * r, 0.7 * r, 0.25 * r, -0.3, 0, Math.PI * 2);
        c.fill();
      });
      // hair, parted, waved, drawn back under the mantle
      c.fillStyle = tone.hair;
      c.beginPath();
      c.moveTo(hx - 0.86 * r, hy - 0.5 * r);
      c.quadraticCurveTo(hx - 0.5 * r, hy - 1.2 * r, hx + 0.4 * r, hy - 1.02 * r);
      c.lineTo(hx + 0.5 * r, hy + 0.2 * r);
      c.quadraticCurveTo(hx - 0.1 * r, hy - 0.35 * r, hx - 0.86 * r, hy - 0.5 * r);
      c.fill();
      strands(
        c,
        [
          [hx - 0.7 * r, hy - 0.55 * r, hx + 0.4 * r, hy - 0.3 * r],
          [hx - 0.5 * r, hy - 0.85 * r, hx + 0.4 * r, hy - 0.05 * r],
          [hx - 0.2 * r, hy - 1 * r, hx + 0.45 * r, hy - 0.7 * r],
        ],
        "rgba(255, 255, 255, 0.12)",
        0.3 * s,
      );
      // ---- the mantle over her head, framing the face, its edge turned back
      const hood = c.createLinearGradient(hx, 0, hx + 2.2 * r, 0);
      hood.addColorStop(0, col(tone.mantle, 0));
      hood.addColorStop(1, col(tone.mantle, 0.4));
      c.fillStyle = hood;
      c.beginPath();
      c.moveTo(hx + 0.1 * r, hy - 1.22 * r);
      c.quadraticCurveTo(hx + 1.5 * r, hy - 1.45 * r, hx + 1.75 * r, hy + 0.2 * r);
      c.quadraticCurveTo(hx + 1.9 * r, hy + 1.4 * r, hx + 1.5 * r, hy + 2.6 * r);
      c.lineTo(hx + 0.6 * r, hy + 2.1 * r);
      c.quadraticCurveTo(hx + 0.75 * r, hy + 0.6 * r, hx + 0.45 * r, hy - 0.2 * r);
      c.quadraticCurveTo(hx + 0.3 * r, hy - 0.9 * r, hx - 0.4 * r, hy - 1.05 * r);
      c.closePath();
      c.fill();
      atop(c, () => {
        shine(c, 0.25, 0.6 * s);
        c.beginPath();
        c.moveTo(hx - 0.3 * r, hy - 1.12 * r);
        c.quadraticCurveTo(hx + 0.4 * r, hy - 0.95 * r, hx + 0.55 * r, hy - 0.2 * r);
        c.stroke();
        ink(c, 0.3, 0.6 * s);
        c.beginPath();
        c.moveTo(hx + 1 * r, hy - 1 * r);
        c.quadraticCurveTo(hx + 1.5 * r, hy + 0.4 * r, hx + 1.1 * r, hy + 2.2 * r);
        c.stroke();
      });
      // a shade has no feet: she dissolves into the ground mist
      c.save();
      c.setTransform(1, 0, 0, 1, 0, 0);
      c.globalCompositeOperation = "destination-out";
      const H = c.canvas.height;
      const fade = c.createLinearGradient(0, H - 26 * s * 2, 0, H);
      fade.addColorStop(0, "rgba(0,0,0,0)");
      fade.addColorStop(1, "rgba(0,0,0,0.95)");
      c.fillStyle = fade;
      c.fillRect(0, H - 26 * s * 2, c.canvas.width, 26 * s * 2);
      c.restore();
    },
    alpha,
  );
  // motes drifting off her
  ctx.save();
  for (let i = 0; i < 5; i++) {
    const p = (((t * 0.25 + i * 0.21) % 1) + 1) % 1;
    const a = Math.sin(p * Math.PI) * 0.28 * (1 - o.leave);
    ctx.fillStyle =
      o.act === 3 ? `rgba(255, 236, 200, ${a.toFixed(3)})` : `rgba(190, 205, 240, ${a.toFixed(3)})`;
    ctx.beginPath();
    ctx.arc(x + dx + (i - 2) * 5 * s, y - 16 * s - p * 80 * s, 1.1 * s, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.restore();
}

// =====================================================================
//  Tantalus
// =====================================================================

/**
 * Tantalus: starved to the bone, chest-deep in black water under a twisted
 * fig heavy with fruit. When he reaches, the branch lifts; when he bends to
 * drink, the water sinks from his cracked lips.
 */
export function drawTantalus(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  L: FigLight,
) {
  const t = L.t;
  const cyc = (((t * 0.2) % 1) + 1) % 1;
  const reach = cyc < 0.5 ? Math.sin((cyc / 0.5) * Math.PI) : 0;
  const bend = cyc >= 0.5 ? Math.sin(((cyc - 0.5) / 0.5) * Math.PI) : 0;
  const W = 40 * s;
  const surf = y - 7 * s + bend * 6 * s;
  const by = y - 66 * s - reach * 15 * s;
  figTree(ctx, x, y, s, L, by);
  // the far half of the pool, and the stones of its rim behind him
  ctx.save();
  ctx.fillStyle = "#0b1115";
  ctx.beginPath();
  ctx.ellipse(x, surf, W, 9 * s, 0, Math.PI, Math.PI * 2);
  ctx.fill();
  // the sky in the black water: a pale band, broken by his ripples
  const sky = ctx.createLinearGradient(0, surf - 9 * s, 0, surf);
  sky.addColorStop(0, rgba(mix(L.color, [90, 120, 150], 0.6), 0.0));
  sky.addColorStop(1, rgba(mix(L.color, [90, 120, 150], 0.6), 0.22 * (1 - L.dim)));
  ctx.fillStyle = sky;
  ctx.beginPath();
  ctx.ellipse(x, surf, W - 2 * s, 8 * s, 0, Math.PI, Math.PI * 2);
  ctx.fill();
  rimStones(ctx, x, surf, W, s, L, true);
  ctx.restore();

  // him, painted
  const hx = -3 * s - bend * 3 * s;
  const hy = surf - y - 22 * s + bend * 10 * s;
  paintLit(ctx, "tantalus", x, y, { l: -40 * s, t: -110 * s, r: 24 * s, b: 6 * s }, L, s, (c) => {
    const SK: RGB = [160, 128, 104];
    const skin = (k: number) => rgba(mix(SK, [60, 40, 30], k), 1);
    const sw = surf - y;
    // shoulders and chest, starved: collarbones, every rib
    // a wasted body: a thin neck on cords, shoulders that are only bone
    // under skin, the chest narrowing to a waist you could close a hand on
    const tg = c.createLinearGradient(hx - 10 * s, 0, hx + 10 * s, 0);
    tg.addColorStop(0, skin(-0.05));
    tg.addColorStop(0.55, skin(0.08));
    tg.addColorStop(1, skin(0.4));
    c.fillStyle = tg;
    c.beginPath();
    c.moveTo(hx - 6 * s, sw + 3 * s);
    c.quadraticCurveTo(hx - 8.5 * s, hy + 14 * s, hx - 9 * s, hy + 9 * s); // flank up to the armpit
    c.quadraticCurveTo(hx - 11 * s, hy + 5 * s, hx - 7.5 * s, hy + 3.6 * s); // the point of the shoulder
    c.quadraticCurveTo(hx - 4 * s, hy + 3 * s, hx - 2.4 * s, hy + 0.5 * s); // trapezius to the neck
    c.lineTo(hx - 2 * s, hy - 2 * s);
    c.lineTo(hx + 2 * s, hy - 2 * s);
    c.lineTo(hx + 2.6 * s, hy + 0.8 * s);
    c.quadraticCurveTo(hx + 5 * s, hy + 3 * s, hx + 8 * s, hy + 4 * s);
    c.quadraticCurveTo(hx + 10.5 * s, hy + 6 * s, hx + 8.5 * s, hy + 10 * s);
    c.quadraticCurveTo(hx + 7.5 * s, hy + 16 * s, hx + 6 * s, sw + 3 * s);
    c.closePath();
    c.fill();
    atop(c, () => {
      // the hollows: above and below the collarbone, the pit of the throat
      c.fillStyle = "rgba(40, 22, 14, 0.35)";
      c.beginPath();
      c.ellipse(hx - 4.5 * s, hy + 4.4 * s, 2.4 * s, 0.9 * s, 0.2, 0, Math.PI * 2);
      c.ellipse(hx + 4.5 * s, hy + 4.4 * s, 2.4 * s, 0.9 * s, -0.2, 0, Math.PI * 2);
      c.ellipse(hx, hy + 2.6 * s, 0.9 * s, 1 * s, 0, 0, Math.PI * 2);
      c.fill();
      // the cords of the neck
      ink(c, 0.4, 0.5 * s);
      c.beginPath();
      c.moveTo(hx - 1.2 * s, hy - 1.6 * s);
      c.lineTo(hx - 0.6 * s, hy + 2 * s);
      c.moveTo(hx + 1.4 * s, hy - 1.6 * s);
      c.lineTo(hx + 0.8 * s, hy + 2 * s);
      c.stroke();
      // the belly sunk under the ribs
      c.fillStyle = "rgba(40, 22, 14, 0.28)";
      c.beginPath();
      c.ellipse(hx, hy + 24 * s, 6 * s, 3 * s, 0, 0, Math.PI * 2);
      c.fill();
    });
    atop(c, () => {
      ink(c, 0.5, 0.75 * s);
      for (let i = 0; i < 5; i++) {
        const ry = hy + 9 * s + i * 2.8 * s;
        c.beginPath();
        c.moveTo(hx - 8 * s, ry);
        c.quadraticCurveTo(hx - 3 * s, ry - 1.6 * s, hx - 0.5 * s, ry + 0.5 * s);
        c.moveTo(hx + 0.5 * s, ry + 0.5 * s);
        c.quadraticCurveTo(hx + 4 * s, ry - 1.4 * s, hx + 8 * s, ry);
        c.stroke();
      }
      ink(c, 0.55, 0.9 * s);
      c.beginPath();
      c.moveTo(hx - 7 * s, hy + 6.5 * s);
      c.lineTo(hx - 1 * s, hy + 8 * s);
      c.moveTo(hx + 1 * s, hy + 8 * s);
      c.lineTo(hx + 7 * s, hy + 6.5 * s);
      c.moveTo(hx, hy + 8 * s);
      c.lineTo(hx, sw);
      c.stroke();
      shine(c, 0.2, 1 * s);
      c.beginPath();
      for (let i = 0; i < 4; i++) {
        const ry = hy + 10.4 * s + i * 2.8 * s;
        c.moveTo(hx - 7.5 * s, ry);
        c.quadraticCurveTo(hx - 3 * s, ry - 1.4 * s, hx - 1 * s, ry + 0.4 * s);
      }
      c.stroke();
    });
    // the arm: bone and tendon, reaching up — or down for the water
    const sh = { x: hx - 7 * s, y: hy + 7 * s };
    const hand =
      bend > 0.05
        ? { x: hx - 13 * s, y: sw + 1 * s }
        : { x: -8 * s, y: by - y + 10 * s + (1 - reach) * 16 * s };
    const el = { x: (sh.x + hand.x) / 2 - 3 * s, y: (sh.y + hand.y) / 2 + 2 * s };
    limb(c, sh.x, sh.y, el.x, el.y, 2.6 * s, 1.7 * s, 0.3 * s, 0.4, skin(0.05));
    limb(c, el.x, el.y, hand.x, hand.y, 1.7 * s, 1.1 * s, 0.4 * s, 0.3, skin(0.1));
    c.fillStyle = skin(0.05);
    c.beginPath();
    c.ellipse(hand.x, hand.y, 1.7 * s, 1.3 * s, 0.6, 0, Math.PI * 2);
    c.fill();
    // the head: hollow cheeks, sunken eyes, cracked lips, matted hair and beard
    c.save();
    c.translate(hx - 1 * s, hy - 3 * s);
    c.rotate(bend * 0.55 - reach * 0.4);
    const r = 5 * s;
    c.fillStyle = "#1c1612";
    c.beginPath();
    c.moveTo(-0.5 * r, -1.1 * r);
    c.quadraticCurveTo(1.2 * r, -1.4 * r, 1.5 * r, 0.2 * r);
    c.quadraticCurveTo(1.6 * r, 1.6 * r, 1.1 * r, 2.4 * r);
    c.lineTo(0.5 * r, 0.9 * r);
    c.quadraticCurveTo(0.2 * r, -0.4 * r, -0.5 * r, -1.1 * r);
    c.fill();
    profile(c, 0, 0, r, skin(0), bend * 0.4);
    face(c, 0, 0, r, { pain: 0.7, open: bend * 0.4 });
    atop(c, () => {
      // the hollow of the cheek
      c.fillStyle = "rgba(40, 20, 12, 0.35)";
      c.beginPath();
      c.ellipse(-0.45 * r, 0.45 * r, 0.3 * r, 0.22 * r, 0.3, 0, Math.PI * 2);
      c.fill();
      // cracked lips
      ink(c, 0.6, 0.06 * r);
      c.beginPath();
      c.moveTo(-0.98 * r, 0.7 * r);
      c.lineTo(-0.9 * r, 0.76 * r);
      c.stroke();
    });
    c.fillStyle = "#231a15";
    c.beginPath();
    c.moveTo(-0.9 * r, 0.7 * r);
    c.quadraticCurveTo(-1 * r, 1.7 * r, -0.4 * r, 2 * r);
    c.quadraticCurveTo(0.2 * r, 1.6 * r, 0.5 * r, 0.7 * r);
    c.quadraticCurveTo(-0.1 * r, 1 * r, -0.6 * r, 0.75 * r);
    c.fill();
    strands(
      c,
      [
        [0, -1.1 * r, 1.2 * r, 1.4 * r],
        [0.5 * r, -1 * r, 1.4 * r, 2.2 * r],
        [-0.7 * r, 1 * r, -0.4 * r, 1.9 * r],
      ],
      "rgba(150, 130, 110, 0.3)",
      0.35 * s,
    );
    c.restore();
  });

  // the near half of the water over his chest, and the bank
  ctx.save();
  const water = ctx.createLinearGradient(0, surf - 8 * s, 0, surf + 9 * s);
  water.addColorStop(0, `rgba(48, 66, 78, ${(0.92 - L.dim * 0.2).toFixed(3)})`);
  water.addColorStop(1, "rgba(8, 12, 16, 0.97)");
  ctx.fillStyle = water;
  ctx.beginPath();
  ctx.ellipse(x, surf, W, 9 * s, 0, 0, Math.PI);
  ctx.fill();
  ctx.strokeStyle = rgba(L.color, 0.35 * L.strength + 0.08);
  ctx.lineWidth = 0.8 * s;
  ctx.beginPath();
  ctx.ellipse(x, surf, W, 9 * s, 0, Math.PI * 1.15, Math.PI * 1.85);
  ctx.stroke();
  if (bend > 0.1) {
    ctx.strokeStyle = `rgba(150, 170, 180, ${(0.4 * bend).toFixed(3)})`;
    ctx.beginPath();
    ctx.ellipse(x + hx - 10 * s, surf - 6 * s * bend, 10 * s, 2 * s, 0, 0, Math.PI * 2);
    ctx.stroke();
  }
  // ripples spreading from him, always, because he never stops trembling
  for (let i = 0; i < 3; i++) {
    const k = (((t * 0.35 + i / 3) % 1) + 1) % 1;
    ctx.strokeStyle = `rgba(170, 190, 200, ${(0.28 * (1 - k)).toFixed(3)})`;
    ctx.lineWidth = 0.6 * s;
    ctx.beginPath();
    ctx.ellipse(x + hx, surf + 1 * s, (8 + k * 26) * s, (1.5 + k * 4) * s, 0, 0, Math.PI);
    ctx.stroke();
  }
  rimStones(ctx, x, surf, W, s, L, false);
  // reeds at the edge, dry and broken
  ctx.strokeStyle = "#3b3320";
  ctx.lineWidth = 0.8 * s;
  ctx.beginPath();
  for (let i = 0; i < 9; i++) {
    const rx = x - W - 2 * s + i * 1.8 * s + (i > 4 ? W * 1.9 : 0);
    const hgt = (10 + hh(i, 21) * 12) * s;
    const lean = (hh(i, 22) - 0.5) * 6 * s + Math.sin(t * 1.3 + i) * 0.8 * s;
    ctx.moveTo(rx, surf + 4 * s);
    ctx.quadraticCurveTo(rx + lean * 0.3, surf - hgt * 0.5, rx + lean, surf + 4 * s - hgt);
  }
  ctx.stroke();
  ctx.restore();
}

/** the stones that ring the pool; the far ones behind him, the near ones in front */
function rimStones(
  ctx: CanvasRenderingContext2D,
  x: number,
  surf: number,
  W: number,
  s: number,
  L: FigLight,
  far: boolean,
) {
  const n = 16;
  for (let i = 0; i < n; i++) {
    const a = far ? Math.PI + (i / (n - 1)) * Math.PI : (i / (n - 1)) * Math.PI;
    const sx = x + Math.cos(a) * (W + 2 * s);
    const sy = surf + Math.sin(a) * (9 * s + 2 * s) + (far ? 0 : 2 * s);
    const rw = (3.6 + hh(i, far ? 31 : 41) * 3.4) * s * (far ? 0.85 : 1.15);
    const rh = rw * (0.55 + hh(i, 51) * 0.2);
    const g = ctx.createLinearGradient(sx, sy - rh, sx, sy + rh);
    const lit = 0.4 + 0.6 * L.strength * (1 - L.dim);
    g.addColorStop(
      0,
      `rgb(${(70 + 50 * lit) | 0}, ${(62 + 40 * lit) | 0}, ${(54 + 30 * lit) | 0})`,
    );
    g.addColorStop(1, "#1d1714");
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.ellipse(sx, sy, rw, rh, (hh(i, 61) - 0.5) * 0.6, 0, Math.PI * 2);
    ctx.fill();
    // wet dark line where the stone meets the water
    ctx.strokeStyle = "rgba(6, 8, 10, 0.5)";
    ctx.lineWidth = 0.6 * s;
    ctx.beginPath();
    ctx.ellipse(
      sx,
      sy,
      rw,
      rh,
      0,
      far ? 0.2 : Math.PI + 0.2,
      far ? Math.PI - 0.2 : Math.PI * 2 - 0.2,
    );
    ctx.stroke();
  }
}

/** one fig leaf: palmate, five rounded lobes, deep sinuses, veins */
function figLeaf(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  r: number,
  a: number,
  col: string,
  vein: string,
) {
  ctx.save();
  ctx.translate(x, y);
  ctx.rotate(a);
  ctx.fillStyle = col;
  ctx.beginPath();
  const N = 40;
  for (let i = 0; i <= N; i++) {
    const th = -Math.PI * 0.95 + (i / N) * Math.PI * 1.9;
    // five lobes around the leaf, the middle one longest
    const lobe = Math.pow(Math.abs(Math.cos(th * 2.5)), 0.6);
    const mid = 1 - (Math.abs(th) / Math.PI) * 0.35;
    const rr = r * (0.35 + 0.65 * lobe) * mid;
    const px = Math.cos(th) * rr + r * 0.25;
    const py = Math.sin(th) * rr * 0.9;
    if (i) ctx.lineTo(px, py);
    else ctx.moveTo(px, py);
  }
  ctx.closePath();
  ctx.fill();
  ctx.strokeStyle = vein;
  ctx.lineWidth = Math.max(0.3, r * 0.06);
  ctx.beginPath();
  for (const th of [-1.26, -0.63, 0, 0.63, 1.26]) {
    ctx.moveTo(r * 0.1, 0);
    ctx.lineTo(r * 0.25 + Math.cos(th) * r * 0.75, Math.sin(th) * r * 0.68);
  }
  ctx.stroke();
  ctx.restore();
}

/**
 * The fig over the pool: a gnarled, leaning trunk with its bark, roots
 * gripping the bank, a crown of broad lobed leaves lit from the sun's side,
 * and one long branch bowed down over him with the fruit — the branch that
 * lifts every time he reaches. `by` is where that branch's fruit hangs.
 */
function figTree(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  s: number,
  L: FigLight,
  by: number,
) {
  const t = L.t;
  ctx.save();
  ctx.lineCap = "round";
  ctx.lineJoin = "round";
  const bark = "#2e231d";
  // roots over the bank
  ctx.strokeStyle = bark;
  for (const [dx, len, w] of [
    [-6, 14, 2.4],
    [4, 12, 2],
    [10, 10, 1.6],
    [-14, 10, 1.4],
  ] as const) {
    ctx.lineWidth = w * s;
    ctx.beginPath();
    ctx.moveTo(x + 44 * s, y - 2 * s);
    ctx.quadraticCurveTo(
      x + (44 + dx * 0.5) * s,
      y + 1 * s,
      x + (44 + dx + (dx < 0 ? -len : len) * 0.4) * s,
      y + 4 * s,
    );
    ctx.stroke();
  }
  // the trunk: a tapering, twisting form, filled, not a stroke
  const tr: Array<[number, number, number]> = [
    [44, 2, 6.5],
    [47, -14, 5.6],
    [44, -30, 5],
    [37, -44, 4.4],
    [31, -56, 3.8],
    [24, -68, 3.2],
    [18, -80, 2.4],
  ];
  const left: Array<[number, number]> = [];
  const right: Array<[number, number]> = [];
  for (let i = 0; i < tr.length; i++) {
    const [px, py, w] = tr[i]!;
    const [qx, qy] = tr[Math.min(tr.length - 1, i + 1)]!;
    const [ox, oy] = tr[Math.max(0, i - 1)]!;
    let nx = -(qy - oy);
    let ny = qx - ox;
    const m = Math.hypot(nx, ny) || 1;
    nx /= m;
    ny /= m;
    left.push([x + (px + nx * w) * s, y + (py + ny * w) * s]);
    right.push([x + (px - nx * w) * s, y + (py - ny * w) * s]);
  }
  const tg = ctx.createLinearGradient(x + 36 * s, 0, x + 52 * s, 0);
  const litL = L.lx < 0;
  const lc = `rgb(${(78 + 40 * L.strength) | 0}, ${(62 + 28 * L.strength) | 0}, ${(50 + 16 * L.strength) | 0})`;
  tg.addColorStop(0, litL ? lc : "#1d1612");
  tg.addColorStop(1, litL ? "#1d1612" : lc);
  ctx.fillStyle = tg;
  ctx.beginPath();
  left.forEach(([px, py], i) => (i ? ctx.lineTo(px, py) : ctx.moveTo(px, py)));
  for (let i = right.length - 1; i >= 0; i--) ctx.lineTo(right[i]![0], right[i]![1]);
  ctx.closePath();
  ctx.fill();
  // bark: long twisting fissures and a knot
  ctx.strokeStyle = "rgba(10, 6, 4, 0.55)";
  ctx.lineWidth = 0.6 * s;
  ctx.beginPath();
  for (let k = -2; k <= 2; k++) {
    tr.forEach(([px, py, w], i) => {
      const xx = x + (px + (k / 2.5) * w * 0.8 + Math.sin(i * 1.7 + k) * 0.6) * s;
      const yy = y + py * s;
      if (i) ctx.lineTo(xx, yy);
      else ctx.moveTo(xx, yy);
    });
  }
  ctx.stroke();
  ctx.fillStyle = "rgba(10, 6, 4, 0.6)";
  ctx.beginPath();
  ctx.ellipse(x + 44 * s, y - 26 * s, 1.6 * s, 2.4 * s, 0.3, 0, Math.PI * 2);
  ctx.fill();
  // the branches
  const branch = (pts: Array<[number, number]>, w0: number, w1: number) => {
    for (let i = 0; i < pts.length - 1; i++) {
      ctx.strokeStyle = bark;
      ctx.lineWidth = (w0 + (w1 - w0) * (i / (pts.length - 1))) * s;
      ctx.beginPath();
      ctx.moveTo(pts[i]![0], pts[i]![1]);
      ctx.lineTo(pts[i + 1]![0], pts[i + 1]![1]);
      ctx.stroke();
    }
  };
  const sway = Math.sin(t * 0.9) * 1.2 * s;
  branch(
    [
      [x + 37 * s, y - 44 * s],
      [x + 50 * s, y - 58 * s],
      [x + 58 * s + sway, y - 72 * s],
      [x + 62 * s + sway, y - 84 * s],
    ],
    3,
    1.2,
  );
  branch(
    [
      [x + 24 * s, y - 68 * s],
      [x + 30 * s, y - 84 * s],
      [x + 38 * s + sway, y - 94 * s],
    ],
    2.4,
    1,
  );
  // the one that bows over him, lifted when he reaches
  const tipX = x - 12 * s;
  branch(
    [
      [x + 22 * s, y - 72 * s],
      [x + 12 * s, (y - 78 * s + by) / 2 - 6 * s],
      [x + 0 * s, by - 6 * s],
      [tipX, by],
    ],
    2.4,
    0.9,
  );
  // the crown: leaf clusters, shaded at the back, lit on the sun's side
  const greens = ["#1b2a17", "#24381d", "#2f4a24", "#3d5c2c"];
  const clusters: Array<[number, number, number]> = [
    [62, -88, 16],
    [40, -98, 14],
    [22, -90, 12],
    [52, -72, 11],
    [8, (by - y) / s - 10, 10],
    [-10, (by - y) / s - 6, 8],
  ];
  for (let pass = 0; pass < 2; pass++) {
    clusters.forEach(([cx, cy, n], ci) => {
      for (let i = 0; i < n; i++) {
        const a = hh(ci, i) * Math.PI * 2;
        const d = Math.sqrt(hh(ci + 7, i)) * (6 + n * 0.5) * s;
        const lx = x + cx * s + Math.cos(a) * d * 1.2 + (cy < -80 ? sway : 0);
        const ly = y + cy * s + Math.sin(a) * d * 0.7;
        // the back pass is darker; leaves facing the light are brighter
        const facing = -(Math.cos(a) * L.lx + Math.sin(a) * L.ly);
        const tone =
          pass === 0 ? 0 : Math.max(1, Math.min(3, Math.round(1.5 + facing * 1.5 * L.strength)));
        const r = (4.2 + hh(ci + 3, i) * 2.4) * s * (pass ? 1 : 1.15);
        figLeaf(
          ctx,
          lx,
          ly,
          r,
          a + hh(ci, i + 9) - 0.5 + Math.sin(t * 1.1 + i) * 0.05,
          greens[tone]!,
          "rgba(170, 200, 120, 0.22)",
        );
      }
    });
  }
  // the figs, ripe to splitting, hung under the low branch
  for (const [fx, fy] of [
    [-10, 4],
    [-4, 3],
    [-7, 9],
    [-14, 8],
    [-1, 8],
  ] as const) {
    const cx = x + fx * s;
    const cy = by + fy * s;
    const g = ctx.createRadialGradient(cx - 1.2 * s, cy - 1.2 * s, 0, cx, cy, 3.4 * s);
    g.addColorStop(0, "#c46a8c");
    g.addColorStop(0.55, "#7a2648");
    g.addColorStop(1, "#38102a");
    ctx.fillStyle = g;
    ctx.beginPath();
    // pear-shaped, narrowing to the stalk
    ctx.moveTo(cx, cy - 3.6 * s);
    ctx.bezierCurveTo(cx + 2.8 * s, cy - 2 * s, cx + 3.4 * s, cy + 2.6 * s, cx, cy + 3 * s);
    ctx.bezierCurveTo(cx - 3.4 * s, cy + 2.6 * s, cx - 2.8 * s, cy - 2 * s, cx, cy - 3.6 * s);
    ctx.fill();
    ctx.fillStyle = "rgba(255, 220, 230, 0.35)";
    ctx.beginPath();
    ctx.ellipse(cx - 1 * s, cy - 0.6 * s, 0.7 * s, 1.1 * s, -0.4, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.restore();
}
