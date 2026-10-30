using System;
using Sisifos.Paint;
using LineCap = Sisifos.Paint.LineCap;
using LineJoin = Sisifos.Paint.LineJoin;

namespace Sisifos.App.UI
{
    /// <summary>
    /// The menus' ornament, drawn by hand rather than set out by rule: a meander border with
    /// the wobble of a brush, palmettes at the corners, a black-figure frieze of the man and
    /// his stone going round like the band on a vase, a rule with laurel, and the start
    /// cut as a terracotta tablet with chipped corners. Every line carries a little
    /// irregularity of its own (a fixed seed, so it does not shimmer), the way a painted
    /// pot does, so nothing reads as stamped out by a machine.
    /// </summary>
    public static partial class Antique
    {
        public static readonly Col GOLD = Col.Rgba(214, 176, 104, 1);
        public static readonly Col TERRACOTTA = Col.Rgba(178, 92, 48, 1);
        public static readonly Col SLIP = Col.Rgba(24, 16, 14, 1);

        /// <summary>a fixed wobble, -1..1, for the i-th mark of a pattern</summary>
        public static double J(double i, double seed)
        {
            double x = Math.Sin(i * 127.1 + seed * 311.7) * 43758.5453;
            return (x - Math.Floor(x)) * 2 - 1;
        }

        static Col A(Col c, double a) { return Col.Rgba((int)(c.r * 255), (int)(c.g * 255), (int)(c.b * 255), a); }

        /// <summary>
        /// A running meander (Greek key) along a horizontal band from x0 to x1, its top at y,
        /// `size` tall: each key a little different, as if painted.
        /// </summary>
        public static void Meander(Ctx c, double x0, double x1, double y, double size, Col col, double a, double seed)
        {
            double u = size;
            c.StrokeStyle = A(col, a);
            c.LineWidth = Math.Max(1, size * 0.13);
            c.LineCap = LineCap.Square;
            c.LineJoin = LineJoin.Miter;
            // the two framing lines
            c.BeginPath();
            c.MoveTo(x0, y - u * 0.22);
            for (double x = x0; x <= x1; x += 24) c.LineTo(x, y - u * 0.22 + J(x, seed) * 0.35);
            c.LineTo(x1, y - u * 0.22);
            c.MoveTo(x0, y + u * 1.22);
            for (double x = x0; x <= x1; x += 24) c.LineTo(x, y + u * 1.22 + J(x, seed + 3) * 0.35);
            c.LineTo(x1, y + u * 1.22);
            c.Stroke();
            // the keys: up, over, down, back in, up — one square spiral each
            double step = u * 1.6;
            int n = (int)Math.Floor((x1 - x0) / step);
            double start = x0 + ((x1 - x0) - n * step) / 2;
            c.BeginPath();
            for (int i = 0; i < n; i++)
            {
                double x = start + i * step, j = J(i, seed) * u * 0.05;
                c.MoveTo(x, y + u + j);
                c.LineTo(x, y + j);
                c.LineTo(x + u, y - j);
                c.LineTo(x + u, y + u * 0.7);
                c.LineTo(x + u * 0.38, y + u * 0.7 + j);
                c.LineTo(x + u * 0.38, y + u * 0.35);
                c.MoveTo(x, y + u + j);
                c.LineTo(x + step, y + u - j);
            }
            c.Stroke();
        }

        /// <summary>a palmette: a fan of leaves from a volute base, the vase-painter's flower</summary>
        public static void Palmette(Ctx c, double x, double y, double size, Col col, double a, double rot)
        {
            c.Save();
            c.Translate(x, y);
            c.Rotate(rot);
            c.FillStyle = A(col, a);
            c.StrokeStyle = A(col, a);
            c.LineWidth = Math.Max(0.8, size * 0.05);
            for (int i = -3; i <= 3; i++)
            {
                double ang = -Math.PI / 2 + i * 0.32;
                double len = size * (1 - Math.Abs(i) * 0.12);
                double ex = Math.Cos(ang) * len, ey = Math.Sin(ang) * len;
                double nx = -Math.Sin(ang) * size * 0.11, ny = Math.Cos(ang) * size * 0.11;
                c.BeginPath();
                c.MoveTo(0, 0);
                c.QuadraticCurveTo(ex * 0.55 + nx, ey * 0.55 + ny, ex, ey);
                c.QuadraticCurveTo(ex * 0.55 - nx, ey * 0.55 - ny, 0, 0);
                c.Fill();
            }
            // the two volutes it springs from
            for (int s = -1; s <= 1; s += 2)
            {
                c.BeginPath();
                c.Arc(s * size * 0.32, size * 0.12, size * 0.2, s < 0 ? 0 : Math.PI, s < 0 ? Math.PI * 1.6 : Math.PI * 2.6, s > 0);
                c.Stroke();
            }
            c.Restore();
        }

        /// <summary>
        /// The menu's frame: the edges darkened with uneven strokes like a worn painting, a
        /// meander band across the top and the bottom, a thin double line down each side,
        /// a palmette in each corner.
        /// </summary>
        public static void FramePaint(Ctx c, double w, double h, double seed)
        {
            // the worn edge: a soft dark border, broken by brush marks
            for (int i = 0; i < 40; i++)
            {
                bool vert = i % 2 == 0;
                double along = (J(i, seed) * 0.5 + 0.5) * (vert ? h : w);
                double r = 30 + (J(i, seed + 1) * 0.5 + 0.5) * 50;
                double x = vert ? (i % 4 == 0 ? 0 : w) : along;
                double y = vert ? along : (i % 4 == 1 ? 0 : h);
                var g = c.CreateRadialGradient(x, y, 0, x, y, r);
                g.AddColorStop(0, "rgba(4, 3, 6, 0.38)");
                g.AddColorStop(1, "rgba(4, 3, 6, 0)");
                c.FillStyle = g;
                c.FillRect(x - r, y - r, r * 2, r * 2);
            }
            double m = Math.Max(10, Math.Min(w, h) * 0.035);
            double size = Math.Max(7, Math.Min(11, h * 0.022));
            Meander(c, m + size * 2.4, w - m - size * 2.4, m, size, GOLD, 0.42, seed);
            Meander(c, m + size * 2.4, w - m - size * 2.4, h - m - size, size, GOLD, 0.42, seed + 7);
            c.StrokeStyle = A(GOLD, 0.28);
            c.LineWidth = 1;
            c.BeginPath();
            for (int s = 0; s < 2; s++)
            {
                double x = s == 0 ? m : w - m;
                for (int d = 0; d < 2; d++)
                {
                    double xx = x + (s == 0 ? 1 : -1) * d * 4;
                    c.MoveTo(xx, m + size * 2.4);
                    for (double y = m + size * 2.4; y <= h - m - size * 2.4; y += 30)
                        c.LineTo(xx + J(y + d * 9, seed + s) * 0.5, y);
                    c.LineTo(xx, h - m - size * 2.4);
                }
            }
            c.Stroke();
            Palmette(c, m + size * 0.6, m + size * 1.6, size * 2.2, GOLD, 0.55, -Math.PI / 4);
            Palmette(c, w - m - size * 0.6, m + size * 1.6, size * 2.2, GOLD, 0.55, Math.PI / 4);
            Palmette(c, m + size * 0.6, h - m - size * 0.6, size * 2.2, GOLD, 0.55, -Math.PI * 3 / 4);
            Palmette(c, w - m - size * 0.6, h - m - size * 0.6, size * 2.2, GOLD, 0.55, Math.PI * 3 / 4);
        }

        /// <summary>the moment card's page: the frame he was in, bordered, the cycle and the climb under it</summary>
        public static void MomentCard(Ctx c, Surface shot, double W, double H, string big, string small, string SERIF)
        {
            // the frame: as wide as the page allows, its height the screen's own proportion
            double fw = W - 120, fh = Math.Min(H * 0.62, fw * shot.height / (double)shot.width);
            // a tall screen (a phone held upright) is cropped to the middle
            double sw = shot.width, sh = shot.height, sx = 0, sy = 0;
            if (sh / sw > fh / fw) { double keep = sw * fh / fw; sy = (sh - keep) / 2; sh = keep; }
            // the whole group (title, key band, picture, key band, the words) centred on the page
            double group = 70 + 40 + fh + 40 + 230;
            double fx = 60, fy = (H - group) / 2 + 110;
            c.Save();
            c.ShadowColor = Col.Parse("rgba(0,0,0,0.7)");
            c.ShadowBlur = 40;
            c.ShadowOffsetY = 14;
            c.FillStyle = "#0b0a0e";
            c.FillRect(fx - 10, fy - 10, fw + 20, fh + 20);
            c.Restore();
            c.DrawImage(shot, sx, sy, sw, sh, fx, fy, fw, fh);
            // the picture's own vignette, as if printed
            var v = c.CreateRadialGradient(fx + fw / 2, fy + fh / 2, fh * 0.45, fx + fw / 2, fy + fh / 2, fw * 0.62);
            v.AddColorStop(0, "rgba(8, 6, 10, 0)");
            v.AddColorStop(1, "rgba(8, 6, 10, 0.4)");
            c.FillStyle = v;
            c.FillRect(fx, fy, fw, fh);
            c.StrokeStyle = "rgba(214,176,104,0.6)";
            c.LineWidth = 2;
            c.StrokeRect(fx - 10, fy - 10, fw + 20, fh + 20);
            // a Greek key above and below, palmettes at the corners
            Meander(c, fx + 30, fx + fw - 30, fy - 46, 14, GOLD, 0.6, 3.7);
            Meander(c, fx + 30, fx + fw - 30, fy + fh + 32, 14, GOLD, 0.6, 9.1);
            Palmette(c, fx + 4, fy - 30, 26, GOLD, 0.7, -Math.PI / 4);
            Palmette(c, fx + fw - 4, fy - 30, 26, GOLD, 0.7, Math.PI / 4);
            Palmette(c, fx + 4, fy + fh + 30, 26, GOLD, 0.7, -Math.PI * 3 / 4);
            Palmette(c, fx + fw - 4, fy + fh + 30, 26, GOLD, 0.7, Math.PI * 3 / 4);

            c.TextAlign = TextAlign.Center;
            c.FillStyle = "rgba(214,176,104,0.9)";
            c.Font = "600 32px " + SERIF;
            c.FillText("S I S Y P H U S", W / 2.0, fy - 82);
            double ty = fy + fh + 150;
            c.FillStyle = "#efe2c4";
            c.Font = "italic 500 80px " + SERIF;
            c.FillText(big, W / 2.0, ty);
            c.Save();
            c.Translate(W / 2.0 - 160, ty + 24);
            Rule(c, 320, 30, 0.8, 2.2);
            c.Restore();
            c.FillStyle = "rgba(239,226,196,0.78)";
            c.Font = "500 38px " + SERIF;
            c.FillText(small, W / 2.0, ty + 104);
        }

        /// <summary>a rule under a title: hand-drawn lines out from a diamond, a laurel sprig to each side</summary>
        public static void Rule(Ctx c, double w, double h, double a, double seed)
        {
            double cx = w / 2, cy = h / 2;
            var g = c.CreateLinearGradient(0, 0, w, 0);
            g.AddColorStop(0, A(GOLD, 0));
            g.AddColorStop(0.5, A(GOLD, 0.75 * a));
            g.AddColorStop(1, A(GOLD, 0));
            c.StrokeStyle = g;
            c.LineWidth = 1;
            c.BeginPath();
            c.MoveTo(0, cy);
            for (double x = 0; x <= cx - 44; x += 18) c.LineTo(x, cy + J(x, seed) * 0.4);
            c.LineTo(cx - 44, cy);
            c.MoveTo(cx + 44, cy);
            for (double x = cx + 44; x <= w; x += 18) c.LineTo(x, cy + J(x, seed + 2) * 0.4);
            c.Stroke();
            c.FillStyle = A(GOLD, 0.9 * a);
            c.BeginPath();
            c.MoveTo(cx, cy - 5); c.LineTo(cx + 5, cy); c.LineTo(cx, cy + 5); c.LineTo(cx - 5, cy);
            c.ClosePath();
            c.Fill();
            // laurel: leaves along a curving stem, each pair a little different
            c.Save();
            c.Translate(cx, cy);
            c.Scale(1.6, 1.6);
            c.Translate(-cx, -cy);
            for (int s = -1; s <= 1; s += 2)
            {
                c.StrokeStyle = A(GOLD, 0.7 * a);
                c.LineWidth = 0.9;
                c.BeginPath();
                c.MoveTo(cx + s * 8, cy);
                c.QuadraticCurveTo(cx + s * 16, cy - 2, cx + s * 24, cy - 1);
                c.Stroke();
                for (int i = 0; i < 4; i++)
                {
                    double lx = cx + s * (10 + i * 4), ly = cy - 1 - i * 0.2;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double ang = s > 0 ? (side < 0 ? -0.9 : 0.7) : (side < 0 ? -Math.PI + 0.9 : Math.PI - 0.7);
                        ang += J(i * 2 + side, seed) * 0.12;
                        double len = 4.2 - i * 0.4;
                        c.FillStyle = A(GOLD, (0.75 - i * 0.1) * a);
                        c.BeginPath();
                        c.Ellipse(lx + Math.Cos(ang) * len * 0.5, ly + Math.Sin(ang) * len * 0.5, len * 0.55, len * 0.22, ang, 0, Math.PI * 2);
                        c.Fill();
                    }
                }
            }
            c.Restore();
        }

        /// <summary>
        /// A tablet of fired clay for a button: chipped corners, a darker rim, a meander
        /// strip along top and bottom, light raking across it. `hover` warms it, `glow` (0..1)
        /// breathes a gold light round it.
        /// </summary>
        public static void Tablet(Ctx c, double w, double h, double hover, double glow, double seed)
        {
            double ch = Math.Min(8, h * 0.22);
            void shape(double inset)
            {
                c.BeginPath();
                c.MoveTo(ch + inset + J(1, seed) * 1.2, inset);
                c.LineTo(w - ch - inset + J(2, seed) * 1.2, inset + J(3, seed) * 0.8);
                c.LineTo(w - inset, ch + inset + J(4, seed));
                c.LineTo(w - inset + J(5, seed) * 0.8, h - ch * 0.6 - inset);
                c.LineTo(w - ch * 1.3 - inset, h - inset);
                c.LineTo(ch * 0.7 + inset + J(6, seed), h - inset + J(7, seed) * 0.6);
                c.LineTo(inset, h - ch - inset);
                c.LineTo(inset + J(8, seed) * 0.8, ch * 1.2 + inset);
                c.ClosePath();
            }
            if (glow > 0.001)
            {
                c.Save();
                c.ShadowColor = Col.Rgba(217, 180, 106, 0.45 * glow);
                var m = c.GetTransform();
                c.ShadowBlur = 22 * Math.Sqrt(Math.Abs(m.a * m.d));
                shape(0);
                c.FillStyle = Col.Rgba(120, 60, 30, 1);
                c.Fill();
                c.Restore();
            }
            shape(0);
            var g = c.CreateLinearGradient(0, 0, w * 0.3, h);
            g.AddColorStop(0, Col.Rgba(196 + (int)(20 * hover), 104 + (int)(16 * hover), 56 + (int)(8 * hover), 1));
            g.AddColorStop(1, Col.Rgba(132 + (int)(16 * hover), 64 + (int)(10 * hover), 34, 1));
            c.FillStyle = g;
            c.Fill();
            // grit in the clay
            c.FillStyle = "rgba(60, 24, 10, 0.25)";
            for (int i = 0; i < 26; i++)
            {
                double x = (J(i, seed + 4) * 0.5 + 0.5) * w, y = (J(i, seed + 5) * 0.5 + 0.5) * h;
                c.BeginPath();
                c.Arc(x, y, 0.5 + (J(i, seed + 6) * 0.5 + 0.5) * 0.7, 0, Math.PI * 2);
                c.Fill();
            }
            // black slip strips, top and bottom, with a small meander in reserve
            double band = Math.Max(3, h * 0.16);
            c.Save();
            shape(0);
            c.Clip();
            c.FillStyle = "rgba(24, 14, 12, 0.92)";
            c.FillRect(0, 0, w, band);
            c.FillRect(0, h - band, w, band);
            Meander(c, ch, w - ch, band * 0.18, band * 0.6, TERRACOTTA, 0.95, seed);
            Meander(c, ch, w - ch, h - band * 0.82, band * 0.6, TERRACOTTA, 0.95, seed + 1);
            c.Restore();
            // the rim: a lit upper edge, a dark lower one
            c.LineWidth = 1;
            c.StrokeStyle = Col.Rgba(255, 210, 160, 0.35 + 0.2 * hover);
            c.BeginPath();
            c.MoveTo(ch, 0.5); c.LineTo(w - ch, 0.5);
            c.Stroke();
            c.StrokeStyle = "rgba(30, 10, 4, 0.7)";
            shape(0.5);
            c.Stroke();
        }

        /// <summary>
        /// A black-figure frieze, the band round a vase: the man setting his shoulder to the
        /// stone and pushing it up the slope, again and again round the band, with palmettes
        /// between. `t` in seconds scrolls it and moves him.
        /// </summary>
        public static void Frieze(Ctx c, double w, double h, double t, double a)
        {
            c.Save();
            c.GlobalAlpha = a;
            // the band: terracotta between black lines, a tongue pattern along its top
            c.FillStyle = Col.Rgba(170, 86, 44, 0.92);
            c.FillRect(0, 2, w, h - 4);
            c.FillStyle = "rgba(20, 12, 10, 0.95)";
            c.FillRect(0, 0, w, 2);
            c.FillRect(0, h - 2, w, 2);
            for (double x = 0; x < w; x += 7)
            {
                c.BeginPath();
                c.Ellipse(x + 3.5, 2, 2.6, 3.2, 0, 0, Math.PI);
                c.Fill();
            }
            double panel = Math.Max(150, h * 5.2);
            double scroll = (t * 6) % panel;
            int n = (int)Math.Ceiling(w / panel) + 2;
            for (int i = -1; i < n; i++)
            {
                double x0 = i * panel - scroll;
                // the slope he climbs
                double gy = h - 4;
                c.FillStyle = "rgba(20, 12, 10, 0.95)";
                c.BeginPath();
                c.MoveTo(x0 + panel * 0.12, gy);
                c.LineTo(x0 + panel * 0.86, gy - h * 0.26);
                c.LineTo(x0 + panel * 0.86, gy);
                c.ClosePath();
                c.Fill();
                double push = Math.Sin(t * 1.6 + i * 1.3);
                double prog = 0.5 + push * 0.04;
                double sx = x0 + panel * prog, sy = gy - h * 0.26 * ((prog - 0.12) / 0.74);
                double R = h * 0.24;
                double bx = sx + R * 1.15, by = sy - R - h * 0.02;
                // the stone
                c.BeginPath();
                c.Arc(bx, by, R, 0, Math.PI * 2);
                c.Fill();
                // the man: in black figure, the leaning body, the driving legs, arms to the stone
                double k = h / 40;
                double lean = 0.95 + push * 0.04;
                double hipX = sx - 6 * k, hipY = sy - 11 * k;
                double shX = hipX + Math.Cos(-lean) * 14 * k, shY = hipY + Math.Sin(-lean) * 14 * k;
                void limb(double ax, double ay, double bx2, double by2, double wa, double wb)
                {
                    double dx = bx2 - ax, dy = by2 - ay, len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1e-6) return;
                    double nx = -dy / len, ny = dx / len;
                    c.BeginPath();
                    c.MoveTo(ax + nx * wa / 2, ay + ny * wa / 2);
                    c.LineTo(bx2 + nx * wb / 2, by2 + ny * wb / 2);
                    c.LineTo(bx2 - nx * wb / 2, by2 - ny * wb / 2);
                    c.LineTo(ax - nx * wa / 2, ay - ny * wa / 2);
                    c.ClosePath();
                    c.Fill();
                    c.BeginPath();
                    c.Arc(bx2, by2, wb / 2, 0, Math.PI * 2);
                    c.Fill();
                }
                double st = Math.Sin(t * 1.6 + i * 1.3) * 2 * k;
                limb(hipX, hipY, hipX - 6 * k + st, sy - 4 * k, 4.4 * k, 3 * k);
                limb(hipX - 6 * k + st, sy - 4 * k, hipX - 11 * k + st, sy + 0.5 * k, 3 * k, 2.2 * k);
                limb(hipX, hipY, hipX + 4 * k - st, sy - 5 * k, 4.4 * k, 3 * k);
                limb(hipX + 4 * k - st, sy - 5 * k, hipX + 1 * k - st, sy + 0.5 * k, 3 * k, 2.2 * k);
                limb(hipX, hipY, shX, shY, 6 * k, 7 * k);
                limb(shX, shY, bx - R * 0.95, by - R * 0.2, 3 * k, 2.4 * k);
                limb(shX - 1 * k, shY + 1.5 * k, bx - R * 0.9, by + R * 0.3, 3 * k, 2.4 * k);
                c.BeginPath();
                c.Arc(shX + 3 * k, shY - 3.2 * k, 3 * k, 0, Math.PI * 2);
                c.Fill();
                // incised lines, scratched through the black: the eye, the line of the back
                c.StrokeStyle = Col.Rgba(196, 110, 62, 0.9);
                c.LineWidth = Math.Max(0.6, 0.5 * k);
                c.BeginPath();
                c.Arc(shX + 4.2 * k, shY - 3.6 * k, 0.6 * k, 0, Math.PI * 2);
                c.MoveTo(hipX + 1 * k, hipY - 1 * k);
                c.QuadraticCurveTo(shX - 3 * k, (hipY + shY) / 2 - 2 * k, shX - 1 * k, shY - 1 * k);
                c.MoveTo(bx - R * 0.4, by - R * 0.5);
                c.QuadraticCurveTo(bx + R * 0.1, by - R * 0.1, bx + R * 0.3, by + R * 0.5);
                c.Stroke();
                // a palmette between the panels
                Palmette(c, x0 + panel * 0.02, h - 5, h * 0.42, SLIP, 0.95, 0);
            }
            // the band fades out toward the sides, so it reads as painted on, not cut off
            c.GlobalCompositeOperation = Composite.DestinationIn;
            c.GlobalAlpha = 1;
            var fade = c.CreateLinearGradient(0, 0, w, 0);
            fade.AddColorStop(0, "rgba(0,0,0,0)");
            fade.AddColorStop(0.12, "rgba(0,0,0,1)");
            fade.AddColorStop(0.88, "rgba(0,0,0,1)");
            fade.AddColorStop(1, "rgba(0,0,0,0)");
            c.FillStyle = fade;
            c.FillRect(0, 0, w, h);
            c.GlobalCompositeOperation = Composite.SourceOver;
            c.Restore();
        }

        /// <summary>
        /// The bands of a scene's letterbox, dressed: the bottom one (`top` false) a deep
        /// umber rather than flat black, a meander along its upper edge in gold touched with
        /// the speaker's colour, a palmette at each end, faint uprights framing the words; the
        /// top one a meander along its lower edge.
        /// </summary>
        public static void DialogBand(Ctx c, double w, double h, Col tint, bool top, double a)
        {
            var mix = Col.Rgba((GOLD.r * 0.65 + tint.r * 0.35) * 255, (GOLD.g * 0.65 + tint.g * 0.35) * 255, (GOLD.b * 0.65 + tint.b * 0.35) * 255, 1);
            double size = Math.Max(5, Math.Min(8, h * 0.05));
            if (top)
            {
                var gt = c.CreateLinearGradient(0, 0, 0, h);
                gt.AddColorStop(0, "rgba(10, 7, 6, 1)");
                gt.AddColorStop(1, "rgba(22, 14, 10, 1)");
                c.FillStyle = gt;
                c.FillRect(0, 0, w, h);
                Meander(c, w * 0.06, w * 0.94, h - size * 2.1, size, mix, 0.35 * a, 4.2);
                return;
            }
            var g = c.CreateLinearGradient(0, 0, 0, h);
            g.AddColorStop(0, "rgba(24, 15, 11, 1)");
            g.AddColorStop(0.5, "rgba(14, 9, 8, 1)");
            g.AddColorStop(1, "rgba(6, 4, 4, 1)");
            c.FillStyle = g;
            c.FillRect(0, 0, w, h);
            // a warm glow behind the words, as from a lamp below the frame
            var lamp = c.CreateRadialGradient(w / 2, h * 0.62, 0, w / 2, h * 0.62, Math.Max(w, h) * 0.5);
            lamp.AddColorStop(0, Col.Rgba(120, 70, 36, 0.16 * a));
            lamp.AddColorStop(1, "rgba(120, 70, 36, 0)");
            c.FillStyle = lamp;
            c.FillRect(0, 0, w, h);
            Meander(c, w * 0.05, w * 0.95, size * 1.4, size, mix, 0.42 * a, 8.8);
            double py = h * 0.55;
            Palmette(c, w * 0.035, py + size * 2, size * 3, mix, 0.5 * a, -Math.PI / 2);
            Palmette(c, w * 0.965, py + size * 2, size * 3, mix, 0.5 * a, Math.PI / 2);
            c.StrokeStyle = A(mix, 0.18 * a);
            c.LineWidth = 1;
            c.BeginPath();
            for (int s = 0; s < 2; s++)
            {
                double x = s == 0 ? w * 0.075 : w * 0.925;
                for (int d = 0; d < 2; d++)
                {
                    double xx = x + (s == 0 ? 1 : -1) * d * 3;
                    c.MoveTo(xx, size * 4.4);
                    for (double y = size * 4.4; y < h - size * 2; y += 20) c.LineTo(xx + J(y + d, 3 + s) * 0.4, y);
                }
            }
            c.Stroke();
        }

        /// <summary>
        /// A plaque of white marble: a slab with one corner chipped, grey veins wandering
        /// through it, a carved border just inside its edge (a cut line in shadow with the lit
        /// lip beside it) and a bronze pin in each corner.
        /// </summary>
        public static void Marble(Ctx c, double w, double h, double seed)
        {
            c.Save();
            c.BeginPath();
            double ch = Math.Min(10, h * 0.12);
            c.MoveTo(0, 2);
            c.LineTo(w - ch, 0);
            c.LineTo(w, ch * 0.8);
            c.LineTo(w, h);
            c.LineTo(2, h);
            c.LineTo(0, h - 2);
            c.ClosePath();
            c.Clip();
            var g = c.CreateLinearGradient(0, 0, w * 0.4, h);
            g.AddColorStop(0, "#f2efe8");
            g.AddColorStop(0.6, "#e4dfd6");
            g.AddColorStop(1, "#d6d0c4");
            c.FillStyle = g;
            c.FillRect(0, 0, w, h);
            // the veins: long wandering grey lines, a few fine ones beside each
            for (int v = 0; v < 5; v++)
            {
                double y0 = (J(v, seed) * 0.5 + 0.5) * h, y1 = (J(v + 9, seed) * 0.5 + 0.5) * h;
                c.StrokeStyle = Col.Rgba(120, 118, 124, 0.12 + (J(v + 3, seed) * 0.5 + 0.5) * 0.14);
                c.LineWidth = 0.6 + (J(v + 5, seed) * 0.5 + 0.5) * 1.2;
                c.BeginPath();
                c.MoveTo(-4, y0);
                for (int i = 1; i <= 8; i++)
                {
                    double x = w * i / 8.0;
                    double y = y0 + (y1 - y0) * i / 8.0 + J(i * 7 + v, seed) * h * 0.08;
                    c.LineTo(x, y);
                }
                c.Stroke();
            }
            // a cloud in the stone
            var cl = c.CreateRadialGradient(w * 0.7, h * 0.35, 0, w * 0.7, h * 0.35, w * 0.4);
            cl.AddColorStop(0, "rgba(255, 255, 255, 0.35)");
            cl.AddColorStop(1, "rgba(255, 255, 255, 0)");
            c.FillStyle = cl;
            c.FillRect(0, 0, w, h);
            c.Restore();
            // the carved border
            double m = 6;
            c.StrokeStyle = "rgba(90, 84, 80, 0.35)";
            c.LineWidth = 1;
            c.BeginPath();
            c.Rect(m, m, w - m * 2, h - m * 2);
            c.Stroke();
            c.StrokeStyle = "rgba(255, 255, 255, 0.7)";
            c.BeginPath();
            c.Rect(m + 1, m + 1, w - m * 2, h - m * 2);
            c.Stroke();
            // the bronze pins
            foreach (var (px, py) in new[] { (m + 3.0, m + 3.0), (w - m - 3, m + 3), (m + 3, h - m - 3), (w - m - 3, h - m - 3) })
            {
                var pg = c.CreateRadialGradient(px - 0.6, py - 0.6, 0, px, py, 2.2);
                pg.AddColorStop(0, "#e6c37e");
                pg.AddColorStop(1, "#7a5524");
                c.FillStyle = pg;
                c.BeginPath();
                c.Arc(px, py, 2, 0, Math.PI * 2);
                c.Fill();
            }
        }

        /// <summary>papyrus over a page: the strips laid side by side and across, their fibres, a joint now and then; a ruled border in red ochre</summary>
        public static void Papyrus(Ctx c, double w, double h, double seed)
        {
            double x = 0;
            int i = 0;
            while (x < w)
            {
                double sw = 11 + (J(i, seed) * 0.5 + 0.5) * 9;
                c.FillStyle = J(i + 4, seed) > 0 ? Col.Rgba(255, 245, 220, 0.07) : Col.Rgba(120, 84, 40, 0.05);
                c.FillRect(x, 0, sw, h);
                c.FillStyle = Col.Rgba(110, 76, 36, 0.06);
                c.FillRect(x + sw - 0.8, 0, 0.8, h);
                x += sw;
                i++;
            }
            c.StrokeStyle = Col.Rgba(120, 86, 40, 0.06);
            c.LineWidth = 0.6;
            c.BeginPath();
            for (double y = 2; y < h; y += 3.2)
            {
                c.MoveTo(0, y);
                c.LineTo(w, y + J(y, seed) * 0.8);
            }
            c.Stroke();
            c.StrokeStyle = Col.Rgba(150, 64, 32, 0.28);
            c.LineWidth = 1;
            c.BeginPath();
            c.Rect(9, 9, w - 18, h - 18);
            c.Stroke();
            c.StrokeStyle = Col.Rgba(150, 64, 32, 0.16);
            c.BeginPath();
            c.Rect(12, 12, w - 24, h - 24);
            c.Stroke();
        }
    }
}
