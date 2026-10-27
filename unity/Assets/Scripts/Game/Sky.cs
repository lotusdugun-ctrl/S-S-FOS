using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /*
     * The sky, as a function of the time of day.
     *
     * Everything here is driven by one number, the sun's elevation in degrees,
     * which follows from the hour. The colours are keyed the way the real sky
     * goes: deep night; astronomical and nautical twilight (indigo overhead, a
     * thin band of afterglow on the horizon under the sun); civil twilight and
     * the blue hour (a pink-orange horizon, the Belt of Venus rising on the far
     * side); the sun on the horizon (red, flattened); golden hour; and day, when
     * the zenith deepens to blue and the horizon pales into haze. Stars, the Milky
     * Way and the moon fade in only as the sky actually gets dark enough to show
     * them.
     */

    public class SkyMoon
    {
        public double x, y, vis;
    }

    public class SkyLight
    {
        public double x, y;
        public RGB color;
        public double strength, elev;
    }

    public class SkyCloud
    {
        public RGB crown;
        public double crownA;
        public RGB mid;
        public double midA;
        public RGB belly;
        public double bellyA;
    }

    public class SkyState
    {
        public double hour;
        /// <summary>sun elevation in degrees</summary>
        public double elev;
        /// <summary>sun position on screen, as fractions</summary>
        public double sunX, sunY;
        /// <summary>where the visible horizon sits, as a fraction of height</summary>
        public double horizon;
        /// <summary>Ramp: zenith, 30°, 10°, 3°, horizon (5 entries)</summary>
        public RGB[] ramp;
        /// <summary>the glow around the sun (forward scattering)</summary>
        public RGB glow;
        public double glowA, glowR;
        /// <summary>afterglow on the horizon under a sun that has just set / is about to rise</summary>
        public double afterA;
        /// <summary>pink band opposite the sun in twilight</summary>
        public double venusA;
        /// <summary>0..1 how many stars show</summary>
        public double stars;
        public SkyMoon moon;
        /// <summary>the light falling on the scene (sun or moon)</summary>
        public SkyLight light;
        /// <summary>landscape grade: lerp toward this colour by this much</summary>
        public RGB grade;
        public double gradeA;
        public SkyCloud cloud;
        /// <summary>0..1 how much the birds are about (they roost at night)</summary>
        public double birds;
        /// <summary>how much of the sky the cloud bank covers (clear nights, fair-weather days)</summary>
        public double cover;
    }

    /// <summary>starField's result: the painted canvas and the bright stars [x, y, mag].</summary>
    public class StarField
    {
        public Surface cv;
        public List<(double x, double y, double mag)> bright;
    }

    public static class Sky
    {
        static double lerp(double a, double b, double t) { return a + (b - a) * t; }
        static RGB mix(RGB a, RGB b, double t)
        {
            return new RGB(lerp(a[0], b[0], t), lerp(a[1], b[1], t), lerp(a[2], b[2], t));
        }
        static double clamp(double x, double a = 0, double b = 1) { return Math.Max(a, Math.Min(b, x)); }
        static double smooth(double e0, double e1, double x)
        {
            double t = clamp((x - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        public static Col rgb(RGB c, double a = 1) { return Css.rgb(c, a); }

        static RGB hex(string s) { return RGB.Hex(s); }

        /// <summary>interpolate a table keyed by sun elevation</summary>
        static T keyed<T>((double e, T v)[] table, double elev, Func<T, T, double, T> f)
        {
            if (elev <= table[0].e) return table[0].v;
            for (int i = 1; i < table.Length; i++)
            {
                double e1 = table[i].e; T v1 = table[i].v;
                double e0 = table[i - 1].e; T v0 = table[i - 1].v;
                if (elev <= e1)
                {
                    // mostly linear: a full smoothstep per segment made the light pause at
                    // every key and then hurry on, which read as the day jumping
                    double u = (elev - e0) / (e1 - e0);
                    return f(v0, v1, u * 0.75 + smooth(0, 1, u) * 0.25);
                }
            }
            return table[table.Length - 1].v;
        }

        static readonly Func<RGB, RGB, double, RGB> mixF = mix;
        static readonly Func<double, double, double, double> lerpF = lerp;

        /// <summary>zenith, 30°, 10°, 3°, horizon</summary>
        static RGB[] ramp(params string[] c)
        {
            var r = new RGB[c.Length];
            for (int i = 0; i < c.Length; i++) r[i] = hex(c[i]);
            return r;
        }
        static RGB[] mixRamp(RGB[] a, RGB[] b, double t)
        {
            var r = new RGB[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = mix(a[i], b[i], t);
            return r;
        }
        static readonly Func<RGB[], RGB[], double, RGB[]> mixRampF = mixRamp;

        static readonly (double, RGB[])[] SKY =
        {
            (-18, ramp("#020309", "#03050d", "#060a17", "#090e1e", "#0c1224")),
            (-12, ramp("#050817", "#0a1029", "#141b3d", "#1d2347", "#262846")),
            (-7, ramp("#0b1231", "#17224c", "#2c3767", "#4b4470", "#6d4a63")),
            (-3, ramp("#152153", "#273a78", "#4f5592", "#a2667c", "#d27a58")),
            (0, ramp("#1d2d63", "#334d8c", "#6b6aa0", "#d08273", "#f29a55")),
            // golden hour: the look the game was tuned in
            (4, ramp("#283a6b", "#4d4f87", "#9c6a8a", "#e3967a", "#f6c27e")),
            (10, ramp("#2d4f8f", "#5076b4", "#93a3c4", "#e6c2a0", "#f3d9b0")),
            (22, ramp("#2a5ea9", "#4f86c8", "#8db5dc", "#c5d6e3", "#e2e4dc")),
            (45, ramp("#2459ad", "#3f7fcb", "#7fb0e0", "#b7d3ec", "#d7e5ef")),
        };

        static (double, RGB) K(double e, double r, double g, double b) { return (e, new RGB(r, g, b)); }
        static (double, double) N(double e, double v) { return (e, v); }

        static readonly (double, RGB)[] GLOW = { K(-10, 120, 60, 70), K(-2, 255, 120, 70), K(3, 255, 175, 100), K(15, 255, 225, 170), K(40, 255, 250, 235) };
        static readonly (double, double)[] GLOWA = { N(-10, 0), N(-3, 0.35), N(2, 0.55), N(12, 0.45), N(40, 0.3) };
        static readonly (double, double)[] GLOWR = { N(-6, 0.45), N(2, 0.6), N(15, 0.4), N(40, 0.25) };
        static readonly (double, double)[] AFTERA = { N(-14, 0), N(-8, 0.25), N(-3, 0.5), N(2, 0) };
        static readonly (double, double)[] VENUSA = { N(-9, 0), N(-4, 0.35), N(1, 0.3), N(6, 0) };
        static readonly (double, RGB)[] SUNCOLOR = { K(-2, 255, 140, 90), K(4, 255, 190, 125), K(15, 255, 225, 185), K(40, 255, 246, 232) };
        static readonly (double, RGB)[] GRADE = { K(-14, 5, 8, 24), K(-8, 14, 18, 46), K(-3, 58, 40, 72), K(2, 110, 52, 44), K(5, 255, 220, 190), K(14, 255, 236, 212), K(30, 236, 238, 240) };
        static readonly (double, double)[] GRADEA = { N(-14, 0.6), N(-8, 0.5), N(-3, 0.32), N(2, 0.08), N(5, 0), N(14, 0.07), N(30, 0.1), N(50, 0.12) };
        static readonly (double, RGB)[] CROWN = { K(-14, 14, 17, 32), K(-6, 40, 38, 66), K(3, 62, 46, 82), K(12, 190, 170, 175), K(25, 244, 245, 248) };
        static readonly (double, double)[] CROWNA = { N(-14, 0.85), N(3, 0.9), N(25, 0.95) };
        static readonly (double, RGB)[] MID = { K(-14, 30, 34, 56), K(-4, 150, 90, 110), K(3, 214, 132, 120), K(15, 228, 210, 200), K(25, 222, 226, 234) };
        static readonly (double, double)[] MIDA = { N(-14, 0.25), N(3, 0.62), N(25, 0.55) };
        static readonly (double, RGB)[] BELLY = { K(-14, 70, 82, 120), K(-5, 230, 110, 90), K(3, 255, 215, 150), K(15, 200, 196, 200), K(25, 160, 168, 184) };
        static readonly (double, double)[] BELLYA = { N(-14, 0.12), N(-5, 0.55), N(3, 1), N(15, 0.55), N(25, 0.45) };
        static readonly (double, double)[] COVER = { N(-14, 0.3), N(-5, 0.6), N(3, 1), N(14, 0.6), N(30, 0.42) };

        /// <summary>sun elevation for an hour of the day, Mediterranean early autumn</summary>
        public static double sunElevation(double hour)
        {
            double h = ((hour % 24) + 24) % 24;
            return 58 * Math.Sin(((h - 6.3) / 12) * Math.PI);
        }

        public static SkyState skyState(double hour, double moonPhase)
        {
            double h = ((hour % 24) + 24) % 24;
            double elev = sunElevation(h);
            double horizon = 0.62;
            // the sun comes up on the left, over the low ground, and goes down behind
            // the hill in the middle of the frame
            double sunX = 0.24 + clamp((h - 5) / 14) * 0.27;
            double sunY = horizon - (elev / 90) * 0.75;

            var r = keyed(SKY, elev, mixRampF);

            // glow: wide and orange at the horizon, tight and white high up
            RGB glow = keyed(GLOW, elev, mixF);
            double glowA = elev < -10 ? 0 : keyed(GLOWA, elev, lerpF);
            double glowR = keyed(GLOWR, elev, lerpF);
            double afterA = elev > 2 ? 0 : keyed(AFTERA, elev, lerpF);
            double venusA = keyed(VENUSA, elev, lerpF);
            double stars = smooth(-4, -15, elev);

            // the moon: up through the night, low in the east at dusk, high after midnight
            double hn = (((h - 18.5) % 24) + 24) % 24; // hours since dusk
            bool moonUp = hn < 12.5;
            double mt = clamp(hn / 12.5);
            var moon = new SkyMoon
            {
                x = 0.84 - mt * 0.62,
                y = horizon - Math.Sin(mt * Math.PI) * 0.52,
                vis = moonUp ? smooth(0, 0.6, Math.Sin(mt * Math.PI)) * (1 - smooth(-6, 4, elev)) : 0,
            };

            // the light on the scene
            double sunStrength = smooth(-4, 3, elev);
            RGB sunColor = keyed(SUNCOLOR, elev, mixF);
            RGB moonLight = new RGB(165, 185, 255);
            bool useSun = sunStrength > 0.05 || moon.vis < 0.05;
            var light = useSun
                ? new SkyLight { x = sunX, y = sunY, color = sunColor, strength = sunStrength, elev = elev }
                : new SkyLight
                {
                    x = moon.x,
                    y = moon.y,
                    color = moonLight,
                    strength = 0.32 * moon.vis * (0.3 + 0.7 * moonPhase),
                    elev = 20,
                };

            RGB grade = keyed(GRADE, elev, mixF);
            double gradeA = keyed(GRADEA, elev, lerpF);

            // clouds: backlit fire at golden hour, white-topped by day, dark by night
            var cloud = new SkyCloud
            {
                crown = keyed(CROWN, elev, mixF),
                crownA = keyed(CROWNA, elev, lerpF),
                mid = keyed(MID, elev, mixF),
                midA = keyed(MIDA, elev, lerpF),
                belly = keyed(BELLY, elev, mixF),
                bellyA = keyed(BELLYA, elev, lerpF),
            };

            double birds = smooth(-7, -1, elev);
            double cover = keyed(COVER, elev, lerpF);

            return new SkyState
            {
                hour = h,
                elev = elev,
                sunX = sunX,
                sunY = sunY,
                horizon = horizon,
                ramp = r,
                glow = glow,
                glowA = glowA,
                glowR = glowR,
                afterA = afterA,
                venusA = venusA,
                stars = stars,
                moon = moon,
                light = light,
                grade = grade,
                gradeA = gradeA,
                cloud = cloud,
                birds = birds,
                cover = cover,
            };
        }

        // PORT: the TS made a fresh offscreen canvas for the Belt of Venus on every call;
        // here one scratch surface is kept and resized (resizing clears it, as a new canvas is clear).
        static Surface venusTmp;

        /// <summary>paint the sky itself — gradient, glow, afterglow, Belt of Venus, sun disc</summary>
        public static void paintSky(Ctx c, double w, double h, SkyState st, double scale)
        {
            double hy = st.horizon * h;
            var g = c.CreateLinearGradient(0, 0, 0, hy);
            RGB z = st.ramp[0], m30 = st.ramp[1], m10 = st.ramp[2], m3 = st.ramp[3], hz = st.ramp[4];
            // elevation angles mapped onto the screen: the horizon sits at hy
            g.AddColorStop(0, rgb(z));
            g.AddColorStop(0.42, rgb(m30));
            g.AddColorStop(0.78, rgb(m10));
            g.AddColorStop(0.93, rgb(m3));
            g.AddColorStop(1, rgb(hz));
            c.FillStyle = g;
            c.FillRect(0, 0, w, hy + 1);
            // below the horizon line, haze carries on down behind the land
            c.FillStyle = rgb(hz);
            c.FillRect(0, hy, w, h - hy);

            double sx = st.sunX * w;
            double sy = st.sunY * h;

            // the Belt of Venus: a pink band above the horizon on the side away from the
            // sun, with the blue-grey of the earth's own shadow under it
            if (st.venusA > 0.01)
            {
                double far = sx < w / 2 ? w : 0;
                var band = c.CreateLinearGradient(0, hy - h * 0.28, 0, hy);
                band.AddColorStop(0, "rgba(235, 160, 170, 0)");
                band.AddColorStop(0.6, Col.Rgba(235, 160, 170, st.venusA));
                band.AddColorStop(0.82, Col.Rgba(110, 110, 160, st.venusA * 0.8));
                band.AddColorStop(1, Col.Rgba(80, 90, 140, st.venusA * 0.6));
                c.Save();
                var side = c.CreateLinearGradient(sx, 0, far, 0);
                side.AddColorStop(0, "rgba(0,0,0,0)");
                side.AddColorStop(1, "rgba(0,0,0,1)");
                // mask the band by distance from the sun, painted through an offscreen
                int tw = (int)Math.Ceiling(w), th = (int)Math.Ceiling(hy);
                if (venusTmp == null) venusTmp = new Surface(tw, th, true, "venus");
                else venusTmp.SetSize(tw, th);
                var tmp = venusTmp;
                var tc = tmp.GetContext();
                tc.FillStyle = band;
                tc.FillRect(0, 0, w, hy);
                tc.GlobalCompositeOperation = Composite.DestinationIn;
                tc.FillStyle = side;
                tc.FillRect(0, 0, w, hy);
                c.DrawImage(tmp, 0, 0);
                c.Restore();
            }

            // afterglow: the sun is below the horizon but still lights the sky over it
            if (st.afterA > 0.01)
            {
                var ag = c.CreateRadialGradient(sx, hy, 0, sx, hy, w * 0.55);
                ag.AddColorStop(0, Col.Rgba(255, 140, 80, st.afterA));
                ag.AddColorStop(0.35, Col.Rgba(220, 100, 90, st.afterA * 0.45));
                ag.AddColorStop(1, "rgba(120, 70, 110, 0)");
                c.FillStyle = ag;
                c.FillRect(0, 0, w, hy + 1);
            }

            // forward-scattering glow around the sun
            if (st.glowA > 0.01)
            {
                double R = w * st.glowR;
                var gl = c.CreateRadialGradient(sx, sy, 0, sx, sy, R);
                gl.AddColorStop(0, rgb(st.glow, st.glowA));
                gl.AddColorStop(0.25, rgb(st.glow, st.glowA * 0.45));
                gl.AddColorStop(1, rgb(st.glow, 0));
                c.Save();
                c.GlobalCompositeOperation = Composite.Lighter;
                c.FillStyle = gl;
                c.FillRect(0, 0, w, h);
                c.Restore();
            }

            // the disc: red and squashed by refraction near the horizon, small and
            // white with a hard bloom when high
            if (st.elev > -1.2)
            {
                double rad = 40 * scale * (st.elev < 8 ? 1.05 : 0.8);
                double flat = st.elev < 6 ? 0.86 + st.elev * 0.02 : 1;
                RGB core = st.elev < 3 ? new RGB(255, 160, 95) : st.elev < 14 ? new RGB(255, 222, 170) : new RGB(255, 252, 244);
                RGB edge = st.elev < 3 ? new RGB(240, 110, 70) : st.elev < 14 ? new RGB(255, 196, 130) : new RGB(255, 240, 215);
                c.Save();
                c.Translate(sx, sy);
                c.Scale(1, flat);
                var d = c.CreateRadialGradient(0, 0, rad * 0.05, 0, 0, rad);
                d.AddColorStop(0, rgb(core));
                d.AddColorStop(0.85, rgb(mix(core, edge, 0.6)));
                // high up the limb dissolves into the bloom instead of ending on a rim
                d.AddColorStop(1, rgb(edge, st.elev > 10 ? 0.35 : 0.9));
                c.FillStyle = d;
                c.BeginPath();
                c.Arc(0, 0, rad, 0, Math.PI * 2);
                c.Fill();
                if (st.elev > 10)
                {
                    c.GlobalCompositeOperation = Composite.Lighter;
                    // a photographic bloom: bright close in, falling off fast, so it
                    // never reads as a coloured ring against the blue
                    var b = c.CreateRadialGradient(0, 0, rad * 0.5, 0, 0, rad * 3.2);
                    b.AddColorStop(0, "rgba(255, 252, 240, 0.6)");
                    b.AddColorStop(0.18, "rgba(255, 250, 238, 0.26)");
                    b.AddColorStop(0.5, "rgba(255, 248, 235, 0.07)");
                    b.AddColorStop(1, "rgba(255, 248, 235, 0)");
                    c.FillStyle = b;
                    c.BeginPath();
                    c.Arc(0, 0, rad * 3.2, 0, Math.PI * 2);
                    c.Fill();
                }
                c.Restore();
            }
        }

        /// <summary>
        /// The night sky: about two thousand stars with a realistic spread of
        /// brightness (most faint, a handful bright), each tinted by its temperature,
        /// and the Milky Way as a soft, mottled band with dark dust lanes through it.
        /// Painted once per screen size.
        /// </summary>
        public static StarField starField(double w, double h, double dpr)
        {
            var cv = new Surface((int)Math.Ceiling(w * dpr), (int)Math.Ceiling(h * dpr), true, "stars");
            var c = cv.GetContext();
            var bright = new List<(double x, double y, double mag)>();
            c.Scale(dpr, dpr);
            var r = Textures.rng(42);
            // the Milky Way: a band from lower left to upper right
            double bandX(double u) { return u * w; }
            double bandY(double u) { return h * (0.62 - u * 0.55); }
            for (int i = 0; i < 260; i++)
            {
                double u = r();
                double px = bandX(u), py = bandY(u);
                double off = (r() - 0.5) * h * 0.22;
                double x = px + off * 0.5;
                double y = py + off;
                double rad = (20 + r() * 70) * (0.6 + 0.4 * Math.Sin(u * 9));
                var g = c.CreateRadialGradient(x, y, 0, x, y, rad);
                bool warm = r() < 0.3;
                g.AddColorStop(0, warm ? "rgba(255, 230, 210, 0.045)" : "rgba(200, 215, 255, 0.05)");
                g.AddColorStop(1, "rgba(200, 215, 255, 0)");
                c.FillStyle = g;
                c.FillRect(x - rad, y - rad, rad * 2, rad * 2);
            }
            // dust lanes
            c.GlobalCompositeOperation = Composite.DestinationOut;
            for (int i = 0; i < 70; i++)
            {
                double u = r();
                double px = bandX(u), py = bandY(u);
                double off = (r() - 0.45) * h * 0.08;
                double rad = 8 + r() * 26;
                var g = c.CreateRadialGradient(px + off, py + off, 0, px + off, py + off, rad);
                g.AddColorStop(0, "rgba(0,0,0,0.6)");
                g.AddColorStop(1, "rgba(0,0,0,0)");
                c.FillStyle = g;
                c.FillRect(px + off - rad, py + off - rad, rad * 2, rad * 2);
            }
            c.GlobalCompositeOperation = Composite.SourceOver;
            // stars, denser along the band
            double N = Js.round((w * h) / 520);
            for (int i = 0; i < N; i++)
            {
                double x = r() * w;
                double y = r() * h * 0.7;
                if (r() < 0.35)
                {
                    double u = r();
                    double px = bandX(u), py = bandY(u);
                    x = px + (r() - 0.5) * h * 0.12;
                    y = py + (r() - 0.5) * h * 0.2;
                }
                double mag = Math.Pow(r(), 3.2); // most faint, a few bright
                double size = 0.35 + mag * 1.4;
                double t = r();
                double cr, cg, cb;
                if (t < 0.15) { cr = 255; cg = 210; cb = 170; }
                else if (t < 0.3) { cr = 190; cg = 210; cb = 255; }
                else { cr = 245; cg = 245; cb = 255; }
                c.FillStyle = Col.Rgba(cr, cg, cb, Css.fixedNum(0.25 + mag * 0.75, 3));
                c.BeginPath();
                c.Arc(x, y, size, 0, Math.PI * 2);
                c.Fill();
                if (mag > 0.55) bright.Add((x, y, mag));
            }
            return new StarField { cv = cv, bright = bright };
        }

        static readonly double[][] MARIA =
        {
            new[] { -0.25, -0.3, 0.28 },
            new[] { 0.15, -0.2, 0.22 },
            new[] { -0.1, 0.15, 0.3 },
            new[] { 0.3, 0.25, 0.16 },
            new[] { -0.4, 0.2, 0.14 },
        };

        /// <summary>
        /// The moon, with its real phase: the lit part bounded by a half-ellipse
        /// terminator, dark maria on the face, the unlit part faintly visible by
        /// earthshine, and a soft halo.
        /// </summary>
        public static void drawMoon(Ctx c, double x, double y, double R, double phase, double alpha)
        {
            if (alpha < 0.01) return;
            c.Save();
            c.GlobalAlpha = alpha;
            // halo
            var halo = c.CreateRadialGradient(x, y, R, x, y, R * 7);
            halo.AddColorStop(0, "rgba(200, 215, 255, 0.22)");
            halo.AddColorStop(1, "rgba(200, 215, 255, 0)");
            c.FillStyle = halo;
            c.BeginPath();
            c.Arc(x, y, R * 7, 0, Math.PI * 2);
            c.Fill();
            // earthshine on the dark limb
            c.FillStyle = "rgba(90, 100, 130, 0.35)";
            c.BeginPath();
            c.Arc(x, y, R, 0, Math.PI * 2);
            c.Fill();
            // the lit part: phase 0 new, 0.5 full, 1 new again; waxing lit on the right
            double p = ((phase % 1) + 1) % 1;
            double lit = 1 - Math.Abs(p - 0.5) * 2; // 0..1..0
            bool waxing = p < 0.5;
            double k = Math.Cos(lit * Math.PI); // terminator ellipse x-radius factor
            c.Save();
            c.Translate(x, y);
            if (!waxing) c.Scale(-1, 1);
            c.BeginPath();
            c.Arc(0, 0, R, -Math.PI / 2, Math.PI / 2); // right half
            c.Ellipse(0, 0, Math.Abs(k) * R, R, 0, Math.PI / 2, -Math.PI / 2, k > 0);
            c.ClosePath();
            c.Clip();
            var face = c.CreateRadialGradient(-R * 0.3, -R * 0.3, R * 0.1, 0, 0, R);
            face.AddColorStop(0, "rgb(250, 248, 238)");
            face.AddColorStop(1, "rgb(215, 215, 210)");
            c.FillStyle = face;
            c.FillRect(-R, -R, R * 2, R * 2);
            // maria
            c.FillStyle = "rgba(150, 155, 165, 0.45)";
            foreach (var m in MARIA)
            {
                c.BeginPath();
                c.Arc(m[0] * R, m[1] * R, m[2] * R, 0, Math.PI * 2);
                c.Fill();
            }
            c.Restore();
            c.Restore();
        }
    }
}
