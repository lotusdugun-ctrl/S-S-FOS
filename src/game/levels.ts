// Level definitions. Add new mountains by appending to LEVELS.

export type Cloud = {
  /** horizontal offset in world units */
  offset: number;
  /** vertical position as a fraction of screen height (sky region) */
  yFrac: number;
  /** size multiplier */
  scale: number;
  /** drift speed in px/s */
  speed: number;
  /** how much slower clouds move than the terrain (0 = far away) */
  parallax: number;
  /** opacity 0..1 */
  alpha: number;
};

export type Sun = {
  /** sun center x as a fraction of screen width */
  xFrac: number;
  /** sun center y as a fraction of screen height */
  yFrac: number;
};

export type Mountain = {
  /** world x of the main peak */
  x: number;
  /** height of the main peak in world units */
  height: number;
  /** base width in world units */
  width: number;
  /** parallax factor against the camera */
  parallax: number;
  /** silhouette color */
  color: string;
  /** draw a snow cap */
  snow: boolean;
};

export type Level = {
  id: string;
  name: string;
  /** total horizontal world length in world units */
  length: number;
  /** vertical rise of the summit */
  rise: number;
  /** how hard the slope fights back (1 = normal) */
  gravityScale: number;
  /** cosmetic ridge silhouettes: [amplitude, frequency, offset] */
  ridges: Array<[number, number, number]>;
  /** vertical sky gradient as [offset 0..1, colour] stops, top to bottom */
  sky: Array<[number, string]>;
  /** the colour the frame's edges sink into (vignette), rather than black */
  fog: string;
  epigraph: string;
  /** drifting clouds (falls back to procedural clouds when absent) */
  clouds?: Cloud[];
  /** setting sun (falls back to a default sun when absent) */
  sun?: Sun;
  /** a distant mountain silhouette */
  mountain?: Mountain;
  /** twisted olive trees on the path: [world x, size multiplier] */
  trees?: Array<[number, number]>;
  /**
   * Ledges: short, steep pitches in the path, each topped by a stone stele.
   * [world x of the crest, extra height the pitch adds]. A steady push cannot
   * climb one — it takes a launch or a run of well-timed heaves — and once the
   * boulder is over the crest it cannot slip back below it, so every ledge is
   * also a checkpoint. Zeus's roll back down ignores them.
   */
  ledges?: Array<[number, number]>;
  /** cypresses, the tall dark flames of these hills: [world x, size] */
  cypresses?: Array<[number, number]>;
  /**
   * Scree: a stretch of loose stones, [from x, to x]. Footing is poor — a push
   * gets less out of each step, and a stone left braced there creeps back.
   */
  scree?: [number, number];
  /** a ridge in the wind: gusts that lean on the stone, 0 (still) to 1 */
  wind?: number;
  /** ruins along the path: [world x, height 0..1 of what is left standing] */
  ruins?: Array<[number, number]>;
};

/** horizontal run of a ledge's pitch, in world units */
export const LEDGE_RUN = 120;

export const LEVELS: Level[] = [
  {
    id: "tartarus",
    name: "Tartaros Yamacı",
    length: 3200,
    rise: 1500,
    gravityScale: 1,
    ridges: [
      [90, 0.0016, 0],
      [150, 0.0009, 900],
      [220, 0.0005, 2100],
    ],
    // Golden hour reads as gold only because there is cold above it to measure
    // against: deep blue at the zenith, down through violet and rose before any
    // of the warmth arrives.
    // Canvas interpolates gradient stops in sRGB, not in oklch, so a few stops
    // far apart get muddy between them — the old eight had to cross from 258° to
    // 70° through magenta in four jumps. Fourteen closely spaced stops leave
    // sRGB almost nothing to guess at. Lightness climbs the whole way down, the
    // hue rotates without ever doubling back, and chroma peaks in the warm band
    // and then falls off again at the horizon, where haze washes colour out.
    // The ramp used to top out at L 0.94 — near white across the whole lower
    // half of the frame, which is exhausting to sit in front of. It peaks at
    // 0.80 now. Nothing here is competing with the disc any more, and a sun is
    // only as bright as what surrounds it is not.
    // A backdrop's job is to be behind something. Two levers pull it back
    // without flattening it: chroma comes down about a quarter, since saturation
    // is what makes a sky insist on being looked at, and the lightness range
    // narrows from 0.54 to 0.44 so neighbouring bands sit closer together and
    // the whole thing reads as one soft wash instead of a stack of stripes.
    sky: [
      // The zenith stays low — that is where "not glaring" is bought — but the
      // lower half climbs back up, because that is the band the man is standing
      // against and he has to be visible against it.
      [0, "oklch(0.22 0.042 254)"],
      [0.09, "oklch(0.26 0.045 258)"],
      [0.18, "oklch(0.3 0.047 264)"],
      [0.27, "oklch(0.35 0.049 274)"],
      [0.35, "oklch(0.4 0.05 288)"],
      [0.43, "oklch(0.45 0.052 306)"],
      [0.51, "oklch(0.5 0.056 330)"],
      [0.58, "oklch(0.54 0.062 352)"],
      [0.65, "oklch(0.58 0.068 14)"],
      [0.72, "oklch(0.62 0.073 32)"],
      [0.79, "oklch(0.65 0.077 45)"],
      [0.86, "oklch(0.68 0.073 56)"],
      [0.93, "oklch(0.7 0.064 66)"],
      [1, "oklch(0.72 0.048 74)"],
    ],
    fog: "oklch(0.24 0.07 272)",
    epigraph: "Sisyphos'u mutlu hayal etmek gerekir.",
    sun: { xFrac: 0.5, yFrac: 0.58 },
    mountain: {
      x: 1750,
      height: 1050,
      width: 1800,
      parallax: 0.14,
      // the farthest mass, so the haze has the most of it — and this near the
      // sun that haze is warm, not blue
      color: "oklch(0.27 0.06 34)",
      snow: false,
    },
    trees: [
      [260, 1],
      [640, 0.75],
      [980, 1.15],
      [1350, 0.85],
      [1780, 1.1],
      [2250, 0.9],
      [2680, 1.2],
      [3050, 0.8],
    ],
    cypresses: [
      [600, 1.05],
      [1180, 1.25],
      [1990, 1.1],
      [2380, 0.95],
      [2860, 1.3],
    ],
    ledges: [
      [900, 38],
      [1720, 42],
      [2520, 46],
    ],
  },
  {
    // loose stone: a long scree field in the middle of the climb
    id: "scree",
    name: "Çakıl Yamacı",
    length: 3400,
    rise: 1560,
    gravityScale: 1,
    ridges: [
      [110, 0.0013, 300],
      [170, 0.0008, 1300],
      [240, 0.0004, 2600],
    ],
    sky: [],
    fog: "oklch(0.25 0.05 250)",
    epigraph: "",
    mountain: {
      x: 2100,
      height: 1250,
      width: 2100,
      parallax: 0.12,
      color: "oklch(0.3 0.045 260)",
      snow: false,
    },
    trees: [
      [300, 0.9],
      [760, 1.05],
      [2250, 0.8],
      [2780, 1.1],
      [3250, 0.85],
    ],
    cypresses: [
      [520, 1.15],
      [2420, 1.0],
      [3100, 1.25],
    ],
    ledges: [
      [720, 36],
      [2320, 44],
      [2950, 40],
    ],
    scree: [1080, 2060],
  },
  {
    // a bare, steep ridge where the wind leans on everything
    id: "windy",
    name: "Rüzgârlı Sırt",
    length: 3000,
    rise: 1680,
    gravityScale: 1.04,
    ridges: [
      [80, 0.0019, 500],
      [140, 0.001, 1500],
      [210, 0.0006, 2700],
    ],
    sky: [],
    fog: "oklch(0.24 0.04 240)",
    epigraph: "",
    mountain: {
      x: 1500,
      height: 1300,
      width: 1600,
      parallax: 0.15,
      color: "oklch(0.29 0.03 250)",
      snow: true,
    },
    trees: [
      [420, 0.7],
      [1900, 0.65],
    ],
    cypresses: [
      [880, 0.9],
      [2600, 0.85],
    ],
    ledges: [
      [1000, 40],
      [2100, 48],
    ],
    wind: 1,
  },
  {
    // the ruins of a temple, its columns still climbing the hill
    id: "ruins",
    name: "Harabeler",
    length: 3300,
    rise: 1500,
    gravityScale: 1,
    ridges: [
      [100, 0.0015, 200],
      [160, 0.0009, 1100],
      [230, 0.0005, 2400],
    ],
    sky: [],
    fog: "oklch(0.25 0.06 290)",
    epigraph: "",
    mountain: {
      x: 1900,
      height: 1000,
      width: 1900,
      parallax: 0.13,
      color: "oklch(0.28 0.05 20)",
      snow: false,
    },
    trees: [
      [240, 1],
      [1150, 0.9],
      [1900, 1.1],
      [2600, 0.8],
    ],
    cypresses: [
      [520, 1.1],
      [1380, 1.2],
      [2440, 1.05],
      [3150, 1.25],
    ],
    ledges: [
      [800, 36],
      [1520, 40],
      [2220, 44],
      [2880, 48],
    ],
    ruins: [
      [380, 0.55],
      [450, 1],
      [520, 0.35],
      [1020, 0.8],
      [1700, 0.45],
      [1760, 1],
      [1830, 0.9],
      [1900, 0.3],
      [2420, 0.7],
      [3020, 1],
      [3090, 0.6],
    ],
  },
];

/** which hill a cycle is climbed on: three cycles on each, in turn */
export function levelForCycle(cycles: number): number {
  return Math.floor(Math.max(0, cycles) / 3) % LEVELS.length;
}

export function getLevel(index: number): Level {
  return LEVELS[index % LEVELS.length]!;
}

/** Terrain height (world Y, up is positive) at world X for a level. */
export function terrainAt(level: Level, x: number): number {
  const t = Math.max(0, Math.min(1, x / level.length));
  const base = level.rise * Math.pow(t, 1.25);
  const bumps = Math.sin(x * 0.004) * 14 * (1 - t) + Math.sin(x * 0.011 + 1.7) * 6;
  let steps = 0;
  if (level.ledges) {
    for (const [lx, lh] of level.ledges) {
      const u = (x - (lx - LEDGE_RUN)) / LEDGE_RUN;
      if (u <= 0) continue;
      if (u >= 1) {
        steps += lh;
        continue;
      }
      steps += lh * u * u * (3 - 2 * u);
    }
  }
  return base + bumps + steps;
}

/** Slope (dY/dX) at world X. */
export function slopeAt(level: Level, x: number): number {
  const d = 1.5;
  return (terrainAt(level, x + d) - terrainAt(level, x - d)) / (2 * d);
}
