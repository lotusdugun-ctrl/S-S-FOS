// Port of src/game/figures.ts: Merope.
using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>drawMerope's options (the TS's anonymous <c>{ act, turned, talking, leave, L }</c>)</summary>
    public class MeropeOpts
    {
        /// <summary>1 | 2 | 3</summary>
        public int act = 1;
        public bool turned;
        public bool talking;
        public double leave;
        public FigLight L;
    }

    public static partial class Figures
    {
        /// <summary>
        /// A woman's head in profile, facing left: a straight Greek line from brow to
        /// the tip of a small nose, full lips, a soft round chin, the jaw curving back
        /// under the ear.
        /// </summary>
        static void profileF(Ctx c, double x, double y, double r, Style fill)
        {
            (double x, double y) P(double px, double py) { return (x + px * r, y + py * r); }
            c.FillStyle = fill;
            c.BeginPath();
            c.MoveTo(P(0.95, -0.05));
            c.BezierCurveTo(P(1.0, -0.75), P(0.55, -1.12), P(0.02, -1.1)); // the back and crown of the skull
            c.BezierCurveTo(P(-0.45, -1.08), P(-0.76, -0.82), P(-0.8, -0.42)); // forehead
            c.QuadraticCurveTo(P(-0.84, -0.3), P(-0.86, -0.24)); // brow
            c.QuadraticCurveTo(P(-0.98, 0.02), P(-1.08, 0.24)); // the straight nose
            c.QuadraticCurveTo(P(-1.1, 0.33), P(-0.96, 0.34)); // its tip and base
            c.QuadraticCurveTo(P(-0.9, 0.38), P(-0.94, 0.47)); // upper lip
            c.QuadraticCurveTo(P(-0.96, 0.52), P(-0.88, 0.55)); // the mouth
            c.QuadraticCurveTo(P(-0.94, 0.6), P(-0.9, 0.66)); // lower lip
            c.QuadraticCurveTo(P(-0.84, 0.72), P(-0.86, 0.8)); // under the lip
            c.BezierCurveTo(P(-0.88, 0.96), P(-0.72, 1.04), P(-0.56, 1.02)); // the chin
            c.BezierCurveTo(P(-0.2, 0.98), P(0.2, 0.78), P(0.42, 0.5)); // the jaw
            c.QuadraticCurveTo(P(0.72, 0.42), P(0.95, -0.05));
            c.ClosePath();
            c.Fill();
        }

        /// <summary>her eye, brow, lips and the shading of a young face</summary>
        static void faceF(Ctx c, double x, double y, double r, double shut, double sorrow, double talk)
        {
            (double x, double y) P(double px, double py) { return (x + px * r, y + py * r); }
            atop(c, () =>
            {
                // the cheek, warm, and the soft shadow of the eye socket
                var c0 = P(-0.45, 0.32);
                var ch = c.CreateRadialGradient(c0.x, c0.y, 0, c0.x, c0.y, 0.42 * r);
                ch.AddColorStop(0, "rgba(220, 120, 110, 0.22)");
                ch.AddColorStop(1, "rgba(220, 120, 110, 0)");
                c.FillStyle = ch;
                c.FillRect(x - r, y - r, 2 * r, 2 * r);
                c.FillStyle = "rgba(60, 40, 60, 0.18)";
                c.BeginPath();
                var e0 = P(-0.6, -0.1);
                c.Ellipse(e0.x, e0.y, 0.24 * r, 0.15 * r, -0.1, 0, Math.PI * 2);
                c.Fill();
                // the eye: almond, the upper lid heavier, lashes
                double ex = -0.64;
                double ey = -0.08 + sorrow * 0.02;
                if (shut > 0.5)
                {
                    c.StrokeStyle = "rgba(40, 24, 24, 0.85)";
                    c.LineWidth = 0.07 * r;
                    c.BeginPath();
                    c.MoveTo(P(ex - 0.15, ey));
                    c.QuadraticCurveTo(P(ex, ey + 0.08), P(ex + 0.15, ey - 0.01));
                    c.Stroke();
                    // lashes resting on the cheek
                    c.LineWidth = 0.035 * r;
                    c.BeginPath();
                    for (int i = 0; i < 4; i++)
                    {
                        double lx = ex - 0.12 + i * 0.07;
                        c.MoveTo(P(lx, ey + 0.05));
                        c.LineTo(P(lx - 0.05, ey + 0.12));
                    }
                    c.Stroke();
                }
                else
                {
                    c.FillStyle = "rgba(244, 236, 230, 0.95)";
                    c.BeginPath();
                    c.MoveTo(P(ex - 0.16, ey));
                    c.QuadraticCurveTo(P(ex - 0.02, ey - 0.12), P(ex + 0.14, ey - 0.01));
                    c.QuadraticCurveTo(P(ex, ey + 0.08), P(ex - 0.16, ey));
                    c.Fill();
                    // the iris, grey-blue like a star's
                    c.FillStyle = "#3d4a5e";
                    c.BeginPath();
                    var i0 = P(ex - 0.07, ey - 0.02);
                    c.Arc(i0.x, i0.y, 0.065 * r, 0, Math.PI * 2);
                    c.Fill();
                    c.FillStyle = "#0d0a0c";
                    c.BeginPath();
                    var i1 = P(ex - 0.08, ey - 0.02);
                    c.Arc(i1.x, i1.y, 0.032 * r, 0, Math.PI * 2);
                    c.Fill();
                    c.FillStyle = "rgba(255, 255, 255, 0.85)";
                    c.BeginPath();
                    var i2 = P(ex - 0.1, ey - 0.05);
                    c.Arc(i2.x, i2.y, 0.016 * r, 0, Math.PI * 2);
                    c.Fill();
                    // the upper lid and its lashes
                    c.StrokeStyle = "rgba(30, 18, 20, 0.9)";
                    c.LineWidth = 0.06 * r;
                    c.BeginPath();
                    c.MoveTo(P(ex - 0.18, ey + 0.01));
                    c.QuadraticCurveTo(P(ex - 0.02, ey - 0.15), P(ex + 0.15, ey - 0.02));
                    c.Stroke();
                    c.LineWidth = 0.03 * r;
                    c.BeginPath();
                    c.MoveTo(P(ex - 0.18, ey + 0.01));
                    c.LineTo(P(ex - 0.26, ey - 0.04));
                    c.MoveTo(P(ex - 0.13, ey - 0.06));
                    c.LineTo(P(ex - 0.2, ey - 0.12));
                    c.Stroke();
                    // the crease of the lid
                    c.StrokeStyle = "rgba(90, 50, 50, 0.35)";
                    c.BeginPath();
                    c.MoveTo(P(ex - 0.12, ey - 0.12));
                    c.QuadraticCurveTo(P(ex, ey - 0.2), P(ex + 0.13, ey - 0.1));
                    c.Stroke();
                }
                // the brow: fine, arched — drawn up at the inner end in sorrow
                c.StrokeStyle = "rgba(42, 28, 26, 0.8)";
                c.LineWidth = 0.055 * r;
                c.BeginPath();
                c.MoveTo(P(-0.8, -0.3 - sorrow * 0.06));
                c.QuadraticCurveTo(P(-0.62, -0.42), P(-0.38, -0.3));
                c.Stroke();
                // the lips, tinted, parted a little when she speaks
                c.FillStyle = "rgba(170, 70, 76, 0.55)";
                c.BeginPath();
                c.MoveTo(P(-0.95, 0.46));
                c.QuadraticCurveTo(P(-0.86, 0.43), P(-0.8, 0.53));
                c.QuadraticCurveTo(P(-0.84, 0.66 + talk * 0.04), P(-0.91, 0.66 + talk * 0.04));
                c.QuadraticCurveTo(P(-0.94, 0.56), P(-0.95, 0.46));
                c.Fill();
                c.StrokeStyle = "rgba(70, 20, 26, 0.6)";
                c.LineWidth = 0.035 * r;
                c.BeginPath();
                c.MoveTo(P(-0.93, 0.55 + talk * 0.02));
                c.LineTo(P(-0.82, 0.55));
                c.Stroke();
                // nostril, and the line of the jaw softly shaded
                c.StrokeStyle = "rgba(90, 50, 50, 0.4)";
                c.LineWidth = 0.035 * r;
                c.BeginPath();
                c.MoveTo(P(-0.97, 0.3));
                c.QuadraticCurveTo(P(-0.9, 0.25), P(-0.86, 0.31));
                c.Stroke();
                var j0 = P(0, 0.6);
                var j1 = P(0, 1.1);
                var jaw = c.CreateLinearGradient(j0.x, j0.y, j1.x, j1.y);
                jaw.AddColorStop(0, "rgba(60, 40, 60, 0)");
                jaw.AddColorStop(1, "rgba(60, 40, 60, 0.28)");
                c.FillStyle = jaw;
                c.FillRect(x - r, y + 0.6 * r, 2 * r, 0.6 * r);
            });
        }

        class MeropeTone
        {
            public RGB cloth, mantle, skin;
            public string hair;
        }

        static readonly MeropeTone[] meropeTones =
        {
            new MeropeTone { cloth = new RGB(216, 224, 240), mantle = new RGB(58, 72, 112), skin = new RGB(226, 200, 194), hair = "#2c2a36" },
            new MeropeTone { cloth = new RGB(232, 234, 242), mantle = new RGB(52, 66, 118), skin = new RGB(232, 186, 160), hair = "#2a201f" },
            new MeropeTone { cloth = new RGB(246, 242, 232), mantle = new RGB(44, 62, 128), skin = new RGB(236, 184, 150), hair = "#261a16" },
        };
        static readonly double[] meropeAlpha = { 0.82, 0.9, 0.97 };

        /// <summary>
        /// Merope, a shade, and one of the seven Pleiades. A young woman, tall and
        /// straight, in a fine ivory chiton and a deep blue himation: over her head,
        /// down her back, and across her body to the front hip, where its end hangs
        /// over her forearm. Her colour comes back to her act by act; she is a ghost
        /// by her cold edge-light and by the mist her feet are lost in, not by being
        /// washed out.
        /// </summary>
        public static void drawMerope(Ctx ctx, double x, double y, double s, MeropeOpts o)
        {
            if (o.leave >= 1) return;
            var L = o.L;
            double t = L.t;
            var tone = meropeTones[o.act - 1];
            double alpha = (meropeAlpha[o.act - 1] + Math.Sin(t * 1.7) * 0.02) * (1 - o.leave);
            double dx = o.leave * -70 * s;
            // the cold light around a shade — gold in the last act, when she is a star again
            double gx = x + dx;
            double gy = y - 44 * s;
            var halo = ctx.CreateRadialGradient(gx, gy, 4 * s, gx, gy, 52 * s);
            halo.AddColorStop(0, o.act == 3 ? "rgba(255, 232, 190, 0.22)" : "rgba(160, 190, 255, 0.2)");
            halo.AddColorStop(1, "rgba(160, 190, 255, 0)");
            ctx.FillStyle = halo;
            ctx.FillRect(gx - 52 * s, gy - 60 * s, 104 * s, 120 * s);
            paintLit(
                ctx,
                "merope",
                x + dx,
                y,
                new Box(-30 * s, -86 * s, 30 * s, 4 * s),
                L,
                s,
                (c) =>
                {
                    if (o.turned) c.Scale(-1, 1);
                    // the wind in her mantle: more on a windy hill, most in a storm
                    double wind = Math.Max(0.5, Math.Min(3, L.wind));
                    double sway = Math.Sin(t * 0.9) * 0.6 * s * (0.7 + 0.3 * wind);
                    double br = Math.Sin(t * 1.3) * 0.25 * s;
                    double bow = o.act == 1 ? 0.12 : o.act == 2 ? 0.06 : -0.04; // the head: bowed, then lifted
                    double hx = -1.2 * s + sway * 0.6;
                    double hy = -73 * s + br;
                    Col col(RGB k, double d) { return rgba(mix(k, new RGB(16, 16, 30), d), 1); }
                    (double x, double y) P(double px, double py) { return (hx + px * s, hy + py * s); }
                    double flutter(double k) { return (Math.Sin(t * 1.6 * (0.8 + 0.2 * wind) + k) + Math.Sin(t * 3.7 + k * 1.9) * 0.25 * (wind - 1)) * 0.8 * s * wind; }

                    // ---- the mantle's back fall: from the crown down her back to the calves
                    var mBack = c.CreateLinearGradient(hx, 0, hx + 18 * s, 0);
                    mBack.AddColorStop(0, col(tone.mantle, 0.12));
                    mBack.AddColorStop(1, col(tone.mantle, 0.5));
                    c.FillStyle = mBack;
                    c.BeginPath();
                    c.MoveTo(P(-1, -5.6));
                    c.BezierCurveTo(P(6, -7), P(8.6, -1), P(8.2, 6));
                    {
                        var a = P(12, 22);
                        var b = P(12.6 + flutter(0) / s, 42);
                        c.BezierCurveTo(a.x, a.y, b.x, b.y, 11.6 * s + flutter(1), -12 * s);
                    }
                    // the edge, rippling where it hangs free
                    for (int i = 1; i <= 4; i++)
                        c.LineTo(11.6 * s - i * 1.8 * s + flutter(i), (-12 + (i % 2 != 0 ? 1.8 : 0)) * s);
                    {
                        var b = P(4, 12);
                        c.QuadraticCurveTo(5 * s, -24 * s, b.x, b.y);
                    }
                    c.ClosePath();
                    c.Fill();
                    atop(c, () =>
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            c.StrokeStyle = i % 2 != 0 ? "rgba(255, 255, 255, 0.07)" : "rgba(8, 10, 24, 0.3)";
                            c.LineWidth = (1.6 - i * 0.2) * s;
                            c.BeginPath();
                            c.MoveTo(P(5.6 + i * 1.1, 6));
                            var a = P(8 + i * 1.2, 22);
                            var b = P(9 + i, 40);
                            c.BezierCurveTo(a.x, a.y, b.x, b.y, 6.6 * s + i * 1.4 * s + flutter(i), -12 * s);
                            c.Stroke();
                        }
                    });

                    // ---- the chiton: a young woman's body under fine linen
                    var dress = c.CreateLinearGradient(hx - 10 * s, 0, hx + 12 * s, 0);
                    dress.AddColorStop(0, col(tone.cloth, 0));
                    dress.AddColorStop(0.5, col(tone.cloth, 0.1));
                    dress.AddColorStop(1, col(tone.cloth, 0.38));
                    c.FillStyle = dress;
                    c.BeginPath();
                    c.MoveTo(P(-1.2, 8.4)); // the throat
                    c.QuadraticCurveTo(P(-3.6, 10.4), P(-5.6, 11.8)); // a sloping shoulder
                    c.QuadraticCurveTo(P(-6.8, 14), P(-7.4, 17.6)); // the breast
                    c.QuadraticCurveTo(P(-7.8, 21), P(-5.6, 22.6));
                    c.QuadraticCurveTo(P(-4.6, 25), P(-4.9, 26.6)); // the waist
                    c.QuadraticCurveTo(P(-6.8, 31), P(-7.2, 36)); // the hip
                    c.QuadraticCurveTo(-9.6 * s + sway * 0.4, -38 * s, -9.4 * s + sway * 0.4, -24 * s); // the thigh, the knee forward
                    c.QuadraticCurveTo(-10 * s, -12 * s, -12.4 * s + sway * 0.6 + flutter(2) * 0.4, 0); // to the hem
                    for (int i = 1; i <= 8; i++)
                        c.LineTo(-12.4 * s + i * 2.9 * s + sway * 0.6 * (1 - i / 8.0), (i % 2 != 0 ? -1.3 : 0.2) * s);
                    {
                        var b = P(7.4, 35);
                        c.QuadraticCurveTo(9 * s, -18 * s, b.x, b.y); // the back of the leg
                    }
                    c.QuadraticCurveTo(P(5.4, 29), P(4.8, 26.4)); // the small of the back
                    c.QuadraticCurveTo(P(6.4, 19), P(6, 12.6)); // the back
                    c.QuadraticCurveTo(P(4.2, 10), P(1.6, 8.4)); // the nape
                    c.ClosePath();
                    c.Fill();
                    atop(c, () =>
                    {
                        // soft folds: broad bands of light and shade, following the forward leg
                        for (int i = 0; i < 7; i++)
                        {
                            double f = -6 + i * 2;
                            double x0 = hx + f * 0.65 * s;
                            double y0 = hy + 28 * s;
                            double kx = (f * 1.05 - 2) * s + sway * 0.3;
                            double ex = (f * 1.5 + 0.5) * s + sway * 0.5;
                            c.StrokeStyle = "rgba(40, 40, 70, 0.16)";
                            c.LineWidth = 1.6 * s;
                            c.BeginPath();
                            c.MoveTo(x0, y0);
                            c.BezierCurveTo(x0 - 1 * s, hy + 44 * s, kx, -22 * s, ex, 0);
                            c.Stroke();
                            c.StrokeStyle = "rgba(255, 255, 255, 0.16)";
                            c.LineWidth = 1 * s;
                            c.BeginPath();
                            c.MoveTo(x0 + 1.1 * s, y0);
                            c.BezierCurveTo(x0 + 0.1 * s, hy + 44 * s, kx + 1.2 * s, -22 * s, ex + 1.3 * s, 0);
                            c.Stroke();
                        }
                        // where the cloth lies on the forward thigh and knee it catches the light
                        var t0 = P(-9, 40);
                        var t1 = P(-4, 40);
                        var th = c.CreateLinearGradient(t0.x, t0.y, t1.x, t1.y);
                        th.AddColorStop(0, "rgba(255, 255, 255, 0.28)");
                        th.AddColorStop(1, "rgba(255, 255, 255, 0)");
                        c.FillStyle = th;
                        c.BeginPath();
                        c.MoveTo(P(-6.6, 34));
                        {
                            var a = P(-9, 46);
                            c.QuadraticCurveTo(a.x, a.y, -8.6 * s, -24 * s);
                        }
                        c.LineTo(-5.6 * s, -24 * s);
                        c.QuadraticCurveTo(P(-5.6, 46), P(-4.8, 34));
                        c.Fill();
                        // the breast, rounded by light, and the folds gathered under it
                        var b0 = P(-5.4, 16.4);
                        var bg = c.CreateRadialGradient(b0.x, b0.y, 0, b0.x, b0.y, 3.6 * s);
                        bg.AddColorStop(0, "rgba(255, 255, 255, 0.25)");
                        bg.AddColorStop(1, "rgba(255, 255, 255, 0)");
                        c.FillStyle = bg;
                        c.FillRect(hx - 10 * s, hy + 12 * s, 9 * s, 9 * s);
                        c.StrokeStyle = "rgba(40, 40, 70, 0.22)";
                        c.LineWidth = 0.6 * s;
                        c.BeginPath();
                        for (int i = 0; i < 3; i++)
                        {
                            c.MoveTo(P(-5.4 + i * 0.4, 20.6 + i * 1.6));
                            c.QuadraticCurveTo(P(-1, 22.4 + i * 1.6), P(4, 19 + i * 2.4));
                        }
                        c.Stroke();
                        // the girdle: a thin cord, tied
                        c.StrokeStyle = col(new RGB(150, 120, 70), 0.1);
                        c.LineWidth = 0.9 * s;
                        c.BeginPath();
                        c.MoveTo(P(-5, 25.6));
                        c.QuadraticCurveTo(P(0, 27.4), P(5.4, 25));
                        c.Stroke();
                    });

                    // ---- the mantle across the body, from the back shoulder over the front hip
                    var m0 = P(-8, 30);
                    var m1 = P(8, 12);
                    var mFront = c.CreateLinearGradient(m0.x, m0.y, m1.x, m1.y);
                    mFront.AddColorStop(0, col(tone.mantle, 0));
                    mFront.AddColorStop(1, col(tone.mantle, 0.32));
                    c.FillStyle = mFront;
                    c.BeginPath();
                    c.MoveTo(P(6.4, 11.6));
                    c.QuadraticCurveTo(P(3.4, 20), P(-5.6, 27.4));
                    c.QuadraticCurveTo(P(-8.6, 33), P(-8.4, 44 + (flutter(3) / s) * 0.5)); // its end hanging over the arm
                    c.LineTo(P(-5.4, 45));
                    c.QuadraticCurveTo(P(-4.6, 37), P(1.6, 35));
                    c.QuadraticCurveTo(P(7, 33.4), P(7.6, 30));
                    c.ClosePath();
                    c.Fill();
                    atop(c, () =>
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            c.StrokeStyle = "rgba(8, 10, 26, 0.32)";
                            c.LineWidth = 0.9 * s;
                            c.BeginPath();
                            c.MoveTo(P(5.8 - i * 0.2, 15 + i * 4.4));
                            c.QuadraticCurveTo(
                                P(-0.4 - i * 1.2, 27 + i * 3),
                                P(-5.4 - i * 0.5, 29.6 + i * 3.2));
                            c.Stroke();
                            c.StrokeStyle = "rgba(170, 190, 255, 0.16)";
                            c.LineWidth = 0.7 * s;
                            c.BeginPath();
                            c.MoveTo(P(5.6 - i * 0.2, 16.6 + i * 4.4));
                            c.QuadraticCurveTo(
                                P(-0.8 - i * 1.2, 28.6 + i * 3),
                                P(-5.8 - i * 0.5, 31.2 + i * 3.2));
                            c.Stroke();
                        }
                        // the border woven with seven small stars
                        for (int i = 0; i < 7; i++)
                        {
                            double k = i / 6.0;
                            var (sx, sy) = P(-5.8 - k * 2.4, 29 + k * 14);
                            c.FillStyle = o.act == 3 ? "rgba(232, 214, 160, 0.9)" : "rgba(232, 214, 160, 0.5)";
                            c.BeginPath();
                            for (int j = 0; j < 8; j++)
                            {
                                double a = (j / 8.0) * Math.PI * 2;
                                double rr = (j % 2 != 0 ? 0.25 : 0.6) * s;
                                if (j != 0) c.LineTo(sx + Math.Cos(a) * rr, sy + Math.Sin(a) * rr);
                                else c.MoveTo(sx + rr, sy);
                            }
                            c.Fill();
                        }
                    });

                    // ---- the arm: her hand holds the mantle's edge at her breast; speaking,
                    // it opens toward him, palm up
                    double open = o.talking ? 0.5 + Math.Sin(t * 1.8) * 0.5 : 0;
                    var sh = (x: hx - 4.6 * s, y: hy + 12.6 * s);
                    var el = (x: hx - 6.8 * s - open * 1.6 * s, y: hy + 25 * s - open * 1.4 * s);
                    var hand = (x: hx - 4.6 * s - open * 8.4 * s, y: hy + 17.6 * s + open * 1.6 * s);
                    // the chiton's loose sleeve over the upper arm, pinned along the top
                    limb(c, sh.x, sh.y, el.x, el.y, 2.5 * s, 1.9 * s, 0.6 * s, 0.4, col(tone.cloth, 0.08));
                    for (int i = 0; i < 3; i++)
                    {
                        double k = 0.15 + i * 0.3;
                        c.FillStyle = "#b99a58";
                        c.BeginPath();
                        c.Arc(
                            sh.x + (el.x - sh.x) * k - 1.2 * s,
                            sh.y + (el.y - sh.y) * k,
                            0.35 * s,
                            0,
                            Math.PI * 2);
                        c.Fill();
                    }
                    limb(c, el.x, el.y, hand.x, hand.y, 1.5 * s, 1 * s, 0.3 * s, 0.4, col(tone.skin, 0.04));
                    // the hand: a palm and long fingers
                    c.Save();
                    c.Translate(hand.x, hand.y);
                    c.Rotate(Math.Atan2(hand.y - el.y, hand.x - el.x) + open * 0.3);
                    c.FillStyle = col(tone.skin, 0.02);
                    c.BeginPath();
                    c.Ellipse(0.6 * s, 0, 1.4 * s, 1 * s, 0, 0, Math.PI * 2);
                    c.Fill();
                    c.StrokeStyle = col(tone.skin, 0.04);
                    c.LineCap = LineCap.Round;
                    c.LineWidth = 0.55 * s;
                    c.BeginPath();
                    for (int i = 0; i < 4; i++)
                    {
                        c.MoveTo(1.6 * s, (-0.6 + i * 0.4) * s);
                        c.LineTo((3.4 - Math.Abs(i - 1.5) * 0.4) * s, (-0.8 + i * 0.55 + open * 0.3) * s);
                    }
                    c.MoveTo(0.6 * s, -0.8 * s);
                    c.LineTo(1.8 * s, -1.6 * s); // the thumb
                    c.Stroke();
                    c.Restore();

                    // ---- the neck and the head
                    var neck = c.CreateLinearGradient(hx - 2 * s, 0, hx + 2 * s, 0);
                    neck.AddColorStop(0, col(tone.skin, 0.04));
                    neck.AddColorStop(1, col(tone.skin, 0.24));
                    c.FillStyle = neck;
                    c.BeginPath();
                    c.MoveTo(P(-1.8, 2.8));
                    c.QuadraticCurveTo(P(-2, 6.6), P(-1.6, 9.4));
                    c.LineTo(P(2, 9.4));
                    c.QuadraticCurveTo(P(1.8, 5.6), P(2.4, 2.6));
                    c.ClosePath();
                    c.Fill();
                    c.Save();
                    c.Translate(hx, hy);
                    c.Rotate(bow);
                    double r = 5.2 * s;
                    var skinG = c.CreateLinearGradient(-r, 0, r, 0);
                    skinG.AddColorStop(0, col(tone.skin, 0));
                    skinG.AddColorStop(1, col(tone.skin, 0.16));
                    profileF(c, 0, 0, r, skinG);
                    faceF(c, 0, 0, r,
                        o.act == 1 && !o.talking ? 1 : blink(t, 0.7),
                        o.act == 2 ? 1 : o.act == 1 ? 0.5 : 0,
                        o.talking ? 0.5 + Math.Sin(t * 7) * 0.5 : 0);
                    // hair: the whole crown and back of the head, parted, waved back from
                    // the brow over the ear to a knot at the nape
                    c.FillStyle = tone.hair;
                    c.BeginPath();
                    c.MoveTo(-0.8 * r, -0.46 * r);
                    c.BezierCurveTo(-0.78 * r, -1.08 * r, 0.2 * r, -1.3 * r, 0.78 * r, -0.86 * r);
                    c.BezierCurveTo(1.12 * r, -0.5 * r, 1.12 * r, 0.2 * r, 0.9 * r, 0.52 * r);
                    c.BezierCurveTo(1.3 * r, 0.6 * r, 1.36 * r, 1.1 * r, 0.9 * r, 1.12 * r); // the knot
                    c.BezierCurveTo(0.6 * r, 1.12 * r, 0.42 * r, 0.8 * r, 0.42 * r, 0.56 * r);
                    c.BezierCurveTo(0.2 * r, 0.3 * r, 0.12 * r, -0.12 * r, -0.12 * r, -0.36 * r); // over the ear
                    c.BezierCurveTo(-0.36 * r, -0.56 * r, -0.6 * r, -0.4 * r, -0.8 * r, -0.46 * r);
                    c.Fill();
                    // the ear, half under the hair
                    c.FillStyle = col(tone.skin, 0.12);
                    c.BeginPath();
                    c.Ellipse(0.14 * r, 0.12 * r, 0.13 * r, 0.22 * r, 0.2, 0, Math.PI * 2);
                    c.Fill();
                    c.FillStyle = "#c9a85a";
                    c.BeginPath();
                    c.Arc(0.12 * r, 0.4 * r, 0.06 * r, 0, Math.PI * 2); // a small gold earring
                    c.Fill();
                    // the waves, and one strand fallen loose by the cheek
                    c.StrokeStyle = "rgba(255, 240, 230, 0.16)";
                    c.LineWidth = 0.05 * r;
                    c.BeginPath();
                    for (int i = 0; i < 4; i++)
                    {
                        c.MoveTo((-0.66 + i * 0.12) * r, (-0.62 - i * 0.1) * r);
                        c.BezierCurveTo(
                            (-0.4 + i * 0.12) * r,
                            (-0.86 - i * 0.06) * r,
                            (-0.2 + i * 0.12) * r,
                            (-0.5 - i * 0.08) * r,
                            (0.3 + i * 0.04) * r,
                            (-0.6 - i * 0.08) * r);
                    }
                    c.Stroke();
                    c.StrokeStyle = tone.hair;
                    c.LineWidth = 0.06 * r;
                    c.BeginPath();
                    c.MoveTo(-0.3 * r, -0.5 * r);
                    c.BezierCurveTo(
                        -0.4 * r,
                        0,
                        -0.2 * r + Math.Sin(t * 1.4) * 0.05 * r,
                        0.4 * r,
                        -0.3 * r,
                        0.9 * r);
                    c.Stroke();
                    // the himation drawn over her head, its edge turned back, framing the face
                    var hood = c.CreateLinearGradient(0, 0, 2.2 * r, 0);
                    hood.AddColorStop(0, col(tone.mantle, 0));
                    hood.AddColorStop(1, col(tone.mantle, 0.42));
                    c.FillStyle = hood;
                    c.BeginPath();
                    c.MoveTo(0.1 * r, -1.3 * r);
                    c.BezierCurveTo(1.0 * r, -1.5 * r, 1.66 * r, -0.8 * r, 1.62 * r, 0.3 * r);
                    c.BezierCurveTo(1.6 * r, 1.2 * r, 1.4 * r, 1.9 * r, 1.3 * r, 2.4 * r);
                    c.LineTo(0.66 * r, 2.2 * r);
                    c.BezierCurveTo(0.8 * r, 1.4 * r, 0.9 * r, 0.6 * r, 0.7 * r, -0.2 * r);
                    c.BezierCurveTo(0.6 * r, -0.8 * r, 0.4 * r, -1.1 * r, -0.04 * r, -1.16 * r);
                    c.ClosePath();
                    c.Fill();
                    atop(c, () =>
                    {
                        // the turned-back edge catches the light; a fold runs back over the skull
                        c.StrokeStyle = "rgba(190, 205, 255, 0.35)";
                        c.LineWidth = 0.1 * r;
                        c.BeginPath();
                        c.MoveTo(-0.04 * r, -1.2 * r);
                        c.BezierCurveTo(0.4 * r, -1.1 * r, 0.7 * r, -0.6 * r, 0.74 * r, -0.1 * r);
                        c.Stroke();
                        c.StrokeStyle = "rgba(6, 8, 24, 0.35)";
                        c.LineWidth = 0.08 * r;
                        c.BeginPath();
                        c.MoveTo(0.5 * r, -1.2 * r);
                        c.BezierCurveTo(1.2 * r, -0.8 * r, 1.3 * r, 0.6 * r, 1.0 * r, 2.2 * r);
                        c.Stroke();
                    });
                    c.Restore();

                    // a shade has no feet: she dissolves into the ground mist
                    c.Save();
                    c.SetTransform(1, 0, 0, 1, 0, 0);
                    c.GlobalCompositeOperation = Composite.DestinationOut;
                    double H = c.canvas.height;
                    var fade = c.CreateLinearGradient(0, H - 22 * s * 2, 0, H);
                    fade.AddColorStop(0, "rgba(0,0,0,0)");
                    fade.AddColorStop(1, "rgba(0,0,0,0.92)");
                    c.FillStyle = fade;
                    c.FillRect(0, H - 22 * s * 2, c.canvas.width, 22 * s * 2);
                    c.Restore();
                },
                alpha);
            // the cold edge-light of a shade, all around her, and motes rising off her
            ctx.Save();
            for (int i = 0; i < 6; i++)
            {
                double p = (((t * 0.22 + i * 0.17) % 1) + 1) % 1;
                double a = Math.Sin(p * Math.PI) * 0.35 * (1 - o.leave);
                ctx.FillStyle =
                    o.act == 3 ? Col.Rgba(255, 236, 200, Css.fixedNum(a, 3)) : Col.Rgba(190, 210, 255, Css.fixedNum(a, 3));
                ctx.BeginPath();
                ctx.Arc(
                    x + dx + (i - 2.5) * 4 * s + Math.Sin(t + i) * 2 * s,
                    y - 10 * s - p * 76 * s,
                    0.9 * s,
                    0,
                    Math.PI * 2);
                ctx.Fill();
            }
            ctx.Restore();
        }
    }
}
