using System;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>An RGB triple, channels 0..255 (the web build's <c>RGB = [number, number, number]</c>).</summary>
    public struct RGB
    {
        public double r, g, b;

        public RGB(double r, double g, double b) { this.r = r; this.g = g; this.b = b; }

        public double this[int i]
        {
            get { return i == 0 ? r : i == 1 ? g : b; }
            set { if (i == 0) r = value; else if (i == 1) g = value; else b = value; }
        }

        /// <summary>"#rrggbb" to RGB.</summary>
        public static RGB Hex(string s)
        {
            return new RGB(
                Convert.ToInt32(s.Substring(1, 2), 16),
                Convert.ToInt32(s.Substring(3, 2), 16),
                Convert.ToInt32(s.Substring(5, 2), 16));
        }

        public override string ToString() { return "[" + r + ", " + g + ", " + b + "]"; }
    }

    public static class Css
    {
        /// <summary>
        /// sky.ts' <c>rgb(c, a)</c>: the colour rounded to whole channels and the alpha to three
        /// decimals, exactly as the template string did.
        /// </summary>
        public static Col rgb(RGB c, double a = 1)
        {
            double al = Math.Round(Math.Max(0, Math.Min(1, a)) * 1000, MidpointRounding.AwayFromZero) / 1000;
            return Col.Rgba(Js.round(c.r), Js.round(c.g), Js.round(c.b), al);
        }

        /// <summary><c>`rgba(${r}, ${g}, ${b}, ${a})`</c> with numbers that were written as they came.</summary>
        public static Col rgba(double r, double g, double b, double a = 1) { return Col.Rgba(r, g, b, a); }

        /// <summary>
        /// A number formatted with toFixed(n) and read back, for template strings that did that
        /// to an alpha (<c>a.toFixed(3)</c>): the rounding is part of the colour.
        /// </summary>
        public static double fixedNum(double v, int digits)
        {
            double k = Math.Pow(10, digits);
            return Math.Round(v * k, MidpointRounding.AwayFromZero) / k;
        }

        /// <summary><c>`oklch(${l} ${c} ${h} / ${a})`</c> built from numbers.</summary>
        public static Col oklch(double l, double c, double h, double a = 1) { return Col.Oklch(l, c, h, a); }

        /// <summary><c>`hsla(${h}, ${s}%, ${l}%, ${a})`</c> built from numbers.</summary>
        public static Col hsla(double h, double s, double l, double a = 1) { return Col.Hsla(h, s, l, a); }
    }
}
