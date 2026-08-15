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
};

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
    // A storm, not a sunset. The light in this scene now comes from underneath —
    // molten rock in the ground — so the sky's whole job is to be the cold thing
    // that measures the heat, and to be dark enough that a glowing crack reads as
    // glowing rather than as a slightly oranger patch of a bright frame.
    //
    // Canvas interpolates gradient stops in sRGB, not in oklch, so stops far
    // apart get muddy between them. That matters more here than it did: the ramp
    // has to travel from blue to a warm break at the horizon, and there are two
    // ways round the hue circle. Going down through 200-120 crosses cyan and
    // green and turns the low sky into pond water. Going up through violet and
    // magenta is the way weather actually does it. The stops below take the
    // second route, closely enough spaced that sRGB has nothing left to guess.
    //
    // Lightness climbs only to 0.5, against 0.72 before. The old sky was the
    // brightest thing in the frame; this one has to sit behind a fire.
    sky: [
      [0, "oklch(0.12 0.02 262)"],
      [0.1, "oklch(0.15 0.022 259)"],
      [0.2, "oklch(0.19 0.024 256)"],
      [0.3, "oklch(0.23 0.026 253)"],
      [0.4, "oklch(0.27 0.027 250)"],
      [0.5, "oklch(0.31 0.028 248)"],
      [0.58, "oklch(0.34 0.029 246)"],
      [0.66, "oklch(0.37 0.031 245)"],
      // from here the ramp turns warm, the long way round through violet
      [0.73, "oklch(0.39 0.034 250)"],
      [0.79, "oklch(0.4 0.038 268)"],
      [0.85, "oklch(0.41 0.045 300)"],
      [0.9, "oklch(0.43 0.058 340)"],
      [0.95, "oklch(0.45 0.078 20)"],
      [1, "oklch(0.48 0.1 42)"],
    ],
    fog: "oklch(0.11 0.028 262)",
    epigraph: "Sisyphos'u mutlu hayal etmek gerekir.",
    // Not a sun any more — the break in the cloud the storm light comes through.
    // It still anchors the horizon and the direction of the sky light; it simply
    // is not a disc you can point at.
    sun: { xFrac: 0.5, yFrac: 0.58 },
    mountain: {
      x: 1750,
      height: 1050,
      width: 1800,
      parallax: 0.14,
      // the farthest mass, so the haze has the most of it. With the sun gone that
      // haze is cold again — blue is what distance does when nothing warm is
      // lighting it
      color: "oklch(0.26 0.028 254)",
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
  },
];

export function getLevel(index: number): Level {
  return LEVELS[index % LEVELS.length]!;
}

/** Terrain height (world Y, up is positive) at world X for a level. */
export function terrainAt(level: Level, x: number): number {
  const t = Math.max(0, Math.min(1, x / level.length));
  const base = level.rise * Math.pow(t, 1.25);
  const bumps = Math.sin(x * 0.004) * 14 * (1 - t) + Math.sin(x * 0.011 + 1.7) * 6;
  return base + bumps;
}

/** Slope (dY/dX) at world X. */
export function slopeAt(level: Level, x: number): number {
  const d = 1.5;
  return (terrainAt(level, x + d) - terrainAt(level, x - d)) / (2 * d);
}
