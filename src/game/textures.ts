/**
 * Procedural textures, painted once into offscreen canvases.
 *
 * Everything here is deterministic (seeded), so the hill and the stone look the
 * same on every visit and every frame, and nothing is generated per frame.
 */

/** small seeded PRNG (mulberry32) */
export function rng(seed: number) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function canvas(w: number, h: number) {
  const cv = document.createElement("canvas");
  cv.width = Math.max(1, Math.ceil(w));
  cv.height = Math.max(1, Math.ceil(h));
  return cv;
}

/**
 * Granite, for the stone: a mid grey-brown body with the three grains granite
 * actually has — dark mica, pale feldspar, glassy quartz — over larger
 * weathering mottles, a few hairline joints, and rust-orange lichen.
 * Painted as a square that the stone's outline clips; it turns with the stone.
 */
export function graniteTexture(px: number): HTMLCanvasElement {
  const cv = canvas(px, px);
  const c = cv.getContext("2d");
  if (!c) return cv;
  const r = rng(91);
  const S = px;
  c.fillStyle = "oklch(0.43 0.018 62)";
  c.fillRect(0, 0, S, S);
  // weathering mottles: broad, soft, both darker and paler
  for (let i = 0; i < 46; i++) {
    const x = r() * S;
    const y = r() * S;
    const rad = (0.06 + r() * 0.22) * S;
    const dark = r() < 0.55;
    const g = c.createRadialGradient(x, y, 0, x, y, rad);
    g.addColorStop(0, dark ? "rgba(30, 24, 20, 0.22)" : "rgba(200, 180, 150, 0.14)");
    g.addColorStop(1, "rgba(0,0,0,0)");
    c.fillStyle = g;
    c.fillRect(x - rad, y - rad, rad * 2, rad * 2);
  }
  // grains
  const grain = (n: number, color: () => string, rmin: number, rmax: number) => {
    for (let i = 0; i < n; i++) {
      c.fillStyle = color();
      const x = r() * S;
      const y = r() * S;
      const rr = (rmin + r() * (rmax - rmin)) * (S / 256);
      c.beginPath();
      c.ellipse(x, y, rr, rr * (0.5 + r() * 0.5), r() * Math.PI, 0, Math.PI * 2);
      c.fill();
    }
  };
  grain(
    1400,
    () => `rgba(${20 + r() * 20}, ${16 + r() * 14}, ${14 + r() * 10}, ${0.35 + r() * 0.4})`,
    0.4,
    1.6,
  );
  grain(
    900,
    () => `rgba(${185 + r() * 40}, ${160 + r() * 30}, ${130 + r() * 30}, ${0.25 + r() * 0.35})`,
    0.4,
    1.5,
  );
  grain(260, () => `rgba(235, 232, 225, ${0.25 + r() * 0.4})`, 0.3, 0.9);
  // joints: hairline cracks the weather has opened
  c.lineCap = "round";
  c.lineJoin = "round";
  for (let k = 0; k < 5; k++) {
    let x = r() * S;
    let y = r() * S;
    let a = r() * Math.PI * 2;
    c.strokeStyle = `rgba(18, 14, 12, ${0.45 + r() * 0.3})`;
    c.lineWidth = (0.8 + r() * 1.4) * (S / 256);
    c.beginPath();
    c.moveTo(x, y);
    const n = 6 + Math.floor(r() * 8);
    for (let i = 0; i < n; i++) {
      a += (r() - 0.5) * 0.9;
      x += Math.cos(a) * S * 0.035;
      y += Math.sin(a) * S * 0.035;
      c.lineTo(x, y);
    }
    c.stroke();
  }
  // lichen: crusty ochre rosettes
  for (let k = 0; k < 7; k++) {
    const x = r() * S;
    const y = r() * S;
    const rad = (0.03 + r() * 0.06) * S;
    for (let j = 0; j < 14; j++) {
      const a = r() * Math.PI * 2;
      const d = r() * rad;
      c.fillStyle = `rgba(${170 + r() * 40}, ${110 + r() * 30}, ${50 + r() * 20}, ${0.12 + r() * 0.18})`;
      c.beginPath();
      c.arc(x + Math.cos(a) * d, y + Math.sin(a) * d, rad * (0.15 + r() * 0.3), 0, Math.PI * 2);
      c.fill();
    }
  }
  return cv;
}

/**
 * The hillside, as a seamless tile anchored to the world: dry, compacted dirt
 * with mottled patches, a scatter of gravel and stones (each lit on the side
 * facing the low sun and shadowed on the other), dry grass litter, and
 * hairline cracks. Everything near an edge is drawn again on the opposite
 * edge, so the tile repeats without a seam.
 */
export function groundTexture(px: number): HTMLCanvasElement {
  const cv = canvas(px, px);
  const c = cv.getContext("2d");
  if (!c) return cv;
  const r = rng(7);
  const S = px;
  const k = S / 512;
  const wrap = (x: number, y: number, rad: number, draw: (x: number, y: number) => void) => {
    for (const dx of [-S, 0, S])
      for (const dy of [-S, 0, S]) {
        const xx = x + dx;
        const yy = y + dy;
        if (xx + rad < 0 || xx - rad > S || yy + rad < 0 || yy - rad > S) continue;
        draw(xx, yy);
      }
  };
  // mottled patches of darker and paler earth
  for (let i = 0; i < 70; i++) {
    const x = r() * S;
    const y = r() * S;
    const rad = (20 + r() * 70) * k;
    const dark = r() < 0.6;
    const a = 0.05 + r() * 0.08;
    wrap(x, y, rad, (xx, yy) => {
      const g = c.createRadialGradient(xx, yy, 0, xx, yy, rad);
      g.addColorStop(0, dark ? `rgba(20, 12, 8, ${a})` : `rgba(220, 170, 110, ${a * 0.8})`);
      g.addColorStop(1, "rgba(0,0,0,0)");
      c.fillStyle = g;
      c.fillRect(xx - rad, yy - rad, rad * 2, rad * 2);
    });
  }
  // fine grit
  for (let i = 0; i < 2600; i++) {
    const x = r() * S;
    const y = r() * S;
    const light = r() < 0.4;
    c.fillStyle = light
      ? `rgba(210, 165, 110, ${0.08 + r() * 0.14})`
      : `rgba(18, 10, 6, ${0.1 + r() * 0.18})`;
    c.fillRect(x, y, (0.6 + r() * 1.4) * k, (0.6 + r() * 1) * k);
  }
  // dry grass litter: short pale strokes lying every which way
  c.lineCap = "round";
  for (let i = 0; i < 160; i++) {
    const x = r() * S;
    const y = r() * S;
    const a = r() * Math.PI;
    const len = (3 + r() * 7) * k;
    c.strokeStyle = `rgba(200, 160, 95, ${0.08 + r() * 0.12})`;
    c.lineWidth = 0.7 * k;
    wrap(x, y, len, (xx, yy) => {
      c.beginPath();
      c.moveTo(xx, yy);
      c.lineTo(xx + Math.cos(a) * len, yy + Math.sin(a) * len * 0.5);
      c.stroke();
    });
  }
  // stones: irregular, lit top-right by the sun, shadow below-left
  for (let i = 0; i < 230; i++) {
    const x = r() * S;
    const y = r() * S;
    const big = r() < 0.12;
    const rad = (big ? 3.5 + r() * 5 : 1 + r() * 2.4) * k;
    const n = 7;
    const shape: Array<[number, number]> = [];
    for (let j = 0; j < n; j++) {
      const a = (j / n) * Math.PI * 2;
      const rr = rad * (0.75 + r() * 0.4);
      shape.push([Math.cos(a) * rr, Math.sin(a) * rr * 0.62]);
    }
    const tone = 0.18 + r() * 0.14;
    wrap(x, y, rad * 2, (xx, yy) => {
      const poly = (ox: number, oy: number) => {
        c.beginPath();
        shape.forEach(([px2, py2], j) =>
          j ? c.lineTo(xx + px2 + ox, yy + py2 + oy) : c.moveTo(xx + px2 + ox, yy + py2 + oy),
        );
        c.closePath();
      };
      c.fillStyle = "rgba(10, 6, 4, 0.35)";
      poly(-rad * 0.35, rad * 0.3);
      c.fill();
      c.fillStyle = `oklch(${tone.toFixed(3)} 0.025 55)`;
      poly(0, 0);
      c.fill();
      c.save();
      poly(0, 0);
      c.clip();
      // a lit crescent on the sun side: light the stone, then lay the body
      // colour back over all but that edge
      c.fillStyle = "rgba(240, 190, 120, 0.32)";
      poly(0, 0);
      c.fill();
      c.fillStyle = `oklch(${tone.toFixed(3)} 0.025 55)`;
      poly(-rad * 0.28, rad * 0.24);
      c.fill();
      c.restore();
      // lit upper-right edge
      c.strokeStyle = `rgba(245, 200, 135, ${0.25 + r() * 0.2})`;
      c.lineWidth = 0.7 * k;
      c.beginPath();
      const a0 = shape[n - 2]!;
      const a1 = shape[n - 1]!;
      const a2 = shape[0]!;
      const a3 = shape[1]!;
      c.moveTo(xx + a0[0], yy + a0[1]);
      c.lineTo(xx + a1[0], yy + a1[1]);
      c.lineTo(xx + a2[0], yy + a2[1]);
      c.lineTo(xx + a3[0], yy + a3[1]);
      c.stroke();
    });
  }
  // hairline cracks in the dried crust
  for (let i = 0; i < 18; i++) {
    let x = r() * S;
    let y = r() * S;
    let a = r() * Math.PI * 2;
    const pts: Array<[number, number]> = [[x, y]];
    const n = 4 + Math.floor(r() * 6);
    for (let j = 0; j < n; j++) {
      a += (r() - 0.5) * 1.2;
      x += Math.cos(a) * 9 * k;
      y += Math.sin(a) * 5 * k;
      pts.push([x, y]);
    }
    c.strokeStyle = `rgba(12, 7, 4, ${0.12 + r() * 0.12})`;
    c.lineWidth = (0.6 + r() * 0.8) * k;
    for (const dx of [-S, 0, S])
      for (const dy of [-S, 0, S]) {
        c.beginPath();
        pts.forEach(([px2, py2], j) =>
          j ? c.lineTo(px2 + dx, py2 + dy) : c.moveTo(px2 + dx, py2 + dy),
        );
        c.stroke();
      }
  }
  return cv;
}
