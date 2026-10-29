using System;

namespace Sisifos.Game
{
    /// <summary>
    /// What a host that can post-process the finished frame on the GPU (the Unity app) needs
    /// to know about it: where the sun is and how much of it shows, how much glow, mist and
    /// rain there is. All screen positions are fractions, y down. Plain numbers, so the
    /// engine stays free of any one renderer.
    /// </summary>
    public sealed class PostFxState
    {
        /// <summary>false: show the frame as drawn (low quality, or nothing to add)</summary>
        public bool on;
        /// <summary>the sun on screen (fractions, y down), how much of it shines through (0..1) and its colour (0..1)</summary>
        public double sunX, sunY, sunVis, sunR, sunG, sunB;
        /// <summary>how much the bright parts bleed light (0..1)</summary>
        public double bloom;
        /// <summary>mist: how thick (0..1), its colour (0..1), and the horizon it lies along (fraction of height, y down)</summary>
        public double fog, fogR, fogG, fogB, horizon;
        /// <summary>rain drawn by the host: how hard (0..1) and how far it leans (x per y)</summary>
        public double rain, rainLean;
        /// <summary>film grain (0..1)</summary>
        public double grain;
        /// <summary>seconds, for anything that moves</summary>
        public double t;
    }

    public partial class SisyphusEngine
    {
        /// <summary>
        /// Set by a host that post-processes the frame (PostFx in the Unity app): the rain
        /// is then left to it — streaks worked out per pixel on the GPU, in three depths —
        /// instead of being stroked here. Snow, and low quality, stay with the engine.
        /// </summary>
        public bool gpuWeather;

        /// <summary>true when the host draws the rain this frame (see gpuWeather)</summary>
        internal bool hostRain
        {
            get { return this.gpuWeather && !this.lowQuality && !this.saga.winter; }
        }

        /// <summary>The post-processing for the frame just drawn, written into `o`.</summary>
        public PostFxState postFx(PostFxState o)
        {
            o = o ?? new PostFxState();
            o.on = this.gpuWeather && !this.lowQuality && this.st != null;
            o.t = this.t;
            if (!o.on) return o;
            var st = this.st;
            bool wet = this.weather == "rain" || this.weather == "storm";
            double wx = wet ? (this.weather == "storm" ? 1 : 0.65) * this.wxFade : 0;
            double storm = this.zeus.state == "gone" ? 0 : this.zeus.state == "fade" ? 0.5 : 1;

            // the sun: up (fading in over the last degrees before it clears the horizon), not
            // behind the god's storm or the rain, and on screen or just off it
            o.sunX = st.sunX;
            o.sunY = st.sunY;
            double up = Math.Max(0, Math.Min(1, (st.elev + 2) / 8));
            double onScreen = Math.Max(0, 1 - Math.Max(0, Math.Max(-st.sunX, st.sunX - 1)) * 3) *
                Math.Max(0, 1 - Math.Max(0, -st.sunY) * 3);
            // high in the sky the shafts thin out: they are a low sun's light
            double low = 1 - Math.Max(0, Math.Min(1, (st.elev - 25) / 30)) * 0.7;
            o.sunVis = up * onScreen * low * (1 - wx * 0.85) * (1 - storm * 0.8) * (1 - st.cover * 0.35);
            var c = st.light.color;
            o.sunR = c.r / 255.0;
            o.sunG = c.g / 255.0;
            o.sunB = c.b / 255.0;

            // the glow: strongest in the gold of the evening and morning, a little at night
            // (the moon, the fire), least at noon, when everything is already bright
            double golden = Math.Max(0, 1 - Math.Abs(st.elev - 4) / 14);
            o.bloom = 0.32 + golden * 0.28 - (st.elev > 30 ? 0.08 : 0);

            // the mist: thick at dawn, a breath of it at dusk, grey and low in the rain
            double h = ((st.hour % 24) + 24) % 24;
            double dawn = Math.Max(0, 1 - Math.Abs(h - 6.3) / 1.8);
            double dusk = Math.Max(0, 1 - Math.Abs(h - 19.6) / 1.6);
            o.fog = Math.Min(1, 0.12 + dawn * 0.45 + dusk * 0.2 + wx * 0.3);
            var hz = st.ramp[st.ramp.Length - 1];
            double grey = wx * 0.6;
            o.fogR = (hz.r * (1 - grey) + 150 * grey) / 255.0;
            o.fogG = (hz.g * (1 - grey) + 156 * grey) / 255.0;
            o.fogB = (hz.b * (1 - grey) + 166 * grey) / 255.0;
            o.horizon = st.horizon;

            // the rain, when the host has it
            o.rain = this.hostRain ? wx : 0;
            o.rainLean = 0.18 + (this.windiness() - 1) * 0.12 + Math.Max(0, -this.windAt(this.camX)) * 0.6;

            o.grain = 1;
            return o;
        }
    }
}
