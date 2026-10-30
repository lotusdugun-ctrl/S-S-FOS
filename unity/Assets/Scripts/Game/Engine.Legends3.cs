using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// The moving far figures rebuilt on Engine.Anatomy's bodies: the centaur as a horse at
    /// the gallop with a man's torso rising from its withers, drawing and loosing; the
    /// horsemen at the canter; the hoplites as men in helmet, corslet and cloak behind their
    /// shields, the front rank thrusting.
    /// </summary>
    public partial class SisyphusEngine
    {
        private void drawCentaur3(Ctx c, double x, double y, double k, double dir)
        {
            double g = this.t * 7.5;
            var hl = new HorseLook { r = 116, g = 74, b = 48, maneR = 44, maneG = 32, maneB = 26, head = false };
            var (wx, wy) = this.horse(c, x, y, 50 * k, dir, 2, g, hl);
            // the man: his waist where the horse's neck would be, leaning into the gallop
            double draw = this.centaurDraw();
            double hgt = 66 * k;
            var lk = Skin(184, 128, 92);
            lk.torsoOnly = true;
            lk.beard = true;
            lk.hairR = 40; lk.hairG = 28; lk.hairB = 22;
            var p = new Pose { lean = 0.12 + Math.Sin(g) * 0.03, head = -0.05 };
            double hipX = wx + dir * 4 * k, hipY = wy + 2 * k;
            // in his own frame (100 = his height): the shoulder is up the torso, the bow out ahead
            double shx = Math.Sin(p.lean) * 31, shy = -Math.Cos(p.lean) * 31;
            this.person(c, hipX, hipY, hgt, dir, p, lk, which =>
                which == 1 ? (shx + 30, shy + 1) : (shx + 30 - draw * 27, shy - 1 - draw * 2));
            // the bow in the far fist, the string drawn back to the near one
            var (bx, by) = personPoint(hipX, hipY, hgt, dir, shx + 31, shy + 1);
            var (nx, ny) = personPoint(hipX, hipY, hgt, dir, shx + 31 - draw * 27, shy - 1 - draw * 2);
            double u = hgt / 100;
            this.mat(c, 96, 62, 36);
            c.LineWidth = 1.6 * u;
            c.BeginPath();
            c.MoveTo(bx - dir * 2 * u, by - 20 * u);
            c.QuadraticCurveTo(bx + dir * 8 * u, by, bx - dir * 2 * u, by + 20 * u);
            c.Stroke();
            this.mat(c, 230, 220, 196);
            c.LineWidth = 0.6 * u;
            c.BeginPath();
            c.MoveTo(bx - dir * 2 * u, by - 20 * u);
            c.LineTo(nx, ny);
            c.LineTo(bx - dir * 2 * u, by + 20 * u);
            c.Stroke();
            if (draw > 0.15)
            {
                this.mat(c, 110, 80, 52);
                c.LineWidth = 0.9 * u;
                c.BeginPath();
                c.MoveTo(nx, ny);
                c.LineTo(bx + dir * 6 * u, by);
                c.Stroke();
            }
            // the quiver across his back
            var (qx, qy) = personPoint(hipX, hipY, hgt, dir, shx - 6, shy + 10);
            this.mat(c, 100, 56, 34);
            seg(c, qx, qy, qx - dir * 3 * u, qy - 15 * u, 4 * u, 4.6 * u);
            // the arrow loosed, flying on ahead
            double fly = this.centaurFly();
            if (fly >= 0 && fly < 1)
            {
                double ax = bx + dir * (6 + fly * 140) * k, ay = by - Math.Sin(fly * Math.PI) * 14 * k;
                this.mat(c, 70, 52, 40);
                c.LineWidth = 0.9 * k;
                c.BeginPath();
                c.MoveTo(ax, ay);
                c.LineTo(ax - dir * 8 * k, ay + 0.6 * k);
                c.Stroke();
            }
        }

        private void rider3(Ctx c, double x, double y, double k, int face, double gait)
        {
            double g = this.t * 6.2 + x * 0.01;
            var hl = new HorseLook { r = 92, g = 64, b = 46, maneR = 30, maneG = 24, maneB = 20, head = true };
            var (wx, wy) = this.horse(c, x, y, 34 * k, face, 1, g, hl);
            double hgt = 34 * k, u = hgt / 100;
            double hipX = wx - face * 7 * u, hipY = wy + 4 * u;
            // the near leg down the horse's side
            var lk = Skin(176, 122, 88);
            lk.helmet = true;
            lk.torsoOnly = true;
            c.Save();
            c.Translate(hipX, hipY);
            c.Scale(face * u, u);
            this.matSkin(c, lk, 1);
            limbB(c, 0, 0, 12, 14, 11, 7.6, 2, 1.2);
            limbB(c, 12, 14, 7, 32, 7.4, 4.2, 0.4, 2.2);
            c.Restore();
            var p = new Pose { lean = 0.08 + Math.Sin(g) * 0.04, armF = 0.5, elbowF = 0.9 };
            double shx = Math.Sin(p.lean) * 31, shy = -Math.Cos(p.lean) * 31;
            this.person(c, hipX, hipY, hgt, face, p, lk, which => which == 0 ? (shx + 10, shy - 12) : (shx + 18, shy + 18));
            // the spear held high, the cloak flying
            var (hx, hy) = personPoint(hipX, hipY, hgt, face, shx + 10, shy - 12);
            this.mat(c, 110, 86, 60);
            c.LineWidth = 1.1 * u;
            c.BeginPath();
            c.MoveTo(hx - face * 14 * u, hy + 30 * u);
            c.LineTo(hx + face * 16 * u, hy - 46 * u);
            c.Stroke();
            this.mat(c, 196, 160, 96);
            c.BeginPath();
            c.MoveTo(hx + face * 16 * u, hy - 46 * u);
            c.LineTo(hx + face * 14 * u, hy - 38 * u);
            c.LineTo(hx + face * 18.4 * u, hy - 39 * u);
            c.ClosePath();
            c.Fill();
        }

        private void hoplite3(Ctx c, double x, double y, double k, int face, int rank, double ph)
        {
            double hgt = 24 * k, u = hgt / 100;
            bool front = rank == 0;
            double thrust = front ? Math.Max(0, Math.Sin(ph * 1.3)) * 14 : 0;
            var p = Gait(ph * 0.5, 0, front ? 0.35 : 0.1);
            // pressing forward, not walking: the stride is short
            p.thighN *= 0.45; p.thighF *= 0.45; p.kneeN *= 0.6; p.kneeF *= 0.6;
            var lk = Skin(176, 120, 86);
            lk.helmet = true;
            lk.dress = 1;
            lk.clothR = 206; lk.clothG = 190; lk.clothB = 160;
            lk.cloak = true;
            double shx = Math.Sin(p.lean) * 31, shy = -Math.Cos(p.lean) * 31 - 52;
            // the spear hand: levelled and thrusting in front, upright behind
            (double, double) spearHand = front ? (shx + 16 + thrust * 0.6, shy + 6) : (shx + 10, shy + 20);
            this.person(c, x, y, hgt, face, p, lk, which => which == 0 ? spearHand : (shx + 14, shy + 18));
            // the spear
            var (hx, hy) = personPoint(x, y, hgt, face, spearHand.Item1, spearHand.Item2);
            this.mat(c, 120, 92, 64);
            c.LineWidth = 1.4 * u;
            c.BeginPath();
            if (front)
            {
                c.MoveTo(hx - face * 34 * u, hy + 3 * u);
                c.LineTo(hx + face * 62 * u, hy - 4 * u);
            }
            else
            {
                double lean = 0.2 + Math.Sin(ph * 0.5) * 0.05;
                c.MoveTo(hx - face * 4 * u, hy + 34 * u);
                c.LineTo(hx + face * lean * 120 * u, hy - 110 * u);
            }
            c.Stroke();
            // the shield: a great bronze round on the far arm, its rim catching the light,
            // the city's sign on its face
            var (sx, sy) = personPoint(x, y, hgt, face, shx + 17, shy + 20);
            this.mat(c, 186, 136, 66);
            c.BeginPath();
            c.Ellipse(sx, sy, 9 * u, 19 * u, 0, 0, Math.PI * 2);
            c.Fill();
            if (this.farLit)
            {
                c.FillStyle = this.farTone(face > 0 ? 140 : 40, face > 0 ? 40 : 46, face > 0 ? 32 : 96, 0.9);
                c.BeginPath();
                c.Ellipse(sx, sy, 3.6 * u, 8 * u, 0, 0, Math.PI * 2);
                c.Fill();
                c.StrokeStyle = this.farTone(255, 226, 160, 0.6);
                c.LineWidth = 1.2 * u;
                c.BeginPath();
                c.Ellipse(sx, sy, 8 * u, 18 * u, 0, -1.4, 0.6);
                c.Stroke();
            }
        }

        // ------------------------------------------------------------------ Heracles and the lion

        /// <summary>
        /// Heracles wrestling the Nemean lion, whose hide no blade could pierce: the beast
        /// reared up against him, its forepaws on his shoulders, he with his arms locked round
        /// its neck, both straining, swaying one way and the other; now and then the lion
        /// throws its head back to roar.
        /// </summary>
        private void drawHeracles(Ctx c, double x, double y, double k, double seed)
        {
            double sway = Math.Sin(this.t * 0.9 + seed * 5) * 0.08 + Math.Sin(this.t * 2.3) * 0.02;
            double roar = Math.Max(0, Math.Sin(this.t * 0.55 + seed * 3)) > 0.85 ? 1 : 0;
            double hgt = 96 * k, u = hgt / 100;
            // the lion: reared up on its hind legs from the right, against him (drawn in a
            // frame facing him: x toward him, up is -y)
            double mane = 1 + Math.Sin(this.t * 3) * 0.03;
            double sw = sway * 20;
            c.Save();
            c.Translate(x + 36 * k, y);
            c.Scale(-k, k);
            this.mat(c, 120, 88, 50);
            // the far hind leg, then the near one: haunch, hock, the paw flat
            limbB(c, 2, -24, 8, -12, 11, 7, 2, 2);
            limbB(c, 8, -12, 2, -1, 6.4, 4.4, 0.6, 1);
            this.mat(c, 150, 112, 64);
            // the body, rising from the haunches to the shoulders
            c.BeginPath();
            c.MoveTo(-6, -22);
            c.QuadraticCurveTo(-8, -44, 12 + sw * 0.3, -60);   // the back
            c.QuadraticCurveTo(24 + sw * 0.5, -66, 30 + sw * 0.5, -60); // over the shoulders
            c.QuadraticCurveTo(30, -46, 18, -36);               // the chest
            c.QuadraticCurveTo(12, -24, 10, -16);               // the belly, tucked
            c.QuadraticCurveTo(2, -10, -6, -22);
            c.ClosePath();
            c.Fill();
            limbB(c, 0, -22, 10, -12, 12, 7.4, 2.2, 2);
            limbB(c, 10, -12, 4, 0, 6.8, 4.6, 0.6, 1);
            c.BeginPath();
            c.Ellipse(7, -1, 4.4, 1.8, 0, 0, Math.PI * 2);
            c.Fill();
            // the tail, lashing, its tuft
            double tl = Math.Sin(this.t * 3.1) * 5;
            c.LineWidth = 2.2;
            c.BeginPath();
            c.MoveTo(-5, -20);
            c.BezierCurveTo(-16, -18 + tl, -22, -4 - tl, -30, -6 + tl);
            c.Stroke();
            this.mat(c, 70, 46, 30);
            c.BeginPath();
            c.Ellipse(-31, -6 + tl, 3, 2, 0.3, 0, Math.PI * 2);
            c.Fill();
            // the forelegs over his shoulders, claws in his back
            this.mat(c, 150, 112, 64);
            limbB(c, 24 + sw * 0.5, -58, 44 + sw, -72, 9, 5.6, 1.2, 1.2);
            limbB(c, 20 + sw * 0.5, -54, 42 + sw, -64, 8, 5.2, 1.2, 1.2);
            // the mane round head and neck, then the head
            double hx = 30 + sw * 0.6, hy = -68;
            this.mat(c, 96, 60, 30);
            c.BeginPath();
            for (int i = 0; i < 14; i++)
            {
                double a = i / 14.0 * Math.PI * 2;
                double r = (11 + (i % 2) * 3.4) * mane;
                if (i == 0) c.MoveTo(hx - 3 + Math.Cos(a) * r, hy + Math.Sin(a) * r * 1.15);
                else c.LineTo(hx - 3 + Math.Cos(a) * r, hy + Math.Sin(a) * r * 1.15);
            }
            c.ClosePath();
            c.Fill();
            this.mat(c, 162, 122, 72);
            c.Save();
            c.Translate(hx, hy);
            c.Rotate(-0.25 - roar * 0.55);
            c.BeginPath();
            c.MoveTo(-5, -6);
            c.QuadraticCurveTo(6, -8, 13, -3);
            c.LineTo(14, 1);
            c.LineTo(9, 2 + roar * 2);
            c.LineTo(4, 3);
            c.LineTo(9, 5 + roar * 6);
            c.QuadraticCurveTo(0, 8, -6, 4);
            c.ClosePath();
            c.Fill();
            c.BeginPath();
            c.Arc(-2, -7, 2.4, 0, Math.PI * 2);   // the ear
            c.Fill();
            if (this.farLit)
            {
                c.FillStyle = this.farTone(20, 12, 10, 0.9);
                c.BeginPath();
                c.Arc(5, -3.6, 0.9, 0, Math.PI * 2);
                c.Fill();
            }
            c.Restore();
            c.Restore();
            // Heracles: braced, leaning into it, arms locked round its neck
            var lk = Skin(186, 126, 88);
            lk.beard = true;
            lk.hairR = 50; lk.hairG = 36; lk.hairB = 28;
            var p = new Pose { lean = 0.42 + sway, thighN = 0.5, kneeN = 0.55, thighF = -0.45, kneeF = 0.25, head = 0.15 };
            double shx = Math.Sin(p.lean) * 31, shy = -Math.Cos(p.lean) * 31 - 48;
            this.person(c, x - 8 * k, y, hgt, 1, p, lk, which => which == 0 ? (shx + 4, shy + 12) : (shx + 2, shy + 6));
            // the club dropped at his feet
            this.mat(c, 110, 80, 52);
            seg(c, x - 34 * k, y - 1 * k, x - 14 * k, y - 4 * k, 2.2 * k, 4 * k);
        }

        // ------------------------------------------------------------------ the wooden horse

        /// <summary>
        /// The horse of Troy: a great horse of planks on a wheeled platform, the trapdoor in its
        /// flank, and men hauling it along by ropes, leaning their weight into them.
        /// </summary>
        private void drawTrojanHorse(Ctx c, double x, double y, double k, double seed)
        {
            double creep = Math.Sin(this.t * 0.2 + seed * 4) * 3 * k;
            double hx = x + creep;
            // the platform and its wheels
            this.mat(c, 110, 78, 50);
            c.BeginPath();
            c.Rect(hx - 44 * k, y - 10 * k, 88 * k, 5 * k);
            c.Fill();
            for (int i = 0; i < 4; i++)
            {
                double wx = hx + (-34 + i * 22.6) * k;
                this.mat(c, 90, 62, 40);
                c.BeginPath();
                c.Arc(wx, y - 5 * k, 5 * k, 0, Math.PI * 2);
                c.Fill();
                if (this.farLit)
                {
                    c.StrokeStyle = this.farTone(40, 28, 20, 0.7);
                    c.LineWidth = 0.7 * k;
                    double a = -creep / (5 * k);
                    c.BeginPath();
                    for (int j = 0; j < 4; j++)
                    {
                        c.MoveTo(wx, y - 5 * k);
                        c.LineTo(wx + Math.Cos(a + j * Math.PI / 2) * 4.6 * k, y - 5 * k + Math.Sin(a + j * Math.PI / 2) * 4.6 * k);
                    }
                    c.Stroke();
                }
            }
            // the horse, standing square, in planks
            var hl = new HorseLook { r = 170, g = 126, b = 78, maneR = 120, maneG = 84, maneB = 50, head = true };
            this.horse(c, hx, y - 10 * k, 84 * k, 1, 0, 1.3, hl);
            if (this.farLit)
            {
                // the planking and the trapdoor
                c.StrokeStyle = this.farTone(70, 48, 30, 0.6);
                c.LineWidth = 0.8 * k;
                c.BeginPath();
                double u = 84 * k / 60;
                for (int i = 0; i < 6; i++)
                {
                    double py = y - 10 * k - (30 + i * 4.6) * u;
                    c.MoveTo(hx - 28 * u, py);
                    c.LineTo(hx + 20 * u, py + 1 * u);
                }
                c.Stroke();
                c.StrokeStyle = this.farTone(40, 26, 18, 0.9);
                c.LineWidth = 1.1 * k;
                c.BeginPath();
                c.Rect(hx - 8 * u, y - 10 * k - 46 * u, 10 * u, 9 * u);
                c.Stroke();
            }
            // the men on the ropes, hauling
            double rx0 = hx + 44 * k, ry0 = y - 12 * k;
            for (int i = 0; i < 3; i++)
            {
                double mx = rx0 + (14 + i * 11) * k;
                var p = Gait(this.t * 2.2 + i * 1.7, 0, 0.9);
                p.thighN *= 0.6; p.thighF *= 0.6;
                var lk = Skin(172, 118, 84);
                lk.dress = 1;
                lk.clothR = i == 1 ? 150 : 196; lk.clothG = i == 1 ? 70 : 182; lk.clothB = i == 1 ? 50 : 150;
                double shx = Math.Sin(p.lean) * 31, shy = -Math.Cos(p.lean) * 31 - 50;
                this.person(c, mx, y, 22 * k, 1, p, lk, which => (shx - 6, shy + 16 + which * 2));
            }
            this.mat(c, 150, 120, 80);
            c.LineWidth = 0.8 * k;
            c.BeginPath();
            c.MoveTo(rx0, ry0);
            c.LineTo(rx0 + 40 * k, y - 15 * k);
            c.Stroke();
        }

        // ------------------------------------------------------------------ Kerberos

        /// <summary>
        /// The hound of the underworld, built as a hound: the deep chest and the tucked waist
        /// of a mastiff, shoulders and haunches in muscle, legs that pace with the paws flat;
        /// three necks from the one chest, each head with its own mind, one snapping; a mane
        /// of spines; a collar of bronze spikes; the serpent for a tail.
        /// </summary>
        private void drawCerberus3(Ctx c, double x, double y, double k, double dir)
        {
            double g = this.t * 3.6;
            double u = k * 0.62;
            c.Save();
            c.Translate(x, y);
            c.Scale(dir * u, u);
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            double bob = Math.Abs(Math.Sin(g)) * 0.8;
            c.Translate(0, -bob);
            double cr = 62, cg = 50, cb = 48;
            void leg(int i, bool far)
            {
                bool fore = i < 2;
                var (hx, hy) = hoofPath(g, i, 0, 16, 5, 0);
                double sx = fore ? 18 : -20, sy = fore ? -30 : -32;
                hx += sx; hy += bob;
                double jx, jy, ex, ey;
                ik2(sx, sy, hx + (fore ? 0 : -2), hy - 7, fore ? 16 : 17, fore ? 12 : 13, fore ? 1 : -1, out jx, out jy, out ex, out ey);
                double kk = far ? 0.75 : 1;
                this.mat(c, cr * kk, cg * kk, cb * kk);
                limbB(c, sx, sy, jx, jy, fore ? 10 : 12, 6, 1.6, fore ? 1.2 : 2.6, 0.3);
                limbB(c, jx, jy, ex, ey, 5.6, 4.4, 0.3, 0.3);
                limbB(c, ex, ey, hx + 2.4, hy - 1.6, 4.4, 3.6, 0.2, 0.2);
                c.BeginPath();
                c.Ellipse(hx + 3, hy - 1.4, 3.8, 1.8, 0, 0, Math.PI * 2);
                c.Fill();
            }
            leg(1, true);
            leg(3, true);
            // the serpent tail, lashing, its head raised
            double lash = Math.Sin(g * 0.8) * 6;
            this.mat(c, 70, 84, 56);
            c.LineWidth = 3.4;
            c.BeginPath();
            c.MoveTo(-30, -38);
            c.BezierCurveTo(-44, -38 + lash, -40, -58 - lash, -54, -56);
            c.Stroke();
            c.BeginPath();
            c.Ellipse(-56, -57, 4, 2.4, 0.3, 0, Math.PI * 2);
            c.Fill();
            // the body: deep chest, tucked waist, haunch
            this.mat(c, cr, cg, cb);
            c.BeginPath();
            c.MoveTo(26, -44);
            c.QuadraticCurveTo(30, -30, 22, -22);      // the deep chest
            c.QuadraticCurveTo(10, -18, 0, -24);       // the brisket up to the waist
            c.QuadraticCurveTo(-10, -28, -18, -26);    // the tucked waist
            c.QuadraticCurveTo(-30, -26, -32, -36);    // the haunch
            c.QuadraticCurveTo(-30, -46, -18, -44);    // the croup
            c.QuadraticCurveTo(0, -42, 12, -50);       // the back up to the shoulders
            c.QuadraticCurveTo(22, -52, 26, -44);
            c.ClosePath();
            c.Fill();
            if (this.farLit)
            {
                // the ribs under the hide
                c.StrokeStyle = this.farTone(20, 14, 14, 0.55);
                c.LineWidth = 0.9;
                c.BeginPath();
                for (int i = 0; i < 4; i++) { double rx = 10 - i * 4; c.MoveTo(rx, -44); c.QuadraticCurveTo(rx - 2, -36, rx, -26 + i * 0.4); }
                c.Stroke();
            }
            // the mane of spines along the back
            this.mat(c, 40, 30, 30);
            c.BeginPath();
            for (int i = 0; i < 8; i++)
            {
                double bx = 18 - i * 4.6, by = -48 + i * 0.6 + (i > 4 ? 2 : 0);
                c.MoveTo(bx - 1.6, by + 1);
                c.LineTo(bx - 1 - i * 0.2, by - 6 + Math.Sin(this.t * 3 + i) * 0.6);
                c.LineTo(bx + 1.6, by + 1);
            }
            c.Fill();
            leg(0, false);
            leg(2, false);
            // three necks from the one chest, and their heads
            for (int i = 0; i < 3; i++)
            {
                double look = Math.Sin(this.t * (1.3 + i * 0.4) + i * 2) * 0.2;
                double a = -1.45 + i * 0.55 + look;
                double nl = 22 + (i == 1 ? 4 : 0);
                double nx = 24 + Math.Cos(a) * nl, ny = -38 + Math.Sin(a) * nl;
                double kk = i == 0 ? 0.8 : i == 1 ? 0.9 : 1;
                this.mat(c, cr * kk, cg * kk, cb * kk);
                limbB(c, 20, -38, nx, ny, 13, 9, 1.4, 1.4);
                double snap = Math.Max(0, Math.Sin(this.t * 1.7 + i * 2.4)) > 0.9 ? 1 : 0;
                c.Save();
                c.Translate(nx, ny);
                c.Rotate(a * 0.4 + 0.15);
                // skull, the broad muzzle, the jaws, the ears laid back
                c.BeginPath();
                c.MoveTo(-6, -4);
                c.QuadraticCurveTo(0, -9, 6, -5);
                c.LineTo(15, -3.6);
                c.QuadraticCurveTo(17, -1, 15, 1);      // the nose
                c.LineTo(6, 1.4);
                c.LineTo(14, 2 + snap * 5);             // the lower jaw
                c.QuadraticCurveTo(6, 7, -4, 5);
                c.QuadraticCurveTo(-8, 1, -6, -4);
                c.ClosePath();
                c.Fill();
                c.BeginPath();
                c.MoveTo(-3, -6); c.LineTo(-9, -11); c.LineTo(-4, -2);
                c.ClosePath();
                c.Fill();
                if (snap > 0 && this.farLit)
                {
                    c.FillStyle = this.farTone(120, 30, 26, 0.9);
                    c.BeginPath();
                    c.MoveTo(6, 1.6); c.LineTo(14, 2.2); c.LineTo(13, 6); c.ClosePath();
                    c.Fill();
                }
                c.Restore();
            }
            // the bronze collar, spiked
            this.mat(c, 196, 150, 82);
            c.BeginPath();
            c.Ellipse(23, -41, 2.2, 6, 0.25, 0, Math.PI * 2);
            c.Fill();
            for (int i = 0; i < 4; i++)
            {
                double sy = -46 + i * 4;
                c.BeginPath();
                c.MoveTo(24, sy); c.LineTo(28, sy + 1); c.LineTo(24, sy + 2);
                c.ClosePath();
                c.Fill();
            }
            c.Restore();
            // the eyes, coals — drawn live so the haze does not put them out
        }
    }
}
