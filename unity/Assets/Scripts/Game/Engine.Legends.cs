using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// The far-ridge figures drawn as figures, not stick-men: bodies built out of tapered
    /// limbs and filled masses, so even as silhouettes against the sky they read as a titan
    /// under the heavens, a centaur at full gallop drawing his bow, the three-headed hound,
    /// and — the largest of them — two armies meeting on a ridge, line against line, with
    /// spears, shields, standards, horsemen, arrows and dust. All in the silhouette colour the
    /// caller has set; the only colour of their own is fire, starlight and dust.
    /// </summary>
    public partial class SisyphusEngine
    {
        /// <summary>true while the ridge figures are drawn a second time as their sunlit rim</summary>
        private bool rimPass;
        /// <summary>the colour of the far air (for dust), set by drawLandmark</summary>
        private RGB landAir;

        /// <summary>a limb: a tapered capsule from a (width wa) to b (width wb)</summary>
        private static void seg(Ctx c, double ax, double ay, double bx, double by, double wa, double wb)
        {
            double dx = bx - ax, dy = by - ay;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) return;
            double nx = -dy / len, ny = dx / len;
            double ra = wa / 2, rb = wb / 2;
            c.BeginPath();
            c.MoveTo(ax + nx * ra, ay + ny * ra);
            c.LineTo(bx + nx * rb, by + ny * rb);
            c.LineTo(bx - nx * rb, by - ny * rb);
            c.LineTo(ax - nx * ra, ay - ny * ra);
            c.ClosePath();
            c.Fill();
            c.BeginPath();
            c.Arc(ax, ay, ra, 0, Math.PI * 2);
            c.Fill();
            c.BeginPath();
            c.Arc(bx, by, rb, 0, Math.PI * 2);
            c.Fill();
        }

        /// <summary>a two-bone limb from a to c, bending at b (all three given), muscled at the top</summary>
        private static void limb3(Ctx c, double ax, double ay, double bx, double by, double cx, double cy, double w0, double w1, double w2)
        {
            seg(c, ax, ay, bx, by, w0, w1);
            seg(c, bx, by, cx, cy, w1, w2);
        }

        /// <summary>a faint lit line inside a silhouette: muscle, fold or edge catching the light</summary>
        private void litLine(Ctx c, double k, double a)
        {
            if (this.rimPass) return;
            c.StrokeStyle = Col.Rgba(255, 226, 190, Css.fixedNum(a, 3));
            c.LineWidth = 0.9 * k;
        }

        // ------------------------------------------------------------------ Atlas

        private void drawAtlas2(Ctx c, double x, double y, double k, double seed)
        {
            // On one knee on his rock, the sphere of the heavens on his shoulders and in his
            // hands, his head bowed under it. He breathes; every so often he heaves it higher
            // and lets it settle, and the whole sky lurches with him.
            double heaveCycle = ((this.t + seed * 20) % 11) / 11;
            double heave = heaveCycle < 0.18 ? Math.Sin(heaveCycle / 0.18 * Math.PI) : 0;
            double breathe = Math.Sin(this.t * 1.1 + seed * 5) * 1.2;
            double L = breathe + heave * 7;
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            double X(double u) { return x + u * k; }
            double Y(double v) { return y + v * k; }
            // the rock: a broken outcrop with a few loose stones
            c.BeginPath();
            c.MoveTo(X(-46), Y(0));
            c.LineTo(X(-40), Y(-8));
            c.LineTo(X(-26), Y(-13));
            c.LineTo(X(-12), Y(-12));
            c.LineTo(X(4), Y(-16));
            c.LineTo(X(22), Y(-13));
            c.LineTo(X(36), Y(-7));
            c.LineTo(X(46), Y(0));
            c.ClosePath();
            c.Fill();
            c.BeginPath();
            c.Arc(X(-52), Y(-2), 3 * k, Math.PI, 0);
            c.Arc(X(52), Y(-1.5), 2.2 * k, Math.PI, 0);
            c.Fill();

            double hipX = -4, hipY = -44 - L * 0.2;
            double chX = 2, chY = -80 - L * 0.45;
            // the back leg: knee on the rock, the shin along it, toes dug in
            limb3(c, X(hipX - 2), Y(hipY + 2), X(-18), Y(-16), X(-38), Y(-14), 13 * k, 8 * k, 5 * k);
            c.BeginPath();
            c.Ellipse(X(-40), Y(-15), 4 * k, 2 * k, 0.3, 0, Math.PI * 2);
            c.Fill();
            // the torso: a deep chest, the back bowed under the weight
            c.BeginPath();
            c.MoveTo(X(hipX - 9), Y(hipY + 4));
            c.BezierCurveTo(X(hipX - 16), Y(hipY - 14), X(chX - 20), Y(chY + 6), X(chX - 13), Y(chY - 4)); // the back, bowed
            c.QuadraticCurveTo(X(chX - 4), Y(chY - 12), X(chX + 14), Y(chY - 6)); // across the shoulders
            c.QuadraticCurveTo(X(chX + 17), Y(chY + 8), X(chX + 11), Y(chY + 16)); // the chest
            c.QuadraticCurveTo(X(chX + 9), Y(hipY - 8), X(hipX + 9), Y(hipY + 2)); // belly
            c.ClosePath();
            c.Fill();
            // the front leg: thigh forward, knee up, foot planted
            limb3(c, X(hipX + 4), Y(hipY), X(20), Y(-38 - L * 0.1), X(22), Y(-15), 14 * k, 8.5 * k, 5.5 * k);
            c.BeginPath();
            c.Ellipse(X(25), Y(-15), 5 * k, 2.2 * k, 0, 0, Math.PI * 2);
            c.Fill();
            // a cloth at the hip, its end lifting in the wind
            double flap = Math.Sin(this.t * 2.2 + seed) * 2 * k * this.windiness();
            c.BeginPath();
            c.MoveTo(X(hipX - 10), Y(hipY - 2));
            c.LineTo(X(hipX + 10), Y(hipY - 4));
            c.LineTo(X(hipX + 6), Y(hipY + 8));
            c.LineTo(X(hipX - 14) - flap, Y(hipY + 10));
            c.ClosePath();
            c.Fill();

            // the arms, up to the sphere: shoulder, elbow out wide, hands spread on it
            double sy = -146 - L;
            double R = 31;
            double slX = chX - 11, slY = chY - 4, srX = chX + 13, srY = chY - 6;
            limb3(c, X(slX), Y(slY), X(-24), Y(chY - 18 - L * 0.3), X(-16), Y(sy + 24), 10 * k, 7.5 * k, 5 * k);
            limb3(c, X(srX), Y(srY), X(32), Y(chY - 20 - L * 0.3), X(25), Y(sy + 25), 10 * k, 7.5 * k, 5 * k);
            // the head, bowed, with its beard and hair
            double hx = 9, hy = chY - 13 - L * 0.1;
            c.BeginPath();
            c.Ellipse(X(hx), Y(hy), 6.2 * k, 7 * k, 0.4, 0, Math.PI * 2);
            c.Fill();
            c.BeginPath();
            c.MoveTo(X(hx + 2), Y(hy + 3));
            c.QuadraticCurveTo(X(hx + 9), Y(hy + 8), X(hx + 5), Y(hy + 14));
            c.QuadraticCurveTo(X(hx + 1), Y(hy + 9), X(hx - 2), Y(hy + 5));
            c.ClosePath();
            c.Fill();

            // the heavens: a sphere bound in rings, the band of the zodiac round it
            double cxs = X(4), cys = Y(sy);
            c.BeginPath();
            c.Arc(cxs, cys, R * k, 0, Math.PI * 2);
            c.Fill();
            // the hands, spread over its lower curve
            for (int side = -1; side <= 1; side += 2)
            {
                double hx0 = side < 0 ? -16 : 25, hy0 = sy + (side < 0 ? 24 : 25);
                for (int f = 0; f < 4; f++)
                {
                    double a = (side < 0 ? 2.2 : 0.95) + (f - 1.5) * 0.12 * side;
                    seg(c, X(hx0), Y(hy0), cxs + Math.Cos(a) * (R + 1.5) * k, cys + Math.Sin(a) * (R + 1.5) * k, 2.4 * k, 1.6 * k);
                }
            }
            if (!this.rimPass)
            {
                double rot = this.t * 0.15 + seed;
                c.Save();
                c.BeginPath();
                c.Arc(cxs, cys, R * k, 0, Math.PI * 2);
                c.Clip();
                // the inner glow of the heavens
                var gl = c.CreateRadialGradient(cxs - 8 * k, cys - 10 * k, 0, cxs, cys, R * k);
                gl.AddColorStop(0, "rgba(150, 170, 255, 0.22)");
                gl.AddColorStop(1, "rgba(150, 170, 255, 0)");
                c.FillStyle = gl;
                c.FillRect(cxs - R * k, cys - R * k, 2 * R * k, 2 * R * k);
                c.StrokeStyle = "rgba(255, 236, 200, 0.35)";
                c.LineWidth = 1.1 * k;
                // meridians turning
                for (int i = 0; i < 4; i++)
                {
                    double a = rot + i * Math.PI / 4;
                    double rx = Math.Abs(Math.Cos(a)) * R * k;
                    c.BeginPath();
                    c.Ellipse(cxs, cys, Math.Max(0.5, rx), R * k, 0.25, 0, Math.PI * 2);
                    c.Stroke();
                }
                // the equator and the zodiac's band, tilted, with its marks
                c.BeginPath();
                c.Ellipse(cxs, cys, R * k, R * 0.3 * k, 0.25, 0, Math.PI * 2);
                c.Stroke();
                c.StrokeStyle = "rgba(255, 210, 140, 0.5)";
                c.LineWidth = 3 * k;
                c.BeginPath();
                c.Ellipse(cxs, cys, R * k, R * 0.42 * k, -0.4, 0, Math.PI);
                c.Stroke();
                c.StrokeStyle = "rgba(40, 30, 50, 0.6)";
                c.LineWidth = 0.8 * k;
                c.BeginPath();
                for (int i = 0; i < 12; i++)
                {
                    double a = (i + 0.5) / 12 * Math.PI + rot * 0.3 % (Math.PI / 12);
                    double ex = Math.Cos(a) * R * k, ey = Math.Sin(a) * R * 0.42 * k;
                    double ca = Math.Cos(-0.4), sa = Math.Sin(-0.4);
                    double px = cxs + ex * ca - ey * sa, py = cys + ex * sa + ey * ca;
                    c.MoveTo(px, py - 1.6 * k);
                    c.LineTo(px, py + 1.6 * k);
                }
                c.Stroke();
                // stars and the lines of their figures
                c.StrokeStyle = "rgba(220, 230, 255, 0.28)";
                c.LineWidth = 0.6 * k;
                double pxPrev = 0, pyPrev = 0;
                for (int i = 0; i < 11; i++)
                {
                    double a = rot * 1.3 + i * 1.7 + seed;
                    double r = R * 0.85 * k * Math.Sqrt(this.hash(i * 3.3 + seed));
                    double px = cxs + Math.Cos(a) * r, py = cys + Math.Sin(a) * r * 0.9;
                    if (i % 4 != 0)
                    {
                        c.BeginPath();
                        c.MoveTo(pxPrev, pyPrev);
                        c.LineTo(px, py);
                        c.Stroke();
                    }
                    double tw = 0.55 + 0.45 * Math.Sin(this.t * 2 + i * 1.3);
                    c.FillStyle = Col.Rgba(255, 246, 216, Css.fixedNum(0.5 + 0.45 * tw, 3));
                    c.BeginPath();
                    c.Arc(px, py, (0.8 + (i % 3) * 0.35) * k, 0, Math.PI * 2);
                    c.Fill();
                    pxPrev = px; pyPrev = py;
                }
                c.Restore();
                // a sheen on the side toward the light
                c.StrokeStyle = "rgba(255, 240, 210, 0.3)";
                c.LineWidth = 1.6 * k;
                c.BeginPath();
                c.Arc(cxs, cys, (R - 1.2) * k, -2.6, -1.4);
                c.Stroke();
                // the body's lines: spine, the shoulder blade, the thigh
                litLine(c, k, 0.16);
                c.BeginPath();
                c.MoveTo(X(hipX - 9), Y(hipY - 6));
                c.BezierCurveTo(X(hipX - 13), Y(hipY - 18), X(chX - 15), Y(chY + 6), X(chX - 9), Y(chY - 4));
                c.MoveTo(X(chX - 2), Y(chY + 2));
                c.QuadraticCurveTo(X(chX + 6), Y(chY + 8), X(chX + 9), Y(chY + 2));
                c.MoveTo(X(hipX + 6), Y(hipY - 2));
                c.QuadraticCurveTo(X(12), Y(-42), X(19), Y(-38));
                c.Stroke();
            }
        }

        // ------------------------------------------------------------------ the centaur

        private void drawCentaur2(Ctx c, double x, double y, double k, double dir)
        {
            // Half man, half horse, at the gallop along the skyline; every few strides he
            // draws his bow to the cheek and looses, and the arrow goes on ahead.
            double g = this.t * 8.5;
            double bob = Math.Abs(Math.Sin(g)) * 2.2;
            c.Save();
            c.Translate(x, y - bob * k);
            c.Scale(dir * k, k);
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            double pitch = Math.Sin(g) * 0.04;
            c.Rotate(pitch);
            // the far legs first (a shade back), then the body, then the near legs
            void leg(bool front, double ph, bool far)
            {
                double s = Math.Sin(g + ph), cc = Math.Cos(g + ph);
                double hx = front ? 12 : -13, hy = -20;
                double kx, ky, fx, fy, ex, ey;
                if (front)
                {
                    kx = hx + 4 + s * 7; ky = hy + 10;
                    fx = kx + 2 + s * 5 - Math.Max(0, cc) * 4; fy = Math.Min(ky + 9, -2 + Math.Max(0, -cc) * -5);
                    ex = fx + 1 + s * 2; ey = Math.Min(0, fy + 4);
                }
                else
                {
                    kx = hx - 2 + s * 6; ky = hy + 8;
                    fx = kx - 3 - s * 2; fy = ky + 7 - Math.Max(0, cc) * 3;
                    ex = fx + 3 + s * 4; ey = Math.Min(0, fy + 6 - Math.Max(0, -cc) * 4);
                }
                limb3(c, hx, hy, kx, ky, fx, fy, front ? 6 : 8, 3.2, 2.4);
                seg(c, fx, fy, ex, ey, 2.4, 2.2);
                c.BeginPath();
                c.Ellipse(ex + 0.8, ey + 0.5, 2, 1.2, 0, 0, Math.PI * 2);
                c.Fill();
            }
            c.Save();
            c.GlobalAlpha *= 0.85;
            leg(true, 0.7, true);
            leg(false, 3.8, true);
            c.Restore();
            // the horse's body: chest, barrel, croup and haunch
            c.BeginPath();
            c.MoveTo(10, -32);
            c.QuadraticCurveTo(0, -30.5, -12, -31.5); // the back
            c.QuadraticCurveTo(-21, -31, -22, -24); // the croup
            c.QuadraticCurveTo(-23, -16, -17, -13); // the haunch
            c.QuadraticCurveTo(-4, -11, 8, -13.5); // the belly
            c.QuadraticCurveTo(17, -15, 18, -22); // the chest
            c.QuadraticCurveTo(18, -29, 14, -33);
            c.ClosePath();
            c.Fill();
            leg(true, 0, false);
            leg(false, 3.1, false);
            // the tail, three strands streaming
            for (int i = 0; i < 3; i++)
            {
                double w = Math.Sin(g * 0.5 + i) * 2;
                c.LineWidth = 2.6 - i * 0.6;
                c.BeginPath();
                c.MoveTo(-21, -29);
                c.BezierCurveTo(-28, -32 + i, -32, -26 + w, -38 - i * 2, -22 + w * 1.4 + i * 2);
                c.Stroke();
            }
            // the man: waist from the horse's shoulders, a broad back, the head turned ahead
            double draw = this.centaurDraw();
            c.BeginPath();
            c.MoveTo(10, -31);
            c.QuadraticCurveTo(9, -40, 11.5, -48);
            c.LineTo(19.5, -50);
            c.QuadraticCurveTo(20, -41, 17, -31);
            c.ClosePath();
            c.Fill();
            c.BeginPath();
            c.Ellipse(17, -54.5, 3.4, 3.9, 0.15, 0, Math.PI * 2);
            c.Fill();
            // hair streaming back, a band round it
            c.BeginPath();
            c.MoveTo(15, -57);
            c.QuadraticCurveTo(8, -58 + Math.Sin(g) * 0.8, 4, -53 + Math.Sin(g * 0.7) * 1.2);
            c.QuadraticCurveTo(10, -54, 14.5, -52);
            c.ClosePath();
            c.Fill();
            // the bow arm straight out ahead, the bow upright in the fist
            seg(c, 18, -48, 31, -48.5, 3.4, 2.4);
            c.LineWidth = 1.5;
            c.BeginPath();
            c.MoveTo(32, -61);
            c.QuadraticCurveTo(38, -55, 33.5, -48.5);
            c.QuadraticCurveTo(38, -42, 32, -36);
            c.Stroke();
            // the string, drawn back to the cheek with the other hand
            double hxs = 31.5 - draw * 12;
            seg(c, 13, -48, 9 + draw * 2, -44 + draw * 1, 3.4, 2.6);
            seg(c, 9 + draw * 2, -44 + draw * 1, hxs, -50, 2.6, 2.2);
            c.LineWidth = 0.6;
            c.BeginPath();
            c.MoveTo(32, -61);
            c.LineTo(hxs, -50);
            c.LineTo(32, -36);
            c.Stroke();
            if (draw > 0.15)
            {
                c.LineWidth = 0.9;
                c.BeginPath();
                c.MoveTo(hxs, -50);
                c.LineTo(36, -49.5);
                c.Stroke();
            }
            // the quiver on his back, arrows up out of it
            seg(c, 9, -40, 6, -53, 3, 3.4);
            c.LineWidth = 0.7;
            c.BeginPath();
            c.MoveTo(5.5, -53); c.LineTo(4.5, -57);
            c.MoveTo(6.8, -53); c.LineTo(6.6, -57.5);
            c.Stroke();
            // the arrow loosed, flying on ahead
            double fly = this.centaurFly();
            if (fly >= 0 && fly < 1)
            {
                double ax = 36 + fly * 140, ay = -49.5 - Math.Sin(fly * Math.PI) * 14;
                c.LineWidth = 0.9;
                c.BeginPath();
                c.MoveTo(ax, ay);
                c.LineTo(ax - 8, ay + 0.6);
                c.Stroke();
            }
            if (!this.rimPass)
            {
                // the horse's muscles and the man's back, catching light
                c.StrokeStyle = "rgba(255, 226, 190, 0.16)";
                c.LineWidth = 0.8;
                c.BeginPath();
                c.MoveTo(-17, -27); c.QuadraticCurveTo(-13, -21, -15, -15);
                c.MoveTo(13, -27); c.QuadraticCurveTo(15, -22, 13, -17);
                c.MoveTo(-6, -29); c.QuadraticCurveTo(0, -27, 6, -29);
                c.MoveTo(13, -46); c.QuadraticCurveTo(15, -40, 14, -34);
                c.Stroke();
            }
            c.Restore();
        }

        /// <summary>how far the centaur's bow is drawn (0..1): a draw, a hold, the loose</summary>
        private double centaurDraw()
        {
            double u = (this.t % 3.2) / 3.2;
            if (u < 0.35) return u / 0.35;
            if (u < 0.5) return 1;
            return 0;
        }

        /// <summary>the loosed arrow's flight (0..1), or -1 before it is loosed</summary>
        private double centaurFly()
        {
            double u = (this.t % 3.2) / 3.2;
            return u < 0.5 ? -1 : (u - 0.5) / 0.4;
        }

        // ------------------------------------------------------------------ Cerberus

        private void drawCerberus2(Ctx c, double x, double y, double k, double dir)
        {
            // The hound of the underworld pacing its stretch of the ridge: a mastiff's deep
            // chest and narrow waist, a mane of spines, three heads each with its own mind —
            // one snapping now and then — and a serpent for a tail.
            double g = this.t * 4;
            c.Save();
            c.Translate(x, y);
            c.Scale(dir * k, k);
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            void leg(double hx, double ph, double w, bool hind)
            {
                double s = Math.Sin(g + ph);
                double kx = hx + (hind ? -3 : 2) + s * 2.5, ky = -9;
                double fx = hx + s * 4, fy = -1.5;
                limb3(c, hx, -15, kx, ky, fx, fy, w, w * 0.55, w * 0.45);
                c.BeginPath();
                c.Ellipse(fx + 1.4, -1.2, 2.4, 1.3, 0, 0, Math.PI * 2);
                c.Fill();
            }
            c.Save();
            c.GlobalAlpha *= 0.85;
            leg(9, Math.PI, 5.5, false);
            leg(-11, 1.4, 6.5, true);
            c.Restore();
            // the body
            c.BeginPath();
            c.MoveTo(14, -26);
            c.QuadraticCurveTo(2, -25, -8, -22); // the back
            c.QuadraticCurveTo(-17, -24, -18, -16); // the haunch
            c.QuadraticCurveTo(-16, -11, -10, -12);
            c.QuadraticCurveTo(-2, -13, 4, -11); // the tucked belly
            c.QuadraticCurveTo(14, -8, 17, -16); // the deep chest
            c.QuadraticCurveTo(18, -23, 14, -26);
            c.ClosePath();
            c.Fill();
            leg(11, 0, 5.5, false);
            leg(-13, 1.4 + Math.PI, 6.5, true);
            // the mane of spines along the neck and back
            c.BeginPath();
            for (int i = 0; i < 7; i++)
            {
                double bx = 13 - i * 3, by = -25.5 + i * 0.4;
                c.MoveTo(bx - 1.2, by + 0.5);
                c.LineTo(bx - 0.4 - i * 0.2, by - 4 + Math.Sin(this.t * 3 + i) * 0.4);
                c.LineTo(bx + 1.2, by + 0.5);
            }
            c.Fill();
            // the serpent tail, lashing, its head raised
            double lash = Math.Sin(g * 0.8) * 4;
            c.LineWidth = 2.2;
            c.BeginPath();
            c.MoveTo(-17, -20);
            c.BezierCurveTo(-26, -20 + lash, -24, -32 - lash, -32, -30);
            c.Stroke();
            c.BeginPath();
            c.Ellipse(-33.5, -30.5, 2.6, 1.6, 0.3, 0, Math.PI * 2);
            c.Fill();
            // three necks and heads
            for (int i = 0; i < 3; i++)
            {
                double look = Math.Sin(this.t * (1.3 + i * 0.4) + i * 2) * 0.22;
                double a = -1.05 + i * 0.5 + look;
                double nx = 14 + Math.Cos(a) * 13, ny = -22 + Math.Sin(a) * 13;
                seg(c, 13, -21, nx, ny, 7, 4.6);
                double snap = Math.Max(0, Math.Sin(this.t * 1.7 + i * 2.4)) > 0.92 ? 1 : 0;
                c.Save();
                c.Translate(nx, ny);
                c.Rotate(a * 0.45 + 0.1);
                // skull, the muzzle, the jaws (open when it snaps), the ears laid back
                c.BeginPath();
                c.Ellipse(0, 0, 4.2, 3.6, 0, 0, Math.PI * 2);
                c.Fill();
                c.BeginPath();
                c.MoveTo(2, -2.2);
                c.LineTo(8.5, -1.2);
                c.QuadraticCurveTo(9.4, 0, 8.6, 0.6);
                c.LineTo(2.4, 1.2);
                c.ClosePath();
                c.Fill();
                c.BeginPath();
                c.MoveTo(2.4, 1.4);
                c.LineTo(7.8, 1.2 + snap * 2.6);
                c.LineTo(7.2, 2.4 + snap * 2.8);
                c.LineTo(1.6, 3);
                c.ClosePath();
                c.Fill();
                c.BeginPath();
                c.MoveTo(-1.2, -3);
                c.LineTo(-4.5, -6.2);
                c.LineTo(-1.6, -1.2);
                c.ClosePath();
                c.Fill();
                if (!this.rimPass)
                {
                    // the eye, a coal
                    c.FillStyle = "rgba(255, 110, 40, 0.85)";
                    c.BeginPath();
                    c.Arc(2.2, -1.4, 0.55, 0, Math.PI * 2);
                    c.Fill();
                }
                c.Restore();
            }
            c.Restore();
        }

        // ------------------------------------------------------------------ the battle

        /// <summary>
        /// Two armies meeting on the ridge. Phalanx against phalanx, three ranks deep, the
        /// front ranks' spears levelled and thrusting, the rear ranks' raised in a forest; the
        /// line where they meet sways one way and the other. Behind each, a standard and a
        /// horseman riding the flank; volleys of arrows arc over; dust rises where they clash,
        /// and a few have fallen.
        /// </summary>
        private void drawBattle(Ctx c, double x, double y, double k, double seed)
        {
            double sway = Math.Sin(this.t * 0.23 + seed * 9) * 10 + Math.Sin(this.t * 0.61 + seed) * 3;
            double front = x + sway * k;
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            // the dust of the clash, behind them
            if (!this.rimPass)
            {
                c.Save();
                var air = this.landAir;
                for (int i = 0; i < 9; i++)
                {
                    double u = ((this.t * 0.18 + i / 9.0 + seed) % 1 + 1) % 1;
                    double px = front + (this.hash(i * 1.7 + seed) - 0.5) * 70 * k + u * 12 * k * this.windiness();
                    double py = y - 6 * k - u * 34 * k;
                    double r = (6 + u * 14) * k;
                    c.FillStyle = Col.Rgba((int)Math.Min(255, air.r + 30), (int)Math.Min(255, air.g + 24), (int)Math.Min(255, air.b + 14), Css.fixedNum(0.22 * (1 - u) * Math.Min(1, u * 5), 3));
                    c.BeginPath();
                    c.Arc(px, py, r, 0, Math.PI * 2);
                    c.Fill();
                }
                c.Restore();
            }
            // the fallen, before the lines
            for (int i = 0; i < 5; i++)
            {
                double fx = x + (this.hash(i * 5.1 + seed) - 0.5) * 120 * k;
                if (Math.Abs(fx - front) < 10 * k) continue;
                c.BeginPath();
                c.Ellipse(fx, y - 1.4 * k, 5 * k, 1.5 * k, 0.05, 0, Math.PI * 2);
                c.Fill();
                c.BeginPath();
                c.Arc(fx + 5.5 * k, y - 2.2 * k, 2.4 * k, 0, Math.PI * 2);
                c.Fill();
                if (i % 2 == 0)
                {
                    c.LineWidth = 0.8 * k;
                    c.BeginPath();
                    c.MoveTo(fx - 9 * k, y - 0.5 * k);
                    c.LineTo(fx + 4 * k, y - 9 * k);
                    c.Stroke();
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                // ranks from the back forward, so the front rank stands over the rest
                for (int rank = 2; rank >= 0; rank--)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        double gap = 7.2 * k;
                        double bx = front + side * (5 * k + i * gap + rank * 2.4 * k);
                        // the files closest to the clash are tighter, pushing
                        if (i == 0) bx -= side * (rank == 0 ? 1.5 : 0) * k;
                        double ph = this.t * (rank == 0 ? 3.4 : 1.6) + i * 1.3 + rank * 0.7 + (side > 0 ? 2 : 0) + seed * 5;
                        this.hoplite(c, bx, y - rank * 0.6 * k, k * (1 - rank * 0.06), -side, rank, ph);
                    }
                }
                // the standard behind each line, its cloth waving
                double sx = front + side * 78 * k;
                c.LineWidth = 1.1 * k;
                c.BeginPath();
                c.MoveTo(sx, y);
                c.LineTo(sx, y - 46 * k);
                c.Stroke();
                double wv = Math.Sin(this.t * 3 + side) * 2 * k;
                c.BeginPath();
                c.MoveTo(sx, y - 45 * k);
                c.QuadraticCurveTo(sx - side * 7 * k, y - 46 * k + wv, sx - side * 13 * k, y - 43 * k - wv * 0.5);
                c.LineTo(sx - side * 12 * k, y - 35 * k - wv * 0.3);
                c.QuadraticCurveTo(sx - side * 6 * k, y - 37 * k + wv, sx, y - 36 * k);
                c.ClosePath();
                c.Fill();
                c.BeginPath();
                c.Arc(sx, y - 48 * k, 2 * k, 0, Math.PI * 2);
                c.Fill();
                // a horseman riding up and down the flank behind his line
                double ride = Math.Sin(this.t * 0.35 + side * 1.3 + seed * 4);
                double hx = front + side * (98 + ride * 18) * k;
                this.rider(c, hx, y, k * 0.95, ride >= 0 ? -side : side, Math.Cos(this.t * 0.35 + side * 1.3 + seed * 4) * side);
            }
            // volleys of arrows arcing over, both ways
            c.LineWidth = 0.7 * k;
            for (int i = 0; i < 10; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                double u = ((this.t * 0.45 + this.hash(i * 2.9 + seed)) % 1 + 1) % 1;
                double x0 = front + side * (60 + this.hash(i * 4.3) * 20) * k;
                double x1 = front - side * (20 + this.hash(i * 6.1) * 50) * k;
                double ax = x0 + (x1 - x0) * u, ay = y - 22 * k - Math.Sin(u * Math.PI) * 46 * k;
                double dx = (x1 - x0) / Math.Max(1, Math.Abs(x1 - x0)), dy = -Math.Cos(u * Math.PI) * 1.6;
                double n = Math.Sqrt(dx * dx + dy * dy);
                c.BeginPath();
                c.MoveTo(ax, ay);
                c.LineTo(ax - dx / n * 5 * k, ay - dy / n * 5 * k);
                c.Stroke();
            }
        }

        /// <summary>
        /// One hoplite, facing `face` (+1 right): greaved legs in stride, a cuirassed body
        /// behind a great round shield, a crested helmet, his spear levelled and thrusting
        /// in the front rank, held upright behind it.
        /// </summary>
        private void hoplite(Ctx c, double x, double y, double k, int face, int rank, double ph)
        {
            double step = Math.Sin(ph) * (rank == 0 ? 1.6 : 0.8);
            double thrust = rank == 0 ? Math.Max(0, Math.Sin(ph * 1.3)) * 4 : 0;
            double X(double u) { return x + face * u * k; }
            double Y(double v) { return y + v * k; }
            // legs
            seg(c, X(-0.5), Y(-10), X(-2 - step), Y(0), 2.4 * k, 1.6 * k);
            seg(c, X(0.5), Y(-10), X(2.5 + step), Y(0), 2.4 * k, 1.6 * k);
            // the body, leaning in
            c.BeginPath();
            c.MoveTo(X(-2.2), Y(-9.5));
            c.LineTo(X(-1.6), Y(-17));
            c.LineTo(X(2.4), Y(-17.6));
            c.LineTo(X(2.4), Y(-9.5));
            c.ClosePath();
            c.Fill();
            // a short cloak behind
            c.BeginPath();
            c.MoveTo(X(-1.5), Y(-17));
            c.QuadraticCurveTo(X(-5), Y(-12 + Math.Sin(ph) * 0.6), X(-4), Y(-9));
            c.LineTo(X(-1.8), Y(-11));
            c.ClosePath();
            c.Fill();
            // head and helmet, its crest running front to back
            c.BeginPath();
            c.Arc(X(1.4), Y(-19.6), 2.2 * k, 0, Math.PI * 2);
            c.Fill();
            c.BeginPath();
            c.MoveTo(X(3.2), Y(-21.2));
            c.QuadraticCurveTo(X(1), Y(-25.5), X(-3), Y(-22.5));
            c.QuadraticCurveTo(X(-1), Y(-23), X(0), Y(-21.6));
            c.ClosePath();
            c.Fill();
            // the spear
            c.LineWidth = 0.7 * k;
            c.BeginPath();
            if (rank == 0)
            {
                c.MoveTo(X(-8 + thrust), Y(-15.5));
                c.LineTo(X(14 + thrust), Y(-14.5));
            }
            else
            {
                double lean = 0.18 * face + Math.Sin(ph * 0.5) * 0.04;
                c.MoveTo(X(3), Y(-6));
                c.LineTo(x + (3 * face + lean * 28 * face) * k, Y(-32));
            }
            c.Stroke();
            // the shield, round, held out in front
            double shx = X(3.6), shy = Y(-13.5);
            c.BeginPath();
            c.Ellipse(shx, shy, 2.2 * k, 4.6 * k, 0, 0, Math.PI * 2);
            c.Fill();
            if (!this.rimPass && rank == 0)
            {
                c.Save();
                c.StrokeStyle = "rgba(255, 222, 170, 0.22)";
                c.LineWidth = 0.5 * k;
                c.BeginPath();
                c.Ellipse(shx, shy, 1.7 * k, 3.8 * k, 0, -1.2, 1.2);
                c.Stroke();
                c.Restore();
            }
        }

        /// <summary>a rider at the canter, facing `face`, his spear raised (`gait` -1..1 sets the stride)</summary>
        private void rider(Ctx c, double x, double y, double k, int face, double gait)
        {
            double g = this.t * 7;
            double bob = Math.Abs(Math.Sin(g)) * 1.4;
            c.Save();
            c.Translate(x, y - bob * k);
            c.Scale(face * k, k);
            // legs at the canter
            for (int i = 0; i < 4; i++)
            {
                double hx = i < 2 ? 7 : -7;
                double s = Math.Sin(g + i * 1.6);
                limb3(c, hx, -12, hx + s * 3.5, -6.5, hx + s * 5, 0, i < 2 ? 3.5 : 4.2, 1.8, 1.4);
            }
            // the body and neck, the head
            c.BeginPath();
            c.Ellipse(0, -14.5, 10, 4.6, 0, 0, Math.PI * 2);
            c.Fill();
            seg(c, 8, -16, 12.5, -23, 4.4, 3);
            c.BeginPath();
            c.MoveTo(11, -25);
            c.LineTo(17, -21.5);
            c.LineTo(15.5, -19.5);
            c.LineTo(10.5, -21.5);
            c.ClosePath();
            c.Fill();
            // the tail
            c.LineWidth = 1.6;
            c.BeginPath();
            c.MoveTo(-9.5, -16);
            c.QuadraticCurveTo(-14, -15 + Math.Sin(g) * 1.5, -15, -9);
            c.Stroke();
            // the rider, his cloak flying, spear up
            seg(c, 0, -18, 1.5, -26, 4, 3.4);
            c.BeginPath();
            c.Arc(2, -28.8, 2, 0, Math.PI * 2);
            c.Fill();
            c.BeginPath();
            c.MoveTo(2.5, -30.5);
            c.QuadraticCurveTo(0, -34, -3, -31);
            c.LineTo(0.5, -30);
            c.ClosePath();
            c.Fill();
            c.BeginPath();
            c.MoveTo(0, -25);
            c.QuadraticCurveTo(-6, -24 + Math.Sin(g * 0.8) * 1.2, -9, -19);
            c.LineTo(-1, -21);
            c.ClosePath();
            c.Fill();
            c.LineWidth = 0.8;
            c.BeginPath();
            c.MoveTo(-2, -20);
            c.LineTo(9, -40);
            c.Stroke();
            c.Restore();
        }
    }
}
