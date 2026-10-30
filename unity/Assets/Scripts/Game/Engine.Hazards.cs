using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// The hill's hazards made felt as well as played: mud that sucks at each step and
    /// spatters, scree that crunches and sends stones rattling back down past him, gusts
    /// you can see coming — streaks of dust and blown leaves across the whole frame.
    /// </summary>
    public partial class SisyphusEngine
    {
        /// <summary>each footfall, by what is under it</summary>
        private void footing(bool onScree, bool inMire)
        {
            double fx = this.x - 64;
            if (inMire)
            {
                this.audio.squelch();
                for (int i = 0; i < 5; i++)
                {
                    this.particles.Add(new Particle
                    {
                        wx = fx + (Js.random() - 0.5) * 14,
                        wy = Levels.terrainAt(this.level, fx) + 1,
                        vx = (Js.random() - 0.6) * 70,
                        vy = 60 + Js.random() * 90,
                        life = 0,
                        maxLife = 0.6 + Js.random() * 0.3,
                        size = (0.9 + Js.random() * 1.2) * this.scale,
                        kind = "mud",
                    });
                }
            }
            else if (onScree)
            {
                this.audio.gravel();
                int n = 1 + (int)(Js.random() * 3);
                for (int i = 0; i < n; i++)
                {
                    this.particles.Add(new Particle
                    {
                        wx = fx + (Js.random() - 0.3) * 12,
                        wy = Levels.terrainAt(this.level, fx) + 2,
                        vx = -60 - Js.random() * 90,
                        vy = 30 + Js.random() * 60,
                        life = 0,
                        maxLife = 1.2 + Js.random() * 0.8,
                        size = (1.2 + Js.random() * 1.6) * this.scale,
                        kind = "stone",
                    });
                }
            }
        }

        private double gustSeen;

        /// <summary>
        /// A gust you can see: as it rises, long pale streaks of dust run across the frame
        /// with it, and leaves torn off the olives tumble past.
        /// </summary>
        private void drawGust()
        {
            double extra = this.windiness() - 1;
            double g = extra <= 0 ? 0 : Math.Pow(Math.Max(0, Math.Sin(this.t * 0.23 + 1.3)), 3) * Math.Min(1, extra * 1.4);
            this.gustSeen += (g - this.gustSeen) * Math.Min(1, this.frameDt * 3);
            if (this.gustSeen < 0.04 || this.lowQuality || this.dbgZoom > 0) return;
            var c = this.ctx;
            double w = this.w, h = this.h, s = this.scale;
            double a = this.gustSeen;
            c.Save();
            c.LineCap = LineCap.Round;
            // the wind blows down the hill (from the right, against him)
            for (int i = 0; i < 26; i++)
            {
                double hh = this.hash(i * 3.1 + 2), h2 = this.hash(i * 7.7 + 1);
                double sp = 0.45 + h2 * 0.5;
                double u = ((this.t * sp + hh) % 1 + 1) % 1;
                double x = w * (1.15 - u * 1.4);
                double y = h * (0.25 + hh * 0.65) + Math.Sin(this.t * 2 + i) * 6 * s;
                double len = (60 + h2 * 140) * s * (0.5 + a);
                double fade = Math.Sin(u * Math.PI);
                c.StrokeStyle = Col.Rgba(236, 222, 196, 0.16 * a * fade);
                c.LineWidth = (0.8 + h2 * 1.4) * s;
                c.BeginPath();
                c.MoveTo(x, y);
                c.QuadraticCurveTo(x + len * 0.5, y - 3 * s, x + len, y + 1 * s);
                c.Stroke();
            }
            // leaves tumbling through it
            int leaves = (int)(4 + a * 10);
            for (int i = 0; i < leaves; i++)
            {
                double hh = this.hash(i * 5.3 + 9), h2 = this.hash(i * 1.9 + 4);
                double u = ((this.t * (0.3 + h2 * 0.3) + hh) % 1 + 1) % 1;
                double x = w * (1.1 - u * 1.25);
                double y = h * (0.35 + hh * 0.5) + Math.Sin(this.t * 3 + i * 2) * 18 * s;
                double rot = this.t * (4 + h2 * 6) + i;
                c.Save();
                c.Translate(x, y);
                c.Rotate(rot);
                c.Scale(1, Math.Abs(Math.Cos(rot * 0.7)) * 0.8 + 0.2);
                c.FillStyle = h2 < 0.5 ? Col.Rgba(110, 120, 70, 0.85 * a) : Col.Rgba(150, 130, 70, 0.85 * a);
                c.BeginPath();
                c.Ellipse(0, 0, 3.2 * s, 1.3 * s, 0, 0, Math.PI * 2);
                c.Fill();
                c.Restore();
            }
            c.Restore();
        }
    }
}
