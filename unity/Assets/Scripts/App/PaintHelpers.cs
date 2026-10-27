using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.App
{
    // Pure C# helpers for the Unity backend (no UnityEngine here, so the headless harness can
    // compile and test them).

    /// <summary>IEEE 754 binary16 conversion (round to nearest even), for RGBAHalf textures.</summary>
    public static class HalfFloat
    {
        public static ushort FromFloat(float f)
        {
            uint x = (uint)BitConverter.SingleToInt32Bits(f);
            uint sign = (x >> 16) & 0x8000u;
            int exp = (int)((x >> 23) & 0xff);
            uint mant = x & 0x7fffffu;
            if (exp == 0xff) return (ushort)(sign | 0x7c00u | (mant != 0 ? 0x200u : 0u));
            int e = exp - 127 + 15;
            if (e >= 0x1f) return (ushort)(sign | 0x7c00u);
            if (e <= 0)
            {
                if (e < -10) return (ushort)sign;
                mant |= 0x800000u;
                int shift = 14 - e;
                uint hm = mant >> shift;
                uint rem = mant & ((1u << shift) - 1);
                uint halfway = 1u << (shift - 1);
                if (rem > halfway || (rem == halfway && (hm & 1) != 0)) hm++;
                return (ushort)(sign | hm);
            }
            uint h = sign | ((uint)e << 10) | (mant >> 13);
            uint r = mant & 0x1fffu;
            if (r > 0x1000u || (r == 0x1000u && (h & 1) != 0)) h++;
            return (ushort)h;
        }

        public static float ToFloat(ushort h)
        {
            int sign = (h >> 15) & 1, exp = (h >> 10) & 0x1f, mant = h & 0x3ff;
            float v;
            if (exp == 0) v = mant * (1f / 16777216f);
            else if (exp == 31) v = mant != 0 ? float.NaN : float.PositiveInfinity;
            else v = (1f + mant / 1024f) * (float)Math.Pow(2, exp - 15);
            return sign != 0 ? -v : v;
        }
    }

    /// <summary>
    /// Gradient lookup rows (premultiplied RGBA as half floats), cached by the stops' content:
    /// the game makes a fresh gradient object every frame for the same sky, and evaluating
    /// Oklab stops 512 times per row per frame would be wasted work.
    /// </summary>
    public sealed class GradientLuts
    {
        public readonly int width;

        sealed class Entry
        {
            public ColorStop[] stops;
            public ushort[] row;
            public int lastUsed;
        }

        readonly Dictionary<int, List<Entry>> cache = new Dictionary<int, List<Entry>>();
        int count;

        public GradientLuts(int width) { this.width = width; }

        static int Hash(IReadOnlyList<ColorStop> s)
        {
            unchecked
            {
                int h = 17 + s.Count;
                for (int i = 0; i < s.Count; i++)
                {
                    var c = s[i];
                    h = h * 31 + c.offset.GetHashCode();
                    h = h * 31 + c.color.r.GetHashCode();
                    h = h * 31 + c.color.g.GetHashCode();
                    h = h * 31 + c.color.b.GetHashCode();
                    h = h * 31 + c.color.a.GetHashCode();
                    h = h * 31 + (c.color.modern ? 1 : 0);
                }
                return h;
            }
        }

        static bool Same(ColorStop[] a, IReadOnlyList<ColorStop> b)
        {
            if (a.Length != b.Count) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].offset != b[i].offset) return false;
                var x = a[i].color; var y = b[i].color;
                if (x.r != y.r || x.g != y.g || x.b != y.b || x.a != y.a || x.modern != y.modern) return false;
            }
            return true;
        }

        /// <summary>The row for a gradient: width texels of premultiplied RGBA (half floats), t = k / (width - 1).</summary>
        public ushort[] Row(Gradient g, int frame)
        {
            var stops = g.Stops;
            int h = Hash(stops);
            if (!cache.TryGetValue(h, out var list))
            {
                list = new List<Entry>(1);
                cache[h] = list;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (Same(list[i].stops, stops)) { list[i].lastUsed = frame; return list[i].row; }
            }
            var e = new Entry { stops = new ColorStop[stops.Count], row = new ushort[width * 4], lastUsed = frame };
            for (int i = 0; i < stops.Count; i++) e.stops[i] = stops[i];
            for (int k = 0; k < width; k++)
            {
                g.SamplePremul(k / (double)(width - 1), out var r, out var gg, out var b, out var a);
                e.row[k * 4] = HalfFloat.FromFloat(r);
                e.row[k * 4 + 1] = HalfFloat.FromFloat(gg);
                e.row[k * 4 + 2] = HalfFloat.FromFloat(b);
                e.row[k * 4 + 3] = HalfFloat.FromFloat(a);
            }
            list.Add(e);
            count++;
            return e.row;
        }

        /// <summary>Forget rows not used for a while.</summary>
        public void Trim(int frame, int maxAge)
        {
            if (count < 256) return;
            List<int> empty = null;
            foreach (var kv in cache)
            {
                kv.Value.RemoveAll(e => frame - e.lastUsed > maxAge);
                if (kv.Value.Count == 0) (empty ?? (empty = new List<int>())).Add(kv.Key);
            }
            if (empty != null) foreach (var k in empty) cache.Remove(k);
            count = 0;
            foreach (var kv in cache) count += kv.Value.Count;
        }
    }

    /// <summary>Where a text run goes: its left edge, baseline and horizontal squeeze (maxWidth).</summary>
    public static class TextLayout
    {
        /// <summary>
        /// Canvas placement of a run (direction ltr, as the web build's canvas was).
        /// <paramref name="width"/> is the run's advance at the font size; ascent and descent are
        /// the font's (positive) at that size. Returns false when nothing is drawn (maxWidth ≤ 0).
        /// </summary>
        public static bool Place(TextRun r, double size, double width, double ascent, double descent,
            out double x0, out double baseline, out double sx)
        {
            sx = 1;
            x0 = r.x; baseline = r.y;
            if (!double.IsNaN(r.maxWidth))
            {
                if (r.maxWidth <= 0) return false;
                if (width > r.maxWidth && width > 0) sx = r.maxWidth / width;
            }
            double w = width * sx;
            switch (r.align)
            {
                case TextAlign.Center: x0 = r.x - w / 2; break;
                case TextAlign.Right:
                case TextAlign.End: x0 = r.x - w; break;
                default: x0 = r.x; break;
            }
            // Chrome: top/bottom/middle use the em box, normalised from the font's ascent and
            // descent; hanging is 80% of the ascent; ideographic is the descent.
            double sum = ascent + descent;
            double A = sum > 0 ? size * ascent / sum : size * 0.8;
            double D = size - A;
            switch (r.baseline)
            {
                case TextBaseline.Top: baseline = r.y + A; break;
                case TextBaseline.Middle: baseline = r.y + (A - D) / 2; break;
                case TextBaseline.Bottom: baseline = r.y - D; break;
                case TextBaseline.Hanging: baseline = r.y + ascent * 0.8; break;
                case TextBaseline.Ideographic: baseline = r.y - descent; break;
                default: baseline = r.y; break;
            }
            return true;
        }
    }

    /// <summary>
    /// Which face a CSS font list resolves to. The web build loaded Cinzel (500, 700), Marcellus
    /// (400) and Cormorant Garamond (400, 400 italic, 500 italic, 600) as web fonts; those ship
    /// as TTFs in StreamingAssets/Fonts. Anything else is a system font.
    /// </summary>
    public struct FontFace : IEquatable<FontFace>
    {
        /// <summary>A bundled file under StreamingAssets/Fonts, or null for a system font.</summary>
        public string file;
        /// <summary>For system fonts: the CSS family ("Georgia", "Times New Roman") or generic ("serif", "sans-serif", "monospace").</summary>
        public string family;
        public int weight;
        public bool italic;

        public bool Equals(FontFace o) { return file == o.file && family == o.family && weight == o.weight && italic == o.italic; }
        public override bool Equals(object o) { return o is FontFace f && Equals(f); }
        public override int GetHashCode()
        {
            unchecked { return ((file ?? family ?? "").GetHashCode() * 31 + weight) * 2 + (italic ? 1 : 0); }
        }
        public override string ToString() { return (file ?? family) + " " + weight + (italic ? " italic" : ""); }
    }

    public static class FontResolver
    {
        struct Bundled { public string family; public string file; public int weight; public bool italic; }

        static readonly Bundled[] bundled =
        {
            new Bundled { family = "cinzel", file = "Cinzel-Medium.ttf", weight = 500 },
            new Bundled { family = "cinzel", file = "Cinzel-Bold.ttf", weight = 700 },
            new Bundled { family = "marcellus", file = "Marcellus-Regular.ttf", weight = 400 },
            new Bundled { family = "cormorant garamond", file = "CormorantGaramond-Regular.ttf", weight = 400 },
            new Bundled { family = "cormorant garamond", file = "CormorantGaramond-Italic.ttf", weight = 400, italic = true },
            new Bundled { family = "cormorant garamond", file = "CormorantGaramond-MediumItalic.ttf", weight = 500, italic = true },
            new Bundled { family = "cormorant garamond", file = "CormorantGaramond-SemiBold.ttf", weight = 600 },
        };

        /// <summary>Families a platform has installed (lower case); generic names are always available.</summary>
        public static Func<string, bool> systemHas = f => false;

        static readonly Dictionary<string, FontFace> memo = new Dictionary<string, FontFace>();

        public static FontFace Resolve(string css)
        {
            lock (memo)
            {
                if (memo.TryGetValue(css ?? "", out var f)) return f;
                f = ResolveUncached(FontSpec.Parse(css));
                memo[css ?? ""] = f;
                return f;
            }
        }

        static FontFace ResolveUncached(FontSpec fs)
        {
            var fams = (fs.family ?? "serif").Split(',');
            foreach (var raw in fams)
            {
                string fam = raw.Trim().Trim('"', '\'').Trim().ToLowerInvariant();
                if (fam.Length == 0) continue;
                int best = -1;
                for (int i = 0; i < bundled.Length; i++)
                {
                    if (bundled[i].family != fam) continue;
                    if (best < 0 || Better(bundled[i], bundled[best], fs.weight, fs.italic)) best = i;
                }
                if (best >= 0) return new FontFace { file = bundled[best].file, weight = bundled[best].weight, italic = bundled[best].italic };
                if (fam == "serif" || fam == "sans-serif" || fam == "monospace" || fam == "cursive" || fam == "system-ui")
                    return new FontFace { family = fam, weight = fs.weight, italic = fs.italic };
                if (systemHas(fam)) return new FontFace { family = raw.Trim().Trim('"', '\'').Trim(), weight = fs.weight, italic = fs.italic };
            }
            return new FontFace { family = "serif", weight = fs.weight, italic = fs.italic };
        }

        /// <summary>CSS font matching: style first, then weight by the CSS Fonts 4 rules.</summary>
        static bool Better(Bundled a, Bundled b, int w, bool italic)
        {
            bool sa = a.italic == italic, sb = b.italic == italic;
            if (sa != sb) return sa;
            return WeightRank(a.weight, w) < WeightRank(b.weight, w);
        }

        static int WeightRank(int have, int want)
        {
            if (have == want) return 0;
            if (want >= 400 && want <= 500)
            {
                // the target, then up to 500, then lighter (descending), then heavier (ascending)
                if (have > want && have <= 500) return 1000 + have - want;
                if (have < want) return 2000 + want - have;
                return 3000 + have - want;
            }
            if (want < 400)
            {
                if (have < want) return 1000 + want - have;
                return 2000 + have - want;
            }
            if (have > want) return 1000 + have - want;
            return 2000 + want - have;
        }
    }
}
