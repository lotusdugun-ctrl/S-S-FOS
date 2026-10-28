using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>
    /// A reference rasteriser for the drawing system: executes a frame exactly as the GPU path
    /// is meant to (premultiplied colour, the same composite equations, stencil clips), on the
    /// CPU, with 2×2 supersampling standing in for MSAA. Used to render frames headless and
    /// compare them with the web build.
    /// </summary>
    public sealed class CpuBackend : IBackend
    {
        public const int SS = 2;

        sealed class Buf
        {
            public int w, h; // in samples
            public float[] px; // premultiplied rgba
            public byte[] stencil;
            public int gen;
        }

        readonly Dictionary<Surface, Buf> bufs = new Dictionary<Surface, Buf>();
        float[][] luts = new float[0][];

        Buf Get(Surface s)
        {
            if (!bufs.TryGetValue(s, out var b) || b.gen != s.generation)
            {
                b = new Buf { w = s.width * SS, h = s.height * SS, gen = s.generation };
                b.px = new float[b.w * b.h * 4];
                b.stencil = new byte[b.w * b.h];
                bufs[s] = b;
            }
            return b;
        }

        public void Release(Surface s) { bufs.Remove(s); }

        public double MeasureText(string text, string font)
        {
            var f = FontSpec.Parse(font);
            return (text?.Length ?? 0) * f.size * 0.48;
        }

        /// <summary>skip rasterising (warm-up frames in tests): only allocations happen</summary>
        public bool skip;

        public void Execute(Frame f)
        {
            if (skip)
            {
                foreach (var c in f.cmds) if (c.kind == CmdKind.Alloc) Get(c.target);
                return;
            }
            // gradient lookup tables, as the atlas would hold them
            luts = new float[f.gradients.Count][];
            for (int i = 0; i < f.gradients.Count; i++)
            {
                var lut = new float[1024 * 4];
                for (int k = 0; k < 1024; k++)
                {
                    f.gradients[i].SamplePremul(k / 1023.0, out var r, out var g, out var b, out var a);
                    lut[k * 4] = r; lut[k * 4 + 1] = g; lut[k * 4 + 2] = b; lut[k * 4 + 3] = a;
                }
                luts[i] = lut;
            }
            foreach (var c in f.cmds)
            {
                switch (c.kind)
                {
                    case CmdKind.Alloc:
                        {
                            // the size recorded with this Alloc, not the surface's current one: a
                            // surface resized twice in one frame must draw at each size in turn
                            var nb = new Buf { w = c.rw * SS, h = c.rh * SS, gen = c.target.generation };
                            nb.px = new float[nb.w * nb.h * 4];
                            nb.stencil = new byte[nb.w * nb.h];
                            bufs[c.target] = nb;
                            break;
                        }
                    case CmdKind.Clear: Clear(Get(c.target), c.rx, c.ry, c.rw, c.rh); break;
                    case CmdKind.Pixels: Pixels(Get(c.target), c); break;
                    case CmdKind.Draw: Draw(f, c); break;
                    case CmdKind.Blur: Blur(Get(c.target), c.radius * SS); break;
                    case CmdKind.Saturation: Saturate(Get(c.target), c); break;
                    case CmdKind.Text: TextBox(f, c); break;
                }
            }
            // stencils do not outlive the frame (the GPU path clears them on first use)
            foreach (var b in bufs.Values) Array.Clear(b.stencil, 0, b.stencil.Length);
        }

        static void Clear(Buf b, int x, int y, int w, int h)
        {
            int x0 = Math.Max(0, x * SS), y0 = Math.Max(0, y * SS);
            int x1 = Math.Min(b.w, (x + w) * SS), y1 = Math.Min(b.h, (y + h) * SS);
            for (int yy = y0; yy < y1; yy++)
                Array.Clear(b.px, (yy * b.w + x0) * 4, Math.Max(0, x1 - x0) * 4);
        }

        static void Pixels(Buf b, Cmd c)
        {
            for (int yy = 0; yy < c.rh * SS; yy++)
            {
                int ty = c.ry * SS + yy;
                if (ty < 0 || ty >= b.h) continue;
                int sy = yy / SS;
                for (int xx = 0; xx < c.rw * SS; xx++)
                {
                    int tx = c.rx * SS + xx;
                    if (tx < 0 || tx >= b.w) continue;
                    int si = (sy * c.rw + xx / SS) * 4;
                    float a = c.pixels[si + 3] / 255f;
                    int di = (ty * b.w + tx) * 4;
                    b.px[di] = c.pixels[si] / 255f * a;
                    b.px[di + 1] = c.pixels[si + 1] / 255f * a;
                    b.px[di + 2] = c.pixels[si + 2] / 255f * a;
                    b.px[di + 3] = a;
                }
            }
        }

        // ------------------------------------------------------------ triangles

        void Draw(Frame f, Cmd c)
        {
            var dst = Get(c.target);
            Buf tex = c.texture != null ? Get(c.texture) : null;
            for (int i = c.first; i < c.first + c.count; i += 3)
            {
                Tri(f, dst, tex, c, f.verts[f.indices[i]], f.verts[f.indices[i + 1]], f.verts[f.indices[i + 2]]);
            }
        }

        void Tri(Frame f, Buf dst, Buf tex, Cmd c, Vert a, Vert b, Vert d)
        {
            double ax = a.x * SS, ay = a.y * SS, bx = b.x * SS, by = b.y * SS, cx = d.x * SS, cy = d.y * SS;
            double area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area) < 1e-12) return;
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
            int x1 = Math.Min(dst.w - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
            int y1 = Math.Min(dst.h - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
            if (x0 > x1 || y0 > y1) return;
            double inv = 1.0 / area;
            double sgn = area > 0 ? 1 : -1;
            // top-left rule on the (orientation-normalised) edge functions, so a sample on an edge
            // shared by two triangles is painted by exactly one of them
            bool tl0 = TopLeft(bx, by, cx, cy, sgn), tl1 = TopLeft(cx, cy, ax, ay, sgn), tl2 = TopLeft(ax, ay, bx, by, sgn);
            var kind = (PaintKind)(int)a.kind;
            int row = (int)a.row;
            Gradient grad = (kind == PaintKind.Linear || kind == PaintKind.Radial) && row < f.gradients.Count ? f.gradients[row] : null;
            float[] lut = grad != null ? luts[row] : null;
            for (int y = y0; y <= y1; y++)
            {
                double py = y + 0.5;
                for (int x = x0; x <= x1; x++)
                {
                    double pxx = x + 0.5;
                    double e0 = ((bx - pxx) * (cy - py) - (by - py) * (cx - pxx)) * sgn;
                    double e1 = ((cx - pxx) * (ay - py) - (cy - py) * (ax - pxx)) * sgn;
                    double e2 = ((ax - pxx) * (by - py) - (ay - py) * (bx - pxx)) * sgn;
                    if (e0 < 0 || e1 < 0 || e2 < 0) continue;
                    if ((e0 == 0 && !tl0) || (e1 == 0 && !tl1) || (e2 == 0 && !tl2)) continue;
                    double w0 = e0 * sgn * inv, w1 = e1 * sgn * inv, w2 = e2 * sgn * inv;
                    int si = y * dst.w + x;
                    if (c.stencil == StencilMode.Push)
                    {
                        if (dst.stencil[si] == c.clip) dst.stencil[si]++;
                        continue;
                    }
                    if (c.stencil == StencilMode.Pop)
                    {
                        if (dst.stencil[si] == c.clip) dst.stencil[si]--;
                        continue;
                    }
                    if (c.clip > 0 && dst.stencil[si] != c.clip) continue;
                    float al = (float)(a.a * w0 + b.a * w1 + d.a * w2);
                    float sr, sg, sb, sa;
                    switch (kind)
                    {
                        case PaintKind.Solid:
                        {
                            float r = (float)(a.r * w0 + b.r * w1 + d.r * w2);
                            float g = (float)(a.g * w0 + b.g * w1 + d.g * w2);
                            float bl = (float)(a.b * w0 + b.b * w1 + d.b * w2);
                            sr = r * al; sg = g * al; sb = bl * al; sa = al;
                            break;
                        }
                        case PaintKind.Linear:
                        case PaintKind.Radial:
                        {
                            double u = a.u * w0 + b.u * w1 + d.u * w2;
                            double v = a.v * w0 + b.v * w1 + d.v * w2;
                            double t = kind == PaintKind.Linear ? ((LinearGradient)grad).T(u, v) : ((RadialGradient)grad).T(u, v);
                            if (double.IsNaN(t)) continue;
                            int k = (int)Math.Round(Math.Max(0, Math.Min(1, t)) * 1023);
                            sr = lut[k * 4] * al; sg = lut[k * 4 + 1] * al; sb = lut[k * 4 + 2] * al; sa = lut[k * 4 + 3] * al;
                            break;
                        }
                        default:
                        {
                            double u = a.u * w0 + b.u * w1 + d.u * w2;
                            double v = a.v * w0 + b.v * w1 + d.v * w2;
                            if (tex == null) continue;
                            if (kind == PaintKind.Pattern || c.repeat)
                            {
                                u -= Math.Floor(u);
                                v -= Math.Floor(v);
                            }
                            Sample(tex, u, v, out sr, out sg, out sb, out sa);
                            if (kind == PaintKind.Mask)
                            {
                                float m = sa * al;
                                float r = (float)(a.r * w0 + b.r * w1 + d.r * w2);
                                float g = (float)(a.g * w0 + b.g * w1 + d.g * w2);
                                float bl = (float)(a.b * w0 + b.b * w1 + d.b * w2);
                                sr = r * m; sg = g * m; sb = bl * m; sa = m;
                            }
                            else { sr *= al; sg *= al; sb *= al; sa *= al; }
                            break;
                        }
                    }
                    Blend(dst.px, si * 4, c.blend, sr, sg, sb, sa);
                }
            }
        }

        static bool TopLeft(double x0, double y0, double x1, double y1, double sgn)
        {
            double ex = (x1 - x0) * sgn, ey = (y1 - y0) * sgn;
            return ey < 0 || (ey == 0 && ex > 0);
        }

        static void Sample(Buf t, double u, double v, out float r, out float g, out float b, out float a)
        {
            double fx = u * t.w - 0.5, fy = v * t.h - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float tx = (float)(fx - x0), ty = (float)(fy - y0);
            r = g = b = a = 0;
            for (int j = 0; j < 2; j++)
            {
                for (int i = 0; i < 2; i++)
                {
                    int xx = Math.Max(0, Math.Min(t.w - 1, x0 + i)), yy = Math.Max(0, Math.Min(t.h - 1, y0 + j));
                    float wgt = (i == 0 ? 1 - tx : tx) * (j == 0 ? 1 - ty : ty);
                    int k = (yy * t.w + xx) * 4;
                    r += t.px[k] * wgt; g += t.px[k + 1] * wgt; b += t.px[k + 2] * wgt; a += t.px[k + 3] * wgt;
                }
            }
        }

        static void Blend(float[] d, int i, Composite op, float sr, float sg, float sb, float sa)
        {
            float dr = d[i], dg = d[i + 1], db = d[i + 2], da = d[i + 3];
            switch (op)
            {
                case Composite.SourceOver:
                    d[i] = sr + dr * (1 - sa); d[i + 1] = sg + dg * (1 - sa); d[i + 2] = sb + db * (1 - sa); d[i + 3] = sa + da * (1 - sa);
                    break;
                case Composite.Lighter:
                    d[i] = Math.Min(1, sr + dr); d[i + 1] = Math.Min(1, sg + dg); d[i + 2] = Math.Min(1, sb + db); d[i + 3] = Math.Min(1, sa + da);
                    break;
                case Composite.SourceAtop:
                    d[i] = sr * da + dr * (1 - sa); d[i + 1] = sg * da + dg * (1 - sa); d[i + 2] = sb * da + db * (1 - sa);
                    break;
                case Composite.SourceIn:
                    d[i] = sr * da; d[i + 1] = sg * da; d[i + 2] = sb * da; d[i + 3] = sa * da;
                    break;
                case Composite.DestinationOut:
                    d[i] = dr * (1 - sa); d[i + 1] = dg * (1 - sa); d[i + 2] = db * (1 - sa); d[i + 3] = da * (1 - sa);
                    break;
                case Composite.DestinationIn:
                    d[i] = dr * sa; d[i + 1] = dg * sa; d[i + 2] = db * sa; d[i + 3] = da * sa;
                    break;
                case Composite.Copy:
                    d[i] = sr; d[i + 1] = sg; d[i + 2] = sb; d[i + 3] = sa;
                    break;
                case Composite.Multiply:
                    d[i] = sr * (1 - da) + dr * (1 - sa) + sr * dr;
                    d[i + 1] = sg * (1 - da) + dg * (1 - sa) + sg * dg;
                    d[i + 2] = sb * (1 - da) + db * (1 - sa) + sb * db;
                    d[i + 3] = sa + da - sa * da;
                    break;
                case Composite.Screen:
                    d[i] = sr + dr - sr * dr; d[i + 1] = sg + dg - sg * dg; d[i + 2] = sb + db - sb * db; d[i + 3] = sa + da - sa * da;
                    break;
                default:
                    goto case Composite.SourceOver;
            }
        }

        // ------------------------------------------------------------ effects

        /// <summary>Gaussian blur, approximated by three box passes each way.</summary>
        static void Blur(Buf b, double sigma)
        {
            if (sigma < 0.3) return;
            int[] boxes = Boxes(sigma, 3);
            var tmp = new float[b.px.Length];
            foreach (int r in boxes)
            {
                BoxH(b.px, tmp, b.w, b.h, (r - 1) / 2);
                BoxV(tmp, b.px, b.w, b.h, (r - 1) / 2);
            }
        }

        static int[] Boxes(double sigma, int n)
        {
            double wIdeal = Math.Sqrt(12 * sigma * sigma / n + 1);
            int wl = (int)Math.Floor(wIdeal);
            if (wl % 2 == 0) wl--;
            int wu = wl + 2;
            double mIdeal = (12 * sigma * sigma - n * wl * wl - 4 * n * wl - 3 * n) / (-4 * wl - 4);
            int m = (int)Math.Round(mIdeal);
            var r = new int[n];
            for (int i = 0; i < n; i++) r[i] = i < m ? wl : wu;
            return r;
        }

        static void BoxH(float[] src, float[] dst, int w, int h, int r)
        {
            float k = 1f / (2 * r + 1);
            for (int y = 0; y < h; y++)
            {
                for (int ch = 0; ch < 4; ch++)
                {
                    float acc = 0;
                    for (int x = -r; x <= r; x++) acc += Px(src, w, x, y, ch, w);
                    for (int x = 0; x < w; x++)
                    {
                        dst[(y * w + x) * 4 + ch] = acc * k;
                        int xo = x - r, xi = x + r + 1;
                        acc -= xo < 0 ? 0 : src[(y * w + xo) * 4 + ch];
                        acc += xi >= w ? 0 : src[(y * w + xi) * 4 + ch];
                    }
                }
            }
        }

        static float Px(float[] s, int w, int x, int y, int ch, int _)
        {
            return x < 0 || x >= w ? 0 : s[(y * w + x) * 4 + ch];
        }

        static void BoxV(float[] src, float[] dst, int w, int h, int r)
        {
            float k = 1f / (2 * r + 1);
            for (int x = 0; x < w; x++)
            {
                for (int ch = 0; ch < 4; ch++)
                {
                    float acc = 0;
                    for (int y = -r; y <= r; y++) acc += y < 0 || y >= h ? 0 : src[(y * w + x) * 4 + ch];
                    for (int y = 0; y < h; y++)
                    {
                        dst[(y * w + x) * 4 + ch] = acc * k;
                        int yo = y - r, yi = y + r + 1;
                        acc -= yo < 0 ? 0 : src[(yo * w + x) * 4 + ch];
                        acc += yi >= h ? 0 : src[(yi * w + x) * 4 + ch];
                    }
                }
            }
        }

        /// <summary>The "saturation" blend: the backdrop keeps its hue and lightness, takes the source's saturation.</summary>
        static void Saturate(Buf b, Cmd c)
        {
            float sa = c.color.a;
            float sSat = Sat(c.color.r, c.color.g, c.color.b);
            int x0 = Math.Max(0, c.rx * SS), y0 = Math.Max(0, c.ry * SS);
            int x1 = Math.Min(b.w, (c.rx + c.rw) * SS), y1 = Math.Min(b.h, (c.ry + c.rh) * SS);
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int i = (y * b.w + x) * 4;
                    float da = b.px[i + 3];
                    if (da <= 0) continue;
                    float r = b.px[i] / da, g = b.px[i + 1] / da, bl = b.px[i + 2] / da;
                    SetSat(ref r, ref g, ref bl, sSat);
                    SetLum(ref r, ref g, ref bl, Lum(b.px[i] / da, b.px[i + 1] / da, b.px[i + 2] / da));
                    // mix: result = (1 - sa)·backdrop + sa·blended, over the existing alpha
                    float or = b.px[i] / da, og = b.px[i + 1] / da, ob = b.px[i + 2] / da;
                    b.px[i] = (or + (r - or) * sa) * da;
                    b.px[i + 1] = (og + (g - og) * sa) * da;
                    b.px[i + 2] = (ob + (bl - ob) * sa) * da;
                }
            }
        }

        static float Lum(float r, float g, float b) { return 0.3f * r + 0.59f * g + 0.11f * b; }
        static float Sat(float r, float g, float b) { return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)); }

        static void SetLum(ref float r, ref float g, ref float b, float l)
        {
            float d = l - Lum(r, g, b);
            r += d; g += d; b += d;
            float L = Lum(r, g, b);
            float n = Math.Min(r, Math.Min(g, b)), x = Math.Max(r, Math.Max(g, b));
            if (n < 0) { r = L + (r - L) * L / (L - n); g = L + (g - L) * L / (L - n); b = L + (b - L) * L / (L - n); }
            if (x > 1) { r = L + (r - L) * (1 - L) / (x - L); g = L + (g - L) * (1 - L) / (x - L); b = L + (b - L) * (1 - L) / (x - L); }
        }

        static void SetSat(ref float r, ref float g, ref float b, float s)
        {
            float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            float span = mx - mn;
            float F(float c) { return span > 0 ? (c - mn) * s / span : 0; }
            r = F(r); g = F(g); b = F(b);
        }

        /// <summary>Text is not rasterised in tests: a faint bar shows where the run sits.</summary>
        void TextBox(Frame f, Cmd c)
        {
            var t = c.text;
            var fs = FontSpec.Parse(t.font);
            double w = MeasureText(t.text, t.font);
            double x = t.x;
            if (t.align == TextAlign.Center) x -= w / 2;
            else if (t.align == TextAlign.Right || t.align == TextAlign.End) x -= w;
            double y = t.y - fs.size * 0.7;
            if (t.baseline == TextBaseline.Middle) y = t.y - fs.size * 0.35;
            else if (t.baseline == TextBaseline.Top || t.baseline == TextBaseline.Hanging) y = t.y;
            var dst = Get(c.target);
            for (int k = 0; k < 4; k++)
            {
                // corners through the run's transform
            }
            double h = fs.size * 0.7;
            for (double yy = y; yy < y + h; yy += 1.0 / SS)
            {
                for (double xx = x; xx < x + w; xx += 1.0 / SS)
                {
                    t.m.Apply(xx, yy, out var dx, out var dy);
                    int sx = (int)(dx * SS), sy = (int)(dy * SS);
                    if (sx < 0 || sy < 0 || sx >= dst.w || sy >= dst.h) continue;
                    // a stripe pattern so it reads as text, not a solid block
                    if (((int)((xx - x) / (fs.size * 0.48)) & 1) == 1) continue;
                    float a = t.color.a * 0.8f;
                    Blend(dst.px, (sy * dst.w + sx) * 4, Composite.SourceOver, t.color.r * a, t.color.g * a, t.color.b * a, a);
                }
            }
        }

        // ------------------------------------------------------------ output

        /// <summary>The surface as 8-bit straight RGBA at its own size (the samples averaged).</summary>
        public byte[] Read(Surface s, out int w, out int h)
        {
            var b = Get(s);
            w = s.width; h = s.height;
            var o = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float r = 0, g = 0, bl = 0, a = 0;
                    for (int j = 0; j < SS; j++)
                        for (int i = 0; i < SS; i++)
                        {
                            int k = ((y * SS + j) * b.w + x * SS + i) * 4;
                            r += b.px[k]; g += b.px[k + 1]; bl += b.px[k + 2]; a += b.px[k + 3];
                        }
                    float n = SS * SS;
                    r /= n; g /= n; bl /= n; a /= n;
                    int oi = (y * w + x) * 4;
                    if (a > 0)
                    {
                        o[oi] = (byte)Math.Round(Math.Min(1, r / a) * 255);
                        o[oi + 1] = (byte)Math.Round(Math.Min(1, g / a) * 255);
                        o[oi + 2] = (byte)Math.Round(Math.Min(1, bl / a) * 255);
                    }
                    o[oi + 3] = (byte)Math.Round(Math.Min(1, a) * 255);
                }
            }
            return o;
        }

        public byte[] ReadPixels(Surface s, out int w, out int h) { return Read(s, out w, out h); }

        public void SavePng(Surface s, string file)
        {
            var px = Read(s, out int w, out int h);
            Png.Write(file, px, w, h);
        }
    }

    public static class Png
    {
        public static void Write(string file, byte[] rgba, int w, int h)
        {
            using var fs = File.Create(file);
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            BE(ihdr, 0, w); BE(ihdr, 4, h);
            ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            Chunk(fs, "IHDR", ihdr);
            var raw = new MemoryStream();
            for (int y = 0; y < h; y++)
            {
                raw.WriteByte(0);
                raw.Write(rgba, y * w * 4, w * 4);
            }
            var z = new MemoryStream();
            using (var zs = new ZLibStream(z, CompressionLevel.Fastest, true)) { raw.Position = 0; raw.CopyTo(zs); }
            Chunk(fs, "IDAT", z.ToArray());
            Chunk(fs, "IEND", new byte[0]);
        }

        static void BE(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            BE(len, 0, data.Length);
            s.Write(len);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            Array.Copy(data, 0, td, 4, data.Length);
            s.Write(td);
            var crc = new byte[4];
            BE(crc, 0, (int)Crc(td));
            s.Write(crc);
        }

        static uint[] table;
        static uint Crc(byte[] d)
        {
            if (table == null)
            {
                table = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    table[n] = c;
                }
            }
            uint crc = 0xFFFFFFFFu;
            foreach (var b in d) crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
