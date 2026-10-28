// The UI Toolkit side of the Unity host: the panel (created at runtime, no editor-made UXML/USS),
// the fonts, CSS colours and lengths, tweens standing in for CSS keyframes, and canvas-painted
// boxes for what UI Toolkit 6.0 cannot draw (gradients, box-shadows, SVG icons).
//
// Scaling: 1 UI unit == 1 CSS pixel. The web build laid out in CSS px (device px / devicePixelRatio);
// the panel uses ConstantPixelSize with scale = GameRoot.devicePixelRatio, so every number copied
// from the Tailwind classes (p-4 = 16, text-[0.7rem] = 11.2 ...) is used as is.
//
// Complex scripts (Arabic, Devanagari, Bengali, CJK): every text element uses the Advanced Text
// Generator (HarfBuzz shaping + ICU bidi/line breaking), set on the root and inherited. Unity 6.0
// only switches it on when TextUtilities.IsAdvancedTextEnabled says so — a delegate the EDITOR sets
// from Project Settings > UI Toolkit > "Enable Advanced Text", and nobody sets in a player. Init()
// sets it by reflection, and takes the ICU data from the PanelSettings asset that
// Editor/SisifosUiSetup.cs creates (Resources/UI/SisifosPanel.asset). Missing glyphs (Arabic,
// Devanagari, Bengali, Han) come from the OS fonts: TextSettings' OS fallback list
// (Font.GetOSFallbacks) plus the script fonts named below.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Sisifos.Paint;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using LineCap = Sisifos.Paint.LineCap;
using LineJoin = Sisifos.Paint.LineJoin;

namespace Sisifos.App.UI
{
    public static class UiKit
    {
        // ------------------------------------------------------------ fonts

        public const string CINZEL = "Cinzel";
        public const string CORMORANT = "Cormorant Garamond";
        public const string MARCELLUS = "Marcellus";
        /// <summary>GameExtras' SERIF stack: Cormorant Garamond</summary>
        public const string SERIF = CORMORANT;
        /// <summary>Tailwind's font-sans (system-ui): Roboto on Android, San Francisco on iOS</summary>
        public const string SANS = "sans";
        /// <summary>font-mono (the kbd keys)</summary>
        public const string MONO = "mono";

        struct Face
        {
            public string file; // Resources/Fonts/<file>, or null for an OS font
            public int weight;
            public bool italic;
        }

        static readonly Dictionary<string, Face[]> faces = new Dictionary<string, Face[]>
        {
            { CINZEL, new[] { new Face { file = "Cinzel-Medium", weight = 500 }, new Face { file = "Cinzel-Bold", weight = 700 } } },
            {
                CORMORANT, new[]
                {
                    new Face { file = "CormorantGaramond-Regular", weight = 400 },
                    new Face { file = "CormorantGaramond-SemiBold", weight = 600 },
                    new Face { file = "CormorantGaramond-Italic", weight = 400, italic = true },
                    new Face { file = "CormorantGaramond-MediumItalic", weight = 500, italic = true },
                }
            },
            { MARCELLUS, new[] { new Face { file = "Marcellus-Regular", weight = 400 } } },
        };

        static readonly Dictionary<string, FontAsset> assets = new Dictionary<string, FontAsset>();
        static readonly List<FontAsset> scriptFallbacks = new List<FontAsset>();

        /// <summary>
        /// The CSS font-matching rule (CSS Fonts 4 §5.2) over the faces the web build loaded:
        /// italic faces first when italic is asked for, then the weight order of the spec.
        /// </summary>
        static Face Match(Face[] list, int weight, bool italic)
        {
            var pool = new List<Face>();
            foreach (var f in list) if (f.italic == italic) pool.Add(f);
            if (pool.Count == 0) pool.AddRange(list);
            Face best = pool[0];
            int Rank(Face f)
            {
                int w = f.weight;
                if (w == weight) return 0;
                if (weight >= 400 && weight <= 500)
                {
                    if (w > weight && w <= 500) return w - weight;
                    if (w < weight) return 1000 + (weight - w);
                    return 2000 + (w - weight);
                }
                if (weight < 400) return w < weight ? weight - w : 1000 + (w - weight);
                return w > weight ? w - weight : 1000 + (weight - w);
            }
            foreach (var f in pool) if (Rank(f) < Rank(best)) best = f;
            return best;
        }

        static FontAsset Asset(string key, Func<FontAsset> make)
        {
            if (assets.TryGetValue(key, out var a)) return a;
            try { a = make(); } catch (Exception e) { Debug.LogException(e); a = null; }
            if (a != null)
            {
                a.isMultiAtlasTexturesEnabled = true;
                if (a.fallbackFontAssetTable == null) a.fallbackFontAssetTable = new List<FontAsset>();
                foreach (var fb in scriptFallbacks) if (fb != a && !a.fallbackFontAssetTable.Contains(fb)) a.fallbackFontAssetTable.Add(fb);
            }
            assets[key] = a;
            return a;
        }

        static FontAsset OsFont(string[] families, string style)
        {
            foreach (var fam in families)
            {
                FontAsset a = null;
                try { a = FontAsset.CreateFontAsset(fam, style); } catch (Exception) { a = null; }
                if (a != null) return a;
            }
            return null;
        }

        static string[] SansFamilies()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new[] { "Roboto", "Google Sans", "Noto Sans", "Droid Sans" };
#elif UNITY_IOS && !UNITY_EDITOR
            return new[] { "SF Pro Text", ".SF UI Text", "Helvetica Neue", "Helvetica" };
#else
            return new[] { "Segoe UI", "Helvetica Neue", "Arial", "Liberation Sans", "DejaVu Sans" };
#endif
        }

        static string[] MonoFamilies()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new[] { "Roboto Mono", "Droid Sans Mono", "Noto Sans Mono" };
#elif UNITY_IOS && !UNITY_EDITOR
            return new[] { "Menlo", "Courier New" };
#else
            return new[] { "Consolas", "Menlo", "Courier New", "DejaVu Sans Mono", "Liberation Mono" };
#endif
        }

        /// <summary>Fonts for the scripts the game's own faces do not have, in the order a browser would try them.</summary>
        static string[][] ScriptFamilies()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new[]
            {
                new[] { "Noto Naskh Arabic", "Noto Sans Arabic", "Droid Arabic Naskh" },
                new[] { "Noto Sans Devanagari", "Noto Serif Devanagari" },
                new[] { "Noto Sans Bengali", "Noto Serif Bengali" },
                new[] { "Noto Sans CJK SC", "Noto Sans SC", "Noto Sans CJK", "Droid Sans Fallback" },
            };
#elif UNITY_IOS && !UNITY_EDITOR
            return new[]
            {
                new[] { "Geeza Pro" },
                new[] { "Kohinoor Devanagari", "Devanagari Sangam MN" },
                new[] { "Kohinoor Bangla", "Bangla Sangam MN" },
                new[] { "PingFang SC", "Heiti SC" },
            };
#else
            return new[]
            {
                new[] { "Segoe UI", "Geeza Pro", "Noto Naskh Arabic", "Arial" },
                new[] { "Nirmala UI", "Kohinoor Devanagari", "Noto Sans Devanagari", "Mangal" },
                new[] { "Nirmala UI", "Kohinoor Bangla", "Noto Sans Bengali", "Vrinda" },
                new[] { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "SimSun" },
            };
#endif
        }

        static void LoadScriptFallbacks()
        {
            if (scriptFallbacks.Count > 0) return;
            foreach (var fams in ScriptFamilies())
            {
                var a = OsFont(fams, "Regular");
                if (a != null && !scriptFallbacks.Contains(a)) scriptFallbacks.Add(a);
            }
        }

        static FontAsset Resolve(string family, int weight, bool italic, out bool synthBold, out bool synthItalic)
        {
            synthBold = false;
            synthItalic = false;
            if (family == SANS || family == MONO || family == null || !faces.ContainsKey(family))
            {
                bool mono = family == MONO;
                string style = weight >= 600 ? "Bold" : weight >= 500 && !mono ? "Medium" : "Regular";
                if (italic) style = style == "Regular" ? "Italic" : style + " Italic";
                var fams = mono ? MonoFamilies() : SansFamilies();
                var a = Asset((mono ? "mono/" : "sans/") + style, () => OsFont(fams, style));
                if (a == null && style != "Regular")
                {
                    a = Asset((mono ? "mono/" : "sans/") + "Regular", () => OsFont(fams, "Regular"));
                    synthBold = weight >= 600;
                    synthItalic = italic;
                }
                return a;
            }
            var f = Match(faces[family], weight, italic);
            // the browser fakes bold when asked for 600+ and the nearest face is 500 or lighter,
            // and fakes italic when no italic face exists
            synthBold = weight >= 600 && f.weight <= 500;
            synthItalic = italic && !f.italic;
            return Asset(f.file, () =>
            {
                var font = Resources.Load<Font>("Fonts/" + f.file);
                return font != null ? FontAsset.CreateFontAsset(font) : null;
            });
        }

        /// <summary>The font asset for a family at a CSS weight and style.</summary>
        public static FontDefinition Font(string family, int weight = 400, bool italic = false)
        {
            var a = Resolve(family, weight, italic, out _, out _);
            return a != null ? FontDefinition.FromSDFFont(a) : default;
        }

        /// <summary>font-family/weight/style on an element (synthesises bold/italic the way a browser does).</summary>
        public static void SetFont(VisualElement e, string family, int weight = 400, bool italic = false)
        {
            var a = Resolve(family, weight, italic, out bool sb, out bool si);
            if (a != null) e.style.unityFontDefinition = FontDefinition.FromSDFFont(a);
            e.style.unityFontStyleAndWeight = sb && si ? FontStyle.BoldAndItalic : sb ? FontStyle.Bold : si ? FontStyle.Italic : FontStyle.Normal;
        }

        // ------------------------------------------------------------ colours

        static readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();

        /// <summary>Any CSS colour (#hex, rgb(a), hsl(a), oklch, named) as a Unity colour (gamma space, as the browser).</summary>
        public static Color C(string css)
        {
            if (css == null) return new Color(0, 0, 0, 0);
            if (colors.TryGetValue(css, out var c)) return c;
            var p = Col.Parse(css);
            c = new Color((float)p.r, (float)p.g, (float)p.b, (float)p.a);
            colors[css] = c;
            return c;
        }

        /// <summary>A CSS colour with its alpha multiplied (Tailwind's color/60 on a token).</summary>
        public static Color C(string css, double alphaMul)
        {
            var c = C(css);
            c.a = (float)(c.a * alphaMul);
            return c;
        }

        public static Color A(Color c, double alphaMul)
        {
            c.a = (float)(c.a * alphaMul);
            return c;
        }

        // the theme tokens of styles.css :root
        public static readonly Color Background = C("oklch(0.15 0.018 265)");
        public static readonly Color Foreground = C("oklch(0.93 0.012 80)");
        public static readonly Color MutedFg = C("oklch(0.66 0.02 70)");
        public static readonly Color Accent = C("oklch(0.72 0.1 60)");
        public static readonly Color Border = C("oklch(0.35 0.02 265 / 0.6)");
        public static readonly Color Muted = C("oklch(0.24 0.02 265)");

        // ------------------------------------------------------------ lengths and media queries

        /// <summary>the root font size: 16, or 18 with "big text" (document.documentElement.style.fontSize)</summary>
        public static float rootFontSize = 16;

        /// <summary>CSS rem</summary>
        public static float Rem(double r) { return (float)(r * rootFontSize); }

        /// <summary>device px per CSS px</summary>
        public static float dpr = 1;

        /// <summary>the viewport in CSS px</summary>
        public static float viewW = 844, viewH = 390;

        /// <summary>safe-area insets in CSS px (notch, home indicator)</summary>
        public static float safeLeft, safeTop, safeRight, safeBottom;

        public static float Vw(double pct) { return (float)(viewW * pct / 100); }
        public static float Vh(double pct) { return (float)(viewH * pct / 100); }

        /// <summary>@media (max-height: px)</summary>
        public static bool MaxHeight(double px) { return viewH <= px; }

        /// <summary>Tailwind's sm: (min-width: 640px)</summary>
        public static bool Sm { get { return viewW >= 640; } }

        /// <summary>(pointer: coarse): a touch screen is the primary pointer</summary>
        public static bool Touch
        {
            get
            {
#if UNITY_EDITOR
                return false;
#else
                return Application.isMobilePlatform || (Input.touchSupported && !Input.mousePresent);
#endif
            }
        }

        /// <summary>a text field has the focus: the game's keyboard shortcuts stand down</summary>
        public static bool typing;

        /// <summary>resize, orientation, big text: re-apply sizes</summary>
        public static event Action changed;

        public static void NotifyChanged()
        {
            changed?.Invoke();
        }

        /// <summary>CSS letter-spacing in em (Tailwind tracking-[…]); joined scripts keep normal spacing (styles.css).</summary>
        public static void Tracking(VisualElement e, double em, string lang)
        {
            if (lang == "hi" || lang == "bn" || lang == "ar") { e.style.letterSpacing = 0; return; }
            float size = 16;
            var fs = e.style.fontSize;
            if (fs.keyword == StyleKeyword.Undefined && fs.value.value > 0) size = fs.value.value;
            e.style.letterSpacing = (float)(em * size);
        }

        /// <summary>dir="rtl" for Arabic (text direction only; rows are reversed with Row()).</summary>
        public static void Dir(VisualElement e, string lang)
        {
            e.languageDirection = lang == "ar" ? LanguageDirection.RTL : LanguageDirection.LTR;
        }

        /// <summary>flex-direction: row, mirrored under dir="rtl"</summary>
        public static void Row(VisualElement e, string lang)
        {
            e.style.flexDirection = lang == "ar" ? FlexDirection.RowReverse : FlexDirection.Row;
        }

        // ------------------------------------------------------------ small style helpers

        public static void Pad(VisualElement e, float x, float y)
        {
            e.style.paddingLeft = x; e.style.paddingRight = x; e.style.paddingTop = y; e.style.paddingBottom = y;
        }

        public static void Margin(VisualElement e, float top, float right, float bottom, float left)
        {
            e.style.marginTop = top; e.style.marginRight = right; e.style.marginBottom = bottom; e.style.marginLeft = left;
        }

        public static void BorderW(VisualElement e, float w)
        {
            e.style.borderTopWidth = w; e.style.borderRightWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w;
        }

        public static void BorderC(VisualElement e, Color c)
        {
            e.style.borderTopColor = c; e.style.borderRightColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c;
        }

        public static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        public static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.top = 0; e.style.right = 0; e.style.bottom = 0;
        }

        /// <summary>
        /// A Label: text, font, size (px), colour, tracking (em), language (tracking rule,
        /// uppercase casing) and CSS line-height (Tailwind's inherited 1.5 by default).
        /// </summary>
        public static Label Text(string text, string family, int weight, bool italic, float size, Color color, double trackingEm = 0, string lang = null, bool upper = false, double lh = 1.5)
        {
            var l = new Label();
            l.pickingMode = PickingMode.Ignore;
            Reset(l);
            if (family != null) SetFont(l, family, weight, italic);
            l.style.fontSize = size;
            l.style.color = color;
            if (trackingEm != 0) Tracking(l, trackingEm, lang);
            if (lh > 0) Leading(l, family, size, lh);
            SetText(l, upper ? Upper(text, lang) : text, lh > 0 ? lh : NaturalLine(family));
            return l;
        }

        /// <summary>Zero the default Label padding/margins UI Toolkit adds.</summary>
        public static void Reset(VisualElement l)
        {
            l.style.marginTop = 0; l.style.marginBottom = 0; l.style.marginLeft = 0; l.style.marginRight = 0;
            l.style.paddingTop = 0; l.style.paddingBottom = 0; l.style.paddingLeft = 0; l.style.paddingRight = 0;
            l.style.whiteSpace = WhiteSpace.Normal;
        }

        /// <summary>text-transform: uppercase, in the language's own casing (Turkish dotted i).</summary>
        public static string Upper(string s, string lang)
        {
            if (s == null) return "";
            CultureInfo ci;
            try { ci = lang == "tr" ? new CultureInfo("tr-TR") : CultureInfo.InvariantCulture; }
            catch (Exception) { ci = CultureInfo.InvariantCulture; }
            return s.ToUpper(ci);
        }

        /// <summary>The face's own line height (hhea ascent − descent + gap) / em, which UI Toolkit lays lines out with.</summary>
        public static double NaturalLine(string family)
        {
            switch (family)
            {
                case CINZEL: return 1.348;
                case CORMORANT: return 1.211;
                case MARCELLUS: return 1.254;
                case MONO: return 1.17;
                default:
#if UNITY_IOS && !UNITY_EDITOR
                    return 1.193; // San Francisco
#else
                    return 1.172; // Roboto
#endif
            }
        }

        /// <summary>
        /// CSS line-height on a label. UI Toolkit 6.0 has no line-height property: the half-leading
        /// above the first and below the last line becomes padding, and the spacing between lines
        /// a rich-text &lt;line-height&gt; tag (see SetText).
        /// </summary>
        public static void Leading(VisualElement e, string family, float size, double lh)
        {
            float pad = Math.Max(0, (float)((lh - NaturalLine(family)) * size / 2));
            e.style.paddingTop = pad;
            e.style.paddingBottom = pad;
        }

        /// <summary>
        /// Set a label's text with CSS line spacing (line-height × font size between baselines) for
        /// the lines after the first. Markup in the text is neutralised.
        /// </summary>
        public static void SetText(UnityEngine.UIElements.TextElement l, string text, double lh)
        {
            text = text ?? "";
            l.enableRichText = true;
            string safe = text.IndexOf('<') >= 0 ? text.Replace("<", "<noparse><</noparse>") : text;
            l.text = "<line-height=" + lh.ToString("0.###", CultureInfo.InvariantCulture) + "em>" + safe;
        }

        public static void Xf(VisualElement e, float tx = 0, float ty = 0, float scale = 1, float rotDeg = 0)
        {
            e.style.translate = new Translate(tx, ty);
            e.style.scale = new Scale(new Vector2(scale, scale));
            e.style.rotate = new Rotate(new Angle(rotDeg, AngleUnit.Degree));
        }

        // ------------------------------------------------------------ time and tweens

        /// <summary>performance.now() for the UI (ms)</summary>
        public static double Now { get { return Time.realtimeSinceStartupAsDouble * 1000.0; } }

        public sealed class TweenHandle
        {
            internal VisualElement owner;
            internal double start, dur, delay, period;
            internal Func<double, double> ease;
            internal Action<double> apply;
            internal Action done;
            internal bool loop, stopped, attached, begun;
            public bool running { get { return !stopped; } }
            public void Stop() { stopped = true; }
        }

        static readonly List<TweenHandle> tweens = new List<TweenHandle>();

        /// <summary>
        /// A CSS animation with fill-mode both: apply(0) at once, apply(eased t) from delay to
        /// delay + dur, then done. Stops if the owner leaves the panel.
        /// </summary>
        public static TweenHandle Tween(VisualElement owner, double durMs, double delayMs, Func<double, double> ease, Action<double> apply, Action done = null)
        {
            var h = new TweenHandle { owner = owner, start = Now, dur = Math.Max(1, durMs), delay = delayMs, ease = ease ?? Linear, apply = apply, done = done };
            try { apply?.Invoke(h.ease(0)); } catch (Exception e) { Debug.LogException(e); }
            tweens.Add(h);
            return h;
        }

        /// <summary>An infinite animation: apply(phase 0..1) every frame after the delay (fill-mode none before it).</summary>
        public static TweenHandle Loop(VisualElement owner, double periodMs, double delayMs, Action<double> apply)
        {
            var h = new TweenHandle { owner = owner, start = Now, period = Math.Max(1, periodMs), delay = delayMs, apply = apply, loop = true };
            tweens.Add(h);
            return h;
        }

        /// <summary>A timeout bound to an element (window.setTimeout, cleared when the element goes).</summary>
        public static TweenHandle After(VisualElement owner, double ms, Action fn)
        {
            return Tween(owner, 1, ms, Linear, null, fn);
        }

        /// <summary>Run every tween; the host calls it once a frame, before the panel updates.</summary>
        public static void Tick()
        {
            double now = Now;
            for (int i = 0; i < tweens.Count; i++)
            {
                var h = tweens[i];
                if (h.stopped) continue;
                if (h.owner != null)
                {
                    if (h.owner.panel != null) h.attached = true;
                    else if (h.attached) { h.stopped = true; continue; }
                }
                double el = now - h.start - h.delay;
                try
                {
                    if (h.loop)
                    {
                        if (el < 0) continue;
                        h.apply?.Invoke((el % h.period) / h.period);
                        continue;
                    }
                    if (el < 0) continue;
                    double t = Math.Min(1, el / h.dur);
                    h.apply?.Invoke(h.ease(t));
                    if (t >= 1)
                    {
                        h.stopped = true;
                        h.done?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    h.stopped = true;
                    Debug.LogException(e);
                }
            }
            tweens.RemoveAll(x => x.stopped);
        }

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2)</summary>
        public static Func<double, double> Bezier(double x1, double y1, double x2, double y2)
        {
            double cx = 3 * x1, bx = 3 * (x2 - x1) - cx, ax = 1 - cx - bx;
            double cy = 3 * y1, by = 3 * (y2 - y1) - cy, ay = 1 - cy - by;
            double X(double t) { return ((ax * t + bx) * t + cx) * t; }
            double Y(double t) { return ((ay * t + by) * t + cy) * t; }
            double dX(double t) { return (3 * ax * t + 2 * bx) * t + cx; }
            return x =>
            {
                if (x <= 0) return 0;
                if (x >= 1) return 1;
                double t = x;
                for (int i = 0; i < 8; i++)
                {
                    double e = X(t) - x;
                    if (Math.Abs(e) < 1e-6) return Y(t);
                    double d = dX(t);
                    if (Math.Abs(d) < 1e-6) break;
                    t -= e / d;
                }
                double lo = 0, hi = 1;
                t = x;
                for (int i = 0; i < 30; i++)
                {
                    double v = X(t);
                    if (Math.Abs(v - x) < 1e-6) break;
                    if (v < x) lo = t; else hi = t;
                    t = (lo + hi) / 2;
                }
                return Y(t);
            };
        }

        public static readonly Func<double, double> Linear = t => t;
        public static readonly Func<double, double> Ease = Bezier(0.25, 0.1, 0.25, 1);
        public static readonly Func<double, double> EaseIn = Bezier(0.42, 0, 1, 1);
        public static readonly Func<double, double> EaseOut = Bezier(0, 0, 0.58, 1);
        public static readonly Func<double, double> EaseInOut = Bezier(0.42, 0, 0.58, 1);
        /// <summary>the game's cubic-bezier(0.2, 0.7, 0.2, 1)</summary>
        public static readonly Func<double, double> Soft = Bezier(0.2, 0.7, 0.2, 1);

        /// <summary>Piecewise-linear keyframes (at in 0..1).</summary>
        public static double Keys(double t, params (double at, double v)[] k)
        {
            return Seg(t, Linear, k);
        }

        /// <summary>
        /// Keyframes the CSS way: the timing function applies inside each segment. Pass the
        /// LINEAR progress (use Tween with Linear and the real easing here).
        /// </summary>
        public static double Seg(double t, Func<double, double> ease, params (double at, double v)[] k)
        {
            if (k.Length == 0) return 0;
            if (t <= k[0].at) return k[0].v;
            for (int i = 1; i < k.Length; i++)
            {
                if (t <= k[i].at)
                {
                    double span = k[i].at - k[i - 1].at;
                    double u = span > 0 ? (t - k[i - 1].at) / span : 1;
                    return k[i - 1].v + (k[i].v - k[i - 1].v) * (ease ?? Linear)(u);
                }
            }
            return k[k.Length - 1].v;
        }

        public static double Lerp(double a, double b, double t) { return a + (b - a) * t; }

        // ------------------------------------------------------------ the panel

        public static UIDocument document;
        public static PanelSettings panelSettings;
        /// <summary>the panel's root; the game screen fills it</summary>
        public static VisualElement root;
        /// <summary>the backend's texture of a surface (UnityBackend.TextureOf)</summary>
        public static Func<Surface, RenderTexture> textureOf;
        /// <summary>true once the advanced text generator could be switched on</summary>
        public static bool advancedText;

        static Material unpremul;

        /// <summary>Create the panel. The host calls it once at startup.</summary>
        public static void Init(GameObject host)
        {
            var asset = Resources.Load<PanelSettings>("UI/SisifosPanel");
            // a copy, so play mode in the editor never writes into the asset
            panelSettings = asset != null ? UnityEngine.Object.Instantiate(asset) : ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "SisifosPanel";
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = dpr;
            panelSettings.sortingOrder = 0;
            panelSettings.clearColor = false;
            // every style is set from code; an empty theme keeps the "no theme" warning away
            if (panelSettings.themeStyleSheet == null) panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            var ts = panelSettings.textSettings != null ? panelSettings.textSettings : ScriptableObject.CreateInstance<PanelTextSettings>();
            LoadScriptFallbacks();
            var list = ts.fallbackFontAssets ?? new List<FontAsset>();
            foreach (var a in scriptFallbacks) if (!list.Contains(a)) list.Add(a);
            ts.fallbackFontAssets = list;
            panelSettings.textSettings = ts;

            advancedText = EnableAdvancedText(panelSettings);

            var go = new GameObject("SisifosUI");
            go.transform.SetParent(host.transform, false);
            go.SetActive(false);
            document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            go.SetActive(true);
            root = document.rootVisualElement;
            root.style.unityTextGenerator = advancedText ? TextGeneratorType.Advanced : TextGeneratorType.Standard;
            SetFont(root, SANS);
            root.style.fontSize = rootFontSize;
            root.style.color = Foreground;
            root.pickingMode = PickingMode.Ignore;

            var sh = Resources.Load<Shader>("Shaders/SisifosUnpremul");
            if (sh == null) sh = Shader.Find("Hidden/SisifosUnpremul");
            if (sh != null) unpremul = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        /// <summary>
        /// Unity 6.0 gates the advanced text generator behind a delegate only the editor sets
        /// (see the file header). Set it, and make sure the panel has the ICU data it needs.
        /// </summary>
        static bool EnableAdvancedText(PanelSettings ps)
        {
            try
            {
                var asm = typeof(VisualElement).Assembly;
                var tu = asm.GetType("UnityEngine.UIElements.TextUtilities");
                var gate = tu?.GetField("IsAdvancedTextEnabled", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (gate == null) return false; // a later Unity: the generator is not gated any more
                // the ICU data: the PanelSettings asset carries it; else any loaded copy
                var icuField = typeof(PanelSettings).GetField("m_ICUDataAsset", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var icu = icuField?.GetValue(ps) as UnityEngine.TextAsset;
                if (icu == null)
                {
                    icu = Resources.Load<UnityEngine.TextAsset>("UI/icudt73l");
                    if (icu != null) icuField?.SetValue(ps, icu);
                }
#if !UNITY_EDITOR
                if (icu == null)
                {
                    Debug.LogWarning("Sisifos: no ICU data in the build (run the editor once so Editor/SisifosUiSetup creates Resources/UI/SisifosPanel.asset); Arabic, Hindi and Bengali will not be shaped.");
                    return false;
                }
#endif
                var prev = gate.GetValue(null) as Func<bool>;
                if (prev == null || !prev()) gate.SetValue(null, new Func<bool>(() => true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        /// <summary>The screen changed (size, dpr or safe area): update the panel and tell the views.</summary>
        public static void SetMetrics(float devicePixelRatio, float cssW, float cssH, float sl, float st, float sr, float sb)
        {
            bool diff = Math.Abs(dpr - devicePixelRatio) > 1e-3 || Math.Abs(viewW - cssW) > 0.5 || Math.Abs(viewH - cssH) > 0.5
                || Math.Abs(safeLeft - sl) > 0.5 || Math.Abs(safeTop - st) > 0.5 || Math.Abs(safeRight - sr) > 0.5 || Math.Abs(safeBottom - sb) > 0.5;
            dpr = devicePixelRatio;
            viewW = cssW; viewH = cssH;
            safeLeft = sl; safeTop = st; safeRight = sr; safeBottom = sb;
            if (panelSettings != null && Math.Abs(panelSettings.scale - dpr) > 1e-3) panelSettings.scale = dpr;
            if (diff)
            {
                foreach (var cb in canvases) cb.Redraw();
                NotifyChanged();
            }
        }

        public static void SetRootFontSize(float px)
        {
            if (Math.Abs(rootFontSize - px) < 1e-3) return;
            rootFontSize = px;
            if (root != null) root.style.fontSize = px;
            NotifyChanged();
        }

        // ------------------------------------------------------------ canvas-painted boxes

        internal static readonly List<CanvasBox> canvases = new List<CanvasBox>();

        /// <summary>Paint the boxes that changed; the host calls it before Gfx.Flush().</summary>
        public static void DrawCanvases()
        {
            for (int i = 0; i < canvases.Count; i++)
            {
                try { canvases[i].Paint(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>Hand the painted boxes to the panel; the host calls it after Gfx.Flush().</summary>
        public static void PresentCanvases()
        {
            for (int i = 0; i < canvases.Count; i++)
            {
                try { canvases[i].Present(unpremul); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        // ------------------------------------------------------------ canvas helpers

        static readonly CultureInfo inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Add an SVG path's "d" to the context's current path (M L H V C S Q T A Z, absolute and
        /// relative, implicit repeats).
        /// </summary>
        public static void Svg(Ctx c, string d)
        {
            int i = 0, n = d.Length;
            char cmd = ' ';
            double x = 0, y = 0, sx = 0, sy = 0, lcx = 0, lcy = 0, lqx = 0, lqy = 0;
            char prev = ' ';
            bool Num(out double v)
            {
                v = 0;
                while (i < n && (d[i] == ' ' || d[i] == ',' || d[i] == '\n' || d[i] == '\t' || d[i] == '\r')) i++;
                if (i >= n) return false;
                int s = i;
                if (d[i] == '-' || d[i] == '+') i++;
                bool dot = false, any = false;
                while (i < n)
                {
                    char ch = d[i];
                    if (ch >= '0' && ch <= '9') { i++; any = true; continue; }
                    if (ch == '.' && !dot) { dot = true; i++; continue; }
                    if ((ch == 'e' || ch == 'E') && any)
                    {
                        i++;
                        if (i < n && (d[i] == '-' || d[i] == '+')) i++;
                        continue;
                    }
                    break;
                }
                if (!any) { i = s; return false; }
                v = double.Parse(d.Substring(s, i - s), NumberStyles.Float, inv);
                return true;
            }
            bool Flag(out bool f)
            {
                f = false;
                while (i < n && (d[i] == ' ' || d[i] == ',')) i++;
                if (i >= n) return false;
                if (d[i] == '0' || d[i] == '1') { f = d[i] == '1'; i++; return true; }
                return false;
            }
            while (i < n)
            {
                while (i < n && (d[i] == ' ' || d[i] == ',' || d[i] == '\n' || d[i] == '\t' || d[i] == '\r')) i++;
                if (i >= n) break;
                char ch = d[i];
                if (char.IsLetter(ch) && ch != 'e' && ch != 'E') { cmd = ch; i++; }
                else if (cmd == ' ') break;
                bool rel = char.IsLower(cmd);
                double ox = rel ? x : 0, oy = rel ? y : 0;
                switch (char.ToUpperInvariant(cmd))
                {
                    case 'Z':
                        c.ClosePath();
                        x = sx; y = sy;
                        prev = 'Z';
                        continue;
                    case 'M':
                        {
                            if (!Num(out double a) || !Num(out double b)) return;
                            x = ox + a; y = oy + b; sx = x; sy = y;
                            c.MoveTo(x, y);
                            cmd = rel ? 'l' : 'L';
                            prev = 'M';
                            break;
                        }
                    case 'L':
                        {
                            if (!Num(out double a) || !Num(out double b)) return;
                            x = ox + a; y = oy + b;
                            c.LineTo(x, y);
                            prev = 'L';
                            break;
                        }
                    case 'H':
                        {
                            if (!Num(out double a)) return;
                            x = (rel ? x : 0) + a;
                            c.LineTo(x, y);
                            prev = 'H';
                            break;
                        }
                    case 'V':
                        {
                            if (!Num(out double a)) return;
                            y = (rel ? y : 0) + a;
                            c.LineTo(x, y);
                            prev = 'V';
                            break;
                        }
                    case 'C':
                        {
                            if (!Num(out double a1) || !Num(out double b1) || !Num(out double a2) || !Num(out double b2) || !Num(out double a) || !Num(out double b)) return;
                            double x1 = ox + a1, y1 = oy + b1, x2 = ox + a2, y2 = oy + b2;
                            x = ox + a; y = oy + b;
                            c.BezierCurveTo(x1, y1, x2, y2, x, y);
                            lcx = x2; lcy = y2;
                            prev = 'C';
                            break;
                        }
                    case 'S':
                        {
                            if (!Num(out double a2) || !Num(out double b2) || !Num(out double a) || !Num(out double b)) return;
                            double x1 = prev == 'C' || prev == 'S' ? 2 * x - lcx : x;
                            double y1 = prev == 'C' || prev == 'S' ? 2 * y - lcy : y;
                            double x2 = ox + a2, y2 = oy + b2;
                            x = ox + a; y = oy + b;
                            c.BezierCurveTo(x1, y1, x2, y2, x, y);
                            lcx = x2; lcy = y2;
                            prev = 'S';
                            break;
                        }
                    case 'Q':
                        {
                            if (!Num(out double a1) || !Num(out double b1) || !Num(out double a) || !Num(out double b)) return;
                            lqx = ox + a1; lqy = oy + b1;
                            x = ox + a; y = oy + b;
                            c.QuadraticCurveTo(lqx, lqy, x, y);
                            prev = 'Q';
                            break;
                        }
                    case 'T':
                        {
                            if (!Num(out double a) || !Num(out double b)) return;
                            if (prev == 'Q' || prev == 'T') { lqx = 2 * x - lqx; lqy = 2 * y - lqy; }
                            else { lqx = x; lqy = y; }
                            x = ox + a; y = oy + b;
                            c.QuadraticCurveTo(lqx, lqy, x, y);
                            prev = 'T';
                            break;
                        }
                    case 'A':
                        {
                            if (!Num(out double rx) || !Num(out double ry) || !Num(out double rot) || !Flag(out bool large) || !Flag(out bool sweep) || !Num(out double a) || !Num(out double b)) return;
                            double x2 = ox + a, y2 = oy + b;
                            Arc(c, x, y, rx, ry, rot, large, sweep, x2, y2);
                            x = x2; y = y2;
                            prev = 'A';
                            break;
                        }
                    default:
                        return;
                }
            }
        }

        /// <summary>SVG elliptical arc (endpoint form) → canvas ellipse (W3C SVG implementation notes F.6.5).</summary>
        static void Arc(Ctx c, double x1, double y1, double rx, double ry, double rotDeg, bool large, bool sweep, double x2, double y2)
        {
            if (rx == 0 || ry == 0) { c.LineTo(x2, y2); return; }
            rx = Math.Abs(rx); ry = Math.Abs(ry);
            double phi = rotDeg * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (x1 - x2) / 2, dy = (y1 - y2) / 2;
            double x1p = cos * dx + sin * dy, y1p = -sin * dx + cos * dy;
            double lam = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
            if (lam > 1) { double s = Math.Sqrt(lam); rx *= s; ry *= s; }
            double num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
            double den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
            double co = den == 0 ? 0 : Math.Sqrt(Math.Max(0, num / den));
            if (large == sweep) co = -co;
            double cxp = co * rx * y1p / ry, cyp = -co * ry * x1p / rx;
            double cx = cos * cxp - sin * cyp + (x1 + x2) / 2, cy = sin * cxp + cos * cyp + (y1 + y2) / 2;
            double Ang(double ux, double uy, double vx, double vy)
            {
                double dot = ux * vx + uy * vy, len = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
                double a = Math.Acos(Math.Max(-1, Math.Min(1, dot / len)));
                return ux * vy - uy * vx < 0 ? -a : a;
            }
            double th1 = Ang(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
            double dth = Ang((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
            if (!sweep && dth > 0) dth -= 2 * Math.PI;
            else if (sweep && dth < 0) dth += 2 * Math.PI;
            c.Ellipse(cx, cy, rx, ry, phi, th1, th1 + dth, !sweep);
        }

        /// <summary>The start and end of a CSS linear-gradient(angle) over a w × h box.</summary>
        public static void CssLinear(double angleDeg, double w, double h, out double x0, out double y0, out double x1, out double y1)
        {
            double a = angleDeg * Math.PI / 180;
            double dx = Math.Sin(a), dy = -Math.Cos(a);
            double len = Math.Abs(w * dx) + Math.Abs(h * dy);
            x0 = w / 2 - dx * len / 2; y0 = h / 2 - dy * len / 2;
            x1 = w / 2 + dx * len / 2; y1 = h / 2 + dy * len / 2;
        }

        /// <summary>
        /// A CSS radial-gradient sized farthest-corner ("circle"/"ellipse at px py"): fills the
        /// rect with stops given as (offset 0..1 of the radius, colour).
        /// </summary>
        public static void CssRadial(Ctx c, double w, double h, bool circle, double px, double py, params (double at, Col col)[] stops)
        {
            double fx = Math.Max(px, w - px), fy = Math.Max(py, h - py);
            c.Save();
            double r, s = 1;
            if (circle) r = Math.Sqrt(fx * fx + fy * fy);
            else
            {
                // farthest-corner ellipse: the farthest sides' aspect, scaled by √2 to pass through the corner
                double rx = fx * Math.Sqrt(2), ry = fy * Math.Sqrt(2);
                s = ry / Math.Max(1e-6, rx);
                c.Translate(px, py);
                c.Scale(1, s);
                c.Translate(-px, -py);
                r = rx;
            }
            var g = c.CreateRadialGradient(px, py, 0, px, py, Math.Max(1e-6, r));
            foreach (var st in stops) g.AddColorStop(Math.Max(0, Math.Min(1, st.at)), st.col);
            c.FillStyle = g;
            // the box, in the (possibly squashed) gradient space
            s = Math.Max(1e-6, s);
            c.FillRect(0, py - py / s, w, h / s);
            c.Restore();
        }

        /// <summary>A rounded rectangle path.</summary>
        public static void RoundRect(Ctx c, double x, double y, double w, double h, double r)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2));
            c.BeginPath();
            if (r <= 0) { c.Rect(x, y, w, h); return; }
            c.MoveTo(x + r, y);
            c.LineTo(x + w - r, y);
            c.ArcTo(x + w, y, x + w, y + r, r);
            c.LineTo(x + w, y + h - r);
            c.ArcTo(x + w, y + h, x + w - r, y + h, r);
            c.LineTo(x + r, y + h);
            c.ArcTo(x, y + h, x, y + h - r, r);
            c.LineTo(x, y + r);
            c.ArcTo(x, y, x + r, y, r);
            c.ClosePath();
        }

        /// <summary>
        /// CSS box-shadow (outer): offset, blur, spread, colour, around a box of radius r. The shape
        /// is filled with its shadow, then cut out again (a box-shadow never shows under its box).
        /// Call it first, before painting anything else of the box.
        /// </summary>
        public static void BoxShadow(Ctx c, double w, double h, double r, double ox, double oy, double blur, double spread, Col color)
        {
            var m = c.GetTransform();
            double k = Math.Sqrt(Math.Abs(m.a * m.d - m.b * m.c));
            c.Save();
            // canvas multiplies the shadow by the shape's alpha: an opaque shadow colour under a
            // fill of the real colour gives a shadow of the real strength
            c.ShadowColor = Col.Rgba(color.r * 255, color.g * 255, color.b * 255, 1);
            c.ShadowBlur = blur * k;
            c.ShadowOffsetX = ox * k;
            c.ShadowOffsetY = oy * k;
            c.FillStyle = color;
            // the shadow of a spread box is the box grown (or shrunk) by the spread
            RoundRect(c, -spread + ox * 0, -spread, w + spread * 2, h + spread * 2, Math.Max(0, r + spread));
            c.Fill();
            c.Restore();
            c.Save();
            c.GlobalCompositeOperation = Composite.DestinationOut;
            c.FillStyle = "#000";
            RoundRect(c, 0, 0, w, h, r);
            c.Fill();
            c.Restore();
        }

        /// <summary>CSS box-shadow inset 0 0 0 Npx colour (an inner ring).</summary>
        public static void InsetRing(Ctx c, double w, double h, double r, double width, Col color)
        {
            c.Save();
            c.StrokeStyle = color;
            c.LineWidth = width;
            RoundRect(c, width / 2, width / 2, w - width, h - width, Math.Max(0, r - width / 2));
            c.Stroke();
            c.Restore();
        }

        /// <summary>GameExtras' PAPER background: #ecdfc2 under three gradients.</summary>
        public static void PaintPaper(Ctx c, double w, double h)
        {
            c.Save();
            c.FillStyle = "#ecdfc2";
            c.FillRect(0, 0, w, h);
            // linear-gradient(170deg, #f3e9d1 0%, #e9dbb9 55%, #e0cda4 100%) (bottom layer)
            CssLinear(170, w, h, out double x0, out double y0, out double x1, out double y1);
            var lg = c.CreateLinearGradient(x0, y0, x1, y1);
            lg.AddColorStop(0, "#f3e9d1");
            lg.AddColorStop(0.55, "#e9dbb9");
            lg.AddColorStop(1, "#e0cda4");
            c.FillStyle = lg;
            c.FillRect(0, 0, w, h);
            // radial-gradient(ellipse at center, rgba(0,0,0,0) 58%, rgba(120,78,30,0.28) 100%)
            CssRadial(c, w, h, false, w / 2, h / 2, (0, Col.Rgba(120, 78, 30, 0)), (0.58, Col.Rgba(120, 78, 30, 0)), (1, Col.Rgba(120, 78, 30, 0.28)));
            // radial-gradient(circle at 72% 30%, rgba(255,252,240,0.5) 0, transparent 45%)
            CssRadial(c, w, h, true, w * 0.72, h * 0.3, (0, Col.Rgba(255, 252, 240, 0.5)), (0.45, Col.Rgba(255, 252, 240, 0)), (1, Col.Rgba(255, 252, 240, 0)));
            c.Restore();
        }
    }

    /// <summary>
    /// A box whose background is painted with the canvas API (Sisifos.Paint.Ctx) into its own
    /// surface: gradients, shadows and icons. The painting can bleed outside the box (shadows).
    /// The draw callback gets CSS px with (0,0) at the box's top-left; the context is already
    /// scaled to device pixels.
    /// </summary>
    public class CanvasBox : VisualElement
    {
        Action<Ctx, double, double> draw;
        readonly VisualElement layer;
        Surface surface;
        RenderTexture shown;
        float bleed;
        bool dirty = true, painted;
        int drawnW, drawnH;
        float drawnDpr;

        public CanvasBox(Action<Ctx, double, double> draw, float bleed = 0)
        {
            this.draw = draw;
            this.bleed = bleed;
            layer = new VisualElement { name = "paint", pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = -bleed;
            layer.style.top = -bleed;
            layer.style.right = -bleed;
            layer.style.bottom = -bleed;
            hierarchy.Add(layer);
            RegisterCallback<AttachToPanelEvent>(e => { if (!UiKit.canvases.Contains(this)) UiKit.canvases.Add(this); dirty = true; });
            RegisterCallback<DetachFromPanelEvent>(e => { UiKit.canvases.Remove(this); Free(); });
            RegisterCallback<GeometryChangedEvent>(e => dirty = true);
        }

        /// <summary>replace the drawing</summary>
        public void SetDraw(Action<Ctx, double, double> d) { draw = d; dirty = true; }

        public float Bleed
        {
            get { return bleed; }
            set
            {
                bleed = value;
                layer.style.left = -value; layer.style.top = -value; layer.style.right = -value; layer.style.bottom = -value;
                dirty = true;
            }
        }

        /// <summary>paint again on the next frame</summary>
        public void Redraw() { dirty = true; }

        internal void Paint()
        {
            if (!dirty || panel == null || draw == null) return;
            var r = layout;
            float w = r.width, h = r.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;
            float k = UiKit.dpr;
            int pw = Math.Max(1, (int)Math.Ceiling((w + bleed * 2) * k));
            int ph = Math.Max(1, (int)Math.Ceiling((h + bleed * 2) * k));
            if (pw > 4096 || ph > 4096)
            {
                double s = 4096.0 / Math.Max(pw, ph);
                pw = (int)(pw * s); ph = (int)(ph * s); k = (float)(k * s);
            }
            // isScreen: the backend resolves its multisampling at the end of every frame, so it can be shown
            if (surface == null) surface = new Surface(pw, ph, true, "ui") { isScreen = true };
            else if (surface.width != pw || surface.height != ph) surface.SetSize(pw, ph);
            var c = surface.GetContext();
            c.SetTransform(1, 0, 0, 1, 0, 0);
            c.ClearRect(0, 0, pw, ph);
            c.SetTransform(k, 0, 0, k, bleed * k, bleed * k);
            draw(c, w, h);
            c.SetTransform(1, 0, 0, 1, 0, 0);
            dirty = false;
            painted = true;
            drawnW = pw; drawnH = ph; drawnDpr = k;
        }

        internal void Present(Material unpremul)
        {
            if (!painted || surface == null) return;
            painted = false;
            var src = UiKit.textureOf?.Invoke(surface);
            if (src == null) return;
            if (shown == null || shown.width != src.width || shown.height != src.height)
            {
                if (shown != null) { shown.Release(); UnityEngine.Object.Destroy(shown); }
                shown = new RenderTexture(src.width, src.height, 0, RenderTextureFormat.ARGB32) { name = "Sisifos.UI", hideFlags = HideFlags.HideAndDontSave };
                shown.Create();
                layer.style.backgroundImage = Background.FromRenderTexture(shown);
            }
            // the paint is premultiplied; UI Toolkit blends straight alpha
            if (unpremul != null) Graphics.Blit(src, shown, unpremul);
            else Graphics.Blit(src, shown);
            layer.MarkDirtyRepaint();
        }

        void Free()
        {
            surface?.Release();
            surface = null;
            if (shown != null) { shown.Release(); UnityEngine.Object.Destroy(shown); shown = null; }
            layer.style.backgroundImage = StyleKeyword.None;
        }
    }

    /// <summary>An inline SVG icon: viewBox units, drawn in its colour (currentColor).</summary>
    public sealed class SvgIcon : CanvasBox
    {
        Color _color = Color.white;
        public Color color
        {
            get { return _color; }
            set { if (value != _color) { _color = value; Redraw(); } }
        }

        /// <param name="draw">draws in viewBox units, (ctx, currentColor)</param>
        public SvgIcon(float w, float h, float viewBox, Action<Ctx, Col> draw) : base(null)
        {
            pickingMode = PickingMode.Ignore;
            style.width = w;
            style.height = h;
            style.flexShrink = 0;
            SetDraw((c, bw, bh) =>
            {
                c.Scale(bw / viewBox, bh / viewBox);
                draw(c, Col.Rgba(_color.r * 255, _color.g * 255, _color.b * 255, _color.a));
            });
        }
    }
}

namespace Sisifos.App.UI
{
    /// <summary>
    /// overflow-y: auto, without UI Toolkit's ScrollView (whose layout lives in the theme's USS):
    /// a clipping viewport and a content column moved by the scroll offset. Touch drags with
    /// momentum (the phone's fling), mouse wheel, no visible bar (overlay scrollbars on phones).
    /// </summary>
    public sealed class ScrollBox : VisualElement
    {
        readonly VisualElement content;
        float offset, velocity;
        int dragId = -1;
        float lastY, startY, startOffset;
        double lastT;
        bool dragged;

        public ScrollBox()
        {
            style.overflow = Overflow.Hidden;
            style.flexGrow = 1;
            content = new VisualElement { name = "content" };
            content.style.position = Position.Absolute;
            content.style.left = 0;
            content.style.right = 0;
            content.style.top = 0;
            content.usageHints = UsageHints.DynamicTransform;
            hierarchy.Add(content);
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.pointerType == "mouse") return;
                dragId = e.pointerId;
                startY = lastY = e.position.y;
                startOffset = offset;
                lastT = UiKit.Now;
                velocity = 0;
                dragged = false;
            }, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (e.pointerId != dragId) return;
                float y = e.position.y;
                if (!dragged && Math.Abs(y - startY) > 6)
                {
                    dragged = true;
                    this.CapturePointer(e.pointerId);
                }
                if (!dragged) return;
                double now = UiKit.Now;
                float dy = y - lastY;
                double dt = Math.Max(1, now - lastT);
                velocity = (float)(0.8 * (-dy / dt) + 0.2 * velocity);
                lastY = y;
                lastT = now;
                Set(startOffset - (y - startY));
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            EventCallback<PointerUpEvent> up = e =>
            {
                if (e.pointerId != dragId) return;
                dragId = -1;
                if (dragged)
                {
                    this.ReleasePointer(e.pointerId);
                    e.StopPropagation();
                    if (UiKit.Now - lastT > 80) velocity = 0;
                }
            };
            RegisterCallback(up, TrickleDown.TrickleDown);
            RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == dragId) { dragId = -1; velocity = 0; } });
            // a drag that moved is not a click on what was under the finger
            RegisterCallback<ClickEvent>(e => { if (dragged) { dragged = false; e.StopPropagation(); } }, TrickleDown.TrickleDown);
            RegisterCallback<WheelEvent>(e => { Set(offset + e.delta.y * 18); e.StopPropagation(); });
            RegisterCallback<GeometryChangedEvent>(e => Set(offset));
            content.RegisterCallback<GeometryChangedEvent>(e => Set(offset));
            schedule.Execute(Fling).Every(16);
        }

        public override VisualElement contentContainer { get { return content; } }

        public float scrollOffset { get { return offset; } set { Set(value); } }

        float Max { get { float m = content.layout.height - layout.height; return float.IsNaN(m) ? 0 : Math.Max(0, m); } }

        void Set(float v)
        {
            offset = Math.Max(0, Math.Min(Max, v));
            content.style.translate = new Translate(0, -offset);
        }

        void Fling()
        {
            if (dragId >= 0 || Math.Abs(velocity) < 0.02f) return;
            Set(offset + velocity * 16);
            // the platform's fling decelerates roughly like this
            velocity *= 0.95f;
            if (offset <= 0 || offset >= Max) velocity = 0;
        }
    }
}
