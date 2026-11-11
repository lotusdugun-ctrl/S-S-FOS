using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// A far range of peaks behind the great mountain, the way a second range stands in the
    /// haze behind the Dolomites. It is painted the same way as the mountain (ridges, rock faces,
    /// the sun on one side and the sky in the shadows), then sunk deep into the air: cooler, paler,
    /// its foot lost in the haze. It gives the sky depth: hill, mountain, range, sky. Painted once
    /// per hill (in the background), and its light and air composed again only when the hour has
    /// moved enough to see.
    /// </summary>
    public partial class SisyphusEngine
    {
        private MountainPainter farPainter = null;
        private string farPainterFor = "";
        private Surface farComp = null;
        private string farCompKey = "";

        /// <summary>
        /// draw the range: `baseY` is where the mountain's foot is on screen, `p` the mountain's
        /// parallax (the range slides at half of it), `hz` the colour of the air, `wet` how grey the
        /// weather makes it, `sunK` which side the sun lights it from (0 left, 1 right), `back` how
        /// much a low sun leaves its face in shadow
        /// </summary>
        private void drawFarRange(double baseY, double p, RGB hz, double wet, double sunK = 0, double back = 0)
        {
            if (this.lowQuality) return;
            var mt = this.level.mountain;
            if (mt == null) return;
            if (this.farPainter == null || this.farPainterFor != this.level.id)
            {
                double seed = 13;
                for (int i = 0; i < this.level.id.Length; i++) seed = (seed * 37 + this.level.id[i]) % 100019;
                RGB rock;
                if (!MOUNTAIN_ROCK.TryGetValue(this.level.id, out rock)) rock = new RGB(190, 176, 164);
                // wider than the screen ever shows, lower than the mountain
                double width = 1900, height = mt.height * 0.84 * FAR_SIZE * 0.62;
                this.farPainter = new MountainPainter(
                    new MountainSpec { x = 0, height = height, width = width, snow = true, seed = seed, rock = rock, snowLine = 0.74, range = true },
                    0.62);
                this.farPainterFor = this.level.id;
                this.farCompKey = "";
            }
            var mp = this.farPainter;
            if (!mp.ready)
            {
                mp.step(1.5);
                return;
            }
            // the air: the haze's colour, cooler; more of it in rain
            double ar = hz[0] * 0.92, ag = hz[1] * 0.95, ab = Math.Min(255, hz[2] * 1.02);
            ar += (150 - ar) * wet * 0.6;
            ag += (156 - ag) * wet * 0.6;
            ab += (166 - ab) * wet * 0.6;
            string key = Js.str(Js.round(ar / 5)) + "|" + Js.str(Js.round(ag / 5)) + "|" + Js.str(Js.round(ab / 5)) + "|" + Js.str(Js.round(sunK * 20)) + "|" + Js.str(Js.round(back * 20));
            int cw = mp.W, ch = mp.H;
            if (this.farComp == null) this.farComp = new Surface(cw, ch, true, "far-range");
            if (this.farCompKey != key)
            {
                this.farCompKey = key;
                if (this.farComp.width != cw || this.farComp.height != ch) this.farComp.SetSize(cw, ch);
                Ctx c = this.farComp.GetContext();
                if (c == null) return;
                c.GlobalCompositeOperation = Composite.Copy;
                c.GlobalAlpha = 1;
                c.DrawImage(mp.a, 0, 0, cw, ch);
                c.GlobalCompositeOperation = Composite.SourceOver;
                if (sunK > 0.01)
                {
                    c.GlobalAlpha = sunK;
                    c.DrawImage(mp.b, 0, 0, cw, ch);
                    c.GlobalAlpha = 1;
                }
                // deep in the air: the range keeps its light and shade, faintly, in the haze's colour
                c.GlobalCompositeOperation = Composite.SourceAtop;
                if (back > 0.01)
                {
                    c.FillStyle = Css.rgb(new RGB(ar * 0.6, ag * 0.62, ab * 0.72), 0.4 * back);
                    c.FillRect(0, 0, cw, ch);
                }
                var air = c.CreateLinearGradient(0, 0, 0, ch);
                air.AddColorStop(0, Css.rgb(new RGB(ar, ag, ab), 0.52));
                air.AddColorStop(0.7, Css.rgb(new RGB(ar, ag, ab), 0.68));
                air.AddColorStop(1, Css.rgb(new RGB(ar, ag, ab), 0.9));
                c.FillStyle = air;
                c.FillRect(0, 0, cw, ch);
                c.GlobalCompositeOperation = Composite.SourceOver;
            }
            // placed against the screen: centred on it half way up the climb, sliding at half the
            // mountain's speed
            double pr = p * 0.5;
            double camMid = (this.level.length - 460) * 0.5;
            double ox = (1280 / 1.2) * 0.5 + camMid * pr;
            double sx = (mp.x0 + ox - this.camX * pr) * this.scale;
            double sw = mp.W * mp.kx * this.scale;
            double sh = mp.H * mp.ky * this.scale * 0.5;
            // its foot a little under the mountain's: further off, lower on the horizon
            double sy = baseY - (mp.top - mp.spec.height * 0.05) * this.scale * 0.5;
            if (sx > this.w || sx + sw < 0) return;
            var ctx = this.ctx;
            ctx.Save();
            ctx.ImageSmoothingEnabled = true;
            ctx.DrawImage(this.farComp, sx, sy, sw, sh);
            ctx.Restore();
        }
    }
}
