// Ported from src/game/mountain.ts.
/*
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
 *
 * PORT: the module's free functions live in the static class `MountainGen`
 * (Levels.cs already has a class `Mountain` for the level data).
 */
using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    public sealed class MountainSpec
    {
        public double x;
        public double height;
        public double width;
        public bool snow;
        /// <summary>the hill's id: seeds the noise</summary>
        public double seed;
        /// <summary>rock colour for this hill</summary>
        public RGB rock;
        /// <summary>where the snow starts, as a share of the height (0 or less: from `snow`)</summary>
        public double snowLine;
        /// <summary>a long range of peaks (the far skyline) instead of one massif</summary>
        public bool range;
    }

    /// <summary>What computeMountain returns (the worker's reply): both lightings and the river.</summary>
    public sealed class MountainPixels
    {
        public byte[] A;
        public byte[] B;
        public List<(double x, double y)> river;
    }

    public static class MountainGen
    {
        internal static double hash(double x, double y, double s)
        {
            int h = Js.ToInt32(x * 374761393 + y * 668265263 + s * 2246822519);
            h = Js.imul(h ^ unchecked((int)Js.urs(h, 13)), 1274126177);
            h ^= unchecked((int)Js.urs(h, 16));
            return unchecked((uint)h) / 4294967295.0;
        }

        internal static double vnoise(double x, double y, double s)
        {
            double xi = Math.Floor(x);
            double yi = Math.Floor(y);
            double xf = x - xi;
            double yf = y - yi;
            double u = xf * xf * (3 - 2 * xf);
            double v = yf * yf * (3 - 2 * yf);
            double a = hash(xi, yi, s);
            double b = hash(xi + 1, yi, s);
            double c = hash(xi, yi + 1, s);
            double d = hash(xi + 1, yi + 1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        internal static double fbm(double x, double y, double s, int oct)
        {
            double sum = 0;
            double amp = 0.5;
            double f = 1;
            for (int i = 0; i < oct; i++)
            {
                sum += vnoise(x * f, y * f, s + i * 17) * amp;
                f *= 2.03;
                amp *= 0.5;
            }
            return sum;
        }

        /// <summary>ridged noise: sharp crests, rounded valleys — what eroded rock looks like</summary>
        internal static double ridged(double x, double y, double s, int oct, double gain = 1.6)
        {
            double sum = 0;
            double amp = 0.55;
            double f = 1;
            double w = 1;
            for (int i = 0; i < oct; i++)
            {
                double n = 1 - Math.Abs(vnoise(x * f, y * f, s + i * 31) * 2 - 1);
                n *= n;
                n *= w;
                w = Math.Min(1, n * gain);
                sum += n * amp;
                f *= 2.1;
                amp *= 0.5;
            }
            return sum;
        }

        /// <summary>the mountain's pixels, worked out in one go (see paint.worker)</summary>
        public static MountainPixels computeMountain(MountainSpec spec, double pxPerUnit)
        {
            return new MountainPainter(spec, pxPerUnit, false).computeAll();
        }

        /// <summary>A Uint8ClampedArray store: NaN to 0, clamped, rounded half to even.</summary>
        internal static byte U8(double v)
        {
            if (!(v > 0)) return 0;
            if (v >= 255) return 255;
            return (byte)Math.Round(v, MidpointRounding.ToEven);
        }
    }

    public sealed class MountainPainter
    {
        public readonly MountainSpec spec;
        /// <summary>world units per sprite pixel, horizontally and vertically</summary>
        public readonly double kx;
        public readonly double ky;
        /// <summary>the sprite's world extent</summary>
        public readonly double x0;
        public readonly double top;
        public readonly int W;
        public readonly int H;
        /// <summary>crest height (world y) at each sprite column (a Float32Array)</summary>
        public readonly float[] crest;
        /// <summary>the crest smoothed wide, for the mass's broad form (the jagged one would streak it)</summary>
        private float[] crestSoft;
        /// <summary>the river, in world coordinates</summary>
        public List<(double x, double y)> river = new List<(double x, double y)>();
        /// <summary>lit from the left, lit from the right (made when the painting is finished)</summary>
        public Surface a;
        public Surface b;
        public bool ready = false;
        /// <summary>the pixels are worked out (here or in the worker); only finishing is left</summary>
        private bool computed = false;
        /// <summary>the worker is working them out</summary>
        private bool pending = false;
        private int stage = 0;
        private int row = 0;
        private float[] hf;
        private byte[] imgA = null;
        private byte[] imgB = null;
        private double snowLine;

        /// <summary>
        /// `pxPerUnit` is how many sprite pixels one world unit across gets; the
        /// mountain is drawn squashed to half height on screen, so vertically it
        /// gets half as many.
        /// </summary>
        public MountainPainter(MountainSpec spec, double pxPerUnit, bool offload = true)
        {
            this.spec = spec;
            double x = spec.x, height = spec.height, width = spec.width;
            kx = 1 / pxPerUnit;
            ky = 2 / pxPerUnit;
            x0 = x - width / 2;
            top = height * 1.22;
            W = (int)Math.Max(8, Math.Ceiling(width / kx));
            // down to well under the base, which the hills in front hide anyway
            H = (int)Math.Max(8, Math.Ceiling((top + height * 0.25) / ky));
            snowLine = spec.snowLine > 0 ? spec.snowLine : spec.snow ? 0.64 : 0.8;
            hf = new float[W * H];
            crest = new float[W];
            crestSoft = new float[W];
            buildCrest();
            buildRidges();
            int R = (int)Math.Max(2, Js.round(W * 0.02));
            for (int i = 0; i < W; i++)
            {
                double sum = 0;
                int n = 0;
                for (int j = Math.Max(0, i - R); j <= Math.Min(W - 1, i + R); j++, n++)
                    sum += crest[j];
                crestSoft[i] = (float)(sum / n);
            }
            // the whole height field and both lightings, worked out in the worker
            if (offload)
            {
                pending = true;
                hf = new float[0];
                Work.Run(() => MountainGen.computeMountain(spec, pxPerUnit), r =>
                {
                    pending = false;
                    if (r != null)
                    {
                        imgA = r.A;
                        imgB = r.B;
                        river = r.river;
                        computed = true;
                    }
                    else
                    {
                        // no worker after all: do it here, a few rows a frame
                        hf = new float[W * H];
                    }
                });
            }
        }

        /// <summary>everything at once (in the worker): the two lightings and the river</summary>
        public MountainPixels computeAll()
        {
            for (int r = 0; r < H; r++) heightRow(r);
            for (int r = 0; r < H; r++) shadeRow(r);
            traceRiver();
            return new MountainPixels { A = imgA, B = imgB, river = river };
        }

        /// <summary>the skyline: a main summit, a shoulder, crags along the way</summary>
        /// <summary>the peaks: where, how high (share of the height), how wide each flank (share of the width)</summary>
        private double[] peakX, peakK, peakL, peakR;

        private void buildPeaks()
        {
            double x = spec.x, width = spec.width, seed = spec.seed;
            if (!spec.range)
            {
                peakX = new[] { x - width * 0.16, x + width * 0.24, x + width * 0.4 };
                peakK = new[] { 1.0, 0.68, 0.42 };
                peakL = new[] { 0.36, 0.3, 0.16 };
                peakR = new[] { 0.46, 0.3, 0.16 };
                return;
            }
            // a range: seven summits of different heights along it, the flanks overlapping
            const int N = 7;
            peakX = new double[N]; peakK = new double[N]; peakL = new double[N]; peakR = new double[N];
            for (int i = 0; i < N; i++)
            {
                peakX[i] = x0 + width * ((i + 0.5) / N + (MountainGen.hash(i, 11, seed) - 0.5) * 0.08);
                peakK[i] = 0.45 + MountainGen.hash(i, 12, seed) * 0.55;
                peakL[i] = 0.08 + MountainGen.hash(i, 13, seed) * 0.08;
                peakR[i] = 0.08 + MountainGen.hash(i, 14, seed) * 0.08;
            }
        }

        private void buildCrest()
        {
            double x = spec.x, height = spec.height, width = spec.width, seed = spec.seed;
            double baseY = height * (spec.range ? 0.2 : 0.12);
            var kOf = new float[W];
            var spire = new float[W];
            buildPeaks();
            for (int i = 0; i < W; i++)
            {
                double wx = x0 + i * kx;
                // the peaks as cones, their union, with concave flanks
                double k = 0;
                for (int p = 0; p < peakX.Length; p++)
                {
                    double d = wx - peakX[p];
                    double c = peakK[p] * (1 - Math.Abs(d) / (width * (d < 0 ? peakL[p] : peakR[p])));
                    if (c > k) k = c;
                }
                // concave flanks: steep near the top, easing out toward the foot
                k = Math.Pow(k, 1.55);
                // crags: large slow bumps and small sharp ones, more of them high up
                double big = (MountainGen.fbm(wx / (width * 0.09), 0.5, seed, 4) - 0.5) * 0.22;
                double sharp = (MountainGen.ridged(wx / (width * 0.02), 1.5, seed + 3, 4) - 0.3) * 0.06;
                double edge = Math.Min(1, Math.Min(wx - x0, x0 + width - wx) / (width * 0.08));
                crest[i] = (float)(baseY * edge + (k + (big + sharp) * (0.3 + k)) * height * edge);
                kOf[i] = (float)k;
            }
            // towers: narrow spires standing out of the upper crest, as in the Dolomites
            for (int j = 0; j < (spec.range ? 10 : 26); j++)
            {
                double cx = x0 + width * (0.12 + 0.76 * MountainGen.hash(j, 1, seed + 5));
                int ci = (int)Math.Max(0, Math.Min(W - 1, (cx - x0) / kx));
                double kc = kOf[ci];
                if (kc < 0.25) continue;
                double wdt = width * (0.008 + 0.014 * MountainGen.hash(j, 2, seed + 5));
                double amp = height * (0.03 + 0.07 * MountainGen.hash(j, 3, seed + 5)) * Math.Min(1, kc * 1.4);
                int i0 = (int)Math.Max(0, (cx - wdt - x0) / kx), i1 = (int)Math.Min(W - 1, (cx + wdt - x0) / kx);
                for (int i = i0; i <= i1; i++)
                {
                    double wx = x0 + i * kx;
                    double edge = Math.Min(1, Math.Min(wx - x0, x0 + width - wx) / (width * 0.08));
                    double v = amp * Math.Pow(Math.Max(0, 1 - Math.Abs(wx - cx) / wdt), 0.6) * edge;
                    if (v > spire[i]) spire[i] = (float)v;
                }
            }
            for (int i = 0; i < W; i++) crest[i] += spire[i];
        }

        public double crestAt(double wx)
        {
            int i = (int)Math.Max(0, Math.Min(W - 1, Js.round((wx - x0) / kx)));
            return crest[i];
        }

        /// <summary>paint some more rows; returns true once the whole thing is done</summary>
        public bool step(double budgetMs)
        {
            if (ready) return true;
            double t0 = Js.now();
            if (!computed)
            {
                if (pending) return false;
                // pass 1: the height field (rows 0..H-1), then pass 2: shading (H..2H-1)
                while (row < H * 2 && Js.now() - t0 < budgetMs)
                {
                    if (row < H) heightRow(row);
                    else shadeRow(row - H);
                    row++;
                }
                if (row >= H * 2)
                {
                    traceRiver();
                    computed = true;
                }
                return false;
            }
            // the finishing, one heavy step a frame
            if (stage == 0 || stage == 1)
            {
                var cv = new Surface(W, H);
                byte[] img = stage == 0 ? imgA : imgB;
                var c = cv.GetContext();
                // soften the single-pixel speckle the noise leaves; distance blurs anyway
                var tmp = new Surface(W, H);
                tmp.PutPixels(img, 0, 0, W, H);
                c.Filter = "blur(0.4px)";
                c.DrawImage(tmp, 0, 0);
                c.Filter = "none";
                if (stage == 0) a = cv;
                else b = cv;
            }
            else
            {
                paintRiver(a);
                paintRiver(b);
                ready = true;
                hf = new float[0];
                imgA = imgB = null;
            }
            stage++;
            return ready;
        }

        // ---------------------------------------------------------------- the ridges
        /*
         * The mountain used to be noise all over at every scale, and at a distance it
         * read as crumpled foil. A mountain is built of ridges: arêtes run down from
         * each peak and branch, and between them lie broad faces, one turned to the
         * sun and one away. The ridges are laid out here as lines; a point's depth
         * toward the viewer is how near it is to a ridge (and how proud that ridge
         * stands), so the faces between come out as planes with a crest at the top.
         * Fine fall-line texture and gullies are laid over that.
         */
        private readonly List<double> rAx = new List<double>(), rAy = new List<double>(), rBx = new List<double>(), rBy = new List<double>(), rHa = new List<double>(), rHb = new List<double>();
        private int ridgeId = 0;
        /// <summary>the ridge field on a coarse grid (it is smooth), and the warp that bends the ridges</summary>
        private float[] ridgeGrid = null;
        private float[] warpGrid = null;
        private int gW, gH;
        private const int GC = 6;
        private const double RIDGE_PROM = 0.22;

        private double h01(double a, double b) { return MountainGen.hash(a, b, spec.seed + 101); }

        private void buildRidges()
        {
            double height = spec.height;
            double[] px = peakX, pk = peakK;
            for (int p = 0; p < px.Length; p++)
            {
                double py = crestAt(px[p]);
                int n = spec.range ? (pk[p] > 0.8 ? 4 : 3) : pk[p] > 0.9 ? 5 : 4;
                for (int j = 0; j < n; j++)
                {
                    double ang = -1.25 + 2.5 * (j + 0.5) / n + (h01(j, pk[p] * 10) - 0.5) * 0.35;
                    growRidge(px[p], py, ang, height * pk[p] * 1.25, pk[p], 0);
                }
            }
        }

        private void growRidge(double cx, double cy, double ang, double length, double prom, int depth)
        {
            ridgeId++;
            const int steps = 7;
            double L = length / steps;
            for (int i = 0; i < steps; i++)
            {
                double ang2 = ang + (h01(ridgeId, i) - 0.5) * 0.5;
                double nx = cx + Math.Sin(ang2) * L;
                double ny = cy - Math.Cos(ang2) * L * 0.55;
                if (ny < 0) break;
                rAx.Add(cx); rAy.Add(cy); rBx.Add(nx); rBy.Add(ny);
                rHa.Add(prom * (1 - (double)i / steps * 0.7));
                rHb.Add(prom * (1 - (double)(i + 1) / steps * 0.7));
                if (depth < 2 && (i == 2 || i == 4) && h01(ridgeId, i + 50) < 0.7)
                {
                    double side = h01(ridgeId, i + 90) < 0.5 ? 1 : -1;
                    growRidge(nx, ny, ang2 + side * (0.55 + h01(ridgeId, i + 7) * 0.4),
                        length * (0.45 - 0.1 * depth) * (1 - (double)i / steps * 0.5), prom * 0.6, depth + 1);
                }
                cx = nx;
                cy = ny;
            }
        }

        /// <summary>the ridges' field on the coarse grid: done once, before the first row</summary>
        private void buildRidgeGrid()
        {
            double width = spec.width, height = spec.height, seed = spec.seed;
            gW = W / GC + 2;
            gH = H / GC + 2;
            ridgeGrid = new float[gW * gH];
            warpGrid = new float[gW * gH];
            int n = rAx.Count;
            // how far a ridge's flanks reach before they fall below everything else
            double reachX = height * RIDGE_PROM / 0.9;
            double[] minX = new double[n], maxX = new double[n], minY = new double[n], maxY = new double[n];
            for (int s = 0; s < n; s++)
            {
                double r = reachX * Math.Max(rHa[s], rHb[s]);
                minX[s] = Math.Min(rAx[s], rBx[s]) - r;
                maxX[s] = Math.Max(rAx[s], rBx[s]) + r;
                minY[s] = Math.Min(rAy[s], rBy[s]) - r / 0.6;
                maxY[s] = Math.Max(rAy[s], rBy[s]) + r / 0.6;
            }
            for (int gy = 0; gy < gH; gy++)
            {
                double wy = top - gy * GC * ky;
                for (int gx = 0; gx < gW; gx++)
                {
                    double wx = x0 + gx * GC * kx;
                    double warp = (MountainGen.fbm(wx / (width * 0.03), wy / (height * 0.05), seed + 41, 3) - 0.5) * width * 0.025;
                    double qx = wx + warp;
                    double best = -height;
                    for (int s = 0; s < n; s++)
                    {
                        if (qx < minX[s] || qx > maxX[s] || wy < minY[s] || wy > maxY[s]) continue;
                        double ax = rAx[s], ay = rAy[s];
                        double vx = rBx[s] - ax, vy = rBy[s] - ay;
                        double t = ((qx - ax) * vx + (wy - ay) * vy) / (vx * vx + vy * vy + 1e-9);
                        t = t < 0 ? 0 : t > 1 ? 1 : t;
                        double dx = qx - (ax + vx * t), dy = (wy - (ay + vy * t)) * 0.6;
                        double v = (rHa[s] + (rHb[s] - rHa[s]) * t) * height * RIDGE_PROM - Math.Sqrt(dx * dx + dy * dy) * 0.9;
                        if (v > best) best = v;
                    }
                    ridgeGrid[gy * gW + gx] = (float)best;
                    warpGrid[gy * gW + gx] = (float)warp;
                }
            }
        }

        private static double bilerp(float[] g, int gw, double fx, double fy)
        {
            int ix = (int)fx, iy = (int)fy;
            double tx = fx - ix, ty = fy - iy;
            int o = iy * gw + ix;
            double a = g[o], b = g[o + 1], c = g[o + gw], d = g[o + gw + 1];
            return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
        }

        /// <summary>depth toward the viewer: the ridges, then gullies and fall-line texture on them</summary>
        private void heightRow(int py)
        {
            if (ridgeGrid == null) buildRidgeGrid();
            double width = spec.width, height = spec.height, seed = spec.seed;
            double wy = top - py * ky;
            double fy = (double)py / GC;
            for (int px = 0; px < W; px++)
            {
                double d = crest[px] - wy;
                // a margin above the crest keeps the slopes right up to the soft edge
                if (d < -3 * ky)
                {
                    hf[py * W + px] = 0;
                    continue;
                }
                double wx = x0 + px * kx;
                double fx = (double)px / GC;
                double ridge = bilerp(ridgeGrid, gW, fx, fy);
                double warp = bilerp(warpGrid, gW, fx, fy);
                // low down the long fall-line grooves soften: meadow and wood lie over the rock
                double low = 0.35 + 0.65 * Math.Max(0, Math.Min(1, (wy / height - 0.25) / 0.2));
                double tex =
                    (MountainGen.ridged(wx / (width * 0.012) + warp / (width * 0.012) * 0.5, wy / (height * 0.07), seed + 11, 3) - 0.35) * height * 0.007 +
                    (MountainGen.ridged(wx / (width * 0.035) + warp / (width * 0.03), wy / (height * 0.16), seed + 13, 2) - 0.3) * height * 0.025 * low;
                // the walls: towers split by deep clefts, up high
                double up = Math.Max(0, Math.Min(1, (wy / height - 0.3) / 0.2));
                if (up > 0)
                    tex += up * (MountainGen.ridged(wx / (width * 0.022) + warp / (width * 0.02), wy / (height * 0.5), seed + 73, 2, 1.0) - 0.3) * height * 0.018;
                hf[py * W + px] = (float)(ridge + tex);
            }
        }

        // the sun from the upper left or the upper right, low enough to rake across the ridges
        static readonly double[] L1 = norm3(-0.8, 0.35, 0.5);
        static readonly double[] L2 = norm3(0.8, 0.35, 0.5);

        static double[] norm3(double x, double y, double z)
        {
            double m = Math.Sqrt(x * x + y * y + z * z);
            return new[] { x / m, y / m, z / m };
        }

        static double clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

        private void shadeRow(int py)
        {
            if (imgA == null)
            {
                imgA = new byte[W * H * 4];
                imgB = new byte[W * H * 4];
            }
            byte[] A = imgA;
            byte[] B = imgB;
            double width = spec.width, height = spec.height, seed = spec.seed;
            RGB rock = spec.rock;
            double wy = top - py * ky;
            double alt = wy / height;
            float[] hf = this.hf;
            // the hollows are judged a few pixels wide (a single pixel's curvature is only noise)
            const int G = 4;
            // cast shadows: along the row, a ridge hides what lies behind it from a low sun
            // (scanned from the sun's side; the sky above the crest casts nothing)
            if (shadeA == null || shadeA.Length != W) { shadeA = new float[W]; shadeB = new float[W]; }
            double fall = kx * SHADOW_TAN, soft = height * 0.004;
            double m = double.NegativeInfinity;
            for (int px = 0; px < W; px++)
            {
                double h = hf[py * W + px];
                m -= fall;
                if (crest[px] - wy >= 0 && h > m) m = h;
                shadeA[px] = (float)(crest[px] - wy >= 0 ? clamp01((m - h) / soft) : 0);
            }
            m = double.NegativeInfinity;
            for (int px = W - 1; px >= 0; px--)
            {
                double h = hf[py * W + px];
                m -= fall;
                if (crest[px] - wy >= 0 && h > m) m = h;
                shadeB[px] = (float)(crest[px] - wy >= 0 ? clamp01((m - h) / soft) : 0);
            }
            for (int px = 0; px < W; px++)
            {
                int o = (py * W + px) * 4;
                double d = crest[px] - wy;
                if (d < 0)
                {
                    A[o + 3] = B[o + 3] = 0;
                    continue;
                }
                int i = py * W + px;
                double h0 = hf[i];
                double hl = hf[px > 0 ? i - 1 : i];
                double hr = hf[px < W - 1 ? i + 1 : i];
                double hu = hf[py > 0 ? i - W : i];
                double hd = hf[py < H - 1 ? i + W : i];
                // normal: screen x right, y up, z toward the viewer
                double nx = -(hr - hl) / (2 * kx);
                double ny = -(hu - hd) / (2 * ky);
                double wx = x0 + px * kx;
                // the rock breaks into flat faces, each tilted its own way: angular light, not noise
                double up = 0.7 + 0.3 * clamp01((alt - 0.25) / 0.2);
                {
                    double warp = bilerp(warpGrid, gW, (double)px / GC, (double)py / GC);
                    facet(wx / (width * 0.022) + warp / (width * 0.03), wy / (height * 0.05), seed + 91, out double a1, out double b1);
                    facet(wx / (width * 0.005), wy / (height * 0.013), seed + 93, out double a2, out double b2);
                    nx += (a1 * 0.8 + a2 * 0.35) * up;
                    ny += (b1 * 0.5 + b2 * 0.22) * up;
                }
                double nz = 1;
                double nm = Math.Sqrt(nx * nx + ny * ny + 1);
                nx /= nm;
                ny /= nm;
                nz /= nm;
                // concavity: gullies hold shadow and snow, ribs shed them
                double hl4 = hf[px >= G ? i - G : i], hr4 = hf[px < W - G ? i + G : i];
                double hu4 = hf[py >= G ? i - G * W : i], hd4 = hf[py < H - G ? i + G * W : i];
                double hxx = (hl4 + hr4 - 2 * h0) / (G * kx * G * kx);
                double hyy = (hu4 + hd4 - 2 * h0) / (G * ky * G * ky);
                double gully = Math.Max(-1, Math.Min(1, (hxx + 4 * hyy) * 0.52));
                double n1 = MountainGen.fbm(wx * 0.006, wy * 0.012, seed + 5, 4);
                double n2 = MountainGen.vnoise(wx * 0.12, wy * 0.2, seed + 9);
                double n3 = MountainGen.fbm(wx / (width * 0.05), wy / (height * 0.04), seed + 61, 4);
                double steep = 1 - nz;
                // ---- the ground: rock in strata, scrub and forest low down, snow on top
                // bedding: darker bands of harder rock, and the colour drifting across the face
                double band = Math.Sin(wy * 0.11 + n1 * 3) * 0.5 + 0.5;
                double strata = band * band * band * -0.12 + Math.Sin(wy * 0.045 + n1 * 5 + wx * 0.002) * 0.05 + (n1 - 0.5) * 0.22 + (n2 - 0.5) * 0.06;
                double r = rock[0] * (1 + strata);
                double g = rock[1] * (1 + strata * 0.95);
                double b = rock[2] * (1 + strata * 0.9);
                // the rock's own texture: dark cracks and joints, and paler debris streaked down the faces
                double crackN = MountainGen.ridged(wx / (width * 0.0035) + n3 * 3, wy / (height * 0.009), seed + 201, 2);
                double crack = clamp01((crackN - 0.55) / 0.3);
                double streakN = MountainGen.fbm(wx / (width * 0.006), wy / (height * 0.12), seed + 211, 3);
                double streak = clamp01((streakN - 0.56) / 0.16) * clamp01((steep - 0.25) / 0.3);
                double tex = (1 - crack * 0.32) * (1 + streak * 0.18);
                r *= tex; g *= tex; b *= tex;
                double forest = clamp01((0.4 - alt + (n1 - 0.5) * 0.5 + (n3 - 0.5) * 0.3 + gully * 0.2) / 0.06);
                // meadow in clean patches on the gentler ground, the rock breaking through where it steepens
                double scrub = clamp01((0.56 - alt + (n1 - 0.5) * 0.3 + (n3 - 0.5) * 0.7 + gully * 0.15 - (steep - 0.3) * 0.6) / 0.03) * (1 - forest);
                // scree: pale fans at the foot of the walls, on the gentler ground
                double scree = clamp01((0.42 - steep) / 0.18) * clamp01((0.62 - alt) / 0.15) * clamp01((alt - 0.2) / 0.1);
                if (scree > 0)
                {
                    double k = scree * 0.55;
                    r += (196 - r) * k;
                    g += (192 - g) * k;
                    b += (186 - b) * k;
                }
                // meadow and scrub, holding on where the ground is not too steep
                if (scrub > 0)
                {
                    double k = scrub * (0.85 + n2 * 0.15);
                    double gr = 0.92 + (n3 - 0.5) * 0.3;
                    r += (98 * gr - r) * k;
                    g += (112 * gr - g) * k;
                    b += (72 * gr - b) * k;
                }
                if (forest > 0)
                {
                    // the wood as a stipple of crowns, dark between them
                    double crown = 1 - clamp01((cellDist(wx / 3, wy / 4.5, seed + 97) - 0.25) / 0.35);
                    double k = forest * (0.9 + 0.1 * crown);
                    double tone = 0.9 + 0.12 * crown + (n3 - 0.5) * 0.35;
                    r += (46 * tone - r) * k;
                    g += (62 * tone - g) * k;
                    b += (44 * tone - b) * k;
                }
                // snow: down the gullies in ragged tongues, off the steepest faces
                double sl = snowLine - gully * 0.3 - (n1 - 0.5) * 0.25 - (n3 - 0.5) * 0.35;
                double snow = clamp01((alt - sl) / 0.05);
                if (snow > 0)
                {
                    double k = snow * (1 - clamp01((steep - 0.55) / 0.2) * 0.85);
                    r += (240 - r) * k;
                    g += (244 - g) * k;
                    b += (250 - b) * k;
                }
                double ao = Math.Max(0.6, Math.Min(1, 1 + gully * 0.3));
                // ---- light: the blue of the sky in the shadows, the warm sun on the faces turned to it
                double sky = (0.34 + 0.2 * ny) * ao;
                double d1 = clamp01((nx * L1[0] + ny * L1[1] + nz * L1[2] + 0.05) / 1.05) * (1 - shadeA[px] * 0.9);
                double d2 = clamp01((nx * L2[0] + ny * L2[1] + nz * L2[2] + 0.05) / 1.05) * (1 - shadeB[px] * 0.9);
                byte al = MountainGen.U8(Math.Min(255, (d / (ky * 1.5)) * 255));
                put(A, o, d1, r, g, b, sky, al);
                put(B, o, d2, r, g, b, sky, al);
            }
        }

        private float[] shadeA, shadeB;
        private const double SHADOW_TAN = 0.75;

        /// <summary>the tilt of the rock face (a cell of a jittered grid) that a point falls in</summary>
        private static void facet(double fx, double fy, double sd, out double gx, out double gy)
        {
            double ix = Math.Floor(fx), iy = Math.Floor(fy);
            double best = double.MaxValue;
            gx = gy = 0;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    double cx = ix + ox, cy = iy + oy;
                    double dx = fx - (cx + MountainGen.hash(cx, cy, sd));
                    double dy = fy - (cy + MountainGen.hash(cx, cy, sd + 1));
                    double dd = dx * dx + dy * dy;
                    if (dd < best)
                    {
                        best = dd;
                        gx = MountainGen.hash(cx, cy, sd + 2) * 2 - 1;
                        gy = MountainGen.hash(cx, cy, sd + 3) * 2 - 1;
                    }
                }
        }

        /// <summary>distance to the nearest point of a jittered grid (cell units)</summary>
        private static double cellDist(double fx, double fy, double sd)
        {
            double ix = Math.Floor(fx), iy = Math.Floor(fy);
            double best = double.MaxValue;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    double cx = ix + ox, cy = iy + oy;
                    double dx = fx - (cx + MountainGen.hash(cx, cy, sd));
                    double dy = fy - (cy + MountainGen.hash(cx, cy, sd + 1));
                    double dd = dx * dx + dy * dy;
                    if (dd < best) best = dd;
                }
            return Math.Sqrt(best);
        }

        private static void put(byte[] D, int o, double sun, double r, double g, double b, double sky, byte alpha)
        {
            // the sun warm on the rock, the shadows cool with the blue of the sky; then a little
            // of the colour taken out, as the distance does
            double R = r * (sky * 0.52 + sun * 1.26);
            double G = g * (sky * 0.6 + sun * 1.02);
            double B = b * (sky * 0.86 + sun * 0.8);
            double L = R * 0.3 + G * 0.59 + B * 0.11;
            const double SAT = 0.86;
            D[o] = MountainGen.U8(Math.Min(255, L + (R - L) * SAT));
            D[o + 1] = MountainGen.U8(Math.Min(255, L + (G - L) * SAT));
            D[o + 2] = MountainGen.U8(Math.Min(255, L + (B - L) * SAT));
            D[o + 3] = alpha;
        }

        /// <summary>the river: out of the snow, down the bottoms of the gullies</summary>
        private void traceRiver()
        {
            double width = spec.width, height = spec.height, seed = spec.seed;
            double startX = spec.x - width * 0.16 + width * (0.1 + MountainGen.hash(seed, 3, 7) * 0.06);
            double wx = startX;
            double wy = Math.Min(crestAt(wx) - height * 0.2, height * (snowLine + 0.02));
            var pts = new List<(double x, double y)> { (wx, wy) };
            double step = height * 0.012;
            int guard = 0;
            while (wy > height * 0.02 && guard++ < 400)
            {
                wy -= step;
                // find the lowest ground (deepest into the mountain) a little to either side
                double best = wx;
                double bestH = double.PositiveInfinity;
                for (int k = -4; k <= 4; k++)
                {
                    double cx = wx + k * width * 0.004;
                    double px = Js.round((cx - x0) / kx);
                    double py = Js.round((top - wy) / ky);
                    if (px < 1 || px >= W - 1 || py < 0 || py >= H) continue;
                    double hgt = hf[(int)(py * W + px)] + Math.Abs(k) * 3;
                    if (hgt < bestH)
                    {
                        bestH = hgt;
                        best = cx;
                    }
                }
                double mv = Math.Max(-width * 0.004, Math.Min(width * 0.004, (best - wx) * 0.5));
                wx += mv + (MountainGen.fbm(wy * 0.012, 3.3, seed + 77, 3) - 0.5) * width * 0.012;
                if (wy > crestAt(wx) - 4) break;
                pts.Add((wx, wy));
            }
            // round off the corners: two passes of corner-cutting
            var q = pts;
            for (int it = 0; it < 3; it++)
            {
                var nq = new List<(double x, double y)> { q[0] };
                for (int i = 0; i < q.Count - 1; i++)
                {
                    double ax = q[i].x, ay = q[i].y;
                    double bx = q[i + 1].x, by = q[i + 1].y;
                    nq.Add((ax * 0.75 + bx * 0.25, ay * 0.75 + by * 0.25));
                    nq.Add((ax * 0.25 + bx * 0.75, ay * 0.25 + by * 0.75));
                }
                nq.Add(q[q.Count - 1]);
                q = nq;
            }
            river = q;
        }

        private void paintRiver(Surface cv)
        {
            var c = cv.GetContext();
            if (river.Count < 2) return;
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            // the cut it runs in, dark, then the water, widening as it goes down
            int n = river.Count;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 1; i < n; i++)
                {
                    double k = (double)i / n;
                    c.StrokeStyle = pass != 0
                        ? (Style)Col.Rgba(196, 214, 228, Css.fixedNum(0.55 + k * 0.35, 3))
                        : (Style)"rgba(24, 28, 26, 0.45)";
                    c.LineWidth = (pass != 0 ? 1.6 + k * 2.6 : 3.6 + k * 4) / kx;
                    c.BeginPath();
                    var p0 = river[i - 1];
                    var p1 = river[i];
                    c.MoveTo((p0.x - x0) / kx, (top - p0.y) / ky);
                    c.LineTo((p1.x - x0) / kx, (top - p1.y) / ky);
                    c.Stroke();
                }
            }
        }
    }
}
