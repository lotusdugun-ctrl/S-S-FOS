using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sisifos.Paint;
using UnityEngine;
using UnityEngine.UIElements;
using LineCap = Sisifos.Paint.LineCap;
using LineJoin = Sisifos.Paint.LineJoin;

namespace Sisifos.App.UI
{
    /// <summary>
    /// Small helpers shared by the story views (StoryView, PrologueView, WisdomBookView):
    /// plain boxes and labels with no theme styling, the word-by-word text flow the web
    /// builds out of inline-block spans (Reveal, the prologue cards, line-clamped entries),
    /// and the CSS backgrounds (gradients, box-shadows) painted with Sisifos.Paint.Ctx.
    /// One UI unit is one CSS px (UiKit); rem and Tailwind spacing go through UiKit.Rem.
    /// </summary>
    internal static class SU
    {
        /// <summary>cubic-bezier(0.2, 0.7, 0.2, 1): every entrance in styles.css</summary>
        public static readonly Func<double, double> Out = UiKit.Bezier(0.2, 0.7, 0.2, 1);

        /// <summary>Tailwind spacing unit n (0.25rem each)</summary>
        public static float Sp(double n) { return UiKit.Rem(n * 0.25); }
        public static float Rem(double r) { return UiKit.Rem(r); }
        public static Color C(string css) { return UiKit.C(css); }

        /// <summary>space advance of the bundled faces, in em (from their hmtx tables)</summary>
        public static double SpaceEm(string family)
        {
            if (family == UiKit.CINZEL) return 0.25;
            if (family == UiKit.MARCELLUS) return 0.2998046875;
            if (family == UiKit.CORMORANT) return 0.234;
            return 0.25;
        }

        public static VisualElement Box(VisualElement parent = null)
        {
            var e = new VisualElement();
            e.pickingMode = PickingMode.Ignore;
            if (parent != null) parent.Add(e);
            return e;
        }

        /// <summary>a label with none of the default theme's margins and paddings</summary>
        public static Label Text(string text, VisualElement parent = null)
        {
            var l = new Label(text);
            l.pickingMode = PickingMode.Ignore;
            Zero(l);
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.enableRichText = false;
            if (parent != null) parent.Add(l);
            return l;
        }

        public static void Zero(VisualElement e)
        {
            var s = e.style;
            s.marginLeft = 0; s.marginRight = 0; s.marginTop = 0; s.marginBottom = 0;
            s.paddingLeft = 0; s.paddingRight = 0; s.paddingTop = 0; s.paddingBottom = 0;
            s.borderLeftWidth = 0; s.borderRightWidth = 0; s.borderTopWidth = 0; s.borderBottomWidth = 0;
            s.minWidth = 0; s.minHeight = 0;
        }

        /// <summary>a font of the web build: weights/styles the face lacks are synthesised as the browser does</summary>
        public static void Font(VisualElement e, string family, int weight = 400, bool italic = false)
        {
            UiKit.SetFont(e, family, weight, italic);
            // Marcellus has one face: the browser fakes bold (600+) and italic for it
            bool fakeBold = family == UiKit.MARCELLUS && weight >= 600;
            bool fakeItalic = family == UiKit.MARCELLUS && italic;
            e.style.unityFontStyleAndWeight = fakeBold && fakeItalic ? FontStyle.BoldAndItalic
                : fakeBold ? FontStyle.Bold : fakeItalic ? FontStyle.Italic : FontStyle.Normal;
        }

        /// <summary>font-size in px, and letter-spacing in em (0 for hi/bn/ar when `tracking` is a tracking-* class)</summary>
        public static void Size(VisualElement e, float px) { e.style.fontSize = px; }

        public static void Pad(VisualElement e, float x, float y)
        {
            e.style.paddingLeft = x; e.style.paddingRight = x; e.style.paddingTop = y; e.style.paddingBottom = y;
        }

        public static void Abs(VisualElement e, float? left, float? top, float? right, float? bottom)
        {
            e.style.position = Position.Absolute;
            e.style.left = left.HasValue ? new StyleLength(left.Value) : new StyleLength(StyleKeyword.Auto);
            e.style.top = top.HasValue ? new StyleLength(top.Value) : new StyleLength(StyleKeyword.Auto);
            e.style.right = right.HasValue ? new StyleLength(right.Value) : new StyleLength(StyleKeyword.Auto);
            e.style.bottom = bottom.HasValue ? new StyleLength(bottom.Value) : new StyleLength(StyleKeyword.Auto);
        }

        public static void Fill(VisualElement e) { Abs(e, 0, 0, 0, 0); }

        public static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        public static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w; e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftColor = c; e.style.borderRightColor = c; e.style.borderTopColor = c; e.style.borderBottomColor = c;
        }

        public static void BorderColor(VisualElement e, Color c)
        {
            e.style.borderLeftColor = c; e.style.borderRightColor = c; e.style.borderTopColor = c; e.style.borderBottomColor = c;
        }

        public static void TranslateY(VisualElement e, float px) { e.style.translate = new Translate(0, px); }

        public static Color Alpha(Color c, double a) { c.a = (float)(c.a * a); return c; }

        /// <summary>a looping CSS animation with a delay and `ease-in-out` keyframes 0%, 50%, 100%</summary>
        public static double PingPong(double phase)
        {
            // 0 → 1 → 0, each half eased with `ease-in-out` (the CSS timing per keyframe segment)
            return phase < 0.5 ? UiKit.EaseInOut(phase * 2) : UiKit.EaseInOut((1 - phase) * 2);
        }

        /// <summary>text-transform: uppercase, by the rules of the page's language (Turkish i → İ)</summary>
        public static string Upper(string s, string lang)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string u = lang == "tr" ? s.ToUpper(new System.Globalization.CultureInfo("tr-TR"))
                : s.ToUpperInvariant();
            return u.Replace("ß", "SS");
        }

        /// <summary>the scripts whose letters join: letter-spacing pulls them apart (styles.css)</summary>
        public static bool Joined(string lang) { return lang == "hi" || lang == "bn" || lang == "ar"; }

        /// <summary>a `tracking-[em]` class: em × font size, or normal for hi/bn/ar</summary>
        public static float Track(double em, float fontPx, string lang) { return Joined(lang) ? 0 : (float)(em * fontPx); }

        /// <summary>:focus-visible: true while the last input was a key, not a pointer</summary>
        public static bool keyboardMode;

        // ------------------------------------------------------------------ text

        static readonly Regex NoSpaceScript = new Regex("[぀-鿿＀-￯]");
        static readonly Regex Ws = new Regex("^\\s+$");
        static readonly Regex OpenQ = new Regex("[“「«\"]");
        static readonly Regex CloseQ = new Regex("[”」»\"]");
        static readonly Regex OnlyOpenQ = new Regex("^[“「«\"]$");

        public static bool NoSpaces(string text) { return NoSpaceScript.IsMatch(text ?? ""); }
        public static bool IsWs(string s) { return Ws.IsMatch(s); }

        /// <summary>`text.split(/(\s+)/)`: words and the runs of space between them</summary>
        public static List<string> SplitWords(string text)
        {
            var out_ = new List<string>();
            if (string.IsNullOrEmpty(text)) { out_.Add(""); return out_; }
            int i = 0;
            while (i < text.Length)
            {
                bool w = char.IsWhiteSpace(text[i]);
                int j = i;
                while (j < text.Length && char.IsWhiteSpace(text[j]) == w) j++;
                out_.Add(text.Substring(i, j - i));
                i = j;
            }
            // JS split keeps an empty string at an edge that starts or ends with a separator
            if (char.IsWhiteSpace(text[0])) out_.Insert(0, "");
            if (char.IsWhiteSpace(text[text.Length - 1])) out_.Add("");
            return out_;
        }

        /// <summary>`Array.from(text)`: code points, not UTF-16 units</summary>
        public static List<string> CodePoints(string text)
        {
            var out_ = new List<string>();
            if (string.IsNullOrEmpty(text)) return out_;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    out_.Add(text.Substring(i, 2));
                    i++;
                }
                else out_.Add(text[i].ToString());
            }
            return out_;
        }

        public struct Tok
        {
            public string t;
            public bool ws;
            /// <summary>inside “quoted words”: set in gold</summary>
            public bool q;
        }

        /// <summary>
        /// Reveal / Prologue `tokens`: words (or characters, for scripts written without
        /// spaces) with the quoted ones marked. Empty parts are dropped; whitespace kept.
        /// </summary>
        public static List<Tok> Tokens(string text)
        {
            var out_ = new List<Tok>();
            bool noSpaces = NoSpaces(text);
            var parts = noSpaces ? CodePoints(text) : SplitWords(text);
            bool quoted = false;
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (IsWs(p)) { out_.Add(new Tok { t = p, ws = true, q = quoted }); continue; }
                if (OpenQ.IsMatch(p)) quoted = true;
                bool q = quoted;
                if (CloseQ.IsMatch(p) && !OnlyOpenQ.IsMatch(p)) quoted = false;
                out_.Add(new Tok { t = p, q = q });
            }
            return out_;
        }

        /// <summary>reveal-time.ts: how long a line takes to come in at `step` ms a word</summary>
        public static double RevealTime(string text, double step = 45)
        {
            bool noSpaces = NoSpaces(text);
            int n;
            if (noSpaces) n = CodePoints(text).Count;
            else n = Regex.Split(text ?? "", "\\s+").Length;
            return n * step + 900;
        }

        /// <summary>dir="auto": the first strong character decides</summary>
        public static bool AutoRtl(string text)
        {
            if (text == null) return false;
            foreach (char ch in text)
            {
                if ((ch >= '֐' && ch <= 'ࣿ') || (ch >= 'יִ' && ch <= '﷿') || (ch >= 'ﹰ' && ch <= '﻿'))
                    return true;
                if (char.IsLetter(ch)) return false;
            }
            return false;
        }

        // ------------------------------------------------------------------ painting

        /// <summary>the gradient line of CSS `linear-gradient(<deg>)` over a w×h box</summary>
        public static LinearGradient CssLinear(Ctx c, double x, double y, double w, double h, double deg)
        {
            double a = deg * Math.PI / 180;
            double dx = Math.Sin(a), dy = -Math.Cos(a);
            double len = Math.Abs(w * Math.Sin(a)) + Math.Abs(h * Math.Cos(a));
            double cx = x + w / 2, cy = y + h / 2;
            return c.CreateLinearGradient(cx - dx * len / 2, cy - dy * len / 2, cx + dx * len / 2, cy + dy * len / 2);
        }

        /// <summary>
        /// CSS `radial-gradient(ellipse at px py, ...)` (farthest-corner) filling a w×h box.
        /// Stops are in 0..1 of the gradient ray; `stops` = (offset, colour).
        /// </summary>
        public static void CssRadialEllipse(Ctx c, double w, double h, double px, double py, params (double o, Col col)[] stops)
        {
            double cx = w * px, cy = h * py;
            double fx = Math.Max(cx, w - cx), fy = Math.Max(cy, h - cy);
            double rx = fx * Math.Sqrt(2), ry = fy * Math.Sqrt(2);
            if (rx <= 0 || ry <= 0) return;
            c.Save();
            c.BeginPath();
            c.Rect(0, 0, w, h);
            c.Clip();
            c.Translate(cx, cy);
            c.Scale(1, ry / rx);
            var g = c.CreateRadialGradient(0, 0, 0, 0, 0, rx);
            foreach (var s in stops) g.AddColorStop(s.o, s.col);
            c.FillStyle = g;
            double k = rx / ry;
            c.FillRect(-cx, -cy * k, w, h * k);
            c.Restore();
        }

        /// <summary>CSS `radial-gradient(circle at px py, ...)` (farthest-corner) filling a w×h box</summary>
        public static void CssRadialCircle(Ctx c, double w, double h, double px, double py, params (double o, Col col)[] stops)
        {
            double cx = w * px, cy = h * py;
            double fx = Math.Max(cx, w - cx), fy = Math.Max(cy, h - cy);
            double r = Math.Sqrt(fx * fx + fy * fy);
            if (r <= 0) return;
            var g = c.CreateRadialGradient(cx, cy, 0, cx, cy, r);
            foreach (var s in stops) g.AddColorStop(s.o, s.col);
            c.FillStyle = g;
            c.FillRect(0, 0, w, h);
        }

        public static void RRect(Ctx c, double x, double y, double w, double h, double r)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2));
            c.MoveTo(x + r, y);
            c.LineTo(x + w - r, y);
            if (r > 0) c.ArcTo(x + w, y, x + w, y + r, r);
            c.LineTo(x + w, y + h - r);
            if (r > 0) c.ArcTo(x + w, y + h, x + w - r, y + h, r);
            c.LineTo(x + r, y + h);
            if (r > 0) c.ArcTo(x, y + h, x, y + h - r, r);
            c.LineTo(x, y + r);
            if (r > 0) c.ArcTo(x, y, x + r, y, r);
            c.ClosePath();
        }

        /// <summary>
        /// An outer CSS box-shadow of the box (x,y,w,h,r), only outside the box as CSS draws it.
        /// The canvas must have room around the box for the blur, and nothing under the box yet:
        /// the shape is drawn with its shadow and the box is then cut out. (A blurred shadow
        /// with a positive spread would lose the part under its own shape; none is used.)
        /// </summary>
        public static void BoxShadow(Ctx c, double x, double y, double w, double h, double r,
            double ox, double oy, double blur, double spread, Col color)
        {
            double sx = x - spread, sy = y - spread, sw = w + spread * 2, sh = h + spread * 2;
            double sr = r > 0 ? Math.Max(0, r + spread) : 0;
            c.Save();
            if (blur <= 0)
            {
                // a hard ring or offset copy: the shape minus the box
                c.FillStyle = color;
                c.BeginPath();
                if (sw > 0 && sh > 0) RRect(c, sx + ox, sy + oy, sw, sh, sr);
                RRect(c, x, y, w, h, r);
                c.Fill("evenodd");
                c.Restore();
                return;
            }
            if (sw > 0 && sh > 0)
            {
                // shadows are in device pixels: the canvas transform does not reach them
                double k = Scale(c);
                c.ShadowColor = color;
                c.ShadowBlur = blur * k;
                c.ShadowOffsetX = ox * k;
                c.ShadowOffsetY = oy * k;
                // opaque: the shadow's alpha is the shape's times the shadow colour's
                c.FillStyle = color.WithAlpha(1);
                c.BeginPath();
                RRect(c, sx, sy, sw, sh, sr);
                c.Fill();
            }
            c.Restore();
            c.Save();
            c.GlobalCompositeOperation = Composite.DestinationOut;
            c.FillStyle = Col.Black;
            c.BeginPath();
            RRect(c, x, y, w, h, r);
            c.Fill();
            c.Restore();
        }

        /// <summary>the device pixels per CSS px of the canvas transform (shadows are not transformed)</summary>
        public static double Scale(Ctx c)
        {
            var m = c.GetTransform();
            return Math.Sqrt(Math.Abs(m.a * m.d - m.b * m.c));
        }

        static readonly Dictionary<long, Surface> scratch = new Dictionary<long, Surface>();

        /// <summary>
        /// An inset CSS box-shadow: only inside the box. The blurred one is made on a scratch
        /// surface larger than the canvas, so the dark that comes in from outside the box is
        /// there to blur even where the box meets the canvas edge.
        /// </summary>
        public static void InsetShadow(Ctx c, double x, double y, double w, double h, double r,
            double ox, double oy, double blur, double spread, Col color)
        {
            double ix = x + spread + ox, iy = y + spread + oy, iw = w - spread * 2, ih = h - spread * 2;
            double ir = Math.Max(0, r - spread);
            if (blur <= 0)
            {
                c.Save();
                c.BeginPath();
                RRect(c, x, y, w, h, r);
                c.Clip();
                c.FillStyle = color;
                c.BeginPath();
                c.Rect(x - 10, y - 10, w + 20, h + 20);
                if (iw > 0 && ih > 0) RRect(c, ix, iy, iw, ih, ir);
                c.Fill("evenodd");
                c.Restore();
                return;
            }
            var m = c.GetTransform();
            double k = Scale(c);
            int margin = (int)Math.Ceiling((blur * 2 + Math.Abs(ox) + Math.Abs(oy) + Math.Abs(spread) + 4) * k);
            int sw = c.canvas.width + margin * 2, sh = c.canvas.height + margin * 2;
            long key = ((long)sw << 32) | (uint)sh;
            Surface t;
            if (!scratch.TryGetValue(key, out t)) { t = new Surface(sw, sh); scratch[key] = t; }
            var tc = t.GetContext();
            tc.ResetTransform();
            tc.ClearRect(0, 0, sw, sh);
            tc.Save();
            tc.Translate(margin, margin);
            tc.Transform(m.a, m.b, m.c, m.d, m.e, m.f);
            tc.BeginPath();
            RRect(tc, x, y, w, h, r);
            tc.Clip();
            double pad = blur * 3 + Math.Abs(ox) + Math.Abs(oy) + Math.Abs(spread) + 4;
            tc.ShadowColor = color;
            tc.ShadowBlur = blur * k;
            tc.FillStyle = color.WithAlpha(1);
            tc.BeginPath();
            tc.Rect(x - pad, y - pad, w + pad * 2, h + pad * 2);
            if (iw > 0 && ih > 0) RRect(tc, ix, iy, iw, ih, ir);
            tc.Fill("evenodd");
            tc.Restore();
            c.Save();
            c.ResetTransform();
            c.DrawImage(t, -margin, -margin);
            c.Restore();
        }
    }

    /// <summary>
    /// A paragraph laid out as the web lays out its inline-block word spans: each word is
    /// its own label (so it can be animated alone), the space between words is the face's
    /// own space advance, lines wrap between words, and each line is exactly
    /// `lineHeight × fontSize` tall. Right-to-left text runs the words from the right.
    /// </summary>
    internal sealed class Flow : VisualElement
    {
        public sealed class Opts
        {
            public string family = UiKit.MARCELLUS;
            public int weight = 400;
            public bool italic;
            public float size = 16;
            public double lineHeight = 1.5;
            public Color color = Color.white;
            /// <summary>colour of quoted words (null: as the rest)</summary>
            public Color? gold;
            /// <summary>quoted words in italic (the prologue)</summary>
            public bool goldItalic;
            /// <summary>"center", "left", "right" or "start" (start = left, or right in RTL)</summary>
            public string align = "center";
            /// <summary>true/false, or null for dir="auto"</summary>
            public bool? rtl;
            public float letterSpacing;
            /// <summary>line-clamp: 0 = none</summary>
            public int maxLines;
            public TextShadow? shadow;
            /// <summary>uppercase the words (text-transform)</summary>
            public bool upper;
        }

        public readonly List<Label> words = new List<Label>();
        /// <summary>the token index (counting whitespace too) of each word</summary>
        public readonly List<int> tokenIndex = new List<int>();
        readonly List<string> wordText = new List<string>();
        readonly List<UiKit.TweenHandle> tweens = new List<UiKit.TweenHandle>();
        Opts o;
        float gap;
        bool clamping;

        public Flow()
        {
            pickingMode = PickingMode.Ignore;
            SU.Zero(this);
            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            style.alignItems = Align.FlexStart;
            style.flexShrink = 0;
            RegisterCallback<GeometryChangedEvent>(OnGeometry);
        }

        public Opts opts { get { return o; } }
        public float lineBox { get { return (float)(o.size * o.lineHeight); } }

        public void Set(string text, Opts opts)
        {
            o = opts;
            StopReveal();
            Clear();
            words.Clear();
            tokenIndex.Clear();
            wordText.Clear();
            bool rtl = o.rtl ?? SU.AutoRtl(text);
            style.flexDirection = rtl ? FlexDirection.RowReverse : FlexDirection.Row;
            string align = o.align == "start" ? (rtl ? "right" : "left") : o.align;
            // in row-reverse the main axis starts at the right
            style.justifyContent = align == "center" ? Justify.Center
                : (align == "left") != rtl ? Justify.FlexStart : Justify.FlexEnd;
            gap = (float)(SU.SpaceEm(o.family) * o.size) + o.letterSpacing;
            var toks = SU.Tokens(text ?? "");
            bool anyWs = false;
            for (int k = 0; k < toks.Count; k++)
            {
                var tk = toks[k];
                if (tk.ws) { anyWs = true; continue; }
                string t = o.upper ? tk.t.ToUpperInvariant() : tk.t;
                var l = SU.Text(t, this);
                SU.Font(l, o.family, o.weight, o.italic || (tk.q && o.goldItalic));
                l.style.fontSize = o.size;
                l.style.height = lineBox;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.color = tk.q && o.gold.HasValue ? o.gold.Value : o.color;
                l.style.letterSpacing = o.letterSpacing;
                l.style.flexShrink = 0;
                if (o.shadow.HasValue) l.style.textShadow = o.shadow.Value;
                bool before = k > 0 && toks[k - 1].ws, after = k + 1 < toks.Count && toks[k + 1].ws;
                // the space between two words is split over both, so each line stays centred
                float lead = before ? gap / 2 : 0, trail = after ? gap / 2 : 0;
                l.style.marginLeft = rtl ? trail : lead;
                l.style.marginRight = rtl ? lead : trail;
                words.Add(l);
                tokenIndex.Add(k);
                wordText.Add(t);
            }
            // …and the half spaces at the ends of the lines are given back to the margins
            style.marginLeft = anyWs ? -gap / 2 : 0;
            style.marginRight = anyWs ? -gap / 2 : 0;
            if (words.Count == 0) style.minHeight = lineBox;
        }

        public void SetColor(Color c)
        {
            for (int i = 0; i < words.Count; i++) words[i].style.color = c;
        }

        /// <summary>
        /// The `prologue-word` animation on every word: out of nothing, from 0.45em lower and
        /// 0.12em wider spaced (the blur of the web build is not available in UI Toolkit 6.0).
        /// `delayOf(n, k)` gives the delay of the n-th word (token index k).
        /// </summary>
        public void Reveal(Func<int, int, double> delayOf)
        {
            StopReveal();
            for (int n = 0; n < words.Count; n++)
            {
                var l = words[n];
                float size = o.size, ls = o.letterSpacing;
                // PORT: filter: blur(10px → 0) is dropped (no filters in UI Toolkit 6.0)
                tweens.Add(UiKit.Tween(l, 1100, delayOf(n, tokenIndex[n]), SU.Out, e =>
                {
                    l.style.opacity = (float)e;
                    l.style.translate = new Translate(0, (float)((1 - e) * 0.45 * size));
                    l.style.letterSpacing = (float)(ls + (1 - e) * 0.12 * size);
                }));
            }
        }

        /// <summary>`reveal-done`: the whole line at once</summary>
        public void Finish()
        {
            StopReveal();
            for (int n = 0; n < words.Count; n++)
            {
                words[n].style.opacity = 1;
                words[n].style.translate = new Translate(0, 0);
                words[n].style.letterSpacing = o.letterSpacing;
            }
        }

        void StopReveal()
        {
            foreach (var t in tweens) if (t != null) t.Stop();
            tweens.Clear();
        }

        // line-clamp: words past the last line are hidden, and the last one shown ends in "…"
        void OnGeometry(GeometryChangedEvent e)
        {
            if (o == null || o.maxLines <= 0 || clamping || words.Count == 0) return;
            if (Math.Abs(e.newRect.width - e.oldRect.width) > 0.5f) { Unclamp(); return; }
            clamping = true;
            try
            {
                float lh = lineBox;
                int lastShown = -1;
                bool over = false;
                for (int i = 0; i < words.Count; i++)
                {
                    if (words[i].style.display.value == DisplayStyle.None) { over = true; continue; }
                    int line = (int)Math.Round(words[i].layout.y / lh);
                    if (line >= o.maxLines) { words[i].style.display = DisplayStyle.None; over = true; }
                    else lastShown = i;
                }
                if (over && lastShown >= 0)
                {
                    for (int i = 0; i < words.Count; i++) words[i].text = wordText[i];
                    words[lastShown].text = wordText[lastShown] + "…";
                    // if the ellipsis pushed it onto a new line, the next pass hides it too
                }
            }
            finally { clamping = false; }
        }

        /// <summary>show every word again (after a width change) and clamp anew</summary>
        public void Unclamp()
        {
            for (int i = 0; i < words.Count; i++)
            {
                words[i].style.display = DisplayStyle.Flex;
                words[i].text = wordText[i];
            }
        }
    }

    /// <summary>a box painted with Ctx; `draw` is called with its size in CSS px</summary>
    internal sealed class PaintBox : CanvasBox
    {
        public PaintBox(Action<Ctx, double, double> draw) : base(draw) { pickingMode = PickingMode.Ignore; }
    }

    /// <summary>
    /// A CSS box-shadow around a rounded box that can fade in and out (hover glows,
    /// `prologue-glow`): absolutely placed over its host, painting only outside the host.
    /// </summary>
    internal sealed class Glow : VisualElement
    {
        readonly float pad;
        public Glow(VisualElement host, float blur, Color color, float radius = -1, float oy = 0, float spread = 0)
        {
            pickingMode = PickingMode.Ignore;
            pad = blur * 1.5f + Math.Abs(oy) + Math.Abs(spread) + 4;
            SU.Abs(this, -pad, -pad, -pad, -pad);
            var col = new Col(color.r, color.g, color.b, color.a);
            var box = new PaintBox((c, w, h) =>
            {
                double bw = w - pad * 2, bh = h - pad * 2;
                double r = radius < 0 ? Math.Min(bw, bh) / 2 : radius;
                SU.BoxShadow(c, pad, pad, bw, bh, r, 0, oy, blur, spread, col);
            });
            SU.Fill(box);
            Add(box);
            style.opacity = 0;
            host.Add(this);
            // behind the host's own content
            SendToBack();
        }
        public float level { set { style.opacity = value; } }
    }

    /// <summary>
    /// A rounded-full bordered button of the story screens (scene answers, begin, continue,
    /// close): a label in a pill that takes focus, answers Enter/Space, and on hover or
    /// keyboard focus shows a soft background and glow (`.scene-btn:hover`).
    /// </summary>
    internal sealed class Pill : VisualElement
    {
        public readonly Label label;
        public Action onClick;
        public Glow hoverGlow;
        public Color baseBg = new Color(0, 0, 0, 0), hoverBg = new Color(0, 0, 0, 0);
        public Color? hoverBorder;
        Color border;
        public float baseOpacity = 1, hoverOpacity = 1;
        bool hover, focused;
        UiKit.TweenHandle fade;
        double level;

        public Pill(string text, Action onClick)
        {
            this.onClick = onClick;
            SU.Zero(this);
            focusable = true;
            pickingMode = PickingMode.Position;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.flexShrink = 0;
            SU.Radius(this, 9999);
            label = SU.Text(text, this);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            RegisterCallback<ClickEvent>(e => { e.StopPropagation(); if (enabledSelf && onClick != null) onClick(); });
            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space)
                {
                    e.StopPropagation();
                    if (enabledSelf && onClick != null) onClick();
                }
            });
            RegisterCallback<PointerEnterEvent>(e => { hover = true; Apply(); });
            RegisterCallback<PointerLeaveEvent>(e => { hover = false; Apply(); });
            RegisterCallback<FocusInEvent>(e => { focused = SU.keyboardMode; Apply(); });
            RegisterCallback<FocusOutEvent>(e => { focused = false; Apply(); });
        }

        public void SetBorder(float w, Color c) { border = c; SU.Border(this, w, c); }

        /// <summary>`transition: background .25s, box-shadow .25s`</summary>
        void Apply()
        {
            double to = hover || focused ? 1 : 0;
            double from = level;
            if (fade != null) fade.Stop();
            fade = UiKit.Tween(this, 250, 0, UiKit.Ease, k =>
            {
                level = from + (to - from) * k;
                style.backgroundColor = Color.Lerp(baseBg, hoverBg, (float)level);
                if (hoverGlow != null) hoverGlow.level = (float)level;
                if (hoverBorder.HasValue) SU.BorderColor(this, Color.Lerp(border, hoverBorder.Value, (float)level));
                if (hoverOpacity != baseOpacity) style.opacity = (float)(baseOpacity + (hoverOpacity - baseOpacity) * level);
            });
        }
    }

    /// <summary>runs `f` once after `ms` on the element's scheduler; Cancel() stops it</summary>
    internal sealed class Timer
    {
        IVisualElementScheduledItem item;
        public static Timer After(VisualElement owner, double ms, Action f)
        {
            var t = new Timer();
            t.item = owner.schedule.Execute(f).StartingIn((long)Math.Max(0, ms));
            return t;
        }
        public void Cancel() { if (item != null) item.Pause(); item = null; }
    }

    /// <summary>a window-level keydown listener (capture phase) while the owner is in a panel</summary>
    internal sealed class KeyHook
    {
        readonly VisualElement owner;
        readonly EventCallback<KeyDownEvent> cb;
        readonly EventCallback<PointerDownEvent> pd;
        VisualElement root;
        public KeyHook(VisualElement owner, Action<KeyDownEvent> f)
        {
            this.owner = owner;
            cb = e => { if (e.keyCode != KeyCode.None) { SU.keyboardMode = true; f(e); } };
            pd = e => SU.keyboardMode = false;
            owner.RegisterCallback<AttachToPanelEvent>(e => Hook());
            owner.RegisterCallback<DetachFromPanelEvent>(e => Unhook());
            if (owner.panel != null) Hook();
        }
        void Hook()
        {
            Unhook();
            root = owner.panel != null ? owner.panel.visualTree : null;
            if (root == null) return;
            root.RegisterCallback(cb, TrickleDown.TrickleDown);
            root.RegisterCallback(pd, TrickleDown.TrickleDown);
        }
        void Unhook()
        {
            if (root == null) return;
            root.UnregisterCallback(cb, TrickleDown.TrickleDown);
            root.UnregisterCallback(pd, TrickleDown.TrickleDown);
            root = null;
        }
    }
}
