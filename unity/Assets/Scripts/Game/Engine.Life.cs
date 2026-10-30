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
        /// Figures from the old stories standing, and moving, on the far ridges: a temple
        /// with smoke going up from its altar, Atlas under the weight of the sky shifting
        /// it on his shoulders, the gate of a sanctuary with its fires burning, a fallen
        /// colossus with birds wheeling over it, a centaur galloping along the skyline,
        /// Cerberus pacing. They stand at intervals along the ridge, a different one at
        /// each, so the skyline keeps changing as he climbs. Drawn in the air's colour for
        /// their distance — darker on the nearer ridge — and only where the ridge shows.
        /// `layer` 0 is the far ridge, 1 the next.
        /// </summary>
        /// <summary>test switches (Autopilot "set"): leave the far figures / the ridge life out</summary>
        internal bool dbgNoFar, dbgNoLife;
        /// <summary>what each far figure's canvas was last painted for (kind and light), by canvas; a counter for the moving ones</summary>
        private readonly System.Collections.Generic.Dictionary<string, string> farPainted = new System.Collections.Generic.Dictionary<string, string>();
        private readonly System.Collections.Generic.Dictionary<string, int> farCount = new System.Collections.Generic.Dictionary<string, int>();

        private void drawLandmark(Ctx ctx, Func<double, double> ridgeY, double p, double off, int layer)
        {
            if (this.lowQuality || this.dbgNoFar) return;
            double s = this.scale;
            double spacing = (layer == 0 ? 0.62 : 0.5) * this.w / s;
            double phase = layer == 0 ? 0.17 * this.w / s : 0.43 * this.w / s;
            // which of them are on screen
            double rel = this.camX * p - phase;
            int n0 = (int)Math.Floor((rel - 200 / s) / spacing);
            int n1 = (int)Math.Ceiling((rel + this.w / s + 200 / s) / spacing);
            var hz = this.st.ramp[this.st.ramp.Length - 1];
            double dk = layer == 0 ? 0.55 : 0.7;
            var sil = Css.rgb(new RGB(hz.r * (1 - dk) + 26 * dk, hz.g * (1 - dk) + 22 * dk, hz.b * (1 - dk) + 36 * dk), 0.95);
            double k = (layer == 0 ? 0.95 : 1.15) * s;
            this.landAir = new RGB(hz.r, hz.g, hz.b);
            // the sunward edge of each figure, lit: a copy drawn first in the light's colour,
            // a little toward the sun, so the silhouette over it keeps a thin bright rim
            var lc = this.st.light.color;
            double sunUp = Math.Max(0, Math.Min(1, (this.st.elev + 2) / 10));
            double rimA = (layer == 0 ? 0.35 : 0.5) * sunUp * (1 - this.st.cover * 0.5);
            var rimCol = Css.rgb(new RGB(Math.Min(255, lc.r * 0.6 + 110), Math.Min(255, lc.g * 0.6 + 90), Math.Min(255, lc.b * 0.6 + 70)), Css.fixedNum(rimA, 3));
            for (int n = n0; n <= n1; n++)
            {
                double hh = this.hash(n * 7.13 + layer * 101 + this.levelIndex * 3.7);
                // now and then the ridge is just a ridge
                if (hh > 0.88) continue;
                // the battle is the largest of them, and comes up more often than any one other
                double pick = this.hash(n * 3.31 + layer * 17 + this.levelIndex * 5.9);
                int kind = pick > 0.78 ? 6 : (int)Math.Floor(pick / 0.78 * 6);
                double sx = (n * spacing - rel) * s;
                // the ones that move go back and forth along their stretch of the ridge
                double dir = 1;
                if (kind == 4 || kind == 5)
                {
                    double speed = kind == 4 ? 26 : 9;
                    double reach = spacing * 0.32 * s;
                    double u = (this.t * speed * s + hh * 1000) % (reach * 4);
                    double tri = u < reach * 2 ? u : reach * 4 - u;
                    dir = u < reach * 2 ? 1 : -1;
                    sx += tri - reach;
                }
                double margin = (kind == 6 ? 300 : 160) * s;
                if (sx < -margin || sx > this.w + margin) continue;
                double gy = ridgeY(sx) + 2 * s;
                this.paintFarFigure(ctx, kind, sx, gy, k, hh, dir, layer, new RGB(hz.r, hz.g, hz.b), dk, ((n % 3) + 3) % 3);
            }
        }

        /// <summary>
        /// One far figure (`kind` as in drawLandmark) at (sx, gy) on screen, scale k, painted
        /// in colour and lit; `slot` picks the scratch canvas it is painted in.
        /// </summary>
        internal void paintFarFigure(Ctx ctx, int kind, double sx, double gy, double k, double hh, double dir, int layer, RGB hz, double dk, int slot)
        {
            // Painted as bodies in the light, not cut-outs: each in the colours of what it
            // is (bronze skin, marble, horsehide, the night-blue of the heavens), lit from
            // the sun's side the way the figures on the hill are, and then given back to
            // the air between it and the eye — the far ridge more than the near one.
            double[] bx = FAR_BOX[kind];
            double m = 6;
            var L = this.npcLight(sx, gy);
            // the light reaches them less than the hill at his feet: a softer key
            L.strength *= 0.8;
            this.farLit = true;
            this.farAir = layer == 0 ? 0.34 : 0.22;
            this.farHaze = hz;
            this.farDark = dk;
            string key = "far" + layer + "_" + slot;
            // Painting one lit costs a canvas of its own and half a dozen passes over it,
            // which a phone feels. The gate, which does not move, is painted again only when
            // the light on it has moved a step; the rest (smoke, birds, breath, gallop,
            // battle) every third frame, which at their distance reads as smooth.
            double ll = (bx[0] - m) * k, tt = (bx[1] - m) * k, rr = (bx[2] + m) * k, bb = (bx[3] + 2) * k;
            bool still = kind == 2;
            string lkey = kind + "|" + Js.round(hh * 1000) + "|" + Js.round(L.lx * 8) + "|" + Js.round(L.ly * 8) + "|" + Js.round(L.strength * 12) + "|" + Js.round(L.dim * 12) + "|" + Js.round(hz.r / 6) + "|" + Js.round(hz.g / 6) + "|" + Js.round(hz.b / 6) + "|" + Js.round(k * 100);
            string prev;
            this.farPainted.TryGetValue(key, out prev);
            int fc;
            this.farCount.TryGetValue(key, out fc);
            this.farCount[key] = fc + 1;
            bool due = still ? prev != lkey : (fc % 3 == 0) || prev == null || !prev.StartsWith(kind + "|");
            if (!due && Figures.blitFar(ctx, key, sx, gy, ll, tt, rr, bb))
            {
                this.farLit = false;
                if (kind == 0) this.farFire(ctx, sx + 61 * k, gy - 10 * k, k, hh);
                else if (kind == 2)
                {
                    this.farFire(ctx, sx - 22.5 * k, gy - 97 * k, k, hh);
                    this.farFire(ctx, sx + 22.5 * k, gy - 97 * k, k, hh + 0.5);
                }
                return;
            }
            this.farPainted[key] = lkey;
            int kk = kind;
            double hh2 = hh, dir2 = dir, k2 = k;
            Figures.paintFar(ctx, key, sx, gy, (bx[0] - m) * k, (bx[1] - m) * k, (bx[2] + m) * k, (bx[3] + 2) * k, L, k * 0.85,
                c =>
                {
                    this.mat(c, 120, 104, 96);
                    c.LineCap = LineCap.Butt;
                    switch (kk)
                    {
                        case 0: this.drawTemple(c, 0, 0, k2, hh2); break;
                        case 1: this.drawAtlas2(c, 0, 0, k2, hh2); break;
                        case 2: this.drawGate(c, 0, 0, k2, hh2); break;
                        case 3: this.drawColossus(c, 0, 0, k2, hh2); break;
                        case 4: this.drawCentaur2(c, 0, 0, k2, dir2); break;
                        case 5: this.drawCerberus2(c, 0, 0, k2, dir2); break;
                        default: this.drawBattle(c, 0, 0, k2, hh2); break;
                    }
                },
                (c, W, H) => this.farAtmosphere(c, W, H, layer));
            this.farLit = false;
            // the fires are drawn after, over the air, so the haze does not put them out
            if (kind == 0) this.farFire(ctx, sx + 61 * k, gy - 10 * k, k, hh);
            else if (kind == 2)
            {
                this.farFire(ctx, sx - 22.5 * k, gy - 97 * k, k, hh);
                this.farFire(ctx, sx + 22.5 * k, gy - 97 * k, k, hh + 0.5);
            }
        }

        /// <summary>a fire on a far silhouette: a flickering tongue and its glow</summary>
        private void farFire(Ctx c, double x, double y, double k, double seed)
        {
            if (this.rimPass || this.farLit) return;
            double f = 0.75 + 0.25 * Math.Sin(this.t * 9 + seed * 7) + 0.12 * Math.Sin(this.t * 23 + seed);
            double night = Math.Max(0.35, Math.Min(1, (8 - this.st.elev) / 10));
            c.Save();
            c.GlobalCompositeOperation = Composite.Lighter;
            var g = c.CreateRadialGradient(x, y - 3 * k, 0, x, y - 3 * k, 14 * k);
            g.AddColorStop(0, Col.Rgba(255, 170, 70, Css.fixedNum(0.55 * f * night, 3)));
            g.AddColorStop(1, "rgba(255, 140, 50, 0)");
            c.FillStyle = g;
            c.FillRect(x - 14 * k, y - 17 * k, 28 * k, 28 * k);
            c.FillStyle = Col.Rgba(255, 200, 110, Css.fixedNum(0.9 * night, 3));
            c.BeginPath();
            c.MoveTo(x - 2.5 * k, y);
            c.QuadraticCurveTo(x - 2 * k, y - 4 * k * f, x + Math.Sin(this.t * 7 + seed) * k, y - 8 * k * f);
            c.QuadraticCurveTo(x + 2 * k, y - 4 * k * f, x + 2.5 * k, y);
            c.ClosePath();
            c.Fill();
            c.Restore();
        }

        private void drawTemple(Ctx c, double x, double y, double k, double seed)
        {
            // three steps, six columns (two of them broken), what is left of the roof
            this.mat(c, 214, 200, 178);
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
                    c.LineTo(cx - 1 * k, top - 3 * k);
                    c.LineTo(cx + 1 * k, top + 1 * k);
                    c.LineTo(cx + 3.2 * k, top - 2 * k);
                }
                else c.LineTo(cx + 3.2 * k, top);
                c.LineTo(cx + 4 * k, y - 12 * k);
                c.ClosePath();
                c.Fill();
                if (this.farLit)
                {
                    // the fluting, catching light and shade by turns; a capital on the whole ones
                    c.Save();
                    c.LineWidth = 0.6 * k;
                    for (int f = -1; f <= 1; f++)
                    {
                        c.StrokeStyle = this.farTone(f < 0 ? 250 : 120, f < 0 ? 240 : 104, f < 0 ? 220 : 92, 0.55);
                        c.BeginPath();
                        c.MoveTo(cx + f * 1.6 * k, y - 13 * k);
                        c.LineTo(cx + f * 1.3 * k, top + 2 * k);
                        c.Stroke();
                    }
                    if (hgt[i] >= 40)
                    {
                        c.FillStyle = this.farTone(228, 216, 196, 1);
                        c.BeginPath();
                        c.Rect(cx - 5 * k, top - 1.6 * k, 10 * k, 2 * k);
                        c.Fill();
                    }
                    c.Restore();
                }
            }
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
            // the altar in front, its fire, and the smoke of the offering going up and away
            c.BeginPath();
            c.Rect(x + 56 * k, y - 10 * k, 10 * k, 10 * k);
            c.Fill();
            this.farFire(c, x + 61 * k, y - 10 * k, k, seed);
            if (this.rimPass) return;
            double wind = 6 + this.windiness() * 6;
            c.Save();
            for (int i = 0; i < 7; i++)
            {
                double u = ((this.t * 0.12 + i / 7.0 + seed) % 1 + 1) % 1;
                double px = x + 61 * k + u * u * wind * 3 * k + Math.Sin(this.t * 0.7 + i) * 2 * k;
                double py = y - 14 * k - u * 70 * k;
                double r = (3 + u * 10) * k;
                c.GlobalAlpha = 0.28 * (1 - u) * Math.Min(1, u * 6);
                c.BeginPath();
                c.Arc(px, py, r, 0, Math.PI * 2);
                c.Fill();
            }
            c.Restore();
        }

        private void drawAtlas(Ctx c, double x, double y, double k, double seed)
        {
            // the titan on one knee, the sky's sphere on his shoulders — and he is never
            // still: he breathes under it, and every so often heaves it higher and settles
            // it again, the whole sky lurching with him
            double heaveCycle = ((this.t + seed * 20) % 11) / 11;
            double heave = heaveCycle < 0.18 ? Math.Sin(heaveCycle / 0.18 * Math.PI) : 0;
            double breathe = Math.Sin(this.t * 1.1 + seed * 5) * 1.2;
            double lift = (breathe + heave * 7) * k;
            c.BeginPath();
            c.MoveTo(x - 40 * k, y);
            c.QuadraticCurveTo(x - 30 * k, y - 14 * k, x, y - 16 * k);
            c.QuadraticCurveTo(x + 30 * k, y - 14 * k, x + 40 * k, y);
            c.ClosePath();
            c.Fill();
            c.LineJoin = LineJoin.Round;
            c.LineCap = LineCap.Round;
            c.LineWidth = 7 * k;
            c.BeginPath();
            c.MoveTo(x - 4 * k, y - 46 * k - lift * 0.3);
            c.LineTo(x - 14 * k, y - 28 * k);
            c.LineTo(x - 30 * k, y - 18 * k);
            c.MoveTo(x + 2 * k, y - 46 * k - lift * 0.3);
            c.LineTo(x + 16 * k, y - 34 * k - lift * 0.2);
            c.LineTo(x + 14 * k, y - 16 * k);
            c.Stroke();
            c.LineWidth = 12 * k;
            c.BeginPath();
            c.MoveTo(x, y - 46 * k - lift * 0.3);
            c.QuadraticCurveTo(x + 2 * k, y - 62 * k - lift * 0.6, x + 8 * k, y - 72 * k - lift);
            c.Stroke();
            c.LineWidth = 5 * k;
            c.BeginPath();
            c.MoveTo(x + 6 * k, y - 70 * k - lift);
            c.LineTo(x - 6 * k, y - 84 * k - lift);
            c.LineTo(x - 8 * k, y - 98 * k - lift);
            c.MoveTo(x + 12 * k, y - 70 * k - lift);
            c.LineTo(x + 22 * k, y - 84 * k - lift);
            c.LineTo(x + 20 * k, y - 98 * k - lift);
            c.Stroke();
            c.BeginPath();
            c.Arc(x + 12 * k, y - 78 * k - lift, 5 * k, 0, Math.PI * 2);
            c.Fill();
            double sy = y - 118 * k - lift;
            c.BeginPath();
            c.Arc(x + 6 * k, sy, 24 * k, 0, Math.PI * 2);
            c.Fill();
            // the heavens turning on his back: their bands and a few stars
            c.Save();
            c.GlobalAlpha = 0.28;
            c.StrokeStyle = "#ffffff";
            c.LineWidth = 1.2 * k;
            double rot = this.t * 0.15 + seed;
            c.BeginPath();
            c.Ellipse(x + 6 * k, sy, 24 * k, 8 * k, -0.35 + Math.Sin(rot) * 0.1, 0, Math.PI * 2);
            c.Ellipse(x + 6 * k, sy, Math.Abs(Math.Cos(rot)) * 22 * k + 2 * k, 24 * k, 0.2, 0, Math.PI * 2);
            c.Stroke();
            c.FillStyle = "#fff6d8";
            for (int i = 0; i < 5; i++)
            {
                double a = rot * 1.3 + i * 1.7;
                double r = 20 * k * this.hash(i * 3.3 + seed);
                c.GlobalAlpha = 0.5 + 0.4 * Math.Sin(this.t * 2 + i);
                c.BeginPath();
                c.Arc(x + 6 * k + Math.Cos(a) * r, sy + Math.Sin(a) * r * 0.9, 1 * k, 0, Math.PI * 2);
                c.Fill();
            }
            c.Restore();
        }

        private void drawGate(Ctx c, double x, double y, double k, double seed)
        {
            // two tall columns and their lintel, alone on the ridge, fires burning on top,
            // a cypress beside them
            this.mat(c, 176, 160, 140);
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
            // the braziers on the lintel's ends
            c.BeginPath();
            c.Rect(x - 26 * k, y - 97 * k, 7 * k, 5 * k);
            c.Rect(x + 19 * k, y - 97 * k, 7 * k, 5 * k);
            c.Fill();
            this.farFire(c, x - 22.5 * k, y - 97 * k, k, seed);
            this.farFire(c, x + 22.5 * k, y - 97 * k, k, seed + 0.5);
            double sway = Math.Sin(this.t * 0.9 + seed) * 2.5 * k * this.windiness();
            this.mat(c, 46, 70, 44);
            c.BeginPath();
            c.MoveTo(x + 44 * k, y);
            c.QuadraticCurveTo(x + 36 * k, y - 40 * k, x + 45 * k + sway, y - 70 * k);
            c.QuadraticCurveTo(x + 54 * k, y - 40 * k, x + 46 * k, y);
            c.ClosePath();
            c.Fill();
        }

        private void drawColossus(Ctx c, double x, double y, double k, double seed)
        {
            // a giant's head of stone, fallen on its side and half sunk in the ground,
            // and the birds that wheel over it
            this.mat(c, 150, 140, 128);
            c.BeginPath();
            c.MoveTo(x - 50 * k, y);
            c.BezierCurveTo(x - 56 * k, y - 30 * k, x - 36 * k, y - 52 * k, x - 6 * k, y - 54 * k);
            c.LineTo(x + 6 * k, y - 50 * k);
            c.LineTo(x + 8 * k, y - 44 * k);
            c.LineTo(x + 22 * k, y - 36 * k);
            c.LineTo(x + 12 * k, y - 32 * k);
            c.LineTo(x + 14 * k, y - 26 * k);
            c.LineTo(x + 10 * k, y - 22 * k);
            c.QuadraticCurveTo(x + 18 * k, y - 12 * k, x + 10 * k, y);
            c.ClosePath();
            c.Fill();
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
            if (this.farLit)
            {
                // the face on its side: the hollow of the eye, the ridge of the brow and
                // the nose, the lips; moss in its cracks
                c.Save();
                c.FillStyle = this.farTone(40, 34, 32, 0.75);
                c.BeginPath();
                c.Ellipse(x - 4 * k, y - 34 * k, 5 * k, 3 * k, 0.4, 0, Math.PI * 2);
                c.Fill();
                c.StrokeStyle = this.farTone(214, 204, 188, 0.6);
                c.LineWidth = 0.9 * k;
                c.BeginPath();
                c.MoveTo(x - 10 * k, y - 40 * k); c.QuadraticCurveTo(x - 2 * k, y - 43 * k, x + 4 * k, y - 38 * k);
                c.MoveTo(x + 8 * k, y - 44 * k); c.LineTo(x + 20 * k, y - 36.5 * k);
                c.MoveTo(x + 9 * k, y - 24 * k); c.QuadraticCurveTo(x + 13 * k, y - 18 * k, x + 9 * k, y - 12 * k);
                c.Stroke();
                c.StrokeStyle = this.farTone(40, 34, 32, 0.6);
                c.BeginPath();
                c.MoveTo(x + 3 * k, y - 22 * k); c.LineTo(x + 11 * k, y - 21 * k);
                c.Stroke();
                c.FillStyle = this.farTone(70, 96, 52, 0.7);
                for (int i = 0; i < 6; i++)
                {
                    c.BeginPath();
                    c.Ellipse(x + (-40 + i * 7) * k, y - (4 + (i % 3) * 7) * k, 3.5 * k, 1.6 * k, 0.3, 0, Math.PI * 2);
                    c.Fill();
                }
                c.Restore();
                this.mat(c, 196, 184, 164);
            }
            c.BeginPath();
            for (int i = 0; i < 3; i++) c.Rect(x + 34 * k + i * 7 * k, y - (10 + i * 3) * k, 5 * k, (10 + i * 3) * k);
            c.Fill();
            this.mat(c, 40, 34, 36);
            c.LineWidth = 1.3 * k;
            c.LineCap = LineCap.Round;
            for (int i = 0; i < 4; i++)
            {
                double a = this.t * (0.5 + i * 0.07) + i * 1.6 + seed * 6;
                double bx = x - 10 * k + Math.Cos(a) * (40 + i * 8) * k;
                double by = y - 80 * k - i * 6 * k + Math.Sin(a) * 12 * k;
                double fl = Math.Sin(this.t * 8 + i) * 2.5 * k;
                c.BeginPath();
                c.MoveTo(bx - 5 * k, by - fl);
                c.QuadraticCurveTo(bx - 2 * k, by - 2 * k, bx, by);
                c.QuadraticCurveTo(bx + 2 * k, by - 2 * k, bx + 5 * k, by - fl);
                c.Stroke();
            }
        }

        private void drawCentaur(Ctx c, double x, double y, double k, double dir)
        {
            // half man, half horse, at the gallop along the skyline, his bow in his hand
            double g = this.t * 9;
            double bob = Math.Abs(Math.Sin(g)) * 2.5 * k;
            c.Save();
            c.Translate(x, y - bob);
            c.Scale(dir, 1);
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            // the horse's body
            c.BeginPath();
            c.Ellipse(0, -22 * k, 16 * k, 7 * k, 0, 0, Math.PI * 2);
            c.Fill();
            // four legs at the gallop
            c.LineWidth = 3 * k;
            c.BeginPath();
            double[] ph = { 0, 0.6, 3.1, 3.7 };
            double[] lx = { 11, 9, -11, -13 };
            for (int i = 0; i < 4; i++)
            {
                double a = Math.Sin(g + ph[i]);
                c.MoveTo(lx[i] * k, -18 * k);
                c.LineTo((lx[i] + a * 6) * k, -9 * k);
                c.LineTo((lx[i] + a * 9 - 2) * k, 0 + bob);
            }
            c.Stroke();
            // the tail, streaming
            c.LineWidth = 2.5 * k;
            c.BeginPath();
            c.MoveTo(-15 * k, -24 * k);
            c.QuadraticCurveTo(-24 * k, -26 * k + Math.Sin(g * 0.5) * 2 * k, -28 * k, -18 * k);
            c.Stroke();
            // the man's torso rising from the horse's shoulders, the bow held out ahead
            c.LineWidth = 6 * k;
            c.BeginPath();
            c.MoveTo(12 * k, -26 * k);
            c.LineTo(15 * k, -42 * k);
            c.Stroke();
            c.BeginPath();
            c.Arc(16 * k, -47 * k, 3.6 * k, 0, Math.PI * 2);
            c.Fill();
            c.LineWidth = 2.2 * k;
            c.BeginPath();
            c.MoveTo(15 * k, -39 * k);
            c.LineTo(26 * k, -40 * k);
            c.Stroke();
            c.LineWidth = 1.3 * k;
            c.BeginPath();
            c.Arc(27 * k, -40 * k, 9 * k, -1.2, 1.2);
            c.MoveTo(27 * k + Math.Cos(-1.2) * 9 * k, -40 * k + Math.Sin(-1.2) * 9 * k);
            c.LineTo(27 * k + Math.Cos(1.2) * 9 * k, -40 * k + Math.Sin(1.2) * 9 * k);
            c.Stroke();
            c.Restore();
        }

        private void drawCerberus(Ctx c, double x, double y, double k, double dir)
        {
            // the hound of the underworld pacing its stretch of the ridge, three heads up,
            // each looking its own way
            double g = this.t * 4;
            c.Save();
            c.Translate(x, y);
            c.Scale(dir, 1);
            c.LineCap = LineCap.Round;
            c.BeginPath();
            c.Ellipse(0, -16 * k, 15 * k, 7 * k, 0, 0, Math.PI * 2);
            c.Fill();
            c.LineWidth = 3.4 * k;
            c.BeginPath();
            double[] lx = { 10, 7, -9, -12 };
            for (int i = 0; i < 4; i++)
            {
                double a = Math.Sin(g + (i % 2) * Math.PI + (i > 1 ? 1.4 : 0));
                c.MoveTo(lx[i] * k, -12 * k);
                c.LineTo((lx[i] + a * 3) * k, 0);
            }
            c.Stroke();
            // the tail, a serpent's, lashing
            c.LineWidth = 2 * k;
            c.BeginPath();
            c.MoveTo(-14 * k, -18 * k);
            c.QuadraticCurveTo(-22 * k, -26 * k + Math.Sin(g) * 4 * k, -28 * k, -22 * k);
            c.Stroke();
            // three necks and heads
            for (int i = 0; i < 3; i++)
            {
                double a = -0.9 + i * 0.45 + Math.Sin(this.t * (1.3 + i * 0.4) + i) * 0.18;
                double nx = 12 * k + Math.Cos(a) * 12 * k, ny = -20 * k + Math.Sin(a) * 12 * k;
                c.LineWidth = 4 * k;
                c.BeginPath();
                c.MoveTo(10 * k, -18 * k);
                c.LineTo(nx, ny);
                c.Stroke();
                c.BeginPath();
                c.Ellipse(nx + 3 * k, ny, 5 * k, 3 * k, a * 0.4, 0, Math.PI * 2);
                c.Fill();
                // ears
                c.BeginPath();
                c.MoveTo(nx, ny - 2 * k);
                c.LineTo(nx - 1 * k, ny - 6 * k);
                c.LineTo(nx + 2 * k, ny - 3 * k);
                c.Fill();
            }
            c.Restore();
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
