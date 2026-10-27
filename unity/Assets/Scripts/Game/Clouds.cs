using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /*
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

    /// <summary>CloudKind = "cumulus" | "strato" (kept as strings).</summary>
    public static class CloudKind
    {
        public const string cumulus = "cumulus";
        public const string strato = "strato";
    }

    /// <summary>the volume and its light, with no canvas: this part can run in a worker</summary>
    public class CloudData
    {
        public int w;
        public int h;
        /// <summary>0-1 opacity of the volume</summary>
        public float[] dens;
        /// <summary>light reaching each point from above, and from below</summary>
        public float[] top;
        public float[] bot;
    }

    public class CloudShape : CloudData
    {
        /// <summary>the tinted picture, and the light it was tinted for</summary>
        public Surface canvas;
        public string key;
        /// <summary>the picture it had before the last retint, cross-faded out over a second</summary>
        public Surface prev;
        public Surface spare;
        /// <summary>undefined in the TS until the first picture → null</summary>
        public double? swapAt;
    }

    public class CloudLight
    {
        /// <summary>0 by night and at golden hour, 1 under a high sun</summary>
        public double day;
        /// <summary>how much this cloud faces the low sun (0-1)</summary>
        public double lit;
        public RGB crown;
        public double crownA;
        public RGB mid;
        public RGB belly;
        public double bellyA;
    }

    public static class Clouds
    {
        /// <summary>put pixels coloured elsewhere (a worker) onto the cloud, fading from the old</summary>
        public static void applyCloudPixels(CloudShape c, byte[] px, string key, double now)
        {
            if (!string.IsNullOrEmpty(c.key))
            {
                var next = c.spare ?? new Surface(c.w, c.h, true, "cloud");
                if (next.width != c.w || next.height != c.h)
                {
                    next.SetSize(c.w, c.h);
                }
                c.spare = c.prev;
                c.prev = c.canvas;
                c.canvas = next;
                c.swapAt = now;
            }
            else
            {
                // the first picture fades in from nothing
                c.prev = null;
                c.swapAt = now;
            }
            c.canvas.PutPixels(px, 0, 0, c.w, c.h);
            c.key = key;
        }

        /// <summary>
        /// Retint into a fresh canvas and keep the old picture to fade from, so a
        /// cloud's colour drifts with the hour instead of stepping.
        /// </summary>
        public static void retintCloud(CloudShape c, CloudLight L, double now)
        {
            if (!string.IsNullOrEmpty(c.key))
            {
                var next = c.spare ?? new Surface(c.w, c.h, true, "cloud");
                if (next.width != c.w || next.height != c.h)
                {
                    next.SetSize(c.w, c.h);
                }
                c.spare = c.prev;
                c.prev = c.canvas;
                c.canvas = next;
                c.swapAt = now;
            }
            tintCloud(c, L);
        }

        static double hash2(double x, double y, double s)
        {
            uint h = unchecked((uint)Js.ToInt32(x * 374761393 + y * 668265263 + s * 2246822519));
            h = unchecked((h ^ (h >> 13)) * 1274126177u);
            h ^= h >> 16;
            return h / 4294967295.0;
        }

        static double vnoise(double x, double y, double s)
        {
            double xi = Math.Floor(x);
            double yi = Math.Floor(y);
            double xf = x - xi;
            double yf = y - yi;
            double u = xf * xf * (3 - 2 * xf);
            double v = yf * yf * (3 - 2 * yf);
            double a = hash2(xi, yi, s);
            double b = hash2(xi + 1, yi, s);
            double c = hash2(xi, yi + 1, s);
            double d = hash2(xi + 1, yi + 1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        static double fbm(double x, double y, double s, int oct)
        {
            double sum = 0;
            double amp = 0.5;
            double norm = 0;
            for (int i = 0; i < oct; i++)
            {
                sum += vnoise(x, y, s + i * 17) * amp;
                norm += amp;
                amp *= 0.5;
                x *= 2.03;
                y *= 2.03;
            }
            return sum / norm;
        }

        static double smooth(double a, double b, double x)
        {
            double t = Math.Max(0, Math.Min(1, (x - a) / (b - a)));
            return t * t * (3 - 2 * t);
        }

        /// <summary>a seeded 0-1 sequence</summary>
        static Func<double> rng(double seed)
        {
            double s = (seed * 9301 + 49297) % 233280;
            return () =>
            {
                s = (s * 9301 + 49297) % 233280;
                return s / 233280;
            };
        }

        /// <summary>build one cloud's volume, `w` pixels wide</summary>
        public static CloudShape buildCloud(double seed, string kind, double w)
        {
            var d = buildCloudData(seed, kind, w);
            var canvas = new Surface(d.w, d.h, true, "cloud");
            return new CloudShape { w = d.w, h = d.h, dens = d.dens, top = d.top, bot = d.bot, canvas = canvas, key = "" };
        }

        struct Blob { public double x, y, rx, ry, k; }

        public static CloudData buildCloudData(double seed, string kind, double wIn)
        {
            var r = rng(seed * 7 + 3);
            bool cumulus = kind == "cumulus";
            double aspect = cumulus ? 0.56 : 0.3;
            int h = (int)Math.Max(8, Js.round(wIn * aspect));
            int w = (int)Math.Max(16, Js.round(wIn));

            // the blobs, measured in widths: X runs 0-1 across, Y 0-aspect down
            var blobs = new List<Blob>();
            double baseY = aspect * 0.86;
            if (cumulus)
            {
                // a broad foot along the flat base, then towers heaped up from the middle
                double foot = 4 + Math.Floor(r() * 3);
                for (int i = 0; i < foot; i++)
                {
                    double x = 0.24 + (i / (foot - 1)) * 0.52 + (r() - 0.5) * 0.05;
                    double rad = 0.08 + r() * 0.05;
                    blobs.Add(new Blob { x = x, y = baseY - rad * 0.45, rx = rad * 1.2, ry = rad, k = 1 });
                }
                double towers = 3 + Math.Floor(r() * 4);
                for (int i = 0; i < towers; i++)
                {
                    double x = 0.28 + r() * 0.44;
                    double centre = 1 - Math.Abs(x - 0.5) * 2;
                    double rad = 0.08 + r() * 0.06 + centre * 0.04;
                    double y = baseY - 0.1 - centre * (0.06 + r() * 0.14) - r() * 0.04;
                    y = Math.Max(y, rad + 0.05);
                    blobs.Add(new Blob { x = x, y = y, rx = rad, ry = rad, k = 0.9 + r() * 0.15 });
                }
                // cauliflower: small domes heaped over the upper surface of every puff
                var big = blobs.ToArray();
                double bubbles = 22 + Math.Floor(r() * 10);
                for (int i = 0; i < bubbles; i++)
                {
                    var pb = big[(int)Math.Floor(r() * big.Length)];
                    double ang = -Math.PI * (0.12 + r() * 0.76);
                    double rad = pb.rx * (0.22 + r() * 0.22);
                    double x = pb.x + Math.Cos(ang) * pb.rx * 0.95;
                    double y = Math.Max(rad + 0.03, pb.y + Math.Sin(ang) * pb.ry * 0.95);
                    blobs.Add(new Blob { x = x, y = y, rx = rad, ry = rad, k = 0.75 });
                }
            }
            else
            {
                // long sheets of altocumulus: many flat, overlapping lenses
                double nb = 8 + Math.Floor(r() * 6);
                for (int i = 0; i < nb; i++)
                {
                    double x = 0.12 + r() * 0.76;
                    double rx = 0.05 + r() * 0.09;
                    double y = aspect * (0.35 + r() * 0.3);
                    double ry = rx * (0.35 + r() * 0.25);
                    blobs.Add(new Blob { x = x, y = y, rx = rx, ry = ry, k = 0.6 + r() * 0.4 });
                }
            }
            var B = blobs.ToArray();

            int n = w * h;
            var dens = new float[n];
            // the unclamped field: read as a height map, its slopes are the billows' faces
            var hgt = new float[n];
            double freq = cumulus ? 9 : 12;
            double ns = seed * 13 + 1;
            double crisp = cumulus ? 0.2 : 0.3;
            for (int j = 0; j < h; j++)
            {
                double v = (double)j / h;
                double Y = v * aspect;
                for (int i = 0; i < w; i++)
                {
                    double u = (double)i / w;
                    double f = 0;
                    // the surface: the union of the blobs as spheres, so every bubble is a
                    // dome with a crease where it meets the next
                    double dome = 0;
                    const double sk = 0.012;
                    for (int bi = 0; bi < B.Length; bi++)
                    {
                        double dx = (u - B[bi].x) / B[bi].rx;
                        double dy = (Y - B[bi].y) / B[bi].ry;
                        double d2 = dx * dx + dy * dy;
                        if (d2 < 9) f += B[bi].k * Math.Exp(-d2 * 1.4);
                        // a soft union: the creases between bubbles are rounded, not cut
                        if (d2 < 1.2) dome += Math.Exp((B[bi].rx * Math.Sqrt(Math.Max(0, 1 - d2))) / sk);
                    }
                    if (f < 0.05) continue;
                    // ragged edges: the noise eats the thin outer parts, leaves the core whole
                    // billows: noise folded on itself (1-|2n-1|) reads as heaped cauliflower
                    double wx = fbm(u * 3 + 7.1, Y * 3, ns + 9, 3) - 0.5;
                    double nz = fbm(u * freq + wx * 1.5, Y * freq + wx, ns, 6);
                    double bill = 1 - Math.Abs(nz * 2 - 1);
                    double d = Math.Min(f, 1.3) - 0.5 + (nz - 0.5) * 1.3 + (bill - 0.5) * 0.45;
                    if (cumulus)
                    {
                        // the condensation level: a cumulus sits on a flat, slightly ragged base
                        d *= 1 - smooth(baseY - 0.015, baseY + 0.02 + nz * 0.03, Y);
                    }
                    else
                    {
                        // sheets break up into rows of little cells
                        double cells = fbm(u * 34, Y * 60, ns + 5, 3);
                        d *= 0.4 + cells * 1.1;
                    }
                    // never touch the sprite's edge
                    d *= smooth(0, 0.06, u) * smooth(1, 0.94, u) * smooth(0, 0.04, Y);
                    dens[j * w + i] = (float)smooth(0, crisp, d);
                    // the domes of the blobs, gently roughened: fine noise here reads as rock
                    hgt[j * w + i] = (float)((dome > 0 ? sk * Math.Log(dome) : 0) + (nz - 0.5) * 0.01);
                }
            }

            // light marched through the volume, up toward the sky and down toward the sun
            var top = new float[n];
            var bot = new float[n];
            // Light through the volume, from above and from below: a running,
            // exponentially fading sum of the density a ray has crossed, at two scales —
            // a long one for the body's own shadow, a short one for each billow shading
            // the next, which is what gives the surface its relief. Running sums rather
            // than a few spaced samples, which left the shading in bands.
            double longK = Math.Exp(-1 / Math.Max(2, h * 0.18));
            double shortK = Math.Exp(-1 / Math.Max(1.5, w / 120.0));
            double kL = (cumulus ? 1.1 : 0.8) / Math.Max(4, h * 0.18);
            double kS = 0.7 / Math.Max(1.5, w / 120.0);
            for (int i = 0; i < w; i++)
            {
                double aL = 0;
                double aS = 0;
                for (int j = 0; j < h; j++)
                {
                    int o = j * w + i;
                    top[o] = (float)Math.Exp(-aL * kL - aS * kS);
                    double d = dens[o];
                    aL = aL * longK + d;
                    aS = aS * shortK + d;
                }
                aL = 0;
                aS = 0;
                for (int j = h - 1; j >= 0; j--)
                {
                    int o = j * w + i;
                    bot[o] = (float)Math.Exp(-aL * kL * 0.75 - aS * kS * 0.6);
                    aL = aL * longK + dens[o];
                    aS = aS * shortK + dens[o];
                }
            }

            // soften the light across, so neighbouring columns don't stripe
            var tmp = new float[w];
            foreach (var L in new[] { top, bot })
            {
                for (int j = 0; j < h; j++)
                {
                    int row = j * w;
                    for (int i = 0; i < w; i++)
                    {
                        double sum = 0;
                        int cnt = 0;
                        for (int k = -3; k <= 3; k++)
                        {
                            int x = i + k;
                            if (x < 0 || x >= w) continue;
                            sum += L[row + x];
                            cnt++;
                        }
                        tmp[i] = (float)(sum / cnt);
                    }
                    Array.Copy(tmp, 0, L, row, w);
                }
            }

            // Each billow is a dome: its face toward the light is bright, the far side in
            // its own shade. Normals from the field's slope, lit from high above (by day)
            // and from low down (the setting sun under the cloud), mixed into the depth
            // light above.
            double S = w;
            for (int j = 1; j < h - 1; j++)
            {
                for (int i = 1; i < w - 1; i++)
                {
                    int o = j * w + i;
                    if (dens[o] < 0.004) continue;
                    double gx = ((double)hgt[o + 1] - hgt[o - 1]) * 0.5 * S;
                    double gy = ((double)hgt[o + w] - hgt[o - w]) * 0.5 * S;
                    double inv = 1 / Math.Sqrt(gx * gx + gy * gy + 1);
                    double nx = -gx * inv;
                    double ny = -gy * inv;
                    double nz = inv;
                    // light from above and a little left, toward the viewer
                    // wrapped: light soaks round into the shaded side of something this soft
                    double up = Math.Max(0, (nx * -0.35 + ny * -0.8 + nz * 0.48 + 0.45) / 1.45);
                    // light from below
                    double dn = Math.Max(0, (nx * 0.2 + ny * 0.85 + nz * 0.48 + 0.35) / 1.35);
                    double t0 = top[o], b0 = bot[o];
                    top[o] = (float)(t0 * 0.45 + up * 0.75 * (0.4 + t0 * 0.6));
                    bot[o] = (float)(b0 * 0.55 + dn * 0.6 * (0.3 + b0 * 0.7));
                }
            }

            return new CloudData { w = w, h = h, dens = dens, top = top, bot = bot };
        }

        /// <summary>colour the volume for the light it is in now</summary>
        public static void tintCloud(CloudShape c, CloudLight L)
        {
            c.canvas.PutPixels(tintPixels(c, L), 0, 0, c.w, c.h);
        }

        /// <summary>Uint8ClampedArray store: clamp to 0..255, round half to even, NaN → 0.</summary>
        static byte u8(double v)
        {
            if (!(v > 0)) return 0;
            if (v >= 255) return 255;
            return (byte)Math.Round(v, MidpointRounding.ToEven);
        }

        /// <summary>the coloured pixels, RGBA — pure arithmetic, so a worker can do it</summary>
        public static byte[] tintPixels(CloudData c, CloudLight L)
        {
            var px = new byte[c.w * c.h * 4];
            float[] dens = c.dens, top = c.top, bot = c.bot;
            double day = L.day;
            // by day: a white top over a blue-grey shade, a cooler, darker base
            RGB dShade = new RGB(158, 171, 196);
            RGB dLit = new RGB(255, 255, 255);
            RGB dBase = new RGB(138, 148, 170);
            double fire = L.bellyA * (0.25 + 0.75 * L.lit);
            for (int i = 0, n = c.w * c.h; i < n; i++)
            {
                double d = dens[i];
                if (d < 0.004)
                {
                    px[i * 4 + 3] = 0;
                    continue;
                }
                double tp = top[i];
                double bt = bot[i];
                // golden hour and night: the crown in shadow, the rim and belly in the light
                double m = 0.25 + 0.75 * Math.Pow(bt, 0.6);
                double r = L.crown[0] + (L.mid[0] - L.crown[0]) * m * 0.7;
                double g = L.crown[1] + (L.mid[1] - L.crown[1]) * m * 0.7;
                double b = L.crown[2] + (L.mid[2] - L.crown[2]) * m * 0.7;
                // thin edges let the light through: the silver (or burning) lining
                double rim = (1 - d) * 0.3 * (0.3 + bt) + Math.Pow(bt, 1.4);
                double k = Math.Min(1, rim * fire);
                r += (L.belly[0] - r) * k;
                g += (L.belly[1] - g) * k;
                b += (L.belly[2] - b) * k;
                if (day > 0)
                {
                    double lt = Math.Min(1, Math.Pow(tp, 0.5) * 1.15);
                    double under = Math.Max(0, 1 - bt) * (1 - tp) * 0.6;
                    double dr = dShade[0] + (dLit[0] - dShade[0]) * lt;
                    double dg = dShade[1] + (dLit[1] - dShade[1]) * lt;
                    double db = dShade[2] + (dLit[2] - dShade[2]) * lt;
                    dr += (dBase[0] - dr) * under;
                    dg += (dBase[1] - dg) * under;
                    db += (dBase[2] - db) * under;
                    r += (dr - r) * day;
                    g += (dg - g) * day;
                    b += (db - b) * day;
                }
                int o = i * 4;
                px[o] = u8(r);
                px[o + 1] = u8(g);
                px[o + 2] = u8(b);
                px[o + 3] = u8(Math.Min(1, d * (0.85 + 0.15 * L.crownA)) * 255);
            }
            return px;
        }
    }
}
