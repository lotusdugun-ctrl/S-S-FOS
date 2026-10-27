// Port of src/game/npcs.ts.
// Small things carried on the hill. The figures themselves live in figures.ts (Figures*.cs).
using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    public static class Npcs
    {
        /// <summary>a little thing in his hand on the way down</summary>
        public static void drawItem(Ctx ctx, double x, double y, double s, string item, double t)
        {
            ctx.Save();
            switch (item)
            {
                case "feather":
                    ctx.FillStyle = "oklch(0.35 0.03 60)";
                    ctx.BeginPath();
                    ctx.Ellipse(x, y - 4 * s, 1.6 * s, 5 * s, 0.4, 0, Math.PI * 2);
                    ctx.Fill();
                    break;
                case "thyme":
                case "poppy":
                    ctx.StrokeStyle = "oklch(0.45 0.1 140)";
                    ctx.LineWidth = 0.8 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(x, y);
                    ctx.LineTo(x - 1 * s, y - 7 * s);
                    ctx.Stroke();
                    ctx.FillStyle = item == "poppy" ? "oklch(0.58 0.22 28)" : "oklch(0.7 0.08 310)";
                    ctx.BeginPath();
                    ctx.Arc(x - 1 * s, y - 8 * s, (item == "poppy" ? 2.4 : 1.4) * s, 0, Math.PI * 2);
                    ctx.Fill();
                    break;
                case "snow":
                    ctx.FillStyle = "rgba(240, 246, 255, 0.95)";
                    ctx.BeginPath();
                    ctx.Arc(x, y - 2 * s, 2.6 * s, 0, Math.PI * 2);
                    ctx.Fill();
                    break;
                case "lethe":
                    {
                        // the water of forgetting, cupped in his hands: still, faintly cold-lit
                        var g = ctx.CreateRadialGradient(x, y - 2 * s, 0, x, y - 2 * s, 9 * s);
                        double a = 0.45 + Math.Sin(t * 2) * 0.12;
                        g.AddColorStop(0, Col.Rgba(200, 225, 255, a));
                        g.AddColorStop(1, "rgba(160, 190, 240, 0)");
                        ctx.FillStyle = g;
                        ctx.BeginPath();
                        ctx.Arc(x, y - 2 * s, 9 * s, 0, Math.PI * 2);
                        ctx.Fill();
                        break;
                    }
                case "light":
                case "understanding":
                    {
                        var g = ctx.CreateRadialGradient(x, y - 2 * s, 0, x, y - 2 * s, 8 * s);
                        double a = 0.5 + Math.Sin(t * 3) * 0.15;
                        g.AddColorStop(0, Col.Rgba(255, 236, 190, a));
                        g.AddColorStop(1, "rgba(255, 220, 160, 0)");
                        ctx.FillStyle = g;
                        ctx.BeginPath();
                        ctx.Arc(x, y - 2 * s, 8 * s, 0, Math.PI * 2);
                        ctx.Fill();
                        break;
                    }
                case "stone":
                case "pebble":
                case "shard":
                    ctx.FillStyle = "oklch(0.4 0.01 60)";
                    ctx.BeginPath();
                    ctx.Ellipse(x, y - 1.6 * s, 2.4 * s, 1.8 * s, 0.3, 0, Math.PI * 2);
                    ctx.Fill();
                    break;
                default:
                    // words, an answer, a look at the sea: nothing in the hand
                    break;
            }
            ctx.Restore();
        }
    }
}
