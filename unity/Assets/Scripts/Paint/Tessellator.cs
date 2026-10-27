using System;
using System.Collections.Generic;
using LibTessDotNet;

namespace Sisifos.Paint
{
    /// <summary>Receives device-space geometry: vertices, then triangles over them.</summary>
    public interface IGeomSink
    {
        int V(double x, double y);
        void T(int a, int b, int c);
    }

    /// <summary>
    /// Turns a flattened path into triangles that cover exactly what canvas' fill() covers.
    /// Most of what the game draws is one convex outline (a limb, an ellipse, a cloud puff),
    /// so that goes as a fan; one concave outline is ear-clipped; anything with several
    /// outlines, holes or crossings goes through the full winding-rule tessellator.
    /// </summary>
    public sealed class Tessellator
    {
        double[] px = new double[256];
        double[] py = new double[256];
        int[] idx = new int[256];
        int[] prev = new int[256];
        int[] next = new int[256];
        Tess tess;
        ContourVertex[] contour = new ContourVertex[256];

        public void Fill(PathData p, bool evenOdd, IGeomSink sink)
        {
            int subs = p.SubpathCount;
            int live = 0, which = -1;
            for (int i = 0; i < subs; i++)
            {
                if (p.SubEnd(i) - p.SubStart(i) >= 3) { live++; which = i; }
            }
            if (live == 0) return;
            if (live == 1)
            {
                int n = Load(p, which);
                if (n < 3) return;
                if (IsConvex(n)) { Fan(n, sink); return; }
                if (!SelfIntersects(n) && EarClip(n, sink)) return;
            }
            Full(p, evenOdd, sink);
        }

        /// <summary>Copy one subpath, dropping repeated points and the closing duplicate.</summary>
        int Load(PathData p, int sub)
        {
            int s = p.SubStart(sub), e = p.SubEnd(sub);
            int need = e - s;
            if (px.Length < need)
            {
                int cap = Math.Max(need, px.Length * 2);
                px = new double[cap]; py = new double[cap];
                idx = new int[cap]; prev = new int[cap]; next = new int[cap];
            }
            int n = 0;
            for (int i = s; i < e; i++)
            {
                double x = p.xs[i], y = p.ys[i];
                if (n > 0 && Math.Abs(x - px[n - 1]) < 1e-7 && Math.Abs(y - py[n - 1]) < 1e-7) continue;
                px[n] = x; py[n] = y; n++;
            }
            while (n > 1 && Math.Abs(px[0] - px[n - 1]) < 1e-7 && Math.Abs(py[0] - py[n - 1]) < 1e-7) n--;
            // drop collinear runs, which confuse both the convexity test and the ear clipper
            int m = 0, dropped = 0;
            for (int i = 0; i < n; i++)
            {
                double ax = m == 0 ? px[n - 1] : px[m - 1], ay = m == 0 ? py[n - 1] : py[m - 1];
                double bx = px[i], by = py[i];
                double cx = px[(i + 1) % n], cy = py[(i + 1) % n];
                double ux = bx - ax, uy = by - ay, vx = cx - bx, vy = cy - by;
                double cr = ux * vy - uy * vx;
                double dot = ux * vx + uy * vy;
                double scale = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
                if (Math.Abs(cr) <= 1e-9 * scale && dot >= 0 && n - dropped > 3)
                {
                    dropped++;
                    continue;
                }
                px[m] = bx; py[m] = by; m++;
            }
            return m;
        }

        bool IsConvex(int n)
        {
            int sign = 0;
            double turning = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n, k = (i + 2) % n;
                double ax = px[j] - px[i], ay = py[j] - py[i];
                double bx = px[k] - px[j], by = py[k] - py[j];
                double cr = ax * by - ay * bx;
                if (Math.Abs(cr) < 1e-10) continue;
                int sg = cr > 0 ? 1 : -1;
                if (sign == 0) sign = sg;
                else if (sg != sign) return false;
                turning += Math.Atan2(cr, ax * bx + ay * by);
            }
            // a star traced the long way round turns the same way every time but twice over
            return Math.Abs(turning) < Math.PI * 2 + 0.01;
        }

        void Fan(int n, IGeomSink sink)
        {
            int b = sink.V(px[0], py[0]);
            int prevV = sink.V(px[1], py[1]);
            for (int i = 2; i < n; i++)
            {
                int v = sink.V(px[i], py[i]);
                sink.T(b, prevV, v);
                prevV = v;
            }
        }

        bool SelfIntersects(int n)
        {
            if (n > 400) return true; // too dear to check; let the full tessellator decide
            for (int i = 0; i < n; i++)
            {
                double ax = px[i], ay = py[i], bx = px[(i + 1) % n], by = py[(i + 1) % n];
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;
                    double cx = px[j], cy = py[j], dx = px[(j + 1) % n], dy = py[(j + 1) % n];
                    if (Cross(ax, ay, bx, by, cx, cy, dx, dy)) return true;
                }
            }
            return false;
        }

        static bool Cross(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
        {
            double d1 = (dx - cx) * (ay - cy) - (dy - cy) * (ax - cx);
            double d2 = (dx - cx) * (by - cy) - (dy - cy) * (bx - cx);
            double d3 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            double d4 = (bx - ax) * (dy - ay) - (by - ay) * (dx - ax);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        bool EarClip(int n, IGeomSink sink)
        {
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += px[i] * py[j] - px[j] * py[i];
            }
            if (Math.Abs(area) < 1e-9) return true; // nothing to fill
            double orient = area > 0 ? 1 : -1;
            for (int i = 0; i < n; i++)
            {
                prev[i] = (i + n - 1) % n;
                next[i] = (i + 1) % n;
                idx[i] = -1;
            }
            int remaining = n;
            int cur = 0;
            int guard = 0;
            int Vert(int i)
            {
                if (idx[i] < 0) idx[i] = sink.V(px[i], py[i]);
                return idx[i];
            }
            while (remaining > 3)
            {
                if (guard++ > remaining * 2 + 8) return false;
                int a = prev[cur], b = cur, c = next[cur];
                if (IsEar(a, b, c, orient))
                {
                    sink.T(Vert(a), Vert(b), Vert(c));
                    next[a] = c;
                    prev[c] = a;
                    remaining--;
                    cur = c;
                    guard = 0;
                }
                else cur = next[cur];
            }
            int x = cur;
            sink.T(Vert(prev[x]), Vert(x), Vert(next[x]));
            return true;
        }

        bool IsEar(int a, int b, int c, double orient)
        {
            double cr = (px[b] - px[a]) * (py[c] - py[b]) - (py[b] - py[a]) * (px[c] - px[b]);
            if (cr * orient <= 0) return false;
            for (int p = next[c]; p != a; p = next[p])
            {
                if (Inside(px[p], py[p], a, b, c, orient)) return false;
            }
            return true;
        }

        bool Inside(double x, double y, int a, int b, int c, double orient)
        {
            double c1 = ((px[b] - px[a]) * (y - py[a]) - (py[b] - py[a]) * (x - px[a])) * orient;
            double c2 = ((px[c] - px[b]) * (y - py[b]) - (py[c] - py[b]) * (x - px[b])) * orient;
            double c3 = ((px[a] - px[c]) * (y - py[c]) - (py[a] - py[c]) * (x - px[c])) * orient;
            return c1 >= 0 && c2 >= 0 && c3 >= 0;
        }

        void Full(PathData p, bool evenOdd, IGeomSink sink)
        {
            if (tess == null) tess = new Tess { NoEmptyPolygons = true };
            int subs = p.SubpathCount;
            int any = 0;
            for (int i = 0; i < subs; i++)
            {
                int s = p.SubStart(i), e = p.SubEnd(i);
                int n = e - s;
                if (n < 3) continue;
                if (contour.Length < n) contour = new ContourVertex[Math.Max(n, contour.Length * 2)];
                var arr = new ContourVertex[n];
                for (int k = 0; k < n; k++)
                {
                    arr[k].Position = new Vec3 { X = (float)p.xs[s + k], Y = (float)p.ys[s + k], Z = 0 };
                }
                tess.AddContour(arr, ContourOrientation.Original);
                any++;
            }
            if (any == 0) return;
            try
            {
                tess.Tessellate(evenOdd ? WindingRule.EvenOdd : WindingRule.NonZero, ElementType.Polygons, 3);
            }
            catch (Exception)
            {
                return;
            }
            var verts = tess.Vertices;
            int vc = tess.VertexCount;
            var map = new int[vc];
            for (int i = 0; i < vc; i++) map[i] = sink.V(verts[i].Position.X, verts[i].Position.Y);
            var el = tess.Elements;
            int ec = tess.ElementCount;
            for (int i = 0; i < ec; i++)
            {
                int a = el[i * 3], b = el[i * 3 + 1], c = el[i * 3 + 2];
                if (a < 0 || b < 0 || c < 0) continue;
                sink.T(map[a], map[b], map[c]);
            }
        }
    }
}
