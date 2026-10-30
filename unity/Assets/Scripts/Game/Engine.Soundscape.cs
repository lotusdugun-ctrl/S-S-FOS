using System;
using System.Collections.Generic;

namespace Sisifos.Game
{
    /// <summary>
    /// What the far figures sound like, faintly: the battle's bronze and its shouting, the
    /// lion's roar, the centaur's bowstring, a bell in a hamlet, the flock bleating. Each
    /// frame the ridges note which of them are on screen (and where); the next update decides
    /// whether one of them is heard, louder the nearer the ridge and the nearer the middle of
    /// the frame, duller the further off, and softer in a scene or in the rain.
    /// </summary>
    public partial class SisyphusEngine
    {
        private struct Heard { public int kind; public double sx; public int layer; }
        private readonly List<Heard> heard = new List<Heard>();
        private readonly List<Heard> heardPrev = new List<Heard>();
        private readonly Dictionary<int, double> nextSound = new Dictionary<int, double>();
        private double lastFly = -1;

        /// <summary>a ridge figure (kind as in drawLandmark; 100 + kind for ridge life) is on screen at sx</summary>
        private void hear(int kind, double sx, int layer)
        {
            this.heard.Add(new Heard { kind = kind, sx = sx, layer = layer });
        }

        /// <summary>called as a frame starts drawing: what was seen last frame becomes what may be heard</summary>
        private void hearSwap()
        {
            this.heardPrev.Clear();
            this.heardPrev.AddRange(this.heard);
            this.heard.Clear();
        }

        /// <summary>
        /// The far world's own clock: in a story scene it all but stops — the battle holds
        /// its breath, the hero and the lion stand locked — and runs on when the scene ends.
        /// </summary>
        internal double farClock;

        private void soundscape()
        {
            this.farClock += this.frameDt * (this.cutscene ? 0.1 : 1);
            if (this.audio == null || this.lowQuality || this.dbgNoFar) return;
            double now = this.t;
            double damp = (this.cutscene ? 0.5 : 1) * (this.weather == "clear" ? 1 : 0.55);
            double fly = this.centaurFly();
            bool loosed = this.lastFly < 0 && fly >= 0;
            this.lastFly = fly;
            foreach (var h in this.heardPrev)
            {
                double centre = 1 - Math.Min(1, Math.Abs(h.sx - this.w / 2) / this.w) * 0.7;
                double vol = (h.layer == 0 ? 0.35 : h.layer == 1 ? 0.45 : 0.6) * centre * damp;
                double muffle = h.layer == 0 ? 0.72 : h.layer == 1 ? 0.55 : 0.38;
                if (h.kind == 4 && loosed) this.audio.farTwang(vol, muffle);
                double next;
                if (this.nextSound.TryGetValue(h.kind, out next) && now < next) continue;
                switch (h.kind)
                {
                    case 6:
                        this.audio.farClash(vol, muffle);
                        if (Js.random() < 0.18) this.audio.farShout(vol, muffle);
                        this.nextSound[h.kind] = now + 0.7 + Js.random() * 1.4;
                        break;
                    case 7:
                        if (this.nextSound.ContainsKey(h.kind)) this.audio.farRoar(vol * 1.3, muffle);
                        this.nextSound[h.kind] = now + 7 + Js.random() * 8;
                        break;
                    case 100:
                        if (this.nextSound.ContainsKey(h.kind)) this.audio.farBell(vol, muffle);
                        this.nextSound[h.kind] = now + 16 + Js.random() * 20;
                        break;
                    case 101:
                        if (this.nextSound.ContainsKey(h.kind)) this.audio.farBleat(vol, muffle, Js.random() < 0.4);
                        this.nextSound[h.kind] = now + 2.5 + Js.random() * 5;
                        break;
                }
            }
        }

        /// <summary>an eagle circling over (cx, cy): wings spread and flexing, the primaries fingered, banking round</summary>
        private void circlingEagle(Paint.Ctx c, double cx, double cy, double s)
        {
            double a = this.t * 0.45;
            double x = cx + Math.Cos(a) * 70 * s, y = cy + Math.Sin(a) * 18 * s;
            double bank = Math.Sin(a) * 0.25;
            double face = -Math.Sin(a) >= 0 ? 1 : -1;
            double flex = Math.Sin(this.t * 1.7) * 0.12;
            double k = s * 1.1;
            c.Save();
            c.Translate(x, y);
            c.Rotate(bank);
            c.Scale(face * k, k);
            c.FillStyle = "rgba(40, 30, 26, 0.92)";
            // the wings: broad, the primaries spread like fingers at the tips
            for (int side = -1; side <= 1; side += 2)
            {
                c.BeginPath();
                c.MoveTo(0, 0);
                c.QuadraticCurveTo(side * 10, -6 - flex * 20, side * 26, -3 - flex * 30);
                for (int f = 0; f < 4; f++)
                    c.LineTo(side * (27 - f * 2.2), -1 - flex * 28 + f * 2.2);
                c.QuadraticCurveTo(side * 12, 4, 0, 3);
                c.ClosePath();
                c.Fill();
            }
            // body, the fanned tail, the head
            c.BeginPath();
            c.Ellipse(0, 1, 3, 7, 0, 0, Math.PI * 2);
            c.Fill();
            c.BeginPath();
            c.MoveTo(-2.4, 6); c.LineTo(-4.6, 13); c.LineTo(4.6, 13); c.LineTo(2.4, 6);
            c.ClosePath();
            c.Fill();
            c.BeginPath();
            c.Arc(0, -6.4, 2.2, 0, Math.PI * 2);
            c.Fill();
            c.Restore();
        }
    }
}
