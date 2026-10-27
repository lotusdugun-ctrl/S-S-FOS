using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /*
     * Trees, painted once into sprites and only swayed per frame.
     *
     * The olive trees used to be a stick with three green circles on it. A real
     * old olive on a dry slope is a short, split, twisting trunk — often two or
     * three stems grown apart — carrying a loose, ragged crown of small dark
     * leaf clusters with sky showing through, silver-green where the light
     * catches it. Against a low sun it is mostly silhouette, edged with light.
     * The cypress is the other tree of these hills: a tall dark flame.
     *
     * Sprites are built at screen density with their base at (ox, oy), and the
     * same rim technique as the figures: the silhouette minus itself shifted away
     * from the sun, laid back on in warm light.
     */

    public class TreeSprite
    {
        public Surface cv;
        public double ox, oy, px;
    }

    public static class Flora
    {
        static Surface mk(double w, double h)
        {
            return new Surface(Math.Max(1, (int)Math.Ceiling(w)), Math.Max(1, (int)Math.Ceiling(h)));
        }

        /// <summary>lay a sunward rim on everything already painted in `cv`</summary>
        static void rimLight(Surface cv, double e, string color)
        {
            // PORT: the TS made a fresh canvas each time and let it be collected; here one
            // scratch surface per sprite size is kept, and cleared by re-setting its size
            // (as a new canvas is clear). Never resized, so several sprites built in one
            // frame never share a surface at two sizes.
            var sz = (cv.width, cv.height);
            if (!rimTmp.TryGetValue(sz, out var r)) rimTmp[sz] = r = mk(cv.width, cv.height);
            else r.SetSize(cv.width, cv.height);
            var rc = r.GetContext();
            var c = cv.GetContext();
            rc.DrawImage(cv, 0, 0);
            rc.GlobalCompositeOperation = Composite.SourceIn;
            rc.FillStyle = color;
            rc.FillRect(0, 0, r.width, r.height);
            rc.GlobalCompositeOperation = Composite.DestinationOut;
            // the sun is to the right of most of the slope and low: rim on the right/top
            rc.DrawImage(cv, -e, e * 0.6);
            c.GlobalCompositeOperation = Composite.SourceAtop;
            c.DrawImage(r, 0, 0);
            c.GlobalCompositeOperation = Composite.SourceOver;
        }

        static readonly Dictionary<(int, int), Surface> rimTmp = new Dictionary<(int, int), Surface>();

        /// <summary>px: sprite pixels per design unit (scale * dpr); seed: which tree</summary>
        public static TreeSprite oliveSprite(double seed, double px)
        {
            var r = Textures.rng(seed * 7919 + 13);
            const double W = 110;
            const double H = 96;
            var cv = mk(W * px, H * px);
            var c = cv.GetContext();
            double ox = (W / 2) * px;
            double oy = (H - 4) * px;
            c.Translate(ox, oy);
            c.Scale(px, px);
            const string bark = "oklch(0.16 0.02 55)";
            const string leafDark = "oklch(0.17 0.022 120)";

            // trunk: two or three stems that split low and twist apart
            double stems = 2 + Math.Floor(r() * 2);
            var tips = new List<(double x, double y)>();
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            for (int i = 0; i < stems; i++)
            {
                double dir = (i - (stems - 1) / 2) * 0.55 + (r() - 0.5) * 0.3;
                double x = (r() - 0.5) * 4;
                double y = 0;
                double wdt = 7 - i * 1.2;
                double a = -Math.PI / 2 + dir;
                c.StrokeStyle = bark;
                const int segs = 5;
                for (int k = 0; k < segs; k++)
                {
                    double len = 7 + r() * 4;
                    a += (r() - 0.5) * 0.6;
                    double nx = x + Math.Cos(a) * len;
                    double ny = y + Math.Sin(a) * len;
                    c.LineWidth = wdt;
                    c.BeginPath();
                    c.MoveTo(x, y);
                    double qx = (x + nx) / 2 + (r() - 0.5) * 4;
                    c.QuadraticCurveTo(qx, (y + ny) / 2, nx, ny);
                    c.Stroke();
                    // knots and burls on an old trunk
                    if (k < 2 && r() < 0.6)
                    {
                        c.FillStyle = bark;
                        c.BeginPath();
                        double ex = nx + (r() - 0.5) * 3;
                        c.Ellipse(ex, ny, wdt * 0.7, wdt * 0.5, r(), 0, Math.PI * 2);
                        c.Fill();
                    }
                    x = nx;
                    y = ny;
                    wdt *= 0.72;
                    // branches off the upper trunk
                    if (k >= 2)
                    {
                        double sgn = r() < 0.5 ? -1 : 1;
                        double ba = a + sgn * (0.5 + r() * 0.6);
                        double bl = 10 + r() * 14;
                        double bx = x + Math.Cos(ba) * bl;
                        double by = y + Math.Sin(ba) * bl;
                        c.LineWidth = Math.Max(1, wdt * 0.8);
                        c.BeginPath();
                        c.MoveTo(x, y);
                        c.QuadraticCurveTo((x + bx) / 2, (y + by) / 2 - 3, bx, by);
                        c.Stroke();
                        tips.Add((bx, by));
                    }
                }
                tips.Add((x, y));
            }

            // crown: many small clusters around the branch tips, ragged, with gaps
            foreach (var (tx, ty) in tips)
            {
                double n = 7 + Math.Floor(r() * 6);
                for (int j = 0; j < n; j++)
                {
                    double a = r() * Math.PI * 2;
                    double d = r() * 12;
                    double cx = tx + Math.Cos(a) * d * 1.3;
                    double cy = ty + Math.Sin(a) * d * 0.7 - 3;
                    double rx = 3 + r() * 5;
                    double ry = 2 + r() * 3;
                    c.FillStyle = leafDark;
                    c.BeginPath();
                    c.Ellipse(cx, cy, rx, ry, (r() - 0.5) * 0.8, 0, Math.PI * 2);
                    c.Fill();
                    // little leaf points along the cluster edge: olive leaves are narrow
                    for (int q = 0; q < 4; q++)
                    {
                        double qa = r() * Math.PI * 2;
                        c.BeginPath();
                        c.Ellipse(cx + Math.Cos(qa) * rx, cy + Math.Sin(qa) * ry, 2.4, 0.8, qa, 0, Math.PI * 2);
                        c.Fill();
                    }
                }
            }
            // silver-green where the leaves catch the light (olive undersides)
            c.GlobalCompositeOperation = Composite.SourceAtop;
            foreach (var (tx, ty) in tips)
            {
                var g = c.CreateRadialGradient(tx + 6, ty - 8, 0, tx + 6, ty - 8, 16);
                g.AddColorStop(0, "rgba(170, 170, 140, 0.13)");
                g.AddColorStop(1, "rgba(170, 170, 140, 0)");
                c.FillStyle = g;
                c.FillRect(tx - 20, ty - 30, 44, 44);
            }
            c.GlobalCompositeOperation = Composite.SourceOver;
            c.SetTransform(1, 0, 0, 1, 0, 0);
            rimLight(cv, Math.Max(1, 1.3 * px), "rgba(240, 190, 130, 0.5)");
            return new TreeSprite { cv = cv, ox = ox, oy = oy, px = px };
        }

        public static TreeSprite cypressSprite(double seed, double px)
        {
            var r = Textures.rng(seed * 104729 + 7);
            const double W = 30;
            const double H = 120;
            var cv = mk(W * px, H * px);
            var c = cv.GetContext();
            double ox = (W / 2) * px;
            double oy = (H - 3) * px;
            c.Translate(ox, oy);
            c.Scale(px, px);
            double height = 100 + r() * 14;
            double width = 9 + r() * 3;
            c.FillStyle = "oklch(0.15 0.03 150)";
            // the flame: widest a third of the way up, tapering to a point, with a
            // feathered edge of foliage tufts so it is never a smooth spindle
            c.BeginPath();
            c.MoveTo(-1.5, 0);
            for (int i = 0; i <= 24; i++)
            {
                double t = i / 24.0;
                double prof = Math.Sin(Math.Min(1, t * 1.6) * Math.PI * 0.5) * Math.Pow(1 - t, 0.8);
                c.LineTo(-width * prof - r() * 1.6 * prof, -t * height);
            }
            for (int i = 24; i >= 0; i--)
            {
                double t = i / 24.0;
                double prof = Math.Sin(Math.Min(1, t * 1.6) * Math.PI * 0.5) * Math.Pow(1 - t, 0.8);
                c.LineTo(width * prof + r() * 1.6 * prof, -t * height);
            }
            c.ClosePath();
            c.Fill();
            for (int i = 0; i < 40; i++)
            {
                double t = 0.1 + r() * 0.85;
                double prof = Math.Sin(Math.Min(1, t * 1.6) * Math.PI * 0.5) * Math.Pow(1 - t, 0.8);
                double side = r() < 0.5 ? -1 : 1;
                c.BeginPath();
                double ex = side * width * prof * (0.8 + r() * 0.3);
                double erx = 2 + r() * 2;
                double ery = 3 + r() * 3;
                c.Ellipse(ex, -t * height, erx, ery, side * 0.3, 0, Math.PI * 2);
                c.Fill();
            }
            c.FillStyle = "oklch(0.14 0.02 55)";
            c.FillRect(-1.5, -4, 3, 6);
            c.SetTransform(1, 0, 0, 1, 0, 0);
            rimLight(cv, Math.Max(1, 1.2 * px), "rgba(240, 190, 130, 0.45)");
            return new TreeSprite { cv = cv, ox = ox, oy = oy, px = px };
        }

        /// <summary>
        /// A clump of dry grass, live (it moves): a fan of thin curved blades from one
        /// root, dark against the light with tips that glow where the sun comes
        /// through the seed heads. `wind` is the current lean, in radians.
        /// </summary>
        public static void grassClump(Ctx c, double x, double y, double size, double seed, double wind, Style dark, Style glow)
        {
            var r = Textures.rng(seed);
            double n = 6 + Math.Floor(r() * 6);
            c.LineCap = LineCap.Round;
            // one path for every blade and one for every seed head: two strokes per
            // clump instead of two per blade
            var heads = grassHeads;
            heads.Clear();
            c.StrokeStyle = dark;
            c.LineWidth = Math.Max(0.7, size * 0.035);
            c.BeginPath();
            for (int i = 0; i < n; i++)
            {
                double spread = (r() - 0.5) * 1.2;
                double len = size * (0.6 + r() * 0.6);
                double bend = wind * (0.6 + r() * 0.8) + spread * 0.6;
                double tx = x + Math.Sin(spread * 0.6 + bend) * len;
                double ty = y - Math.Cos(spread * 0.6 + bend * 0.5) * len;
                c.MoveTo(x + spread * size * 0.08, y);
                c.QuadraticCurveTo(x + Math.Sin(spread * 0.4) * len * 0.5, y - len * 0.55, tx, ty);
                if (r() < 0.55)
                {
                    heads.Add(x + (tx - x) * 0.82); heads.Add(y + (ty - y) * 0.82);
                    heads.Add(tx); heads.Add(ty);
                }
            }
            c.Stroke();
            if (heads.Count > 0)
            {
                c.StrokeStyle = glow;
                c.LineWidth = Math.Max(0.8, size * 0.045);
                c.BeginPath();
                for (int i = 0; i < heads.Count; i += 4)
                {
                    c.MoveTo(heads[i], heads[i + 1]);
                    c.LineTo(heads[i + 2], heads[i + 3]);
                }
                c.Stroke();
            }
        }

        // reused per call (grassClump runs every frame); main thread only
        static readonly List<double> grassHeads = new List<double>();
    }
}
