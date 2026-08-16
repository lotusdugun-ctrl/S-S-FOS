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
  /** horizontal parallax factor against the camera */
  parallax: number;
  /**
   * Vertical parallax, which is not the same number and cannot be.
   *
   * Horizontally a far mountain should barely move. Vertically it must barely
   * move *for a different reason*: the slope climbs 1500 units, so the near
   * skyline rides a long way up the screen during a run, and a far peak sharing
   * the horizontal factor sinks under it around the middle of the climb. Near
   * zero keeps the peak pinned near the top of the frame, which is where a
   * distant summit belongs anyway.
   */
  parallaxY: number;
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
      /*
       * Moved left, for the same reason the height changed: the peak has to be
       * on screen, and it was not.
       *
       * The peak sits at x - width*0.18, and at 1900 that projected to about
       * 1540 px on a 1280 px frame — off the right edge for the entire climb.
       * The mass showed, its summit never did. At 1130 the peak crosses the
       * frame from roughly 990 px down to 455 px as he climbs, which is on
       * screen throughout and drifting the right way.
       */
      x: 1130,
      /*
       * Lowered from 1050, and the reason is Prometheus rather than taste.
       *
       * At the old height the peak projected to roughly 100-200 px *above* the
       * top of a 640 px frame for the whole climb: the mass filled the right of
       * the picture and its summit was never in it. Fine for a silhouette,
       * useless once somebody is standing on top of it.
       *
       * 675 with a vertical parallax of 0.03 holds the peak between 45 and 68 px
       * from the top for the entire run, clearing the near ridge by at least 49
       * px at the tightest point, which is around seventy percent of the way up.
       * Solved rather than guessed — the first two attempts put it off the top
       * of the screen and then buried it behind the slope.
       */
      height: 675,
      width: 2000,
      parallax: 0.14,
      parallaxY: 0.03,
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
