// Ported from src/game/levels.ts. Level definitions. Add new mountains by appending to LEVELS.
using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    public sealed class Cloud
    {
        /// <summary>horizontal offset in world units</summary>
        public double offset;
        /// <summary>vertical position as a fraction of screen height (sky region)</summary>
        public double yFrac;
        /// <summary>size multiplier</summary>
        public double scale;
        /// <summary>drift speed in px/s</summary>
        public double speed;
        /// <summary>how much slower clouds move than the terrain (0 = far away)</summary>
        public double parallax;
        /// <summary>opacity 0..1</summary>
        public double alpha;
    }

    public sealed class Sun
    {
        /// <summary>sun center x as a fraction of screen width</summary>
        public double xFrac;
        /// <summary>sun center y as a fraction of screen height</summary>
        public double yFrac;
    }

    public sealed class Mountain
    {
        /// <summary>world x of the main peak</summary>
        public double x;
        /// <summary>height of the main peak in world units</summary>
        public double height;
        /// <summary>base width in world units</summary>
        public double width;
        /// <summary>parallax factor against the camera</summary>
        public double parallax;
        /// <summary>silhouette color</summary>
        public string color;
        /// <summary>draw a snow cap</summary>
        public bool snow;
    }

    public sealed class Level
    {
        public string id;
        public string name;
        /// <summary>total horizontal world length in world units</summary>
        public double length;
        /// <summary>vertical rise of the summit</summary>
        public double rise;
        /// <summary>how hard the slope fights back (1 = normal)</summary>
        public double gravityScale;
        /// <summary>cosmetic ridge silhouettes: [amplitude, frequency, offset]</summary>
        public (double, double, double)[] ridges;
        /// <summary>vertical sky gradient as [offset 0..1, colour] stops, top to bottom</summary>
        public (double, string)[] sky;
        /// <summary>the colour the frame's edges sink into (vignette), rather than black</summary>
        public string fog;
        public string epigraph;
        /// <summary>drifting clouds (falls back to procedural clouds when absent: null)</summary>
        public Cloud[] clouds;
        /// <summary>setting sun (falls back to a default sun when absent: null)</summary>
        public Sun sun;
        /// <summary>a distant mountain silhouette (or null)</summary>
        public Mountain mountain;
        /// <summary>twisted olive trees on the path: [world x, size multiplier] (or null)</summary>
        public (double, double)[] trees;
        /// <summary>
        /// Ledges: short, steep pitches in the path, each topped by a stone stele.
        /// [world x of the crest, extra height the pitch adds]. A steady push cannot
        /// climb one — it takes a launch or a run of well-timed heaves — and once the
        /// boulder is over the crest it cannot slip back below it, so every ledge is
        /// also a checkpoint. Zeus's roll back down ignores them. (or null)
        /// </summary>
        public (double, double)[] ledges;
        /// <summary>cypresses, the tall dark flames of these hills: [world x, size] (or null)</summary>
        public (double, double)[] cypresses;
        /// <summary>
        /// Scree: a stretch of loose stones, [from x, to x]. Footing is poor — a push
        /// gets less out of each step, and a stone left braced there creeps back. (or null)
        /// </summary>
        public (double, double)? scree;
        /// <summary>a ridge in the wind: gusts that lean on the stone, 0 (still) to 1 (or null)</summary>
        public double? wind;
        /// <summary>ruins along the path: [world x, height 0..1 of what is left standing] (or null)</summary>
        public (double, double)[] ruins;
    }

    public static class Levels
    {
        /// <summary>horizontal run of a ledge's pitch, in world units</summary>
        public const double LEDGE_RUN = 120;

        // PORT: the long comments on the tartarus sky (why fourteen close oklch stops, peaking
        // at L 0.80 with chroma pulled back) live in src/game/levels.ts.
        public static readonly Level[] LEVELS = new Level[]
        {
            new Level
            {
                id = "tartarus",
                name = "Tartaros Yamacı",
                length = 3200,
                rise = 1500,
                gravityScale = 1,
                ridges = new (double, double, double)[] { (90, 0.0016, 0), (150, 0.0009, 900), (220, 0.0005, 2100) },
                sky = new (double, string)[]
                {
                    (0, "oklch(0.22 0.042 254)"),
                    (0.09, "oklch(0.26 0.045 258)"),
                    (0.18, "oklch(0.3 0.047 264)"),
                    (0.27, "oklch(0.35 0.049 274)"),
                    (0.35, "oklch(0.4 0.05 288)"),
                    (0.43, "oklch(0.45 0.052 306)"),
                    (0.51, "oklch(0.5 0.056 330)"),
                    (0.58, "oklch(0.54 0.062 352)"),
                    (0.65, "oklch(0.58 0.068 14)"),
                    (0.72, "oklch(0.62 0.073 32)"),
                    (0.79, "oklch(0.65 0.077 45)"),
                    (0.86, "oklch(0.68 0.073 56)"),
                    (0.93, "oklch(0.7 0.064 66)"),
                    (1, "oklch(0.72 0.048 74)"),
                },
                fog = "oklch(0.24 0.07 272)",
                epigraph = "Sisyphos'u mutlu hayal etmek gerekir.",
                sun = new Sun
                {
                    xFrac = 0.5,
                    yFrac = 0.58,
                },
                mountain = new Mountain
                {
                    x = 1750,
                    height = 1050,
                    width = 1800,
                    parallax = 0.14,
                    color = "oklch(0.27 0.06 34)",
                    snow = false,
                },
                trees = new (double, double)[]
                {
                    (260, 1),
                    (640, 0.75),
                    (980, 1.15),
                    (1350, 0.85),
                    (1780, 1.1),
                    (2250, 0.9),
                    (2680, 1.2),
                    (3050, 0.8),
                },
                ledges = new (double, double)[] { (900, 38), (1720, 42), (2520, 46) },
                cypresses = new (double, double)[] { (600, 1.05), (1180, 1.25), (1990, 1.1), (2380, 0.95), (2860, 1.3) },
            },
            new Level
            {
                id = "scree",
                name = "Çakıl Yamacı",
                length = 3400,
                rise = 1560,
                gravityScale = 1,
                ridges = new (double, double, double)[] { (110, 0.0013, 300), (170, 0.0008, 1300), (240, 0.0004, 2600) },
                sky = new (double, string)[] {  },
                fog = "oklch(0.25 0.05 250)",
                epigraph = "",
                mountain = new Mountain
                {
                    x = 2100,
                    height = 1250,
                    width = 2100,
                    parallax = 0.12,
                    color = "oklch(0.3 0.045 260)",
                    snow = false,
                },
                trees = new (double, double)[] { (300, 0.9), (760, 1.05), (2250, 0.8), (2780, 1.1), (3250, 0.85) },
                ledges = new (double, double)[] { (720, 36), (2320, 44), (2950, 40) },
                cypresses = new (double, double)[] { (520, 1.15), (2420, 1), (3100, 1.25) },
                scree = (1080, 2060),
            },
            new Level
            {
                id = "windy",
                name = "Rüzgârlı Sırt",
                length = 3000,
                rise = 1680,
                gravityScale = 1.04,
                ridges = new (double, double, double)[] { (80, 0.0019, 500), (140, 0.001, 1500), (210, 0.0006, 2700) },
                sky = new (double, string)[] {  },
                fog = "oklch(0.24 0.04 240)",
                epigraph = "",
                mountain = new Mountain
                {
                    x = 1500,
                    height = 1300,
                    width = 1600,
                    parallax = 0.15,
                    color = "oklch(0.29 0.03 250)",
                    snow = true,
                },
                trees = new (double, double)[] { (420, 0.7), (1900, 0.65) },
                ledges = new (double, double)[] { (1000, 40), (2100, 48) },
                cypresses = new (double, double)[] { (880, 0.9), (2600, 0.85) },
                wind = 1,
            },
            new Level
            {
                id = "ruins",
                name = "Harabeler",
                length = 3300,
                rise = 1500,
                gravityScale = 1,
                ridges = new (double, double, double)[] { (100, 0.0015, 200), (160, 0.0009, 1100), (230, 0.0005, 2400) },
                sky = new (double, string)[] {  },
                fog = "oklch(0.25 0.06 290)",
                epigraph = "",
                mountain = new Mountain
                {
                    x = 1900,
                    height = 1000,
                    width = 1900,
                    parallax = 0.13,
                    color = "oklch(0.28 0.05 20)",
                    snow = false,
                },
                trees = new (double, double)[] { (240, 1), (1150, 0.9), (1900, 1.1), (2600, 0.8) },
                ledges = new (double, double)[] { (800, 36), (1520, 40), (2220, 44), (2880, 48) },
                cypresses = new (double, double)[] { (520, 1.1), (1380, 1.2), (2440, 1.05), (3150, 1.25) },
                ruins = new (double, double)[]
                {
                    (380, 0.55),
                    (450, 1),
                    (520, 0.35),
                    (1020, 0.8),
                    (1700, 0.45),
                    (1760, 1),
                    (1830, 0.9),
                    (1900, 0.3),
                    (2420, 0.7),
                    (3020, 1),
                    (3090, 0.6),
                },
            },
        };

        /// <summary>which hill a cycle is climbed on: three cycles on each, in turn</summary>
        public static int levelForCycle(double cycles)
        {
            return (int)(Math.Floor(Math.Max(0, cycles) / 3) % LEVELS.Length);
        }

        public static Level getLevel(int index)
        {
            return LEVELS[index % LEVELS.Length];
        }

        /// <summary>Terrain height (world Y, up is positive) at world X for a level.</summary>
        public static double terrainAt(Level level, double x)
        {
            double t = Math.Max(0, Math.Min(1, x / level.length));
            double @base = level.rise * Math.Pow(t, 1.25);
            double bumps = Math.Sin(x * 0.004) * 14 * (1 - t) + Math.Sin(x * 0.011 + 1.7) * 6;
            double steps = 0;
            if (level.ledges != null)
            {
                foreach (var (lx, lh) in level.ledges)
                {
                    double u = (x - (lx - LEDGE_RUN)) / LEDGE_RUN;
                    if (u <= 0) continue;
                    if (u >= 1)
                    {
                        steps += lh;
                        continue;
                    }
                    steps += lh * u * u * (3 - 2 * u);
                }
            }
            return @base + bumps + steps;
        }

        /// <summary>Slope (dY/dX) at world X.</summary>
        public static double slopeAt(Level level, double x)
        {
            const double d = 1.5;
            return (terrainAt(level, x + d) - terrainAt(level, x - d)) / (2 * d);
        }
    }
}
