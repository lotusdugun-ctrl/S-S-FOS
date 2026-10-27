using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sisifos.Paint
{
    /// <summary>
    /// A 2D drawing context with the semantics of the HTML canvas the game was first painted
    /// with: the same paths, transforms, gradients, composite modes, shadows and clips, so the
    /// drawing code could come across line for line and still paint the same picture. Shapes
    /// are tessellated into triangles and handed to the frame; nothing is rasterised here.
    /// </summary>
    public sealed class Ctx
    {
        sealed class State
        {
            public Mat2D m = Mat2D.Identity;
            public Style fill = Col.Black;
            public Style stroke = Col.Black;
            public double lineWidth = 1;
            public LineCap cap = LineCap.Butt;
            public LineJoin join = LineJoin.Miter;
            public double miterLimit = 10;
            public double alpha = 1;
            public Composite comp = Composite.SourceOver;
            public double[] dash;
            public double dashOffset;
            public double shadowBlur;
            public Col shadowColor = Col.Transparent;
            public double shadowX, shadowY;
            public string font = "10px sans-serif";
            public TextAlign align = TextAlign.Start;
            public TextBaseline baseline = TextBaseline.Alphabetic;
            public double filterBlur;
            public string filter = "none";
            public bool smoothing = true;
            public int clipDepth;

            public State Clone() { return (State)MemberwiseClone(); }
        }

        public readonly Surface canvas;
        State st = new State();
        readonly Stack<State> stack = new Stack<State>();
        readonly PathData path = new PathData();
        readonly List<ClipRec> clips = new List<ClipRec>();

        struct ClipRec { public int first, count, frame; }

        static readonly Tessellator tess = new Tessellator();
        static readonly Stroker stroker = new Stroker();
        static readonly Emitter emit = new Emitter();
        static readonly Dictionary<long, Surface> temps = new Dictionary<long, Surface>();

        internal Ctx(Surface s) { canvas = s; }

        internal void ResetState()
        {
            st = new State();
            stack.Clear();
            path.Clear();
            clips.Clear();
        }

        // ------------------------------------------------------------ state

        public Style FillStyle { get { return st.fill; } set { st.fill = value; } }
        public Style StrokeStyle { get { return st.stroke; } set { st.stroke = value; } }
        public double LineWidth
        {
            get { return st.lineWidth; }
            set { if (value > 0 && !double.IsInfinity(value) && !double.IsNaN(value)) st.lineWidth = value; }
        }
        public LineCap LineCap { get { return st.cap; } set { st.cap = value; } }
        public LineJoin LineJoin { get { return st.join; } set { st.join = value; } }
        public double MiterLimit { get { return st.miterLimit; } set { if (value > 0) st.miterLimit = value; } }
        public double GlobalAlpha
        {
            get { return st.alpha; }
            set { if (value >= 0 && value <= 1) st.alpha = value; }
        }
        public Composite GlobalCompositeOperation { get { return st.comp; } set { st.comp = value; } }
        public double ShadowBlur { get { return st.shadowBlur; } set { if (value >= 0) st.shadowBlur = value; } }
        public Col ShadowColor { get { return st.shadowColor; } set { st.shadowColor = value; } }
        public double ShadowOffsetX { get { return st.shadowX; } set { st.shadowX = value; } }
        public double ShadowOffsetY { get { return st.shadowY; } set { st.shadowY = value; } }
        public string Font { get { return st.font; } set { st.font = value; } }
        public TextAlign TextAlign { get { return st.align; } set { st.align = value; } }
        public TextBaseline TextBaseline { get { return st.baseline; } set { st.baseline = value; } }
        public bool ImageSmoothingEnabled { get { return st.smoothing; } set { st.smoothing = value; } }
        public double LineDashOffset { get { return st.dashOffset; } set { st.dashOffset = value; } }

        /// <summary>Only "none" and "blur(Npx)" are understood (all the game ever used).</summary>
        public string Filter
        {
            get { return st.filter; }
            set
            {
                st.filter = value ?? "none";
                st.filterBlur = 0;
                if (value != null && value.StartsWith("blur("))
                {
                    string n = value.Substring(5).TrimEnd(')').Trim();
                    if (n.EndsWith("px")) n = n.Substring(0, n.Length - 2);
                    double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out st.filterBlur);
                }
            }
        }

        public void SetLineDash(double[] segments)
        {
            if (segments == null || segments.Length == 0) { st.dash = null; return; }
            foreach (var d in segments) if (d < 0 || double.IsNaN(d)) return;
            // an odd list is doubled, as canvas does
            st.dash = segments.Length % 2 == 1 ? Concat(segments, segments) : (double[])segments.Clone();
        }

        public double[] GetLineDash() { return st.dash == null ? new double[0] : (double[])st.dash.Clone(); }

        static double[] Concat(double[] a, double[] b)
        {
            var r = new double[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        public void Save() { stack.Push(st.Clone()); }

        public void Restore()
        {
            if (stack.Count == 0) return;
            var back = stack.Pop();
            // undo clips made since the save
            while (clips.Count > back.clipDepth)
            {
                var c = clips[clips.Count - 1];
                clips.RemoveAt(clips.Count - 1);
                if (c.frame == Gfx.FrameId)
                {
                    Gfx.frame.Add(new Cmd
                    {
                        kind = CmdKind.Draw, target = canvas, blend = Composite.SourceOver, first = c.first,
                        count = c.count, clip = clips.Count + 1, stencil = StencilMode.Pop,
                    });
                }
            }
            st = back;
        }

        // ------------------------------------------------------------ transforms

        public void Translate(double x, double y) { st.m = st.m.Translate(x, y); }
        public void Rotate(double a) { st.m = st.m.Rotate(a); }
        public void Scale(double x, double y) { st.m = st.m.Scale(x, y); }
        public void Transform(double a, double b, double c, double d, double e, double f)
        {
            st.m = st.m.Mul(new Mat2D(a, b, c, d, e, f));
        }
        public void SetTransform(double a, double b, double c, double d, double e, double f)
        {
            st.m = new Mat2D(a, b, c, d, e, f);
        }
        public void SetTransform(Mat2D m) { st.m = m; }
        public void ResetTransform() { st.m = Mat2D.Identity; }
        public Mat2D GetTransform() { return st.m; }

        // ------------------------------------------------------------ paths

        public void BeginPath() { path.Clear(); }

        public void MoveTo(double x, double y)
        {
            st.m.Apply(x, y, out var dx, out var dy);
            path.MoveTo(dx, dy);
        }

        public void LineTo(double x, double y)
        {
            st.m.Apply(x, y, out var dx, out var dy);
            path.LineTo(dx, dy);
        }

        public void QuadraticCurveTo(double cx, double cy, double x, double y)
        {
            st.m.Apply(cx, cy, out var dcx, out var dcy);
            st.m.Apply(x, y, out var dx, out var dy);
            path.QuadTo(dcx, dcy, dx, dy);
        }

        public void BezierCurveTo(double c1x, double c1y, double c2x, double c2y, double x, double y)
        {
            st.m.Apply(c1x, c1y, out var a, out var b);
            st.m.Apply(c2x, c2y, out var c, out var d);
            st.m.Apply(x, y, out var e, out var f);
            path.CubicTo(a, b, c, d, e, f);
        }

        public void Arc(double x, double y, double r, double a0, double a1, bool ccw = false)
        {
            if (r < 0) r = 0;
            path.Arc(st.m, x, y, r, r, 0, a0, a1, ccw);
        }

        public void Ellipse(double x, double y, double rx, double ry, double rot, double a0, double a1, bool ccw = false)
        {
            if (rx < 0) rx = 0;
            if (ry < 0) ry = 0;
            path.Arc(st.m, x, y, rx, ry, rot, a0, a1, ccw);
        }

        public void ArcTo(double x1, double y1, double x2, double y2, double r)
        {
            path.ArcTo(st.m, st.m.Inverse(), x1, y1, x2, y2, r);
        }

        public void Rect(double x, double y, double w, double h)
        {
            MoveTo(x, y);
            LineTo(x + w, y);
            LineTo(x + w, y + h);
            LineTo(x, y + h);
            ClosePath();
        }

        public void RoundRect(double x, double y, double w, double h, double r)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(Math.Abs(w), Math.Abs(h)) / 2));
            MoveTo(x + r, y);
            LineTo(x + w - r, y);
            Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
            LineTo(x + w, y + h - r);
            Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
            LineTo(x + r, y + h);
            Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
            LineTo(x, y + r);
            Arc(x + r, y + r, r, Math.PI, Math.PI * 1.5);
            ClosePath();
        }

        public void ClosePath() { path.ClosePath(); }

        // ------------------------------------------------------------ gradients

        public LinearGradient CreateLinearGradient(double x0, double y0, double x1, double y1)
        {
            return new LinearGradient(x0, y0, x1, y1);
        }

        public RadialGradient CreateRadialGradient(double x0, double y0, double r0, double x1, double y1, double r1)
        {
            return new RadialGradient(x0, y0, r0, x1, y1, r1);
        }

        public Pattern CreatePattern(Surface img, string repetition) { return new Pattern(img, repetition); }

        // ------------------------------------------------------------ painting

        enum Geom { Fill, Stroke, Image }

        bool fillEvenOdd;
        // image quad, in local space
        Surface img;
        double isx, isy, isw, ish, idx, idy, idw, idh;

        public void Fill() { fillEvenOdd = false; Paint(Geom.Fill, st.fill); }

        public void Fill(string rule) { fillEvenOdd = rule == "evenodd"; Paint(Geom.Fill, st.fill); }

        public void Stroke() { Paint(Geom.Stroke, st.stroke); }

        public void FillRect(double x, double y, double w, double h)
        {
            if (w == 0 || h == 0) return;
            if (st.comp == Composite.Saturation) { Saturate(x, y, w, h); return; }
            var keep = SwapPath();
            Rect(x, y, w, h);
            fillEvenOdd = false;
            Paint(Geom.Fill, st.fill);
            RestorePath(keep);
        }

        public void StrokeRect(double x, double y, double w, double h)
        {
            var keep = SwapPath();
            Rect(x, y, w, h);
            Paint(Geom.Stroke, st.stroke);
            RestorePath(keep);
        }

        public void ClearRect(double x, double y, double w, double h)
        {
            // the device-space bounds of the transformed rectangle
            Bounds(x, y, w, h, out int rx, out int ry, out int rw, out int rh);
            if (rw <= 0 || rh <= 0) return;
            bool axis = Math.Abs(st.m.b) < 1e-9 && Math.Abs(st.m.c) < 1e-9;
            if (axis && clips.Count == 0)
            {
                Gfx.frame.Add(new Cmd { kind = CmdKind.Clear, target = canvas, rx = rx, ry = ry, rw = rw, rh = rh, clip = clips.Count });
                return;
            }
            // rotated or clipped: erase the exact shape
            var keep = SwapPath();
            var save = st;
            st = st.Clone();
            st.comp = Composite.DestinationOut;
            st.alpha = 1;
            st.shadowColor = Col.Transparent;
            st.filterBlur = 0;
            Rect(x, y, w, h);
            fillEvenOdd = false;
            Paint(Geom.Fill, Col.Black);
            st = save;
            RestorePath(keep);
        }

        void Bounds(double x, double y, double w, double h, out int rx, out int ry, out int rw, out int rh)
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            for (int i = 0; i < 4; i++)
            {
                double px = x + ((i & 1) != 0 ? w : 0), py = y + ((i & 2) != 0 ? h : 0);
                st.m.Apply(px, py, out var dx, out var dy);
                minX = Math.Min(minX, dx); minY = Math.Min(minY, dy);
                maxX = Math.Max(maxX, dx); maxY = Math.Max(maxY, dy);
            }
            int x0 = (int)Math.Floor(Math.Max(0, minX)), y0 = (int)Math.Floor(Math.Max(0, minY));
            int x1 = (int)Math.Ceiling(Math.Min(canvas.width, maxX)), y1 = (int)Math.Ceiling(Math.Min(canvas.height, maxY));
            rx = x0; ry = y0; rw = x1 - x0; rh = y1 - y0;
        }

        void Saturate(double x, double y, double w, double h)
        {
            Bounds(x, y, w, h, out int rx, out int ry, out int rw, out int rh);
            if (rw <= 0 || rh <= 0) return;
            Col c = st.fill.kind == StyleKind.Color ? st.fill.color : Col.Black;
            Gfx.frame.Add(new Cmd
            {
                kind = CmdKind.Saturation, target = canvas, rx = rx, ry = ry, rw = rw, rh = rh,
                color = c.Fade(st.alpha), clip = clips.Count,
            });
        }

        // the path is shared between operations that need a temporary one
        double[] keepX = new double[0], keepY = new double[0];
        readonly List<int> keepStarts = new List<int>();
        readonly List<bool> keepClosed = new List<bool>();

        int SwapPath()
        {
            int n = path.count;
            if (keepX.Length < n) { keepX = new double[n * 2]; keepY = new double[n * 2]; }
            Array.Copy(path.xs, keepX, n);
            Array.Copy(path.ys, keepY, n);
            keepStarts.Clear(); keepStarts.AddRange(path.starts);
            keepClosed.Clear(); keepClosed.AddRange(path.closed);
            path.Clear();
            return n;
        }

        void RestorePath(int n)
        {
            path.Clear();
            for (int s = 0; s < keepStarts.Count; s++)
            {
                int a = keepStarts[s], b = s + 1 < keepStarts.Count ? keepStarts[s + 1] : n;
                if (b <= a) continue;
                path.MoveTo(keepX[a], keepY[a]);
                for (int i = a + 1; i < b; i++) path.LineTo(keepX[i], keepY[i]);
                if (keepClosed[s]) path.ClosePath();
            }
        }

        // ------------------------------------------------------------ images

        public void DrawImage(Surface image, double dx, double dy)
        {
            if (image == null) return;
            DrawImage(image, 0, 0, image.width, image.height, dx, dy, image.width, image.height);
        }

        public void DrawImage(Surface image, double dx, double dy, double dw, double dh)
        {
            if (image == null) return;
            DrawImage(image, 0, 0, image.width, image.height, dx, dy, dw, dh);
        }

        public void DrawImage(Surface image, double sx, double sy, double sw, double sh, double dx, double dy, double dw, double dh)
        {
            if (image == null || sw == 0 || sh == 0 || dw == 0 || dh == 0) return;
            img = image;
            isx = sx; isy = sy; isw = sw; ish = sh;
            idx = dx; idy = dy; idw = dw; idh = dh;
            Paint(Geom.Image, Col.White);
            img = null;
        }

        // ------------------------------------------------------------ clip

        public void Clip() { Clip("nonzero"); }

        public void Clip(string rule)
        {
            var f = Gfx.frame;
            int first = f.indexCount;
            emit.Begin(f, Col.White, 1, Mat2D.Identity, default(Style));
            tess.Fill(path, rule == "evenodd", emit);
            int count = f.indexCount - first;
            clips.Add(new ClipRec { first = first, count = count, frame = Gfx.FrameId });
            st.clipDepth = clips.Count;
            f.Add(new Cmd
            {
                kind = CmdKind.Draw, target = canvas, blend = Composite.SourceOver, first = first, count = count,
                clip = clips.Count - 1, stencil = StencilMode.Push,
            });
        }

        // ------------------------------------------------------------ text

        public double MeasureText(string text) { return Gfx.MeasureText(text, st.font); }

        public void FillText(string text, double x, double y) { Text(text, x, y, double.NaN, false); }
        public void FillText(string text, double x, double y, double maxWidth) { Text(text, x, y, maxWidth, false); }
        public void StrokeText(string text, double x, double y) { Text(text, x, y, double.NaN, true); }

        void Text(string text, double x, double y, double maxWidth, bool stroke)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = stroke ? st.stroke : st.fill;
            Col c = style.kind == StyleKind.Color ? style.color : Col.White;
            var run = new TextRun
            {
                text = text, font = st.font, x = x, y = y, align = st.align, baseline = st.baseline,
                color = c.Fade(st.alpha), m = st.m, maxWidth = maxWidth, stroke = stroke, lineWidth = st.lineWidth,
            };
            if (ShadowOn)
            {
                var t = Temp();
                Gfx.frame.Add(new Cmd { kind = CmdKind.Clear, target = t, rx = 0, ry = 0, rw = t.width, rh = t.height });
                Gfx.frame.Add(new Cmd { kind = CmdKind.Text, target = t, text = run, blend = Composite.SourceOver });
                ShadowFrom(t);
            }
            Gfx.frame.Add(new Cmd { kind = CmdKind.Text, target = canvas, text = run, blend = st.comp, clip = clips.Count });
        }

        // ------------------------------------------------------------ core

        bool ShadowOn
        {
            get
            {
                return st.shadowColor.a > 0 && (st.shadowBlur > 0 || st.shadowX != 0 || st.shadowY != 0);
            }
        }

        /// <summary>One scratch surface per size, reused in order through the frame.</summary>
        Surface Temp()
        {
            long key = ((long)canvas.width << 32) | (uint)canvas.height;
            if (!temps.TryGetValue(key, out var t))
            {
                t = new Surface(canvas.width, canvas.height, canvas.antialias, "temp");
                temps[key] = t;
            }
            return t;
        }

        void Paint(Geom g, Style style)
        {
            var f = Gfx.frame;
            if (ShadowOn)
            {
                var t = Temp();
                f.Add(new Cmd { kind = CmdKind.Clear, target = t, rx = 0, ry = 0, rw = t.width, rh = t.height });
                Produce(t, g, style, Composite.SourceOver, 0, 1);
                ShadowFrom(t);
            }
            var comp = st.comp;
            bool unbounded = comp == Composite.SourceIn || comp == Composite.DestinationIn || comp == Composite.Copy;
            if (st.filterBlur > 0 || unbounded)
            {
                // paint it on its own first, then lay that over the whole target in one go:
                // the modes that clear what the source does not cover need a source that covers everything
                var t = Temp();
                f.Add(new Cmd { kind = CmdKind.Clear, target = t, rx = 0, ry = 0, rw = t.width, rh = t.height });
                Produce(t, g, style, Composite.SourceOver, 0, st.alpha);
                if (st.filterBlur > 0)
                    f.Add(new Cmd { kind = CmdKind.Blur, target = t, radius = (float)(st.filterBlur) });
                Blit(t, canvas, comp, Col.White, 0, 0, false);
                return;
            }
            Produce(canvas, g, style, comp, clips.Count, st.alpha);
        }

        /// <summary>The blurred, coloured silhouette of what is in <paramref name="t"/>, laid under the target.</summary>
        void ShadowFrom(Surface t)
        {
            var f = Gfx.frame;
            if (st.shadowBlur > 0) f.Add(new Cmd { kind = CmdKind.Blur, target = t, radius = (float)(st.shadowBlur / 2) });
            Blit(t, canvas, st.comp, st.shadowColor.Fade(st.alpha), st.shadowX, st.shadowY, true);
        }

        /// <summary>Draw a whole surface onto another, device pixel for device pixel.</summary>
        void Blit(Surface src, Surface dst, Composite comp, Col tint, double ox, double oy, bool mask)
        {
            var f = Gfx.frame;
            int first = f.indexCount;
            float k = mask ? (float)PaintKind.Mask : (float)PaintKind.Image;
            float x0 = (float)ox, y0 = (float)oy, x1 = x0 + src.width, y1 = y0 + src.height;
            int a = f.AddVert(new Vert { x = x0, y = y0, r = tint.r, g = tint.g, b = tint.b, a = tint.a, u = 0, v = 0, kind = k });
            int b = f.AddVert(new Vert { x = x1, y = y0, r = tint.r, g = tint.g, b = tint.b, a = tint.a, u = 1, v = 0, kind = k });
            int c = f.AddVert(new Vert { x = x1, y = y1, r = tint.r, g = tint.g, b = tint.b, a = tint.a, u = 1, v = 1, kind = k });
            int d = f.AddVert(new Vert { x = x0, y = y1, r = tint.r, g = tint.g, b = tint.b, a = tint.a, u = 0, v = 1, kind = k });
            f.AddIndex(a); f.AddIndex(b); f.AddIndex(c);
            f.AddIndex(a); f.AddIndex(c); f.AddIndex(d);
            bool unbounded = comp == Composite.SourceIn || comp == Composite.DestinationIn || comp == Composite.Copy;
            if (unbounded && (ox != 0 || oy != 0 || src.width < dst.width || src.height < dst.height))
            {
                // the parts of the target the quad misses are cleared by these modes as well
                // (only reached when a shadow is offset; the plain path covers the whole target)
            }
            f.Draw(dst, comp, src, false, first, 6, clips.Count);
        }

        /// <summary>Tessellate the geometry and append it, painted with the style, onto a target.</summary>
        void Produce(Surface target, Geom g, Style style, Composite comp, int clip, double alpha)
        {
            var f = Gfx.frame;
            int first = f.indexCount;
            Surface tex = null;
            bool repeat = false;
            if (g == Geom.Image)
            {
                tex = img;
                EmitImage(f, alpha);
            }
            else
            {
                if (style.kind == StyleKind.Pattern)
                {
                    if (style.pattern == null || style.pattern.image == null) return;
                    tex = style.pattern.image;
                    repeat = style.pattern.repeatX || style.pattern.repeatY;
                }
                emit.Begin(f, style.kind == StyleKind.Color ? style.color : Col.White, alpha, st.m.Inverse(), style);
                if (g == Geom.Fill) tess.Fill(path, fillEvenOdd, emit);
                else StrokeGeom();
            }
            f.Draw(target, comp, tex, repeat, first, f.indexCount - first, clip);
        }

        void StrokeGeom()
        {
            double scale = st.m.MeanScale;
            stroker.halfWidth = st.lineWidth * scale / 2;
            stroker.cap = st.cap;
            stroker.join = st.join;
            stroker.miterLimit = st.miterLimit;
            if (st.dash != null)
            {
                var d = new double[st.dash.Length];
                for (int i = 0; i < d.Length; i++) d[i] = st.dash[i] * scale;
                stroker.dash = d;
                stroker.dashOffset = st.dashOffset * scale;
            }
            else stroker.dash = null;
            stroker.Stroke(path, emit);
        }

        void EmitImage(Frame f, double alpha)
        {
            float iw = img.width, ih = img.height;
            float u0 = (float)(isx / iw), v0 = (float)(isy / ih), u1 = (float)((isx + isw) / iw), v1 = (float)((isy + ish) / ih);
            float al = (float)alpha;
            double[] lx = { idx, idx + idw, idx + idw, idx };
            double[] ly = { idy, idy, idy + idh, idy + idh };
            float[] us = { u0, u1, u1, u0 };
            float[] vs = { v0, v0, v1, v1 };
            int baseV = -1;
            for (int i = 0; i < 4; i++)
            {
                st.m.Apply(lx[i], ly[i], out var dx, out var dy);
                int vi = f.AddVert(new Vert
                {
                    x = (float)dx, y = (float)dy, r = 1, g = 1, b = 1, a = al, u = us[i], v = vs[i],
                    kind = (float)PaintKind.Image,
                });
                if (i == 0) baseV = vi;
            }
            f.AddIndex(baseV); f.AddIndex(baseV + 1); f.AddIndex(baseV + 2);
            f.AddIndex(baseV); f.AddIndex(baseV + 2); f.AddIndex(baseV + 3);
        }

        /// <summary>Writes tessellated vertices with the paint attributes of the current fill.</summary>
        sealed class Emitter : IGeomSink
        {
            Frame f;
            Vert proto;
            Mat2D inv;
            bool local;
            bool pattern;
            float pw, ph;

            public void Begin(Frame frame, Col color, double alpha, Mat2D inverse, Style style)
            {
                f = frame;
                inv = inverse;
                proto = new Vert();
                local = false;
                pattern = false;
                switch (style.kind)
                {
                    case StyleKind.Linear:
                    {
                        var g = (LinearGradient)style.gradient;
                        proto.kind = (float)PaintKind.Linear;
                        proto.row = f.RowFor(g);
                        proto.p0 = (float)g.x0; proto.p1 = (float)g.y0; proto.p2 = (float)g.x1; proto.p3 = (float)g.y1;
                        proto.r = proto.g = proto.b = 1; proto.a = (float)alpha;
                        local = true;
                        break;
                    }
                    case StyleKind.Radial:
                    {
                        var g = (RadialGradient)style.gradient;
                        proto.kind = (float)PaintKind.Radial;
                        proto.row = f.RowFor(g);
                        proto.p0 = (float)g.x0; proto.p1 = (float)g.y0; proto.p2 = (float)g.r0;
                        proto.p3 = (float)g.x1; proto.p4 = (float)g.y1; proto.p5 = (float)g.r1;
                        proto.r = proto.g = proto.b = 1; proto.a = (float)alpha;
                        local = true;
                        break;
                    }
                    case StyleKind.Pattern:
                        proto.kind = (float)PaintKind.Pattern;
                        proto.r = proto.g = proto.b = 1; proto.a = (float)alpha;
                        pw = style.pattern.image.width;
                        ph = style.pattern.image.height;
                        local = true;
                        pattern = true;
                        break;
                    default:
                        proto.kind = (float)PaintKind.Solid;
                        proto.r = color.r; proto.g = color.g; proto.b = color.b;
                        proto.a = (float)(color.a * alpha);
                        break;
                }
            }

            public int V(double x, double y)
            {
                var v = proto;
                v.x = (float)x;
                v.y = (float)y;
                if (local)
                {
                    inv.Apply(x, y, out var lx, out var ly);
                    if (pattern) { v.u = (float)(lx / pw); v.v = (float)(ly / ph); }
                    else { v.u = (float)lx; v.v = (float)ly; }
                }
                return f.AddVert(v);
            }

            public void T(int a, int b, int c)
            {
                f.AddIndex(a);
                f.AddIndex(b);
                f.AddIndex(c);
            }
        }
    }
}
