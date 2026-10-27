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
        internal static double ridged(double x, double y, double s, int oct)
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
                w = Math.Min(1, n * 1.6);
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
            snowLine = spec.snow ? 0.64 : 0.8;
            hf = new float[W * H];
            crest = new float[W];
            crestSoft = new float[W];
            buildCrest();
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
        private void buildCrest()
        {
            double x = spec.x, height = spec.height, width = spec.width, seed = spec.seed;
            double baseY = height * 0.12;
            double p1x = x - width * 0.16;
            double p2x = x + width * 0.24;
            for (int i = 0; i < W; i++)
            {
                double wx = x0 + i * kx;
                // two cones, their union, with concave flanks
                double c1 = 1 - Math.Abs(wx - p1x) / (width * (wx < p1x ? 0.36 : 0.46));
                double c2 = 0.68 * (1 - Math.Abs(wx - p2x) / (width * 0.3));
                double c3 = 0.42 * (1 - Math.Abs(wx - (x + width * 0.4)) / (width * 0.16));
                double k = Math.Max(Math.Max(0, c1), Math.Max(c2, c3));
                // concave flanks: steep near the top, easing out toward the foot
                k = Math.Pow(k, 1.55);
                // crags: large slow bumps and small sharp ones, more of them high up
                double big = (MountainGen.fbm(wx / (width * 0.09), 0.5, seed, 4) - 0.5) * 0.22;
                double sharp = (MountainGen.ridged(wx / (width * 0.02), 1.5, seed + 3, 4) - 0.3) * 0.06;
                double edge = Math.Min(1, Math.Min(wx - x0, x0 + width - wx) / (width * 0.08));
                crest[i] = (float)(baseY * edge + (k + (big + sharp) * (0.3 + k)) * height * edge);
            }
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
                c.Filter = "blur(0.7px)";
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

        /// <summary>depth toward the viewer at a world point: ridges stand out, gullies sink</summary>
        private void heightRow(int py)
        {
            double width = spec.width, height = spec.height, seed = spec.seed;
            double wy = top - py * ky;
            double spine = spec.x - width * 0.16;
            for (int px = 0; px < W; px++)
            {
                double wx = x0 + px * kx;
                double d = crest[px] - wy;
                if (d < -2)
                {
                    hf[py * W + px] = 0;
                    continue;
                }
                // the whole mass bulges toward you along its spine, and turns away at its edges
                double ds = Math.Max(0, crestSoft[px] - wy);
                double macro = -Math.Abs(wx - spine) * 0.35 + Math.Sqrt(Math.Min(ds, height * 0.2)) * 9;
                // gullies run down the fall line: noise stretched tall
                // the fall lines wander: warp the noise so gullies bend and join
                double gx = wx / (width * 0.04);
                double gy = wy / (height * 0.13);
                gx += (MountainGen.fbm(gx * 0.35, gy * 0.35, seed + 41, 3) - 0.5) * 3.2;
                double ribs = MountainGen.ridged(gx, gy, seed + 11, 4) * height * 0.075;
                double detail =
                    (MountainGen.fbm(wx / (width * 0.012), wy / (height * 0.03), seed + 29, 3) - 0.5) * height * 0.014;
                hf[py * W + px] = (float)(macro + ribs + detail);
            }
        }

        // the lights: from the upper left and the upper right, toward the viewer
        static readonly double[] L1 = { -0.62, 0.5, 0.6 };
        static readonly double[] L2 = { 0.62, 0.5, 0.6 };

        private void shadeRow(int py)
        {
            if (imgA == null)
            {
                imgA = new byte[W * H * 4];
                imgB = new byte[W * H * 4];
            }
            byte[] A = imgA;
            byte[] B = imgB;
            double height = spec.height, seed = spec.seed;
            RGB rock = spec.rock;
            double wy = top - py * ky;
            double alt = wy / height;
            float[] hf = this.hf;
            for (int px = 0; px < W; px++)
            {
                int o = (py * W + px) * 4;
                double crestH = crest[px];
                double d = crestH - wy;
                if (d < 0)
                {
                    A[o + 3] = B[o + 3] = 0;
                    continue;
                }
                int i = py * W + px;
                double hl = hf[px > 0 ? i - 1 : i];
                double hr = hf[px < W - 1 ? i + 1 : i];
                double hu = hf[py > 0 ? i - W : i];
                double hd = hf[py < H - 1 ? i + W : i];
                // normal: screen x right, y up, z toward the viewer
                double nx = -(hr - hl) / (2 * kx);
                double ny = -(hu - hd) / (2 * ky);
                double nz = 1;
                double nm = Js.hypot(nx, ny, nz);
                nx /= nm;
                ny /= nm;
                nz /= nm;
                double wx = x0 + px * kx;
                // concavity: gullies hold shadow and snow
                double lap = (hl + hr + hu + hd - 4 * (double)hf[i]) / (kx * 4);
                double gully = Math.Max(-1, Math.Min(1, lap * 0.6));
                double n1 = MountainGen.fbm(wx * 0.006, wy * 0.012, seed + 5, 4);
                double n2 = MountainGen.vnoise(wx * 0.12, wy * 0.2, seed + 9);
                // ---- the ground: forest low, scrub above, rock in strata, snow on top
                double r, g, b;
                double strata = Math.Sin(wy * 0.045 + n1 * 5) * 0.08 + (n2 - 0.5) * 0.12;
                r = rock[0] * (1 + strata);
                g = rock[1] * (1 + strata);
                b = rock[2] * (1 + strata);
                // the trees climb the gullies, where there is water, and fall back off the ribs
                double forest = Math.Max(
                    0,
                    Math.Min(1, (0.42 - alt + (n1 - 0.5) * 0.6 + gully * 0.14) / 0.05));
                double scrub =
                    Math.Max(0, Math.Min(1, (0.6 - alt + (n1 - 0.5) * 0.6 + gully * 0.1) / 0.08)) *
                    (1 - forest);
                if (scrub > 0)
                {
                    double k = scrub * (0.55 + n2 * 0.35);
                    r += (96 - r) * k;
                    g += (98 - g) * k;
                    b += (62 - b) * k;
                }
                if (forest > 0)
                {
                    // trees as a stipple of dark crowns over lighter ground
                    double tree = 0.6 + 0.4 * Math.Max(0, Math.Min(1, (n2 - 0.3) / 0.4));
                    double k = forest * tree;
                    r += (34 - r) * k;
                    g += (54 - g) * k;
                    b += (34 - b) * k;
                }
                // snow: down the gullies, off the steepest faces
                double steep = 1 - nz;
                double sl = snowLine - gully * 0.14 - (n1 - 0.5) * 0.3 + steep * 0.16;
                double snow = Math.Max(0, Math.Min(1, (alt - sl) / 0.07));
                if (snow > 0)
                {
                    double st2 = Math.Max(0, Math.Min(1, (steep - 0.4) / 0.3));
                    double k = snow * (1 - st2 * st2 * (3 - 2 * st2) * 0.5);
                    r += (238 - r) * k;
                    g += (242 - g) * k;
                    b += (248 - b) * k;
                }
                // ambient occlusion in the gullies, and at the foot of the mass
                double ao =
                    Math.Max(0.55, Math.Min(1, 1 + gully * 0.35)) * (0.82 + 0.18 * Math.Min(1, alt * 2 + 0.2));
                // ---- light: blue sky from above, the sun from one side or the other
                double sky = (0.56 + 0.2 * ny) * ao;
                double d1 = Math.Max(0, nx * L1[0] + ny * L1[1] + nz * L1[2]);
                double d2 = Math.Max(0, nx * L2[0] + ny * L2[1] + nz * L2[2]);
                put(A, o, d1, r, g, b, sky, ao, d);
                put(B, o, d2, r, g, b, sky, ao, d);
            }
        }

        private void put(byte[] D, int o, double dif, double r, double g, double b, double sky, double ao, double d)
        {
            double sun = dif * 0.78 * (0.75 + 0.25 * ao);
            D[o] = MountainGen.U8(Math.Min(255, r * (sky * 0.82 + sun * 1.08)));
            D[o + 1] = MountainGen.U8(Math.Min(255, g * (sky * 0.9 + sun * 1.0)));
            D[o + 2] = MountainGen.U8(Math.Min(255, b * (sky * 1.12 + sun * 0.86)));
            // a soft edge against the sky
            D[o + 3] = MountainGen.U8(Math.Min(255, (d / (ky * 1.5)) * 255));
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
