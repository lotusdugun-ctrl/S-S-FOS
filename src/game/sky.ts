import { rng } from "./textures";

/**
 * The sky, as a function of the time of day.
 *
 * Everything here is driven by one number, the sun's elevation in degrees,
 * which follows from the hour. The colours are keyed the way the real sky
 * goes: deep night; astronomical and nautical twilight (indigo overhead, a
 * thin band of afterglow on the horizon under the sun); civil twilight and
 * the blue hour (a pink-orange horizon, the Belt of Venus rising on the far
 * side); the sun on the horizon (red, flattened); golden hour; and day, when
 * the zenith deepens to blue and the horizon pales into haze. Stars, the Milky
 * Way and the moon fade in only as the sky actually gets dark enough to show
 * them.
 */

export type RGB = [number, number, number];

const lerp = (a: number, b: number, t: number) => a + (b - a) * t;
const mix = (a: RGB, b: RGB, t: number): RGB => [
  lerp(a[0], b[0], t),
  lerp(a[1], b[1], t),
  lerp(a[2], b[2], t),
];
const clamp = (x: number, a = 0, b = 1) => Math.max(a, Math.min(b, x));
const smooth = (e0: number, e1: number, x: number) => {
  const t = clamp((x - e0) / (e1 - e0));
  return t * t * (3 - 2 * t);
};
export const rgb = (c: RGB, a = 1) =>
  `rgba(${Math.round(c[0])}, ${Math.round(c[1])}, ${Math.round(c[2])}, ${a.toFixed(3)})`;
const hex = (s: string): RGB => [
  parseInt(s.slice(1, 3), 16),
  parseInt(s.slice(3, 5), 16),
  parseInt(s.slice(5, 7), 16),
];

/** interpolate a table keyed by sun elevation */
function keyed<T>(table: Array<[number, T]>, elev: number, f: (a: T, b: T, t: number) => T): T {
  if (elev <= table[0]![0]) return table[0]![1];
  for (let i = 1; i < table.length; i++) {
    const [e1, v1] = table[i]!;
    const [e0, v0] = table[i - 1]!;
    if (elev <= e1) {
      // mostly linear: a full smoothstep per segment made the light pause at
      // every key and then hurry on, which read as the day jumping
      const u = (elev - e0) / (e1 - e0);
      return f(v0, v1, u * 0.75 + smooth(0, 1, u) * 0.25);
    }
  }
  return table[table.length - 1]![1];
}

/** zenith, 30°, 10°, 3°, horizon */
type Ramp = [RGB, RGB, RGB, RGB, RGB];
const ramp = (...c: string[]) => c.map(hex) as Ramp;
const mixRamp = (a: Ramp, b: Ramp, t: number) => a.map((c, i) => mix(c, b[i]!, t)) as Ramp;

const SKY: Array<[number, Ramp]> = [
  [-18, ramp("#020309", "#03050d", "#060a17", "#090e1e", "#0c1224")],
  [-12, ramp("#050817", "#0a1029", "#141b3d", "#1d2347", "#262846")],
  [-7, ramp("#0b1231", "#17224c", "#2c3767", "#4b4470", "#6d4a63")],
  [-3, ramp("#152153", "#273a78", "#4f5592", "#a2667c", "#d27a58")],
  [0, ramp("#1d2d63", "#334d8c", "#6b6aa0", "#d08273", "#f29a55")],
  // golden hour: the look the game was tuned in
  [4, ramp("#283a6b", "#4d4f87", "#9c6a8a", "#e3967a", "#f6c27e")],
  [10, ramp("#2d4f8f", "#5076b4", "#93a3c4", "#e6c2a0", "#f3d9b0")],
  [22, ramp("#2a5ea9", "#4f86c8", "#8db5dc", "#c5d6e3", "#e2e4dc")],
  [45, ramp("#2459ad", "#3f7fcb", "#7fb0e0", "#b7d3ec", "#d7e5ef")],
];

export type SkyState = {
  hour: number;
  /** sun elevation in degrees */
  elev: number;
  /** sun position on screen, as fractions */
  sunX: number;
  sunY: number;
  /** where the visible horizon sits, as a fraction of height */
  horizon: number;
  ramp: Ramp;
  /** the glow around the sun (forward scattering) */
  glow: RGB;
  glowA: number;
  glowR: number;
  /** afterglow on the horizon under a sun that has just set / is about to rise */
  afterA: number;
  /** pink band opposite the sun in twilight */
  venusA: number;
  /** 0..1 how many stars show */
  stars: number;
  moon: { x: number; y: number; vis: number };
  /** the light falling on the scene (sun or moon) */
  light: { x: number; y: number; color: RGB; strength: number; elev: number };
  /** landscape grade: lerp toward this colour by this much */
  grade: RGB;
  gradeA: number;
  cloud: { crown: RGB; crownA: number; mid: RGB; midA: number; belly: RGB; bellyA: number };
  /** 0..1 how much the birds are about (they roost at night) */
  birds: number;
  /** 0..1 dawn mist */
  mist: number;
  /** how much of the sky the cloud bank covers (clear nights, fair-weather days) */
  cover: number;
};

/** sun elevation for an hour of the day, Mediterranean early autumn */
export function sunElevation(hour: number) {
  const h = ((hour % 24) + 24) % 24;
  return 58 * Math.sin(((h - 6.3) / 12) * Math.PI);
}

export function skyState(hour: number, moonPhase: number): SkyState {
  const h = ((hour % 24) + 24) % 24;
  const elev = sunElevation(h);
  const horizon = 0.62;
  // the sun comes up on the left, over the low ground, and goes down behind
  // the hill in the middle of the frame
  const sunX = 0.24 + clamp((h - 5) / 14) * 0.27;
  const sunY = horizon - (elev / 90) * 0.75;

  const r = keyed(SKY, elev, mixRamp);

  // glow: wide and orange at the horizon, tight and white high up
  const glow = keyed<RGB>(
    [
      [-10, [120, 60, 70]],
      [-2, [255, 120, 70]],
      [3, [255, 175, 100]],
      [15, [255, 225, 170]],
      [40, [255, 250, 235]],
    ],
    elev,
    mix,
  );
  const glowA =
    elev < -10
      ? 0
      : keyed<number>(
          [
            [-10, 0],
            [-3, 0.35],
            [2, 0.55],
            [12, 0.45],
            [40, 0.3],
          ],
          elev,
          lerp,
        );
  const glowR = keyed<number>(
    [
      [-6, 0.45],
      [2, 0.6],
      [15, 0.4],
      [40, 0.25],
    ],
    elev,
    lerp,
  );
  const afterA =
    elev > 2
      ? 0
      : keyed<number>(
          [
            [-14, 0],
            [-8, 0.25],
            [-3, 0.5],
            [2, 0],
          ],
          elev,
          lerp,
        );
  const venusA = keyed<number>(
    [
      [-9, 0],
      [-4, 0.35],
      [1, 0.3],
      [6, 0],
    ],
    elev,
    lerp,
  );
  const stars = smooth(-4, -15, elev);

  // the moon: up through the night, low in the east at dusk, high after midnight
  const hn = (((h - 18.5) % 24) + 24) % 24; // hours since dusk
  const moonUp = hn < 12.5;
  const mt = clamp(hn / 12.5);
  const moon = {
    x: 0.84 - mt * 0.62,
    y: horizon - Math.sin(mt * Math.PI) * 0.52,
    vis: moonUp ? smooth(0, 0.6, Math.sin(mt * Math.PI)) * (1 - smooth(-6, 4, elev)) : 0,
  };

  // the light on the scene
  const sunStrength = smooth(-4, 3, elev);
  const sunColor = keyed<RGB>(
    [
      [-2, [255, 140, 90]],
      [4, [255, 190, 125]],
      [15, [255, 225, 185]],
      [40, [255, 246, 232]],
    ],
    elev,
    mix,
  );
  const moonLight: RGB = [165, 185, 255];
  const useSun = sunStrength > 0.05 || moon.vis < 0.05;
  const light = useSun
    ? { x: sunX, y: sunY, color: sunColor, strength: sunStrength, elev }
    : {
        x: moon.x,
        y: moon.y,
        color: moonLight,
        strength: 0.32 * moon.vis * (0.3 + 0.7 * moonPhase),
        elev: 20,
      };

  const grade = keyed<RGB>(
    [
      [-14, [5, 8, 24]],
      [-8, [14, 18, 46]],
      [-3, [58, 40, 72]],
      [2, [110, 52, 44]],
      [5, [255, 220, 190]],
      [14, [255, 236, 212]],
      [30, [236, 238, 240]],
    ],
    elev,
    mix,
  );
  const gradeA = keyed<number>(
    [
      [-14, 0.6],
      [-8, 0.5],
      [-3, 0.32],
      [2, 0.08],
      [5, 0],
      [14, 0.07],
      [30, 0.1],
      [50, 0.12],
    ],
    elev,
    lerp,
  );

  // clouds: backlit fire at golden hour, white-topped by day, dark by night
  const cloud = {
    crown: keyed<RGB>(
      [
        [-14, [14, 17, 32]],
        [-6, [40, 38, 66]],
        [3, [62, 46, 82]],
        [12, [190, 170, 175]],
        [25, [244, 245, 248]],
      ],
      elev,
      mix,
    ),
    crownA: keyed<number>(
      [
        [-14, 0.85],
        [3, 0.9],
        [25, 0.95],
      ],
      elev,
      lerp,
    ),
    mid: keyed<RGB>(
      [
        [-14, [30, 34, 56]],
        [-4, [150, 90, 110]],
        [3, [214, 132, 120]],
        [15, [228, 210, 200]],
        [25, [222, 226, 234]],
      ],
      elev,
      mix,
    ),
    midA: keyed<number>(
      [
        [-14, 0.25],
        [3, 0.62],
        [25, 0.55],
      ],
      elev,
      lerp,
    ),
    belly: keyed<RGB>(
      [
        [-14, [70, 82, 120]],
        [-5, [230, 110, 90]],
        [3, [255, 215, 150]],
        [15, [200, 196, 200]],
        [25, [160, 168, 184]],
      ],
      elev,
      mix,
    ),
    bellyA: keyed<number>(
      [
        [-14, 0.12],
        [-5, 0.55],
        [3, 1],
        [15, 0.55],
        [25, 0.45],
      ],
      elev,
      lerp,
    ),
  };

  const birds = smooth(-7, -1, elev);
  const cover = keyed<number>(
    [
      [-14, 0.3],
      [-5, 0.6],
      [3, 1],
      [14, 0.6],
      [30, 0.42],
    ],
    elev,
    lerp,
  );
  const mist = clamp(1 - Math.abs(h - 6.4) / 2.2) * 0.9;

  return {
    hour: h,
    elev,
    sunX,
    sunY,
    horizon,
    ramp: r,
    glow,
    glowA,
    glowR,
    afterA,
    venusA,
    stars,
    moon,
    light,
    grade,
    gradeA,
    cloud,
    birds,
    mist,
    cover,
  };
}

/** paint the sky itself — gradient, glow, afterglow, Belt of Venus, sun disc */
export function paintSky(
  c: CanvasRenderingContext2D,
  w: number,
  h: number,
  st: SkyState,
  scale: number,
) {
  const hy = st.horizon * h;
  const g = c.createLinearGradient(0, 0, 0, hy);
  const [z, m30, m10, m3, hz] = st.ramp;
  // elevation angles mapped onto the screen: the horizon sits at hy
  g.addColorStop(0, rgb(z));
  g.addColorStop(0.42, rgb(m30));
  g.addColorStop(0.78, rgb(m10));
  g.addColorStop(0.93, rgb(m3));
  g.addColorStop(1, rgb(hz));
  c.fillStyle = g;
  c.fillRect(0, 0, w, hy + 1);
  // below the horizon line, haze carries on down behind the land
  c.fillStyle = rgb(hz);
  c.fillRect(0, hy, w, h - hy);

  const sx = st.sunX * w;
  const sy = st.sunY * h;

  // the Belt of Venus: a pink band above the horizon on the side away from the
  // sun, with the blue-grey of the earth's own shadow under it
  if (st.venusA > 0.01) {
    const far = sx < w / 2 ? w : 0;
    const band = c.createLinearGradient(0, hy - h * 0.28, 0, hy);
    band.addColorStop(0, "rgba(235, 160, 170, 0)");
    band.addColorStop(0.6, `rgba(235, 160, 170, ${st.venusA})`);
    band.addColorStop(0.82, `rgba(110, 110, 160, ${st.venusA * 0.8})`);
    band.addColorStop(1, `rgba(80, 90, 140, ${st.venusA * 0.6})`);
    c.save();
    const side = c.createLinearGradient(sx, 0, far, 0);
    side.addColorStop(0, "rgba(0,0,0,0)");
    side.addColorStop(1, "rgba(0,0,0,1)");
    // mask the band by distance from the sun, painted through an offscreen
    const tmp = document.createElement("canvas");
    tmp.width = Math.ceil(w);
    tmp.height = Math.ceil(hy);
    const tc = tmp.getContext("2d");
    if (tc) {
      tc.fillStyle = band;
      tc.fillRect(0, 0, w, hy);
      tc.globalCompositeOperation = "destination-in";
      tc.fillStyle = side;
      tc.fillRect(0, 0, w, hy);
      c.drawImage(tmp, 0, 0);
    }
    c.restore();
  }

  // afterglow: the sun is below the horizon but still lights the sky over it
  if (st.afterA > 0.01) {
    const ag = c.createRadialGradient(sx, hy, 0, sx, hy, w * 0.55);
    ag.addColorStop(0, `rgba(255, 140, 80, ${st.afterA})`);
    ag.addColorStop(0.35, `rgba(220, 100, 90, ${st.afterA * 0.45})`);
    ag.addColorStop(1, "rgba(120, 70, 110, 0)");
    c.fillStyle = ag;
    c.fillRect(0, 0, w, hy + 1);
  }

  // forward-scattering glow around the sun
  if (st.glowA > 0.01) {
    const R = w * st.glowR;
    const gl = c.createRadialGradient(sx, sy, 0, sx, sy, R);
    gl.addColorStop(0, rgb(st.glow, st.glowA));
    gl.addColorStop(0.25, rgb(st.glow, st.glowA * 0.45));
    gl.addColorStop(1, rgb(st.glow, 0));
    c.save();
    c.globalCompositeOperation = "lighter";
    c.fillStyle = gl;
    c.fillRect(0, 0, w, h);
    c.restore();
  }

  // the disc: red and squashed by refraction near the horizon, small and
  // white with a hard bloom when high
  if (st.elev > -1.2) {
    const rad = 40 * scale * (st.elev < 8 ? 1.05 : 0.8);
    const flat = st.elev < 6 ? 0.86 + st.elev * 0.02 : 1;
    const core = st.elev < 3 ? [255, 160, 95] : st.elev < 14 ? [255, 222, 170] : [255, 252, 244];
    const edge = st.elev < 3 ? [240, 110, 70] : st.elev < 14 ? [255, 196, 130] : [255, 240, 215];
    c.save();
    c.translate(sx, sy);
    c.scale(1, flat);
    const d = c.createRadialGradient(0, 0, rad * 0.05, 0, 0, rad);
    d.addColorStop(0, rgb(core as RGB));
    d.addColorStop(0.85, rgb(mix(core as RGB, edge as RGB, 0.6)));
    // high up the limb dissolves into the bloom instead of ending on a rim
    d.addColorStop(1, rgb(edge as RGB, st.elev > 10 ? 0.35 : 0.9));
    c.fillStyle = d;
    c.beginPath();
    c.arc(0, 0, rad, 0, Math.PI * 2);
    c.fill();
    if (st.elev > 10) {
      c.globalCompositeOperation = "lighter";
      // a photographic bloom: bright close in, falling off fast, so it
      // never reads as a coloured ring against the blue
      const b = c.createRadialGradient(0, 0, rad * 0.5, 0, 0, rad * 3.2);
      b.addColorStop(0, "rgba(255, 252, 240, 0.6)");
      b.addColorStop(0.18, "rgba(255, 250, 238, 0.26)");
      b.addColorStop(0.5, "rgba(255, 248, 235, 0.07)");
      b.addColorStop(1, "rgba(255, 248, 235, 0)");
      c.fillStyle = b;
      c.beginPath();
      c.arc(0, 0, rad * 3.2, 0, Math.PI * 2);
      c.fill();
    }
    c.restore();
  }
}

/**
 * The night sky: about two thousand stars with a realistic spread of
 * brightness (most faint, a handful bright), each tinted by its temperature,
 * and the Milky Way as a soft, mottled band with dark dust lanes through it.
 * Painted once per screen size.
 */
export function starField(
  w: number,
  h: number,
  dpr: number,
): { cv: HTMLCanvasElement; bright: Array<[number, number, number]> } {
  const cv = document.createElement("canvas");
  cv.width = Math.ceil(w * dpr);
  cv.height = Math.ceil(h * dpr);
  const c = cv.getContext("2d");
  const bright: Array<[number, number, number]> = [];
  if (!c) return { cv, bright };
  c.scale(dpr, dpr);
  const r = rng(42);
  // the Milky Way: a band from lower left to upper right
  const band = (u: number) => ({ x: u * w, y: h * (0.62 - u * 0.55) });
  for (let i = 0; i < 260; i++) {
    const u = r();
    const p = band(u);
    const off = (r() - 0.5) * h * 0.22;
    const x = p.x + off * 0.5;
    const y = p.y + off;
    const rad = (20 + r() * 70) * (0.6 + 0.4 * Math.sin(u * 9));
    const g = c.createRadialGradient(x, y, 0, x, y, rad);
    const warm = r() < 0.3;
    g.addColorStop(0, warm ? "rgba(255, 230, 210, 0.045)" : "rgba(200, 215, 255, 0.05)");
    g.addColorStop(1, "rgba(200, 215, 255, 0)");
    c.fillStyle = g;
    c.fillRect(x - rad, y - rad, rad * 2, rad * 2);
  }
  // dust lanes
  c.globalCompositeOperation = "destination-out";
  for (let i = 0; i < 70; i++) {
    const u = r();
    const p = band(u);
    const off = (r() - 0.45) * h * 0.08;
    const rad = 8 + r() * 26;
    const g = c.createRadialGradient(p.x + off, p.y + off, 0, p.x + off, p.y + off, rad);
    g.addColorStop(0, "rgba(0,0,0,0.6)");
    g.addColorStop(1, "rgba(0,0,0,0)");
    c.fillStyle = g;
    c.fillRect(p.x + off - rad, p.y + off - rad, rad * 2, rad * 2);
  }
  c.globalCompositeOperation = "source-over";
  // stars, denser along the band
  const N = Math.round((w * h) / 520);
  for (let i = 0; i < N; i++) {
    let x = r() * w;
    let y = r() * h * 0.7;
    if (r() < 0.35) {
      const p = band(r());
      x = p.x + (r() - 0.5) * h * 0.12;
      y = p.y + (r() - 0.5) * h * 0.2;
    }
    const mag = Math.pow(r(), 3.2); // most faint, a few bright
    const size = 0.35 + mag * 1.4;
    const t = r();
    const col = t < 0.15 ? [255, 210, 170] : t < 0.3 ? [190, 210, 255] : [245, 245, 255];
    c.fillStyle = `rgba(${col[0]}, ${col[1]}, ${col[2]}, ${(0.25 + mag * 0.75).toFixed(3)})`;
    c.beginPath();
    c.arc(x, y, size, 0, Math.PI * 2);
    c.fill();
    if (mag > 0.55) bright.push([x, y, mag]);
  }
  return { cv, bright };
}

/**
 * The moon, with its real phase: the lit part bounded by a half-ellipse
 * terminator, dark maria on the face, the unlit part faintly visible by
 * earthshine, and a soft halo.
 */
export function drawMoon(
  c: CanvasRenderingContext2D,
  x: number,
  y: number,
  R: number,
  phase: number,
  alpha: number,
) {
  if (alpha < 0.01) return;
  c.save();
  c.globalAlpha = alpha;
  // halo
  const halo = c.createRadialGradient(x, y, R, x, y, R * 7);
  halo.addColorStop(0, "rgba(200, 215, 255, 0.22)");
  halo.addColorStop(1, "rgba(200, 215, 255, 0)");
  c.fillStyle = halo;
  c.beginPath();
  c.arc(x, y, R * 7, 0, Math.PI * 2);
  c.fill();
  // earthshine on the dark limb
  c.fillStyle = "rgba(90, 100, 130, 0.35)";
  c.beginPath();
  c.arc(x, y, R, 0, Math.PI * 2);
  c.fill();
  // the lit part: phase 0 new, 0.5 full, 1 new again; waxing lit on the right
  const p = ((phase % 1) + 1) % 1;
  const lit = 1 - Math.abs(p - 0.5) * 2; // 0..1..0
  const waxing = p < 0.5;
  const k = Math.cos(lit * Math.PI); // terminator ellipse x-radius factor
  c.save();
  c.translate(x, y);
  if (!waxing) c.scale(-1, 1);
  c.beginPath();
  c.arc(0, 0, R, -Math.PI / 2, Math.PI / 2); // right half
  c.ellipse(0, 0, Math.abs(k) * R, R, 0, Math.PI / 2, -Math.PI / 2, k > 0);
  c.closePath();
  c.clip();
  const face = c.createRadialGradient(-R * 0.3, -R * 0.3, R * 0.1, 0, 0, R);
  face.addColorStop(0, "rgb(250, 248, 238)");
  face.addColorStop(1, "rgb(215, 215, 210)");
  c.fillStyle = face;
  c.fillRect(-R, -R, R * 2, R * 2);
  // maria
  c.fillStyle = "rgba(150, 155, 165, 0.45)";
  for (const [mx, my, mr] of [
    [-0.25, -0.3, 0.28],
    [0.15, -0.2, 0.22],
    [-0.1, 0.15, 0.3],
    [0.3, 0.25, 0.16],
    [-0.4, 0.2, 0.14],
  ] as const) {
    c.beginPath();
    c.arc(mx * R, my * R, mr * R, 0, Math.PI * 2);
    c.fill();
  }
  c.restore();
  c.restore();
}
