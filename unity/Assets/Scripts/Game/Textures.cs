using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /*
     * Procedural textures, painted once into offscreen canvases.
     *
     * Everything here is deterministic (seeded), so the hill and the stone look the
     * same on every visit and every frame, and nothing is generated per frame.
     */
    public static class Textures
    {
        /// <summary>small seeded PRNG (mulberry32); bit-identical to the TS (uint arithmetic wraps as Math.imul / >>> 0 do)</summary>
        public static Func<double> rng(double seed)
        {
            uint a = Js.ToUint32(seed);
            return () =>
            {
                unchecked
                {
                    a = a + 0x6d2b79f5u;
                    uint t = a;
                    t = (t ^ (t >> 15)) * (t | 1u);
                    t ^= t + (t ^ (t >> 7)) * (t | 61u);
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            };
        }

        static Surface canvas(double w, double h)
        {
            return new Surface(Math.Max(1, (int)Math.Ceiling(w)), Math.Max(1, (int)Math.Ceiling(h)));
        }

        /// <summary>
        /// Granite, for the stone: a mid grey-brown body with the three grains granite
        /// actually has — dark mica, pale feldspar, glassy quartz — over larger
        /// weathering mottles, a few hairline joints, and rust-orange lichen.
        /// Painted as a square that the stone's outline clips; it turns with the stone.
        /// </summary>
        public static Surface graniteTexture(double px)
        {
            var cv = canvas(px, px);
            var c = cv.GetContext();
            var r = rng(91);
            double S = px;
            c.FillStyle = "oklch(0.43 0.018 62)";
            c.FillRect(0, 0, S, S);
            // weathering mottles: broad, soft, both darker and paler
            for (int i = 0; i < 46; i++)
            {
                double x = r() * S;
                double y = r() * S;
                double rad = (0.06 + r() * 0.22) * S;
                bool dark = r() < 0.55;
                var g = c.CreateRadialGradient(x, y, 0, x, y, rad);
                g.AddColorStop(0, dark ? "rgba(30, 24, 20, 0.22)" : "rgba(200, 180, 150, 0.14)");
                g.AddColorStop(1, "rgba(0,0,0,0)");
                c.FillStyle = g;
                c.FillRect(x - rad, y - rad, rad * 2, rad * 2);
            }
            // grains
            void grain(int n, Func<Col> color, double rmin, double rmax)
            {
                for (int i = 0; i < n; i++)
                {
                    c.FillStyle = color();
                    double x = r() * S;
                    double y = r() * S;
                    double rr = (rmin + r() * (rmax - rmin)) * (S / 256);
                    c.BeginPath();
                    double ry = rr * (0.5 + r() * 0.5);
                    c.Ellipse(x, y, rr, ry, r() * Math.PI, 0, Math.PI * 2);
                    c.Fill();
                }
            }
            grain(1400, () =>
            {
                double cr = 20 + r() * 20, cg = 16 + r() * 14, cb = 14 + r() * 10, ca = 0.35 + r() * 0.4;
                return Col.Rgba(cr, cg, cb, ca);
            }, 0.4, 1.6);
            grain(900, () =>
            {
                double cr = 185 + r() * 40, cg = 160 + r() * 30, cb = 130 + r() * 30, ca = 0.25 + r() * 0.35;
                return Col.Rgba(cr, cg, cb, ca);
            }, 0.4, 1.5);
            grain(260, () => Col.Rgba(235, 232, 225, 0.25 + r() * 0.4), 0.3, 0.9);
            // joints: hairline cracks the weather has opened
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            for (int k = 0; k < 5; k++)
            {
                double x = r() * S;
                double y = r() * S;
                double a = r() * Math.PI * 2;
                c.StrokeStyle = Col.Rgba(18, 14, 12, 0.45 + r() * 0.3);
                c.LineWidth = (0.8 + r() * 1.4) * (S / 256);
                c.BeginPath();
                c.MoveTo(x, y);
                double n = 6 + Math.Floor(r() * 8);
                for (int i = 0; i < n; i++)
                {
                    a += (r() - 0.5) * 0.9;
                    x += Math.Cos(a) * S * 0.035;
                    y += Math.Sin(a) * S * 0.035;
                    c.LineTo(x, y);
                }
                c.Stroke();
            }
            // lichen: crusty ochre rosettes
            for (int k = 0; k < 7; k++)
            {
                double x = r() * S;
                double y = r() * S;
                double rad = (0.03 + r() * 0.06) * S;
                for (int j = 0; j < 14; j++)
                {
                    double a = r() * Math.PI * 2;
                    double d = r() * rad;
                    double cr = 170 + r() * 40, cg = 110 + r() * 30, cb = 50 + r() * 20, ca = 0.12 + r() * 0.18;
                    c.FillStyle = Col.Rgba(cr, cg, cb, ca);
                    c.BeginPath();
                    c.Arc(x + Math.Cos(a) * d, y + Math.Sin(a) * d, rad * (0.15 + r() * 0.3), 0, Math.PI * 2);
                    c.Fill();
                }
            }
            return cv;
        }

        static readonly double[] ONE = { -1, 0, 1 };

        /// <summary>
        /// The hillside, as a seamless tile anchored to the world: dry, compacted dirt
        /// with mottled patches, a scatter of gravel and stones (each lit on the side
        /// facing the low sun and shadowed on the other), dry grass litter, and
        /// hairline cracks. Everything near an edge is drawn again on the opposite
        /// edge, so the tile repeats without a seam.
        /// </summary>
        public static Surface groundTexture(double px)
        {
            var cv = canvas(px, px);
            var c = cv.GetContext();
            var r = rng(7);
            double S = px;
            double k = S / 512;
            void wrap(double x, double y, double rad, Action<double, double> draw)
            {
                foreach (var ddx in ONE)
                    foreach (var ddy in ONE)
                    {
                        double dx = ddx * S, dy = ddy * S;
                        double xx = x + dx;
                        double yy = y + dy;
                        if (xx + rad < 0 || xx - rad > S || yy + rad < 0 || yy - rad > S) continue;
                        draw(xx, yy);
                    }
            }
            // mottled patches of darker and paler earth
            for (int i = 0; i < 70; i++)
            {
                double x = r() * S;
                double y = r() * S;
                double rad = (20 + r() * 70) * k;
                bool dark = r() < 0.6;
                double a = 0.05 + r() * 0.08;
                wrap(x, y, rad, (xx, yy) =>
                {
                    var g = c.CreateRadialGradient(xx, yy, 0, xx, yy, rad);
                    g.AddColorStop(0, dark ? Col.Rgba(20, 12, 8, a) : Col.Rgba(220, 170, 110, a * 0.8));
                    g.AddColorStop(1, "rgba(0,0,0,0)");
                    c.FillStyle = g;
                    c.FillRect(xx - rad, yy - rad, rad * 2, rad * 2);
                });
            }
            // fine grit
            for (int i = 0; i < 2600; i++)
            {
                double x = r() * S;
                double y = r() * S;
                bool light = r() < 0.4;
                c.FillStyle = light
                    ? Col.Rgba(210, 165, 110, 0.08 + r() * 0.14)
                    : Col.Rgba(18, 10, 6, 0.1 + r() * 0.18);
                double fw = (0.6 + r() * 1.4) * k;
                double fh = (0.6 + r() * 1) * k;
                c.FillRect(x, y, fw, fh);
            }
            // dry grass litter: short pale strokes lying every which way
            c.LineCap = LineCap.Round;
            for (int i = 0; i < 160; i++)
            {
                double x = r() * S;
                double y = r() * S;
                double a = r() * Math.PI;
                double len = (3 + r() * 7) * k;
                c.StrokeStyle = Col.Rgba(200, 160, 95, 0.08 + r() * 0.12);
                c.LineWidth = 0.7 * k;
                wrap(x, y, len, (xx, yy) =>
                {
                    c.BeginPath();
                    c.MoveTo(xx, yy);
                    c.LineTo(xx + Math.Cos(a) * len, yy + Math.Sin(a) * len * 0.5);
                    c.Stroke();
                });
            }
            // stones: irregular, lit top-right by the sun, shadow below-left
            for (int i = 0; i < 230; i++)
            {
                double x = r() * S;
                double y = r() * S;
                bool big = r() < 0.12;
                double rad = (big ? 3.5 + r() * 5 : 1 + r() * 2.4) * k;
                const int n = 7;
                var shape = new (double x, double y)[n];
                for (int j = 0; j < n; j++)
                {
                    double a = ((double)j / n) * Math.PI * 2;
                    double rr = rad * (0.75 + r() * 0.4);
                    shape[j] = (Math.Cos(a) * rr, Math.Sin(a) * rr * 0.62);
                }
                double tone = 0.18 + r() * 0.14;
                Col body = Col.Oklch(Css.fixedNum(tone, 3), 0.025, 55);
                wrap(x, y, rad * 2, (xx, yy) =>
                {
                    void poly(double ox, double oy)
                    {
                        c.BeginPath();
                        for (int j = 0; j < shape.Length; j++)
                        {
                            if (j != 0) c.LineTo(xx + shape[j].x + ox, yy + shape[j].y + oy);
                            else c.MoveTo(xx + shape[j].x + ox, yy + shape[j].y + oy);
                        }
                        c.ClosePath();
                    }
                    c.FillStyle = "rgba(10, 6, 4, 0.35)";
                    poly(-rad * 0.35, rad * 0.3);
                    c.Fill();
                    c.FillStyle = body;
                    poly(0, 0);
                    c.Fill();
                    c.Save();
                    poly(0, 0);
                    c.Clip();
                    // a lit crescent on the sun side: light the stone, then lay the body
                    // colour back over all but that edge
                    c.FillStyle = "rgba(240, 190, 120, 0.32)";
                    poly(0, 0);
                    c.Fill();
                    c.FillStyle = body;
                    poly(-rad * 0.28, rad * 0.24);
                    c.Fill();
                    c.Restore();
                    // lit upper-right edge
                    c.StrokeStyle = Col.Rgba(245, 200, 135, 0.25 + r() * 0.2);
                    c.LineWidth = 0.7 * k;
                    c.BeginPath();
                    var a0 = shape[n - 2];
                    var a1 = shape[n - 1];
                    var a2 = shape[0];
                    var a3 = shape[1];
                    c.MoveTo(xx + a0.x, yy + a0.y);
                    c.LineTo(xx + a1.x, yy + a1.y);
                    c.LineTo(xx + a2.x, yy + a2.y);
                    c.LineTo(xx + a3.x, yy + a3.y);
                    c.Stroke();
                });
            }
            // hairline cracks in the dried crust
            for (int i = 0; i < 18; i++)
            {
                double x = r() * S;
                double y = r() * S;
                double a = r() * Math.PI * 2;
                var pts = new List<(double x, double y)> { (x, y) };
                double n = 4 + Math.Floor(r() * 6);
                for (int j = 0; j < n; j++)
                {
                    a += (r() - 0.5) * 1.2;
                    x += Math.Cos(a) * 9 * k;
                    y += Math.Sin(a) * 5 * k;
                    pts.Add((x, y));
                }
                c.StrokeStyle = Col.Rgba(12, 7, 4, 0.12 + r() * 0.12);
                c.LineWidth = (0.6 + r() * 0.8) * k;
                foreach (var ddx in ONE)
                    foreach (var ddy in ONE)
                    {
                        double dx = ddx * S, dy = ddy * S;
                        c.BeginPath();
                        for (int j = 0; j < pts.Count; j++)
                        {
                            if (j != 0) c.LineTo(pts[j].x + dx, pts[j].y + dy);
                            else c.MoveTo(pts[j].x + dx, pts[j].y + dy);
                        }
                        c.Stroke();
                    }
            }
            return cv;
        }
    }
}
