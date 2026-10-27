using System;
using System.Collections.Generic;

namespace Sisifos.Paint
{
    public enum Composite
    {
        SourceOver,
        Lighter,
        SourceAtop,
        SourceIn,
        DestinationOut,
        DestinationIn,
        Copy,
        Saturation,
        Multiply,
        Screen,
    }

    public enum LineCap { Butt, Round, Square }
    public enum LineJoin { Miter, Round, Bevel }
    public enum TextAlign { Start, Left, Center, Right, End }
    public enum TextBaseline { Alphabetic, Top, Middle, Bottom, Hanging, Ideographic }

    public static class CompositeNames
    {
        public static Composite Parse(string s)
        {
            switch (s)
            {
                case "source-over": return Composite.SourceOver;
                case "lighter": return Composite.Lighter;
                case "source-atop": return Composite.SourceAtop;
                case "source-in": return Composite.SourceIn;
                case "destination-out": return Composite.DestinationOut;
                case "destination-in": return Composite.DestinationIn;
                case "copy": return Composite.Copy;
                case "saturation": return Composite.Saturation;
                case "multiply": return Composite.Multiply;
                case "screen": return Composite.Screen;
            }
            throw new ArgumentException("composite: " + s);
        }
    }

    public struct ColorStop
    {
        public double offset;
        public Col color;
        public ColorStop(double o, Col c) { offset = o; color = c; }
    }

    /// <summary>A canvas gradient. Coordinates are in the user space in force when it is used to paint.</summary>
    public abstract class Gradient
    {
        internal readonly List<ColorStop> stops = new List<ColorStop>(4);
        /// <summary>Set by the renderer: which row of this frame's gradient atlas holds it.</summary>
        internal int atlasRow = -1;
        internal int atlasFrame = -1;
        internal int version;
        internal int atlasVersion;

        public void AddColorStop(double offset, Col color)
        {
            if (double.IsNaN(offset)) return;
            offset = offset < 0 ? 0 : offset > 1 ? 1 : offset;
            // stops keep insertion order among equal offsets (canvas does)
            int i = stops.Count;
            while (i > 0 && stops[i - 1].offset > offset) i--;
            stops.Insert(i, new ColorStop(offset, color));
            version++;
        }

        public void AddColorStop(double offset, string css) { AddColorStop(offset, Col.Parse(css)); }

        public IReadOnlyList<ColorStop> Stops { get { return stops; } }

        bool Modern()
        {
            for (int i = 0; i < stops.Count; i++) if (stops[i].color.modern) return true;
            return false;
        }

        /// <summary>
        /// The colour at t, interpolated UNpremultiplied as Chrome's canvas does (in Oklab when any
        /// stop was written in a modern colour space, in sRGB otherwise). Returns premultiplied sRGB.
        /// </summary>
        public void SamplePremul(double t, out float r, out float g, out float b, out float a)
        {
            int n = stops.Count;
            if (n == 0) { r = g = b = a = 0; return; }
            if (t <= stops[0].offset || n == 1) { Pre(stops[0].color, out r, out g, out b, out a); return; }
            if (t >= stops[n - 1].offset) { Pre(stops[n - 1].color, out r, out g, out b, out a); return; }
            int i = 1;
            while (i < n && stops[i].offset < t) i++;
            var s0 = stops[i - 1];
            var s1 = stops[i];
            double span = s1.offset - s0.offset;
            double k = span <= 1e-9 ? 1 : (t - s0.offset) / span;
            if (Modern())
            {
                s0.color.ToOklab(out var L0, out var A0, out var B0);
                s1.color.ToOklab(out var L1, out var A1, out var B1);
                double a0 = s0.color.a, a1 = s1.color.a;
                double al = a0 + (a1 - a0) * k;
                if (al <= 1e-9) { r = g = b = a = 0; return; }
                // unpremultiplied, as Chrome does (oklch(0.7 0.15 30 / 0.6) -> rgba(0,0,0,0)
                // reads back (91,40,34,76) at the midpoint)
                double L = L0 + (L1 - L0) * k;
                double A = A0 + (A1 - A0) * k;
                double B = B0 + (B1 - B0) * k;
                var c = Col.Oklab(L, A, B, al);
                Pre(c, out r, out g, out b, out a);
                return;
            }
            // Legacy (sRGB) stops: Chrome's canvas interpolates these UNpremultiplied (checked:
            // rgba(200,180,150,0.6) -> rgba(0,0,0,0) reads back (101,91,74,76) at the midpoint,
            // not (200,180,150,76)), then premultiplies. A fade to "rgba(0,0,0,0)" darkens.
            float kf = (float)k;
            var c0 = s0.color; var c1 = s1.color;
            a = c0.a + (c1.a - c0.a) * kf;
            r = (c0.r + (c1.r - c0.r) * kf) * a;
            g = (c0.g + (c1.g - c0.g) * kf) * a;
            b = (c0.b + (c1.b - c0.b) * kf) * a;
        }

        static void Pre(Col c, out float r, out float g, out float b, out float a)
        {
            r = c.r * c.a; g = c.g * c.a; b = c.b * c.a; a = c.a;
        }
    }

    public sealed class LinearGradient : Gradient
    {
        public readonly double x0, y0, x1, y1;
        public LinearGradient(double x0, double y0, double x1, double y1)
        {
            this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1;
        }

        public double T(double x, double y)
        {
            double dx = x1 - x0, dy = y1 - y0;
            double l2 = dx * dx + dy * dy;
            if (l2 < 1e-12) return 0;
            return ((x - x0) * dx + (y - y0) * dy) / l2;
        }
    }

    public sealed class RadialGradient : Gradient
    {
        public readonly double x0, y0, r0, x1, y1, r1;
        public RadialGradient(double x0, double y0, double r0, double x1, double y1, double r1)
        {
            this.x0 = x0; this.y0 = y0; this.r0 = r0; this.x1 = x1; this.y1 = y1; this.r1 = r1;
        }

        /// <summary>The two-point conical gradient parameter at a point, or NaN where the gradient paints nothing.</summary>
        public double T(double px, double py)
        {
            double cdx = x1 - x0, cdy = y1 - y0, dr = r1 - r0;
            double pdx = px - x0, pdy = py - y0;
            double a = cdx * cdx + cdy * cdy - dr * dr;
            double b = pdx * cdx + pdy * cdy + r0 * dr;
            double c = pdx * pdx + pdy * pdy - r0 * r0;
            if (Math.Abs(a) < 1e-9)
            {
                if (Math.Abs(b) < 1e-12) return double.NaN;
                double t0 = c / (2 * b);
                return r0 + t0 * dr >= 0 ? t0 : double.NaN;
            }
            double disc = b * b - a * c;
            if (disc < 0) return double.NaN;
            double sq = Math.Sqrt(disc);
            double ta = (b + sq) / a, tb = (b - sq) / a;
            double hi = Math.Max(ta, tb), lo = Math.Min(ta, tb);
            if (r0 + hi * dr >= 0) return hi;
            if (r0 + lo * dr >= 0) return lo;
            return double.NaN;
        }
    }

    public sealed class Pattern
    {
        public readonly Surface image;
        public readonly bool repeatX, repeatY;
        public Pattern(Surface image, string repetition)
        {
            this.image = image;
            repeatX = repetition == "repeat" || repetition == "repeat-x" || string.IsNullOrEmpty(repetition);
            repeatY = repetition == "repeat" || repetition == "repeat-y" || string.IsNullOrEmpty(repetition);
        }
    }

    public enum StyleKind { Color, Linear, Radial, Pattern }

    /// <summary>What a fill or stroke paints with: a colour, a gradient or a pattern.</summary>
    public struct Style
    {
        public StyleKind kind;
        public Col color;
        public Gradient gradient;
        public Pattern pattern;

        public static implicit operator Style(Col c) { return new Style { kind = StyleKind.Color, color = c }; }
        public static implicit operator Style(string css) { return new Style { kind = StyleKind.Color, color = Col.Parse(css) }; }
        public static implicit operator Style(Gradient g)
        {
            return new Style { kind = g is RadialGradient ? StyleKind.Radial : StyleKind.Linear, gradient = g };
        }
        public static implicit operator Style(LinearGradient g) { return new Style { kind = StyleKind.Linear, gradient = g }; }
        public static implicit operator Style(RadialGradient g) { return new Style { kind = StyleKind.Radial, gradient = g }; }
        public static implicit operator Style(Pattern p) { return new Style { kind = StyleKind.Pattern, pattern = p }; }
    }
}
