// Ported from src/game/zeus.ts.
/*
 * Zeus.
 *
 * He used to be a small robed figure standing beside the stone at the summit —
 * the same size as the man, which is the one thing a god should not be. Now he
 * is a colossus: a bearded, broad-chested torso rising out of a storm bank in
 * the sky over the peak, the thunderbolt drawn back in one fist, then hurled.
 *
 * Drawing a body this detailed every frame would be expensive, so each pose is
 * painted once into an offscreen sprite (body, muscle modelling, hair, beard,
 * himation, laurel, and a true rim light) and the frame only blits it. Only
 * the eyes, the bolt and the clouds — the things that move — are live.
 *
 * Design space: x right, y down, the waist at (0, 0), the raised fist near
 * y = -270. Everything is scaled by `k` when the sprite is built.
 *
 * ZeusPose is a string: "raise" | "hurl".
 */
using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>zeus.ts' <c>Pt = [number, number]</c>: p[0], p[1], or <c>var (x, y) = p</c>.</summary>
    public struct Pt
    {
        public double x, y;
        public Pt(double x, double y) { this.x = x; this.y = y; }
        public double this[int i] { get { return i == 0 ? x : y; } }
        public void Deconstruct(out double x, out double y) { x = this.x; y = this.y; }
    }

    public static class Zeus
    {
        /// <summary>a limb as a tapered capsule with a muscle belly, like the man's, but bigger</summary>
        static void limb(Ctx c, Pt a, Pt b, double wa, double wb, double belly, double at = 0.45)
        {
            double ax = a.x, ay = a.y;
            double bx = b.x, by = b.y;
            double dx = bx - ax;
            double dy = by - ay;
            double len = Js.hypot(dx, dy);
            if (len == 0 || double.IsNaN(len)) len = 1;
            double nx = -dy / len;
            double ny = dx / len;
            double mx = ax + dx * at;
            double my = ay + dy * at;
            double wm = wa + (wb - wa) * at;
            (double p0x, double p0y, double p2x, double p2y, double cx, double cy) side(double sg, double extra)
            {
                double p0x = ax + nx * wa * sg;
                double p0y = ay + ny * wa * sg;
                double p2x = bx + nx * wb * sg;
                double p2y = by + ny * wb * sg;
                double qx = mx + nx * (wm + extra) * sg;
                double qy = my + ny * (wm + extra) * sg;
                return (p0x, p0y, p2x, p2y, 2 * qx - 0.5 * (p0x + p2x), 2 * qy - 0.5 * (p0y + p2y));
            }
            var f = side(1, Math.Max(0, belly));
            var k = side(-1, Math.Max(0, -belly));
            c.BeginPath();
            c.MoveTo(f.p0x, f.p0y);
            c.QuadraticCurveTo(f.cx, f.cy, f.p2x, f.p2y);
            c.LineTo(k.p2x, k.p2y);
            c.QuadraticCurveTo(k.cx, k.cy, k.p0x, k.p0y);
            c.ClosePath();
            c.Fill();
            c.BeginPath();
            c.Arc(ax, ay, wa, 0, Math.PI * 2);
            c.Arc(bx, by, wb, 0, Math.PI * 2);
            c.Fill();
        }

        sealed class Rig
        {
            public Pt rSh, rEl, rHand, lSh, lEl, lHand;
            public double lean;
        }

        /// <summary>joint positions for each pose, in design units</summary>
        static Rig rig(string pose)
        {
            // his right arm (screen left) carries the bolt; his left rests on the hip
            return pose == "raise"
                ? new Rig
                {
                    rSh = new Pt(-70, -142),
                    rEl = new Pt(-118, -196),
                    rHand = new Pt(-104, -262),
                    lSh = new Pt(70, -142),
                    lEl = new Pt(104, -84),
                    lHand = new Pt(70, -40),
                    lean = 0,
                }
                : new Rig
                {
                    rSh = new Pt(-70, -140),
                    rEl = new Pt(-128, -114),
                    rHand = new Pt(-182, -84),
                    lSh = new Pt(70, -144),
                    lEl = new Pt(108, -98),
                    lHand = new Pt(96, -52),
                    lean = -6,
                };
        }

        /// <summary>the hand that holds the bolt, in design units, for each pose</summary>
        public static Pt zeusHand(string pose)
        {
            return rig(pose).rHand;
        }

        static readonly (double, double, double)[] CURLS =
        {
            (-22, -172, 8),
            (-16, -156, 8),
            (-6, -142, 8),
            (6, -144, 8),
            (14, -158, 8),
            (19, -174, 7),
            (-2, -128, 7),
        };
        static readonly (double, double, double)[] CROWN =
        {
            (-20, -226, 9),
            (-10, -233, 10),
            (2, -235, 10),
            (13, -230, 9),
            (-24, -214, 7),
            (19, -216, 7),
        };

        /// <summary>
        /// Every solid part of him, filled with whatever `paint` returns for it. The
        /// same call paints the shaded body and, with a flat colour, the silhouette the
        /// rim light is cut from — so the rim follows the real outline by construction.
        /// </summary>
        internal static void paintBody(Ctx c, string pose, Func<string, Style> paint)
        {
            var r = rig(pose);
            double L = r.lean;

            // hair falling behind the neck to the shoulders
            c.FillStyle = paint("hairBack");
            c.BeginPath();
            c.MoveTo(-26 + L, -222);
            c.QuadraticCurveTo(-40 + L, -186, -34 + L, -156);
            c.LineTo(34 + L, -156);
            c.QuadraticCurveTo(40 + L, -186, 26 + L, -222);
            c.ClosePath();
            c.Fill();

            // himation behind his left shoulder, hanging down his back
            c.FillStyle = paint("clothBack");
            c.BeginPath();
            c.MoveTo(50, -164);
            c.QuadraticCurveTo(100, -160, 98, -110);
            c.QuadraticCurveTo(104, -40, 92, 30);
            c.LineTo(50, 30);
            c.ClosePath();
            c.Fill();

            // the arm behind the body when it is drawn back (raise): painted first
            c.FillStyle = paint("armFar");
            if (pose == "raise")
            {
                limb(c, r.rSh, r.rEl, 22, 17, -7);
                limb(c, r.rEl, r.rHand, 17, 11, 6, 0.3);
            }

            // torso: traps out to the shoulder, lats sweeping in to the waist
            c.FillStyle = paint("torso");
            c.BeginPath();
            c.MoveTo(-18 + L, -168);
            c.QuadraticCurveTo(-50 + L, -160, -78 + L, -142);
            c.QuadraticCurveTo(-88 + L, -116, -72, -96);
            c.QuadraticCurveTo(-56, -60, -46, -24);
            c.QuadraticCurveTo(-48, 0, -50, 30);
            c.LineTo(50, 30);
            c.QuadraticCurveTo(48, 0, 46, -24);
            c.QuadraticCurveTo(56, -60, 72, -96);
            c.QuadraticCurveTo(88 + L, -116, 78 + L, -142);
            c.QuadraticCurveTo(50 + L, -160, 18 + L, -168);
            c.ClosePath();
            c.Fill();

            // deltoids: caps of muscle over the joints, wider than the arm
            c.FillStyle = paint("delt");
            c.BeginPath();
            c.Ellipse(r.rSh[0] - 4 + L, r.rSh[1] + 6, 24, 27, -0.35, 0, Math.PI * 2);
            c.Fill();
            c.BeginPath();
            c.Ellipse(r.lSh[0] + 4 + L, r.lSh[1] + 6, 24, 27, 0.35, 0, Math.PI * 2);
            c.Fill();

            // left arm, resting on the hip (or bracing for the throw)
            c.FillStyle = paint("armNear");
            limb(c, r.lSh, r.lEl, 22, 17, 7);
            limb(c, r.lEl, r.lHand, 17, 12, -6, 0.3);
            c.BeginPath();
            c.Ellipse(r.lHand[0], r.lHand[1], 13, 11, 0.4, 0, Math.PI * 2);
            c.Fill();

            // the throwing arm, out in front of him once the bolt has gone
            if (pose == "hurl")
            {
                c.FillStyle = paint("armNear");
                limb(c, r.rSh, r.rEl, 23, 17, 8);
                limb(c, r.rEl, r.rHand, 17, 11, -6, 0.3);
                // open hand, fingers spread after the release
                c.Save();
                c.Translate(r.rHand[0], r.rHand[1]);
                c.Rotate(Math.Atan2(r.rHand[1] - r.rEl[1], r.rHand[0] - r.rEl[0]));
                c.BeginPath();
                c.Ellipse(6, 0, 14, 11, 0, 0, Math.PI * 2);
                c.Fill();
                for (int i = -2; i <= 2; i++)
                {
                    c.BeginPath();
                    c.Ellipse(20, i * 5.2, 9, 2.8, i * 0.22, 0, Math.PI * 2);
                    c.Fill();
                }
                c.Restore();
            }
            else
            {
                // fist closed around the bolt: the back of the hand, four fingers wrapped
                // round the shaft with their knuckles stacked, and the thumb over them
                c.FillStyle = paint("armFar");
                double fx = r.rHand.x, fy = r.rHand.y;
                c.BeginPath();
                c.Ellipse(fx - 3, fy + 2, 11, 13, 0.15, 0, Math.PI * 2);
                c.Fill();
                for (int i = 0; i < 4; i++)
                {
                    c.BeginPath();
                    c.Ellipse(fx + 7, fy - 9 + i * 6, 5.5, 3.6, 0.2, 0, Math.PI * 2);
                    c.Fill();
                }
                c.BeginPath();
                c.Ellipse(fx + 2, fy - 12, 6, 3.4, -0.6, 0, Math.PI * 2);
                c.Fill();
            }

            // gold armbands high on both upper arms and cuffs at the wrists
            c.FillStyle = paint("gold");
            void band(Pt a, Pt b, double at, double wdt, double thick)
            {
                double x = a[0] + (b[0] - a[0]) * at;
                double y = a[1] + (b[1] - a[1]) * at;
                double ang = Math.Atan2(b[1] - a[1], b[0] - a[0]);
                c.Save();
                c.Translate(x, y);
                c.Rotate(ang);
                c.BeginPath();
                c.Rect(-thick / 2, -wdt, thick, wdt * 2);
                c.Fill();
                c.Restore();
            }
            band(r.lSh, r.lEl, 0.42, 20, 6);
            band(r.lEl, r.lHand, 0.78, 13, 7);
            if (pose == "hurl")
            {
                band(r.rSh, r.rEl, 0.42, 21, 6);
                band(r.rEl, r.rHand, 0.78, 13, 7);
            }
            else
            {
                band(r.rSh, r.rEl, 0.42, 20, 6);
                band(r.rEl, r.rHand, 0.75, 12, 7);
            }

            // himation: over the left shoulder, down across the left side of the chest
            // and gathered at the right hip, so the right pectoral and the abdomen show
            c.FillStyle = paint("cloth");
            c.BeginPath();
            c.MoveTo(34 + L, -168);
            c.QuadraticCurveTo(72 + L, -174, 96 + L, -150);
            c.QuadraticCurveTo(110, -100, 98, -46);
            c.QuadraticCurveTo(92, -6, 94, 34);
            c.LineTo(-66, 34);
            c.QuadraticCurveTo(-60, 12, -48, 2);
            c.QuadraticCurveTo(-10, -18, 26, -46);
            c.QuadraticCurveTo(52, -78, 50, -116);
            c.QuadraticCurveTo(46, -146, 34 + L, -168);
            c.ClosePath();
            c.Fill();

            // neck
            c.FillStyle = paint("torso");
            c.BeginPath();
            c.MoveTo(-16 + L, -186);
            c.LineTo(16 + L, -186);
            c.LineTo(24 + L, -156);
            c.LineTo(-24 + L, -156);
            c.ClosePath();
            c.Fill();

            // head: a heavy skull with a high forehead
            c.FillStyle = paint("head");
            c.BeginPath();
            c.Ellipse(-2 + L, -207, 21, 27, -0.05, 0, Math.PI * 2);
            c.Fill();
            // ears, just showing at the sides
            c.BeginPath();
            c.Ellipse(-21 + L, -204, 4, 7, 0.1, 0, Math.PI * 2);
            c.Ellipse(17 + L, -204, 4, 7, -0.1, 0, Math.PI * 2);
            c.Fill();

            // beard: from the cheekbones down to a point on the chest, in heavy curls
            c.FillStyle = paint("beard");
            c.BeginPath();
            c.MoveTo(-21 + L, -200);
            c.QuadraticCurveTo(-30 + L, -168, -18 + L, -146);
            c.QuadraticCurveTo(-8 + L, -128, -2 + L, -120);
            c.QuadraticCurveTo(5 + L, -128, 14 + L, -146);
            c.QuadraticCurveTo(26 + L, -168, 17 + L, -200);
            c.QuadraticCurveTo(12 + L, -186, -2 + L, -186);
            c.QuadraticCurveTo(-16 + L, -186, -21 + L, -200);
            c.ClosePath();
            c.Fill();
            foreach (var (x, y, rr) in CURLS)
            {
                c.BeginPath();
                c.Arc(x + L, y, rr, 0, Math.PI * 2);
                c.Fill();
            }
            // moustache, sweeping down at the corners of the mouth
            c.BeginPath();
            c.MoveTo(-17 + L, -184);
            c.QuadraticCurveTo(-10 + L, -194, -2 + L, -191);
            c.QuadraticCurveTo(6 + L, -194, 13 + L, -184);
            c.QuadraticCurveTo(4 + L, -186, -2 + L, -186);
            c.QuadraticCurveTo(-8 + L, -186, -17 + L, -184);
            c.Fill();

            // hair: curls over the crown, framing the forehead and the temples
            c.FillStyle = paint("hair");
            foreach (var (x, y, rr) in CROWN)
            {
                c.BeginPath();
                c.Arc(x + L, y, rr, 0, Math.PI * 2);
                c.Fill();
            }
            // long locks falling from the temples down onto the shoulders, wavy
            for (int si = 0; si < 2; si++)
            {
                double side = si == 0 ? -1 : 1;
                for (int k = 0; k < 3; k++)
                {
                    double x0 = side * (20 + k * 3) + L;
                    c.BeginPath();
                    c.MoveTo(x0 - side * 4, -206 + k * 2);
                    c.BezierCurveTo(
                        x0 + side * 8,
                        -190 + k * 3,
                        x0 - side * 2,
                        -176,
                        x0 + side * (10 + k * 2),
                        -158 + k * 4);
                    c.BezierCurveTo(x0 + side * 4, -164, x0 + side * 5, -184, x0 - side * 1, -204 + k * 2);
                    c.ClosePath();
                    c.Fill();
                }
            }
        }

        static readonly (double, double, double)[] FLICKS =
        {
            (-20, -226, 6),
            (-10, -233, 7),
            (2, -235, 7),
            (13, -230, 6),
            (-22, -172, 5),
            (-16, -156, 5),
            (-6, -142, 5),
            (6, -144, 5),
            (14, -158, 5),
            (19, -174, 5),
            (-2, -128, 5),
        };
        static readonly (double, double, double)[] DARK_CURLS =
        {
            (-16, -152, 6),
            (6, -140, 6),
            (-4, -124, 5),
            (16, -166, 5),
        };

        /// <summary>muscle modelling, face and cloth folds, painted over the body with source-atop</summary>
        internal static void model(Ctx c, string pose)
        {
            var r = rig(pose);
            double L = r.lean;
            void soft(double x, double y, double rx, double ry, Col color, double rot = 0)
            {
                c.Save();
                c.Translate(x, y);
                c.Rotate(rot);
                c.Scale(1, ry / rx);
                var g = c.CreateRadialGradient(0, 0, 0, 0, 0, rx);
                g.AddColorStop(0, color);
                g.AddColorStop(1, "rgba(0,0,0,0)");
                c.FillStyle = g;
                c.BeginPath();
                c.Arc(0, 0, rx, 0, Math.PI * 2);
                c.Fill();
                c.Restore();
            }
            void groove(Pt[] pts, double w, double alpha)
            {
                c.StrokeStyle = Col.Oklch(0.2, 0.04, 300, alpha);
                c.LineWidth = w;
                c.LineCap = LineCap.Round;
                c.BeginPath();
                c.MoveTo(pts[0][0], pts[0][1]);
                if (pts.Length == 3) c.QuadraticCurveTo(pts[1][0], pts[1][1], pts[2][0], pts[2][1]);
                else for (int i = 1; i < pts.Length; i++) c.LineTo(pts[i][0], pts[i][1]);
                c.Stroke();
            }
            Col hi = "oklch(0.92 0.06 80 / 0.32)";
            Col hiSoft = "oklch(0.92 0.06 80 / 0.18)";
            Col lo = "oklch(0.18 0.04 300 / 0.35)";

            // ---- chest: the right pectoral as a mass, the left under the cloth
            soft(-30 + L, -128, 30, 18, hi, 0.2);
            groove(
                new[]
                {
                    new Pt(-62 + L, -112),
                    new Pt(-34 + L, -98),
                    new Pt(-6 + L, -110),
                },
                4,
                0.22);
            groove(
                new[]
                {
                    new Pt(-4 + L, -150),
                    new Pt(-4 + L, -100),
                },
                3,
                0.25);
            // ---- abdominals: a soft grid of masses, not lines
            for (int row = 0; row < 3; row++)
            {
                double y = -84 + row * 20;
                soft(-18, y, 11, 8, hiSoft);
                soft(4, y, 10, 8, hiSoft);
                groove(
                    new[]
                    {
                        new Pt(-28, y + 9),
                        new Pt(-16, y + 12),
                        new Pt(-4, y + 9),
                    },
                    2.5,
                    0.13);
            }
            groove(
                new[]
                {
                    new Pt(-6, -98),
                    new Pt(-6, -60),
                    new Pt(-8, -24),
                },
                2.5,
                0.14);
            // obliques and serratus: the ribs fanning into the side
            for (int i = 0; i < 3; i++)
            {
                groove(
                    new[]
                    {
                        new Pt(-60 + i * 2, -92 + i * 11),
                        new Pt(-50 + i * 2, -88 + i * 11),
                        new Pt(-42 + i * 2, -94 + i * 11),
                    },
                    2.2,
                    0.16);
            }
            soft(-52, -60, 12, 28, lo, 0.2);
            // ---- shoulders and arms: lit along the outer, sunward curve
            soft(r.rSh[0] - 4, r.rSh[1] + 4, 20, 22, hi);
            soft(r.lSh[0] + 6, r.lSh[1] + 2, 18, 20, hiSoft);
            void along(Pt a, Pt b, double f, double rx, double ry, Col col)
            {
                soft(
                    a[0] + (b[0] - a[0]) * f,
                    a[1] + (b[1] - a[1]) * f,
                    rx,
                    ry,
                    col,
                    Math.Atan2(b[1] - a[1], b[0] - a[0]));
            }
            along(r.rSh, r.rEl, 0.5, 18, 9, hi);
            along(r.lSh, r.lEl, 0.5, 16, 8, hiSoft);
            along(r.rEl, r.rHand, 0.3, 14, 7, hiSoft);
            along(r.lEl, r.lHand, 0.3, 12, 6, hiSoft);

            // ---- face
            // the brow overhangs the eyes: a band of shadow, then the lit forehead above it
            soft(-2 + L, -222, 18, 7, hiSoft);
            soft(-12 + L, -206, 8, 5, lo);
            soft(8 + L, -206, 8, 5, lo);
            // nose: a lit ridge and a shadowed side
            c.StrokeStyle = "oklch(0.88 0.06 75 / 0.55)";
            c.LineWidth = 3;
            c.LineCap = LineCap.Round;
            c.BeginPath();
            c.MoveTo(-3 + L, -212);
            c.LineTo(-4 + L, -196);
            c.Stroke();
            soft(2 + L, -198, 4, 6, lo);
            // brows: heavy, drawn together — he is not pleased
            c.StrokeStyle = "oklch(0.3 0.02 80)";
            c.LineWidth = 3.4;
            c.BeginPath();
            c.MoveTo(-19 + L, -214);
            c.QuadraticCurveTo(-11 + L, -216, -4 + L, -210);
            c.MoveTo(14 + L, -214);
            c.QuadraticCurveTo(7 + L, -216, 0 + L, -210);
            c.Stroke();
            // eyes: pale, almost lit from inside
            c.FillStyle = "oklch(0.95 0.04 85)";
            foreach (double ex in new double[] { -12, 7 })
            {
                c.BeginPath();
                c.Ellipse(ex + L, -205, 3.6, 2, 0, 0, Math.PI * 2);
                c.Fill();
            }
            c.FillStyle = "oklch(0.55 0.1 240)";
            foreach (double ex in new double[] { -13, 6 })
            {
                c.BeginPath();
                c.Arc(ex + L, -205, 1.4, 0, Math.PI * 2);
                c.Fill();
            }
            // cheekbones catching the light
            soft(-14 + L, -196, 7, 4, hiSoft);
            soft(10 + L, -196, 7, 4, hi);

            // ---- hair and beard: highlight flicks so the curls read as curls
            c.StrokeStyle = "oklch(0.97 0.02 85 / 0.5)";
            c.LineWidth = 2;
            void flick(double x, double y, double rr)
            {
                c.BeginPath();
                c.Arc(x + L, y, rr, Math.PI * 1.1, Math.PI * 1.8);
                c.Stroke();
            }
            foreach (var (x, y, rr) in FLICKS)
                flick(x, y, rr);
            c.StrokeStyle = "oklch(0.35 0.02 80 / 0.5)";
            c.LineWidth = 1.6;
            foreach (var (x, y, rr) in DARK_CURLS)
            {
                c.BeginPath();
                c.Arc(x + L, y, rr, Math.PI * 0.1, Math.PI * 0.8);
                c.Stroke();
            }

            // ---- himation: long pulls from the shoulder down to the gathered hip
            void fold(double x0, double y0, double cx, double cy, double x1, double y1, bool dark)
            {
                c.StrokeStyle = dark ? "oklch(0.3 0.03 300 / 0.45)" : "oklch(0.97 0.03 85 / 0.3)";
                c.LineWidth = dark ? 4 : 2.5;
                c.BeginPath();
                c.MoveTo(x0, y0);
                c.QuadraticCurveTo(cx, cy, x1, y1);
                c.Stroke();
            }
            fold(60, -160, 70, -80, 20, -20, true);
            fold(70, -156, 82, -80, 40, -10, false);
            fold(84, -148, 96, -70, 62, 20, true);
            fold(48, -120, 30, -50, -30, 10, false);
            fold(40, -90, 10, -30, -46, 26, true);
            fold(92, -120, 100, -40, 84, 30, false);
            // gold meander border along the cloth's front edge
            c.StrokeStyle = "oklch(0.8 0.13 82 / 0.9)";
            c.LineWidth = 4.5;
            c.BeginPath();
            c.MoveTo(34 + L, -168);
            c.QuadraticCurveTo(46, -146, 50, -116);
            c.QuadraticCurveTo(52, -78, 26, -46);
            c.QuadraticCurveTo(-10, -18, -48, 2);
            c.QuadraticCurveTo(-60, 12, -66, 34);
            c.Stroke();
            c.StrokeStyle = "oklch(0.5 0.1 70 / 0.9)";
            c.LineWidth = 1.4;
            c.SetLineDash(new double[] { 4, 5 });
            c.Stroke();
            c.SetLineDash(new double[0]);

            // ---- age and power: lines on the brow, crow's feet, collarbones, forearms
            c.StrokeStyle = "oklch(0.25 0.04 40 / 0.35)";
            c.LineWidth = 1.2;
            for (int i = 0; i < 3; i++)
            {
                c.BeginPath();
                c.MoveTo(-14 + L, -221 + i * 3.2);
                c.QuadraticCurveTo(-2 + L, -224 + i * 3.2, 10 + L, -221 + i * 3.2);
                c.Stroke();
            }
            foreach (var (ex, dir) in new (double, double)[] { (-17, -1), (12, 1) })
            {
                for (int i = 0; i < 2; i++)
                {
                    c.BeginPath();
                    c.MoveTo(ex + L, -206 + i * 2);
                    c.LineTo(ex + dir * 4 + L, -207 + i * 3.5);
                    c.Stroke();
                }
            }
            c.StrokeStyle = "oklch(0.2 0.04 300 / 0.3)";
            c.LineWidth = 2.5;
            c.BeginPath();
            c.MoveTo(-8 + L, -152);
            c.QuadraticCurveTo(-34 + L, -146, -62 + L, -150);
            c.Stroke();
            c.StrokeStyle = "oklch(0.92 0.06 80 / 0.25)";
            c.LineWidth = 2;
            c.BeginPath();
            c.MoveTo(-8 + L, -155);
            c.QuadraticCurveTo(-34 + L, -149, -62 + L, -153);
            c.Stroke();
            // forearm flexors: a line down each forearm where the muscle bellies part
            c.StrokeStyle = "oklch(0.2 0.04 300 / 0.28)";
            c.LineWidth = 2;
            foreach (var (a, b) in new (Pt, Pt)[] { (r.rEl, r.rHand), (r.lEl, r.lHand) })
            {
                c.BeginPath();
                c.MoveTo(a[0] + (b[0] - a[0]) * 0.15, a[1] + (b[1] - a[1]) * 0.15);
                c.QuadraticCurveTo(
                    a[0] + (b[0] - a[0]) * 0.45 + 3,
                    a[1] + (b[1] - a[1]) * 0.45,
                    a[0] + (b[0] - a[0]) * 0.75,
                    a[1] + (b[1] - a[1]) * 0.75);
                c.Stroke();
            }
            // beard strands: long wavy lines through the curls
            c.StrokeStyle = "oklch(0.4 0.02 80 / 0.35)";
            c.LineWidth = 1.3;
            for (int i = -3; i <= 3; i++)
            {
                c.BeginPath();
                c.MoveTo(i * 4 + L, -184);
                c.BezierCurveTo(i * 5 + 3 + L, -170, i * 3 - 3 + L, -150, i * 2 + L, -130 + Math.Abs(i) * 3);
                c.Stroke();
            }

            // ---- laurel: olive-gold leaves on a band over the brow
            for (int i = 0; i < 11; i++)
            {
                double a = Math.PI * (1.02 + i * 0.096);
                double x = Math.Cos(a) * 24 - 2 + L;
                double y = Math.Sin(a) * 20 - 214;
                c.Save();
                c.Translate(x, y);
                c.Rotate(a + Math.PI / 2 + (i % 2 != 0 ? 0.55 : -0.55));
                c.FillStyle = i % 2 != 0 ? "oklch(0.78 0.13 92)" : "oklch(0.68 0.12 110)";
                c.BeginPath();
                c.Ellipse(0, -2, 3, 7, 0, 0, Math.PI * 2);
                c.Fill();
                c.Restore();
            }
        }

        /// <summary>the thunderbolt as Greek art drew it: a spindle of zigzag flame, pointed at both ends</summary>
        public static void drawKeraunos(Ctx c, double x, double y, double len, double rot, double glow)
        {
            c.Save();
            c.Translate(x, y);
            c.Rotate(rot);
            double half = len / 2;
            void zig(double sx, double amp)
            {
                c.BeginPath();
                c.MoveTo(0, -half);
                for (int i = 1; i < 6; i++)
                {
                    double t = i / 6.0;
                    c.LineTo(sx * amp * (i % 2 != 0 ? 1 : -0.4) * Math.Sin(t * Math.PI), -half + t * len);
                }
                c.LineTo(0, half);
                c.Stroke();
            }
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            c.ShadowColor = "oklch(0.88 0.15 85)";
            c.ShadowBlur = 14 * glow;
            c.StrokeStyle = Col.Oklch(0.9, 0.13, 85, 0.5 * glow);
            c.LineWidth = len * 0.12;
            zig(1, len * 0.09);
            zig(-1, len * 0.09);
            c.StrokeStyle = "oklch(0.98 0.04 95)";
            c.LineWidth = len * 0.045;
            zig(1, len * 0.09);
            zig(-1, len * 0.09);
            c.ShadowBlur = 0;
            c.Restore();
        }
    }

    public sealed class ZeusPainter
    {
        public sealed class Sprite
        {
            public Surface cv;
            public double ox, oy, k;
        }

        private Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>the posed body as a sprite, `k` px per design unit; origin = the waist</summary>
        public Sprite sprite(string pose, double k)
        {
            string key = pose + ":" + Js.toFixed(k, 3);
            if (cache.TryGetValue(key, out var hit)) return hit;
            if (cache.Count > 6) cache.Clear();

            const double pad = 24;
            const double minX = -230;
            const double maxX = 140;
            const double minY = -300;
            const double maxY = 40;
            int w = (int)Math.Ceiling((maxX - minX) * k + pad * 2);
            int h = (int)Math.Ceiling((maxY - minY) * k + pad * 2);
            double ox = -minX * k + pad;
            double oy = -minY * k + pad;
            Surface mk() { return new Surface(w, h); }

            // 1. the shaded body
            var cv = mk();
            var c = cv.GetContext();
            c.Translate(ox, oy);
            c.Scale(k, k);
            // Lit from below by the low sun and the bolt, so the values climb toward
            // the waist, and the shadow side stays the violet of the storm.
            // Bronze lit by the low sun from his right and below; the far side of
            // every form drops into the storm's violet.
            var skin = c.CreateLinearGradient(-120, -260, 90, 20);
            skin.AddColorStop(0, "oklch(0.42 0.05 320)");
            skin.AddColorStop(0.45, "oklch(0.52 0.06 40)");
            skin.AddColorStop(1, "oklch(0.7 0.08 62)");
            var cloth = c.CreateLinearGradient(110, -170, -60, 30);
            cloth.AddColorStop(0, "oklch(0.6 0.03 290)");
            cloth.AddColorStop(0.5, "oklch(0.82 0.035 80)");
            cloth.AddColorStop(1, "oklch(0.62 0.04 300)");
            var hair = c.CreateLinearGradient(-30, -240, 30, -120);
            hair.AddColorStop(0, "oklch(0.6 0.015 80)");
            hair.AddColorStop(1, "oklch(0.86 0.02 85)");
            var fills = new Dictionary<string, Style>
            {
                { "hairBack", "oklch(0.66 0.015 80)" },
                { "clothBack", "oklch(0.42 0.03 300)" },
                { "armFar", skin },
                { "torso", skin },
                { "delt", skin },
                { "armNear", skin },
                { "cloth", cloth },
                { "head", skin },
                { "beard", hair },
                { "hair", hair },
                { "gold", "oklch(0.78 0.13 82)" },
            };
            Zeus.paintBody(c, pose, p => fills.TryGetValue(p, out var f) ? f : (Style)skin);
            c.GlobalCompositeOperation = Composite.SourceAtop;
            Zeus.model(c, pose);
            c.GlobalCompositeOperation = Composite.SourceOver;

            // 2. a true rim light: the silhouette minus itself shifted toward the light
            var sil = mk();
            var sc = sil.GetContext();
            sc.Translate(ox, oy);
            sc.Scale(k, k);
            Style white = "#fff";
            Zeus.paintBody(sc, pose, _ => white);
            void rim(double dx, double dy, string color, double blur)
            {
                var r = mk();
                var rc = r.GetContext();
                rc.DrawImage(sil, 0, 0);
                rc.GlobalCompositeOperation = Composite.SourceIn;
                rc.FillStyle = color;
                rc.FillRect(0, 0, w, h);
                rc.GlobalCompositeOperation = Composite.DestinationOut;
                rc.DrawImage(sil, dx, dy);
                c.Save();
                c.SetTransform(1, 0, 0, 1, 0, 0);
                c.GlobalCompositeOperation = Composite.SourceAtop;
                c.DrawImage(r, 0, 0);
                c.Restore();
            }
            double e = Math.Max(1.5, 3.2 * k);
            // the sun, low and to his right: a warm edge along the lower right
            rim(-e, -e * 0.6, "rgba(255, 214, 150, 0.9)", 0);
            // the sky above: a cool, thin edge across the top of every form
            rim(0, e * 0.9, "rgba(214, 224, 255, 0.5)", 0);

            var s = new Sprite { cv = cv, ox = ox, oy = oy, k = k };
            cache[key] = s;
            return s;
        }

        /// <summary>
        /// One storm cloud mass: dark, with a lit underside where the bolt and the
        /// sun reach it. `alpha` fades the whole bank in and out.
        /// </summary>
        public void cloud(Ctx c, double x, double y, double r, double alpha, double flash)
        {
            var g = c.CreateRadialGradient(x, y - r * 0.25, r * 0.1, x, y, r);
            g.AddColorStop(0, Col.Oklch(0.27, 0.03, 285, 0.92 * alpha));
            g.AddColorStop(0.7, Col.Oklch(0.3, 0.035, 290, 0.75 * alpha));
            g.AddColorStop(1, "oklch(0.32 0.04 295 / 0)");
            c.FillStyle = g;
            c.BeginPath();
            c.Arc(x, y, r, 0, Math.PI * 2);
            c.Fill();
            var u = c.CreateRadialGradient(x, y + r * 0.5, 0, x, y + r * 0.5, r * 0.9);
            u.AddColorStop(0, Col.Oklch(0.82, 0.1, 60, (0.22 + flash * 0.4) * alpha));
            u.AddColorStop(1, "oklch(0.8 0.1 60 / 0)");
            c.FillStyle = u;
            c.BeginPath();
            c.Arc(x, y + r * 0.5, r * 0.9, 0, Math.PI * 2);
            c.Fill();
        }
    }
}
