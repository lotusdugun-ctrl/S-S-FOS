using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// Bodies for the far figures, built the way a draughtsman builds them rather than out of
    /// sticks: limbs with the swell of their muscle on the right side (the quadriceps in
    /// front of the thigh, the calf behind the shin, biceps and triceps), a torso with a
    /// chest, a waist and a seat, a neck into a head with a profile; and the horse (and the
    /// hound) with their own build — chest, withers, back, croup, a neck with its crest, the
    /// long head, legs that bend the way theirs do at knee and hock, hooves that land and push
    /// off in a real walk, canter or gallop. Everything is drawn in a frame of its own: x
    /// toward the way it faces, y down, the ground at y = 0.
    /// </summary>
    public partial class SisyphusEngine
    {
        // ---------------------------------------------------------------- shapes

        /// <summary>
        /// A limb from a (width wa) to b (width wb) with a swelling on each side: `front` on the
        /// side the figure faces (+x), `back` on the other, peaking at `at` of the way along.
        /// </summary>
        private static void limbB(Ctx c, double ax, double ay, double bx, double by, double wa, double wb, double front, double back, double at = 0.38)
        {
            double dx = bx - ax, dy = by - ay;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) return;
            double nx = -dy / len, ny = dx / len;
            if (nx < 0 || (nx == 0 && ny > 0)) { nx = -nx; ny = -ny; }
            double ra = wa / 2, rb = wb / 2;
            double mx = ax + dx * at, my = ay + dy * at;
            double rm = ra + (rb - ra) * at;
            c.BeginPath();
            c.MoveTo(ax + nx * ra, ay + ny * ra);
            c.QuadraticCurveTo(mx + nx * (rm + front) * 1.25, my + ny * (rm + front) * 1.25, bx + nx * rb, by + ny * rb);
            c.LineTo(bx - nx * rb, by - ny * rb);
            c.QuadraticCurveTo(mx - nx * (rm + back) * 1.25, my - ny * (rm + back) * 1.25, ax - nx * ra, ay - ny * ra);
            c.ClosePath();
            c.Fill();
            c.BeginPath();
            c.Arc(ax, ay, ra, 0, Math.PI * 2);
            c.Fill();
            c.BeginPath();
            c.Arc(bx, by, rb, 0, Math.PI * 2);
            c.Fill();
        }

        /// <summary>two bones from a to a point at distance l1+l2 at most, bending toward `bendSign` (+1 forward)</summary>
        private static void ik2(double ax, double ay, double tx, double ty, double l1, double l2, double bendSign, out double jx, out double jy, out double ex, out double ey)
        {
            double dx = tx - ax, dy = ty - ay;
            double d = Math.Sqrt(dx * dx + dy * dy);
            double reach = l1 + l2 - 0.01;
            if (d > reach) { tx = ax + dx / d * reach; ty = ay + dy / d * reach; dx = tx - ax; dy = ty - ay; d = reach; }
            if (d < 1e-6) d = 1e-6;
            double a = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
            double h = Math.Sqrt(Math.Max(0, l1 * l1 - a * a));
            double ux = dx / d, uy = dy / d;
            double px = -uy, py = ux;
            if (px * bendSign < 0) { px = -px; py = -py; }
            jx = ax + ux * a + px * h;
            jy = ay + uy * a + py * h;
            ex = tx; ey = ty;
        }

        // ---------------------------------------------------------------- people

        /// <summary>how a person is dressed and coloured (r, g, b, 0..255)</summary>
        internal struct Look
        {
            public double skinR, skinG, skinB;
            public double clothR, clothG, clothB;
            public double hairR, hairG, hairB;
            /// <summary>0 bare, 1 a short tunic, 2 a long robe (to the ankle)</summary>
            public int dress;
            public bool helmet, cloak, woman, beard;
            /// <summary>only from the hips up (a rider, the centaur's man): (x, y) is then the hip</summary>
            public bool torsoOnly;
        }

        internal static Look Skin(double r, double g, double b) { return new Look { skinR = r, skinG = g, skinB = b, clothR = 200, clothG = 186, clothB = 156, hairR = 46, hairG = 34, hairB = 28 }; }

        /// <summary>a pose: angles in radians from straight down, + toward the way he faces</summary>
        internal struct Pose
        {
            public double lean, thighN, thighF, kneeN, kneeF, armN, armF, elbowN, elbowF, head;
        }

        /// <summary>a walk or run cycle at phase g (radians); `run` 0 walk .. 1 run, `effort` leans into it</summary>
        internal static Pose Gait(double g, double run, double effort)
        {
            double sN = Math.Sin(g), sF = Math.Sin(g + Math.PI);
            double swing = 0.42 + run * 0.35;
            var p = new Pose
            {
                lean = 0.06 + run * 0.18 + effort * 0.35,
                thighN = sN * swing,
                thighF = sF * swing,
                // the knee folds as the leg swings through, most just after the push-off
                kneeN = 0.12 + Math.Max(0, Math.Sin(g - 0.9)) * (0.7 + run * 0.9),
                kneeF = 0.12 + Math.Max(0, Math.Sin(g + Math.PI - 0.9)) * (0.7 + run * 0.9),
                armN = -sN * (0.3 + run * 0.4),
                armF = -sF * (0.3 + run * 0.4),
                elbowN = 0.35 + run * 1.0,
                elbowF = 0.35 + run * 1.0,
                head = -effort * 0.1,
            };
            return p;
        }

        /// <summary>
        /// A person `hgt` tall at (x, y) facing `face`, in the pose; the arms can be overridden
        /// (`armsAt`, in the figure's frame) to hold something. Returns where the near hand,
        /// the shoulders and the head are (in screen space) for what they carry.
        /// </summary>
        internal void person(Ctx c, double x, double y, double hgt, double face, Pose p, Look lk,
            Func<int, (double, double)?> handTarget = null)
        {
            double u = hgt / 100;
            c.Save();
            c.Translate(x, y);
            c.Scale(face * u, u);
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            const double THIGH = 25, SHIN = 24, UPPER = 17, FORE = 15.5;
            // the hip height that keeps the lower foot on the ground
            double legDrop(double thigh, double knee)
            {
                double ky = Math.Cos(thigh) * THIGH;
                double sy = Math.Cos(thigh - knee) * SHIN;
                return ky + sy + 3;
            }
            double hipY = lk.torsoOnly ? 0 : -Math.Max(legDrop(p.thighN, p.kneeN), legDrop(p.thighF, p.kneeF));
            double hipX = 0;
            double TORSO = 31;
            double shX = hipX + Math.Sin(p.lean) * TORSO, shY = hipY - Math.Cos(p.lean) * TORSO;
            (double, double, double, double, double, double) leg(double thigh, double knee)
            {
                double kx = hipX + Math.Sin(thigh) * THIGH, ky = hipY + Math.Cos(thigh) * THIGH;
                double a2 = thigh - knee;
                double ax = kx + Math.Sin(a2) * SHIN, ay = ky + Math.Cos(a2) * SHIN;
                return (kx, ky, ax, ay, a2, 0);
            }
            void drawLeg(double thigh, double knee, bool near)
            {
                var (kx, ky, ax, ay, a2, _) = leg(thigh, knee);
                this.matSkin(c, lk, near ? 1 : 0.8);
                limbB(c, hipX, hipY, kx, ky, 11, 7.6, 2.2, 1.2, 0.35);  // the thigh, the quadriceps in front
                limbB(c, kx, ky, ax, ay, 7.4, 4.2, 0.4, 2.3, 0.3);       // the shin, the calf behind
                // the foot: heel, the arch, the toes, flat when it is down
                double flat = ay > -4 ? 1 : 0.4;
                c.BeginPath();
                c.MoveTo(ax - 2.4, ay - 1.5);
                c.QuadraticCurveTo(ax - 3, ay + 2.6, ax + 1, ay + 2.6);
                c.LineTo(ax + 8.5 * flat + 4 * (1 - flat), ay + 2.6 - (1 - flat) * 2);
                c.QuadraticCurveTo(ax + 8, ay, ax + 1.5, ay - 1.5);
                c.ClosePath();
                c.Fill();
            }
            (double, double, double, double) arm(double sx, double sy, double a, double el, int which)
            {
                double ex = sx + Math.Sin(a) * UPPER, ey = sy + Math.Cos(a) * UPPER;
                double a2 = a + el;
                double hx = ex + Math.Sin(a2) * FORE, hy = ey + Math.Cos(a2) * FORE;
                var tgt = handTarget != null ? handTarget(which) : null;
                if (tgt.HasValue)
                {
                    var (tx, ty) = tgt.Value;
                    ik2(sx, sy, tx, ty, UPPER, FORE, -1, out ex, out ey, out hx, out hy);
                }
                return (ex, ey, hx, hy);
            }
            void drawArm(double sx, double sy, double a, double el, int which, bool near)
            {
                var (ex, ey, hx, hy) = arm(sx, sy, a, el, which);
                this.matSkin(c, lk, near ? 1 : 0.8);
                limbB(c, sx, sy, ex, ey, 7.6, 5.4, 1.4, 1.6, 0.42);   // biceps in front, triceps behind
                limbB(c, ex, ey, hx, hy, 5.4, 3.4, 1.4, 0.6, 0.25);   // the forearm, full by the elbow
                c.BeginPath();
                c.Ellipse(hx + (hx - ex) * 0.12, hy + (hy - ey) * 0.12, 2.6, 2.1, Math.Atan2(hy - ey, hx - ex), 0, Math.PI * 2);
                c.Fill();
            }
            // far side first
            drawArm(shX - 1.5, shY + 1, p.armF, p.elbowF, 1, false);
            if (!lk.torsoOnly) drawLeg(p.thighF, p.kneeF, false);
            // the torso: the seat and the small of the back behind, belly and chest in front,
            // the shoulders' mass at the top — along the leaning axis
            double ux = Math.Sin(p.lean), uy = -Math.Cos(p.lean);
            double nx = -uy, ny = ux; // forward
            (double, double) T(double along, double off) => (hipX + ux * along * TORSO + nx * off, hipY + uy * along * TORSO + ny * off);
            var back = new[] { T(-0.04, -7), T(0.14, -9.4), T(0.42, -6.6), T(0.72, -8.4), T(0.98, -6.2) };
            var front = new[] { T(1.02, 5.2), T(0.84, 8.6), T(0.62, 7.4), T(0.44, 6), T(0.2, 7.6), T(-0.04, 6.4) };
            this.matSkin(c, lk, 1);
            c.BeginPath();
            var (x0, y0) = back[0];
            c.MoveTo(x0, y0);
            for (int i = 1; i < back.Length; i++)
            {
                var (xa, ya) = back[i - 1]; var (xb, yb) = back[i];
                c.QuadraticCurveTo(xa, ya, (xa + xb) / 2, (ya + yb) / 2);
            }
            var (bxL, byL) = back[back.Length - 1];
            c.LineTo(bxL, byL);
            // over the shoulder
            var (fx0, fy0) = front[0];
            c.QuadraticCurveTo(shX + ux * 4, shY + uy * 4, fx0, fy0);
            for (int i = 1; i < front.Length; i++)
            {
                var (xa, ya) = front[i - 1]; var (xb, yb) = front[i];
                c.QuadraticCurveTo(xa, ya, (xa + xb) / 2, (ya + yb) / 2);
            }
            var (fxl, fyl) = front[front.Length - 1];
            c.LineTo(fxl, fyl);
            c.ClosePath();
            c.Fill();
            // the near leg and the clothes over the hips
            if (!lk.torsoOnly) drawLeg(p.thighN, p.kneeN, true);
            if (lk.dress > 0 && !lk.torsoOnly)
            {
                this.mat(c, lk.clothR, lk.clothG, lk.clothB);
                double hem = lk.dress == 2 ? -3 : hipY + 16;
                var (kxN, kyN, _, _, _, _) = leg(p.thighN, p.kneeN);
                var (kxF, kyF, _, _, _, _) = leg(p.thighF, p.kneeF);
                double spread = lk.dress == 2 ? 5 : 3;
                c.BeginPath();
                var (cx0, cy0) = T(0.62, -7.4);
                var (cx1, cy1) = T(0.62, 7.8);
                c.MoveTo(cx0, cy0);
                c.LineTo(cx1, cy1);
                double frontX = Math.Max(kxN, kxF) + spread, backX = Math.Min(kxN, kxF) - spread;
                if (lk.dress == 1) { frontX = hipX + 8 + Math.Max(0, Math.Max(kxN, kxF) - hipX) * 0.5; backX = hipX - 8 + Math.Min(0, Math.Min(kxN, kxF) - hipX) * 0.5; }
                c.QuadraticCurveTo(hipX + 9, hipY + 2, frontX, hem);
                c.QuadraticCurveTo((frontX + backX) / 2, hem + 1.5, backX, hem);
                c.QuadraticCurveTo(hipX - 9, hipY + 2, cx0, cy0);
                c.ClosePath();
                c.Fill();
                // its folds
                c.StrokeStyle = this.farTone(lk.clothR * 0.55, lk.clothG * 0.55, lk.clothB * 0.55, 0.6);
                c.LineWidth = 0.8;
                c.BeginPath();
                c.MoveTo(hipX + 2, hipY + 2); c.LineTo((frontX + hipX) / 2, hem - 1);
                c.MoveTo(hipX - 3, hipY + 3); c.LineTo((backX + hipX) / 2, hem - 1);
                c.Stroke();
                if (lk.cloak)
                {
                    c.BeginPath();
                    var (sx0, sy0) = T(1.0, -6);
                    c.MoveTo(sx0, sy0);
                    c.QuadraticCurveTo(sx0 - 9, (sy0 + hipY) / 2, hipX - 13, hipY + 8 + Math.Sin(this.t * 2 + x) * 1.5);
                    c.LineTo(hipX - 6, hipY + 2);
                    c.QuadraticCurveTo(sx0 - 3, (sy0 + hipY) / 2, sx0 + 2, sy0);
                    c.ClosePath();
                    c.Fill();
                }
            }
            // neck and head: the head carried forward of the shoulders, a profile to the face
            double nkx = shX + ux * 3 + nx * 1.5, nky = shY + uy * 3 + ny * 1.5;
            double hcx = nkx + Math.Sin(p.lean + p.head) * 9 + 1.5, hcy = nky - Math.Cos(p.lean + p.head) * 9;
            this.matSkin(c, lk, 1);
            limbB(c, shX + nx * 0.5, shY + ny * 0.5, nkx, nky, 7, 5.6, 0.6, 0.6);
            c.Save();
            c.Translate(hcx, hcy);
            c.Rotate(p.lean * 0.6 + p.head);
            c.BeginPath();
            // the skull and the face: brow, nose, lips, chin, jaw back to the ear
            c.MoveTo(-6.2, -1);
            c.BezierCurveTo(-6.6, -8.6, 3.6, -9.6, 5.6, -3);
            c.LineTo(7.2, 0.6);   // the nose
            c.LineTo(5.8, 1.4);
            c.LineTo(6, 3);       // the lips
            c.QuadraticCurveTo(5.2, 6.2, 2, 6.6);  // the chin
            c.QuadraticCurveTo(-2.4, 6.2, -4.8, 3.2);
            c.QuadraticCurveTo(-6.4, 1.6, -6.2, -1);
            c.ClosePath();
            c.Fill();
            // hair, or the helmet
            if (lk.helmet)
            {
                this.mat(c, 190, 146, 74);
                c.BeginPath();
                c.MoveTo(-7, 2.6);
                c.BezierCurveTo(-8, -9.5, 5, -11, 6.4, -2.6);
                c.LineTo(4.6, -2);
                c.LineTo(4.4, 3.6);   // the cheek-piece
                c.LineTo(1.6, 4.4);
                c.LineTo(1.2, -0.6);
                c.LineTo(-4, -0.4);
                c.LineTo(-4.4, 3.8);
                c.ClosePath();
                c.Fill();
                // the crest, front to back
                this.mat(c, 150, 40, 34);
                c.BeginPath();
                c.MoveTo(4, -8.6);
                c.QuadraticCurveTo(-2, -16, -11, -7);
                c.QuadraticCurveTo(-5, -10.6, 2, -8.2);
                c.ClosePath();
                c.Fill();
            }
            else
            {
                this.mat(c, lk.hairR, lk.hairG, lk.hairB);
                c.BeginPath();
                if (lk.woman)
                {
                    c.MoveTo(4.6, -6);
                    c.BezierCurveTo(1, -10.6, -8.4, -8.4, -7.6, 0);
                    c.QuadraticCurveTo(-9, 6, -6.6, 11);   // gathered at the nape
                    c.QuadraticCurveTo(-4.4, 6, -4.2, 0.6);
                    c.QuadraticCurveTo(-1, -4.6, 4.6, -6);
                }
                else
                {
                    c.MoveTo(5, -5.4);
                    c.BezierCurveTo(2, -10, -8, -9, -6.8, 0.4);
                    c.LineTo(-4.4, 2.6);
                    c.QuadraticCurveTo(-3.4, -3.4, 2.4, -4.2);
                    c.ClosePath();
                }
                c.Fill();
                if (lk.beard)
                {
                    c.BeginPath();
                    c.MoveTo(-3.6, 2.6);
                    c.QuadraticCurveTo(-1, 9, 4, 7.4);
                    c.LineTo(5.6, 3.6);
                    c.QuadraticCurveTo(2, 5, -0.4, 2.2);
                    c.ClosePath();
                    c.Fill();
                }
            }
            c.Restore();
            // the near arm last, over the body
            drawArm(shX + 0.5, shY + 1.5, p.armN, p.elbowN, 0, true);
            c.Restore();
        }

        /// <summary>a skin part of a person, a little darker for the far side</summary>
        private void matSkin(Ctx c, Look lk, double k)
        {
            this.mat(c, lk.skinR * k, lk.skinG * k, lk.skinB * k);
        }

        /// <summary>where the shoulder of a person drawn by `person` lands in screen space (for spears, shields)</summary>
        internal static (double, double) personPoint(double x, double y, double hgt, double face, double lx, double ly)
        {
            double u = hgt / 100;
            return (x + face * lx * u, y + ly * u);
        }

        // ---------------------------------------------------------------- horses

        /// <summary>
        /// The legs of a four-footed animal through one stride: for leg i (0 near fore, 1 far
        /// fore, 2 near hind, 3 far hind) where its hoof is relative to the leg's top, in a walk
        /// (0), canter (1) or gallop (2). Stance: the hoof on the ground going back; swing: it
        /// lifts, folds and reaches forward.
        /// </summary>
        private static (double, double) hoofPath(double g, int leg, int gait, double stride, double lift, double depth)
        {
            double[][] offs =
            {
                new[] { 0.0, 0.5, 0.75, 0.25 },   // walk: four beats
                new[] { 0.35, 0.55, 0.0, 0.25 },  // canter
                new[] { 0.42, 0.52, 0.0, 0.12 },  // gallop
            };
            double duty = gait == 0 ? 0.62 : gait == 1 ? 0.45 : 0.36;
            double ph = (((g / (Math.PI * 2) + offs[gait][leg]) % 1) + 1) % 1;
            if (ph < duty)
            {
                double s = ph / duty;
                return (stride * (0.5 - s), depth);
            }
            double w = (ph - duty) / (1 - duty);
            double x = stride * (-0.5 + w);
            double y = depth - Math.Sin(w * Math.PI) * lift;
            return (x, y);
        }

        /// <summary>the look of a horse</summary>
        internal struct HorseLook
        {
            public double r, g, b, maneR, maneG, maneB;
            public bool head;
        }

        /// <summary>
        /// A horse at (x, y) facing `face`, `len` long (chest to buttock), in a gait (0 walk,
        /// 1 canter, 2 gallop) at phase g. With `look.head` false there is no neck or head: the
        /// centaur's man rises from the withers instead. Returns the withers (screen space).
        /// </summary>
        internal (double, double) horse(Ctx c, double x, double y, double len, double face, int gait, double g, HorseLook look)
        {
            double u = len / 60;
            c.Save();
            c.Translate(x, y);
            c.Scale(face * u, u);
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            double bob = gait == 0 ? Math.Abs(Math.Sin(g)) * 0.6 : gait == 1 ? Math.Sin(g) * 1.6 : Math.Sin(g * 2) * 1.4;
            double pitch = gait == 2 ? Math.Sin(g) * 0.05 : gait == 1 ? Math.Sin(g) * 0.04 : 0;
            c.Translate(0, -bob);
            c.Rotate(pitch);
            double stride = gait == 0 ? 16 : gait == 1 ? 26 : 36;
            double lift = gait == 0 ? 6 : gait == 1 ? 10 : 13;
            // the tops of the legs: the point of the shoulder and the hip
            double fsx = 15, fsy = -40, hsx = -22, hsy = -44;
            void leg(int i, bool far)
            {
                bool fore = i < 2;
                double sx = fore ? fsx : hsx, sy = fore ? fsy : hsy;
                var (hx, hy) = hoofPath(g, i, gait, stride, lift, 0);
                hx += fore ? 15 : -20;
                // keep the hoof on the ground line (y = 0 in the body's frame, bob undone)
                hy += bob;
                double fx = hx + (fore ? -1.5 : -1), fy = hy - 5.5; // fetlock above the hoof
                double l1 = fore ? 20 : 22, l2 = fore ? 17.5 : 18;
                double jx, jy, ex, ey;
                // the knee bends forward of its line only when the leg folds; the hock always points back
                ik2(sx, sy, fx, fy, l1, l2, fore ? 1 : -1, out jx, out jy, out ex, out ey);
                double k = far ? 0.78 : 1;
                this.mat(c, look.r * k, look.g * k, look.b * k);
                if (fore)
                {
                    limbB(c, sx, sy, jx, jy, 9, 5, 1.6, 1.2, 0.3);   // forearm, muscled at the top
                    limbB(c, jx, jy, ex, ey, 4.4, 3.2, 0.2, 0.2);    // cannon
                }
                else
                {
                    limbB(c, sx, sy, jx, jy, 11, 5.4, 1.2, 2.4, 0.3); // gaskin, the hamstring behind
                    limbB(c, jx, jy, ex, ey, 4.6, 3.2, 0.2, 0.4);
                }
                // pastern and hoof, darker
                c.BeginPath();
                c.MoveTo(ex - 1.6, ey);
                c.LineTo(hx + 1.6, hy - 2.4);
                c.LineTo(hx + 2.6, hy);
                c.LineTo(hx - 2.4, hy);
                c.LineTo(ex - 2.2, ey + 1);
                c.ClosePath();
                c.Fill();
                this.mat(c, 40, 32, 30);
                c.BeginPath();
                c.MoveTo(hx - 2.4, hy); c.LineTo(hx + 2.8, hy); c.LineTo(hx + 1.6, hy - 2.6); c.LineTo(hx - 2, hy - 2.4);
                c.ClosePath();
                c.Fill();
            }
            leg(1, true);
            leg(3, true);
            // the tail, flowing back from the dock
            this.mat(c, look.maneR, look.maneG, look.maneB);
            double tw = Math.Sin(g * (gait == 0 ? 1 : 1.5)) * (gait == 2 ? 3 : 1.6);
            c.BeginPath();
            c.MoveTo(-29, -50);
            c.BezierCurveTo(-36, -50 - (gait == 2 ? 3 : 0), -40, -40 + tw, -38 - (gait == 2 ? 8 : 2), -24 + tw * 2);
            c.BezierCurveTo(-36, -34 + tw, -33, -42, -27, -46);
            c.ClosePath();
            c.Fill();
            // the body: chest, girth, belly, flank, the round of the quarters, croup, back, withers
            this.mat(c, look.r, look.g, look.b);
            c.BeginPath();
            c.MoveTo(22, -50);                                 // the point of the shoulder
            c.QuadraticCurveTo(25, -40, 19, -33);              // the chest
            c.QuadraticCurveTo(14, -27, 6, -27.5);             // girth
            c.QuadraticCurveTo(-8, -26, -16, -30);             // belly
            c.QuadraticCurveTo(-22, -31, -26, -35);            // the flank, up to the stifle
            c.QuadraticCurveTo(-33, -40, -31.5, -48);          // the buttock
            c.QuadraticCurveTo(-30, -55, -24, -56.5);          // the croup
            c.QuadraticCurveTo(-8, -54, 4, -55.5);             // the back
            c.QuadraticCurveTo(10, -58.5, 14, -57);            // the withers
            c.QuadraticCurveTo(20, -55, 22, -50);
            c.ClosePath();
            c.Fill();
            // the muscle under the hide, caught by the light: shoulder, quarters, the barrel's round
            if (this.farLit)
            {
                c.FillStyle = this.farTone(look.r * 0.5, look.g * 0.5, look.b * 0.5, 0.35);
                c.BeginPath();
                c.Ellipse(2, -29.5, 13, 2.6, 0, 0, Math.PI * 2);
                c.Fill();
                c.StrokeStyle = this.farTone(look.r * 0.4, look.g * 0.4, look.b * 0.4, 0.55);
                c.LineWidth = 0.7;
                c.BeginPath();
                c.MoveTo(10, -51); c.QuadraticCurveTo(9, -42, 13, -34);      // the back of the shoulder
                c.MoveTo(-17, -52); c.QuadraticCurveTo(-15, -42, -21, -34);  // the line of the quarters
                c.Stroke();
            }
            double wx = 12, wy = -57;
            if (look.head)
            {
                // the neck: the crest arching up from the withers to the poll, the underline
                // from the chest to the throat; the head long, the jaw round, the muzzle soft
                double nod = gait == 0 ? Math.Sin(g * 2) * 1.6 : Math.Sin(g) * (gait == 2 ? 3.2 : 2.4);
                double px = 31, py = -73 + nod;      // the poll
                this.mat(c, look.r, look.g, look.b);
                c.BeginPath();
                c.MoveTo(8, -56);
                c.QuadraticCurveTo(20, -70 + nod * 0.5, px, py);
                c.LineTo(px + 5, py + 8);
                c.QuadraticCurveTo(27, -56 + nod * 0.4, 23.5, -46);
                c.ClosePath();
                c.Fill();
                c.Save();
                c.Translate(px, py);
                c.Rotate(0.62 + nod * 0.02);
                c.Scale(1.3, 1.3);
                c.BeginPath();
                c.MoveTo(-2, -2);
                c.QuadraticCurveTo(6, -4.2, 17, -1.4);     // the face
                c.QuadraticCurveTo(20, 0.6, 18.6, 3.4);   // the muzzle
                c.QuadraticCurveTo(15, 5.4, 11, 4.4);     // the lip and chin
                c.QuadraticCurveTo(5, 7.6, 0.6, 6.6);     // the round of the jaw
                c.QuadraticCurveTo(-3, 4, -2, -2);
                c.ClosePath();
                c.Fill();
                // the ears, pricked
                c.BeginPath();
                c.MoveTo(-0.6, -1.4); c.LineTo(-1.6, -7.6); c.LineTo(2.2, -2.6);
                c.ClosePath();
                c.Fill();
                if (this.farLit)
                {
                    c.FillStyle = this.farTone(20, 14, 14, 0.9);
                    c.BeginPath();
                    c.Arc(5.2, 0.4, 0.9, 0, Math.PI * 2);   // the eye
                    c.Arc(17.2, 2.2, 0.6, 0, Math.PI * 2);  // the nostril
                    c.Fill();
                }
                c.Restore();
                // the mane along the crest, lifting in the wind of the gallop: a strip that
                // follows the crest from the poll to the withers, ragged at its edge
                this.mat(c, look.maneR, look.maneG, look.maneB);
                double fl = gait == 2 ? 4 : gait == 1 ? 2.6 : 1.4;
                (double, double) crest(double t)
                {
                    double it = 1 - t;
                    return (it * it * 8 + 2 * it * t * 20 + t * t * px, it * it * -56.5 + 2 * it * t * (-70 + nod * 0.5) + t * t * py);
                }
                c.BeginPath();
                var (m0x, m0y) = crest(1);
                c.MoveTo(m0x + 1, m0y);
                for (int i = 10; i >= 0; i--)
                {
                    var (qx, qy) = crest(i / 10.0);
                    c.LineTo(qx, qy + 0.6);
                }
                for (int i = 0; i <= 10; i++)
                {
                    var (qx, qy) = crest(i / 10.0);
                    double rag = (i % 2 == 0 ? 1 : 0.55) * (2.2 + fl * 0.5);
                    c.LineTo(qx - fl * 0.6 - Math.Sin(this.t * 6 + i) * 0.5, qy - rag);
                }
                c.ClosePath();
                c.Fill();
            }
            this.mat(c, look.r, look.g, look.b);
            leg(0, false);
            leg(2, false);
            c.Restore();
            // the withers in screen space, for a rider or a centaur's man
            double cs = Math.Cos(pitch), sn = Math.Sin(pitch);
            double rx = wx * cs - (wy) * sn, ry = wx * sn + (wy) * cs - bob;
            return (x + face * rx * u, y + ry * u);
        }
    }
}
