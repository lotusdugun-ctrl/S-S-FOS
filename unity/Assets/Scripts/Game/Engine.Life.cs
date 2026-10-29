using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// What else lives in the world: a landmark of the old stories standing on the far
    /// ridge of each hill, Pegasus crossing the sky now and then, butterflies over the
    /// path by day and fireflies after dusk. Small, far, quiet — none of it asks to be
    /// looked at, and all of it is gone in low quality.
    /// </summary>
    public partial class SisyphusEngine
    {
        /// <summary>
        /// The far ridge's landmark, drawn in the ridge's own colour so the air between
        /// holds it at the same distance: on Tartarus a ruined temple, on the scree Atlas
        /// kneeling under the sky, on the windy ridge the gate of a sanctuary, among the
        /// ruins a fallen colossus. `ridgeY` gives the ridge's crest at a screen x; `p` its
        /// parallax and `off` its offset, as the ridge loop uses them.
        /// </summary>
        private void drawLandmark(Ctx ctx, Func<double, double> ridgeY, double p, double off, Style tone)
        {
            if (this.lowQuality) return;
            double s = this.scale;
            // The far ridge only shows to the left of him, below the skyline where the hill
            // falls away; the landmark stands there a third of the way across when he is a
            // quarter of the way up, and drifts slowly left as he climbs.
            double wx = off + 0.17 * this.w / s + p * 0.25 * this.level.length;
            double sx = (wx - this.camX * p - off) * s;
            if (sx < -160 * s || sx > this.w + 160 * s) return;
            double gy = ridgeY(sx) + 2 * s;
            double k = 0.85 * s;
            ctx.Save();
            // a far silhouette: the horizon's own colour, deepened, as the air leaves it
            var hz = this.st.ramp[this.st.ramp.Length - 1];
            var sil = Css.rgb(new RGB(hz.r * 0.45 + 30 * 0.55, hz.g * 0.45 + 26 * 0.55, hz.b * 0.45 + 40 * 0.55), 0.9);
            ctx.FillStyle = sil;
            ctx.StrokeStyle = sil;
            ctx.LineCap = LineCap.Butt;
            switch (this.level.id)
            {
                case "scree": this.drawAtlas(ctx, sx, gy, k); break;
                case "windy": this.drawGate(ctx, sx, gy, k); break;
                case "ruins": this.drawColossus(ctx, sx, gy, k); break;
                default: this.drawTemple(ctx, sx, gy, k); break;
            }
            ctx.Restore();
        }

        private void drawTemple(Ctx c, double x, double y, double k)
        {
            // three steps, six columns (two of them broken), what is left of the roof
            c.BeginPath();
            c.Rect(x - 62 * k, y - 4 * k, 124 * k, 4 * k);
            c.Rect(x - 56 * k, y - 8 * k, 112 * k, 4 * k);
            c.Rect(x - 50 * k, y - 12 * k, 100 * k, 4 * k);
            c.Fill();
            double[] hgt = { 46, 46, 30, 46, 46, 18 };
            for (int i = 0; i < 6; i++)
            {
                double cx = x - 42 * k + i * 16.8 * k;
                double top = y - 12 * k - hgt[i] * k;
                c.BeginPath();
                c.MoveTo(cx - 4 * k, y - 12 * k);
                c.LineTo(cx - 3.2 * k, top);
                if (hgt[i] < 40)
                {
                    // a broken shaft ends in a jagged edge
                    c.LineTo(cx - 1 * k, top - 3 * k);
                    c.LineTo(cx + 1 * k, top + 1 * k);
                    c.LineTo(cx + 3.2 * k, top - 2 * k);
                }
                else c.LineTo(cx + 3.2 * k, top);
                c.LineTo(cx + 4 * k, y - 12 * k);
                c.ClosePath();
                c.Fill();
            }
            // the architrave and the half of the pediment still standing, over the left columns
            c.BeginPath();
            c.Rect(x - 50 * k, y - 64 * k, 72 * k, 6 * k);
            c.Fill();
            c.BeginPath();
            c.MoveTo(x - 52 * k, y - 64 * k);
            c.LineTo(x - 10 * k, y - 82 * k);
            c.LineTo(x + 8 * k, y - 74 * k);
            c.LineTo(x + 22 * k, y - 64 * k);
            c.ClosePath();
            c.Fill();
            // fallen drums at its foot
            c.BeginPath();
            c.Ellipse(x + 50 * k, y - 3 * k, 7 * k, 3.5 * k, 0, 0, Math.PI * 2);
            c.Ellipse(x + 64 * k, y - 2.5 * k, 5 * k, 3 * k, 0.3, 0, Math.PI * 2);
            c.Fill();
        }

        private void drawAtlas(Ctx c, double x, double y, double k)
        {
            // the titan on one knee, the sky's sphere on his shoulders, arms up to hold it
            c.BeginPath();
            // the rock he kneels on
            c.MoveTo(x - 40 * k, y);
            c.QuadraticCurveTo(x - 30 * k, y - 14 * k, x, y - 16 * k);
            c.QuadraticCurveTo(x + 30 * k, y - 14 * k, x + 40 * k, y);
            c.ClosePath();
            c.Fill();
            c.LineJoin = LineJoin.Round;
            c.LineCap = LineCap.Round;
            // the back leg, kneeling, and the front leg, bent
            c.LineWidth = 7 * k;
            c.BeginPath();
            c.MoveTo(x - 4 * k, y - 46 * k);
            c.LineTo(x - 14 * k, y - 28 * k);
            c.LineTo(x - 30 * k, y - 18 * k);
            c.MoveTo(x + 2 * k, y - 46 * k);
            c.LineTo(x + 16 * k, y - 34 * k);
            c.LineTo(x + 14 * k, y - 16 * k);
            c.Stroke();
            // the body, bowed forward under the weight
            c.LineWidth = 12 * k;
            c.BeginPath();
            c.MoveTo(x, y - 46 * k);
            c.QuadraticCurveTo(x + 2 * k, y - 62 * k, x + 8 * k, y - 72 * k);
            c.Stroke();
            // the arms, up and back to the sphere
            c.LineWidth = 5 * k;
            c.BeginPath();
            c.MoveTo(x + 6 * k, y - 70 * k);
            c.LineTo(x - 6 * k, y - 84 * k);
            c.LineTo(x - 8 * k, y - 98 * k);
            c.MoveTo(x + 12 * k, y - 70 * k);
            c.LineTo(x + 22 * k, y - 84 * k);
            c.LineTo(x + 20 * k, y - 98 * k);
            c.Stroke();
            // the head, bent down between the arms
            c.BeginPath();
            c.Arc(x + 12 * k, y - 78 * k, 5 * k, 0, Math.PI * 2);
            c.Fill();
            // the heavens: a great sphere, its bands just showing
            c.BeginPath();
            c.Arc(x + 6 * k, y - 118 * k, 24 * k, 0, Math.PI * 2);
            c.Fill();
            c.Save();
            c.GlobalAlpha = 0.18;
            c.StrokeStyle = "#ffffff";
            c.LineWidth = 1.2 * k;
            c.BeginPath();
            c.Ellipse(x + 6 * k, y - 118 * k, 24 * k, 8 * k, -0.35, 0, Math.PI * 2);
            c.Ellipse(x + 6 * k, y - 118 * k, 9 * k, 24 * k, 0.2, 0, Math.PI * 2);
            c.Stroke();
            c.Restore();
        }

        private void drawGate(Ctx c, double x, double y, double k)
        {
            // two tall columns and their lintel, alone on the ridge, a cypress beside them
            c.BeginPath();
            c.Rect(x - 30 * k, y - 6 * k, 60 * k, 6 * k);
            c.Fill();
            for (int i = -1; i <= 1; i += 2)
            {
                double cx = x + i * 18 * k;
                c.BeginPath();
                c.MoveTo(cx - 4.5 * k, y - 6 * k);
                c.LineTo(cx - 3.6 * k, y - 80 * k);
                c.LineTo(cx + 3.6 * k, y - 80 * k);
                c.LineTo(cx + 4.5 * k, y - 6 * k);
                c.ClosePath();
                c.Fill();
                c.BeginPath();
                c.Rect(cx - 6 * k, y - 84 * k, 12 * k, 4 * k);
                c.Fill();
            }
            c.BeginPath();
            c.Rect(x - 28 * k, y - 92 * k, 56 * k, 8 * k);
            c.Fill();
            // the cypress, swaying a little in the wind of the ridge
            double sway = Math.Sin(this.t * 0.9) * 2 * k * this.windiness();
            c.BeginPath();
            c.MoveTo(x + 44 * k, y);
            c.QuadraticCurveTo(x + 36 * k, y - 40 * k, x + 45 * k + sway, y - 70 * k);
            c.QuadraticCurveTo(x + 54 * k, y - 40 * k, x + 46 * k, y);
            c.ClosePath();
            c.Fill();
        }

        private void drawColossus(Ctx c, double x, double y, double k)
        {
            // a giant's head of stone, fallen on its side and half sunk in the ground
            c.BeginPath();
            c.MoveTo(x - 50 * k, y);
            c.BezierCurveTo(x - 56 * k, y - 30 * k, x - 36 * k, y - 52 * k, x - 6 * k, y - 54 * k); // crown
            c.LineTo(x + 6 * k, y - 50 * k); // brow
            c.LineTo(x + 8 * k, y - 44 * k);
            c.LineTo(x + 22 * k, y - 36 * k); // the nose, pointing at the sky
            c.LineTo(x + 12 * k, y - 32 * k);
            c.LineTo(x + 14 * k, y - 26 * k); // lips
            c.LineTo(x + 10 * k, y - 22 * k);
            c.QuadraticCurveTo(x + 18 * k, y - 12 * k, x + 10 * k, y); // chin into the ground
            c.ClosePath();
            c.Fill();
            // a crack across the face, and a hand's broken fingers nearby
            c.Save();
            c.GlobalAlpha = 0.25;
            c.StrokeStyle = "#000000";
            c.LineWidth = 1.2 * k;
            c.BeginPath();
            c.MoveTo(x - 20 * k, y - 50 * k);
            c.LineTo(x - 12 * k, y - 30 * k);
            c.LineTo(x - 18 * k, y - 10 * k);
            c.Stroke();
            c.Restore();
            c.BeginPath();
            for (int i = 0; i < 3; i++) c.Rect(x + 34 * k + i * 7 * k, y - (10 + i * 3) * k, 5 * k, (10 + i * 3) * k);
            c.Fill();
        }

        /// <summary>
        /// Pegasus, very far off, crossing the sky by day: once every couple of minutes,
        /// fourteen seconds from one side to the other, wings beating slowly.
        /// </summary>
        private void drawPegasus()
        {
            if (this.lowQuality || this.weather != "clear" || this.st.elev < 4) return;
            const double period = 130, dur = 16;
            double ph = this.t % period;
            if (ph > dur) return;
            double k = this.scale * 0.9;
            double u = ph / dur;
            int n = (int)Math.Floor(this.t / period);
            bool rtl = (n % 2) == 0;
            double x = rtl ? this.w * (1.1 - 1.2 * u) : this.w * (-0.1 + 1.2 * u);
            double y = this.h * (0.16 + 0.08 * this.hash(n * 3.7)) + Math.Sin(u * Math.PI * 3) * 6 * k;
            double flap = Math.Sin(this.t * 4.2);
            var ctx = this.ctx;
            ctx.Save();
            ctx.Translate(x, y);
            if (!rtl) ctx.Scale(-1, 1);
            ctx.GlobalAlpha = 0.5 * Math.Min(1, Math.Min(u, 1 - u) * 8);
            ctx.FillStyle = "rgba(48, 46, 66, 1)";
            ctx.StrokeStyle = "rgba(48, 46, 66, 1)";
            ctx.LineCap = LineCap.Round;
            // the body, a horse at the gallop facing left
            ctx.BeginPath();
            ctx.Ellipse(0, 0, 9 * k, 3.6 * k, 0, 0, Math.PI * 2);
            ctx.Fill();
            ctx.LineWidth = 2.2 * k;
            ctx.BeginPath();
            ctx.MoveTo(-7 * k, -1 * k);
            ctx.LineTo(-11 * k, -6 * k); // neck
            ctx.LineTo(-14 * k, -5 * k); // head
            ctx.Stroke();
            ctx.LineWidth = 1.3 * k;
            ctx.BeginPath();
            double g = Math.Sin(this.t * 6) * 2 * k;
            ctx.MoveTo(-6 * k, 2 * k); ctx.LineTo(-10 * k, 7 * k + g); // forelegs
            ctx.MoveTo(-4 * k, 2 * k); ctx.LineTo(-7 * k, 7 * k - g);
            ctx.MoveTo(6 * k, 2 * k); ctx.LineTo(10 * k, 6 * k - g); // hind legs
            ctx.MoveTo(4 * k, 2 * k); ctx.LineTo(7 * k, 7 * k + g);
            ctx.MoveTo(9 * k, -1 * k); ctx.QuadraticCurveTo(14 * k, -2 * k, 15 * k, 3 * k); // tail
            ctx.Stroke();
            // the wings, beating
            ctx.BeginPath();
            ctx.MoveTo(-3 * k, -2 * k);
            ctx.QuadraticCurveTo(2 * k, (-10 - 8 * flap) * k, 12 * k, (-14 - 10 * flap) * k);
            ctx.QuadraticCurveTo(6 * k, (-6 - 3 * flap) * k, 3 * k, -2 * k);
            ctx.ClosePath();
            ctx.Fill();
            ctx.Restore();
        }

        /// <summary>
        /// Butterflies over the path in the light of day, fireflies in the grass after
        /// dusk: a few each, in the world (they stay where they are as he passes), never
        /// in rain or storm.
        /// </summary>
        private void drawCreatures(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (this.lowQuality || this.weather != "clear") return;
            var ctx = this.ctx;
            var L = this.level;
            double s = this.scale;
            double elev = this.st.elev;
            double day = Math.Max(0, Math.Min(1, (elev - 4) / 6));
            double dark = Math.Max(0, Math.Min(1, (3 - elev) / 6));
            if (day <= 0 && dark <= 0) return;
            double span = this.w / s + 200;
            double left = this.camX - 100;
            ctx.Save();
            // butterflies: each lives in a cell of the hill, fluttering around its own spot
            if (day > 0)
            {
                const double cell = 420;
                for (double c0 = Math.Floor(left / cell) * cell; c0 < left + span; c0 += cell)
                {
                    double hh = this.hash(c0 * 0.013 + 1.7);
                    if (hh > 0.7) continue;
                    double t = this.t + hh * 50;
                    double wx = c0 + hh * cell + Math.Sin(t * 0.37) * 60 + Math.Sin(t * 1.3) * 14;
                    double wy = Levels.terrainAt(L, wx) + 18 + Math.Sin(t * 0.8) * 10 + Math.Abs(Math.Sin(t * 2.1)) * 8;
                    double x = toScreenX(wx), y = toScreenY(wy);
                    if (x < -20 || x > this.w + 20) continue;
                    double flap = Math.Abs(Math.Sin(t * 13));
                    string col = hh < 0.25 ? "rgba(250, 240, 200, 0.9)" : hh < 0.5 ? "rgba(245, 170, 80, 0.9)" : "rgba(220, 230, 250, 0.9)";
                    ctx.GlobalAlpha = day;
                    ctx.FillStyle = col;
                    ctx.BeginPath();
                    ctx.Ellipse(x - 1.6 * s * flap, y, 1.8 * s * flap + 0.3, 1.4 * s, -0.3, 0, Math.PI * 2);
                    ctx.Ellipse(x + 1.6 * s * flap, y, 1.8 * s * flap + 0.3, 1.4 * s, 0.3, 0, Math.PI * 2);
                    ctx.Fill();
                }
            }
            // fireflies: low in the grass, each glowing and going dark on its own slow beat
            if (dark > 0)
            {
                ctx.GlobalCompositeOperation = Composite.Lighter;
                const double cell = 90;
                for (double c0 = Math.Floor(left / cell) * cell; c0 < left + span; c0 += cell)
                {
                    double hh = this.hash(c0 * 0.031 + 4.1);
                    if (hh > 0.55) continue;
                    double t = this.t + hh * 30;
                    double glow = Math.Max(0, Math.Sin(t * (0.8 + hh) + hh * 9));
                    if (glow < 0.05) continue;
                    double wx = c0 + hh * cell + Math.Sin(t * 0.5) * 20;
                    double wy = Levels.terrainAt(L, wx) + 6 + hh * 40 + Math.Sin(t * 0.9) * 6;
                    double x = toScreenX(wx), y = toScreenY(wy);
                    if (x < -10 || x > this.w + 10) continue;
                    double r = 7 * s;
                    var g = ctx.CreateRadialGradient(x, y, 0, x, y, r);
                    g.AddColorStop(0, Col.Rgba(230, 255, 140, Css.fixedNum(0.9 * glow * dark, 3)));
                    g.AddColorStop(0.25, Col.Rgba(190, 240, 90, Css.fixedNum(0.35 * glow * dark, 3)));
                    g.AddColorStop(1, "rgba(190, 240, 90, 0)");
                    ctx.FillStyle = g;
                    ctx.FillRect(x - r, y - r, r * 2, r * 2);
                }
            }
            ctx.Restore();
        }
    }
}
