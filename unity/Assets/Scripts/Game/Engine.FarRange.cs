using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// A far range of peaks behind the great mountain, pale with distance, the way a second
    /// range stands in the haze behind the Dolomites. It gives the sky depth: hill, mountain,
    /// range, sky. The silhouette is painted once into a picture per hill (and again only when
    /// the air's colour has changed enough to see), then only drawn, twice side by side, sliding
    /// slower than the mountain.
    /// </summary>
    public partial class SisyphusEngine
    {
        private Surface farRange = null;
        private string farRangeKey = "";
        private const int FAR_W = 2048, FAR_H = 320;

        /// <summary>the range's skyline in its picture, 0 (bottom) to 1 (the highest peak)</summary>
        private static double farRidge(double u, double seed)
        {
            double y = 0.18;
            // a line of peaks, each a cone with its own height and width
            for (int i = 0; i < 12; i++)
            {
                double c = (i + 0.5) / 12 + (MountainGen.hash(i, 1, seed) - 0.5) * 0.06;
                double hgt = 0.3 + MountainGen.hash(i, 2, seed) * 0.62;
                double wdt = 0.06 + MountainGen.hash(i, 3, seed) * 0.08;
                // the picture is tiled: measure the distance round the seam
                double d = Math.Abs(u - c);
                d = Math.Min(d, 1 - d);
                double k = Math.Max(0, 1 - d / wdt);
                y = Math.Max(y, hgt * Math.Pow(k, 1.6));
            }
            // crags along the crest
            y += (MountainGen.ridged(u * 90, 0.5, seed + 7, 4) - 0.3) * 0.12 * (0.3 + y) + (MountainGen.fbm(u * 24, 1.5, seed + 3, 3) - 0.5) * 0.1;
            return Math.Max(0.05, Math.Min(1, y));
        }

        /// <summary>
        /// draw the range: `baseY` is where the mountain's foot is on screen, `p` the mountain's
        /// parallax (the range slides at half of it), `hz` the colour of the air, `wet` how grey the
        /// weather makes it
        /// </summary>
        private void drawFarRange(double baseY, double p, RGB hz, double wet)
        {
            if (this.lowQuality) return;
            double seed = 13;
            for (int i = 0; i < this.level.id.Length; i++) seed = (seed * 37 + this.level.id[i]) % 100019;
            // the far rock: the air's colour, a little deeper, cooler, greyer in rain
            double lr = hz[0] * 0.8, lg = hz[1] * 0.83, lb = hz[2] * 0.92;
            lr += (128 - lr) * wet * 0.5;
            lg += (132 - lg) * wet * 0.5;
            lb += (140 - lb) * wet * 0.5;
            string key = this.level.id + "|" + Js.str(Js.round(lr / 6)) + "|" + Js.str(Js.round(lg / 6)) + "|" + Js.str(Js.round(lb / 6));
            if (this.farRange == null) this.farRange = new Surface(FAR_W, FAR_H, true, "far-range");
            if (this.farRangeKey != key)
            {
                this.farRangeKey = key;
                Ctx c = this.farRange.GetContext();
                if (c == null) return;
                c.GlobalCompositeOperation = Composite.Copy;
                c.FillStyle = "rgba(0, 0, 0, 0)";
                c.FillRect(0, 0, FAR_W, FAR_H);
                c.GlobalCompositeOperation = Composite.SourceOver;
                var grad = c.CreateLinearGradient(0, 0, 0, FAR_H);
                grad.AddColorStop(0, Css.rgb(new RGB(lr, lg, lb), 0.92));
                grad.AddColorStop(0.55, Css.rgb(new RGB(lr * 1.04, lg * 1.04, lb * 1.03), 0.8));
                grad.AddColorStop(1, Css.rgb(new RGB(hz[0] * 0.97, hz[1] * 0.97, hz[2] * 0.98), 0.3));
                c.FillStyle = grad;
                c.BeginPath();
                c.MoveTo(0, FAR_H);
                const int STEP = 6;
                for (int x = 0; x <= FAR_W; x += STEP)
                {
                    double y = FAR_H * (1 - farRidge((double)x / FAR_W, seed) * 0.92);
                    c.LineTo(x, y);
                }
                c.LineTo(FAR_W, FAR_H);
                c.ClosePath();
                c.Fill();
                // light on the crests from above
                c.StrokeStyle = Css.rgb(new RGB(Math.Min(255, hz[0] * 1.08), Math.Min(255, hz[1] * 1.06), Math.Min(255, hz[2] * 1.04)), 0.45);
                c.LineWidth = 2;
                c.BeginPath();
                for (int x = 0; x <= FAR_W; x += STEP)
                {
                    double y = FAR_H * (1 - farRidge((double)x / FAR_W, seed) * 0.92) + 1;
                    if (x == 0) c.MoveTo(x, y);
                    else c.LineTo(x, y);
                }
                c.Stroke();
            }
            double dw = FAR_W * this.scale * 0.95;
            double dh = FAR_H * this.scale * 0.95;
            double off = -(this.camX * p * 0.5 * this.scale) % dw;
            if (off > 0) off -= dw;
            var ctx = this.ctx;
            ctx.Save();
            ctx.ImageSmoothingEnabled = true;
            for (double x = off; x < this.w; x += dw)
                ctx.DrawImage(this.farRange, x, baseY - dh, dw, dh);
            ctx.Restore();
        }
    }
}
