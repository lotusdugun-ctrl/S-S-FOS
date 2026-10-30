using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// The world going on without him, on the nearer ridges: hamlets with their smoke and,
    /// after dark, their lit doors; a shepherd and his flock drifting as they graze, the dog
    /// circling; a traveller leading a laden donkey along the crest; women carrying water
    /// jars home; a ploughman behind his oxen. Small, in colour, lit from the sun's side and
    /// veiled by the air — never more than a glance away from the climb, so the hill is not
    /// the only place in the world.
    /// </summary>
    public partial class SisyphusEngine
    {
        private void drawRidgeLife(Ctx ctx, Func<double, double> ridgeY, double p, int ridge)
        {
            if (this.lowQuality || this.dbgNoLife) return;
            double s = this.scale;
            double k = (ridge == 1 ? 0.8 : 1.0) * s;
            double spacing = (ridge == 1 ? 0.55 : 0.47) * this.w / s;
            double phase = (ridge == 1 ? 0.31 : 0.07) * this.w / s;
            double rel = this.camX * p - phase;
            int n0 = (int)Math.Floor((rel - 160 / s) / spacing);
            int n1 = (int)Math.Ceiling((rel + this.w / s + 160 / s) / spacing);
            var hz = this.st.ramp[this.st.ramp.Length - 1];
            this.farHaze = new RGB(hz.r, hz.g, hz.b);
            this.farAir = ridge == 1 ? 0.26 : 0.16;
            this.farDark = ridge == 1 ? 0.6 : 0.7;
            this.farLit = true;
            double night = Math.Max(0, Math.Min(1, (3 - this.st.elev) / 7));
            // which side the light comes from, for a lit edge on each figure
            double sunX = this.st.sunX * this.w;
            var lc = this.st.light.color;
            double sunUp = Math.Max(0, Math.Min(1, (this.st.elev + 2) / 10)) * (1 - this.st.cover * 0.5);
            ctx.Save();
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;
            for (int n = n0; n <= n1; n++)
            {
                double hh = this.hash(n * 5.71 + ridge * 41 + this.levelIndex * 2.3);
                if (hh > 0.8) continue;
                int kind = (int)Math.Floor(this.hash(n * 2.93 + ridge * 13 + this.levelIndex * 7.1) * 5);
                double sx = (n * spacing - rel) * s;
                if (sx < -140 * s || sx > this.w + 140 * s) continue;
                double lit = sunX > sx ? 1 : -1;
                this.lifeRim = Col.Rgba(Js.ToInt32(Math.Min(255, lc.r * 0.5 + 130)), Js.ToInt32(Math.Min(255, lc.g * 0.5 + 110)), Js.ToInt32(Math.Min(255, lc.b * 0.5 + 90)), Css.fixedNum(0.55 * sunUp, 3));
                this.lifeLit = lit;
                switch (kind)
                {
                    case 0: this.lifeHamlet(ctx, ridgeY, sx, k, hh, night); break;
                    case 1: this.lifeFlock(ctx, ridgeY, sx, k, hh); break;
                    case 2: this.lifeTraveller(ctx, ridgeY, sx, k, hh, spacing * s); break;
                    case 3: this.lifeWaterCarriers(ctx, ridgeY, sx, k, hh, spacing * s); break;
                    default: this.lifePlough(ctx, ridgeY, sx, k, hh); break;
                }
            }
            ctx.Restore();
            this.farLit = false;
        }

        /// <summary>the lit edge colour and the side (+1 right) the light comes from, for the current figure</summary>
        private Col lifeRim;
        private double lifeLit = 1;

        /// <summary>a small person (Engine.Anatomy): `walk` the stride phase (NaN: standing), coloured clothes</summary>
        private void lifePerson(Ctx c, double x, double y, double k, double face, double walk, double r, double g, double b, bool woman)
        {
            bool standing = double.IsNaN(walk);
            var p = standing ? new Pose { armN = 0.05, armF = -0.05, elbowN = 0.2, elbowF = 0.2 } : Gait(walk, 0, 0);
            var lk = Skin(168, 116, 82);
            lk.dress = woman ? 2 : 1;
            lk.clothR = r; lk.clothG = g; lk.clothB = b;
            lk.woman = woman;
            lk.beard = !woman && ((int)(x * 7) % 3 == 0);
            this.person(c, x, y, 15.5 * k, face, p, lk);
        }

        private void lifeHamlet(Ctx c, Func<double, double> ridgeY, double x, double k, double seed, double night)
        {
            int houses = 2 + (int)(seed * 10) % 3;
            for (int i = 0; i < houses; i++)
            {
                double hx = x + (i * 15 - houses * 7) * k + (this.hash(i + seed * 9) - 0.5) * 4 * k;
                double gy = ridgeY(hx) + 1.5 * k;
                double hw = (9 + this.hash(i * 3.1 + seed) * 5) * k, hhgt = (7 + this.hash(i * 1.7 + seed) * 4) * k;
                // whitewashed walls, the shaded side, a flat roof with its parapet
                this.mat(c, 222, 210, 188);
                c.BeginPath();
                c.Rect(hx - hw / 2, gy - hhgt, hw, hhgt);
                c.Fill();
                c.FillStyle = this.farTone(120, 110, 120, 0.55);
                c.BeginPath();
                if (this.lifeLit > 0) c.Rect(hx - hw / 2, gy - hhgt, hw * 0.3, hhgt);
                else c.Rect(hx + hw * 0.2, gy - hhgt, hw * 0.3, hhgt);
                c.Fill();
                this.mat(c, 160, 120, 86);
                c.BeginPath();
                c.Rect(hx - hw / 2 - 0.8 * k, gy - hhgt - 1.4 * k, hw + 1.6 * k, 1.6 * k);
                c.Fill();
                // the door: dark by day, a warm lit doorway by night
                double dw = 2.2 * k, dh = 4 * k, dx = hx + (this.hash(i * 7.7 + seed) - 0.5) * hw * 0.4;
                if (night > 0.15)
                {
                    double fl = 0.8 + 0.2 * Math.Sin(this.t * 5 + i * 3 + seed * 11);
                    c.Save();
                    c.GlobalCompositeOperation = Composite.Lighter;
                    var g = c.CreateRadialGradient(dx, gy - dh / 2, 0, dx, gy - dh / 2, 9 * k);
                    g.AddColorStop(0, Col.Rgba(255, 170, 80, Css.fixedNum(0.4 * night * fl, 3)));
                    g.AddColorStop(1, "rgba(255, 150, 60, 0)");
                    c.FillStyle = g;
                    c.FillRect(dx - 9 * k, gy - dh / 2 - 9 * k, 18 * k, 18 * k);
                    c.Restore();
                    c.FillStyle = Col.Rgba(255, 196, 110, Css.fixedNum(0.9 * night * fl, 3));
                }
                else c.FillStyle = this.farTone(40, 30, 30, 0.9);
                c.BeginPath();
                c.Rect(dx - dw / 2, gy - dh, dw, dh);
                c.Fill();
                // smoke from one roof, leaning with the wind
                if (i == 0)
                {
                    double wind = 4 + this.windiness() * 5;
                    c.Save();
                    for (int j = 0; j < 6; j++)
                    {
                        double u = ((this.t * 0.09 + j / 6.0 + seed) % 1 + 1) % 1;
                        double px = hx + u * u * wind * 4 * k + Math.Sin(this.t * 0.6 + j) * 1.5 * k;
                        double py = gy - hhgt - 2 * k - u * 46 * k;
                        c.FillStyle = this.farTone(200, 196, 196, 0.22 * (1 - u) * Math.Min(1, u * 6));
                        c.BeginPath();
                        c.Arc(px, py, (2 + u * 7) * k, 0, Math.PI * 2);
                        c.Fill();
                    }
                    c.Restore();
                }
            }
            // a goat tethered by the wall, an olive tree beside
            double tx = x + houses * 9 * k;
            double ty = ridgeY(tx) + 1 * k;
            this.mat(c, 100, 84, 60);
            seg(c, tx, ty, tx + 1 * k, ty - 8 * k, 2 * k, 1.4 * k);
            this.mat(c, 96, 112, 70);
            c.BeginPath();
            c.Ellipse(tx + 1 * k, ty - 11 * k, 7 * k, 4.5 * k, 0, 0, Math.PI * 2);
            c.Ellipse(tx - 3 * k, ty - 9 * k, 4 * k, 3 * k, 0, 0, Math.PI * 2);
            c.Fill();
            c.FillStyle = this.farTone(160, 176, 120, 0.6);
            c.BeginPath();
            c.Ellipse(tx + (1 + this.lifeLit * 2) * k, ty - 13 * k, 3.6 * k, 2 * k, 0, 0, Math.PI * 2);
            c.Fill();
        }

        /// <summary>a sheep or goat: the fleece (or the goat's lean coat), thin legs, the head down to graze or up</summary>
        private void lifeSheep(Ctx c, double x, double y, double k, double face, double graze, bool goat, bool dark)
        {
            double r = dark ? 58 : goat ? 150 : 226, g = dark ? 50 : goat ? 116 : 216, b = dark ? 48 : goat ? 86 : 198;
            // legs: thin, the far pair darker
            this.mat(c, goat ? r * 0.7 : 70, goat ? g * 0.7 : 58, goat ? b * 0.7 : 52);
            c.LineWidth = 0.75 * k;
            c.BeginPath();
            c.MoveTo(x - 2.2 * k, y - 2.6 * k); c.LineTo(x - 2.5 * k, y);
            c.MoveTo(x - 1.2 * k, y - 2.6 * k); c.LineTo(x - 1 * k, y);
            c.MoveTo(x + 1.8 * k * face, y - 2.6 * k); c.LineTo(x + 2.1 * k * face, y);
            c.MoveTo(x + 2.8 * k * face, y - 2.6 * k); c.LineTo(x + 2.7 * k * face, y);
            c.Stroke();
            this.mat(c, r, g, b);
            c.BeginPath();
            if (goat)
            {
                c.Ellipse(x, y - 3.7 * k, 3.6 * k, 1.8 * k, 0, 0, Math.PI * 2);
            }
            else
            {
                // the fleece: lumps of wool round a barrel body
                for (int i = 0; i < 5; i++)
                {
                    double ox = (i - 2) * 1.4 * k, oy = (i % 2 == 0 ? -0.3 : 0.3) * k;
                    c.Arc(x + ox, y - 4 * k + oy, 1.6 * k, 0, Math.PI * 2);
                }
            }
            c.Fill();
            // the neck and head, dark-faced on the sheep
            double hx = x + 3.8 * k * face, hy = y - 4.4 * k + graze * 3.2 * k;
            if (!goat) this.mat(c, 60, 52, 48);
            seg(c, x + 2.4 * k * face, y - 4.4 * k, hx, hy, 1.6 * k, 1.1 * k);
            c.BeginPath();
            c.Ellipse(hx + 0.6 * k * face, hy + 0.2 * k, 1.4 * k, 0.9 * k, face * (0.4 + graze * 0.7), 0, Math.PI * 2);
            c.Fill();
            if (goat)
            {
                c.StrokeStyle = this.farTone(70, 60, 50, 0.9);
                c.LineWidth = 0.5 * k;
                c.BeginPath();
                c.MoveTo(hx - 0.2 * k * face, hy - 0.7 * k);
                c.QuadraticCurveTo(hx - 1.4 * k * face, hy - 2.6 * k, hx - 2.4 * k * face, hy - 1.8 * k);
                // the beard
                c.MoveTo(hx + 1 * k * face, hy + 0.8 * k);
                c.LineTo(hx + 0.8 * k * face, hy + 1.8 * k);
                c.Stroke();
            }
            c.FillStyle = this.lifeRim;
            c.BeginPath();
            c.Ellipse(x + this.lifeLit * 1.4 * k, y - 5 * k, 1.8 * k, 0.7 * k, 0, 0, Math.PI * 2);
            c.Fill();
        }

        /// <summary>an ox under the yoke: the heavy forehand, the hump, the dewlap, the head low, horns</summary>
        private void lifeOx(Ctx c, double x, double y, double k, double face, double walk, double r, double g, double b)
        {
            double u = k / 6;
            c.Save();
            c.Translate(x, y);
            c.Scale(face * u, u);
            for (int i = 0; i < 4; i++)
            {
                bool fore = i < 2, far = i % 2 == 1;
                var (hx, hy) = hoofPath(walk, i, 0, 14, 4, 0);
                double sx = fore ? 18 : -18, sy = -34;
                hx += sx;
                double jx, jy, ex, ey;
                ik2(sx, sy, hx, hy - 4, 18, 14, fore ? 1 : -1, out jx, out jy, out ex, out ey);
                double kk = far ? 0.75 : 1;
                this.mat(c, r * kk, g * kk, b * kk);
                limbB(c, sx, sy, jx, jy, 10, 6, 1.4, 1.8);
                limbB(c, jx, jy, ex, ey, 5.4, 4.4, 0.2, 0.2);
                this.mat(c, 40, 32, 28);
                c.BeginPath();
                c.Rect(hx - 2.4, hy - 3, 5, 3);
                c.Fill();
            }
            this.mat(c, r, g, b);
            c.BeginPath();
            c.MoveTo(26, -46);
            c.QuadraticCurveTo(30, -34, 24, -24);      // the dewlap
            c.QuadraticCurveTo(0, -20, -22, -24);      // the belly
            c.QuadraticCurveTo(-30, -30, -28, -44);    // the quarters
            c.QuadraticCurveTo(-20, -52, 0, -50);      // the back
            c.QuadraticCurveTo(12, -58, 22, -54);      // the hump
            c.QuadraticCurveTo(26, -52, 26, -46);
            c.ClosePath();
            c.Fill();
            // the head, low and forward, the horns curving up and out
            c.BeginPath();
            c.MoveTo(24, -50);
            c.QuadraticCurveTo(34, -48, 38, -38);
            c.LineTo(36, -33);
            c.QuadraticCurveTo(30, -34, 24, -38);
            c.ClosePath();
            c.Fill();
            this.mat(c, 220, 206, 176);
            c.LineWidth = 1.8;
            c.BeginPath();
            c.MoveTo(30, -48);
            c.QuadraticCurveTo(28, -56, 34, -58);
            c.Stroke();
            c.FillStyle = this.lifeRim;
            c.BeginPath();
            c.Ellipse(-2 + this.lifeLit * 4, -49, 16, 2.6, 0, 0, Math.PI * 2);
            c.Fill();
            c.Restore();
        }

        private void lifeFlock(Ctx c, Func<double, double> ridgeY, double x, double k, double seed)
        {
            // the flock drifts slowly one way and back as it grazes; each animal on its own beat
            double drift = Math.Sin(this.t * 0.05 + seed * 6) * 26 * k;
            bool goats = seed < 0.4;
            for (int i = 0; i < 8; i++)
            {
                double ox = (this.hash(i * 2.3 + seed) - 0.5) * 56 * k;
                double wander = Math.Sin(this.t * (0.13 + i * 0.02) + i * 1.9) * 4 * k;
                double ax = x + drift + ox + wander;
                double face = Math.Cos(this.t * (0.13 + i * 0.02) + i * 1.9) >= 0 ? 1 : -1;
                double graze = Math.Max(0, Math.Sin(this.t * (0.5 + i * 0.07) + i * 2.1));
                this.lifeSheep(c, ax, ridgeY(ax) + 1 * k, k, face, graze, goats, i == 3);
            }
            // the shepherd with his crook, leaning on it, now and then walking after them
            double shx = x + drift - 34 * k;
            double shy = ridgeY(shx) + 1 * k;
            this.lifePerson(c, shx, shy, k, 1, double.NaN, 120, 92, 64, false);
            this.mat(c, 96, 70, 46);
            c.LineWidth = 0.7 * k;
            c.BeginPath();
            c.MoveTo(shx + 3 * k, shy);
            c.LineTo(shx + 3.6 * k, shy - 17 * k);
            c.QuadraticCurveTo(shx + 5.6 * k, shy - 19 * k, shx + 5 * k, shy - 16 * k);
            c.Stroke();
            // the dog circling the flock
            double da = this.t * 0.7 + seed * 5;
            double dx = x + drift + Math.Cos(da) * 34 * k;
            double dface = -Math.Sin(da) >= 0 ? 1 : -1;
            double dy = ridgeY(dx) + 1 * k;
            double run = Math.Sin(this.t * 12) * 1.2;
            this.mat(c, 70, 56, 46);
            c.LineWidth = 0.8 * k;
            c.BeginPath();
            c.MoveTo(dx - 2 * k * dface, dy - 2 * k); c.LineTo(dx + (-2.6 - run) * k * dface, dy);
            c.MoveTo(dx + 2 * k * dface, dy - 2 * k); c.LineTo(dx + (2.6 + run) * k * dface, dy);
            c.Stroke();
            c.BeginPath();
            c.Ellipse(dx, dy - 3 * k, 3 * k, 1.4 * k, 0, 0, Math.PI * 2);
            c.Ellipse(dx + 3.2 * k * dface, dy - 4.4 * k, 1.3 * k, 1 * k, 0, 0, Math.PI * 2);
            c.Fill();
            c.BeginPath();
            c.MoveTo(dx - 3 * k * dface, dy - 3 * k);
            c.LineTo(dx - 5 * k * dface, dy - 4.6 * k + run * 0.5 * k);
            c.Stroke();
        }

        private void lifeTraveller(Ctx c, Func<double, double> ridgeY, double x, double k, double seed, double reach)
        {
            // along the crest at a walk, the donkey behind him with its load
            double speed = 9 * k;
            double u = (this.t * speed + seed * 900) % (reach * 1.6);
            double face = seed < 0.5 ? 1 : -1;
            double px = x + face * (u - reach * 0.8);
            double gy = ridgeY(px) + 1 * k;
            double walk = this.t * 4.2 + seed * 3;
            this.lifePerson(c, px, gy, k, face, walk, 150, 70, 52, false);
            // his staff
            this.mat(c, 100, 76, 50);
            c.LineWidth = 0.6 * k;
            c.BeginPath();
            c.MoveTo(px + 2.4 * k * face + Math.Sin(walk) * 0.8 * k, gy);
            c.LineTo(px + 2.8 * k * face, gy - 15 * k);
            c.Stroke();
            // the donkey behind him with its load: a small grey horse at the walk, panniers on its back
            double dx = px - face * 15 * k;
            double dy = ridgeY(dx) + 1 * k;
            var hl = new HorseLook { r = 120, g = 112, b = 106, maneR = 60, maneG = 54, maneB = 50, head = true };
            var (wx, wy) = this.horse(c, dx, dy, 13 * k, face, 0, walk, hl);
            this.mat(c, 176, 140, 96);
            c.BeginPath();
            c.Ellipse(wx - face * 4 * k, wy + 2.4 * k, 3.4 * k, 3 * k, 0, 0, Math.PI * 2);
            c.Fill();
            this.mat(c, 150, 70, 52);
            c.BeginPath();
            c.Rect(wx - face * 4 * k - 3 * k, wy - 0.6 * k, 6 * k, 1.6 * k);
            c.Fill();
        }

        private void lifeWaterCarriers(Ctx c, Func<double, double> ridgeY, double x, double k, double seed, double reach)
        {
            // two or three women walking home, the jars on their heads, one steadying it
            double speed = 6 * k;
            double face = seed < 0.5 ? -1 : 1;
            double u = (this.t * speed + seed * 700) % (reach * 1.4);
            for (int i = 0; i < 3; i++)
            {
                double px = x + face * (u - reach * 0.7) - face * i * 9 * k;
                double gy = ridgeY(px) + 1 * k;
                double walk = this.t * 3.4 + i * 1.3;
                double bob = Math.Abs(Math.Sin(walk)) * 0.4 * k;
                double r = i == 0 ? 170 : i == 1 ? 90 : 196, g = i == 0 ? 120 : i == 1 ? 110 : 186, b = i == 0 ? 80 : i == 1 ? 140 : 160;
                this.lifePerson(c, px, gy - bob, k, face, walk, r, g, b, true);
                // the jar: belly, neck, a lit side
                double jx = px, jy = gy - 17 * k - bob;
                this.mat(c, 176, 96, 60);
                c.BeginPath();
                c.Ellipse(jx, jy, 1.6 * k, 1.9 * k, 0, 0, Math.PI * 2);
                c.Fill();
                c.BeginPath();
                c.Rect(jx - 0.6 * k, jy - 3 * k, 1.2 * k, 1.4 * k);
                c.Fill();
                c.FillStyle = this.lifeRim;
                c.BeginPath();
                c.Ellipse(jx + this.lifeLit * 0.7 * k, jy - 0.3 * k, 0.6 * k, 1.2 * k, 0, 0, Math.PI * 2);
                c.Fill();
                if (i == 1)
                {
                    this.mat(c, 150, 104, 76);
                    seg(c, px + 1 * k * face, gy - 11 * k - bob, jx + 1.8 * k * face, jy + 0.8 * k, 1.1 * k, 0.9 * k);
                }
            }
        }

        private void lifePlough(Ctx c, Func<double, double> ridgeY, double x, double k, double seed)
        {
            // a ploughman behind two oxen, going along his furrow and turning at its end
            double len = 50 * k;
            double u = (this.t * 4 * k + seed * 400) % (len * 2);
            double face = u < len ? 1 : -1;
            double px = x - len / 2 + (u < len ? u : len * 2 - u);
            double walk = this.t * 2.6;
            // the furrows he has turned, darker earth
            c.StrokeStyle = this.farTone(70, 50, 40, 0.5);
            c.LineWidth = 0.9 * k;
            for (int j = 0; j < 3; j++)
            {
                c.BeginPath();
                for (double fx = x - len / 2; fx <= x + len / 2; fx += 4 * k)
                {
                    double fy = ridgeY(fx) + (2 + j * 2.2) * k;
                    if (fx == x - len / 2) c.MoveTo(fx, fy); else c.LineTo(fx, fy);
                }
                c.Stroke();
            }
            for (int o = 1; o >= 0; o--)
            {
                double ox = px + face * (11 + o * 2.4) * k;
                double oy = ridgeY(ox) + (1 + o * 0.6) * k;
                this.lifeOx(c, ox, oy, k, face, walk + o * 0.7, o == 0 ? 124 : 92, o == 0 ? 96 : 72, o == 0 ? 74 : 58);
            }
            // the yoke, the plough's beam and the man at its handle
            this.mat(c, 110, 84, 58);
            c.LineWidth = 0.8 * k;
            double gy = ridgeY(px) + 1 * k;
            c.BeginPath();
            c.MoveTo(px + face * 2 * k, gy - 4 * k);
            c.LineTo(px + face * 14 * k, gy - 8 * k);
            c.MoveTo(px + face * 1 * k, gy);
            c.LineTo(px + face * 3 * k, gy - 6 * k);
            c.Stroke();
            this.lifePerson(c, px - face * 3 * k, gy, k, face, walk, 196, 186, 160, false);
        }
    }
}
