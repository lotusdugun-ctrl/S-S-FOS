using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Sisifos.Paint;
using UnityEngine;

namespace Sisifos.App
{
    /// <summary>
    /// Text for the canvas emulation. A run (fillText/strokeText) becomes textured quads in the
    /// frame's mesh, painted in the "glyph" kind (texture alpha × run colour) with the run's
    /// composite mode and clip, so it sits in draw order like any other paint.
    ///
    /// On Android and iOS a whole run is rasterised by the OS text stack (android.graphics /
    /// CoreText) into an alpha bitmap and cached: that gives the same shaping, bidi and font
    /// fallback a browser gets (Arabic, Devanagari, Bengali, CJK), and the web fonts the game
    /// used (Cinzel, Marcellus, Cormorant Garamond) ship as TTFs in StreamingAssets/Fonts.
    /// In the editor and on desktop, a Unity dynamic OS font is drawn glyph by glyph
    /// (no complex-script shaping there; it is only for development).
    /// MeasureText goes through the same engine as drawing, so layout matches what is drawn.
    /// </summary>
    public abstract class TextEngine : IDisposable
    {
        public static TextEngine Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return new AndroidText(); }
            catch (Exception e) { Debug.LogException(e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { return new IosText(); }
            catch (Exception e) { Debug.LogException(e); }
#endif
            return new UnityFontText();
        }

        /// <summary>Advance width of a run in CSS pixels at the font's size (measureText().width).</summary>
        public abstract double Measure(string text, string font);

        /// <summary>Called before any Emit of a frame.</summary>
        public virtual void BeginFrame(Frame f, int frame) { }

        /// <summary>Append the run's quads to the backend's mesh; returns the texture they sample, or null.</summary>
        public abstract Texture Emit(TextRun run, UnityBackend be);

        public virtual void EndFrame(int frame) { }

        public virtual void Dispose() { }

        static readonly Dictionary<string, FontSpec> specs = new Dictionary<string, FontSpec>();

        /// <summary>FontSpec.Parse, memoised (it splits strings).</summary>
        protected static FontSpec Spec(string font)
        {
            lock (specs)
            {
                if (specs.TryGetValue(font ?? "", out var f)) return f;
                f = FontSpec.Parse(font);
                if (specs.Count > 512) specs.Clear();
                specs[font ?? ""] = f;
                return f;
            }
        }

        protected static double DeviceScale(in Mat2D m)
        {
            double s = Math.Sqrt(Math.Abs(m.a * m.d - m.b * m.c));
            return s > 1e-6 ? s : 1;
        }

        /// <summary>Raster size for a font size under a transform: whole pixels, so the cache is not churned by tiny scale changes.</summary>
        protected static int RasterPx(double size, double scale)
        {
            int px = (int)Math.Ceiling(size * scale - 0.01);
            return Math.Max(1, Math.Min(px, 512));
        }

        protected struct MeasureKey : IEquatable<MeasureKey>
        {
            public string text, font;
            public bool Equals(MeasureKey o) { return text == o.text && font == o.font; }
            public override bool Equals(object o) { return o is MeasureKey k && Equals(k); }
            public override int GetHashCode() { unchecked { return text.GetHashCode() * 397 ^ (font ?? "").GetHashCode(); } }
        }
    }

    /// <summary>Text drawn from whole-run alpha bitmaps made by a platform rasteriser.</summary>
    public abstract class BitmapTextEngine : TextEngine
    {
        protected struct Raw
        {
            /// <summary>Alpha, top row first, <see cref="stride"/> bytes per row.</summary>
            public byte[] alpha;
            public int w, h, stride;
            /// <summary>The pen origin (left end of the baseline) in bitmap pixels, y down.</summary>
            public float originX, originY;
        }

        /// <summary>Advance width at a size (CSS px).</summary>
        protected abstract double MeasureFace(string text, FontFace face, double size);
        /// <summary>Ascent and descent (positive) at a size.</summary>
        protected abstract void MetricsFace(FontFace face, double size, out double ascent, out double descent);
        /// <summary>The run in white on transparent; strokePx &lt; 0 fills.</summary>
        protected abstract bool RasterFace(string text, FontFace face, float px, float strokePx, out Raw raw);

        struct Key : IEquatable<Key>
        {
            public string text;
            public FontFace face;
            public int px, stroke;
            public bool Equals(Key o) { return px == o.px && stroke == o.stroke && text == o.text && face.Equals(o.face); }
            public override bool Equals(object o) { return o is Key k && Equals(k); }
            public override int GetHashCode() { unchecked { return ((text.GetHashCode() * 397) ^ face.GetHashCode()) * 31 + px * 7 + stroke; } }
        }

        sealed class Bmp
        {
            public Texture2D tex;
            public int w, h, texW;
            public float ox, oy;
            public int last;
        }

        readonly Dictionary<Key, Bmp> bitmaps = new Dictionary<Key, Bmp>();
        readonly Dictionary<MeasureKey, double> widths = new Dictionary<MeasureKey, double>();
        readonly Dictionary<FontFace, Vector2> metrics = new Dictionary<FontFace, Vector2>();
        readonly List<Key> evict = new List<Key>();
        protected readonly object gate = new object();

        public override double Measure(string text, string font)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var k = new MeasureKey { text = text, font = font };
            lock (gate)
            {
                if (widths.TryGetValue(k, out var w)) return w;
                var fs = Spec(font);
                var face = FontResolver.Resolve(font);
                try { w = MeasureFace(text, face, fs.size); }
                catch (Exception e) { Debug.LogException(e); w = fs.size * 0.5 * text.Length; }
                if (widths.Count > 8192) widths.Clear();
                widths[k] = w;
                return w;
            }
        }

        void Metrics(FontFace face, double size, out double asc, out double desc)
        {
            if (!metrics.TryGetValue(face, out var m))
            {
                // linear in size: measured once at 100px
                try { MetricsFace(face, 100, out var a, out var d); m = new Vector2((float)(a / 100), (float)(d / 100)); }
                catch (Exception e) { Debug.LogException(e); m = new Vector2(0.9f, 0.25f); }
                metrics[face] = m;
            }
            asc = m.x * size;
            desc = m.y * size;
        }

        public override Texture Emit(TextRun run, UnityBackend be)
        {
            var fs = Spec(run.font);
            if (fs.size <= 0) return null;
            var face = FontResolver.Resolve(run.font);
            double width = Measure(run.text, run.font);
            lock (gate)
            {
                Metrics(face, fs.size, out var asc, out var desc);
                if (!TextLayout.Place(run, fs.size, width, asc, desc, out var x0, out var baseY, out var sx)) return null;
                int px = RasterPx(fs.size, DeviceScale(run.m));
                // keep the bitmap under mobile texture limits (very long runs at large sizes)
                if (width * px / fs.size > 3800) px = Math.Max(1, (int)(3800 * fs.size / width));
                double k = px / fs.size; // raster pixels per CSS pixel
                float strokePx = run.stroke ? (float)Math.Max(0.25, run.lineWidth * k) : -1f;
                var key = new Key { text = run.text, face = face, px = px, stroke = run.stroke ? (int)Math.Round(strokePx * 8) : -1 };
                if (!bitmaps.TryGetValue(key, out var b))
                {
                    Raw raw;
                    bool ok;
                    try { ok = RasterFace(run.text, face, px, strokePx, out raw); }
                    catch (Exception e) { Debug.LogException(e); ok = false; raw = default(Raw); }
                    if (!ok || raw.w <= 0 || raw.h <= 0) return null;
                    var tex = new Texture2D(raw.stride, raw.h, TextureFormat.Alpha8, false, true)
                    {
                        hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                        name = "Sisifos.Text",
                    };
                    tex.LoadRawTextureData(raw.alpha);
                    tex.Apply(false, true);
                    b = new Bmp { tex = tex, w = raw.w, h = raw.h, texW = raw.stride, ox = raw.originX, oy = raw.originY };
                    bitmaps[key] = b;
                }
                b.last = be.FrameNo;
                // bitmap pixel (X, Y) → user space → device
                double l = x0 + (0 - b.ox) / k * sx, r = x0 + (b.w - b.ox) / k * sx;
                double t = baseY + (0 - b.oy) / k, bt = baseY + (b.h - b.oy) / k;
                var m = run.m;
                m.Apply(l, t, out var ax, out var ay);
                m.Apply(r, t, out var bx, out var by);
                m.Apply(r, bt, out var cx, out var cy);
                m.Apply(l, bt, out var dx, out var dy);
                be.GlyphQuad((float)ax, (float)ay, (float)bx, (float)by, (float)cx, (float)cy, (float)dx, (float)dy,
                    run.color, 0, 0, b.w / (float)b.texW, 1);
                return b.tex;
            }
        }

        public override void EndFrame(int frame)
        {
            if ((frame % 120) != 0) return;
            lock (gate)
            {
                evict.Clear();
                foreach (var kv in bitmaps) if (frame - kv.Value.last > 240) evict.Add(kv.Key);
                foreach (var k in evict)
                {
                    UnityEngine.Object.Destroy(bitmaps[k].tex);
                    bitmaps.Remove(k);
                }
            }
        }

        public override void Dispose()
        {
            lock (gate)
            {
                foreach (var b in bitmaps.Values) UnityEngine.Object.Destroy(b.tex);
                bitmaps.Clear();
            }
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>android.graphics text (Minikin: shaping, bidi, system font fallback) through JNI.</summary>
    public sealed class AndroidText : BitmapTextEngine
    {
        const int ANTI_ALIAS_FLAG = 1, SUBPIXEL_TEXT_FLAG = 128;

        readonly AndroidJavaObject paint, assets, styleFill, styleStroke, alpha8;
        readonly AndroidJavaClass typeface, bitmapClass, byteBuffer;
        readonly int sdk;
        readonly int mainThread;
        readonly Dictionary<FontFace, AndroidJavaObject> faces = new Dictionary<FontFace, AndroidJavaObject>();
        FontFace curFace;
        bool hasFace;
        float curSize = -1, curStroke = -2;

        public AndroidText()
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                assets = activity.Call<AndroidJavaObject>("getAssets");
            using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
            typeface = new AndroidJavaClass("android.graphics.Typeface");
            bitmapClass = new AndroidJavaClass("android.graphics.Bitmap");
            byteBuffer = new AndroidJavaClass("java.nio.ByteBuffer");
            using (var st = new AndroidJavaClass("android.graphics.Paint$Style"))
            {
                styleFill = st.GetStatic<AndroidJavaObject>("FILL");
                styleStroke = st.GetStatic<AndroidJavaObject>("STROKE");
            }
            using (var cfg = new AndroidJavaClass("android.graphics.Bitmap$Config")) alpha8 = cfg.GetStatic<AndroidJavaObject>("ALPHA_8");
            paint = new AndroidJavaObject("android.graphics.Paint", ANTI_ALIAS_FLAG | SUBPIXEL_TEXT_FLAG);
            paint.Call("setColor", -1); // opaque white
        }

        void Attach()
        {
            // MeasureText may come from a Work job (thread pool)
            if (Thread.CurrentThread.ManagedThreadId != mainThread) AndroidJNI.AttachCurrentThread();
        }

        AndroidJavaObject Face(FontFace f)
        {
            if (faces.TryGetValue(f, out var t)) return t;
            if (f.file != null)
            {
                // StreamingAssets live under assets/ in the APK
                t = typeface.CallStatic<AndroidJavaObject>("createFromAsset", assets, "Fonts/" + f.file);
            }
            else
            {
                string field = f.family == "sans-serif" || f.family == "system-ui" ? "SANS_SERIF"
                    : f.family == "monospace" ? "MONOSPACE" : "SERIF";
                using (var baseFace = typeface.GetStatic<AndroidJavaObject>(field))
                {
                    if (sdk >= 28) t = typeface.CallStatic<AndroidJavaObject>("create", baseFace, f.weight, f.italic);
                    else t = typeface.CallStatic<AndroidJavaObject>("create", baseFace, (f.weight >= 600 ? 1 : 0) | (f.italic ? 2 : 0));
                }
            }
            faces[f] = t;
            return t;
        }

        void Setup(FontFace f, float size, float stroke)
        {
            if (!hasFace || !curFace.Equals(f))
            {
                paint.Call<AndroidJavaObject>("setTypeface", Face(f))?.Dispose();
                curFace = f;
                hasFace = true;
            }
            if (size != curSize) { paint.Call("setTextSize", size); curSize = size; }
            if (stroke != curStroke)
            {
                if (stroke < 0) paint.Call("setStyle", styleFill);
                else
                {
                    paint.Call("setStyle", styleStroke);
                    paint.Call("setStrokeWidth", stroke);
                }
                curStroke = stroke;
            }
        }

        protected override double MeasureFace(string text, FontFace face, double size)
        {
            Attach();
            Setup(face, (float)size, -1);
            return paint.Call<float>("measureText", text);
        }

        protected override void MetricsFace(FontFace face, double size, out double ascent, out double descent)
        {
            Attach();
            Setup(face, (float)size, -1);
            using (var fm = paint.Call<AndroidJavaObject>("getFontMetrics"))
            {
                ascent = -fm.Get<float>("ascent");
                descent = fm.Get<float>("descent");
            }
        }

        protected override bool RasterFace(string text, FontFace face, float px, float strokePx, out Raw raw)
        {
            raw = default(Raw);
            Attach();
            Setup(face, px, strokePx);
            float adv = paint.Call<float>("measureText", text);
            float top, bottom;
            using (var fm = paint.Call<AndroidJavaObject>("getFontMetrics"))
            {
                top = fm.Get<float>("top");
                bottom = fm.Get<float>("bottom");
            }
            int pad = (int)Math.Ceiling(px * 0.3f + Math.Max(0f, strokePx));
            int w = (int)Math.Ceiling(adv) + 2 * pad, h = (int)Math.Ceiling(bottom - top) + 2 * pad;
            if (w <= 0 || h <= 0) return false;
            using (var bmp = bitmapClass.CallStatic<AndroidJavaObject>("createBitmap", w, h, alpha8))
            {
                using (var canvas = new AndroidJavaObject("android.graphics.Canvas", bmp))
                    canvas.Call("drawText", text, (float)pad, pad - top, paint);
                int stride = bmp.Call<int>("getRowBytes");
                using (var buf = byteBuffer.CallStatic<AndroidJavaObject>("allocate", stride * h))
                {
                    bmp.Call("copyPixelsToBuffer", buf);
                    sbyte[] data = buf.Call<sbyte[]>("array");
                    raw.alpha = new byte[data.Length];
                    Buffer.BlockCopy(data, 0, raw.alpha, 0, data.Length);
                }
                bmp.Call("recycle");
                raw.w = w; raw.h = h; raw.stride = stride;
                raw.originX = pad; raw.originY = pad - top;
            }
            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (var f in faces.Values) f?.Dispose();
            faces.Clear();
            paint.Dispose(); assets.Dispose(); styleFill.Dispose(); styleStroke.Dispose(); alpha8.Dispose();
            typeface.Dispose(); bitmapClass.Dispose(); byteBuffer.Dispose();
        }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>CoreText (shaping, bidi, cascade fallback) through Assets/Plugins/iOS/SisifosText.mm.</summary>
    public sealed class IosText : BitmapTextEngine
    {
        [DllImport("__Internal")] static extern IntPtr SisifosText_LoadFont(byte[] utf8Path, int len);
        [DllImport("__Internal")] static extern float SisifosText_Measure(byte[] utf8, int len, IntPtr font, byte[] family, int familyLen, int weight, int italic, float size);
        [DllImport("__Internal")] static extern void SisifosText_Metrics(IntPtr font, byte[] family, int familyLen, int weight, int italic, float size, out float ascent, out float descent);
        [DllImport("__Internal")] static extern IntPtr SisifosText_Raster(byte[] utf8, int len, IntPtr font, byte[] family, int familyLen, int weight, int italic, float size, float strokeWidth,
            out int w, out int h, out int stride, out float originX, out float originY);
        [DllImport("__Internal")] static extern void SisifosText_Free(IntPtr p);

        readonly Dictionary<string, IntPtr> files = new Dictionary<string, IntPtr>();
        static readonly System.Text.Encoding utf8 = new System.Text.UTF8Encoding(false);

        IntPtr Font(FontFace f)
        {
            if (f.file == null) return IntPtr.Zero;
            if (files.TryGetValue(f.file, out var p)) return p;
            var path = utf8.GetBytes(Application.streamingAssetsPath + "/Fonts/" + f.file);
            p = SisifosText_LoadFont(path, path.Length);
            files[f.file] = p;
            return p;
        }

        static byte[] Family(FontFace f)
        {
            // CSS generic families → what Safari uses on iOS
            string fam = f.family ?? "";
            switch (fam)
            {
                case "serif": fam = "Times New Roman"; break;
                case "sans-serif": case "system-ui": fam = ".AppleSystemUIFont"; break;
                case "monospace": fam = "Courier"; break;
            }
            return utf8.GetBytes(fam);
        }

        public IosText()
        {
            // installed families the CSS lists name (Georgia and Times New Roman ship with iOS)
            FontResolver.systemHas = fam => fam == "georgia" || fam == "times new roman" || fam == "times" || fam == "helvetica"
                || fam == "palatino" || fam == "baskerville" || fam == "didot" || fam == "american typewriter";
        }

        protected override double MeasureFace(string text, FontFace face, double size)
        {
            var t = utf8.GetBytes(text);
            var fam = Family(face);
            return SisifosText_Measure(t, t.Length, Font(face), fam, fam.Length, face.weight, face.italic ? 1 : 0, (float)size);
        }

        protected override void MetricsFace(FontFace face, double size, out double ascent, out double descent)
        {
            var fam = Family(face);
            SisifosText_Metrics(Font(face), fam, fam.Length, face.weight, face.italic ? 1 : 0, (float)size, out var a, out var d);
            ascent = a; descent = d;
        }

        protected override bool RasterFace(string text, FontFace face, float px, float strokePx, out Raw raw)
        {
            raw = default(Raw);
            var t = utf8.GetBytes(text);
            var fam = Family(face);
            IntPtr p = SisifosText_Raster(t, t.Length, Font(face), fam, fam.Length, face.weight, face.italic ? 1 : 0, px, strokePx,
                out int w, out int h, out int stride, out float ox, out float oy);
            if (p == IntPtr.Zero) return false;
            try
            {
                raw.alpha = new byte[stride * h];
                Marshal.Copy(p, raw.alpha, 0, raw.alpha.Length);
            }
            finally { SisifosText_Free(p); }
            raw.w = w; raw.h = h; raw.stride = stride; raw.originX = ox; raw.originY = oy;
            return true;
        }
    }
#endif

    /// <summary>
    /// Editor / desktop text: a Unity dynamic font made from OS fonts, one quad per character.
    /// PORT: no shaping or bidi here (Arabic, Indic scripts draw as unjoined code points), no
    /// stroked text (strokeText fills), and the bundled web fonts are only used if installed.
    /// Device builds use AndroidText / IosText.
    /// </summary>
    public sealed class UnityFontText : TextEngine
    {
        readonly Dictionary<FontFace, Font> fonts = new Dictionary<FontFace, Font>();
        readonly Dictionary<MeasureKey, double> widths = new Dictionary<MeasureKey, double>();
        readonly int mainThread;
        bool rebuilt;

        public UnityFontText()
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            Font.textureRebuilt += OnRebuilt;
        }

        void OnRebuilt(Font f) { rebuilt = true; }

        public override void Dispose()
        {
            Font.textureRebuilt -= OnRebuilt;
            foreach (var f in fonts.Values) UnityEngine.Object.Destroy(f);
            fonts.Clear();
        }

        static string[] Names(FontFace f)
        {
            if (f.file != null)
            {
                if (f.file.StartsWith("Cinzel")) return new[] { "Cinzel", "Trajan Pro", "Georgia", "Times New Roman", "DejaVu Serif" };
                if (f.file.StartsWith("Marcellus")) return new[] { "Marcellus", "Georgia", "Times New Roman", "DejaVu Serif" };
                return new[] { "Cormorant Garamond", "EB Garamond", "Garamond", "Georgia", "Times New Roman", "DejaVu Serif" };
            }
            switch (f.family)
            {
                case "sans-serif": case "system-ui": return new[] { "Helvetica Neue", "Segoe UI", "Arial", "DejaVu Sans", "Noto Sans" };
                case "monospace": return new[] { "Menlo", "Consolas", "Courier New", "DejaVu Sans Mono" };
                case "serif": return new[] { "Times New Roman", "Georgia", "DejaVu Serif", "Noto Serif" };
            }
            return new[] { f.family, "Georgia", "Times New Roman", "DejaVu Serif" };
        }

        Font FontFor(FontFace f)
        {
            if (fonts.TryGetValue(f, out var font)) return font;
            font = Font.CreateDynamicFontFromOSFont(Names(f), 32);
            font.hideFlags = HideFlags.HideAndDontSave;
            fonts[f] = font;
            return font;
        }

        static FontStyle Style(FontFace f)
        {
            bool bold = f.weight >= 600 && f.file == null;
            if (bold && f.italic) return FontStyle.BoldAndItalic;
            if (bold) return FontStyle.Bold;
            return f.italic && f.file == null ? FontStyle.Italic : FontStyle.Normal;
        }

        public override double Measure(string text, string font)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var fs = Spec(font);
            if (Thread.CurrentThread.ManagedThreadId != mainThread) return fs.size * 0.48 * text.Length; // Font is main-thread only
            var k = new MeasureKey { text = text, font = font };
            if (widths.TryGetValue(k, out var w)) return w;
            var face = FontResolver.Resolve(font);
            var f = FontFor(face);
            var st = Style(face);
            int ms = Math.Max(1, (int)Math.Round(fs.size));
            f.RequestCharactersInTexture(text, ms, st);
            double sum = 0;
            for (int i = 0; i < text.Length; i++)
                if (f.GetCharacterInfo(text[i], out var ci, ms, st)) sum += ci.advance;
            w = sum * fs.size / ms;
            if (widths.Count > 8192) widths.Clear();
            widths[k] = w;
            return w;
        }

        public override void BeginFrame(Frame f, int frame)
        {
            // every character of the frame must be in the font textures before any uv is read
            for (int pass = 0; pass < 4; pass++)
            {
                rebuilt = false;
                for (int i = 0; i < f.cmds.Count; i++)
                {
                    var c = f.cmds[i];
                    if (c.kind != CmdKind.Text || string.IsNullOrEmpty(c.text.text)) continue;
                    var fs = Spec(c.text.font);
                    var face = FontResolver.Resolve(c.text.font);
                    FontFor(face).RequestCharactersInTexture(c.text.text, RasterPx(fs.size, DeviceScale(c.text.m)), Style(face));
                }
                if (!rebuilt) break;
            }
        }

        public override Texture Emit(TextRun run, UnityBackend be)
        {
            var fs = Spec(run.font);
            if (fs.size <= 0) return null;
            var face = FontResolver.Resolve(run.font);
            var font = FontFor(face);
            var st = Style(face);
            double width = Measure(run.text, run.font);
            double asc = fs.size * 0.9, desc = fs.size * 0.25;
            if (font.fontSize > 0 && font.lineHeight > 0)
            {
                asc = fs.size * font.ascent / (double)font.fontSize;
                desc = fs.size * Math.Max(0, font.lineHeight - font.ascent) / (double)font.fontSize;
            }
            if (!TextLayout.Place(run, fs.size, width, asc, desc, out var x0, out var baseY, out var sx)) return null;
            int px = RasterPx(fs.size, DeviceScale(run.m));
            double k = px / fs.size;
            var m = run.m;
            double pen = 0;
            for (int i = 0; i < run.text.Length; i++)
            {
                if (!font.GetCharacterInfo(run.text[i], out var ci, px, st)) continue;
                double l = x0 + (pen + ci.minX) / k * sx, r = x0 + (pen + ci.maxX) / k * sx;
                double t = baseY - ci.maxY / k, b = baseY - ci.minY / k;
                pen += ci.advance;
                if (ci.maxX <= ci.minX || ci.maxY <= ci.minY) continue;
                m.Apply(l, t, out var ax, out var ay);
                m.Apply(r, t, out var bx, out var by);
                m.Apply(r, b, out var cx, out var cy);
                m.Apply(l, b, out var dx, out var dy);
                be.GlyphQuadUV((float)ax, (float)ay, (float)bx, (float)by, (float)cx, (float)cy, (float)dx, (float)dy, run.color,
                    ci.uvTopLeft, ci.uvTopRight, ci.uvBottomRight, ci.uvBottomLeft);
            }
            return font.material != null ? font.material.mainTexture : null;
        }
    }
}
