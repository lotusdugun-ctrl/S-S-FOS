using System;
using System.Collections.Generic;

namespace Sisifos.Paint
{
    /// <summary>
    /// The current path, already flattened into device-space polylines (canvas transforms each
    /// point by the matrix in force when it is added, so this is what the path really is).
    /// </summary>
    public sealed class PathData
    {
        public double[] xs = new double[256];
        public double[] ys = new double[256];
        public int count;

        /// <summary>Start index of each subpath; a subpath runs to the next start (or count).</summary>
        public readonly List<int> starts = new List<int>(16);
        public readonly List<bool> closed = new List<bool>(16);

        /// <summary>Flattening tolerance in device pixels.</summary>
        public const double Tolerance = 0.2;

        double curX, curY;
        bool hasCur;

        public void Clear()
        {
            count = 0;
            starts.Clear();
            closed.Clear();
            hasCur = false;
        }

        public int SubpathCount { get { return starts.Count; } }

        public int SubStart(int i) { return starts[i]; }
        public int SubEnd(int i) { return i + 1 < starts.Count ? starts[i + 1] : count; }

        void Push(double x, double y)
        {
            if (count == xs.Length)
            {
                Array.Resize(ref xs, count * 2);
                Array.Resize(ref ys, count * 2);
            }
            xs[count] = x;
            ys[count] = y;
            count++;
            curX = x;
            curY = y;
        }

        public void MoveTo(double x, double y)
        {
            starts.Add(count);
            closed.Add(false);
            Push(x, y);
            hasCur = true;
        }

        public void LineTo(double x, double y)
        {
            if (!hasCur) { MoveTo(x, y); return; }
            // a closed subpath that keeps drawing starts a new one at its first point
            if (closed[closed.Count - 1]) StartAfterClose();
            Push(x, y);
        }

        void StartAfterClose()
        {
            int s = starts[starts.Count - 1];
            double x = xs[s], y = ys[s];
            starts.Add(count);
            closed.Add(false);
            Push(x, y);
        }

        public void ClosePath()
        {
            if (!hasCur || starts.Count == 0) return;
            if (closed[closed.Count - 1]) return;
            closed[closed.Count - 1] = true;
            int s = starts[starts.Count - 1];
            curX = xs[s];
            curY = ys[s];
        }

        public bool HasCurrent { get { return hasCur; } }
        public double CurX { get { return curX; } }
        public double CurY { get { return curY; } }

        /// <summary>A quadratic Bézier in device space.</summary>
        public void QuadTo(double cx, double cy, double x, double y)
        {
            if (!hasCur) MoveTo(cx, cy);
            if (closed[closed.Count - 1]) StartAfterClose();
            double x0 = curX, y0 = curY;
            double dd = Math.Abs(x0 - 2 * cx + x) + Math.Abs(y0 - 2 * cy + y);
            int n = Segments(dd);
            for (int i = 1; i <= n; i++)
            {
                double t = (double)i / n, u = 1 - t;
                Push(u * u * x0 + 2 * u * t * cx + t * t * x, u * u * y0 + 2 * u * t * cy + t * t * y);
            }
        }

        /// <summary>A cubic Bézier in device space.</summary>
        public void CubicTo(double c1x, double c1y, double c2x, double c2y, double x, double y)
        {
            if (!hasCur) MoveTo(c1x, c1y);
            if (closed[closed.Count - 1]) StartAfterClose();
            double x0 = curX, y0 = curY;
            double dd = Math.Max(
                Math.Abs(x0 - 2 * c1x + c2x) + Math.Abs(y0 - 2 * c1y + c2y),
                Math.Abs(c1x - 2 * c2x + x) + Math.Abs(c1y - 2 * c2y + y));
            int n = Segments(dd * 1.5);
            for (int i = 1; i <= n; i++)
            {
                double t = (double)i / n, u = 1 - t;
                double a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
                Push(a * x0 + b * c1x + c * c2x + d * x, a * y0 + b * c1y + c * c2y + d * y);
            }
        }

        static int Segments(double dd)
        {
            // the classic bound: n = sqrt(dd / (8·tol)) segments keep the error under tol
            int n = (int)Math.Ceiling(Math.Sqrt(dd / (8 * Tolerance) * 2));
            return n < 1 ? 1 : n > 200 ? 200 : n;
        }

        /// <summary>
        /// An elliptical arc in local space (centre, radii, rotation, angles), mapped through m.
        /// Connects from the current point with a straight line, as canvas does.
        /// </summary>
        public void Arc(in Mat2D m, double cx, double cy, double rx, double ry, double rot, double a0, double a1, bool ccw)
        {
            if (rx < 0 || ry < 0) throw new ArgumentException("negative radius");
            const double TAU = Math.PI * 2;
            double sweep;
            if (!ccw)
            {
                sweep = a1 - a0;
                if (sweep >= TAU) sweep = TAU;
                else if (sweep < 0) { sweep = sweep % TAU; if (sweep < 0) sweep += TAU; }
            }
            else
            {
                sweep = a1 - a0;
                if (sweep <= -TAU) sweep = -TAU;
                else if (sweep > 0) { sweep = sweep % TAU; if (sweep > 0) sweep -= TAU; }
            }
            double r = Math.Max(rx, ry) * m.MaxScale;
            // segment count from the chord error: tol = r(1 - cos(dθ/2))
            int n;
            if (r < 0.01) n = 1;
            else
            {
                double ratio = 1 - Tolerance / r;
                double dth = ratio <= -1 ? Math.PI : 2 * Math.Acos(Math.Max(-1, Math.Min(1, ratio)));
                n = (int)Math.Ceiling(Math.Abs(sweep) / Math.Max(dth, 1e-3));
                if (n < 1) n = 1;
                if (n > 360) n = 360;
            }
            double cr = Math.Cos(rot), sr = Math.Sin(rot);
            for (int i = 0; i <= n; i++)
            {
                double ang = a0 + sweep * i / n;
                double lx = rx * Math.Cos(ang), ly = ry * Math.Sin(ang);
                double px = cx + lx * cr - ly * sr, py = cy + lx * sr + ly * cr;
                m.Apply(px, py, out var dx, out var dy);
                if (i == 0)
                {
                    if (!hasCur) MoveTo(dx, dy);
                    else LineTo(dx, dy);
                }
                else Push(dx, dy);
            }
        }

        /// <summary>arcTo in local space (tangent arc between two lines), mapped through m.</summary>
        public void ArcTo(in Mat2D m, Mat2D inv, double x1, double y1, double x2, double y2, double radius)
        {
            if (!hasCur) { m.Apply(x1, y1, out var mx, out var my); MoveTo(mx, my); }
            inv.Apply(curX, curY, out var x0, out var y0);
            double v1x = x0 - x1, v1y = y0 - y1, v2x = x2 - x1, v2y = y2 - y1;
            double l1 = Math.Sqrt(v1x * v1x + v1y * v1y), l2 = Math.Sqrt(v2x * v2x + v2y * v2y);
            double cross = v1x * v2y - v1y * v2x;
            if (l1 < 1e-9 || l2 < 1e-9 || Math.Abs(cross) < 1e-9 || radius == 0)
            {
                m.Apply(x1, y1, out var lx, out var ly);
                LineTo(lx, ly);
                return;
            }
            v1x /= l1; v1y /= l1; v2x /= l2; v2y /= l2;
            double cosA = v1x * v2x + v1y * v2y;
            double ang = Math.Acos(Math.Max(-1, Math.Min(1, cosA)));
            double dist = radius / Math.Tan(ang / 2);
            double t1x = x1 + v1x * dist, t1y = y1 + v1y * dist;
            double t2x = x1 + v2x * dist, t2y = y1 + v2y * dist;
            // centre: along the bisector
            double bx = v1x + v2x, by = v1y + v2y;
            double bl = Math.Sqrt(bx * bx + by * by);
            bx /= bl; by /= bl;
            double cd = radius / Math.Sin(ang / 2);
            double ccx = x1 + bx * cd, ccy = y1 + by * cd;
            double s0 = Math.Atan2(t1y - ccy, t1x - ccx);
            double s1 = Math.Atan2(t2y - ccy, t2x - ccx);
            bool ccw = cross > 0;
            m.Apply(t1x, t1y, out var dtx, out var dty);
            LineTo(dtx, dty);
            Arc(m, ccx, ccy, radius, radius, 0, s0, s1, ccw);
        }
    }
}
