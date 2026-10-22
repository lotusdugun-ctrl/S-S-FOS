/**
 * The great mountain in the distance, painted.
 *
 * It used to be one flat silhouette. Now it is a landform: a craggy crest
 * line, a height field of ridges and gullies worked out from noise, lit by a
 * sun to its left and again by a sun to its right (the two are cross-faded
 * as the day moves), with forest on its lower slopes, scrub above, bare rock
 * in bands of strata, a snow cap that runs down into the gullies, and a
 * river that comes out of the snow and finds its way down the valleys.
 *
 * All of that is far too much to work out every frame, so it is painted once
 * per hill into two offscreen pictures — a few rows a frame, so building it
 * never stalls the game — and then only drawn.
 */
import type { RGB } from "./sky";

export type MountainSpec = {
  x: number;
  height: number;
  width: number;
  snow: boolean;
  /** the hill's id: seeds the noise */
  seed: number;
  /** rock colour for this hill */
  rock: RGB;
};

function hash(x: number, y: number, s: number) {
  let h = (x * 374761393 + y * 668265263 + s * 2246822519) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h ^= h >>> 16;
  return (h >>> 0) / 4294967295;
}

function vnoise(x: number, y: number, s: number) {
  const xi = Math.floor(x);
  const yi = Math.floor(y);
  const xf = x - xi;
  const yf = y - yi;
  const u = xf * xf * (3 - 2 * xf);
  const v = yf * yf * (3 - 2 * yf);
  const a = hash(xi, yi, s);
  const b = hash(xi + 1, yi, s);
  const c = hash(xi, yi + 1, s);
  const d = hash(xi + 1, yi + 1, s);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}

function fbm(x: number, y: number, s: number, oct: number) {
  let sum = 0;
  let amp = 0.5;
  let f = 1;
  for (let i = 0; i < oct; i++) {
    sum += vnoise(x * f, y * f, s + i * 17) * amp;
    f *= 2.03;
    amp *= 0.5;
  }
  return sum;
}

/** ridged noise: sharp crests, rounded valleys — what eroded rock looks like */
function ridged(x: number, y: number, s: number, oct: number) {
  let sum = 0;
  let amp = 0.55;
  let f = 1;
  let w = 1;
  for (let i = 0; i < oct; i++) {
    let n = 1 - Math.abs(vnoise(x * f, y * f, s + i * 31) * 2 - 1);
    n *= n;
    n *= w;
    w = Math.min(1, n * 1.6);
    sum += n * amp;
    f *= 2.1;
    amp *= 0.5;
  }
  return sum;
}

export class MountainPainter {
  readonly spec: MountainSpec;
  /** world units per sprite pixel, horizontally and vertically */
  readonly kx: number;
  readonly ky: number;
  /** the sprite's world extent */
  readonly x0: number;
  readonly top: number;
  readonly W: number;
  readonly H: number;
  /** crest height (world y) at each sprite column */
  readonly crest: Float32Array;
  /** the crest smoothed wide, for the mass's broad form (the jagged one would streak it) */
  private crestSoft: Float32Array;
  /** the river, in world coordinates */
  river: Array<[number, number]> = [];
  /** lit from the left, lit from the right */
  readonly a: HTMLCanvasElement;
  readonly b: HTMLCanvasElement;
  ready = false;
  private row = 0;
  private hf: Float32Array;
  private imgA: ImageData | null = null;
  private imgB: ImageData | null = null;
  private snowLine: number;

  /**
   * `pxPerUnit` is how many sprite pixels one world unit across gets; the
   * mountain is drawn squashed to half height on screen, so vertically it
   * gets half as many.
   */
  constructor(spec: MountainSpec, pxPerUnit: number) {
    this.spec = spec;
    const { x, height, width } = spec;
    this.kx = 1 / pxPerUnit;
    this.ky = 2 / pxPerUnit;
    this.x0 = x - width / 2;
    this.top = height * 1.22;
    this.W = Math.max(8, Math.ceil(width / this.kx));
    // down to well under the base, which the hills in front hide anyway
    this.H = Math.max(8, Math.ceil((this.top + height * 0.25) / this.ky));
    this.snowLine = spec.snow ? 0.64 : 0.8;
    this.a = document.createElement("canvas");
    this.b = document.createElement("canvas");
    this.a.width = this.b.width = this.W;
    this.a.height = this.b.height = this.H;
    this.hf = new Float32Array(this.W * this.H);
    this.crest = new Float32Array(this.W);
    this.crestSoft = new Float32Array(this.W);
    this.buildCrest();
    const R = Math.max(2, Math.round(this.W * 0.02));
    for (let i = 0; i < this.W; i++) {
      let sum = 0;
      let n = 0;
      for (let j = Math.max(0, i - R); j <= Math.min(this.W - 1, i + R); j++, n++)
        sum += this.crest[j]!;
      this.crestSoft[i] = sum / n;
    }
  }

  /** the skyline: a main summit, a shoulder, crags along the way */
  private buildCrest() {
    const { x, height, width, seed } = this.spec;
    const baseY = height * 0.12;
    const p1x = x - width * 0.16;
    const p2x = x + width * 0.24;
    for (let i = 0; i < this.W; i++) {
      const wx = this.x0 + i * this.kx;
      // two cones, their union, with concave flanks
      const c1 = 1 - Math.abs(wx - p1x) / (width * (wx < p1x ? 0.36 : 0.46));
      const c2 = 0.68 * (1 - Math.abs(wx - p2x) / (width * 0.3));
      const c3 = 0.42 * (1 - Math.abs(wx - (x + width * 0.4)) / (width * 0.16));
      let k = Math.max(0, c1, c2, c3);
      // concave flanks: steep near the top, easing out toward the foot
      k = Math.pow(k, 1.55);
      // crags: large slow bumps and small sharp ones, more of them high up
      const big = (fbm(wx / (width * 0.09), 0.5, seed, 4) - 0.5) * 0.22;
      const sharp = (ridged(wx / (width * 0.02), 1.5, seed + 3, 4) - 0.3) * 0.06;
      const edge = Math.min(1, Math.min(wx - this.x0, this.x0 + width - wx) / (width * 0.08));
      this.crest[i] = baseY * edge + (k + (big + sharp) * (0.3 + k)) * height * edge;
    }
  }

  crestAt(wx: number) {
    const i = Math.max(0, Math.min(this.W - 1, Math.round((wx - this.x0) / this.kx)));
    return this.crest[i]!;
  }

  /** paint some more rows; returns true once the whole thing is done */
  step(budgetMs: number): boolean {
    if (this.ready) return true;
    const t0 = performance.now();
    const { W, H } = this;
    // pass 1: the height field (rows 0..H-1), then pass 2: shading (H..2H-1)
    while (this.row < H * 2 && performance.now() - t0 < budgetMs) {
      for (let k = 0; k < 1 && this.row < H * 2; k++, this.row++) {
        if (this.row < H) this.heightRow(this.row);
        else this.shadeRow(this.row - H);
      }
    }
    // the finishing, one heavy step a frame
    if (this.row >= H * 2 && performance.now() - t0 < budgetMs) {
      const stage = this.row - H * 2;
      if (stage === 0 || stage === 1) {
        const cv = stage === 0 ? this.a : this.b;
        const img = stage === 0 ? this.imgA! : this.imgB!;
        const c = cv.getContext("2d");
        if (c) {
          // soften the single-pixel speckle the noise leaves; distance blurs anyway
          const tmp = document.createElement("canvas");
          tmp.width = cv.width;
          tmp.height = cv.height;
          tmp.getContext("2d")?.putImageData(img, 0, 0);
          c.filter = "blur(0.7px)";
          c.drawImage(tmp, 0, 0);
          c.filter = "none";
        }
      } else {
        this.traceRiver();
        this.paintRiver(this.a);
        this.paintRiver(this.b);
        this.ready = true;
        this.hf = new Float32Array(0);
        this.imgA = this.imgB = null;
      }
      this.row++;
    }
    return this.ready;
  }

  /** depth toward the viewer at a world point: ridges stand out, gullies sink */
  private heightRow(py: number) {
    const { W, spec } = this;
    const { width, height, seed } = spec;
    const wy = this.top - py * this.ky;
    const spine = spec.x - width * 0.16;
    for (let px = 0; px < W; px++) {
      const wx = this.x0 + px * this.kx;
      const d = this.crest[px]! - wy;
      if (d < -2) {
        this.hf[py * W + px] = 0;
        continue;
      }
      // the whole mass bulges toward you along its spine, and turns away at its edges
      const ds = Math.max(0, this.crestSoft[px]! - wy);
      const macro = -Math.abs(wx - spine) * 0.35 + Math.sqrt(Math.min(ds, height * 0.2)) * 9;
      // gullies run down the fall line: noise stretched tall
      // the fall lines wander: warp the noise so gullies bend and join
      let gx = wx / (width * 0.04);
      const gy = wy / (height * 0.13);
      gx += (fbm(gx * 0.35, gy * 0.35, seed + 41, 3) - 0.5) * 3.2;
      const ribs = ridged(gx, gy, seed + 11, 4) * height * 0.075;
      const detail =
        (fbm(wx / (width * 0.012), wy / (height * 0.03), seed + 29, 3) - 0.5) * height * 0.014;
      this.hf[py * W + px] = macro + ribs + detail;
    }
  }

  private shadeRow(py: number) {
    const { W, H, spec } = this;
    if (!this.imgA) {
      this.imgA = new ImageData(W, H);
      this.imgB = new ImageData(W, H);
    }
    const A = this.imgA.data;
    const B = this.imgB!.data;
    const { height, seed, rock } = spec;
    const wy = this.top - py * this.ky;
    const alt = wy / height;
    const hf = this.hf;
    // the lights: from the upper left and the upper right, toward the viewer
    const L1 = [-0.62, 0.5, 0.6];
    const L2 = [0.62, 0.5, 0.6];
    for (let px = 0; px < W; px++) {
      const o = (py * W + px) * 4;
      const crestH = this.crest[px]!;
      const d = crestH - wy;
      if (d < 0) {
        A[o + 3] = B[o + 3] = 0;
        continue;
      }
      const i = py * W + px;
      const hl = hf[px > 0 ? i - 1 : i]!;
      const hr = hf[px < W - 1 ? i + 1 : i]!;
      const hu = hf[py > 0 ? i - W : i]!;
      const hd = hf[py < H - 1 ? i + W : i]!;
      // normal: screen x right, y up, z toward the viewer
      let nx = -(hr - hl) / (2 * this.kx);
      let ny = -(hu - hd) / (2 * this.ky);
      let nz = 1;
      const nm = Math.hypot(nx, ny, nz);
      nx /= nm;
      ny /= nm;
      nz /= nm;
      const wx = this.x0 + px * this.kx;
      // concavity: gullies hold shadow and snow
      const lap = (hl + hr + hu + hd - 4 * hf[i]!) / (this.kx * 4);
      const gully = Math.max(-1, Math.min(1, lap * 0.6));
      const n1 = fbm(wx * 0.006, wy * 0.012, seed + 5, 4);
      const n2 = vnoise(wx * 0.12, wy * 0.2, seed + 9);
      // ---- the ground: forest low, scrub above, rock in strata, snow on top
      let r: number;
      let g: number;
      let b: number;
      const strata = Math.sin(wy * 0.045 + n1 * 5) * 0.08 + (n2 - 0.5) * 0.12;
      r = rock[0] * (1 + strata);
      g = rock[1] * (1 + strata);
      b = rock[2] * (1 + strata);
      // the trees climb the gullies, where there is water, and fall back off the ribs
      const forest = Math.max(
        0,
        Math.min(1, (0.42 - alt + (n1 - 0.5) * 0.6 + gully * 0.14) / 0.05),
      );
      const scrub =
        Math.max(0, Math.min(1, (0.6 - alt + (n1 - 0.5) * 0.6 + gully * 0.1) / 0.08)) *
        (1 - forest);
      if (scrub > 0) {
        const k = scrub * (0.55 + n2 * 0.35);
        r += (96 - r) * k;
        g += (98 - g) * k;
        b += (62 - b) * k;
      }
      if (forest > 0) {
        // trees as a stipple of dark crowns over lighter ground
        const tree = 0.6 + 0.4 * Math.max(0, Math.min(1, (n2 - 0.3) / 0.4));
        const k = forest * tree;
        r += (34 - r) * k;
        g += (54 - g) * k;
        b += (34 - b) * k;
      }
      // snow: down the gullies, off the steepest faces
      const steep = 1 - nz;
      const sl = this.snowLine - gully * 0.14 - (n1 - 0.5) * 0.3 + steep * 0.16;
      const snow = Math.max(0, Math.min(1, (alt - sl) / 0.07));
      if (snow > 0) {
        const st2 = Math.max(0, Math.min(1, (steep - 0.4) / 0.3));
        const k = snow * (1 - st2 * st2 * (3 - 2 * st2) * 0.5);
        r += (238 - r) * k;
        g += (242 - g) * k;
        b += (248 - b) * k;
      }
      // ambient occlusion in the gullies, and at the foot of the mass
      const ao =
        Math.max(0.55, Math.min(1, 1 + gully * 0.35)) * (0.82 + 0.18 * Math.min(1, alt * 2 + 0.2));
      // ---- light: blue sky from above, the sun from one side or the other
      const sky = (0.56 + 0.2 * ny) * ao;
      const d1 = Math.max(0, nx * L1[0]! + ny * L1[1]! + nz * L1[2]!);
      const d2 = Math.max(0, nx * L2[0]! + ny * L2[1]! + nz * L2[2]!);
      const put = (D: Uint8ClampedArray, dif: number) => {
        const sun = dif * 0.78 * (0.75 + 0.25 * ao);
        D[o] = Math.min(255, r * (sky * 0.82 + sun * 1.08));
        D[o + 1] = Math.min(255, g * (sky * 0.9 + sun * 1.0));
        D[o + 2] = Math.min(255, b * (sky * 1.12 + sun * 0.86));
        // a soft edge against the sky
        D[o + 3] = Math.min(255, (d / (this.ky * 1.5)) * 255);
      };
      put(A, d1);
      put(B, d2);
    }
  }

  /** the river: out of the snow, down the bottoms of the gullies */
  private traceRiver() {
    const { spec, W, H } = this;
    const { width, height, seed } = spec;
    const startX = spec.x - width * 0.16 + width * (0.1 + hash(seed, 3, 7) * 0.06);
    let wx = startX;
    let wy = Math.min(this.crestAt(wx) - height * 0.2, height * (this.snowLine + 0.02));
    const pts: Array<[number, number]> = [[wx, wy]];
    const step = height * 0.012;
    let guard = 0;
    while (wy > height * 0.02 && guard++ < 400) {
      wy -= step;
      // find the lowest ground (deepest into the mountain) a little to either side
      let best = wx;
      let bestH = Infinity;
      for (let k = -4; k <= 4; k++) {
        const cx = wx + k * width * 0.004;
        const px = Math.round((cx - this.x0) / this.kx);
        const py = Math.round((this.top - wy) / this.ky);
        if (px < 1 || px >= W - 1 || py < 0 || py >= H) continue;
        const hgt = this.hf[py * W + px]! + Math.abs(k) * 3;
        if (hgt < bestH) {
          bestH = hgt;
          best = cx;
        }
      }
      const mv = Math.max(-width * 0.004, Math.min(width * 0.004, (best - wx) * 0.5));
      wx += mv + (fbm(wy * 0.012, 3.3, seed + 77, 3) - 0.5) * width * 0.012;
      if (wy > this.crestAt(wx) - 4) break;
      pts.push([wx, wy]);
    }
    // round off the corners: two passes of corner-cutting
    let q = pts;
    for (let it = 0; it < 3; it++) {
      const nq: Array<[number, number]> = [q[0]!];
      for (let i = 0; i < q.length - 1; i++) {
        const [ax, ay] = q[i]!;
        const [bx, by] = q[i + 1]!;
        nq.push(
          [ax * 0.75 + bx * 0.25, ay * 0.75 + by * 0.25],
          [ax * 0.25 + bx * 0.75, ay * 0.25 + by * 0.75],
        );
      }
      nq.push(q[q.length - 1]!);
      q = nq;
    }
    this.river = q;
  }

  private paintRiver(cv: HTMLCanvasElement) {
    const c = cv.getContext("2d");
    if (!c || this.river.length < 2) return;
    const P = (q: [number, number]) =>
      [(q[0] - this.x0) / this.kx, (this.top - q[1]) / this.ky] as const;
    c.lineCap = "round";
    c.lineJoin = "round";
    // the cut it runs in, dark, then the water, widening as it goes down
    const n = this.river.length;
    for (let pass = 0; pass < 2; pass++) {
      for (let i = 1; i < n; i++) {
        const k = i / n;
        c.strokeStyle = pass
          ? `rgba(196, 214, 228, ${(0.55 + k * 0.35).toFixed(3)})`
          : "rgba(24, 28, 26, 0.45)";
        c.lineWidth = (pass ? 1.6 + k * 2.6 : 3.6 + k * 4) / this.kx;
        c.beginPath();
        c.moveTo(...P(this.river[i - 1]!));
        c.lineTo(...P(this.river[i]!));
        c.stroke();
      }
    }
  }
}
