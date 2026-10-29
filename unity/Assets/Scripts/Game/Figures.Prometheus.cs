// Port of src/game/figures.ts: Prometheus, his rock, the path fire and Zeus's eagle.
using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>phase: "away" | "arrive" | "feed" | "leave"</summary>
    public class EagleVisit
    {
        public string phase = "away";
        public double t;
        public EagleVisit() { }
        public EagleVisit(string phase, double t) { this.phase = phase; this.t = t; }
    }

    /// <summary>
    /// How Prometheus is, as his own story has left him: chained (0); the chains
    /// broken, still kneeling where they held him (1); on his feet (2). `healed`:
    /// the eagle has stopped coming and the wound has closed. `onRock`: drawn at
    /// his cliff, with its rings and chains; off it, he sits down by the path.
    /// </summary>
    public class ProLook
    {
        /// <summary>0 | 1 | 2</summary>
        public int free;
        public bool healed;
        public bool onRock;
        public ProLook() { }
        public ProLook(int free, bool healed, bool onRock) { this.free = free; this.healed = healed; this.onRock = onRock; }
    }

    public static partial class Figures
    {
        public static readonly ProLook PRO_CHAINED = new ProLook(0, false, true);

        /// <summary>his cliff once he has left it: the rings in the rock, the broken chains hanging</summary>
        public static void drawEmptyRock(Ctx ctx, double x, double y, double s, FigLight L)
        {
            cliff(ctx, x + 10 * s, y, s, L);
            ctx.Save();
            double[][] rings = { new[] { x - 6 * s, y - 118 * s }, new[] { x + 26 * s, y - 124 * s } };
            foreach (var rr in rings)
            {
                double rx = rr[0], ry = rr[1];
                ctx.StrokeStyle = "#232428";
                ctx.LineWidth = 1.4 * s;
                ctx.BeginPath();
                ctx.Arc(rx, ry, 2.6 * s, 0, Math.PI * 2);
                ctx.Stroke();
                // a few links, then nothing
                double sway = Math.Sin(L.t * 0.9 + rx * 0.01) * 1.2 * s;
                for (int i = 0; i < 5; i++)
                {
                    double k = (i + 0.5) / 5;
                    ctx.StrokeStyle = "#151619";
                    ctx.LineWidth = 1.1 * s;
                    ctx.BeginPath();
                    if (i % 2 == 0)
                        ctx.Ellipse(rx + sway * k, ry + 3 * s + k * 14 * s, 1.4 * s, 2 * s, 0, 0, Math.PI * 2);
                    else
                    {
                        ctx.MoveTo(rx + sway * k, ry + 1.5 * s + k * 14 * s);
                        ctx.LineTo(rx + sway * k, ry + 4.5 * s + k * 14 * s);
                    }
                    ctx.Stroke();
                }
            }
            // the grooves the chains wore in the rock, and his place under them, bare
            ctx.StrokeStyle = "rgba(10, 8, 6, 0.35)";
            ctx.LineWidth = 1 * s;
            ctx.BeginPath();
            ctx.MoveTo(x - 8 * s, y - 112 * s);
            ctx.LineTo(x - 10 * s, y - 92 * s);
            ctx.MoveTo(x + 24 * s, y - 118 * s);
            ctx.LineTo(x + 22 * s, y - 98 * s);
            ctx.Stroke();
            ctx.Restore();
        }

        /// <summary>
        /// A small fire by the path: dry roots and a few sticks, the flame breathing.
        /// `size` 1 is the fire he keeps; small for the one that comes back by
        /// itself; 0 is cold ash.
        /// </summary>
        public static void drawPathFire(Ctx ctx, double x, double y, double s, double t, double size)
        {
            ctx.Save();
            // the ring of ash
            ctx.FillStyle = "rgba(60, 56, 52, 0.85)";
            ctx.BeginPath();
            ctx.Ellipse(x, y, 9 * s, 2.4 * s, 0, 0, Math.PI * 2);
            ctx.Fill();
            // the sticks
            ctx.StrokeStyle = size > 0 ? "#2a1a10" : "#3a3633";
            ctx.LineWidth = 1.4 * s;
            ctx.LineCap = LineCap.Round;
            ctx.BeginPath();
            ctx.MoveTo(x - 7 * s, y + 0.5 * s);
            ctx.LineTo(x + 3 * s, y - 3.5 * s);
            ctx.MoveTo(x + 7 * s, y + 0.5 * s);
            ctx.LineTo(x - 3 * s, y - 3.5 * s);
            ctx.MoveTo(x - 5 * s, y - 1 * s);
            ctx.LineTo(x + 6 * s, y - 1.5 * s);
            ctx.Stroke();
            if (size > 0)
            {
                // the glow on the ground, then the flame: three tongues breathing out of step
                var g = ctx.CreateRadialGradient(x, y - 3 * s, 0, x, y - 3 * s, 26 * s * size);
                g.AddColorStop(0, "rgba(255, 170, 80, 0.35)");
                g.AddColorStop(1, "rgba(255, 140, 60, 0)");
                ctx.FillStyle = g;
                ctx.BeginPath();
                ctx.Arc(x, y - 3 * s, 26 * s * size, 0, Math.PI * 2);
                ctx.Fill();
                for (int i = 0; i < 3; i++)
                {
                    double ph = t * (5 + i * 1.7) + i * 2.1;
                    double h = (9 + Math.Sin(ph) * 2.2 + (i == 1 ? 3 : 0)) * s * size;
                    double w = (3.2 - Math.Abs(i - 1) * 0.8) * s * Math.Max(0.6, size);
                    double bx = x + (i - 1) * 2.4 * s * size + Math.Sin(ph * 0.7) * 0.6 * s;
                    var fg = ctx.CreateLinearGradient(0, y - 2 * s, 0, y - 2 * s - h);
                    fg.AddColorStop(0, "rgba(255, 210, 120, 0.95)");
                    fg.AddColorStop(0.5, "rgba(255, 140, 50, 0.85)");
                    fg.AddColorStop(1, "rgba(200, 60, 20, 0)");
                    ctx.FillStyle = fg;
                    ctx.BeginPath();
                    ctx.MoveTo(bx - w, y - 2 * s);
                    ctx.QuadraticCurveTo(
                        bx - w * 0.8,
                        y - 2 * s - h * 0.6,
                        bx + Math.Sin(ph) * 1.2 * s,
                        y - 2 * s - h);
                    ctx.QuadraticCurveTo(bx + w * 0.8, y - 2 * s - h * 0.6, bx + w, y - 2 * s);
                    ctx.ClosePath();
                    ctx.Fill();
                }
            }
            ctx.Restore();
        }

        /// <summary>a capsule (roundRect is missing before Safari 16)</summary>
        static void pill(Ctx c, double x, double y, double w, double h)
        {
            double r = Math.Min(h, w) / 2;
            c.MoveTo(x + r, y);
            c.LineTo(x + w - r, y);
            c.Arc(x + w - r, y + r, r, -Math.PI / 2, Math.PI / 2);
            c.LineTo(x + r, y + h);
            c.Arc(x + r, y + r, r, Math.PI / 2, (Math.PI * 3) / 2);
            c.ClosePath();
        }

        /// <summary>a small deterministic hash</summary>
        static double hh(double i, double k)
        {
            double v = Math.Sin(i * 127.1 + k * 311.7) * 43758.5453;
            return v - Math.Floor(v);
        }

        static readonly double[][] cliffOutline =
        {
            new double[] { -24, 4 },
            new double[] { -21, -22 },
            new double[] { -24, -38 },
            new double[] { -17, -60 },
            new double[] { -15, -84 },
            new double[] { -8, -98 },
            new double[] { -6, -118 },
            new double[] { 2, -134 },
            new double[] { 9, -142 },
            new double[] { 13, -152 },
            new double[] { 19, -146 },
            new double[] { 25, -150 },
            new double[] { 31, -138 },
            new double[] { 36, -122 },
            new double[] { 41, -116 },
            new double[] { 44, -92 },
            new double[] { 50, -74 },
            new double[] { 53, -48 },
            new double[] { 58, -30 },
            new double[] { 62, 4 },
        };

        static readonly double[][] cliffFissures =
        {
            new double[] { 4, -130, 5 },
            new double[] { 32, -124, 6 },
            new double[] { -12, -74, 3 },
            new double[] { 20, -60, 4 },
        };

        /// <summary>
        /// The crag he is chained to, drawn straight onto the hill behind him: a
        /// fractured column of dark rock, every block cut by the light into a lit
        /// face and a face in shadow, bedded in horizontal strata, scarred, with a
        /// crust of lichen, and the old blood of every day down the front of it.
        /// </summary>
        static void cliff(Ctx ctx, double x, double y, double s, FigLight L)
        {
            double lit = Math.Max(0.15, 0.5 - L.lx * 0.5) * (0.4 + 0.6 * L.strength) * (1 - L.dim * 0.7);
            var outline = cliffOutline;
            ctx.Save();
            ctx.BeginPath();
            for (int q = 0; q < outline.Length; q++)
            {
                if (q != 0) ctx.LineTo(x + outline[q][0] * s, y + outline[q][1] * s);
                else ctx.MoveTo(x + outline[q][0] * s, y + outline[q][1] * s);
            }
            ctx.ClosePath();
            var bse = ctx.CreateLinearGradient(0, y - 150 * s, 0, y);
            bse.AddColorStop(0, "#3a3230");
            bse.AddColorStop(1, "#231d1c");
            ctx.FillStyle = bse;
            ctx.Fill();
            ctx.Clip();
            // the rock breaks in long vertical shards, as columnar basalt does: each
            // shard a lit face and a face in shadow, each a little different
            double bx = x - 28 * s;
            int i = 0;
            while (bx < x + 64 * s)
            {
                double bw = (9 + hh(i, 1) * 13) * s;
                double top = y - (150 - (Math.Abs(bx - x - 18 * s) / s) * 1.1 - hh(i, 2) * 30) * s;
                double ridge = bx + bw * (0.35 + hh(i, 3) * 0.3);
                bool litLeft = L.lx < 0;
                // a shard may break across partway down
                double brk = top + (30 + hh(i, 4) * 60) * s;
                for (int seg = 0; seg < 2; seg++)
                {
                    double y0 = seg == 0 ? y + 4 * s : brk - 1.5 * s;
                    double y1 = seg == 0 ? brk : top;
                    int k = seg;
                    double v = 0.75 + hh(i, 5 + k) * 0.5;
                    double sh = (k != 0 ? 1.5 : -1) * s;
                    ctx.FillStyle = Col.Rgba(150 + lit * 70, 122 + lit * 50, 104 + lit * 30, Css.fixedNum((0.06 + lit * 0.2) * v, 3));
                    ctx.BeginPath();
                    if (litLeft)
                    {
                        ctx.MoveTo(bx, y0);
                        ctx.LineTo(bx + sh, y1);
                        ctx.LineTo(ridge + sh, y1);
                        ctx.LineTo(ridge, y0);
                    }
                    else
                    {
                        ctx.MoveTo(ridge, y0);
                        ctx.LineTo(ridge + sh, y1);
                        ctx.LineTo(bx + bw + sh, y1);
                        ctx.LineTo(bx + bw, y0);
                    }
                    ctx.ClosePath();
                    ctx.Fill();
                    ctx.FillStyle = Col.Rgba(6, 3, 4, Css.fixedNum(0.22 * v, 3));
                    ctx.BeginPath();
                    if (litLeft)
                    {
                        ctx.MoveTo(ridge, y0);
                        ctx.LineTo(ridge + sh, y1);
                        ctx.LineTo(bx + bw + sh, y1);
                        ctx.LineTo(bx + bw, y0);
                    }
                    else
                    {
                        ctx.MoveTo(bx, y0);
                        ctx.LineTo(bx + sh, y1);
                        ctx.LineTo(ridge + sh, y1);
                        ctx.LineTo(ridge, y0);
                    }
                    ctx.ClosePath();
                    ctx.Fill();
                }
                // the break, and the joint to the next shard
                ctx.StrokeStyle = "rgba(8, 4, 4, 0.55)";
                ctx.LineWidth = 0.8 * s;
                ctx.BeginPath();
                ctx.MoveTo(bx, brk);
                ctx.LineTo(bx + bw * 0.5, brk + (hh(i, 8) - 0.5) * 4 * s);
                ctx.LineTo(bx + bw, brk + (hh(i, 9) - 0.5) * 3 * s);
                ctx.MoveTo(bx + bw, y + 4 * s);
                ctx.LineTo(bx + bw + 1 * s, top);
                ctx.Stroke();
                bx += bw;
                i++;
            }
            // the whole far side of the crag turned away from the light
            var sideG = ctx.CreateLinearGradient(x - 24 * s, 0, x + 62 * s, 0);
            double away0 = L.lx < 0 ? 0 : 0.62, away1 = L.lx < 0 ? 0.62 : 0;
            sideG.AddColorStop(0, Col.Rgba(8, 4, 6, away0));
            sideG.AddColorStop(0.45, "rgba(8, 4, 6, 0.18)");
            sideG.AddColorStop(1, Col.Rgba(8, 4, 6, away1));
            ctx.FillStyle = sideG;
            ctx.FillRect(x - 30 * s, y - 160 * s, 100 * s, 170 * s);
            // fissures
            ctx.StrokeStyle = "rgba(0, 0, 0, 0.6)";
            ctx.LineWidth = 1 * s;
            ctx.BeginPath();
            foreach (var fz in cliffFissures)
            {
                double x0 = fz[0], y0 = fz[1];
                int n = (int)fz[2];
                double px = x + x0 * s;
                double py = y + y0 * s;
                ctx.MoveTo(px, py);
                for (int k = 0; k < n; k++)
                {
                    px += (hh(x0, k) - 0.5) * 8 * s;
                    py += (8 + hh(y0, k) * 6) * s;
                    ctx.LineTo(px, py);
                }
            }
            ctx.Stroke();
            // lichen: crusts of rust and grey-green on the lit, wet-free faces
            for (int j = 0; j < 26; j++)
            {
                double lx = x + (-18 + hh(j, 7) * 70) * s;
                double ly = y - hh(j, 9) * 140 * s;
                ctx.FillStyle = j % 3 == 0 ? "rgba(150, 110, 40, 0.28)" : "rgba(120, 130, 100, 0.22)";
                ctx.BeginPath();
                ctx.Ellipse(
                    lx,
                    ly,
                    (1 + hh(j, 3) * 2.4) * s,
                    (0.8 + hh(j, 4) * 1.4) * s,
                    hh(j, 5) * 3,
                    0,
                    Math.PI * 2);
                ctx.Fill();
            }
            // old blood down the face of the rock, dried black over older rust
            ctx.StrokeStyle = "rgba(70, 14, 8, 0.45)";
            ctx.LineWidth = 3.4 * s;
            ctx.BeginPath();
            ctx.MoveTo(x - 6 * s, y - 56 * s);
            ctx.QuadraticCurveTo(x - 3 * s, y - 26 * s, x - 7 * s, y);
            ctx.Stroke();
            ctx.StrokeStyle = "rgba(90, 8, 6, 0.7)";
            ctx.LineWidth = 1.6 * s;
            ctx.BeginPath();
            ctx.MoveTo(x - 6 * s, y - 50 * s);
            ctx.QuadraticCurveTo(x - 4 * s, y - 24 * s, x - 7 * s, y);
            ctx.MoveTo(x + 4 * s, y - 44 * s);
            ctx.QuadraticCurveTo(x + 6 * s, y - 20 * s, x + 3 * s, y);
            ctx.Stroke();
            ctx.Restore();
            // a crisp lit edge where the crag meets the sky on the light's side
            ctx.Save();
            ctx.StrokeStyle = Col.Rgba(240, 206, 170, Css.fixedNum(0.28 * lit, 3));
            ctx.LineWidth = 1 * s;
            ctx.BeginPath();
            int e0 = L.lx < 0 ? 0 : 10, e1 = L.lx < 0 ? 11 : outline.Length;
            for (int q = e0; q < e1; q++)
            {
                if (q != e0) ctx.LineTo(x + outline[q][0] * s, y + outline[q][1] * s);
                else ctx.MoveTo(x + outline[q][0] * s, y + outline[q][1] * s);
            }
            ctx.Stroke();
            ctx.Restore();
        }

        /// <summary>
        /// Prometheus: a titan on one knee at the foot of the cliff, both wrists
        /// chained high above him so his whole body hangs open to the bird. While it
        /// feeds he throws his head back and the muscles stand out of him; after, he
        /// hangs slack.
        /// `gaze`: where he is looking: `turn` 1 is the way he faces (left, down the slope),
        /// -1 over his shoulder the other way, and between the two the head is
        /// turning; `tilt` lifts the face (positive) or drops it.
        /// PORT: null for `eagle`, `gaze` or `look` means the TS default
        /// ({ phase: "away", t: 0 }, { turn: 1, tilt: 0 }, PRO_CHAINED).
        /// </summary>
        public static void drawPrometheus(Ctx ctx, double x, double y, double s, FigLight L,
            EagleVisit eagle = null, ProGaze gaze = null, ProLook look = null)
        {
            if (eagle == null) eagle = new EagleVisit("away", 0);
            if (gaze == null) gaze = new ProGaze(1, 0);
            if (look == null) look = PRO_CHAINED;
            double t = L.t;
            bool feeding = eagle.phase == "feed";
            int free = look.free;
            double writhe = feeding
                ? Math.Sin(t * 6.5) * 0.7 + Math.Sin(t * 11) * 0.3
                : Math.Sin(t * 0.8) * 0.08;
            double slack = feeding ? 0 : 1;
            if (look.onRock) cliff(ctx, x + 10 * s, y, s, L);
            // the rings in the rock
            var ringN = (x: x - 6 * s, y: y - 118 * s);
            var ringF = (x: x + 26 * s, y: y - 124 * s);
            paintLit(ctx, "prometheus", x, y, new Box(-70 * s, -140 * s, 60 * s, 6 * s), L, s, (c) =>
            {
                RGB SK = new RGB(150, 98, 70);
                Col skin(double k) { return rgba(mix(SK, new RGB(60, 34, 24), k), 1); }
                // pose: kneeling under the chains; free, still kneeling where they held
                // him; at last on his feet, one hand on the rock
                double up = free == 2 ? 23 * s : 0;
                var hip = (x: 2 * s, y: -27 * s + slack * 1.5 * s - up);
                var sh = (x: -2 * s + writhe * 1.6 * s, y: -64 * s + slack * 3 * s - up);
                var shN = (x: sh.x - 7 * s, y: sh.y + 1 * s);
                var shF = (x: sh.x + 7 * s, y: sh.y - 1 * s);
                var wrN =
                    free == 0
                        ? (x: ringN.x - x - 4 * s + writhe * 1.5 * s, y: ringN.y - y + 22 * s + slack * 5 * s)
                        : free == 1
                            ? (x: -9 * s, y: -9 * s) // on the near knee
                            : (x: sh.x - 8 * s, y: hip.y + 3 * s); // hanging at his side
                var wrF =
                    free == 0
                        ? (x: ringF.x - x - 2 * s - writhe * 1.5 * s, y: ringF.y - y + 22 * s + slack * 5 * s)
                        : free == 1
                            ? (x: -12 * s, y: -31 * s) // on the far knee
                            : (x: 19 * s, y: -80 * s); // flat on the rock
                (double x, double y) elbow((double x, double y) a, (double x, double y) b, double @out)
                {
                    return (x: (a.x + b.x) / 2 + @out * s, y: (a.y + b.y) / 2 + 2 * s);
                }
                var elN = elbow(shN, wrN, -6);
                var elF = elbow(shF, wrF, 6);

                // chains first, behind everything: forged links, alternately seen
                // face-on and edge-on, sagging between the ring and the cuff — or, once
                // they are broken, hanging from the ring and ending in nothing
                if (look.onRock)
                {
                    for (int ci = 0; ci < 2; ci++)
                    {
                        var r = ci == 0 ? ringN : ringF;
                        var w = ci == 0 ? wrN : wrF;
                        double rx = r.x - x;
                        double ry = r.y - y;
                        int n = free == 0 ? 11 : 5;
                        double sway = Math.Sin(t * 0.9 + rx) * 1.2 * s;
                        var end = free == 0 ? w : (x: rx + sway, y: ry + 16 * s);
                        var pts = new List<(double, double)>();
                        for (int i = 0; i <= n; i++)
                        {
                            double k = (double)i / n;
                            pts.Add((
                                rx + (end.x - rx) * k,
                                ry + (end.y - ry) * k + (free == 0 ? Math.Sin(k * Math.PI) * 4 * s * slack : 0)));
                        }
                        for (int i = 0; i < n; i++)
                        {
                            var (ax, ay) = pts[i];
                            var (bx, by) = pts[i + 1];
                            double a = Math.Atan2(by - ay, bx - ax);
                            double mx = (ax + bx) / 2;
                            double my = (ay + by) / 2;
                            double half = Js.hypot(bx - ax, by - ay) * 0.62;
                            c.Save();
                            c.Translate(mx, my);
                            c.Rotate(a);
                            if (i % 2 == 0)
                            {
                                c.StrokeStyle = "#151619";
                                c.LineWidth = 1.2 * s;
                                c.BeginPath();
                                c.Ellipse(0, 0, half, 1.5 * s, 0, 0, Math.PI * 2);
                                c.Stroke();
                                c.StrokeStyle = "rgba(150, 140, 130, 0.22)";
                                c.LineWidth = 0.35 * s;
                                c.BeginPath();
                                c.Ellipse(0, -0.3 * s, half * 0.9, 1.2 * s, 0, Math.PI * 1.1, Math.PI * 1.9);
                                c.Stroke();
                            }
                            else
                            {
                                c.FillStyle = "#121315";
                                c.BeginPath();
                                pill(c, -half, -0.65 * s, half * 2, 1.3 * s);
                                c.Fill();
                            }
                            c.Restore();
                        }
                        // the ring, hammered into the rock with an iron staple
                        c.StrokeStyle = "#232428";
                        c.LineWidth = 1.4 * s;
                        c.BeginPath();
                        c.Arc(rx, ry, 2.6 * s, 0, Math.PI * 2);
                        c.Stroke();
                        c.FillStyle = "#16171a";
                        c.FillRect(rx - 1.6 * s, ry - 4.4 * s, 3.2 * s, 2.4 * s);
                        c.FillStyle = "rgba(120, 50, 20, 0.45)";
                        c.BeginPath();
                        c.Ellipse(rx, ry + 5 * s, 1.2 * s, 4 * s, 0, 0, Math.PI * 2);
                        c.Fill();
                    }
                }

                // the far arm and leg, a shade darker
                limb(c, shF.x, shF.y, elF.x, elF.y, 4.6 * s, 3.6 * s, 1.6 * s, 0.45, skin(0.35));
                limb(c, elF.x, elF.y, wrF.x, wrF.y, 3.6 * s, 2.4 * s, 1.2 * s, 0.3, skin(0.35));
                var kneeF =
                    free == 2 ? (x: 6 * s, y: -25 * s) : (x: -14 * s, y: -30 * s + writhe * 1.2 * s);
                var footF = free == 2 ? (x: 8 * s, y: -1 * s) : (x: -15 * s, y: -1 * s);
                limb(c, hip.x + 2 * s, hip.y, kneeF.x, kneeF.y, 7 * s, 4.4 * s, 2.6 * s, 0.4, skin(0.3));
                limb(c, kneeF.x, kneeF.y, footF.x, footF.y, 4.4 * s, 2.6 * s, -2 * s, 0.32, skin(0.3));
                c.FillStyle = skin(0.35);
                c.BeginPath();
                c.Ellipse(footF.x - 3 * s, footF.y - 0.5 * s, 5 * s, 1.8 * s, 0, 0, Math.PI * 2);
                c.Fill();

                // the near leg: kneeling, the shin flat along the ground behind him
                var kneeN = free == 2 ? (x: -3 * s, y: -25 * s) : (x: -6 * s, y: -4 * s);
                var footN = free == 2 ? (x: -5 * s, y: -1 * s) : (x: 16 * s, y: -2.4 * s);
                limb(c, hip.x, hip.y, kneeN.x, kneeN.y, 7.6 * s, 4.6 * s, 2.8 * s, 0.42, skin(0.08));
                limb(c, kneeN.x, kneeN.y, footN.x, footN.y, 4.4 * s, 2.4 * s, -2.2 * s, 0.35, skin(0.12));
                if (free == 2)
                {
                    c.FillStyle = skin(0.12);
                    c.BeginPath();
                    c.Ellipse(footN.x - 3 * s, footN.y - 0.5 * s, 5 * s, 1.8 * s, 0, 0, Math.PI * 2);
                    c.Fill();
                }

                // the torso: a titan's, ribs pulled open by the raised arms
                var torso = c.CreateLinearGradient(sh.x - 14 * s, 0, sh.x + 14 * s, 0);
                torso.AddColorStop(0, skin(-0.1));
                torso.AddColorStop(0.6, skin(0.05));
                torso.AddColorStop(1, skin(0.35));
                c.FillStyle = torso;
                c.BeginPath();
                c.MoveTo(hip.x - 9 * s, hip.y + 2 * s);
                c.QuadraticCurveTo(hip.x - 12 * s, hip.y - 14 * s, sh.x - 13 * s, sh.y + 12 * s); // flank, chest
                c.QuadraticCurveTo(sh.x - 15 * s, sh.y + 2 * s, shN.x - 2 * s, shN.y - 3 * s); // pectoral to armpit
                c.QuadraticCurveTo(sh.x, sh.y - 6 * s, shF.x + 3 * s, shF.y - 2 * s); // trapezius
                c.QuadraticCurveTo(sh.x + 14 * s, sh.y + 12 * s, hip.x + 10 * s, hip.y - 6 * s); // lat, back
                c.QuadraticCurveTo(hip.x + 11 * s, hip.y, hip.x + 8 * s, hip.y + 3 * s);
                c.ClosePath();
                c.Fill();
                // anatomy
                atop(c, () =>
                {
                    ink(c, 0.45, 0.9 * s);
                    // pectoral's lower edge
                    c.BeginPath();
                    c.MoveTo(sh.x - 13 * s, sh.y + 10 * s);
                    c.QuadraticCurveTo(sh.x - 6 * s, sh.y + 14 * s, sh.x + 1 * s, sh.y + 9 * s);
                    c.Stroke();
                    // the ribs, standing out of a body stretched by its arms
                    ink(c, 0.32, 0.7 * s);
                    for (int i = 0; i < 4; i++)
                    {
                        double ry = sh.y + 15 * s + i * 3.4 * s;
                        c.BeginPath();
                        c.MoveTo(sh.x - 12 * s + i * 0.6 * s, ry);
                        c.QuadraticCurveTo(sh.x - 5 * s, ry - 2 * s, sh.x + 2 * s, ry + 1.5 * s);
                        c.Stroke();
                    }
                    // the belly: a long line and the bands across it
                    ink(c, 0.38, 0.8 * s);
                    c.BeginPath();
                    c.MoveTo(sh.x - 6 * s, sh.y + 13 * s);
                    c.QuadraticCurveTo(hip.x - 8 * s, hip.y - 10 * s, hip.x - 5 * s, hip.y);
                    foreach (double k in new[] { 0.25, 0.5, 0.75 })
                    {
                        double by = sh.y + 13 * s + (hip.y - sh.y - 13 * s) * k;
                        c.MoveTo(sh.x - 11 * s + k * 1 * s, by);
                        c.QuadraticCurveTo(sh.x - 7 * s, by - 1.4 * s, sh.x - 2 * s, by + 0.6 * s);
                    }
                    // the oblique into the hip
                    c.MoveTo(sh.x + 2 * s, sh.y + 20 * s);
                    c.QuadraticCurveTo(hip.x - 2 * s, hip.y - 8 * s, hip.x - 8 * s, hip.y + 1 * s);
                    c.Stroke();
                    // serratus
                    ink(c, 0.35, 0.7 * s);
                    c.BeginPath();
                    for (int i = 0; i < 3; i++)
                    {
                        c.MoveTo(sh.x - 13 * s, sh.y + 8 * s + i * 3 * s);
                        c.LineTo(sh.x - 9 * s, sh.y + 10 * s + i * 3 * s);
                    }
                    c.Stroke();
                    // light on the chest and along the flank
                    shine(c, 0.22, 2 * s);
                    c.BeginPath();
                    c.MoveTo(sh.x - 12 * s, sh.y + 4 * s);
                    c.QuadraticCurveTo(sh.x - 8 * s, sh.y + 2 * s, sh.x - 3 * s, sh.y + 4 * s);
                    c.Stroke();
                });

                // the wound under the ribs on his near side: torn, wet, never closing
                var wd = (x: sh.x - 4 * s, y: sh.y + 22 * s);
                // with the eagle gone the wound closes, and leaves a scar
                double open = look.healed ? 0.16 : feeding ? 1 : eagle.phase == "leave" ? 0.9 : 0.6;
                var wg = c.CreateRadialGradient(wd.x, wd.y, 0, wd.x, wd.y, 7 * s);
                wg.AddColorStop(0, "#1c0202");
                wg.AddColorStop(0.45, "#5e0906");
                wg.AddColorStop(0.8, "rgba(120, 18, 12, 0.85)");
                wg.AddColorStop(1, "rgba(130, 30, 20, 0)");
                c.FillStyle = wg;
                c.BeginPath();
                c.Ellipse(wd.x, wd.y, 5 * s * open, 7.5 * s * open, 0.35, 0, Math.PI * 2);
                c.Fill();
                // torn edges
                c.StrokeStyle = "rgba(170, 70, 60, 0.7)";
                c.LineWidth = 0.6 * s;
                c.BeginPath();
                for (int i = 0; i < 7; i++)
                {
                    double a = (i / 7.0) * Math.PI * 2;
                    c.MoveTo(wd.x + Math.Cos(a) * 3.6 * s * open, wd.y + Math.Sin(a) * 5.4 * s * open);
                    c.LineTo(wd.x + Math.Cos(a + 0.2) * 5 * s * open, wd.y + Math.Sin(a + 0.2) * 7 * s * open);
                }
                c.Stroke();
                // blood down the belly and the thigh
                c.StrokeStyle = look.healed ? "rgba(0, 0, 0, 0)" : "rgba(115, 10, 6, 0.9)";
                c.LineWidth = 1.3 * s;
                c.BeginPath();
                c.MoveTo(wd.x - 1 * s, wd.y + 5 * s);
                c.BezierCurveTo(wd.x + 1 * s, wd.y + 12 * s, wd.x - 2 * s, hip.y - 4 * s, wd.x, hip.y + 6 * s);
                c.MoveTo(wd.x + 3 * s, wd.y + 6 * s);
                c.QuadraticCurveTo(wd.x + 5 * s, wd.y + 12 * s, wd.x + 4 * s, wd.y + 16 * s);
                c.Stroke();

                // a torn loincloth
                c.FillStyle = "#4d1712";
                c.BeginPath();
                c.MoveTo(hip.x - 10 * s, hip.y - 4 * s);
                c.QuadraticCurveTo(hip.x, hip.y - 6 * s, hip.x + 10 * s, hip.y - 5 * s);
                c.LineTo(hip.x + 8 * s, hip.y + 8 * s);
                c.LineTo(hip.x + 3 * s, hip.y + 5 * s);
                c.LineTo(hip.x - 1 * s, hip.y + 11 * s);
                c.LineTo(hip.x - 5 * s, hip.y + 6 * s);
                c.LineTo(hip.x - 11 * s, hip.y + 9 * s);
                c.ClosePath();
                c.Fill();
                atop(c, () =>
                {
                    c.StrokeStyle = "rgba(0, 0, 0, 0.35)";
                    c.LineWidth = 0.7 * s;
                    c.BeginPath();
                    foreach (double fx in new double[] { -6, -1, 4 })
                    {
                        c.MoveTo(hip.x + fx * s, hip.y - 4 * s);
                        c.LineTo(hip.x + (fx + 1) * s, hip.y + 7 * s);
                    }
                    c.Stroke();
                });

                // the near arm, up to the chain: deltoid, biceps, forearm, a fist on the links
                limb(c, shN.x, shN.y, elN.x, elN.y, 5.2 * s, 3.8 * s, 2 * s, 0.45, skin(0));
                limb(c, elN.x, elN.y, wrN.x, wrN.y, 3.8 * s, 2.5 * s, 1.4 * s, 0.3, skin(0.02));
                atop(c, () =>
                {
                    ink(c, 0.35, 0.7 * s);
                    along(c, shN.x, shN.y, elN.x, elN.y, 0, 0.2, 0.85, 1 * s);
                    along(c, elN.x, elN.y, wrN.x, wrN.y, 0.6 * s, 0.1, 0.6, 0.8 * s);
                    shine(c, 0.2, 1.4 * s);
                    along(c, shN.x, shN.y, elN.x, elN.y, 2.6 * s, 0.25, 0.75, 1 * s);
                });
                for (int wi = 0; wi < 2; wi++)
                {
                    var w = wi == 0 ? wrN : wrF;
                    c.FillStyle = skin(0.1);
                    c.BeginPath();
                    c.Ellipse(w.x, w.y - 2 * s, 3 * s, 2.6 * s, 0, 0, Math.PI * 2);
                    c.Fill();
                    // the iron cuff
                    c.FillStyle = "#2f3136";
                    c.BeginPath();
                    c.Ellipse(w.x, w.y + 1.5 * s, 3.8 * s, 2.2 * s, 0.2, 0, Math.PI * 2);
                    c.Fill();
                    c.StrokeStyle = "rgba(200, 200, 210, 0.25)";
                    c.LineWidth = 0.6 * s;
                    c.Stroke();
                }

                // the neck, straining
                var hd = (
                    x: sh.x - 4 * s + writhe * 1.5 * s,
                    y: sh.y - 12 * s + slack * 2 * s);
                limb(c, sh.x - 1 * s, sh.y, hd.x + 1 * s, hd.y + 4 * s, 5 * s, 3.8 * s, 1 * s, 0.5, skin(0.05));
                // the head: thrown back in a scream while it feeds, hung forward after
                c.Save();
                c.Translate(hd.x, hd.y);
                if (!feeding)
                {
                    // turning: the head narrows through the turn rather than snapping round
                    double tn = Math.Max(-1, Math.Min(1, gaze.turn));
                    c.Scale(Js.sign(tn == 0 || double.IsNaN(tn) ? 1 : tn) * Math.Max(0.28, Math.Abs(tn)), 1);
                }
                c.Rotate(feeding ? -0.45 + writhe * 0.15 : 0.14 * slack + gaze.tilt);
                double rr = 5.6 * s;
                // hair, long and matted, falling behind
                c.FillStyle = "#231c19";
                c.BeginPath();
                c.MoveTo(-0.6 * rr, -1.05 * rr);
                c.QuadraticCurveTo(0.8 * rr, -1.5 * rr, 1.4 * rr, -0.4 * rr);
                c.QuadraticCurveTo(1.9 * rr, 0.8 * rr, 1.5 * rr, 2.2 * rr);
                c.LineTo(0.6 * rr, 1.2 * rr);
                c.QuadraticCurveTo(0.2 * rr, -0.2 * rr, -0.6 * rr, -1.05 * rr);
                c.Fill();
                profile(c, 0, 0, rr, skin(0), feeding ? 0.9 + writhe * 0.3 : 0.1);
                face(c, 0, 0, rr, pain: feeding ? 1 : 0.6, shut: feeding ? 1 : blink(t, 2.3), open: feeding ? 0.9 : 0.1);
                // the beard: heavy, curling, over the jaw and down the throat
                c.FillStyle = "#2a211d";
                c.BeginPath();
                c.MoveTo(-0.92 * rr, 0.62 * rr);
                c.QuadraticCurveTo(-1.2 * rr, 1.6 * rr, -0.4 * rr, 2.1 * rr);
                c.QuadraticCurveTo(0.2 * rr, 1.9 * rr, 0.6 * rr, 0.7 * rr);
                c.QuadraticCurveTo(0.2 * rr, 0.9 * rr, -0.4 * rr, 0.6 * rr);
                c.ClosePath();
                c.Fill();
                // hair over the brow
                c.BeginPath();
                c.MoveTo(-0.8 * rr, -0.5 * rr);
                c.QuadraticCurveTo(-0.6 * rr, -1.2 * rr, 0.3 * rr, -1.15 * rr);
                c.QuadraticCurveTo(-0.2 * rr, -0.9 * rr, -0.8 * rr, -0.5 * rr);
                c.Fill();
                strands(
                    c,
                    new[]
                    {
                        new[] { -0.3 * rr, -1.1 * rr, 1.2 * rr, 0.6 * rr },
                        new[] { 0.2 * rr, -1.2 * rr, 1.6 * rr, 1.4 * rr },
                        new[] { 0.6 * rr, -0.9 * rr, 1.4 * rr, 2 * rr },
                        new[] { -0.8 * rr, 0.9 * rr, -0.5 * rr, 1.9 * rr },
                        new[] { -0.4 * rr, 0.9 * rr, -0.1 * rr, 1.8 * rr },
                    },
                    "rgba(120, 100, 90, 0.35)",
                    0.4 * s);
                c.Restore();

                // the bird
                if (eagle.phase != "away")
                {
                    var perch = (x: hip.x + 4 * s, y: hip.y - 8 * s);
                    double ex = perch.x;
                    double ey = perch.y;
                    var pose = new EaglePose { spread = 0.5, flap = 0, peck = 0, perched = true };
                    if (eagle.phase == "arrive")
                    {
                        double k = Math.Min(1, eagle.t / 2.2);
                        double e = 1 - (1 - k) * (1 - k);
                        ex = perch.x + (1 - e) * 240 * s;
                        ey = perch.y - (1 - e) * 170 * s;
                        pose = new EaglePose { spread = 1, flap = Math.Sin(t * 7), peck = 0, perched = k > 0.9 };
                    }
                    else if (eagle.phase == "leave")
                    {
                        double k = Math.Min(1, eagle.t / 2.2);
                        ex = perch.x + k * k * 280 * s;
                        ey = perch.y - k * 210 * s;
                        pose = new EaglePose { spread = 1, flap = Math.Sin(t * 8), peck = 0, perched = false };
                    }
                    else
                    {
                        double cy = (((eagle.t * 1.4) % 1) + 1) % 1;
                        double peck = cy < 0.3 ? cy / 0.3 : Math.Max(0, 1 - (cy - 0.3) / 0.45);
                        pose = new EaglePose { spread = 0.45 + Math.Sin(eagle.t * 2.3) * 0.12, flap = 0, peck = peck, perched = true };
                    }
                    drawEagle(c, ex, ey, s * 1.4, pose);
                    if (feeding && pose.peck < 0.25)
                    {
                        c.FillStyle = "rgba(125, 12, 8, 0.9)";
                        for (int i = 0; i < 3; i++)
                        {
                            double p = (((t * 2.2 + i * 0.33) % 1) + 1) % 1;
                            c.BeginPath();
                            c.Arc(wd.x + (i - 1) * 2 * s, wd.y + p * 24 * s, 0.9 * s, 0, Math.PI * 2);
                            c.Fill();
                        }
                    }
                }
            });
        }

        /// <summary>one feather: a long tapered vane with its shaft, pointing along `ang`</summary>
        static void feather(Ctx c, double x, double y, double ang, double len, double wid, Style fill, Style edge,
            double bend = 0.12, bool notch = false)
        {
            c.Save();
            c.Translate(x, y);
            c.Rotate(ang);
            c.FillStyle = fill;
            c.BeginPath();
            c.MoveTo(0, -wid * 0.35);
            c.QuadraticCurveTo(
                len * 0.45,
                -wid * 0.62 - len * bend * 0.3,
                len * 0.86,
                -wid * (notch ? 0.18 : 0.38) - len * bend * 0.4);
            c.QuadraticCurveTo(len * 1.02, -len * bend * 0.45, len * 0.97, wid * 0.12 - len * bend * 0.4);
            c.QuadraticCurveTo(len * 0.5, wid * 0.62 - len * bend * 0.2, 0, wid * 0.35);
            c.ClosePath();
            c.Fill();
            c.StrokeStyle = edge;
            c.LineWidth = Math.Max(0.25, wid * 0.09);
            c.Stroke();
            // the shaft, pale, a little off-centre toward the leading edge
            c.StrokeStyle = "rgba(196, 170, 130, 0.35)";
            c.LineWidth = Math.Max(0.2, wid * 0.07);
            c.BeginPath();
            c.MoveTo(0, -wid * 0.08);
            c.QuadraticCurveTo(len * 0.5, -wid * 0.12 - len * bend * 0.28, len * 0.9, -len * bend * 0.42);
            c.Stroke();
            c.Restore();
        }

        class EaglePose
        {
            public double spread;
            public double flap;
            public double peck;
            public bool perched;
        }

        static class EAGLE
        {
            public const string flight = "#1f150e";
            public const string flightEdge = "rgba(8, 5, 3, 0.7)";
            public const string covert = "#4b321e";
            public const string covertPale = "#8a6a42";
            public const string body = "#3a2717";
            public const string chest = "#56391f";
            public const string gold = "#b98a45";
            public const string goldDeep = "#8c6430";
        }

        /// <summary>
        /// One wing, built feather by feather in its own frame: the leading edge runs
        /// along +x from the shoulder (0,0) to the tip, and the feathers trail toward
        /// +y. Ten secondaries hang from the arm; seven primaries fan from the hand,
        /// slotted apart at their tips like fingers; over their roots, three ranks of
        /// coverts, the middle rank with the pale tawny bar of a golden eagle.
        /// </summary>
        static void eagleWing(Ctx c, double span, double chord, double open, bool far)
        {
            string f(string col) { return far ? shade(col, 0.72) : col; }
            double wrist = span * 0.46;
            // primaries: from the hand, fanning from trailing to pointing out along the span
            for (int i = 6; i >= 0; i--)
            {
                double u = wrist + (span * 0.2 * i) / 6;
                double ang = 1.35 - (i / 6.0) * (1.05 + open * 0.3);
                double len = span * (0.34 + i * 0.035) * (0.9 + open * 0.15);
                feather(
                    c,
                    u,
                    -span * 0.04 * (i / 6.0),
                    ang,
                    len,
                    chord * 0.24,
                    f(EAGLE.flight),
                    EAGLE.flightEdge,
                    0.1,
                    i > 2);
            }
            // secondaries: a row hanging from the arm, slanting back toward the body
            for (int i = 0; i < 10; i++)
            {
                double u = (wrist * (i + 0.5)) / 10;
                double len = chord * (1 - Math.Abs(i - 4.5) * 0.015);
                feather(c, u, 0, 1.75 - i * 0.012, len, chord * 0.26, f(EAGLE.flight), EAGLE.flightEdge, 0.06);
            }
            // greater coverts
            for (int i = 0; i < 12; i++)
            {
                double u = ((wrist + span * 0.08) * (i + 0.5)) / 12;
                feather(c, u, -chord * 0.04, 1.7, chord * 0.5, chord * 0.22, f(EAGLE.covert), "rgba(20, 12, 6, 0.6)", 0.05);
            }
            // median coverts: the pale bar
            for (int i = 0; i < 11; i++)
            {
                double u = (wrist * (i + 0.6)) / 11;
                feather(c, u, -chord * 0.1, 1.65, chord * 0.32, chord * 0.2, f(EAGLE.covertPale), "rgba(40, 26, 12, 0.55)", 0.04);
            }
            // lesser coverts: small scallops along the leading edge
            for (int i = 0; i < 12; i++)
            {
                double u = (wrist * (i + 0.5)) / 12;
                feather(c, u, -chord * 0.16, 1.6, chord * 0.2, chord * 0.18, f(EAGLE.covert), "rgba(20, 12, 6, 0.5)", 0.02);
            }
            // primary coverts over the hand
            for (int i = 0; i < 5; i++)
            {
                double u = wrist + (span * 0.16 * i) / 5;
                feather(c, u, -chord * 0.1, 1.2 - i * 0.12, chord * 0.42, chord * 0.2, f(EAGLE.covert), "rgba(20, 12, 6, 0.55)", 0.05);
            }
            // the leading edge itself: a smooth, dark, rounded bone-line
            c.StrokeStyle = f("#2a1b10");
            c.LineWidth = chord * 0.16;
            c.LineCap = LineCap.Round;
            c.BeginPath();
            c.MoveTo(0, -chord * 0.12);
            c.QuadraticCurveTo(wrist * 0.6, -chord * 0.26, wrist + span * 0.1, -chord * 0.12);
            c.Stroke();
        }

        static readonly Dictionary<string, string> shadeCache = new Dictionary<string, string>();
        static string shade(string hex, double k)
        {
            string key = hex + "|" + Js.str(k);
            string res;
            if (shadeCache.TryGetValue(key, out res)) return res;
            int n = Convert.ToInt32(hex.Substring(1), 16);
            double r = ((n >> 16) & 255) * k;
            double g = ((n >> 8) & 255) * k;
            double b = (n & 255) * k;
            res = "rgb(" + Js.ToInt32(r) + ", " + Js.ToInt32(g) + ", " + Js.ToInt32(b) + ")";
            shadeCache[key] = res;
            return res;
        }

        /// <summary>
        /// A golden eagle, seen from the side, facing left, feet at (x, y). On its
        /// prey it mantles: both wings raised and arched over its back, the flight
        /// feathers hanging like a cloak, tail fanned down for balance, head driven
        /// down to tear. In the air its wings open wide with the primaries splayed.
        /// </summary>
        static void drawEagle(Ctx c, double x, double y, double s, EaglePose p)
        {
            c.Save();
            c.Translate(x, y);
            c.LineJoin = LineJoin.Round;
            bool perched = p.perched;
            // the body's anchor points
            var back = (x: 5 * s, y: (perched ? -22 : -14) * s);
            var chest = (x: -5 * s, y: (perched ? -15 : -12) * s);
            var rump = (x: 14 * s, y: (perched ? -12 : -12) * s);
            var wingAt = (x: 3 * s, y: back.y + 2 * s);

            // ---- the far wing, raised behind
            c.Save();
            c.Translate(wingAt.x + 1.5 * s, wingAt.y - 1 * s);
            c.Rotate(perched ? -1.05 - p.spread * 0.25 : -1.45 + p.flap * 0.9);
            eagleWing(c, (perched ? 30 : 44) * s, (perched ? 13 : 14) * s, perched ? p.spread : 1, true);
            c.Restore();

            // ---- the tail: twelve broad feathers, fanned, banded dark at the tips
            double tailAng = perched ? 0.62 : 0.1;
            for (int i = 0; i < 9; i++)
            {
                double a = tailAng + (i - 4) * (perched ? 0.1 : 0.06);
                feather(c, rump.x - 2 * s, rump.y, a, 14 * s, 5 * s, i % 2 != 0 ? "#241810" : "#2b1d13", "rgba(10, 6, 4, 0.6)", 0.02);
            }
            c.StrokeStyle = "rgba(14, 9, 6, 0.55)";
            c.LineWidth = 2.2 * s;
            c.BeginPath();
            c.Arc(rump.x - 2 * s, rump.y, 12 * s, tailAng - 0.42, tailAng + 0.42);
            c.Stroke();

            // ---- legs: shaggy feathered trousers, then bare yellow toes and black talons
            if (perched)
            {
                c.FillStyle = EAGLE.body;
                c.BeginPath();
                c.MoveTo(-2 * s, -15 * s);
                c.QuadraticCurveTo(-6 * s, -8 * s, -3 * s, -3 * s);
                // the ragged hem of the trousers
                for (int i = 0; i < 5; i++) c.LineTo((-2 + i * 1.6) * s, (i % 2 != 0 ? -4.2 : -2.6) * s);
                c.QuadraticCurveTo(7 * s, -9 * s, 6 * s, -16 * s);
                c.ClosePath();
                c.Fill();
                c.StrokeStyle = "rgba(150, 110, 70, 0.3)";
                c.LineWidth = 0.45 * s;
                c.BeginPath();
                for (int i = 0; i < 6; i++)
                {
                    c.MoveTo((-3 + i * 1.6) * s, -12 * s + (i % 2) * s);
                    c.LineTo((-3.6 + i * 1.6) * s, -4.6 * s);
                }
                c.Stroke();
                // toes, scaled and yellow, gripping
                c.StrokeStyle = "#d1a634";
                c.LineCap = LineCap.Round;
                c.LineWidth = 1.5 * s;
                c.BeginPath();
                c.MoveTo(0, -3 * s);
                c.QuadraticCurveTo(-3 * s, -2 * s, -5 * s, 0);
                c.MoveTo(0.5 * s, -3 * s);
                c.QuadraticCurveTo(-1 * s, -1 * s, -1.8 * s, 0.8 * s);
                c.MoveTo(1.5 * s, -3 * s);
                c.QuadraticCurveTo(3.5 * s, -2 * s, 4 * s, 0.4 * s);
                c.Stroke();
                c.StrokeStyle = "rgba(120, 80, 20, 0.6)";
                c.LineWidth = 0.3 * s;
                c.BeginPath();
                for (int i = 0; i < 4; i++)
                {
                    c.MoveTo((-1 - i * 1) * s, (-2.6 + i * 0.5) * s);
                    c.LineTo((-1.4 - i * 1) * s, (-1.6 + i * 0.5) * s);
                }
                c.Stroke();
                // talons: long, black, curved, sunk into him
                c.StrokeStyle = "#0e0b0a";
                c.LineWidth = 0.9 * s;
                c.BeginPath();
                c.MoveTo(-5 * s, 0);
                c.QuadraticCurveTo(-6.6 * s, 0.6 * s, -6.2 * s, 2.4 * s);
                c.MoveTo(-1.8 * s, 0.8 * s);
                c.QuadraticCurveTo(-2.8 * s, 1.8 * s, -2.2 * s, 3 * s);
                c.MoveTo(4 * s, 0.4 * s);
                c.QuadraticCurveTo(5.2 * s, 1.4 * s, 4.4 * s, 2.8 * s);
                c.Stroke();
            }
            else
            {
                // in the air: legs drawn up, talons forward and open
                c.FillStyle = EAGLE.body;
                c.BeginPath();
                c.Ellipse(2 * s, -8 * s, 4 * s, 3 * s, 0.4, 0, Math.PI * 2);
                c.Fill();
                c.StrokeStyle = "#d1a634";
                c.LineWidth = 1.4 * s;
                c.BeginPath();
                c.MoveTo(0, -6 * s);
                c.LineTo(-4 * s, -3 * s);
                c.Stroke();
                c.StrokeStyle = "#0e0b0a";
                c.LineWidth = 0.8 * s;
                c.BeginPath();
                c.MoveTo(-4 * s, -3 * s);
                c.QuadraticCurveTo(-6 * s, -2 * s, -5.6 * s, 0);
                c.Stroke();
            }

            // ---- the body: a deep, heavy chest, the back dark
            var bg = c.CreateLinearGradient(0, back.y - 2 * s, 0, chest.y + 8 * s);
            bg.AddColorStop(0, "#2a1c11");
            bg.AddColorStop(0.5, EAGLE.body);
            bg.AddColorStop(1, EAGLE.chest);
            c.FillStyle = bg;
            c.BeginPath();
            c.MoveTo(chest.x - 2 * s, chest.y - 4 * s);
            c.QuadraticCurveTo(back.x - 8 * s, back.y - 2 * s, back.x, back.y);
            c.QuadraticCurveTo(rump.x + 1 * s, back.y + 3 * s, rump.x + 1 * s, rump.y);
            c.QuadraticCurveTo(rump.x - 2 * s, rump.y + 5 * s, 6 * s, -11 * s);
            c.QuadraticCurveTo(-1 * s, -6 * s, chest.x - 1 * s, chest.y + 5 * s);
            c.QuadraticCurveTo(chest.x - 5 * s, chest.y + 1 * s, chest.x - 2 * s, chest.y - 4 * s);
            c.ClosePath();
            c.Fill();
            // breast and flank feathering: short overlapping scallops, streaked
            atop(c, () =>
            {
                c.LineWidth = 0.45 * s;
                for (int r = 0; r < 4; r++)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        double fx = chest.x + (i * 3.2 + (r % 2) * 1.6) * s;
                        double fy = chest.y - 1 * s + r * 2.6 * s + i * 0.6 * s;
                        c.StrokeStyle = "rgba(160, 118, 72, 0.32)";
                        c.BeginPath();
                        c.MoveTo(fx - 1.5 * s, fy);
                        c.QuadraticCurveTo(fx, fy + 1.4 * s, fx + 1.5 * s, fy);
                        c.Stroke();
                        c.StrokeStyle = "rgba(14, 8, 4, 0.35)";
                        c.BeginPath();
                        c.MoveTo(fx, fy - 0.6 * s);
                        c.LineTo(fx + 0.2 * s, fy + 0.8 * s);
                        c.Stroke();
                    }
                }
            });

            // ---- neck and head: down into the prey while it feeds
            double hx = (perched ? -11 : -13) * s - p.peck * 2.5 * s;
            double hy = (perched ? -14 : -15) * s + p.peck * 5 * s;
            double ha = perched ? 0.35 + p.peck * 0.35 : 0;
            c.FillStyle = EAGLE.body;
            c.BeginPath();
            c.MoveTo(chest.x + 1 * s, back.y + 1 * s);
            c.QuadraticCurveTo(hx + 3 * s, hy - 5 * s, hx + 1 * s, hy - 3.2 * s);
            c.LineTo(hx + 1.6 * s, hy + 3 * s);
            c.QuadraticCurveTo(chest.x - 2 * s, chest.y + 1 * s, chest.x - 1 * s, chest.y + 3 * s);
            c.ClosePath();
            c.Fill();
            // golden hackles on the nape: pointed lanceolate feathers, not a cap
            for (int i = 0; i < 9; i++)
            {
                double k = i / 8.0;
                double bx = hx + 1 * s + k * (back.x - 2 * s - hx);
                double by = hy - 3 * s + k * (back.y + 1 * s - hy + 3 * s) + Math.Sin(k * 3) * s;
                feather(c, bx, by, 0.5 + k * 0.3, (3.6 - k * 1.2) * s, 1.4 * s,
                    i % 2 != 0 ? EAGLE.gold : EAGLE.goldDeep, "rgba(60, 36, 12, 0.45)", 0.08);
            }
            c.Save();
            c.Translate(hx, hy);
            c.Rotate(-ha);
            // the skull: flat-crowned, small for the body
            c.FillStyle = "#3d2a19";
            c.BeginPath();
            c.MoveTo(3.4 * s, 0.8 * s);
            c.QuadraticCurveTo(3.2 * s, -3.2 * s, -0.4 * s, -3.4 * s);
            c.QuadraticCurveTo(-3 * s, -3.2 * s, -3.6 * s, -1.2 * s);
            c.LineTo(-3.4 * s, 1.4 * s);
            c.QuadraticCurveTo(-1 * s, 3.2 * s, 2.4 * s, 2.8 * s);
            c.ClosePath();
            c.Fill();
            // golden crown and nape
            c.FillStyle = EAGLE.gold;
            c.BeginPath();
            c.MoveTo(-0.6 * s, -3.3 * s);
            c.QuadraticCurveTo(2.8 * s, -3.4 * s, 3.4 * s, 0.2 * s);
            c.QuadraticCurveTo(2 * s, -1.4 * s, -0.2 * s, -2.2 * s);
            c.ClosePath();
            c.Fill();
            // the beak: grey-horn at the base, black at the hook, a deep gape
            var bk = c.CreateLinearGradient(-3.6 * s, 0, -8.6 * s, 0);
            bk.AddColorStop(0, "#6e675e");
            bk.AddColorStop(0.55, "#3c3631");
            bk.AddColorStop(1, "#121010");
            c.FillStyle = bk;
            c.BeginPath();
            c.MoveTo(-3.4 * s, -1.6 * s);
            c.QuadraticCurveTo(-7.2 * s, -2.4 * s, -8.6 * s, 0.2 * s); // the culmen, arched
            c.QuadraticCurveTo(-9 * s, 2.2 * s, -7.8 * s, 3.2 * s); // the hook
            c.QuadraticCurveTo(-7.9 * s, 2 * s, -7.2 * s, 1.6 * s);
            c.LineTo(-3.4 * s, 1.5 * s); // lower mandible
            c.ClosePath();
            c.Fill();
            // the cere and gape, yellow
            c.FillStyle = "#d9ae3a";
            c.BeginPath();
            c.MoveTo(-3.3 * s, -1.6 * s);
            c.QuadraticCurveTo(-4.8 * s, -1.9 * s, -5.2 * s, -0.8 * s);
            c.LineTo(-5 * s, 0.2 * s);
            c.LineTo(-3.4 * s, 0.4 * s);
            c.ClosePath();
            c.Fill();
            c.StrokeStyle = "#cf9f2e";
            c.LineWidth = 0.5 * s;
            c.BeginPath();
            c.MoveTo(-2.6 * s, 1.2 * s);
            c.LineTo(-7 * s, 1.5 * s);
            c.Stroke();
            // nostril
            c.FillStyle = "#3a2a10";
            c.BeginPath();
            c.Ellipse(-4.4 * s, -0.9 * s, 0.4 * s, 0.25 * s, 0, 0, Math.PI * 2);
            c.Fill();
            // the eye under its shelf of brow: amber, hard, with a dark pupil
            c.FillStyle = "#c78d22";
            c.BeginPath();
            c.Arc(-1.6 * s, -1 * s, 0.95 * s, 0, Math.PI * 2);
            c.Fill();
            c.FillStyle = "#070505";
            c.BeginPath();
            c.Arc(-1.85 * s, -1 * s, 0.5 * s, 0, Math.PI * 2);
            c.Fill();
            c.FillStyle = "rgba(255, 240, 210, 0.7)";
            c.BeginPath();
            c.Arc(-2 * s, -1.3 * s, 0.16 * s, 0, Math.PI * 2);
            c.Fill();
            c.FillStyle = "#1e140c";
            c.BeginPath();
            c.MoveTo(-3.6 * s, -1.8 * s);
            c.QuadraticCurveTo(-1.6 * s, -3 * s, 0.4 * s, -1.9 * s);
            c.QuadraticCurveTo(-1.4 * s, -2.1 * s, -3.6 * s, -1.8 * s);
            c.Fill();
            if (perched && p.peck > 0.35)
            {
                c.FillStyle = "rgba(130, 12, 8, 0.95)";
                c.BeginPath();
                c.Ellipse(-8.2 * s, 2.6 * s, 1.1 * s, 0.8 * s, 0.4, 0, Math.PI * 2);
                c.Fill();
                c.StrokeStyle = "rgba(110, 10, 6, 0.9)";
                c.LineWidth = 0.6 * s;
                c.BeginPath();
                c.MoveTo(-8 * s, 3 * s);
                c.QuadraticCurveTo(-8.6 * s, 5 * s, -9.4 * s, 6 * s);
                c.Stroke();
            }
            c.Restore();

            // ---- the near wing, arched over the back like a cloak
            c.Save();
            c.Translate(wingAt.x, wingAt.y);
            c.Rotate(perched ? -0.42 - p.spread * 0.22 : -1.25 + p.flap * 0.9);
            eagleWing(c, (perched ? 32 : 46) * s, (perched ? 14 : 15) * s, perched ? p.spread : 1, false);
            c.Restore();
            c.Restore();
        }
    }
}
