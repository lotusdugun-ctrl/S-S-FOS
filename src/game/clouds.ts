/**
 * Clouds as volumes rather than as glows.
 *
 * Each cloud is built once as a small density field: a heap of soft blobs
 * (a flat-based cumulus near the camera, long thin sheets further off) eaten
 * away at the edges by fractal noise, which is what gives real cloud its
 * ragged, cauliflower border. Light is then marched through that field twice,
 * from above and from below, so the shading comes from the shape itself: a
 * bright top and a grey base by day, a shadowed crown over a burning underside
 * at golden hour. The field is kept, and only the colouring is redone when the
 * hour moves on, a few clouds per frame.
 */
import type { RGB } from "./sky";

export type CloudKind = "cumulus" | "strato";

export type CloudShape = {
  w: number;
  h: number;
  /** 0-1 opacity of the volume */
  dens: Float32Array;
  /** light reaching each point from above, and from below */
  top: Float32Array;
  bot: Float32Array;
  /** the tinted picture, and the light it was tinted for */
  canvas: HTMLCanvasElement;
  key: string;
};

function hash2(x: number, y: number, s: number): number {
  let h = (x * 374761393 + y * 668265263 + s * 2246822519) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h ^= h >>> 16;
  return (h >>> 0) / 4294967295;
}

function vnoise(x: number, y: number, s: number): number {
  const xi = Math.floor(x);
  const yi = Math.floor(y);
  const xf = x - xi;
  const yf = y - yi;
  const u = xf * xf * (3 - 2 * xf);
  const v = yf * yf * (3 - 2 * yf);
  const a = hash2(xi, yi, s);
  const b = hash2(xi + 1, yi, s);
  const c = hash2(xi, yi + 1, s);
  const d = hash2(xi + 1, yi + 1, s);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}

function fbm(x: number, y: number, s: number, oct: number): number {
  let sum = 0;
  let amp = 0.5;
  let norm = 0;
  for (let i = 0; i < oct; i++) {
    sum += vnoise(x, y, s + i * 17) * amp;
    norm += amp;
    amp *= 0.5;
    x *= 2.03;
    y *= 2.03;
  }
  return sum / norm;
}

const smooth = (a: number, b: number, x: number) => {
  const t = Math.max(0, Math.min(1, (x - a) / (b - a)));
  return t * t * (3 - 2 * t);
};

/** a seeded 0-1 sequence */
function rng(seed: number) {
  let s = (seed * 9301 + 49297) % 233280;
  return () => {
    s = (s * 9301 + 49297) % 233280;
    return s / 233280;
  };
}

/** build one cloud's volume, `w` pixels wide */
export function buildCloud(seed: number, kind: CloudKind, w: number): CloudShape {
  const r = rng(seed * 7 + 3);
  const aspect = kind === "cumulus" ? 0.56 : 0.3;
  const h = Math.max(8, Math.round(w * aspect));
  w = Math.max(16, Math.round(w));

  // the blobs, measured in widths: X runs 0-1 across, Y 0-aspect down
  type Blob = { x: number; y: number; rx: number; ry: number; k: number };
  const blobs: Blob[] = [];
  const baseY = aspect * 0.86;
  if (kind === "cumulus") {
    // a broad foot along the flat base, then towers heaped up from the middle
    const foot = 4 + Math.floor(r() * 3);
    for (let i = 0; i < foot; i++) {
      const x = 0.24 + (i / (foot - 1)) * 0.52 + (r() - 0.5) * 0.05;
      const rad = 0.08 + r() * 0.05;
      blobs.push({ x, y: baseY - rad * 0.45, rx: rad * 1.2, ry: rad, k: 1 });
    }
    const towers = 3 + Math.floor(r() * 4);
    for (let i = 0; i < towers; i++) {
      const x = 0.28 + r() * 0.44;
      const centre = 1 - Math.abs(x - 0.5) * 2;
      const rad = 0.08 + r() * 0.06 + centre * 0.04;
      let y = baseY - 0.1 - centre * (0.06 + r() * 0.14) - r() * 0.04;
      y = Math.max(y, rad + 0.05);
      blobs.push({ x, y, rx: rad, ry: rad, k: 0.9 + r() * 0.15 });
    }
    // cauliflower: small domes heaped over the upper surface of every puff
    const big = blobs.slice();
    const bubbles = 22 + Math.floor(r() * 10);
    for (let i = 0; i < bubbles; i++) {
      const pb = big[Math.floor(r() * big.length)]!;
      const ang = -Math.PI * (0.12 + r() * 0.76);
      const rad = pb.rx * (0.22 + r() * 0.22);
      const x = pb.x + Math.cos(ang) * pb.rx * 0.95;
      const y = Math.max(rad + 0.03, pb.y + Math.sin(ang) * pb.ry * 0.95);
      blobs.push({ x, y, rx: rad, ry: rad, k: 0.75 });
    }
  } else {
    // long sheets of altocumulus: many flat, overlapping lenses
    const n = 8 + Math.floor(r() * 6);
    for (let i = 0; i < n; i++) {
      const x = 0.12 + r() * 0.76;
      const rx = 0.05 + r() * 0.09;
      const y = aspect * (0.35 + r() * 0.3);
      blobs.push({ x, y, rx, ry: rx * (0.35 + r() * 0.25), k: 0.6 + r() * 0.4 });
    }
  }

  const n = w * h;
  const dens = new Float32Array(n);
  /** the unclamped field: read as a height map, its slopes are the billows' faces */
  const hgt = new Float32Array(n);
  const freq = kind === "cumulus" ? 9 : 12;
  const ns = seed * 13 + 1;
  const crisp = kind === "cumulus" ? 0.2 : 0.3;
  for (let j = 0; j < h; j++) {
    const v = j / h;
    const Y = v * aspect;
    for (let i = 0; i < w; i++) {
      const u = i / w;
      let f = 0;
      // the surface: the union of the blobs as spheres, so every bubble is a
      // dome with a crease where it meets the next
      let dome = 0;
      const sk = 0.012;
      for (const b of blobs) {
        const dx = (u - b.x) / b.rx;
        const dy = (Y - b.y) / b.ry;
        const d2 = dx * dx + dy * dy;
        if (d2 < 9) f += b.k * Math.exp(-d2 * 1.4);
        // a soft union: the creases between bubbles are rounded, not cut
        if (d2 < 1.2) dome += Math.exp((b.rx * Math.sqrt(Math.max(0, 1 - d2))) / sk);
      }
      if (f < 0.05) continue;
      // ragged edges: the noise eats the thin outer parts, leaves the core whole
      // billows: noise folded on itself (1-|2n-1|) reads as heaped cauliflower
      const wx = fbm(u * 3 + 7.1, Y * 3, ns + 9, 3) - 0.5;
      const nz = fbm(u * freq + wx * 1.5, Y * freq + wx, ns, 6);
      const bill = 1 - Math.abs(nz * 2 - 1);
      let d = Math.min(f, 1.3) - 0.5 + (nz - 0.5) * 1.3 + (bill - 0.5) * 0.45;
      if (kind === "cumulus") {
        // the condensation level: a cumulus sits on a flat, slightly ragged base
        d *= 1 - smooth(baseY - 0.015, baseY + 0.02 + nz * 0.03, Y);
      } else {
        // sheets break up into rows of little cells
        const cells = fbm(u * 34, Y * 60, ns + 5, 3);
        d *= 0.4 + cells * 1.1;
      }
      // never touch the sprite's edge
      d *= smooth(0, 0.06, u) * smooth(1, 0.94, u) * smooth(0, 0.04, Y);
      dens[j * w + i] = smooth(0, crisp, d);
      // the domes of the blobs, gently roughened: fine noise here reads as rock
      hgt[j * w + i] = (dome > 0 ? sk * Math.log(dome) : 0) + (nz - 0.5) * 0.01;
    }
  }

  // light marched through the volume, up toward the sky and down toward the sun
  const top = new Float32Array(n);
  const bot = new Float32Array(n);
  // Light through the volume, from above and from below: a running,
  // exponentially fading sum of the density a ray has crossed, at two scales —
  // a long one for the body's own shadow, a short one for each billow shading
  // the next, which is what gives the surface its relief. Running sums rather
  // than a few spaced samples, which left the shading in bands.
  const longK = Math.exp(-1 / Math.max(2, h * 0.18));
  const shortK = Math.exp(-1 / Math.max(1.5, w / 120));
  const kL = (kind === "cumulus" ? 1.1 : 0.8) / Math.max(4, h * 0.18);
  const kS = 0.7 / Math.max(1.5, w / 120);
  for (let i = 0; i < w; i++) {
    let aL = 0;
    let aS = 0;
    for (let j = 0; j < h; j++) {
      const o = j * w + i;
      top[o] = Math.exp(-aL * kL - aS * kS);
      const d = dens[o]!;
      aL = aL * longK + d;
      aS = aS * shortK + d;
    }
    aL = 0;
    aS = 0;
    for (let j = h - 1; j >= 0; j--) {
      const o = j * w + i;
      bot[o] = Math.exp(-aL * kL * 0.75 - aS * kS * 0.6);
      aL = aL * longK + dens[o]!;
      aS = aS * shortK + dens[o]!;
    }
  }

  // soften the light across, so neighbouring columns don't stripe
  const tmp = new Float32Array(w);
  for (const L of [top, bot]) {
    for (let j = 0; j < h; j++) {
      const row = j * w;
      for (let i = 0; i < w; i++) {
        let sum = 0;
        let cnt = 0;
        for (let k = -3; k <= 3; k++) {
          const x = i + k;
          if (x < 0 || x >= w) continue;
          sum += L[row + x]!;
          cnt++;
        }
        tmp[i] = sum / cnt;
      }
      L.set(tmp, row);
    }
  }

  // Each billow is a dome: its face toward the light is bright, the far side in
  // its own shade. Normals from the field's slope, lit from high above (by day)
  // and from low down (the setting sun under the cloud), mixed into the depth
  // light above.
  const S = w;
  for (let j = 1; j < h - 1; j++) {
    for (let i = 1; i < w - 1; i++) {
      const o = j * w + i;
      if (dens[o]! < 0.004) continue;
      const gx = (hgt[o + 1]! - hgt[o - 1]!) * 0.5 * S;
      const gy = (hgt[o + w]! - hgt[o - w]!) * 0.5 * S;
      const inv = 1 / Math.sqrt(gx * gx + gy * gy + 1);
      const nx = -gx * inv;
      const ny = -gy * inv;
      const nz = inv;
      // light from above and a little left, toward the viewer
      // wrapped: light soaks round into the shaded side of something this soft
      const up = Math.max(0, (nx * -0.35 + ny * -0.8 + nz * 0.48 + 0.45) / 1.45);
      // light from below
      const dn = Math.max(0, (nx * 0.2 + ny * 0.85 + nz * 0.48 + 0.35) / 1.35);
      top[o] = top[o]! * 0.45 + up * 0.75 * (0.4 + top[o]! * 0.6);
      bot[o] = bot[o]! * 0.55 + dn * 0.6 * (0.3 + bot[o]! * 0.7);
    }
  }

  const canvas = document.createElement("canvas");
  canvas.width = w;
  canvas.height = h;
  return { w, h, dens, top, bot, canvas, key: "" };
}

export type CloudLight = {
  /** 0 by night and at golden hour, 1 under a high sun */
  day: number;
  /** how much this cloud faces the low sun (0-1) */
  lit: number;
  crown: RGB;
  crownA: number;
  mid: RGB;
  belly: RGB;
  bellyA: number;
};

/** colour the volume for the light it is in now */
export function tintCloud(c: CloudShape, L: CloudLight) {
  const ctx = c.canvas.getContext("2d");
  if (!ctx) return;
  const img = ctx.createImageData(c.w, c.h);
  const px = img.data;
  const { dens, top, bot } = c;
  const day = L.day;
  // by day: a white top over a blue-grey shade, a cooler, darker base
  const dShade: RGB = [158, 171, 196];
  const dLit: RGB = [255, 255, 255];
  const dBase: RGB = [138, 148, 170];
  const fire = L.bellyA * (0.25 + 0.75 * L.lit);
  for (let i = 0, n = c.w * c.h; i < n; i++) {
    const d = dens[i]!;
    if (d < 0.004) {
      px[i * 4 + 3] = 0;
      continue;
    }
    const tp = top[i]!;
    const bt = bot[i]!;
    // golden hour and night: the crown in shadow, the rim and belly in the light
    const m = 0.25 + 0.75 * Math.pow(bt, 0.6);
    let r = L.crown[0] + (L.mid[0] - L.crown[0]) * m * 0.7;
    let g = L.crown[1] + (L.mid[1] - L.crown[1]) * m * 0.7;
    let b = L.crown[2] + (L.mid[2] - L.crown[2]) * m * 0.7;
    // thin edges let the light through: the silver (or burning) lining
    const rim = (1 - d) * 0.3 * (0.3 + bt) + Math.pow(bt, 1.4);
    const k = Math.min(1, rim * fire);
    r += (L.belly[0] - r) * k;
    g += (L.belly[1] - g) * k;
    b += (L.belly[2] - b) * k;
    if (day > 0) {
      const lt = Math.min(1, Math.pow(tp, 0.5) * 1.15);
      const under = Math.max(0, 1 - bt) * (1 - tp) * 0.6;
      let dr = dShade[0] + (dLit[0] - dShade[0]) * lt;
      let dg = dShade[1] + (dLit[1] - dShade[1]) * lt;
      let db = dShade[2] + (dLit[2] - dShade[2]) * lt;
      dr += (dBase[0] - dr) * under;
      dg += (dBase[1] - dg) * under;
      db += (dBase[2] - db) * under;
      r += (dr - r) * day;
      g += (dg - g) * day;
      b += (db - b) * day;
    }
    const o = i * 4;
    px[o] = r;
    px[o + 1] = g;
    px[o + 2] = b;
    px[o + 3] = Math.min(1, d * (0.85 + 0.15 * L.crownA)) * 255;
  }
  ctx.putImageData(img, 0, 0);
}
