// Port of src/game/figures.ts (shared part: types, the lit painter, limbs and faces).
// The figures themselves are in Figures.Prometheus.cs, Figures.Merope.cs and Figures.Tantalus.cs.
//
// The others on the hill, painted properly.
//
// Each figure is drawn flat into a scratch canvas of its own — base colours,
// then the anatomy and the cloth drawn in — and then lit the way Sisyphus is:
// a broad soft light on the side facing the sun, a core shadow turning away,
// warm light bounced up from the ground, and a fine rim along the sunward
// edge, all worked out from the figure's own silhouette so they follow every
// pose. Only then does it go onto the hill.
//
// Every figure faces left (down the slope, toward where Sisyphus comes from)
// with its origin on the ground under it, and `s` is the scene's scale.
using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    public class FigLight
    {
        public RGB color;
        public double strength;
        /// <summary>unit vector toward the light, in screen space</summary>
        public double lx;
        public double ly;
        public double t;
        /// <summary>after dark: how much the colour has drained out (0-0.6)</summary>
        public double dim;
        /// <summary>how hard the wind blows where they stand (1 a breeze; more on a windy hill, in a storm)</summary>
        public double wind = 1;
    }

    /// <summary>
    /// Where Prometheus is looking (the TS's anonymous <c>{ turn, tilt }</c>): `turn` 1 is the way he
    /// faces (left, down the slope), -1 over his shoulder the other way; `tilt` lifts the face.
    /// </summary>
    public class ProGaze
    {
        public double turn = 1;
        public double tilt = 0;
        public ProGaze() { }
        public ProGaze(double turn, double tilt) { this.turn = turn; this.tilt = tilt; }
    }

    public static partial class Figures
    {
        const int R = 2; // scratch resolution, per CSS pixel

        static readonly Dictionary<string, Surface> pool = new Dictionary<string, Surface>();
        static Surface scratch(string key, int w, int h)
        {
            Surface c;
            if (!pool.TryGetValue(key, out c))
            {
                c = new Surface(w, h, true, "fig:" + key);
                pool[key] = c;
            }
            if (c.width != w || c.height != h)
            {
                c.SetSize(w, h);
            }
            return c;
        }

        static Col rgba(RGB c, double a)
        {
            return Col.Rgba(Js.round(c[0]), Js.round(c[1]), Js.round(c[2]), Css.fixedNum(a, 3));
        }
        static RGB mix(RGB a, RGB b, double k)
        {
            return new RGB(
                a[0] + (b[0] - a[0]) * k,
                a[1] + (b[1] - a[1]) * k,
                a[2] + (b[2] - a[2]) * k);
        }

        struct Box
        {
            public double l, t, r, b;
            public Box(double l, double t, double r, double b) { this.l = l; this.t = t; this.r = r; this.b = b; }
        }

        /// <summary>
        /// Paint a figure. `draw` draws it flat, in a frame whose origin is the
        /// figure's ground point; `box` is the frame's extent around that origin,
        /// in scene units (already multiplied by s).
        /// </summary>
        static void paintLit(Ctx ctx, string key, double x, double y, Box box, FigLight L, double s,
            Action<Ctx> draw, double alpha = 1)
        {
            int W = (int)Math.Max(1, Math.Ceiling((box.r - box.l) * R));
            int H = (int)Math.Max(1, Math.Ceiling((box.b - box.t) * R));
            var cv = scratch(key, W, H);
            var c = cv.GetContext();
            c.SetTransform(1, 0, 0, 1, 0, 0);
            c.GlobalCompositeOperation = Composite.SourceOver;
            c.GlobalAlpha = 1;
            c.ClearRect(0, 0, W, H);
            c.SetTransform(R, 0, 0, R, -box.l * R, -box.t * R);
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            draw(c);

            // the light, at a quarter of the size and stretched back over (the stretch
            // is the softness)
            int lw = (int)Math.Max(1, Math.Ceiling(W / 4.0));
            int lh = (int)Math.Max(1, Math.Ceiling(H / 4.0));
            var lo = scratch("lo:" + key, lw, lh);
            var lc = lo.GetContext();
            void pass(Col color, double ox, double oy)
            {
                double k = (double)lw / W;
                lc.SetTransform(1, 0, 0, 1, 0, 0);
                lc.GlobalCompositeOperation = Composite.SourceOver;
                lc.ClearRect(0, 0, lw, lh);
                lc.DrawImage(cv, 0, 0, lw, lh);
                lc.GlobalCompositeOperation = Composite.SourceIn;
                lc.FillStyle = color;
                lc.FillRect(0, 0, lw, lh);
                lc.GlobalCompositeOperation = Composite.DestinationOut;
                lc.DrawImage(cv, ox * k, oy * k, lw, lh);
                c.SetTransform(1, 0, 0, 1, 0, 0);
                c.GlobalCompositeOperation = Composite.SourceAtop;
                c.DrawImage(lo, 0, 0, W, H);
            }
            double K = 6 * s * R;
            RGB warm = mix(L.color, new RGB(255, 196, 150), 0.55);
            double st = L.strength;
            pass(
                Col.Rgba(18, 10, 14, Css.fixedNum(0.55 * (1 - L.dim * 0.4), 3)),
                L.lx * K * 1.3,
                L.ly * K * 1.3);
            pass(rgba(warm, 0.3 * st + 0.05), -L.lx * K, -L.ly * K);
            pass(Col.Rgba(255, 168, 110, Css.fixedNum(0.16 * (1 - L.dim), 3)), 0, -K * 0.8);
            // after dark the colour drains to moonlit blue
            if (L.dim > 0.01)
            {
                c.SetTransform(1, 0, 0, 1, 0, 0);
                c.GlobalCompositeOperation = Composite.SourceAtop;
                c.FillStyle = Col.Rgba(12, 16, 34, Css.fixedNum(L.dim, 3));
                c.FillRect(0, 0, W, H);
            }
            // the rim: the sunward edge, thin and warm
            if (st > 0.05)
            {
                var rim = scratch("rim:" + key, W, H);
                var rc = rim.GetContext();
                double e = Math.Max(1.2, 1.3 * s * R);
                rc.SetTransform(1, 0, 0, 1, 0, 0);
                rc.GlobalCompositeOperation = Composite.SourceOver;
                rc.ClearRect(0, 0, W, H);
                rc.DrawImage(cv, 0, 0);
                rc.GlobalCompositeOperation = Composite.SourceIn;
                rc.FillStyle = rgba(warm, 0.6 * st);
                rc.FillRect(0, 0, W, H);
                rc.GlobalCompositeOperation = Composite.DestinationOut;
                rc.DrawImage(cv, -L.lx * e, -L.ly * e);
                c.SetTransform(1, 0, 0, 1, 0, 0);
                c.GlobalCompositeOperation = Composite.SourceAtop;
                c.DrawImage(rim, 0, 0);
            }
            c.GlobalCompositeOperation = Composite.SourceOver;
            ctx.Save();
            ctx.GlobalAlpha *= alpha;
            ctx.DrawImage(cv, x + box.l, y + box.t, box.r - box.l, box.b - box.t);
            ctx.Restore();
        }

        /// <summary>
        /// A limb: tapered, with a muscle belly on one side (+ toward the way the
        /// figure faces, which is -x here). Joints round.
        /// </summary>
        static void limb(Ctx c, double ax, double ay, double bx, double by, double wa, double wb,
            double belly, double at, Style fill)
        {
            double dx = bx - ax;
            double dy = by - ay;
            double len = Js.hypot(dx, dy);
            if (len == 0 || double.IsNaN(len)) len = 1;
            double nx = -dy / len;
            double ny = dx / len;
            if (nx > 0)
            {
                nx = -nx;
                ny = -ny;
            }
            double mx = ax + dx * at;
            double my = ay + dy * at;
            double wm = wa + (wb - wa) * at;
            // side(sg, extra) -> p0, p2, control
            double fp0x, fp0y, fp2x, fp2y, fcx, fcy, kp0x, kp0y, kp2x, kp2y, kcx, kcy;
            void side(double sg, double extra, out double p0x, out double p0y, out double p2x, out double p2y, out double cx, out double cy)
            {
                p0x = ax + nx * wa * sg;
                p0y = ay + ny * wa * sg;
                p2x = bx + nx * wb * sg;
                p2y = by + ny * wb * sg;
                double dpx = mx + nx * (wm + extra) * sg;
                double dpy = my + ny * (wm + extra) * sg;
                cx = 2 * dpx - 0.5 * (p0x + p2x);
                cy = 2 * dpy - 0.5 * (p0y + p2y);
            }
            side(1, Math.Max(0, belly), out fp0x, out fp0y, out fp2x, out fp2y, out fcx, out fcy);
            side(-1, Math.Max(0, -belly), out kp0x, out kp0y, out kp2x, out kp2y, out kcx, out kcy);
            c.FillStyle = fill;
            c.BeginPath();
            c.MoveTo(fp0x, fp0y);
            c.QuadraticCurveTo(fcx, fcy, fp2x, fp2y);
            c.LineTo(kp2x, kp2y);
            c.QuadraticCurveTo(kcx, kcy, kp0x, kp0y);
            c.ClosePath();
            c.Fill();
            c.BeginPath();
            c.Arc(ax, ay, wa, 0, Math.PI * 2);
            c.Arc(bx, by, wb, 0, Math.PI * 2);
            c.Fill();
        }

        /// <summary>a line along a limb, offset to one side and bowed — a muscle's edge</summary>
        static void along(Ctx c, double ax, double ay, double bx, double by, double off, double t0, double t1, double bow)
        {
            double dx = bx - ax;
            double dy = by - ay;
            double len = Js.hypot(dx, dy);
            if (len == 0 || double.IsNaN(len)) len = 1;
            double nx = -dy / len;
            double ny = dx / len;
            if (nx > 0)
            {
                nx = -nx;
                ny = -ny;
            }
            double tm = (t0 + t1) / 2;
            c.BeginPath();
            c.MoveTo(ax + dx * t0 + nx * off, ay + dy * t0 + ny * off);
            c.QuadraticCurveTo(
                ax + dx * tm + nx * (off + bow),
                ay + dy * tm + ny * (off + bow),
                ax + dx * t1 + nx * off,
                ay + dy * t1 + ny * off);
            c.Stroke();
        }

        static void ink(Ctx c, double a, double w)
        {
            c.StrokeStyle = Col.Rgba(36, 14, 8, a);
            c.LineWidth = w;
        }
        static void shine(Ctx c, double a, double w)
        {
            c.StrokeStyle = Col.Rgba(255, 220, 185, a);
            c.LineWidth = w;
        }
        /// <summary>draw only onto what is already there</summary>
        static void atop(Ctx c, Action fn)
        {
            c.Save();
            c.GlobalCompositeOperation = Composite.SourceAtop;
            fn();
            c.Restore();
        }

        /// <summary>a head in profile, facing left, as one path: skull, brow, nose, lips, chin</summary>
        static void profile(Ctx c, double x, double y, double r, Style fill, double open = 0)
        {
            c.FillStyle = fill;
            c.BeginPath();
            c.MoveTo(x + 0.95 * r, y - 0.1 * r); // back of the skull
            c.QuadraticCurveTo(x + 0.95 * r, y - 1.05 * r, x + 0.05 * r, y - 1.08 * r); // crown
            c.QuadraticCurveTo(x - 0.72 * r, y - 1.0 * r, x - 0.82 * r, y - 0.42 * r); // forehead
            c.QuadraticCurveTo(x - 0.96 * r, y - 0.3 * r, x - 0.86 * r, y - 0.18 * r); // brow ridge
            c.LineTo(x - 0.88 * r, y - 0.08 * r); // eye socket
            c.LineTo(x - 1.28 * r, y + 0.3 * r); // nose
            c.QuadraticCurveTo(x - 1.3 * r, y + 0.44 * r, x - 0.98 * r, y + 0.46 * r); // nostril
            c.QuadraticCurveTo(x - 0.9 * r, y + 0.56 * r, x - 0.96 * r, y + 0.66 * r); // upper lip
            c.LineTo(x - 0.9 * r, y + 0.72 * r + open * 0.3 * r);
            c.QuadraticCurveTo(
                x - 0.98 * r,
                y + 0.82 * r + open * 0.3 * r,
                x - 0.88 * r,
                y + 0.92 * r + open * 0.32 * r); // lower lip
            c.QuadraticCurveTo(
                x - 0.94 * r,
                y + 1.18 * r + open * 0.32 * r,
                x - 0.62 * r,
                y + 1.2 * r + open * 0.32 * r); // chin
            c.QuadraticCurveTo(x - 0.1 * r, y + 1.18 * r + open * 0.2 * r, x + 0.42 * r, y + 0.62 * r); // jaw
            c.QuadraticCurveTo(x + 0.62 * r, y + 0.4 * r, x + 0.95 * r, y - 0.1 * r);
            c.ClosePath();
            c.Fill();
        }

        /// <summary>
        /// the eye, ear and brow on a profile; `pain` knots the brow, `shut` closes the eye.
        /// PORT: the TS options object <c>{ pain?, shut?, open?, ear? }</c> became optional parameters.
        /// </summary>
        /// <summary>
        /// A blink: 1 for the eighth of a second the lids are down, every three and a half
        /// to five seconds, never two faces in step (`seed`). Now and then a double blink.
        /// </summary>
        internal static double blink(double t, double seed)
        {
            double period = 3.5 + (seed * 0.731 % 1) * 1.6;
            double p = (((t + seed * 1.37) % period) + period) % period;
            bool twice = ((int)Math.Floor((t + seed * 1.37) / period) + (int)(seed * 10)) % 3 == 0;
            return p < 0.13 || (twice && p > 0.3 && p < 0.42) ? 1 : 0;
        }

        static void face(Ctx c, double x, double y, double r, double pain = 0, double shut = 0, double open = 0, bool ear = true)
        {
            atop(c, () =>
            {
                // the socket's shadow
                c.FillStyle = "rgba(40, 18, 12, 0.35)";
                c.BeginPath();
                c.Ellipse(x - 0.62 * r, y - 0.08 * r, 0.26 * r, 0.18 * r, 0, 0, Math.PI * 2);
                c.Fill();
                // the eye
                if (shut > 0.5)
                {
                    ink(c, 0.75, 0.09 * r);
                    c.BeginPath();
                    c.MoveTo(x - 0.8 * r, y - 0.04 * r);
                    c.QuadraticCurveTo(x - 0.64 * r, y + 0.06 * r, x - 0.48 * r, y - 0.04 * r);
                    c.Stroke();
                }
                else
                {
                    c.FillStyle = "rgba(235, 225, 210, 0.9)";
                    c.BeginPath();
                    c.Ellipse(x - 0.66 * r, y - 0.06 * r, 0.14 * r, 0.08 * r, 0, 0, Math.PI * 2);
                    c.Fill();
                    c.FillStyle = "#1a0f0a";
                    c.BeginPath();
                    c.Arc(x - 0.74 * r, y - 0.06 * r, 0.065 * r, 0, Math.PI * 2);
                    c.Fill();
                    ink(c, 0.8, 0.07 * r);
                    c.BeginPath();
                    c.MoveTo(x - 0.82 * r, y - 0.13 * r);
                    c.QuadraticCurveTo(x - 0.66 * r, y - 0.2 * r, x - 0.5 * r, y - 0.1 * r);
                    c.Stroke();
                }
                // the brow
                ink(c, 0.85, 0.12 * r);
                c.BeginPath();
                c.MoveTo(x - 0.88 * r, y - (0.32 - pain * 0.12) * r);
                c.QuadraticCurveTo(
                    x - 0.66 * r,
                    y - (0.42 + pain * 0.1) * r,
                    x - 0.42 * r,
                    y - (0.32 - pain * 0.05) * r);
                c.Stroke();
                if (pain > 0.3)
                {
                    // the furrow between the brows, the creases at the eye
                    ink(c, 0.4 * pain, 0.05 * r);
                    c.BeginPath();
                    c.MoveTo(x - 0.76 * r, y - 0.52 * r);
                    c.LineTo(x - 0.7 * r, y - 0.36 * r);
                    c.MoveTo(x - 0.46 * r, y + 0.02 * r);
                    c.LineTo(x - 0.36 * r, y + 0.1 * r);
                    c.Stroke();
                }
                // nostril and the line of the cheek
                ink(c, 0.4, 0.06 * r);
                c.BeginPath();
                c.MoveTo(x - 1.05 * r, y + 0.4 * r);
                c.QuadraticCurveTo(x - 0.98 * r, y + 0.32 * r, x - 0.92 * r, y + 0.4 * r);
                c.MoveTo(x - 0.86 * r, y + 0.5 * r);
                c.QuadraticCurveTo(x - 0.62 * r, y + 0.42 * r, x - 0.5 * r, y + 0.7 * r);
                c.Stroke();
                if (open > 0.2)
                {
                    c.FillStyle = "#1b0907";
                    c.BeginPath();
                    c.Ellipse(
                        x - 0.9 * r,
                        y + 0.78 * r + open * 0.16 * r,
                        0.1 * r,
                        0.06 * r + open * 0.14 * r,
                        0,
                        0,
                        Math.PI * 2);
                    c.Fill();
                }
                if (ear)
                {
                    c.FillStyle = "rgba(70, 30, 20, 0.35)";
                    c.BeginPath();
                    c.Ellipse(x + 0.18 * r, y + 0.06 * r, 0.16 * r, 0.26 * r, 0.15, 0, Math.PI * 2);
                    c.Fill();
                }
            });
        }

        /// <summary>hair as a mass with strands combed through it; each entry is x0, y0, x1, y1</summary>
        static void strands(Ctx c, double[][] pts, Style col, double w)
        {
            c.StrokeStyle = col;
            c.LineWidth = w;
            c.BeginPath();
            foreach (var p in pts)
            {
                double x0 = p[0], y0 = p[1], x1 = p[2], y1 = p[3];
                c.MoveTo(x0, y0);
                c.QuadraticCurveTo((x0 + x1) / 2 + (y1 - y0) * 0.15, (y0 + y1) / 2, x1, y1);
            }
            c.Stroke();
        }

        // ---- PORT: spread-argument helpers for the `...P(px, py)` idiom of the TS
        static void MoveTo(this Ctx c, (double x, double y) p) { c.MoveTo(p.x, p.y); }
        static void LineTo(this Ctx c, (double x, double y) p) { c.LineTo(p.x, p.y); }
        static void QuadraticCurveTo(this Ctx c, (double x, double y) a, (double x, double y) b) { c.QuadraticCurveTo(a.x, a.y, b.x, b.y); }
        static void BezierCurveTo(this Ctx c, (double x, double y) a, (double x, double y) b, (double x, double y) d) { c.BezierCurveTo(a.x, a.y, b.x, b.y, d.x, d.y); }
    }
}
