using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// Moments that make the climb more than a loop:
    /// - the flow: five perfect heaves in a row and he is in it — a chord opens under the
    ///   music, gold motes rise round him and the stone, the light blooms a little;
    /// - cairns along the path, one for each stretch of the Book he has filled: the hill
    ///   keeps what he has learned, a lamp burning on each after dark;
    /// - the small things on the way: a goat bolting across the slope with its bell, a
    ///   rattle of pebbles coming down past him, a traveller on his way down who wishes him
    ///   well;
    /// - the sky's rare gifts: a rainbow after rain, a night of falling stars;
    /// - his voice: humming as he walks down, a breath let out at the top — and, near the end
    ///   of the story, whistling. One must imagine Sisyphus happy.
    /// </summary>
    public partial class SisyphusEngine
    {
        // ---------------------------------------------------------------- the flow

        /// <summary>0..1: how far he is into the flow of a run of perfect heaves</summary>
        internal double flow;

        private void updateMoments(double dt)
        {
            bool inFlow = this.phase == "playing" && this.streak >= 5;
            this.flow += ((inFlow ? 1 : 0) - this.flow) * Math.Min(1, dt * (inFlow ? 0.8 : 0.5));
            if (this.flow < 0.002) this.flow = 0;
            this.audio.setFlow(this.flow);
            this.updateEvent(dt);
            this.updateVoice(dt);
        }

        /// <summary>gold motes rising round him and the stone while he is in the flow</summary>
        private void drawFlow(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (this.flow < 0.02) return;
            var c = this.ctx;
            double s = this.scale;
            double cx = toScreenX(this.x - 20), cy = toScreenY(Levels.terrainAt(this.level, this.x - 20));
            c.Save();
            c.GlobalCompositeOperation = Composite.Lighter;
            for (int i = 0; i < 18; i++)
            {
                double hh = this.hash(i * 4.7 + 1), h2 = this.hash(i * 2.3 + 8);
                double u = ((this.t * (0.25 + h2 * 0.2) + hh) % 1 + 1) % 1;
                double x = cx + (hh - 0.5) * 120 * s + Math.Sin(this.t * 1.3 + i) * 6 * s;
                double y = cy - u * 110 * s;
                double a = this.flow * Math.Sin(u * Math.PI) * 0.8;
                double r = (1 + h2 * 1.6) * s;
                var g = c.CreateRadialGradient(x, y, 0, x, y, r * 4);
                g.AddColorStop(0, Col.Rgba(255, 220, 140, a));
                g.AddColorStop(1, "rgba(255, 200, 120, 0)");
                c.FillStyle = g;
                c.FillRect(x - r * 4, y - r * 4, r * 8, r * 8);
            }
            c.Restore();
        }

        // ---------------------------------------------------------------- cairns

        static readonly int[] CAIRN_AT = { 5, 15, 30, 50, 75, 100, 150, 200, 300, 400, 500, 650, 800, 1000 };

        /// <summary>
        /// The cairns he has raised: one for each step of the Book filled (five sayings, then
        /// fifteen, thirty...), set along the path at their own places, stones stacked as a
        /// man stacks them, a small lamp on top that is lit when the light goes.
        /// </summary>
        private void drawCairns(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            int have = this.unlocked != null ? this.unlocked.Count : 0;
            int n = 0;
            while (n < CAIRN_AT.Length && have >= CAIRN_AT[n]) n++;
            if (n == 0) return;
            var c = this.ctx;
            double s = this.scale;
            double night = Math.Max(0, Math.Min(1, (6 - this.st.elev) / 8));
            var L = this.npcLight();
            for (int i = 0; i < n; i++)
            {
                double wx = this.level.length * (0.1 + i * 0.061) + 26;
                double sx = toScreenX(wx);
                if (sx < -30 || sx > this.w + 30) continue;
                double sy = toScreenY(Levels.terrainAt(this.level, wx)) + 2 * s;
                double lit = L.lx > 0 ? 1 : -1;
                int stones = 4 + (i % 3);
                double y = sy;
                for (int j = 0; j < stones; j++)
                {
                    double rw = (7 - j * 1.1 + this.hash(i * 9 + j) * 1.6) * s, rh = (3.2 - j * 0.3) * s;
                    double ox = (this.hash(i * 3 + j * 5) - 0.5) * 2.4 * s;
                    double tone = 96 + this.hash(i + j * 7) * 40;
                    c.FillStyle = Col.Rgba(tone, tone * 0.92, tone * 0.86, 1);
                    c.BeginPath();
                    c.Ellipse(sx + ox, y - rh, rw, rh, 0, 0, Math.PI * 2);
                    c.Fill();
                    c.FillStyle = Col.Rgba(255, 230, 190, 0.25 * L.strength);
                    c.BeginPath();
                    c.Ellipse(sx + ox + lit * rw * 0.3, y - rh * 1.5, rw * 0.5, rh * 0.4, 0, 0, Math.PI * 2);
                    c.Fill();
                    y -= rh * 1.7;
                }
                // the lamp: a small clay lamp, its flame lit as the light goes
                c.FillStyle = "rgba(150, 80, 44, 1)";
                c.BeginPath();
                c.Ellipse(sx, y - 1.2 * s, 2.6 * s, 1.2 * s, 0, 0, Math.PI * 2);
                c.Fill();
                if (night > 0.05)
                {
                    double f = 0.8 + 0.2 * Math.Sin(this.t * 9 + i * 3);
                    c.Save();
                    c.GlobalCompositeOperation = Composite.Lighter;
                    var g = c.CreateRadialGradient(sx + 2 * s, y - 4 * s, 0, sx + 2 * s, y - 4 * s, 14 * s);
                    g.AddColorStop(0, Col.Rgba(255, 170, 70, 0.45 * night * f));
                    g.AddColorStop(1, "rgba(255, 150, 60, 0)");
                    c.FillStyle = g;
                    c.FillRect(sx - 12 * s, y - 18 * s, 28 * s, 28 * s);
                    c.Restore();
                    c.FillStyle = Col.Rgba(255, 214, 130, 0.95 * night);
                    c.BeginPath();
                    c.MoveTo(sx + 1.2 * s, y - 2 * s);
                    c.QuadraticCurveTo(sx + 1.6 * s, y - 5 * s * f, sx + 2.4 * s + Math.Sin(this.t * 7 + i) * 0.4 * s, y - 7 * s * f);
                    c.QuadraticCurveTo(sx + 3 * s, y - 4 * s, sx + 3.2 * s, y - 2 * s);
                    c.ClosePath();
                    c.Fill();
                }
            }
        }

        // ---------------------------------------------------------------- the small things

        private sealed class WayEvent
        {
            public string kind;   // goat, pebbles, traveller
            public double at;     // world x where it happens
            public double t = -1; // seconds since it began, -1 before
        }
        private WayEvent wayEvent;
        private double eventForCycle = -1;

        /// <summary>greetings from a traveller going down, in each language</summary>
        static string Greeting(string lang, int i)
        {
            string[][] g =
            {
                new[] { "Kolay gelsin.", "Yolun açık olsun.", "Tanrılar seninle olsun." },
                new[] { "May the work go easy.", "A clear road to you.", "The gods be with you." },
                new[] { "辛苦了。", "一路顺风。", "愿众神与你同在。" },
                new[] { "काम आसान हो।", "राह आसान हो।", "देवता तुम्हारे साथ हों।" },
                new[] { "Que te sea leve.", "Buen camino.", "Que los dioses te acompañen." },
                new[] { "Bon courage.", "Bonne route.", "Que les dieux soient avec toi." },
                new[] { "كان الله في عونك.", "طريق سالكة.", "لتكن الآلهة معك." },
                new[] { "কাজ সহজ হোক।", "পথ শুভ হোক।", "দেবতারা তোমার সঙ্গে থাকুন।" },
                new[] { "Bom trabalho.", "Bom caminho.", "Que os deuses te acompanhem." },
            };
            int li = lang == "tr" ? 0 : lang == "en" ? 1 : lang == "zh" ? 2 : lang == "hi" ? 3 : lang == "es" ? 4 : lang == "fr" ? 5 : lang == "ar" ? 6 : lang == "bn" ? 7 : lang == "pt" ? 8 : 1;
            return g[li][i % 3];
        }

        private void updateEvent(double dt)
        {
            if (this.lowQuality || this.perfLight) { this.wayEvent = null; return; }
            // one small thing on about every other climb, somewhere in its middle stretch
            if (this.eventForCycle != this.cycles)
            {
                this.eventForCycle = this.cycles;
                double hh = this.hash(this.cycles * 2.17 + 0.5);
                this.wayEvent = hh < 0.55
                    ? new WayEvent { kind = hh < 0.2 ? "goat" : hh < 0.38 ? "pebbles" : "traveller", at = this.level.length * (0.3 + this.hash(this.cycles * 5.3) * 0.4) }
                    : null;
            }
            var e = this.wayEvent;
            if (e == null || this.phase != "playing") return;
            if (e.t < 0)
            {
                // it begins a little ahead of him
                if (this.x > e.at - 260 && this.x < e.at) { e.t = 0; this.eventSound(e); }
                return;
            }
            e.t += dt;
            if (e.t > 9) this.wayEvent = null;
        }

        private void eventSound(WayEvent e)
        {
            if (e.kind == "goat") this.audio.goatBell(0.8);
            else if (e.kind == "pebbles") this.audio.pebbles(0.8);
        }

        private void drawWayEvent(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            var e = this.wayEvent;
            if (e == null || e.t < 0) return;
            var c = this.ctx;
            double s = this.scale;
            double k = s * 1.15;
            if (e.kind == "goat")
            {
                // it bolts down across the slope ahead of him, bell ringing, and is gone
                double wx = e.at - e.t * 120;
                double sx = toScreenX(wx), sy = toScreenY(Levels.terrainAt(this.level, wx)) + 1 * s;
                double g = e.t * 14;
                double leap = Math.Abs(Math.Sin(g * 0.5)) * 5 * s;
                c.Save();
                c.FillStyle = "rgba(150, 116, 86, 1)";
                c.StrokeStyle = "rgba(120, 90, 66, 1)";
                c.LineWidth = 1.4 * s;
                c.Translate(sx, sy - leap);
                c.Scale(-1, 1);
                for (int i = 0; i < 4; i++)
                {
                    double lx = (i < 2 ? 6 : -6) * s, sw = Math.Sin(g + i * 1.6) * 3 * s;
                    c.BeginPath();
                    c.MoveTo(lx, -8 * s);
                    c.LineTo(lx + sw, 0);
                    c.Stroke();
                }
                c.BeginPath();
                c.Ellipse(0, -10 * s, 9 * s, 4.4 * s, -0.1, 0, Math.PI * 2);
                c.Fill();
                c.BeginPath();
                c.Ellipse(10 * s, -15 * s, 3.4 * s, 2.4 * s, 0.4, 0, Math.PI * 2);
                c.Fill();
                c.StrokeStyle = "rgba(70, 58, 48, 1)";
                c.LineWidth = 1.1 * s;
                c.BeginPath();
                c.MoveTo(9 * s, -17 * s); c.QuadraticCurveTo(6 * s, -22 * s, 3 * s, -20 * s);
                c.Stroke();
                c.FillStyle = "rgba(200, 160, 80, 1)";
                c.BeginPath();
                c.Arc(8 * s, -11.5 * s, 1.2 * s, 0, Math.PI * 2);
                c.Fill();
                c.Restore();
            }
            else if (e.kind == "pebbles")
            {
                // a handful of stones bounding down the path past him
                for (int i = 0; i < 6; i++)
                {
                    double d = Math.Max(0, e.t - i * 0.12);
                    double wx = e.at + 120 - d * (150 + i * 12);
                    double sx = toScreenX(wx), sy = toScreenY(Levels.terrainAt(this.level, wx));
                    double bounce = Math.Abs(Math.Sin(d * (7 + i))) * (8 - Math.Min(7, d)) * s;
                    c.FillStyle = Col.Rgba(110 + i * 8, 100 + i * 6, 92 + i * 5, 1);
                    c.BeginPath();
                    c.Ellipse(sx, sy - (2 + i % 3) * s - bounce, (2 + i % 3) * s, (1.6 + i % 2) * s, d * 6, 0, Math.PI * 2);
                    c.Fill();
                }
            }
            else
            {
                // a traveller on his way down: he slows as he passes, says a word, walks on
                double wx = e.at + 200 - e.t * 70;
                double sx = toScreenX(wx), sy = toScreenY(Levels.terrainAt(this.level, wx)) + 2 * s;
                var p = Gait(e.t * 5, 0, 0);
                var lk = Skin(170, 116, 84);
                lk.dress = 1; lk.clothR = 120; lk.clothG = 96; lk.clothB = 70; lk.cloak = true; lk.beard = true;
                // he is on the path, near: none of the far air on him
                bool lit0 = this.farLit;
                double air0 = this.farAir, dark0 = this.farDark;
                this.farLit = true; this.farAir = 0; this.farDark = -0.5;
                this.person(c, sx, sy, 70 * s, -1, p, lk);
                this.farLit = lit0; this.farAir = air0; this.farDark = dark0;
                // his staff
                c.StrokeStyle = "rgba(96, 70, 46, 1)";
                c.LineWidth = 1.6 * s;
                c.BeginPath();
                c.MoveTo(sx - 12 * s, sy);
                c.LineTo(sx - 9 * s, sy - 66 * s);
                c.Stroke();
                double near = Math.Abs(wx - this.x);
                if (near < 260 && e.t > 0.4)
                {
                    string text = Greeting(this.lang, (int)(this.cycles % 3));
                    double a = Math.Max(0, Math.Min(1, (260 - near) / 80));
                    c.Save();
                    c.Font = "italic 500 " + Js.round(13 * s) + "px Cormorant Garamond";
                    c.TextAlign = TextAlign.Center;
                    c.FillStyle = Col.Rgba(20, 14, 10, 0.45 * a);
                    c.FillText(text, sx + 1, sy - 82 * s + 1);
                    c.FillStyle = Col.Rgba(246, 232, 200, 0.95 * a);
                    c.FillText(text, sx, sy - 82 * s);
                    c.Restore();
                }
            }
        }

        // ---------------------------------------------------------------- the sky's gifts

        /// <summary>
        /// A rainbow: after rain (the climb before was wet), or now and then on a clear
        /// afternoon with the sun low behind him — an arc in the sky opposite the sun, its
        /// bands soft, brighter at the foot, a faint second bow outside it.
        /// </summary>
        internal static bool dbgRainbow;

        private void drawRainbow()
        {
            if (this.lowQuality || this.weather != "clear") return;
            bool afterRain = this.saga.weatherFor(Math.Max(0, this.cycles - 1), weatherFor(Math.Max(0, this.cycles - 1))) != "clear";
            bool rare = dbgRainbow || this.hash(this.cycles * 3.71 + 1.3) < 0.06;
            if (!afterRain && !rare) return;
            double elev = this.st.elev;
            if (elev < 3 || elev > 38) return;
            double a = Math.Min(1, (elev - 3) / 6) * Math.Min(1, (38 - elev) / 10) * 0.32;
            var c = this.ctx;
            double w = this.w, h = this.h;
            // away from the sun: on the far side of the sky from it, its foot on the hills
            double sx = this.st.sunX;
            double cx = sx < 0.5 ? w * (0.86 - sx * 0.2) : w * (0.14 + (1 - sx) * 0.2), cy = h * 0.9;
            double R = h * (0.5 + elev / 200);
            Col[] bands =
            {
                Col.Rgba(130, 70, 170, 1), Col.Rgba(70, 100, 200, 1), Col.Rgba(80, 180, 120, 1),
                Col.Rgba(235, 220, 110, 1), Col.Rgba(240, 160, 80, 1), Col.Rgba(220, 80, 70, 1),
            };
            c.Save();
            for (int bow = 0; bow < 2; bow++)
            {
                double r0 = R * (bow == 0 ? 1 : 1.16);
                double bw = h * (bow == 0 ? 0.011 : 0.008);
                for (int i = 0; i < bands.Length; i++)
                {
                    var col = bands[bow == 0 ? i : bands.Length - 1 - i];
                    // soft: each band bleeds into the next
                    c.StrokeStyle = Col.Rgba(col.r * 255, col.g * 255, col.b * 255, a * 0.55 * (bow == 0 ? 1 : 0.4));
                    c.LineWidth = bw * 1.8;
                    c.BeginPath();
                    c.Arc(cx, cy, r0 + i * bw, Math.PI * 1.02, Math.PI * 1.98);
                    c.Stroke();
                }
            }
            c.Restore();
        }

        /// <summary>is this one of the nights the stars fall (one night in seven or so)</summary>
        internal bool starfallNight { get { return this.hash(this.cycles * 1.93 + 4.4) < 0.15; } }

        // ---------------------------------------------------------------- his voice

        private double voiceT = 4;

        private void updateVoice(double dt)
        {
            this.voiceT -= dt;
            if (this.voiceT > 0) return;
            bool late = this.saga.stage >= 3;
            if (this.phase == "descent" && this.walkV > 20)
            {
                if (late && Js.random() < 0.7) this.audio.whistle(this.calm ? 0.6 : 1);
                else this.audio.hum(1);
                this.voiceT = late ? 7 + Js.random() * 6 : 12 + Js.random() * 10;
            }
            else this.voiceT = 2;
        }
    }
}
