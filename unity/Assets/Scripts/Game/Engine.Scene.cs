// Port of src/game/engine.ts lines 3328-5198 (part C): drawForeground through drawBolt —
// shadows, the boulder, birds, the eagle, motes, the ground, outcrops, stones, whispers,
// vibrance, the others on the hill, echoes, the wedge, the stone's memory, steles, the breath
// ring, wind, weather, mire, scree, keepsakes, ruins, trees, Zeus and his bolt — plus the
// fields declared in that range.
using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    public partial class SisyphusEngine
    {
        private void drawForeground()
        {
            var ctx = this.ctx;
            double w = this.w, h = this.h;
            double s = this.scale;
            double scroll = this.camX * 1.5 * s;
            double period = 360 * s;
            double first = Math.Floor(scroll / period) - 1;
            const int n = 9;
            var ptsX = new double[n + 1];
            var ptsY = new double[n + 1];
            for (double i = first; i * period - scroll < w + period; i++)
            {
                double h1 = this.hash(i * 3.77 + 0.5);
                double h2 = this.hash(i * 7.13 + 2.5);
                double x = i * period - scroll + h1 * period * 0.6;
                if (h1 > 0.3)
                {
                    // a weathered rock: an irregular polygon with a lit top edge
                    double wide = (40 + h2 * 70) * s;
                    double rise = (22 + h1 * 40) * s;
                    for (int k = 0; k <= n; k++)
                    {
                        double t = (double)k / n;
                        double a = Math.PI * (1 - t);
                        double jag = 0.78 + this.hash(i * 11.3 + k * 1.7) * 0.32;
                        ptsX[k] = x + Math.Cos(a) * wide * jag;
                        ptsY[k] = h + 6 - Math.Sin(a) * rise * jag;
                    }
                    ctx.FillStyle = "oklch(0.09 0.012 40)";
                    ctx.BeginPath();
                    ctx.MoveTo(x - wide * 1.05, h + 8);
                    for (int k = 0; k <= n; k++) ctx.LineTo(ptsX[k], ptsY[k]);
                    ctx.LineTo(x + wide * 1.05, h + 8);
                    ctx.ClosePath();
                    ctx.Fill();
                    ctx.StrokeStyle = "rgba(255, 196, 130, 0.22)";
                    ctx.LineWidth = 1.4 * s;
                    ctx.LineJoin = LineJoin.Round;
                    ctx.BeginPath();
                    for (int k = 2; k <= n - 1; k++)
                    {
                        if (k == 2) ctx.MoveTo(ptsX[k], ptsY[k]);
                        else ctx.LineTo(ptsX[k], ptsY[k]);
                    }
                    ctx.Stroke();
                }
                // big grass clumps, close enough to see every blade move
                double clumps = 1 + Math.Floor(h2 * 3);
                for (int k = 0; k < clumps; k++)
                {
                    double gx = x + (this.hash(i * 5.1 + k) - 0.5) * period * 0.7;
                    Flora.grassClump(
                        ctx,
                        gx,
                        h + 4,
                        (40 + this.hash(i * 2.3 + k) * 40) * s,
                        i * 31 + k,
                        this.windAt(gx / s + this.camX) * 1.3,
                        "oklch(0.08 0.012 40 / 0.95)",
                        "rgba(255, 200, 130, 0.16)");
                }
            }
        }

        /// <summary>
        /// A long shadow on the face of the hill. The sun is low and behind the
        /// slope, so anything standing on the skyline throws its shadow down the
        /// face toward the viewer and away from the sun's side — soft, widening and
        /// fading with distance the way a penumbra does.
        /// </summary>
        private void drawLongShadow(double x, double y, double half, double len)
        {
            var ctx = this.ctx;
            var sun = this.lightScreen();
            if (sun.strength < 0.03) return;
            // a low light throws long shadows, a high one short ones
            len *= Math.Max(0.3, Math.Min(1.35, 1.35 - sun.elev / 45));
            // Away from the sun, and more sideways the further from the sun's line —
            // continuously (it used to snap from one side to the other when the stone
            // crossed the sun's line). Right under the sun it points straight down the
            // slope, and swings smoothly through that point.
            double dx = Math.Max(-0.95, Math.Min(0.95, (x - sun.x) / (this.w * 0.45)));
            double dy = 0.55;
            double dl = Js.hypot(dx, dy);
            dx /= dl;
            dy /= dl;
            double tx = x + dx * len;
            double ty = y + dy * len;
            double px = -dy;
            double py = dx;
            var g = ctx.CreateLinearGradient(x, y, tx, ty);
            double k = Math.Min(1, sun.strength * 1.1);
            g.AddColorStop(0, Col.Rgba(14, 8, 12, Css.fixedNum(0.42 * k, 3)));
            g.AddColorStop(0.6, Col.Rgba(14, 8, 12, Css.fixedNum(0.16 * k, 3)));
            g.AddColorStop(1, "rgba(14, 8, 12, 0)");
            ctx.FillStyle = g;
            ctx.BeginPath();
            ctx.MoveTo(x + px * half, y + py * half * 0.4);
            ctx.QuadraticCurveTo(tx + px * half * 1.8, ty + py * half, tx, ty);
            ctx.QuadraticCurveTo(tx - px * half * 1.8, ty - py * half, x - px * half, y - py * half * 0.4);
            ctx.ClosePath();
            ctx.Fill();
        }

        private void drawContactShadow(double bx, double by, double R, double ang)
        {
            var ctx = this.ctx;
            ctx.Save();
            ctx.GlobalAlpha = 0.45;
            ctx.FillStyle = "oklch(0.08 0 0)";
            ctx.BeginPath();
            ctx.Ellipse(bx, by + 2, R * 0.9, R * 0.22, ang, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Restore();
        }

        /// <summary>granite texture for the stone, painted once at screen density</summary>
        private Surface stoneTex = null;
        private double stoneTexPx = 0;

        /// <summary>
        /// The stone: a rough granite boulder, backlit.
        ///
        /// A rock between you and a low sun is mostly in its own shadow, with a hard,
        /// bright edge where the light grazes the far side. That is what this paints:
        /// the real grain of the stone (turning with it), darkened on the face toward
        /// you, a rim of light that follows its actual outline on the sun's side, a
        /// little warm bounce from the ground underneath, and the dark where it sits
        /// in the dirt.
        /// </summary>
        private void drawBoulder(double bx, double by, double R, double ang)
        {
            var ctx = this.ctx;
            double s = this.scale;

            // an irregular outline: a few broad lumps, then smaller chips
            const int N = 40;
            var ptsX = new double[N];
            var ptsY = new double[N];
            for (int i = 0; i < N; i++)
            {
                double a = ((double)i / N) * Math.PI * 2;
                double n =
                    Math.Sin(a * 2 + 0.6) * 0.045 +
                    Math.Sin(a * 3 + 1.3) * 0.05 +
                    Math.Sin(a * 7 + 4.2) * 0.028 +
                    Math.Sin(a * 13 + 0.7) * 0.016 +
                    (this.hash(i * 3.7) - 0.5) * 0.03;
                double r = R * (1 + n);
                ptsX[i] = Math.Cos(a) * r;
                ptsY[i] = Math.Sin(a) * r;
            }
            void outline()
            {
                ctx.MoveTo(ptsX[0], ptsY[0]);
                for (int i = 1; i < N; i++) ctx.LineTo(ptsX[i], ptsY[i]);
                ctx.ClosePath();
            }

            double rot = ang + this.roll;
            double cr = Math.Cos(rot);
            double sr = Math.Sin(rot);
            (double, double) toLocal(double vx, double vy)
            {
                return (vx * cr + vy * sr, -vx * sr + vy * cr);
            }
            var sun = this.lightScreen();
            // warmed toward stone and softened under a high sun, so the rim reads as
            // light on rock rather than a white outline
            double hiSun = Math.Max(0, Math.Min(1, (sun.elev - 8) / 35));
            var lc = new RGB(
                sun.color[0] * 0.6 + 250 * 0.4,
                sun.color[1] * 0.6 + 196 * 0.4,
                sun.color[2] * 0.6 + 150 * 0.4);
            double ls = Math.Min(1, sun.strength * 1.15) * (1 - hiSun * 0.5);
            double sdx = sun.x - bx;
            double sdy = sun.y - by;
            double slen = Js.hypot(sdx, sdy);
            if (slen == 0 || double.IsNaN(slen)) slen = 1;
            var (lx, ly) = toLocal(sdx / slen, sdy / slen);
            var (dnx, dny) = toLocal(0, 1);

            // texture at the screen's pixel density, rebuilt only if the size changes
            double want = Math.Ceiling(R * 2.2 * this.dpr);
            if (this.stoneTex == null || Math.Abs(this.stoneTexPx - want) > want * 0.25)
            {
                this.stoneTex = Textures.graniteTexture(Math.Max(64, Math.Min(512, want)));
                this.stoneTexPx = want;
            }

            ctx.Save();
            ctx.Translate(bx, by);
            ctx.Rotate(rot);
            ctx.BeginPath();
            outline();
            ctx.Save();
            ctx.Clip();
            ctx.DrawImage(this.stoneTex, -R * 1.1, -R * 1.1, R * 2.2, R * 2.2);

            // the face toward us is in the stone's own shadow; the sun side is not
            var shade = ctx.CreateLinearGradient(lx * R, ly * R, -lx * R, -ly * R);
            shade.AddColorStop(0, "rgba(40, 26, 18, 0)");
            shade.AddColorStop(0.35, "rgba(30, 20, 18, 0.28)");
            shade.AddColorStop(1, "rgba(16, 12, 16, 0.62)");
            ctx.FillStyle = shade;
            ctx.FillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

            // sky fill from above, cool and faint, so the top reads as a curved surface
            var (ux, uy) = toLocal(0, -1);
            var sky = ctx.CreateRadialGradient(ux * R * 0.7, uy * R * 0.7, 0, ux * R * 0.7, uy * R * 0.7, R * 0.9);
            sky.AddColorStop(0, "rgba(170, 160, 200, 0.14)");
            sky.AddColorStop(1, "rgba(170, 160, 200, 0)");
            ctx.FillStyle = sky;
            ctx.FillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

            // warm bounce off the sunlit dirt under it
            var bounce = ctx.CreateRadialGradient(dnx * R * 0.9, dny * R * 0.9, 0, dnx * R * 0.9, dny * R * 0.9, R * 0.8);
            bounce.AddColorStop(0, "rgba(190, 120, 60, 0.16)");
            bounce.AddColorStop(1, "rgba(190, 120, 60, 0)");
            ctx.FillStyle = bounce;
            ctx.FillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

            // where it sits in the dirt: dark
            var ao = ctx.CreateRadialGradient(dnx * R * 0.75, dny * R * 0.75, R * 0.05, dnx * R * 0.75, dny * R * 0.75, R * 0.6);
            ao.AddColorStop(0, "rgba(0,0,0,0.42)");
            ao.AddColorStop(1, "rgba(0,0,0,0)");
            ctx.FillStyle = ao;
            ctx.FillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);

            // the glow just inside the lit edge, where light scatters into the grain
            var glow = ctx.CreateRadialGradient(lx * R * 1.05, ly * R * 1.05, 0, lx * R * 1.05, ly * R * 1.05, R * 0.7);
            glow.AddColorStop(0, Sky.rgb(lc, 0.45 * ls));
            glow.AddColorStop(1, "rgba(255, 200, 130, 0)");
            ctx.FillStyle = glow;
            ctx.FillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);
            this.drawStoneMemory(ctx, R);
            ctx.Restore();

            // the rim: each edge of the outline lit by how squarely it faces the sun
            ctx.LineCap = LineCap.Round;
            for (int i = 0; i < N; i++)
            {
                double x0 = ptsX[i], y0 = ptsY[i];
                double x1 = ptsX[(i + 1) % N], y1 = ptsY[(i + 1) % N];
                double mx = (x0 + x1) / 2;
                double my = (y0 + y1) / 2;
                double ml = Js.hypot(mx, my);
                if (ml == 0 || double.IsNaN(ml)) ml = 1;
                double facing = (mx / ml) * lx + (my / ml) * ly;
                if (facing < 0.2) continue;
                double k = (facing - 0.2) / 0.8;
                ctx.StrokeStyle = Sky.rgb(lc, (0.25 + k * 0.7) * ls);
                ctx.LineWidth = (0.8 + k * 1.8) * s;
                ctx.BeginPath();
                ctx.MoveTo(x0, y0);
                ctx.LineTo(x1, y1);
                ctx.Stroke();
            }
            ctx.Restore();
        }

        /// <summary>
        /// One bird in silhouette, as you see a large bird against a bright sky from
        /// below and a little to the side: a small body, and two wings bent at the
        /// wrist like a gull's. `e` is wing elevation, -1 (bottom of the downstroke)
        /// to 1 (top of the upstroke); the hand lags the arm through the stroke, so
        /// the wing bends rather than swinging like a board.
        /// </summary>
        private void drawBird(double x, double y, double s, double e, double dir, double tilt, Style color)
        {
            var ctx = this.ctx;
            ctx.Save();
            ctx.Translate(x, y);
            ctx.Rotate(tilt);
            ctx.Scale(dir, 1);
            ctx.FillStyle = color;
            // body, foreshortened, with the head just showing ahead of it
            ctx.BeginPath();
            ctx.Ellipse(0.3 * s, 0.2 * s, 1.5 * s, 0.75 * s, 0.15, 0, Math.PI * 2);
            ctx.Fill();
            ctx.BeginPath();
            ctx.Arc(1.7 * s, -0.15 * s, 0.55 * s, 0, Math.PI * 2);
            ctx.Fill();
            double lag = Math.Sin(Math.Asin(Math.Max(-1, Math.Min(1, e))) - 0.5);
            for (int si = 0; si < 2; si++)
            {
                double side = si == 0 ? -1 : 1;
                // the far wing (behind the body) a touch shorter: it is further away
                double k = side < 0 ? 0.9 : 1;
                double wx = side * 3.6 * s * k;
                double wy = -e * 2.4 * s - 0.5 * s;
                double tx = side * 8.2 * s * k;
                double ty = -lag * 4.6 * s + 0.4 * s;
                ctx.BeginPath();
                ctx.MoveTo(side * 0.6 * s, -0.4 * s);
                // leading edge: arm up to the wrist, then the hand out to the tip
                ctx.QuadraticCurveTo(side * 2 * s, wy - 0.4 * s, wx, wy);
                ctx.QuadraticCurveTo(side * 6 * s * k, (wy + ty) / 2 - 0.6 * s, tx, ty);
                // trailing edge: back along the primaries and secondaries to the body
                ctx.QuadraticCurveTo(side * 5.4 * s * k, (wy + ty) / 2 + 1 * s, wx * 0.85, wy + 1.2 * s);
                ctx.QuadraticCurveTo(side * 1.8 * s, 0.9 * s, side * 0.4 * s, 0.7 * s);
                ctx.ClosePath();
                ctx.Fill();
            }
            ctx.Restore();
        }

        private void renderBirds()
        {
            double w = this.w;
            double h = this.h;
            // birds roost at night: they thin out through dusk and are gone after dark
            double about = this.st.birds;
            if (about < 0.02) return;
            this.ctx.Save();
            this.ctx.GlobalAlpha = about;
            // against a burning sky a bird is a hard silhouette; the far ones lift a
            // little into the haze
            foreach (var b in this.birds)
            {
                double px = w * 0.38 + b.ox * this.scale * b.par;
                if (px < -80 || px > w + 80) continue;
                double py = b.fy * h + Math.Sin(b.wander) * b.bob;
                double s = b.size * this.scale * 0.85;
                // a real wingbeat is not a sine: the downstroke is the fast power stroke
                double ph = b.flap % (Math.PI * 2);
                double e =
                    b.glide > 0
                        ? 0.25
                        : ph < Math.PI * 0.8
                            ? Math.Cos((ph / (Math.PI * 0.8)) * Math.PI)
                            : -Math.Cos(((ph - Math.PI * 0.8) / (Math.PI * 1.2)) * Math.PI);
                double haze = 1 - b.par * 4;
                var color = Col.Oklch(Css.fixedNum(0.17 + haze * 0.14, 3), 0.03, 40, Css.fixedNum(0.95 - haze * 0.25, 3));
                double dir = Js.sign(b.speed);
                if (dir == 0 || double.IsNaN(dir)) dir = 1;
                this.drawBird(px, py, s, e, dir, Math.Sin(b.bank) * 0.08, color);
            }

            // Zeus's eagle, high over the hill, riding a thermal. It leaves when he comes.
            if (this.zeus.state == "gone" && about > 0.5 && !this.saga.eagleDead) this.drawEagle();
            this.ctx.Restore();
        }

        /// <summary>
        /// A golden eagle seen from below, soaring.
        ///
        /// What makes an eagle read as an eagle from the ground: wings very long and
        /// broad, nearly parallel-edged, bending slightly forward at the wrist; the
        /// hand ending in six or seven separate primaries spread like fingers, each
        /// slotted from the next; a head and heavy hooked bill that project well ahead
        /// of the wings; a fairly long tail, fanned and rounded. Against a low sun the
        /// trailing edge — flight feathers, thin and translucent — glows, while the
        /// coverts stay dark.
        ///
        /// It circles on a thermal, so its heading turns as it goes round and it
        /// banks into the turn; every so often it gives a few slow, deep wingbeats.
        /// </summary>
        private void drawEagle()
        {
            var ctx = this.ctx;
            double w = this.w, h = this.h;
            double t = this.t * 0.11;
            // position on the circle and the heading of its tangent (the way it flies)
            double cx = w * 0.66 + Math.Cos(t) * w * 0.1;
            double cy = h * 0.13 + Math.Sin(t) * h * 0.035;
            // heading on the (horizontal) circle it is flying, not on the screen
            double heading = Math.Atan2(Math.Cos(t), -Math.Sin(t)) + Math.PI / 2;
            double u = Math.Max(1, this.scale) * 1.15;
            // the bank: the outer wing reads a little longer, the inner shorter
            double bank = Math.Sin(t * 2) * 0.12;
            // a short bout of slow wingbeats every ~11 s; otherwise a fixed glide
            double bout = (this.t % 11) / 11;
            bool flapping = bout < 0.12;
            double beat = flapping ? Math.Sin((bout / 0.12) * Math.PI * 4) : 0;
            double spanK = 1 - Math.Abs(beat) * 0.28;

            ctx.Save();
            ctx.Translate(cx, cy);
            // we see it from far below and to the side, so its circle is foreshortened:
            // flying across the view the wings close up toward a line, flying toward
            // or away they spread to their full span
            ctx.Scale(1, 0.5);
            ctx.Rotate(heading);
            const string dark = "oklch(0.2 0.025 45 / 0.92)";
            const string coverts = "oklch(0.26 0.035 55 / 0.92)";

            void wing(double side)
            {
                double k = spanK * (1 + side * bank);
                double X(double v) { return side * v * u * k; }
                // leading edge: shoulder, forward to the wrist, then the outer hand
                ctx.BeginPath();
                ctx.MoveTo(X(1.6), -2.2 * u);
                ctx.QuadraticCurveTo(X(6), -3.4 * u, X(10.5), -3.9 * u + beat * 1.2 * u);
                ctx.QuadraticCurveTo(X(14), -3.4 * u, X(16.5), -2.6 * u);
                // the primaries: seven fingers, slotted, the middle ones longest
                const int fingers = 7;
                for (int f = 0; f < fingers; f++)
                {
                    double q = (double)f / (fingers - 1);
                    double baseY = -2.6 + q * 4.6;
                    double len = 4.2 + Math.Sin(q * Math.PI) * 2.2;
                    double tipX = 16.5 + len;
                    double tipY = baseY - 0.4 + q * 1.2;
                    ctx.LineTo(X(16.5 + len * 0.55), (baseY - 0.35) * u);
                    ctx.QuadraticCurveTo(X(tipX + 0.3), (tipY - 0.2) * u, X(tipX), tipY * u);
                    ctx.QuadraticCurveTo(X(tipX - 0.2), (tipY + 0.45) * u, X(16.5 + len * 0.5), (baseY + 0.55) * u);
                    ctx.LineTo(X(16.6), (baseY + 0.7) * u);
                }
                // trailing edge: the bulge of the long secondaries, back in to the body
                ctx.QuadraticCurveTo(X(13), 3.4 * u, X(9), 4.2 * u);
                ctx.QuadraticCurveTo(X(4.5), 4.8 * u, X(1.4), 3 * u);
                ctx.ClosePath();
                ctx.FillStyle = dark;
                ctx.Fill();
                // the coverts: a darker-warm band along the front of the wing
                ctx.FillStyle = coverts;
                ctx.BeginPath();
                ctx.MoveTo(X(1.6), -2.2 * u);
                ctx.QuadraticCurveTo(X(6), -3.4 * u, X(10.5), -3.9 * u + beat * 1.2 * u);
                ctx.QuadraticCurveTo(X(13.5), -3.3 * u, X(15.5), -2.4 * u);
                ctx.QuadraticCurveTo(X(10), -0.6 * u, X(1.6), 0.2 * u);
                ctx.ClosePath();
                ctx.Fill();
                // the backlit trailing edge: thin flight feathers with the sun through them
                ctx.StrokeStyle = "rgba(255, 196, 130, 0.38)";
                ctx.LineWidth = Math.Max(0.8, 0.9 * u);
                ctx.BeginPath();
                ctx.MoveTo(X(16), 1.9 * u);
                ctx.QuadraticCurveTo(X(13), 3.2 * u, X(9), 4 * u);
                ctx.QuadraticCurveTo(X(4.5), 4.6 * u, X(1.8), 2.9 * u);
                ctx.Stroke();
            }
            wing(-1);
            wing(1);

            // tail: long, fanned, rounded at the end, the tip translucent
            ctx.FillStyle = dark;
            ctx.BeginPath();
            ctx.MoveTo(-1.3 * u, 4 * u);
            ctx.QuadraticCurveTo(-3.6 * u, 8.5 * u, -3.1 * u, 10.2 * u);
            ctx.QuadraticCurveTo(0, 11.4 * u, 3.1 * u, 10.2 * u);
            ctx.QuadraticCurveTo(3.6 * u, 8.5 * u, 1.3 * u, 4 * u);
            ctx.ClosePath();
            ctx.Fill();
            ctx.StrokeStyle = "rgba(255, 196, 130, 0.3)";
            ctx.LineWidth = Math.Max(0.8, 0.8 * u);
            ctx.BeginPath();
            ctx.MoveTo(-3 * u, 10 * u);
            ctx.QuadraticCurveTo(0, 11.2 * u, 3 * u, 10 * u);
            ctx.Stroke();
            // body: heavy chest tapering to the tail
            ctx.FillStyle = dark;
            ctx.BeginPath();
            ctx.Ellipse(0, 0.6 * u, 2.3 * u, 4.6 * u, 0, 0, Math.PI * 2);
            ctx.Fill();
            // head and neck projecting well ahead of the wings, with the hooked bill
            ctx.BeginPath();
            ctx.Ellipse(0, -4.6 * u, 1.35 * u, 1.9 * u, 0, 0, Math.PI * 2);
            ctx.Fill();
            ctx.BeginPath();
            ctx.MoveTo(-0.55 * u, -6.1 * u);
            ctx.QuadraticCurveTo(0, -7.6 * u, 0.55 * u, -6.1 * u);
            ctx.ClosePath();
            ctx.Fill();
            // golden nape catching the light
            ctx.FillStyle = "rgba(220, 160, 90, 0.35)";
            ctx.BeginPath();
            ctx.Ellipse(0, -3.9 * u, 1.1 * u, 0.9 * u, 0, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Restore();
        }

        /// <summary>
        /// Dust hanging in the low sun. Only the motes near the disc catch enough
        /// light to show; the rest of the sky stays clean. Drawn additively and
        /// tiny, so they read as air rather than as particles.
        /// </summary>
        private void drawMotes()
        {
            if (this.lowQuality) return;
            var ctx = this.ctx;
            double w = this.w, h = this.h;
            var sun = this.sunScreen();
            double storm =
                (this.zeus.state == "gone" ? 1 : 0.2) *
                Math.Max(0, Math.Min(1, this.st.light.strength)) *
                (this.st.elev < 20 ? 1 : 0.4);
            ctx.Save();
            ctx.GlobalCompositeOperation = Composite.Lighter;
            foreach (var m in this.motes)
            {
                double drift = this.t * (6 + m.z * 10);
                double x = (m.fx * w + drift - this.camX * this.scale * m.z * 0.15) % (w + 40);
                if (x < -20) x += w + 40;
                x -= 20;
                double y = m.fy * h + Math.Sin(this.t * 0.5 + m.ph) * 8 * m.z;
                double d = Js.hypot(x - sun.x, (y - sun.y) * 1.4) / (w * 0.32);
                double a = Math.Exp(-d * d) * 0.38 * storm * (0.6 + 0.4 * Math.Sin(this.t * 1.7 + m.ph));
                if (a < 0.02) continue;
                ctx.FillStyle = Col.Rgba(255, 236, 196, Css.fixedNum(a, 3));
                ctx.BeginPath();
                ctx.Arc(x, y, 0.4 + m.z * 0.8, 0, Math.PI * 2);
                ctx.Fill();
            }
            ctx.Restore();
        }

        /// <summary>deterministic pseudo-random hash for procedural scatter</summary>
        private double hash(double n)
        {
            double x = Math.Sin(n * 127.1 + 311.7) * 43758.5453;
            return x - Math.Floor(x);
        }

        /// <summary>2D deterministic hash for stable per-position variation</summary>
        private double hash2(double x, double y)
        {
            double n = Math.Sin(x * 127.1 + y * 311.7 + 74.7) * 43758.5453;
            return n - Math.Floor(n);
        }

        /// <summary>the hillside's surface texture, as a world-anchored repeating pattern</summary>
        private Pattern groundPat = null;
        private double groundPatDpr = 0;

        /// <summary>
        /// The face of the hill: a seamless tile of real-looking dirt — mottled earth,
        /// grit, gravel and stones each lit on the sun's side, dry grass litter, cracks
        /// in the crust — pinned to the world so it scrolls with the hill. Over that:
        /// the light. The low sun only grazes the top of the slope, so a warm band runs
        /// just under the skyline and the face drops off into shade below it.
        /// </summary>
        private void drawGroundDetail(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var ctx = this.ctx;
            var L = this.level;
            double s = this.scale;
            double w = this.w, h = this.h;

            const double TILE = 512;
            if (this.groundPat == null || this.groundPatDpr != this.dpr)
            {
                var tex = Textures.groundTexture(TILE * this.dpr);
                this.groundPat = ctx.CreatePattern(tex, "repeat");
                this.groundPatDpr = this.dpr;
            }
            var pat = this.groundPat;
            if (pat != null && !this.lowQuality)
            {
                double ox = -((this.camX * s) % TILE);
                double oy = (this.groundY + this.camY * s * 0.55 + this.shakeY) % TILE;
                // PORT: Pattern has no setTransform. The TS gives the pattern the matrix
                // translate(ox, oy) scale(1/dpr) and fills (0, 0, w, h); the same pixels come
                // from applying that matrix to the context and filling the rectangle mapped
                // back through its inverse.
                ctx.FillStyle = pat;
                ctx.GlobalAlpha = 0.9;
                ctx.Save();
                ctx.Translate(ox, oy);
                ctx.Scale(1 / this.dpr, 1 / this.dpr);
                ctx.FillRect(-ox * this.dpr, -oy * this.dpr, w * this.dpr, h * this.dpr);
                ctx.Restore();
                ctx.GlobalAlpha = 1;
            }

            // the light: a warm band under the skyline, then shade falling away below
            void line()
            {
                ctx.BeginPath();
                for (double px = -20; px <= w + 20; px += 16)
                {
                    double y = toScreenY(Levels.terrainAt(L, this.camX + px / s));
                    if (px == -20) ctx.MoveTo(px, y);
                    else ctx.LineTo(px, y);
                }
            }
            ctx.LineJoin = LineJoin.Round;
            ctx.LineCap = LineCap.Round;
            for (int pass = 0; pass < 2; pass++)
            {
                double lw = pass == 0 ? 120 : 30;
                double a = pass == 0 ? 0.07 : 0.12;
                ctx.StrokeStyle = Sky.rgb(this.st.light.color, a * Math.Min(1, this.st.light.strength * 1.2));
                ctx.LineWidth = lw * s;
                line();
                ctx.Stroke();
            }
        }

        /// <summary>
        /// Rock breaking through the surface. The skyline is where all the contrast in
        /// the frame is, so a notch in it is worth more than any amount of texture
        /// painted on the shadowed face below. Each outcrop is a small angular block of
        /// bedrock, tilted to a bedding angle; the lit top edge is the only bright mark.
        /// </summary>
        private void drawOutcrops(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var ctx = this.ctx;
            var L = this.level;
            double s = this.scale;
            const double step = 96;
            double left = this.camX - step;
            double right = this.camX + this.w / s + step;
            const int n = 9;
            var topX = new double[n + 1];
            var topY = new double[n + 1];

            for (double wx = Math.Floor(left / step) * step; wx < right; wx += step)
            {
                double h1 = this.hash(wx * 0.83 + 13);
                if (h1 < 0.44) continue;
                double h2 = this.hash(wx * 1.71 + 27);
                double h3 = this.hash(wx * 3.19 + 41);

                double ox = wx + (h1 - 0.5) * step * 0.7;
                double x = toScreenX(ox);
                double y = toScreenY(Levels.terrainAt(L, ox));
                double wide = (10 + h2 * 22) * s;
                double rise = (6 + h3 * 17) * s;
                double tilt = (h2 - 0.5) * 0.5;
                // the local slope, so a block sits along the hill instead of on a shelf
                double lean = Math.Atan(Levels.slopeAt(L, ox) * this.scale * 0.55) * -1;

                ctx.Save();
                ctx.Translate(x, y + 1);
                ctx.Rotate(lean + tilt * 0.35);

                // an irregular, weathered block: jagged top, flat-ish base in the soil
                for (int k = 0; k <= n; k++)
                {
                    double t = (double)k / n;
                    double jag = this.hash(wx * 0.37 + k * 2.3);
                    double hump = Math.Pow(Math.Sin(t * Math.PI), 0.6);
                    topX[k] = -wide + t * wide * 2;
                    topY[k] = -rise * hump * (0.7 + jag * 0.4) + (jag - 0.5) * 2 * s;
                }
                ctx.FillStyle = "oklch(0.2 0.025 48)";
                ctx.BeginPath();
                ctx.MoveTo(-wide * 1.05, rise * 0.55);
                for (int k = 0; k <= n; k++) ctx.LineTo(topX[k], topY[k]);
                ctx.LineTo(wide * 1.05, rise * 0.55);
                ctx.ClosePath();
                ctx.Fill();
                // a fracture face catching the light, and a dark joint through the block
                ctx.FillStyle = "oklch(0.3 0.03 55 / 0.7)";
                ctx.BeginPath();
                ctx.MoveTo(topX[5], topY[5]);
                ctx.LineTo(topX[6], topY[6]);
                ctx.LineTo(topX[7], topY[7]);
                ctx.LineTo(topX[6] - wide * 0.1, rise * 0.1);
                ctx.ClosePath();
                ctx.Fill();
                ctx.StrokeStyle = "oklch(0.1 0.02 45 / 0.6)";
                ctx.LineWidth = Math.Max(0.7, 0.9 * s);
                ctx.BeginPath();
                ctx.MoveTo(topX[3], topY[3]);
                ctx.LineTo(topX[3] + wide * 0.08, rise * 0.2);
                ctx.Stroke();

                // the hairline the low sun leaves along the top
                ctx.StrokeStyle = "oklch(0.78 0.08 66 / 0.6)";
                ctx.LineWidth = Math.Max(0.7, 1.1 * s);
                ctx.LineJoin = LineJoin.Round;
                ctx.BeginPath();
                for (int k = 2; k <= n; k++)
                {
                    if (k == 2) ctx.MoveTo(topX[k], topY[k]);
                    else ctx.LineTo(topX[k], topY[k]);
                }
                ctx.Stroke();
                ctx.Restore();
            }
        }

        private void drawStones(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var ctx = this.ctx;
            double s = this.scale;
            const double step = 44;
            double startWx = Math.Floor(this.camX / step) * step;
            double endWx = this.camX + this.w / this.scale;
            for (double wx = startWx; wx < endWx; wx += step)
            {
                double h1 = this.hash(wx);
                if (h1 < 0.42) continue;
                double px = toScreenX(wx + (h1 - 0.5) * step);
                double py = toScreenY(Levels.terrainAt(this.level, wx)) + 1;
                double sz = (0.5 + ((h1 * 7919) % 100) / 100) * 3.4 * s;
                // soft cast shadow on the dirt
                ctx.FillStyle = "rgba(0,0,0,0.18)";
                ctx.BeginPath();
                ctx.Ellipse(px + sz * 0.25, py + sz * 0.18, sz * 1.05, sz * 0.6, 0, 0, Math.PI * 2);
                ctx.Fill();
                ctx.FillStyle = Col.Oklch(0.3, 0.025, 60, 0.55 + h1 * 0.3);
                ctx.BeginPath();
                ctx.Ellipse(px, py, sz, sz * 0.62, (h1 - 0.5) * 0.6, 0, Math.PI * 2);
                ctx.Fill();
                // sunlit top edge
                ctx.FillStyle = "oklch(0.52 0.03 68 / 0.5)";
                ctx.BeginPath();
                ctx.Ellipse(px - sz * 0.15, py - sz * 0.3, sz * 0.42, sz * 0.2, 0, 0, Math.PI * 2);
                ctx.Fill();
                // moss speck on the shadowed side
                if (h1 > 0.7)
                {
                    ctx.FillStyle = "oklch(0.45 0.05 130 / 0.35)";
                    ctx.BeginPath();
                    ctx.Ellipse(px + sz * 0.3, py + sz * 0.2, sz * 0.28, sz * 0.14, 0.5, 0, Math.PI * 2);
                    ctx.Fill();
                }
            }
        }

        /// <summary>in the void, the stone breathes out pale wisps — old voices, rising</summary>
        private void drawWhispers(double bx, double by, double R)
        {
            if (this.saga.stage < 2 || this.phase != "playing" || this.lowQuality) return;
            var ctx = this.ctx;
            double s = this.scale;
            ctx.Save();
            for (int i = 0; i < 4; i++)
            {
                double p = (this.t * 0.18 + i * 0.25) % 1;
                double x = bx + (this.hash(i * 4.1) - 0.5) * R * 1.2 + Math.Sin(this.t * 0.8 + i) * 6 * s;
                double y = by - R * 0.4 - p * 60 * s;
                double a = Math.Sin(p * Math.PI) * (this.saga.stage == 3 ? 0.16 : 0.12);
                var g = ctx.CreateRadialGradient(x, y, 0, x, y, 9 * s);
                bool gold = this.saga.stage == 3;
                double cr = gold ? 255 : 205, cg = gold ? 226 : 215, cb = gold ? 170 : 240;
                g.AddColorStop(0, Col.Rgba(cr, cg, cb, Css.fixedNum(a, 3)));
                g.AddColorStop(1, Col.Rgba(cr, cg, cb, 0));
                ctx.FillStyle = g;
                ctx.FillRect(x - 10 * s, y - 10 * s, 20 * s, 20 * s);
            }
            ctx.Restore();
        }

        /// <summary>
        /// Colour. Every grade, overcast and wet tint laid over the land pulls it a
        /// little toward grey, and on a rainy or late climb they add up until the
        /// world looks washed out. One pass in the "saturation" blend gives back
        /// what they took — more under grey weather, less at night, when the colour
        /// should drain.
        /// </summary>
        private void vibrance(Ctx ctx)
        {
            if (this.lowQuality) return;
            double night = Math.Max(0, Math.Min(1, (-this.st.elev + 1) / 9));
            double grey = (this.weather == "rain" ? 0.8 : this.weather == "storm" ? 1 : 0) * this.wxFade;
            double a = (0.08 + grey * 0.06 + this.st.gradeA * 0.15) * (1 - night * 0.75);
            // on a clear day the colour is all there already: the pass costs a whole
            // screen and would change almost nothing
            if (a < 0.1) return;
            ctx.Save();
            ctx.GlobalCompositeOperation = Composite.Saturation;
            ctx.GlobalAlpha = Math.Min(0.3, a);
            ctx.FillStyle = "rgb(255, 0, 0)";
            ctx.FillRect(0, 0, this.w, this.h);
            ctx.Restore();
        }

        /// <summary>the light the others on the hill are painted in</summary>
        private FigLight npcLight()
        {
            var l = this.lightScreen();
            // toward the light from where the figures stand, roughly mid-screen low
            double lx = l.x - this.w * 0.5;
            double ly = l.y - this.h * 0.6;
            double m = Js.hypot(lx, ly);
            if (m == 0 || double.IsNaN(m)) m = 1;
            lx /= m;
            ly /= m;
            return new FigLight
            {
                color = l.color,
                strength = l.strength,
                lx = lx,
                ly = ly,
                t = this.t,
                dim = Math.Max(0, Math.Min(0.6, (2 - this.st.elev) / 13)),
            };
        }

        /// <summary>a point on screen (the TS's anonymous <c>{ x, y }</c> in npcPos)</summary>
        public class NpcPosT
        {
            public double x, y;
            public NpcPosT() { }
            public NpcPosT(double x, double y) { this.x = x; this.y = y; }
        }

        /// <summary>set by the interface while one of them is speaking</summary>
        public string speaker = null;
        /// <summary>the eagle's daily visit to Prometheus, and whether it has come this climb</summary>
        private EagleVisit eagle = new EagleVisit("away", 0);
        private bool eagleDone = false;
        /// <summary>where Prometheus is looking, eased toward where he wants to</summary>
        // PORT: the TS's inline { turn, tilt } is the ProGaze class drawPrometheus takes.
        private ProGaze proGaze = new ProGaze(1, 0);
        /// <summary>the man's head on screen last frame, for the others to look at</summary>
        private double headX = 0;
        /// <summary>Merope walking away at the end, 0-1</summary>
        private double meropeLeave = 0;
        private bool meropeLeaving = false;
        /// <summary>where the others are on screen this frame, for the camera in a scene (absent key = undefined)</summary>
        private Dictionary<string, NpcPosT> npcPos = new Dictionary<string, NpcPosT>();

        /// <summary>how Tantalus is, from where his story has got to</summary>
        private TanLook tanLook()
        {
            double t = this.saga.tanPost;
            bool mud = t == 16 || t == 17;
            return new TanLook
            {
                still = t >= 1,
                stand = t >= 12 && !mud,
                mud = mud,
                back = t == 18 || t == 19,
                crown = t < 20,
                sprout = t >= 23,
            };
        }

        /// <summary>Merope, Tantalus in his pool, Prometheus on his rock</summary>
        private void drawNpcs(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var L = this.level;
            double s = this.scale;
            var ctx = this.ctx;
            var Lt = this.npcLight();
            NpcPosT at(double f)
            {
                double wx = L.length * f;
                double sx = toScreenX(wx);
                return sx > -120 && sx < this.w + 120
                    ? new NpcPosT(sx, toScreenY(Levels.terrainAt(L, wx)) - 5 * s)
                    : null;
            }
            this.npcPos.Clear();
            var tan = at(NPC_AT.tan);
            if (tan != null) this.npcPos["tan"] = tan;
            if (tan != null) Figures.drawTantalus(ctx, tan.x, tan.y + 3 * s, s * 1.3, Lt, this.tanLook());
            // what is drawn follows what has been said: the chains, the eagle, where he sits
            double tp = this.saga.proPost;
            bool below = this.saga.proAt != NPC_AT.pro;
            if (below)
            {
                var rock = at(NPC_AT.pro);
                if (rock != null) Figures.drawEmptyRock(ctx, rock.x + 18 * s, rock.y, s * 1.3, Lt);
            }
            var pro = at(this.saga.proAt);
            if (pro != null) this.npcPos["pro"] = new NpcPosT(pro.x + 30 * s, pro.y - 30 * s);
            if (pro != null && below && tp >= 11)
            {
                // the fire he keeps for the shades; out the night he put it out, small
                // when it comes back by itself
                double size = tp == 17 ? 0 : tp == 18 ? 0.45 : 1;
                Figures.drawPathFire(ctx, pro.x - 22 * s, pro.y + 3 * s, s * 1.3, this.t, size);
            }
            if (pro != null)
            {
                // the eagle comes once a climb, when he is in sight — until the day it doesn't
                if (
                    this.eagle.phase == "away" &&
                    !this.eagleDone &&
                    this.phase == "playing" &&
                    this.saga.eagleComes)
                {
                    this.eagle = new EagleVisit("arrive", 0);
                    this.eagleDone = true;
                }
                // his eyes follow the man going past; now and then they go up to the sky
                double phx = pro.x + 10 * s;
                double phy = pro.y - 99 * s;
                double dx = this.headX - phx;
                double dy = phy - this.headY;
                double slot = Math.Floor(this.t / 9);
                bool glancing = this.t % 9 < 1.8 && this.hash(slot * 7.3) > 0.45;
                double wantTurn, wantTilt;
                if (glancing)
                {
                    wantTurn = this.hash(slot * 3.1) > 0.5 ? 1 : -1;
                    wantTilt = 0.5;
                }
                else
                {
                    wantTurn = dx < -8 * s ? 1 : -1;
                    wantTilt = Math.Max(-0.3, Math.Min(0.45, Math.Atan2(dy, Math.Abs(dx) + 40 * s) * 0.7));
                }
                double k = Math.Min(1, this.frameDt * 3.2);
                this.proGaze.turn += (wantTurn - this.proGaze.turn) * k;
                this.proGaze.tilt += (wantTilt - this.proGaze.tilt) * Math.Min(1, this.frameDt * 2.5);
                var look = new ProLook(
                    below ? 1 : tp >= 6 ? 2 : tp >= 5 ? 1 : 0,
                    tp >= 3,
                    !below);
                Figures.drawPrometheus(ctx, pro.x + 18 * s, pro.y, s * 1.3, Lt, this.eagle, this.proGaze, look);
            }
            var me = at(NPC_AT.merope);
            if (me != null) this.npcPos["merope"] = new NpcPosT(me.x, me.y - 40 * s);
            double leaving = this.meropeLeaving ? this.meropeLeave : 0;
            bool visible = this.saga.meropeHere || (this.saga.s.finale && leaving < 1);
            if (me != null && visible)
            {
                int act = this.saga.stage;
                Figures.drawMerope(ctx, me.x, me.y + 4 * s, s * 1.15, new MeropeOpts
                {
                    act = act,
                    turned = this.saga.meropeTurned,
                    talking = this.speaker == "merope",
                    leave = leaving,
                    L = Lt,
                });
            }
        }

        /// <summary>the echoes of earlier slips on this hill: pale, still, flickering</summary>
        private void drawEchoes(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (this.ghosts.Count == 0 || this.lowQuality) return;
            var ctx = this.ctx;
            var L = this.level;
            ctx.Save();
            for (int i = 0; i < this.ghosts.Count; i++)
            {
                var g = this.ghosts[i];
                if (g.lv != L.id) continue;
                double sx = toScreenX(g.wx);
                if (sx < -200 || sx > this.w + 200) continue;
                // fainter the older it is; it fades as he comes close, like a memory
                double near = Math.Min(1, Math.Abs(toScreenX(this.x - 64) - sx) / (160 * this.scale));
                double fl = 0.75 + Math.Sin(this.t * 2.3 + i * 1.7) * 0.25;
                ctx.GlobalAlpha = (0.08 + 0.1 * ((double)(i + 1) / this.ghosts.Count)) * fl * near;
                double sy = toScreenY(Levels.terrainAt(L, g.wx));
                ctx.DrawImage(g.cv, sx + g.ox, sy + g.oy, g.w, g.h);
            }
            ctx.Restore();
        }

        /// <summary>the wedge a companion laid in the path</summary>
        private void drawWedge(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (!this.saga.s.wedge && this.wedgeT > 1.2) return;
            if (this.phase != "playing") return;
            double wx = this.wedgeX();
            double x = toScreenX(wx);
            if (x < -40 || x > this.w + 40) return;
            double s = this.scale;
            double y = toScreenY(Levels.terrainAt(this.level, wx));
            var ctx = this.ctx;
            ctx.Save();
            ctx.FillStyle = "oklch(0.38 0.03 50)";
            ctx.BeginPath();
            ctx.MoveTo(x - 9 * s, y + 1 * s);
            ctx.LineTo(x - 1 * s, y - 9 * s);
            ctx.LineTo(x + 3 * s, y + 1 * s);
            ctx.ClosePath();
            ctx.Fill();
            // a faint ember glow while it waits, a flash when it holds
            double flash = Math.Max(0, 1 - this.wedgeT / 1.2);
            double a = (this.saga.s.wedge ? 0.25 + Math.Sin(this.t * 2) * 0.1 : 0) + flash * 0.7;
            var g = ctx.CreateRadialGradient(x - 2 * s, y - 3 * s, 0, x - 2 * s, y - 3 * s, 16 * s);
            g.AddColorStop(0, Col.Rgba(255, 190, 110, Css.fixedNum(a, 3)));
            g.AddColorStop(1, "rgba(255, 190, 110, 0)");
            ctx.FillStyle = g;
            ctx.FillRect(x - 20 * s, y - 20 * s, 40 * s, 40 * s);
            ctx.Restore();
        }

        /// <summary>
        /// The stone remembers. Every summit leaves a scar — every time the god threw
        /// it down — so an old stone is a map of its falls. Once he has made the climb
        /// his own, the cracks glow from inside. Drawn in the stone's own turning frame,
        /// clipped to its outline.
        /// </summary>
        private void drawStoneMemory(Ctx ctx, double R)
        {
            double n = Math.Min(48, this.cycles);
            double s = this.scale;
            int stage = this.saga.stage;
            double glow = this.saga.s.ended ? 1 : stage == 3 ? 0.55 : 0;
            bool pain = this.saga.s.burden == "pro";
            ctx.Save();
            ctx.LineCap = LineCap.Round;
            var ptsX = new double[8];
            var ptsY = new double[8];
            for (int i = 0; i < n; i++)
            {
                double a0 = this.hash(i * 7.31 + 0.2) * Math.PI * 2;
                double r0 = R * (0.15 + this.hash(i * 3.17) * 0.75);
                double px = Math.Cos(a0) * r0;
                double py = Math.Sin(a0) * r0;
                int segs = 2 + (int)Math.Floor(this.hash(i * 5.5) * 3);
                // older scars are worn shallow; the newest are sharp
                double age = (n - i) / Math.Max(1, n);
                double depth = 0.18 + (1 - age) * 0.32;
                int np = 0;
                ptsX[np] = px; ptsY[np] = py; np++;
                double dir = this.hash(i * 9.1) * Math.PI * 2;
                for (int k = 0; k < segs; k++)
                {
                    dir += (this.hash(i * 11 + k) - 0.5) * 1.4;
                    double len = R * (0.08 + this.hash(i * 13 + k) * 0.14);
                    px += Math.Cos(dir) * len;
                    py += Math.Sin(dir) * len;
                    ptsX[np] = px; ptsY[np] = py; np++;
                }
                void path()
                {
                    ctx.BeginPath();
                    ctx.MoveTo(ptsX[0], ptsY[0]);
                    for (int j = 1; j < np; j++) ctx.LineTo(ptsX[j], ptsY[j]);
                }
                path();
                ctx.StrokeStyle = Col.Rgba(14, 10, 12, Css.fixedNum(depth, 3));
                ctx.LineWidth = (0.6 + (1 - age) * 0.8) * s;
                ctx.Stroke();
                if (glow > 0 || pain)
                {
                    path();
                    double pulse = 0.7 + Math.Sin(this.t * 1.6 + i) * 0.3;
                    ctx.StrokeStyle = pain
                        ? Col.Rgba(255, 90, 40, Css.fixedNum(0.55 * pulse, 3))
                        : Col.Rgba(255, 214, 140, Css.fixedNum(0.5 * glow * pulse, 3));
                    ctx.LineWidth = 0.6 * s;
                    ctx.Stroke();
                }
            }
            ctx.Restore();
        }

        /// <summary>
        /// A stele at the top of every ledge. Dark stone until the boulder has gone
        /// over it; after that a small flame burns on it, so the way back down shows
        /// how much of the hill has been won this cycle.
        /// </summary>
        private void drawSteles(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var ledges = this.level.ledges;
            if (ledges == null) return;
            var ctx = this.ctx;
            double s = this.scale;
            foreach (var ledge in ledges)
            {
                double lx = ledge.Item1;
                double wx = lx + 46;
                double sx = toScreenX(wx);
                if (sx < -40 || sx > this.w + 40) continue;
                double sy = toScreenY(Levels.terrainAt(this.level, wx)) + 3 * s;
                double sw = 10 * s;
                double sh = 50 * s;
                // shaft, slightly tapered, with a capital
                ctx.FillStyle = "oklch(0.24 0.03 48)";
                ctx.BeginPath();
                ctx.MoveTo(sx - sw / 2, sy);
                ctx.LineTo(sx - sw * 0.4, sy - sh);
                ctx.LineTo(sx + sw * 0.4, sy - sh);
                ctx.LineTo(sx + sw / 2, sy);
                ctx.ClosePath();
                ctx.Fill();
                ctx.FillRect(sx - sw * 0.75, sy - sh - 4 * s, sw * 1.5, 4 * s);
                // sunlit edge
                ctx.FillStyle = "oklch(0.55 0.06 60 / 0.6)";
                ctx.FillRect(sx + sw * 0.28, sy - sh, 1.4 * s, sh);

                bool lit = this.checkpoint > lx && this.phase == "playing";
                if (!lit) continue;
                double fy = sy - sh - 6 * s;
                double flare = Math.Max(0, 1 - this.checkpointT / 0.8);
                double flick = 1 + Math.Sin(this.t * 13 + lx) * 0.12 + Math.Sin(this.t * 7.3) * 0.08;
                double r = (14 + flare * 30) * s * flick;
                var glow = ctx.CreateRadialGradient(sx, fy, 0, sx, fy, r);
                glow.AddColorStop(0, Col.Oklch(0.92, 0.13, 80, 0.55 + flare * 0.4));
                glow.AddColorStop(1, "oklch(0.8 0.14 60 / 0)");
                ctx.FillStyle = glow;
                ctx.BeginPath();
                ctx.Arc(sx, fy, r, 0, Math.PI * 2);
                ctx.Fill();
                ctx.FillStyle = "oklch(0.95 0.1 85)";
                ctx.BeginPath();
                ctx.Ellipse(sx, fy - 2 * s * flick, 2.2 * s, 4.2 * s * flick, 0, 0, Math.PI * 2);
                ctx.Fill();
            }
        }

        static readonly double[] DASH_OUT = { 1, 6 };
        static readonly double[] DASH_LOW = { 6, 5 };

        /// <summary>
        /// The breath ring over his head, and the beat closing in on it.
        ///
        /// The outer arc is his breath, gold going to ember red as it runs out. A
        /// second, fainter circle shrinks onto it once per beat: press as they meet
        /// and the heave lands. It fades away whenever there is nothing to say — full
        /// breath and no push — so a player standing still sees an empty sky.
        /// </summary>
        private void drawBreathRing(double cx, double cy)
        {
            var ctx = this.ctx;
            bool show =
                this.active &&
                this.phase == "playing" &&
                (this.breath < 0.98 || this.input > 0.15 || this.slipT > 0 || this.denyT < 0.6);
            this.hudA += ((show ? 1 : 0) - this.hudA) * Math.Min(1, this.frameDt * 6);
            if (this.hudA < 0.02) return;
            // the bold ring: larger, heavier, and told apart by shape as well as by
            // colour (dashed when breath is low, dotted when it has run out), for
            // anyone who cannot easily tell amber from red
            bool bold = this.boldRing;
            double lw = bold ? 1.9 : 1;
            double r = Math.Max(9, 12 * this.scale) * (bold ? 1.35 : 1);
            ctx.Save();
            ctx.GlobalAlpha = this.hudA;
            ctx.LineCap = LineCap.Round;

            // track
            ctx.StrokeStyle = bold ? "oklch(0.12 0.02 40 / 0.75)" : "oklch(0.2 0.02 40 / 0.45)";
            ctx.LineWidth = 3.2 * lw;
            ctx.BeginPath();
            ctx.Arc(cx, cy, r, 0, Math.PI * 2);
            ctx.Stroke();

            // breath
            bool low = this.breath < 0.3;
            bool deny = this.denyT < 0.6 && Math.Sin(this.denyT * 40) > 0;
            ctx.StrokeStyle =
                this.slipT > 0 || deny
                    ? (Style)"oklch(0.62 0.19 28)"
                    : low
                        ? (Style)Col.Oklch(0.7, 0.16, 40, 0.7 + Math.Sin(this.t * 12) * 0.3)
                        : (Style)"oklch(0.88 0.11 82)";
            if (bold)
            {
                ctx.StrokeStyle =
                    this.slipT > 0 || deny ? "#ffffff" : low ? "oklch(0.95 0.02 90)" : "oklch(0.95 0.06 210)";
                ctx.SetLineDash(this.slipT > 0 || deny ? DASH_OUT : low ? DASH_LOW : null);
            }
            ctx.LineWidth = 2.6 * lw;
            if (this.breath > 0.001)
            {
                ctx.BeginPath();
                ctx.Arc(cx, cy, r, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * this.breath);
                ctx.Stroke();
            }
            ctx.SetLineDash(null);
            // launch threshold tick, so it is visible whether a launch is affordable
            double ta = -Math.PI / 2 + Math.PI * 2 * KICK_COST;
            ctx.StrokeStyle = "oklch(0.95 0.02 80 / 0.7)";
            ctx.LineWidth = 1.2;
            ctx.BeginPath();
            ctx.MoveTo(cx + Math.Cos(ta) * (r - 3), cy + Math.Sin(ta) * (r - 3));
            ctx.LineTo(cx + Math.Cos(ta) * (r + 3), cy + Math.Sin(ta) * (r + 3));
            ctx.Stroke();

            // the beat: a circle closing in onto the ring
            if (this.kickT < 0 && this.slipT <= 0)
            {
                double toBeat = (BEAT - (this.t % BEAT)) / BEAT; // 1 -> 0 as the beat lands
                double near = 1 - toBeat;
                ctx.StrokeStyle = Col.Oklch(0.96, 0.03, 85, 0.15 + near * (bold ? 0.8 : 0.55));
                ctx.LineWidth = 1.4 * lw;
                ctx.BeginPath();
                ctx.Arc(cx, cy, r + 3 + toBeat * 16, 0, Math.PI * 2);
                ctx.Stroke();
            }

            // the last heave, as a ring thrown outward
            var fx = this.heaveFx;
            if (fx != null && fx.q != "miss" && fx.t < 0.45)
            {
                double k = fx.t / 0.45;
                ctx.GlobalAlpha = this.hudA * (1 - k);
                ctx.StrokeStyle = fx.q == "perfect" ? "oklch(0.9 0.14 82)" : "oklch(0.92 0.03 85)";
                ctx.LineWidth = fx.q == "perfect" ? 2.4 : 1.4;
                ctx.BeginPath();
                ctx.Arc(cx, cy, r + 4 + k * (fx.q == "perfect" ? 22 : 12), 0, Math.PI * 2);
                ctx.Stroke();
            }

            // the streak: "×4" beside the ring, swelling as it grows, fading after a break
            if (this.streakShown >= 2 && this.streakT < 1.6)
            {
                bool alive = this.streak >= 2;
                double pop = Math.Max(0, 1 - this.streakT / 0.25);
                double fade = alive ? 1 : Math.Max(0, 1 - this.streakT / 0.6);
                double size = Js.round(
                    (13 + Math.Min(8, this.streakShown) * 1.2 + pop * 6) * Math.Max(0.8, this.scale));
                ctx.GlobalAlpha = this.hudA * fade;
                ctx.Font = "italic 600 " + Js.str(size) + "px Georgia, 'Times New Roman', serif";
                ctx.TextAlign = TextAlign.Left;
                ctx.TextBaseline = TextBaseline.Middle;
                ctx.ShadowColor = "oklch(0.85 0.14 80 / 0.9)";
                ctx.ShadowBlur = 10 + pop * 10;
                ctx.FillStyle = this.streakShown >= 5 ? "oklch(0.93 0.13 85)" : "oklch(0.95 0.06 85)";
                ctx.FillText("×" + Js.str(this.streakShown), cx + r + 10, cy - 1);
                ctx.ShadowBlur = 0;
            }
            ctx.Restore();
        }

        /// <summary>how much stronger than a fair day the wind is here: the ridge, the storm</summary>
        private double windiness()
        {
            string w = this.weather;
            return 1 + (this.level.wind ?? 0) * 1.6 + (w == "storm" ? 1.3 : w == "rain" ? 0.4 : 0);
        }

        /// <summary>the push a gust gives the stone, back down the hill (world units/s²)</summary>
        private double gustForce()
        {
            double extra = this.windiness() - 1;
            if (extra <= 0) return 0;
            double ramp = 0.35 + 0.65 * this.difficultyRamp();
            return extra * Math.Pow(Math.Max(0, Math.Sin(this.t * 0.23 + 1.3)), 3) * 120 * ramp;
        }

        /// <summary>
        /// How much of the hills' and the weather's bite is felt: a quarter or so on
        /// the early cycles, all of it from the tenth on. The new hills and the first
        /// storms arrive while a player is still learning the rhythm; they should
        /// look the part straight away and only gradually play it.
        /// </summary>
        private double difficultyRamp()
        {
            return Math.Min(1, Math.Max(0, (this.cycles - 2) / 8));
        }

        /// <summary>
        /// The wind. A slow swell with gusts riding on it that travel along the hill
        /// (so neighbouring clumps lean in turn, not in unison). Radians of lean.
        /// </summary>
        private double windAt(double wx)
        {
            double t = this.t;
            double swell = Math.Sin(t * 0.7 + wx * 0.002) * 0.08;
            double gust =
                Math.Pow(Math.Max(0, Math.Sin(t * 0.23 + 1.3)), 3) *
                (0.18 + Math.Sin(t * 2.1 - wx * 0.012) * 0.08) *
                this.windiness();
            double flutter = Math.Sin(t * 3.3 + wx * 0.05) * 0.04;
            return -(swell + gust + flutter);
        }

        private Dictionary<string, TreeSprite> treeCache = new Dictionary<string, TreeSprite>();

        /// <summary>
        /// The sky a weather front brings: grey cloud pulled down over the colour of
        /// the hour. Rain and storm are overcast; fog only veils.
        /// </summary>
        private void drawOvercast()
        {
            // overcast, but the hour's colour still shows through it
            double k = (this.weather == "rain" ? 0.66 : this.weather == "storm" ? 0.78 : 0) * this.wxFade;
            if (k <= 0) return;
            var ctx = this.ctx;
            double w = this.w, h = this.h;
            double night = Math.Max(0, Math.Min(1, (-this.st.elev - 2) / 10));
            double c0 = Js.round(62 * (1 - night * 0.75));
            double c1 = Js.round(68 * (1 - night * 0.75));
            double c2 = Js.round(82 * (1 - night * 0.75));
            var g = ctx.CreateLinearGradient(0, 0, 0, h * this.st.horizon);
            g.AddColorStop(0, Col.Rgba(c0, c1, c2, k));
            g.AddColorStop(1, Col.Rgba(c0 + 30, c1 + 30, c2 + 30, k * 0.82));
            ctx.FillStyle = g;
            ctx.FillRect(0, 0, w, h * this.st.horizon + 2);
            // under a storm, heavy cloud bellies drifting low across the frame
            if (this.weather == "storm" || this.weather == "rain")
            {
                int n = this.lowQuality ? 4 : 7;
                for (int i = 0; i < n; i++)
                {
                    double cx = ((this.hash(i * 3.7) * (w + 600) + this.t * (14 + i * 3)) % (w + 600)) - 300;
                    double cy = h * (0.06 + this.hash(i * 5.1) * 0.22);
                    double r = w * (0.18 + this.hash(i * 2.3) * 0.16);
                    var cg = ctx.CreateRadialGradient(cx, cy, 0, cx, cy, r);
                    cg.AddColorStop(0, Col.Rgba(c0 - 20, c1 - 20, c2 - 20, k * 0.7));
                    cg.AddColorStop(1, Col.Rgba(c0 - 20, c1 - 20, c2 - 20, 0));
                    ctx.FillStyle = cg;
                    ctx.FillRect(cx - r, cy - r, r * 2, r * 2);
                }
            }
        }

        /// <summary>Rain in front of everything: slanting streaks that lean with the wind.</summary>
        private void drawWeather()
        {
            var ctx = this.ctx;
            double w = this.w, h = this.h;
            if (this.weather != "rain" && this.weather != "storm") return;
            bool heavy = this.weather == "storm";
            if (this.saga.winter)
            {
                // winter: in despair the rain turns to snow, drifting and slow
                double sn = Js.round(
                    (heavy ? 260 : 170) * (this.lowQuality ? 0.45 : 1) * (w / 1000) * this.wxFade);
                ctx.Save();
                ctx.FillStyle = "rgba(240, 245, 255, 0.75)";
                for (int i = 0; i < sn; i++)
                {
                    double sp = 50 + this.hash(i * 1.7) * 70;
                    double y = ((this.hash(i * 4.3) * (h + 40) + this.t * sp) % (h + 40)) - 20;
                    double x =
                        ((((this.hash(i * 7.1) * (w + 100) + Math.Sin(this.t * 0.7 + i) * 20 - this.t * 30) %
                            (w + 100)) +
                            w +
                            100) %
                            (w + 100)) -
                        50;
                    double r = (0.8 + this.hash(i * 2.9) * 1.6) * this.scale;
                    ctx.BeginPath();
                    ctx.Arc(x, y, r, 0, Math.PI * 2);
                    ctx.Fill();
                }
                ctx.Restore();
                return;
            }
            double n = Js.round(
                (heavy ? 230 : 150) * (this.lowQuality ? 0.45 : 1) * (w / 1000) * this.wxFade);
            double lean = 0.18 + (this.windiness() - 1) * 0.12 + Math.Max(0, -this.windAt(this.camX)) * 0.6;
            double t = this.t;
            ctx.Save();
            ctx.StrokeStyle = heavy ? "rgba(200, 212, 230, 0.32)" : "rgba(200, 212, 230, 0.26)";
            ctx.LineWidth = Math.Max(1, this.scale);
            ctx.BeginPath();
            for (int i = 0; i < n; i++)
            {
                double sp = 900 + this.hash(i * 1.7) * 500;
                double len = (14 + this.hash(i * 2.9) * 14) * this.scale;
                double y = ((this.hash(i * 4.3) * (h + 60) + t * sp) % (h + 60)) - 30;
                double x =
                    ((((this.hash(i * 7.1) * (w + 200) - y * lean + t * sp * lean * 0.2) % (w + 200)) +
                        w +
                        200) %
                        (w + 200)) -
                    100;
                ctx.MoveTo(x, y);
                ctx.LineTo(x - len * lean, y + len);
            }
            ctx.Stroke();
            ctx.Restore();
        }

        /// <summary>the mud: a black, wet sheen over the trail, pools that catch the sky</summary>
        private void drawMire(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var mm = this.mireSpan();
            if (mm == null) return;
            double m0 = mm.Value.Item1, m1 = mm.Value.Item2;
            var ctx = this.ctx;
            double s = this.scale;
            var L = this.level;
            double x0 = toScreenX(m0);
            double x1 = toScreenX(m1);
            if (x1 < -40 || x0 > this.w + 40) return;
            ctx.Save();
            // the mud itself, thickest in the middle of the stretch
            ctx.BeginPath();
            for (double wx = m0; wx <= m1; wx += 8)
            {
                double edge = Math.Min(1, Math.Min((wx - m0) / 90, (m1 - wx) / 90));
                double y = toScreenY(Levels.terrainAt(L, wx));
                if (wx == m0) ctx.MoveTo(toScreenX(wx), y - 1 * s);
                else ctx.LineTo(toScreenX(wx), y - (1 + edge * 2.5 + Math.Sin(wx * 0.11) * 0.8) * s);
            }
            for (double wx = m1; wx >= m0; wx -= 8)
            {
                double edge = Math.Min(1, Math.Min((wx - m0) / 90, (m1 - wx) / 90));
                ctx.LineTo(toScreenX(wx), toScreenY(Levels.terrainAt(L, wx)) + (6 + edge * 10) * s);
            }
            ctx.ClosePath();
            ctx.FillStyle = "rgba(18, 13, 12, 0.88)";
            ctx.Fill();
            // pools: the sky caught in them, and bubbles that rise and burst
            for (double wx = m0 + 30; wx < m1 - 20; wx += 46)
            {
                double h = this.hash(wx * 0.71);
                double px = toScreenX(wx + h * 20);
                if (px < -30 || px > this.w + 30) continue;
                double py = toScreenY(Levels.terrainAt(L, wx + h * 20)) + 2 * s;
                var sky = this.st.ramp[3];
                ctx.FillStyle = Col.Rgba(Js.ToInt32(sky[0]), Js.ToInt32(sky[1]), Js.ToInt32(sky[2]), 0.28);
                ctx.BeginPath();
                ctx.Ellipse(px, py, (8 + h * 10) * s, (1.4 + h) * s, 0, 0, Math.PI * 2);
                ctx.Fill();
                double b = (((this.t * (0.4 + h * 0.5) + h) % 1) + 1) % 1;
                if (b < 0.8)
                {
                    ctx.StrokeStyle = Col.Rgba(120, 110, 100, Css.fixedNum(0.5 * (1 - b), 3));
                    ctx.LineWidth = 0.7 * s;
                    ctx.BeginPath();
                    ctx.Arc(
                        px + (h - 0.5) * 8 * s,
                        py - b * 1.5 * s,
                        (0.6 + b * 1.6) * s,
                        Math.PI,
                        Math.PI * 2);
                    ctx.Stroke();
                }
            }
            ctx.Restore();
        }

        /// <summary>loose stones strewn over the scree field, so the bad footing shows</summary>
        private void drawScree(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var scN = this.level.scree;
            if (scN == null) return;
            double sc0 = scN.Value.Item1, sc1 = scN.Value.Item2;
            var ctx = this.ctx;
            double s = this.scale;
            ctx.Save();
            for (double wx = sc0; wx < sc1; wx += 6)
            {
                double px = toScreenX(wx);
                if (px < -20 || px > this.w + 20) continue;
                // thinner at the edges of the field
                double edge = Math.Min(1, Math.Min((wx - sc0) / 160, (sc1 - wx) / 160));
                if (this.hash(wx * 0.37) > 0.2 + edge * 0.75) continue;
                double py = toScreenY(Levels.terrainAt(this.level, wx)) + (2 + this.hash(wx * 1.3) * 22) * s;
                double r = (2.4 + this.hash(wx * 2.1) * 4.6) * s;
                double tone = 0.42 + this.hash(wx * 3.3) * 0.2;
                double rot = this.hash(wx) - 0.5;
                ctx.FillStyle = "rgba(14, 9, 6, 0.45)";
                ctx.BeginPath();
                ctx.Ellipse(px - r * 0.3, py + r * 0.45, r * 1.35, r * 0.6, rot, 0, Math.PI * 2);
                ctx.Fill();
                ctx.FillStyle = Col.Oklch(Css.fixedNum(tone, 3), 0.018, 70);
                ctx.BeginPath();
                ctx.Ellipse(px, py, r * 1.3, r * 0.8, rot, 0, Math.PI * 2);
                ctx.Fill();
                ctx.FillStyle = "rgba(255, 236, 200, 0.28)";
                ctx.BeginPath();
                ctx.Ellipse(px + r * 0.25, py - r * 0.32, r * 0.65, r * 0.32, rot, 0, Math.PI * 2);
                ctx.Fill();
            }
            ctx.Restore();
        }

        static readonly string[] NUMERALS = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

        /// <summary>
        /// Keepsakes of a finished chapter of the book, standing on every hill: a
        /// small stele for each chapter near the summit, its numeral cut in it, and
        /// for Rome a line of torches up the path, for China paper lanterns.
        /// </summary>
        private void drawKeepsakes(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var done = this.chaptersDone;
            if (done.Count == 0) return;
            var ctx = this.ctx;
            double s = this.scale;
            var L = this.level;
            double night = Math.Max(0, Math.Min(1, (-this.st.elev + 2) / 10));
            double ground(double wx) { return toScreenY(Levels.terrainAt(L, wx)); }
            // Rome: torches
            if (done.Contains(4))
            {
                for (double wx = 300; wx < L.length - 200; wx += 460)
                {
                    double px = toScreenX(wx);
                    if (px < -40 || px > this.w + 40) continue;
                    double gy = ground(wx) + 2 * s;
                    double top = gy - 46 * s;
                    ctx.StrokeStyle = "oklch(0.24 0.03 50)";
                    ctx.LineWidth = 2.6 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(px, gy);
                    ctx.LineTo(px, top);
                    ctx.Stroke();
                    ctx.FillStyle = "oklch(0.3 0.03 50)";
                    ctx.FillRect(px - 4 * s, top - 3 * s, 8 * s, 5 * s);
                    double fl = 1 + Math.Sin(this.t * 13 + wx) * 0.12 + Math.Sin(this.t * 7.3 + wx * 2) * 0.08;
                    double glowR = (26 + night * 30) * s * fl;
                    var g = ctx.CreateRadialGradient(px, top - 8 * s, 0, px, top - 8 * s, glowR);
                    g.AddColorStop(0, Col.Rgba(255, 190, 90, 0.35 + night * 0.35));
                    g.AddColorStop(1, "rgba(255, 150, 60, 0)");
                    ctx.Save();
                    ctx.GlobalCompositeOperation = Composite.Lighter;
                    ctx.FillStyle = g;
                    ctx.FillRect(px - glowR, top - 8 * s - glowR, glowR * 2, glowR * 2);
                    ctx.Restore();
                    ctx.FillStyle = "rgba(255, 214, 140, 0.95)";
                    ctx.BeginPath();
                    ctx.Ellipse(
                        px,
                        top - 8 * s,
                        3.4 * s,
                        7.5 * s * fl,
                        Math.Sin(this.t * 3 + wx) * 0.15,
                        0,
                        Math.PI * 2);
                    ctx.Fill();
                }
            }
            // China: paper lanterns on low posts
            if (done.Contains(5))
            {
                for (double wx = 520; wx < L.length - 200; wx += 520)
                {
                    double px = toScreenX(wx);
                    if (px < -40 || px > this.w + 40) continue;
                    double gy = ground(wx) + 2 * s;
                    double arm = gy - 52 * s;
                    ctx.StrokeStyle = "oklch(0.22 0.03 40)";
                    ctx.LineWidth = 2 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(px, gy);
                    ctx.LineTo(px, arm);
                    ctx.LineTo(px + 12 * s, arm);
                    ctx.Stroke();
                    double sway = Math.Sin(this.t * 1.4 + wx) * 1.5 * s;
                    double lx = px + 12 * s + sway;
                    double ly = arm + 13 * s;
                    double glowR = (22 + night * 26) * s;
                    var g = ctx.CreateRadialGradient(lx, ly, 0, lx, ly, glowR);
                    g.AddColorStop(0, Col.Rgba(255, 90, 60, 0.3 + night * 0.35));
                    g.AddColorStop(1, "rgba(255, 60, 40, 0)");
                    ctx.Save();
                    ctx.GlobalCompositeOperation = Composite.Lighter;
                    ctx.FillStyle = g;
                    ctx.FillRect(lx - glowR, ly - glowR, glowR * 2, glowR * 2);
                    ctx.Restore();
                    ctx.StrokeStyle = "rgba(40, 20, 10, 0.8)";
                    ctx.LineWidth = 0.8 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(px + 12 * s, arm);
                    ctx.LineTo(lx, ly - 7 * s);
                    ctx.Stroke();
                    ctx.FillStyle = Col.Oklch(Css.fixedNum(0.55 + night * 0.15, 2), 0.19, 30);
                    ctx.BeginPath();
                    ctx.Ellipse(lx, ly, 6.5 * s, 8 * s, 0, 0, Math.PI * 2);
                    ctx.Fill();
                    ctx.FillStyle = "oklch(0.25 0.04 60)";
                    ctx.FillRect(lx - 4 * s, ly - 9 * s, 8 * s, 2 * s);
                    ctx.FillRect(lx - 4 * s, ly + 7 * s, 8 * s, 2 * s);
                }
            }
            // a stele for every finished chapter, in a row below the summit
            var sorted = new List<double>(done);
            sorted.Sort();
            for (int i = 0; i < sorted.Count; i++)
            {
                double ch = sorted[i];
                double wx = L.length - 360 + i * 26;
                double px = toScreenX(wx);
                if (px < -30 || px > this.w + 30) continue;
                double gy = ground(wx) + 3 * s;
                double hh = 26 * s;
                double ww = 12 * s;
                ctx.FillStyle = "oklch(0.36 0.02 70)";
                ctx.BeginPath();
                ctx.MoveTo(px - ww / 2, gy);
                ctx.LineTo(px - ww / 2, gy - hh + 4 * s);
                ctx.QuadraticCurveTo(px, gy - hh - 3 * s, px + ww / 2, gy - hh + 4 * s);
                ctx.LineTo(px + ww / 2, gy);
                ctx.ClosePath();
                ctx.Fill();
                ctx.FillStyle = "rgba(20, 14, 8, 0.7)";
                ctx.Font = "600 " + Js.toFixed(Math.Max(6, 7 * s), 1) + "px Georgia, serif";
                ctx.TextAlign = TextAlign.Center;
                string numeral = ch >= 0 && ch < NUMERALS.Length && ch == Math.Floor(ch) ? NUMERALS[(int)ch] : "";
                ctx.FillText(numeral, px, gy - hh * 0.45);
            }
        }

        /// <summary>the columns of a fallen temple, standing and broken along the path</summary>
        private void drawRuins(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var ruins = this.level.ruins;
            if (ruins == null) return;
            var ctx = this.ctx;
            double s = this.scale;
            var light = this.lightScreen();
            var rim = Sky.rgb(light.color, 0.35 + light.strength * 0.4);
            foreach (var ruin in ruins)
            {
                double wx = ruin.Item1, left = ruin.Item2;
                double px = toScreenX(wx);
                if (px < -60 || px > this.w + 60) continue;
                double bse = toScreenY(Levels.terrainAt(this.level, wx)) + 4 * s;
                double full = 118 * s;
                double hgt = full * (0.25 + left * 0.75);
                double cw = 15 * s;
                ctx.Save();
                // the stylobate block it stands on
                ctx.FillStyle = "oklch(0.3 0.02 60)";
                ctx.FillRect(px - cw * 1.1, bse - 6 * s, cw * 2.2, 8 * s);
                // the shaft, tapering a little, its top broken unless it is whole
                double top = bse - hgt;
                ctx.BeginPath();
                ctx.MoveTo(px - cw * 0.78, bse - 6 * s);
                ctx.LineTo(px - cw * 0.66, top);
                if (left >= 0.98)
                {
                    ctx.LineTo(px + cw * 0.66, top);
                }
                else
                {
                    ctx.LineTo(px - cw * 0.2, top - 7 * s * this.hash(wx));
                    ctx.LineTo(px + cw * 0.15, top + 5 * s);
                    ctx.LineTo(px + cw * 0.66, top - 4 * s * this.hash(wx * 2));
                }
                ctx.LineTo(px + cw * 0.78, bse - 6 * s);
                ctx.ClosePath();
                ctx.FillStyle = "oklch(0.34 0.022 62)";
                ctx.Fill();
                // flutes
                ctx.StrokeStyle = "rgba(20, 14, 10, 0.35)";
                ctx.LineWidth = Math.Max(0.6, 0.8 * s);
                for (int f = -2; f <= 2; f++)
                {
                    ctx.BeginPath();
                    ctx.MoveTo(px + f * cw * 0.26, bse - 8 * s);
                    ctx.LineTo(px + f * cw * 0.23, top + 6 * s);
                    ctx.Stroke();
                }
                // the lit edge on the side the light comes from
                double side = light.x > px ? 1 : -1;
                ctx.StrokeStyle = rim;
                ctx.LineWidth = 1.4 * s;
                ctx.BeginPath();
                ctx.MoveTo(px + side * cw * 0.78, bse - 6 * s);
                ctx.LineTo(px + side * cw * 0.66, top + 3 * s);
                ctx.Stroke();
                // a whole column keeps its capital
                if (left >= 0.98)
                {
                    ctx.FillStyle = "oklch(0.36 0.022 62)";
                    ctx.FillRect(px - cw * 1.05, top - 7 * s, cw * 2.1, 7 * s);
                    ctx.FillRect(px - cw * 0.85, top - 2 * s, cw * 1.7, 3 * s);
                }
                ctx.Restore();
            }
        }

        /// <summary>draw a cached tree sprite at its base, skewed by the wind from the root up</summary>
        private void drawTree(string kind, double seed, double x, double y, double size, double wx)
        {
            var ctx = this.ctx;
            double d = this.dpr;
            double px = Js.round(size * d * 8) / 8;
            string key = kind + ":" + Js.str(seed) + ":" + Js.str(px);
            if (!this.treeCache.TryGetValue(key, out var sp) || sp == null)
            {
                if (this.treeCache.Count > 40) this.treeCache.Clear();
                sp = kind == "olive" ? Flora.oliveSprite(seed, px) : Flora.cypressSprite(seed, px);
                this.treeCache[key] = sp;
            }
            double sway = this.windAt(wx) * (kind == "cypress" ? 0.18 : 0.12);
            ctx.Save();
            ctx.Translate(x, y);
            ctx.Transform(1, 0, sway, 1, 0, 0);
            ctx.DrawImage(sp.cv, -sp.ox / d, -sp.oy / d, sp.cv.width / d, sp.cv.height / d);
            ctx.Restore();
        }

        private void drawOliveTrees(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            double s = this.scale;
            this.drawScree(toScreenX, toScreenY);
            this.drawMire(toScreenX, toScreenY);
            this.drawRuins(toScreenX, toScreenY);
            this.drawKeepsakes(toScreenX, toScreenY);
            if (this.level.cypresses != null)
            {
                foreach (var cy in this.level.cypresses)
                {
                    double wx = cy.Item1, sc = cy.Item2;
                    double px = toScreenX(wx);
                    if (px < -60 || px > this.w + 60) continue;
                    this.drawTree(
                        "cypress",
                        Js.round(wx),
                        px,
                        toScreenY(Levels.terrainAt(this.level, wx)) + 2,
                        sc * s * 0.95,
                        wx);
                }
            }
            if (this.level.trees != null)
            {
                foreach (var tr in this.level.trees)
                {
                    double wx = tr.Item1, sc = tr.Item2;
                    double px = toScreenX(wx);
                    if (px < -120 || px > this.w + 120) continue;
                    this.drawTree(
                        "olive",
                        Js.round(wx),
                        px,
                        toScreenY(Levels.terrainAt(this.level, wx)) + 2,
                        sc * s * 1.05,
                        wx);
                }
            }
        }

        static readonly double[] ZEUS_EYES = { -12, 7 };

        /// <summary>
        /// The summit cinematic: the sky goes dark, a storm bank rolls in over the
        /// peak, and Zeus rises out of it — a colossus, not a man standing beside the
        /// stone. See Zeus.cs for the figure itself.
        /// </summary>
        private void drawZeus(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            string st = this.zeus.state;
            this.boltFrom = null;
            if (st == "gone") return;
            var ctx = this.ctx;
            double w = this.w, h = this.h;
            var L = this.level;
            double ease(double x) { return 1 - Math.Pow(1 - Math.Max(0, Math.Min(1, x)), 3); }

            // how present he is: rises in over the first stretch, fades as the stone goes
            double a = 1;
            double rise = 0;
            if (st == "appear")
            {
                a = ease(this.zeus.t / 1.3);
                rise = 1 - a;
            }
            else if (st == "fade")
            {
                a = 1 - ease(this.zeus.t / 1.4);
                rise = -0.25 * (1 - a);
            }

            // the storm: the whole frame drops toward night
            ctx.FillStyle = Col.Rgba(14, 12, 34, Css.fixedNum(0.42 * a, 3));
            ctx.FillRect(0, 0, w, h);

            // size and place him: about half the frame tall, over and right of the peak
            double k = Math.Min((h * 0.46) / 300, (w * 0.45) / 380);
            double sumX = toScreenX(L.length);
            double sumY = toScreenY(Levels.terrainAt(L, L.length));
            double cx = Math.Max(w * 0.42, Math.Min(w * 0.82, sumX + w * 0.3));
            double waist = Math.Max(300 * k + h * 0.03, Math.Min(sumY - h * 0.04, h * 0.62));
            double breathe = Math.Sin(this.t * 1.3) * 2 * k;
            double y = waist + rise * 120 * k + breathe;
            double flashNow = st == "strike" ? Math.Max(0, 1 - this.zeus.t / 0.5) : 0;

            // cloud wall rolling in along the top of the frame
            for (int i = 0; i < 9; i++)
            {
                double px = ((double)i / 8) * w + Math.Sin(this.t * 0.4 + i * 1.7) * 18;
                double py = h * 0.02 + (i % 2) * h * 0.05 - (1 - a) * h * 0.2;
                this.zeusPainter.cloud(ctx, px, py, h * (0.13 + (i % 3) * 0.03), a * 0.95, flashNow);
            }

            ctx.Save();
            ctx.GlobalAlpha = a;
            // a faint halo of the god-light behind him
            var halo = ctx.CreateRadialGradient(cx, y - 200 * k, 0, cx, y - 200 * k, 220 * k);
            halo.AddColorStop(0, Col.Rgba(255, 228, 170, 0.28 + flashNow * 0.4));
            halo.AddColorStop(1, "rgba(255, 228, 170, 0)");
            ctx.GlobalCompositeOperation = Composite.Lighter;
            ctx.FillStyle = halo;
            ctx.FillRect(cx - 240 * k, y - 430 * k, 480 * k, 460 * k);
            ctx.GlobalCompositeOperation = Composite.SourceOver;

            // the body: wind-up until the throw, then the follow-through. The first
            // fraction of a second of the throw cross-fades the two so the arm snaps
            // rather than teleports.
            double kq = Js.round(k * 40) / 40;
            // built at the screen's pixel density, so he is as sharp on a phone as here
            double d = this.dpr;
            var raise = this.zeusPainter.sprite("raise", kq * d);
            var hurl = this.zeusPainter.sprite("hurl", kq * d);
            void blit(ZeusPainter.Sprite sp)
            {
                ctx.DrawImage(sp.cv, cx - sp.ox / d, y - sp.oy / d, sp.cv.width / d, sp.cv.height / d);
            }
            double snap = st == "appear" ? 0 : Math.Min(1, this.zeus.t / 0.09);
            if (raise != null && snap < 1)
            {
                ctx.GlobalAlpha = a * (1 - snap);
                blit(raise);
            }
            if (hurl != null && snap > 0)
            {
                ctx.GlobalAlpha = a * snap;
                blit(hurl);
            }
            ctx.GlobalAlpha = a;

            // eyes kindle as the throw comes, and blaze at it
            double charge = st == "appear" ? ease((this.zeus.t - 0.6) / 1.2) : 1;
            double eyeGlow = 0.25 + charge * 0.5 + flashNow * 0.6;
            ctx.GlobalCompositeOperation = Composite.Lighter;
            foreach (double ex in ZEUS_EYES)
            {
                double gx = cx + ex * kq;
                double gy = y - 205 * kq;
                var g = ctx.CreateRadialGradient(gx, gy, 0, gx, gy, 9 * kq);
                g.AddColorStop(0, Col.Rgba(220, 235, 255, Css.fixedNum(eyeGlow, 3)));
                g.AddColorStop(1, "rgba(160, 200, 255, 0)");
                ctx.FillStyle = g;
                ctx.BeginPath();
                ctx.Arc(gx, gy, 9 * kq, 0, Math.PI * 2);
                ctx.Fill();
            }
            ctx.GlobalCompositeOperation = Composite.SourceOver;

            // the bolt in his fist while he winds up, brightening to the throw
            if (st == "appear")
            {
                var hand = Zeus.zeusHand("raise");
                double flick = 0.85 + Math.Sin(this.t * 37) * 0.15;
                Zeus.drawKeraunos(ctx, cx + hand.x * kq, y + hand.y * kq, 110 * kq, -0.18, (0.4 + charge * 0.6) * flick);
            }
            else if (st == "strike")
            {
                var hand = Zeus.zeusHand("hurl");
                this.boltFrom = new BoltFromT { x = cx + hand.x * kq, y = y + hand.y * kq };
            }

            // the storm bank he stands in, over his waist
            for (int i = 0; i < 8; i++)
            {
                double px = cx + (i - 3.5) * 56 * k + Math.Sin(this.t * 0.7 + i) * 6 * k;
                double py = y + (16 + (i % 2) * 10) * k;
                this.zeusPainter.cloud(ctx, px, py, (50 + (i % 3) * 12) * k, 1, flashNow);
            }
            ctx.Restore();
        }

        /// <summary>
        /// The bolt from his hand to the stone. A channel reshaped by Math.random() on
        /// every frame is static, not lightning, so the clock is quantised: one
        /// channel holds for ~70 ms and then re-strikes, the way a real flash does.
        /// </summary>
        private void drawBolt(double tx, double ty)
        {
            var from = this.boltFrom;
            if (from == null || this.zeus.state != "strike" || this.zeus.t > 0.9) return;
            var ctx = this.ctx;
            double s = this.scale;
            double life = 1 - this.zeus.t / 0.9;
            double strike = Math.Floor(this.zeus.t * 14);
            double jitter(double i) { return this.hash(strike * 31.7 + i * 5.3) - 0.5; }
            double power = (0.55 + this.hash(strike * 3.1) * 0.45) * (0.4 + life * 0.6);
            double ox = from.x, oy = from.y;
            double len = Js.hypot(tx - ox, ty - oy);
            const int segs = 14;
            var chX = new double[segs + 1];
            var chY = new double[segs + 1];
            chX[0] = ox;
            chY[0] = oy;
            for (int i = 1; i <= segs; i++)
            {
                double tt = (double)i / segs;
                double spread = len * 0.09 * Math.Pow(Math.Sin(tt * Math.PI), 0.7);
                chX[i] = ox + (tx - ox) * tt + jitter(i) * spread;
                chY[i] = oy + (ty - oy) * tt + jitter(i + 40) * spread * 0.4;
            }
            void trace()
            {
                ctx.BeginPath();
                for (int i = 0; i <= segs; i++)
                {
                    if (i == 0) ctx.MoveTo(chX[i], chY[i]);
                    else ctx.LineTo(chX[i], chY[i]);
                }
            }
            ctx.Save();
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;
            ctx.GlobalCompositeOperation = Composite.Lighter;
            ctx.StrokeStyle = Col.Rgba(255, 210, 140, Css.fixedNum(0.25 * power, 3));
            ctx.LineWidth = 22 * s;
            trace();
            ctx.Stroke();
            ctx.StrokeStyle = Col.Rgba(255, 238, 200, Css.fixedNum(0.6 * power, 3));
            ctx.LineWidth = 7 * s;
            trace();
            ctx.Stroke();
            ctx.GlobalCompositeOperation = Composite.SourceOver;
            ctx.StrokeStyle = Col.Rgba(255, 255, 255, Css.fixedNum(0.95 * power, 3));
            ctx.LineWidth = 2.4 * s;
            trace();
            ctx.Stroke();
            // forks peeling off the channel and dying out
            ctx.StrokeStyle = Col.Rgba(255, 236, 190, Css.fixedNum(0.7 * power, 3));
            ctx.LineWidth = 1.6 * s;
            ctx.BeginPath();
            for (int b = 0; b < 6; b++)
            {
                int at = 2 + (int)Math.Floor(this.hash(strike * 9.7 + b) * (segs - 4));
                double fx = chX[at], fy = chY[at];
                ctx.MoveTo(fx, fy);
                for (int k = 1; k <= 3; k++)
                {
                    fx += (jitter(b * 7 + k) * 60 + 8) * s * (1 - k * 0.22);
                    fy += (jitter(b * 13 + k) * 30 + 10) * s * (1 - k * 0.22);
                    ctx.LineTo(fx, fy);
                }
            }
            ctx.Stroke();
            // where it lands: a white bloom on the stone
            var g = ctx.CreateRadialGradient(tx, ty, 0, tx, ty, 70 * s);
            g.AddColorStop(0, Col.Rgba(255, 250, 230, Css.fixedNum(0.9 * power, 3)));
            g.AddColorStop(0.3, Col.Rgba(255, 220, 160, Css.fixedNum(0.4 * power, 3)));
            g.AddColorStop(1, "rgba(255, 210, 150, 0)");
            ctx.GlobalCompositeOperation = Composite.Lighter;
            ctx.FillStyle = g;
            ctx.BeginPath();
            ctx.Arc(tx, ty, 70 * s, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Restore();
        }

        /// <summary>
        /// offscreen layers for the man, so his rim light can follow his real outline.
        /// PORT: the TS starts with [] and writes slots 0-2; here the three slots exist
        /// from the start, empty (null), so `figLayers[i] = mk(figLayers[i])` works.
        /// </summary>
        private List<Surface> figLayers = new List<Surface> { null, null, null };
        private Surface rimLayer = null;
        /// <summary>the old climb's last frame, while it dissolves (see restart)</summary>
        private Surface fadeCv = null;
        private double fadeT = 99;
        /// <summary>seconds into stepping back from the stone, and where he stepped from</summary>
        private double setT = 99;
        private double setFrom = 0;
        /// <summary>a fifth-size scratch for the figure's soft light and shade</summary>
        private Surface figLo = null;
        /// <summary>set only while drawFigureLit is drawing him: far limbs, body, near arm (3 entries, or null)</summary>
        private Ctx[] figCtxs = null;
    }
}
