using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// The year turning while he climbs: every six climbs a new season — summer, autumn,
    /// winter, spring — so the labour is seen to go on through time. Autumn sends leaves
    /// tumbling across the hill and warms the light; winter thins the colour, cools the
    /// shadows and lets a few flakes drift down on still, cold hours; spring brings the hill
    /// into flower whatever his mood and blows petals about. (The saga's own winter, of
    /// despair, and its spring, of a run of clean climbs, still come on top of this.)
    /// </summary>
    public partial class SisyphusEngine
    {
        internal const int CLIMBS_PER_SEASON = 6;

        /// <summary>0 summer, 1 autumn, 2 winter, 3 spring</summary>
        internal int season
        {
            get { return (int)(Math.Floor(Math.Max(0, this.cycles) / CLIMBS_PER_SEASON) % 4); }
        }

        /// <summary>at least this much flower on the hill, from the season (spring)</summary>
        internal double seasonBloom { get { return this.season == 3 ? 0.55 : 0; } }

        /// <summary>
        /// The season's drift across the frame: leaves in autumn (tumbling, turning their pale
        /// side to the light), flakes in winter (only in the cold hours, slow), petals in
        /// spring. A fixed set of particles, each on its own path, wrapping round the frame
        /// and moved by the wind.
        /// </summary>
        private void drawSeason()
        {
            int se = this.season;
            if (se == 0 || this.lowQuality || this.perfLight) return;
            var c = this.ctx;
            double w = this.w, h = this.h, s = this.scale;
            double cold = Math.Max(0, Math.Min(1, (8 - this.st.elev) / 10));
            int n = se == 1 ? 26 : se == 2 ? (int)(14 + 30 * cold) : 22;
            if (se == 2 && this.weather != "clear") return; // the weather has its own snow and rain
            double wind = this.windiness();
            c.Save();
            for (int i = 0; i < n; i++)
            {
                double hh = this.hash(i * 7.7 + se * 13), h2 = this.hash(i * 3.1 + 5), h3 = this.hash(i * 11.3 + 2);
                double fall = se == 2 ? 14 + h2 * 10 : se == 1 ? 26 + h2 * 20 : 18 + h2 * 14;
                double drift = (se == 2 ? 6 : 22) * wind + 8;
                double tt = this.t + hh * 100;
                // across and down the frame, wrapping; a sway on top
                double x = ((hh * w + tt * drift * s - this.camX * 0.25 * s) % (w + 40) + (w + 40)) % (w + 40) - 20;
                double y = ((h2 * h + tt * fall * s) % (h + 40) + (h + 40)) % (h + 40) - 20;
                x += Math.Sin(tt * (0.8 + h3)) * 14 * s;
                double spin = tt * (1.5 + h3 * 3);
                if (se == 1)
                {
                    // a leaf: oval, a midrib, rust to ochre, its underside paler as it turns
                    double turn = Math.Cos(spin);
                    double r = 160 + h3 * 70, g = 70 + h2 * 60, b = 20 + hh * 20;
                    double lite = 0.5 + 0.5 * turn;
                    c.Save();
                    c.Translate(x, y);
                    c.Rotate(spin * 0.7);
                    c.Scale(Math.Max(0.15, Math.Abs(turn)), 1);
                    c.FillStyle = Col.Rgba(r * (0.8 + 0.3 * lite), g * (0.8 + 0.3 * lite), b, 0.85);
                    c.BeginPath();
                    c.Ellipse(0, 0, 2.2 * s, 4 * s, 0, 0, Math.PI * 2);
                    c.Fill();
                    c.StrokeStyle = Col.Rgba(90, 40, 16, 0.6);
                    c.LineWidth = 0.4 * s;
                    c.BeginPath();
                    c.MoveTo(0, -4 * s); c.LineTo(0, 5 * s);
                    c.Stroke();
                    c.Restore();
                }
                else if (se == 2)
                {
                    double a = 0.35 + 0.5 * cold * (0.5 + 0.5 * h3);
                    c.FillStyle = Col.Rgba(236, 242, 255, a);
                    c.BeginPath();
                    c.Arc(x, y, (0.8 + h3 * 1.4) * s, 0, Math.PI * 2);
                    c.Fill();
                }
                else
                {
                    // a petal: almond-blossom pink and white, fluttering
                    double turn = Math.Cos(spin * 1.3);
                    c.Save();
                    c.Translate(x, y);
                    c.Rotate(spin);
                    c.Scale(Math.Max(0.2, Math.Abs(turn)), 1);
                    c.FillStyle = hh < 0.5 ? Col.Rgba(250, 214, 224, 0.85) : Col.Rgba(255, 244, 246, 0.85);
                    c.BeginPath();
                    c.Ellipse(0, 0, 1.4 * s, 2.2 * s, 0, 0, Math.PI * 2);
                    c.Fill();
                    c.Restore();
                }
            }
            c.Restore();
        }
    }
}
