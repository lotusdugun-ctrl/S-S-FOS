using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sisifos.Paint
{
    /// <summary>
    /// An sRGB colour with straight (not premultiplied) alpha, every channel 0..1.
    /// The game was painted in CSS colours, so every CSS form it uses parses here
    /// exactly as a browser would read it (rgb/rgba, #hex, hsl/hsla, oklch).
    /// </summary>
    public struct Col : IEquatable<Col>
    {
        public float r, g, b, a;
        /// <summary>
        /// Written in a modern CSS colour space (oklch/oklab). A gradient with such a stop is
        /// interpolated in Oklab rather than sRGB, as browsers do.
        /// </summary>
        public bool modern;

        public Col(float r, float g, float b, float a = 1f)
        {
            this.r = r; this.g = g; this.b = b; this.a = a; modern = false;
        }

        public static readonly Col Transparent = new Col(0, 0, 0, 0);
        public static readonly Col Black = new Col(0, 0, 0, 1);
        public static readonly Col White = new Col(1, 1, 1, 1);

        /// <summary>CSS rgba(): channels 0..255, alpha 0..1.</summary>
        public static Col Rgba(double r, double g, double b, double a = 1)
        {
            return new Col(
                (float)(Clamp255(r) / 255.0),
                (float)(Clamp255(g) / 255.0),
                (float)(Clamp255(b) / 255.0),
                (float)Clamp01(a));
        }

        public static Col Rgb(double r, double g, double b) { return Rgba(r, g, b, 1); }

        /// <summary>CSS hsl()/hsla(): h in degrees, s and l in percent (0..100).</summary>
        public static Col Hsla(double h, double s, double l, double a = 1)
        {
            h = ((h % 360) + 360) % 360 / 360.0;
            s = Clamp01(s / 100.0);
            l = Clamp01(l / 100.0);
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            return new Col(
                (float)Hue(p, q, h + 1.0 / 3),
                (float)Hue(p, q, h),
                (float)Hue(p, q, h - 1.0 / 3),
                (float)Clamp01(a));
        }

        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        /// <summary>CSS oklch(L C H / a). L 0..1, C chroma, H degrees.</summary>
        public static Col Oklch(double L, double C, double H, double a = 1)
        {
            double hr = H * Math.PI / 180.0;
            return Oklab(L, C * Math.Cos(hr), C * Math.Sin(hr), a);
        }

        public static Col Oklab(double L, double A, double B, double alpha = 1)
        {
            double l_ = L + 0.3963377774 * A + 0.2158037573 * B;
            double m_ = L - 0.1055613458 * A - 0.0638541728 * B;
            double s_ = L - 0.0894841775 * A - 1.2914855480 * B;
            double l = l_ * l_ * l_, m = m_ * m_ * m_, s = s_ * s_ * s_;
            double r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            double g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            double b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
            var c = new Col((float)Encode(r), (float)Encode(g), (float)Encode(b), (float)Clamp01(alpha));
            c.modern = true;
            return c;
        }

        static double Decode(double c)
        {
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>This colour in Oklab.</summary>
        public void ToOklab(out double L, out double A, out double B)
        {
            double R = Decode(r), G = Decode(g), Bl = Decode(b);
            double l = 0.4122214708 * R + 0.5363325363 * G + 0.0514459929 * Bl;
            double m = 0.2119034982 * R + 0.6806995451 * G + 0.1073969566 * Bl;
            double s = 0.0883024619 * R + 0.2817188376 * G + 0.6299787005 * Bl;
            l = Math.Cbrt(l); m = Math.Cbrt(m); s = Math.Cbrt(s);
            L = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
            A = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
            B = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;
        }

        static double Encode(double x)
        {
            // linear -> sRGB transfer, then clipped into gamut the way browsers clip
            double v = x <= 0.0031308 ? 12.92 * x : 1.055 * Math.Pow(Math.Max(0, x), 1 / 2.4) - 0.055;
            return Clamp01(v);
        }

        public Col WithAlpha(double alpha) { var c = this; c.a = (float)Clamp01(alpha); return c; }
        public Col Fade(double k) { var c = this; c.a = (float)Clamp01(a * k); return c; }

        public static Col Lerp(Col x, Col y, double t)
        {
            float k = (float)t;
            return new Col(x.r + (y.r - x.r) * k, x.g + (y.g - x.g) * k, x.b + (y.b - x.b) * k, x.a + (y.a - x.a) * k);
        }

        static double Clamp255(double v) { return v < 0 ? 0 : v > 255 ? 255 : v; }
        static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : (double.IsNaN(v) ? 0 : v); }

        public bool Equals(Col o) { return r == o.r && g == o.g && b == o.b && a == o.a; }
        public override bool Equals(object obj) { return obj is Col c && Equals(c); }
        public override int GetHashCode() { return HashCode.Combine(r, g, b, a); }
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "rgba({0:0},{1:0},{2:0},{3:0.###})", r * 255, g * 255, b * 255, a);
        }

        // ------------------------------------------------------------------ CSS

        static readonly Dictionary<string, Col> cache = new Dictionary<string, Col>(512);

        /// <summary>Parse a CSS colour string. Cached: constant colours cost one lookup.</summary>
        public static Col Parse(string css)
        {
            if (css == null) return Transparent;
            lock (cache)
            {
                if (cache.TryGetValue(css, out var hit)) return hit;
            }
            var c = ParseUncached(css);
            lock (cache)
            {
                if (cache.Count > 4096) cache.Clear();
                cache[css] = c;
            }
            return c;
        }

        static Col ParseUncached(string s)
        {
            s = s.Trim().ToLowerInvariant();
            if (s.Length == 0 || s == "transparent" || s == "none") return Transparent;
            if (s[0] == '#') return ParseHex(s);
            switch (s)
            {
                case "black": return Black;
                case "white": return White;
                case "red": return new Col(1, 0, 0);
                case "gold": return Rgb(255, 215, 0);
            }
            int open = s.IndexOf('(');
            int close = s.LastIndexOf(')');
            if (open < 0 || close < open) throw new FormatException("colour: " + s);
            string fn = s.Substring(0, open).Trim();
            string body = s.Substring(open + 1, close - open - 1);
            // both the comma form and the space / slash form
            double alpha = 1;
            int slash = body.IndexOf('/');
            if (slash >= 0)
            {
                alpha = Num(body.Substring(slash + 1).Trim(), 1);
                body = body.Substring(0, slash);
            }
            var parts = body.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4) alpha = Num(parts[3], 1);
            switch (fn)
            {
                case "rgb":
                case "rgba":
                    return Rgba(Num(parts[0], 255), Num(parts[1], 255), Num(parts[2], 255), alpha);
                case "hsl":
                case "hsla":
                    return Hsla(Num(parts[0], 1), Num(parts[1], 100), Num(parts[2], 100), alpha);
                case "oklch":
                    return Oklch(Num(parts[0], 1), Num(parts[1], 0.4), Num(parts[2], 1), alpha);
                case "oklab":
                    return Oklab(Num(parts[0], 1), Num(parts[1], 0.4), Num(parts[2], 0.4), alpha);
            }
            throw new FormatException("colour: " + s);
        }

        /// <summary>A CSS number, where a percentage means a fraction of <paramref name="full"/>.</summary>
        static double Num(string t, double full)
        {
            t = t.Trim();
            if (t.EndsWith("deg")) t = t.Substring(0, t.Length - 3);
            if (t.EndsWith("%"))
                return double.Parse(t.Substring(0, t.Length - 1), CultureInfo.InvariantCulture) / 100.0 * full;
            return double.Parse(t, CultureInfo.InvariantCulture);
        }

        static Col ParseHex(string s)
        {
            string h = s.Substring(1);
            int Hx(char c) { return Convert.ToInt32(c.ToString(), 16); }
            if (h.Length == 3 || h.Length == 4)
            {
                float r = Hx(h[0]) * 17 / 255f, g = Hx(h[1]) * 17 / 255f, b = Hx(h[2]) * 17 / 255f;
                float a = h.Length == 4 ? Hx(h[3]) * 17 / 255f : 1f;
                return new Col(r, g, b, a);
            }
            if (h.Length == 6 || h.Length == 8)
            {
                int v(int i) { return Convert.ToInt32(h.Substring(i, 2), 16); }
                return new Col(v(0) / 255f, v(2) / 255f, v(4) / 255f, h.Length == 8 ? v(6) / 255f : 1f);
            }
            throw new FormatException("colour: " + s);
        }

        public static implicit operator Col(string css) { return Parse(css); }
    }
}
