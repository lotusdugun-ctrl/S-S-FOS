using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// The figures of the myths who meet him on the hill now and then: Thanatos, whom he once
    /// chained; Orpheus with his lyre; Persephone, whom he talked into letting him go; Ixion on his
    /// burning wheel; the Danaids with their leaking jars; Odysseus, who saw him in the underworld.
    /// Each is drawn flat in its own colours and then lit like the others on the hill (paintLit),
    /// standing at the roadside, facing down the slope toward him.
    /// </summary>
    public static partial class Figures
    {
        /// <summary>a standing figure's robe: shoulders to the ground, flaring a little, with folds</summary>
        static void robe(Ctx c, double s, RGB col, double sway, double width = 1, double hem = 0)
        {
            var g = c.CreateLinearGradient(-14 * s, 0, 14 * s, 0);
            g.AddColorStop(0, rgba(mix(col, new RGB(255, 255, 255), 0.08), 1));
            g.AddColorStop(1, rgba(mix(col, new RGB(10, 10, 20), 0.3), 1));
            c.FillStyle = g;
            c.BeginPath();
            c.MoveTo(-9 * s * width, -66 * s);
            c.QuadraticCurveTo(-13 * s * width, -40 * s, -15 * s * width + sway, -2 * s + hem);
            c.LineTo(15 * s * width + sway, -2 * s + hem);
            c.QuadraticCurveTo(13 * s * width, -40 * s, 9 * s * width, -66 * s);
            c.QuadraticCurveTo(0, -70 * s, -9 * s * width, -66 * s);
            c.ClosePath();
            c.Fill();
            // folds falling from the shoulders
            c.StrokeStyle = rgba(mix(col, new RGB(10, 10, 20), 0.45), 0.55);
            c.LineWidth = 0.9 * s;
            for (int i = -2; i <= 2; i++)
            {
                c.BeginPath();
                c.MoveTo(i * 3.2 * s * width, -60 * s);
                c.QuadraticCurveTo(i * 4.5 * s * width + sway * 0.5, -30 * s, i * 5.6 * s * width + sway, -3 * s);
                c.Stroke();
            }
        }

        /// <summary>a head on a neck, in profile looking left, with hair</summary>
        static void head(Ctx c, double s, RGB skin, RGB hair, double hx, double hy, bool beard = false, bool longHair = false)
        {
            // neck
            c.FillStyle = rgba(mix(skin, new RGB(20, 10, 10), 0.15), 1);
            c.FillRect(hx - 2.4 * s, hy + 4 * s, 4.8 * s, 6 * s);
            if (longHair)
            {
                c.FillStyle = rgba(hair, 1);
                c.BeginPath();
                c.MoveTo(hx + 1 * s, hy - 7 * s);
                c.QuadraticCurveTo(hx + 10 * s, hy - 4 * s, hx + 8 * s, hy + 14 * s);
                c.LineTo(hx + 2 * s, hy + 12 * s);
                c.ClosePath();
                c.Fill();
            }
            profile(c, hx, hy, 7 * s, rgba(skin, 1));
            // hair over the crown and the back of the head
            c.FillStyle = rgba(hair, 1);
            c.BeginPath();
            c.MoveTo(hx - 5.6 * s, hy - 4.6 * s);
            c.QuadraticCurveTo(hx - 2 * s, hy - 10.4 * s, hx + 5 * s, hy - 7 * s);
            c.QuadraticCurveTo(hx + 8.2 * s, hy - 3 * s, hx + 6.4 * s, hy + 2.6 * s);
            c.QuadraticCurveTo(hx + 3 * s, hy - 2 * s, hx - 5.6 * s, hy - 4.6 * s);
            c.Fill();
            if (beard)
            {
                c.BeginPath();
                c.MoveTo(hx - 6.2 * s, hy + 3.4 * s);
                c.QuadraticCurveTo(hx - 7.6 * s, hy + 10 * s, hx - 2 * s, hy + 10.6 * s);
                c.QuadraticCurveTo(hx + 3 * s, hy + 8 * s, hx + 3 * s, hy + 3 * s);
                c.ClosePath();
                c.Fill();
            }
            // the eye
            c.FillStyle = "rgba(20, 14, 12, 0.85)";
            c.BeginPath();
            c.Arc(hx - 4.2 * s, hy - 1.2 * s, 0.75 * s, 0, Math.PI * 2);
            c.Fill();
        }

        /// <summary>an arm from the shoulder to the hand (a thick round line)</summary>
        static void arm(Ctx c, double s, RGB col, double ax, double ay, double ex, double ey, double hx, double hy)
        {
            c.StrokeStyle = rgba(col, 1);
            c.LineWidth = 4.2 * s;
            c.BeginPath();
            c.MoveTo(ax, ay);
            c.QuadraticCurveTo(ex, ey, hx, hy);
            c.Stroke();
        }

        static readonly RGB SKIN = new RGB(206, 160, 124);
        static readonly RGB SKIN_PALE = new RGB(196, 190, 192);

        /// <summary>Thanatos: Death, dark-winged and pale, whom Sisyphus once put in chains</summary>
        public static void drawThanatos(Ctx ctx, double x, double y, double s, FigLight L)
        {
            double t = L.t;
            paintLit(ctx, "myth-thanatos", x, y, new Box(-46 * s, -104 * s, 46 * s, 4 * s), L, s, c =>
            {
                double beat = Math.Sin(t * 1.1) * 0.06;
                // the wings, folded half open behind him
                for (int side = 0; side < 2; side++)
                {
                    double dir = side == 0 ? 1 : 0.8;
                    c.FillStyle = side == 0 ? "rgba(18, 16, 22, 1)" : "rgba(30, 27, 34, 1)";
                    c.BeginPath();
                    c.MoveTo(4 * s, -64 * s);
                    c.QuadraticCurveTo(30 * s * dir, -98 * s - beat * 40 * s, 40 * s * dir, -70 * s);
                    for (int f = 0; f < 5; f++)
                    {
                        double fx = (40 - f * 6) * s * dir, fy = (-70 + f * 9) * s;
                        c.LineTo(fx - 3 * s, fy + 7 * s);
                        c.LineTo(fx - 4 * s, fy + 4 * s);
                    }
                    c.QuadraticCurveTo(14 * s * dir, -20 * s, 6 * s, -40 * s);
                    c.ClosePath();
                    c.Fill();
                }
                robe(c, s, new RGB(24, 22, 28), Math.Sin(t * 0.8) * 0.8 * s, 1.05);
                arm(c, s, new RGB(44, 40, 52), 6 * s, -60 * s, 2 * s, -40 * s, -10 * s, -34 * s);
                head(c, s, SKIN_PALE, new RGB(18, 16, 22), -1 * s, -75 * s, false, true);
                // the torch of Death, held low and turned down
                c.StrokeStyle = "rgba(70, 54, 40, 1)";
                c.LineWidth = 1.6 * s;
                c.BeginPath();
                c.MoveTo(-10 * s, -34 * s);
                c.LineTo(-16 * s, -14 * s);
                c.Stroke();
                c.FillStyle = "rgba(255, 150, 70, 0.9)";
                c.BeginPath();
                c.Ellipse(-16.5 * s, -12 * s, 2.2 * s, 3.4 * s + Math.Sin(t * 9) * 0.6 * s, 0, 0, Math.PI * 2);
                c.Fill();
            });
        }

        /// <summary>Orpheus, singer of Thrace, with his lyre</summary>
        public static void drawOrpheus(Ctx ctx, double x, double y, double s, FigLight L)
        {
            double t = L.t;
            paintLit(ctx, "myth-orpheus", x, y, new Box(-30 * s, -96 * s, 30 * s, 4 * s), L, s, c =>
            {
                double sway = Math.Sin(t * 1.4) * 1.2 * s;
                robe(c, s, new RGB(226, 216, 196), sway, 0.92);
                // the blue mantle over one shoulder
                c.FillStyle = "rgba(58, 84, 142, 1)";
                c.BeginPath();
                c.MoveTo(-8 * s, -66 * s);
                c.QuadraticCurveTo(6 * s, -50 * s, 13 * s + sway, -18 * s);
                c.LineTo(6 * s + sway, -16 * s);
                c.QuadraticCurveTo(2 * s, -44 * s, -10 * s, -58 * s);
                c.ClosePath();
                c.Fill();
                head(c, s, SKIN, new RGB(74, 48, 30), 0, -75 * s, false, false);
                // a laurel
                c.StrokeStyle = "rgba(96, 132, 60, 1)";
                c.LineWidth = 1.4 * s;
                c.BeginPath();
                c.Arc(0, -76 * s, 7.4 * s, Math.PI * 1.05, Math.PI * 1.95);
                c.Stroke();
                // the lyre, held before him, the strings trembling as he plays
                double lx = -11 * s, ly = -44 * s;
                c.StrokeStyle = "rgba(170, 120, 60, 1)";
                c.LineWidth = 2 * s;
                c.BeginPath();
                c.MoveTo(lx - 5 * s, ly - 14 * s);
                c.QuadraticCurveTo(lx - 7 * s, ly, lx, ly + 4 * s);
                c.QuadraticCurveTo(lx + 7 * s, ly, lx + 5 * s, ly - 14 * s);
                c.Stroke();
                c.BeginPath();
                c.MoveTo(lx - 6 * s, ly - 12 * s);
                c.LineTo(lx + 6 * s, ly - 12 * s);
                c.Stroke();
                c.StrokeStyle = "rgba(240, 226, 190, 0.9)";
                c.LineWidth = 0.5 * s;
                for (int i = -2; i <= 2; i++)
                {
                    double tr = Math.Sin(t * 30 + i) * 0.3 * s;
                    c.BeginPath();
                    c.MoveTo(lx + i * 1.6 * s, ly - 12 * s);
                    c.LineTo(lx + i * 1.2 * s + tr, ly + 2 * s);
                    c.Stroke();
                }
                arm(c, s, SKIN, -4 * s, -60 * s, -10 * s, -52 * s, lx + 2 * s, ly - 4 * s + Math.Sin(t * 6) * 1 * s);
            });
        }

        /// <summary>Persephone, queen of the dead, with a pomegranate</summary>
        public static void drawPersephone(Ctx ctx, double x, double y, double s, FigLight L, bool given)
        {
            double t = L.t;
            paintLit(ctx, "myth-persephone", x, y, new Box(-30 * s, -98 * s, 30 * s, 4 * s), L, s, c =>
            {
                double sway = Math.Sin(t * 0.9) * 0.8 * s;
                robe(c, s, new RGB(118, 34, 48), sway, 1.0);
                // a dark veil down her back
                c.FillStyle = "rgba(40, 22, 34, 1)";
                c.BeginPath();
                c.MoveTo(1 * s, -82 * s);
                c.QuadraticCurveTo(14 * s, -64 * s, 12 * s + sway, -20 * s);
                c.LineTo(6 * s, -22 * s);
                c.QuadraticCurveTo(8 * s, -60 * s, -2 * s, -76 * s);
                c.ClosePath();
                c.Fill();
                head(c, s, new RGB(214, 178, 152), new RGB(30, 20, 18), 0, -75 * s, false, true);
                // her crown
                c.FillStyle = "rgba(226, 182, 80, 1)";
                c.BeginPath();
                c.MoveTo(-5 * s, -82 * s);
                for (int i = 0; i < 5; i++)
                {
                    c.LineTo(-5 * s + i * 2.6 * s + 1.3 * s, -86 * s);
                    c.LineTo(-5 * s + (i + 1) * 2.6 * s, -82 * s);
                }
                c.ClosePath();
                c.Fill();
                // the pomegranate in her open hand (gone once she has given it)
                arm(c, s, new RGB(214, 178, 152), -4 * s, -60 * s, -14 * s, -52 * s, -16 * s, -44 * s);
                if (!given)
                {
                    c.FillStyle = "rgba(176, 30, 44, 1)";
                    c.BeginPath();
                    c.Arc(-17 * s, -47 * s, 3.4 * s, 0, Math.PI * 2);
                    c.Fill();
                    c.FillStyle = "rgba(120, 20, 30, 1)";
                    c.FillRect(-17.6 * s, -51.6 * s, 1.4 * s, 1.6 * s);
                }
                // asphodel at her feet
                c.FillStyle = "rgba(238, 232, 220, 0.95)";
                for (int i = 0; i < 4; i++)
                {
                    c.BeginPath();
                    c.Arc((-18 + i * 9) * s, (-4 - (i % 2) * 3) * s, 1.6 * s, 0, Math.PI * 2);
                    c.Fill();
                }
            });
        }

        /// <summary>a Danaid with her leaking jar on her shoulder</summary>
        public static void drawDanaids(Ctx ctx, double x, double y, double s, FigLight L)
        {
            double t = L.t;
            paintLit(ctx, "myth-danaids", x, y, new Box(-56 * s, -96 * s, 44 * s, 4 * s), L, s, c =>
            {
                for (int k = 2; k >= 0; k--)
                {
                    c.Save();
                    c.Translate((k - 1) * 22 * s, k == 1 ? 0 : -2 * s);
                    double sc = k == 1 ? 1 : 0.92;
                    c.Scale(sc, sc);
                    double bob = Math.Sin(t * 1.6 + k * 2) * 1 * s;
                    robe(c, s, k == 1 ? new RGB(214, 204, 186) : new RGB(196, 186, 170), bob, 0.85);
                    head(c, s, new RGB(210, 170, 140), new RGB(52, 34, 24), 0, -75 * s + bob * 0.3, false, true);
                    // the amphora on the right shoulder, held up by one arm
                    c.FillStyle = "rgba(170, 96, 56, 1)";
                    c.BeginPath();
                    c.Ellipse(10 * s, -82 * s + bob, 5.4 * s, 8 * s, 0.35, 0, Math.PI * 2);
                    c.Fill();
                    c.FillStyle = "rgba(30, 20, 16, 1)";
                    c.FillRect(8 * s, -84 * s + bob, 5 * s, 1.2 * s);
                    arm(c, s, new RGB(210, 170, 140), 7 * s, -64 * s, 14 * s, -72 * s, 12 * s, -78 * s + bob);
                    // the water running out of the cracked jar
                    c.FillStyle = "rgba(150, 196, 236, 0.85)";
                    for (int d = 0; d < 3; d++)
                    {
                        double ph = (t * 1.8 + d / 3.0 + k * 0.37) % 1;
                        c.BeginPath();
                        c.Arc(14 * s, -74 * s + ph * 70 * s, 0.9 * s, 0, Math.PI * 2);
                        c.Fill();
                    }
                    c.Restore();
                }
            });
        }

        /// <summary>Odysseus, back from Troy, who saw Sisyphus at his stone in the land of the dead</summary>
        public static void drawOdysseus(Ctx ctx, double x, double y, double s, FigLight L)
        {
            double t = L.t;
            paintLit(ctx, "myth-odysseus", x, y, new Box(-30 * s, -112 * s, 30 * s, 4 * s), L, s, c =>
            {
                double sway = Math.Sin(t * 0.7) * 0.6 * s;
                // the spear he leans on
                c.StrokeStyle = "rgba(96, 72, 46, 1)";
                c.LineWidth = 1.6 * s;
                c.BeginPath();
                c.MoveTo(-15 * s, 0);
                c.LineTo(-17 * s, -104 * s);
                c.Stroke();
                c.FillStyle = "rgba(170, 160, 150, 1)";
                c.BeginPath();
                c.MoveTo(-17 * s, -112 * s);
                c.LineTo(-19.2 * s, -103 * s);
                c.LineTo(-14.8 * s, -103 * s);
                c.ClosePath();
                c.Fill();
                robe(c, s, new RGB(150, 112, 74), sway, 0.95, -14 * s);
                // legs below the short tunic
                c.StrokeStyle = rgba(SKIN, 1);
                c.LineWidth = 3.6 * s;
                c.BeginPath();
                c.MoveTo(-4 * s, -16 * s); c.LineTo(-5 * s, -1 * s);
                c.MoveTo(4 * s, -16 * s); c.LineTo(5 * s, -1 * s);
                c.Stroke();
                // the travelling cloak
                c.FillStyle = "rgba(92, 64, 44, 1)";
                c.BeginPath();
                c.MoveTo(-6 * s, -66 * s);
                c.QuadraticCurveTo(14 * s, -60 * s, 14 * s + sway, -26 * s);
                c.LineTo(8 * s + sway, -28 * s);
                c.QuadraticCurveTo(6 * s, -54 * s, -8 * s, -60 * s);
                c.ClosePath();
                c.Fill();
                head(c, s, SKIN, new RGB(60, 42, 30), 0, -75 * s, true, false);
                // the pilos, a sailor's felt cap
                c.FillStyle = "rgba(120, 96, 66, 1)";
                c.BeginPath();
                c.MoveTo(-6.6 * s, -80 * s);
                c.QuadraticCurveTo(1 * s, -94 * s, 7 * s, -80 * s);
                c.ClosePath();
                c.Fill();
                arm(c, s, SKIN, -4 * s, -60 * s, -12 * s, -50 * s, -16 * s, -56 * s);
            });
        }

        /// <summary>Ixion bound to his burning wheel, rolling (the wheel's centre at x, y)</summary>
        public static void drawIxionWheel(Ctx ctx, double x, double y, double s, double spin, double t)
        {
            double rad = 30 * s;
            ctx.Save();
            ctx.Translate(x, y);
            // the fire round it
            ctx.GlobalCompositeOperation = Composite.Lighter;
            var g = ctx.CreateRadialGradient(0, 0, rad * 0.6, 0, 0, rad * 1.7);
            g.AddColorStop(0, "rgba(255, 170, 70, 0.55)");
            g.AddColorStop(1, "rgba(255, 90, 20, 0)");
            ctx.FillStyle = g;
            ctx.BeginPath();
            ctx.Arc(0, 0, rad * 1.7, 0, Math.PI * 2);
            ctx.Fill();
            ctx.GlobalCompositeOperation = Composite.SourceOver;
            ctx.Rotate(spin);
            ctx.StrokeStyle = "rgba(60, 36, 22, 1)";
            ctx.LineWidth = 4 * s;
            ctx.BeginPath();
            ctx.Arc(0, 0, rad, 0, Math.PI * 2);
            ctx.Stroke();
            ctx.LineWidth = 2.2 * s;
            for (int i = 0; i < 4; i++)
            {
                double a = i * Math.PI / 4;
                ctx.BeginPath();
                ctx.MoveTo(Math.Cos(a) * rad, Math.Sin(a) * rad);
                ctx.LineTo(-Math.Cos(a) * rad, -Math.Sin(a) * rad);
                ctx.Stroke();
            }
            // the bound man, spread across the spokes
            ctx.StrokeStyle = "rgba(196, 140, 104, 1)";
            ctx.LineWidth = 3 * s;
            ctx.BeginPath();
            ctx.MoveTo(-rad * 0.7, -rad * 0.7); ctx.LineTo(0, 0); ctx.LineTo(rad * 0.7, -rad * 0.7);
            ctx.MoveTo(-rad * 0.6, rad * 0.75); ctx.LineTo(0, 0); ctx.LineTo(rad * 0.6, rad * 0.75);
            ctx.Stroke();
            ctx.FillStyle = "rgba(196, 140, 104, 1)";
            ctx.BeginPath();
            ctx.Arc(0, -rad * 0.12, 4 * s, 0, Math.PI * 2);
            ctx.Fill();
            // flames licking the rim
            ctx.FillStyle = "rgba(255, 196, 90, 0.9)";
            for (int i = 0; i < 10; i++)
            {
                double a = i * Math.PI / 5 + Math.Sin(t * 7 + i) * 0.1;
                double fx = Math.Cos(a) * rad, fy = Math.Sin(a) * rad;
                ctx.BeginPath();
                ctx.Arc(fx, fy, (2 + Math.Sin(t * 11 + i * 2) * 0.8) * s, 0, Math.PI * 2);
                ctx.Fill();
            }
            ctx.Restore();
        }

        /// <summary>Thanatos' chains across the path: two posts and the links hanging between</summary>
        public static void drawDeathChains(Ctx ctx, double x, double y, double s, double t, double broken)
        {
            ctx.Save();
            ctx.StrokeStyle = "rgba(60, 56, 62, 1)";
            ctx.LineWidth = 3 * s;
            ctx.BeginPath();
            ctx.MoveTo(x - 14 * s, y); ctx.LineTo(x - 14 * s, y - 44 * s);
            ctx.MoveTo(x + 14 * s, y); ctx.LineTo(x + 14 * s, y - 44 * s);
            ctx.Stroke();
            if (broken < 1)
            {
                ctx.GlobalAlpha = 1 - broken;
                ctx.StrokeStyle = "rgba(150, 146, 156, 1)";
                ctx.LineWidth = 1.4 * s;
                for (int row = 0; row < 3; row++)
                {
                    double yy = y - (12 + row * 12) * s;
                    for (int i = 0; i < 6; i++)
                    {
                        double u = i / 5.0;
                        double lx = x - 14 * s + u * 28 * s;
                        double sag = Math.Sin(u * Math.PI) * 4 * s + Math.Sin(t * 2 + row) * 0.6 * s;
                        ctx.BeginPath();
                        ctx.Ellipse(lx, yy + sag + broken * 30 * s * u, 2.6 * s, 1.6 * s, (i % 2) * 1.2, 0, Math.PI * 2);
                        ctx.Stroke();
                    }
                }
            }
            ctx.Restore();
        }
    }
}
