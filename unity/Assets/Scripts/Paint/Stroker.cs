using System;
using System.Collections.Generic;

namespace Sisifos.Paint
{
    /// <summary>
    /// Turns flattened polylines into the triangles of a stroke: segment quads that share their
    /// corners where the line bends gently (so a semi-transparent stroke along a curve never
    /// paints any pixel twice), proper joins where it turns hard, and caps at the ends.
    /// </summary>
    public sealed class Stroker
    {
        double[] x = new double[256];
        double[] y = new double[256];
        readonly List<double> dx = new List<double>(256);
        readonly List<double> dy = new List<double>(256);

        public double halfWidth;
        public LineCap cap;
        public LineJoin join;
        public double miterLimit = 10;
        public double[] dash;      // device-space lengths, or null
        public double dashOffset;  // device-space

        IGeomSink sink;

        public void Stroke(PathData p, IGeomSink sink)
        {
            this.sink = sink;
            int subs = p.SubpathCount;
            for (int s = 0; s < subs; s++)
            {
                int a = p.SubStart(s), b = p.SubEnd(s);
                bool closed = p.closed[s];
                int n = Load(p, a, b);
                if (n == 1 || (n == 2 && Same(0, 1)))
                {
                    // a zero-length subpath: with round caps it is a dot
                    if (dash == null && cap == LineCap.Round && (b - a) >= 2) Dot(x[0], y[0]);
                    continue;
                }
                if (n < 2) continue;
                if (closed && n >= 3 && Same(0, n - 1)) n--;
                if (dash != null && dash.Length > 0) Dashed(n, closed);
                else if (closed && n >= 3) Closed(n);
                else Open(0, n);
            }
        }

        bool Same(int i, int j) { return Math.Abs(x[i] - x[j]) < 1e-7 && Math.Abs(y[i] - y[j]) < 1e-7; }

        int Load(PathData p, int a, int b)
        {
            int need = b - a + 2;
            if (x.Length < need) { x = new double[need * 2]; y = new double[need * 2]; }
            int n = 0;
            for (int i = a; i < b; i++)
            {
                double px = p.xs[i], py = p.ys[i];
                if (n > 0 && Math.Abs(px - x[n - 1]) < 1e-7 && Math.Abs(py - y[n - 1]) < 1e-7) continue;
                x[n] = px; y[n] = py; n++;
            }
            if (n == 1 && b - a >= 2) { x[1] = x[0]; y[1] = y[0]; return 1; }
            return n;
        }

        void Dot(double cx, double cy)
        {
            double r = halfWidth;
            int segs = ArcSegs(r, Math.PI * 2);
            int c = sink.V(cx, cy);
            int first = sink.V(cx + r, cy), prevV = first;
            for (int i = 1; i < segs; i++)
            {
                double a = Math.PI * 2 * i / segs;
                int v = sink.V(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
                sink.T(c, prevV, v);
                prevV = v;
            }
            sink.T(c, prevV, first);
        }

        static int ArcSegs(double r, double sweep)
        {
            if (r < 0.3) return Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2)));
            double ratio = 1 - PathData.Tolerance / r;
            double dth = ratio <= -1 ? Math.PI : 2 * Math.Acos(Math.Max(-1, Math.Min(1, ratio)));
            int n = (int)Math.Ceiling(Math.Abs(sweep) / Math.Max(dth, 1e-3));
            return n < 1 ? 1 : n > 128 ? 128 : n;
        }

        // ---------------------------------------------------------------- dashes

        readonly List<double> dashX = new List<double>(64);
        readonly List<double> dashY = new List<double>(64);
        double[] keepX = new double[0], keepY = new double[0];

        void Dashed(int n, bool closed)
        {
            double total = 0;
            foreach (var d in dash) total += Math.Max(0, d);
            if (total <= 1e-9) { if (closed && n >= 3) Closed(n); else Open(0, n); return; }
            // copy the outline out: emitting a dash reuses the point buffers
            int pts = closed ? n + 1 : n;
            if (keepX.Length < pts) { keepX = new double[pts * 2]; keepY = new double[pts * 2]; }
            for (int i = 0; i < n; i++) { keepX[i] = x[i]; keepY[i] = y[i]; }
            if (closed) { keepX[n] = x[0]; keepY[n] = y[0]; }
            int di = 0;
            double off = ((dashOffset % total) + total) % total;
            while (off >= dash[di]) { off -= dash[di]; di = (di + 1) % dash.Length; }
            double left = dash[di] - off;
            bool on = di % 2 == 0;
            dashX.Clear(); dashY.Clear();
            if (on) { dashX.Add(keepX[0]); dashY.Add(keepY[0]); }
            for (int i = 0; i + 1 < pts; i++)
            {
                double ax = keepX[i], ay = keepY[i], bx = keepX[i + 1], by = keepY[i + 1];
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                double pos = 0;
                while (len - pos > left)
                {
                    pos += left;
                    double t = pos / len;
                    double qx = ax + (bx - ax) * t, qy = ay + (by - ay) * t;
                    if (on) { dashX.Add(qx); dashY.Add(qy); EmitDash(); }
                    else { dashX.Clear(); dashY.Clear(); dashX.Add(qx); dashY.Add(qy); }
                    on = !on;
                    di = (di + 1) % dash.Length;
                    left = dash[di];
                }
                left -= len - pos;
                if (on) { dashX.Add(bx); dashY.Add(by); }
            }
            if (on) EmitDash();
        }

        void EmitDash()
        {
            int m = dashX.Count;
            if (m >= 2)
            {
                if (x.Length < m) { x = new double[m * 2]; y = new double[m * 2]; }
                int k = 0;
                for (int i = 0; i < m; i++)
                {
                    if (k > 0 && Math.Abs(dashX[i] - x[k - 1]) < 1e-7 && Math.Abs(dashY[i] - y[k - 1]) < 1e-7) continue;
                    x[k] = dashX[i]; y[k] = dashY[i]; k++;
                }
                if (k >= 2) Open(0, k);
                else if (cap == LineCap.Round) Dot(dashX[0], dashY[0]);
            }
            dashX.Clear(); dashY.Clear();
        }

        // ---------------------------------------------------------------- lines

        // per-vertex rails: where each segment ends (in) and the next starts (out)
        int[] lin = new int[256], rin = new int[256], lout = new int[256], rout = new int[256];

        void Ensure(int n)
        {
            if (lin.Length < n)
            {
                lin = new int[n * 2]; rin = new int[n * 2]; lout = new int[n * 2]; rout = new int[n * 2];
            }
            dx.Clear(); dy.Clear();
        }

        void Dirs(int n, bool closed)
        {
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                double ex = x[j] - x[i], ey = y[j] - y[i];
                double l = Math.Sqrt(ex * ex + ey * ey);
                if (l < 1e-12) { dx.Add(1); dy.Add(0); }
                else { dx.Add(ex / l); dy.Add(ey / l); }
            }
        }

        double SegLen(int i, int n)
        {
            int j = (i + 1) % n;
            return Math.Sqrt((x[j] - x[i]) * (x[j] - x[i]) + (y[j] - y[i]) * (y[j] - y[i]));
        }

        void Open(int start, int n)
        {
            Ensure(n);
            double w = halfWidth;
            if (cap == LineCap.Square)
            {
                double ex = x[1] - x[0], ey = y[1] - y[0], l = Math.Sqrt(ex * ex + ey * ey);
                if (l > 0) { x[0] -= ex / l * w; y[0] -= ey / l * w; }
                ex = x[n - 1] - x[n - 2]; ey = y[n - 1] - y[n - 2]; l = Math.Sqrt(ex * ex + ey * ey);
                if (l > 0) { x[n - 1] += ex / l * w; y[n - 1] += ey / l * w; }
            }
            Dirs(n, false);
            // ends
            {
                double nx = -dy[0] * w, ny = dx[0] * w;
                lout[0] = sink.V(x[0] + nx, y[0] + ny);
                rout[0] = sink.V(x[0] - nx, y[0] - ny);
                int e = n - 1;
                nx = -dy[e - 1] * w; ny = dx[e - 1] * w;
                lin[e] = sink.V(x[e] + nx, y[e] + ny);
                rin[e] = sink.V(x[e] - nx, y[e] - ny);
            }
            for (int i = 1; i < n - 1; i++) Join(i, i - 1, i, n);
            for (int i = 0; i < n - 1; i++)
            {
                sink.T(lout[i], rout[i], rin[i + 1]);
                sink.T(lout[i], rin[i + 1], lin[i + 1]);
            }
            if (cap == LineCap.Round)
            {
                RoundCap(x[0], y[0], dx[0], dy[0], rout[0], lout[0], true);
                RoundCap(x[n - 1], y[n - 1], dx[n - 2], dy[n - 2], lin[n - 1], rin[n - 1], false);
            }
        }

        void Closed(int n)
        {
            Ensure(n);
            Dirs(n, true);
            for (int i = 0; i < n; i++) Join(i, (i + n - 1) % n, i, n);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                sink.T(lout[i], rout[i], rin[j]);
                sink.T(lout[i], rin[j], lin[j]);
            }
        }

        /// <summary>
        /// A half disc on the end of a line. For the start it sweeps back from the right rail to
        /// the left; for the end, forward from the left rail to the right.
        /// </summary>
        void RoundCap(double cx, double cy, double ux, double uy, int fromV, int toV, bool start)
        {
            double w = halfWidth;
            // angle of the left normal
            double aL = Math.Atan2(ux, -uy);
            int segs = ArcSegs(w, Math.PI);
            int c = sink.V(cx, cy);
            int prevV = fromV;
            for (int i = 1; i < segs; i++)
            {
                double t = (double)i / segs;
                double a = start ? aL + Math.PI - Math.PI * t : aL - Math.PI * t;
                // start: from the right normal (aL+π) round through the back to the left (aL)
                // end: from the left normal (aL) through the front to the right (aL-π)
                int v = sink.V(cx + w * Math.Cos(a), cy + w * Math.Sin(a));
                sink.T(c, prevV, v);
                prevV = v;
            }
            sink.T(c, prevV, toV);
        }

        /// <summary>The corner at vertex i between segment s0 (into it) and s1 (out of it).</summary>
        void Join(int i, int s0, int s1, int n)
        {
            double w = halfWidth;
            double d0x = dx[s0], d0y = dy[s0], d1x = dx[s1], d1y = dy[s1];
            double n0x = -d0y, n0y = d0x, n1x = -d1y, n1y = d1x; // left normals
            double px = x[i], py = y[i];
            double cr = d0x * d1y - d0y * d1x;
            double dot = d0x * d1x + d0y * d1y;
            double mx = n0x + n1x, my = n0y + n1y;
            double ml = Math.Sqrt(mx * mx + my * my);
            // cos of half the turn
            double ch = ml / 2;
            if (ml > 1e-9) { mx /= ml; my /= ml; }
            // a gentle bend: both rails meet at their miter points
            if (dot > 0.866 || Math.Abs(cr) < 1e-9)
            {
                double k = ch > 0.2 ? w / ch : w;
                if (ml <= 1e-9) { mx = n0x; my = n0y; k = w; }
                int L = sink.V(px + mx * k, py + my * k);
                int R = sink.V(px - mx * k, py - my * k);
                lin[i] = lout[i] = L;
                rin[i] = rout[i] = R;
                return;
            }
            // which way it turns: toward the left normal when cr > 0, so the left rail is inside
            bool leftInner = cr > 0;
            double sgn = leftInner ? 1 : -1; // inner normal = sgn * normal
            double limit = Math.Min(SegLen(s0, n), SegLen(s1, n));
            double mlen = ch > 1e-6 ? w / ch : double.MaxValue;
            int inner0, inner1, center;
            if (mlen <= Math.Sqrt(limit * limit + w * w))
            {
                int M = sink.V(px + sgn * mx * mlen, py + sgn * my * mlen);
                inner0 = inner1 = center = M;
            }
            else
            {
                inner0 = sink.V(px + sgn * n0x * w, py + sgn * n0y * w);
                inner1 = sink.V(px + sgn * n1x * w, py + sgn * n1y * w);
                center = sink.V(px, py);
                sink.T(center, inner0, inner1);
            }
            int outer0 = sink.V(px - sgn * n0x * w, py - sgn * n0y * w);
            int outer1 = sink.V(px - sgn * n1x * w, py - sgn * n1y * w);
            if (leftInner)
            {
                lin[i] = inner0; lout[i] = inner1;
                rin[i] = outer0; rout[i] = outer1;
            }
            else
            {
                rin[i] = inner0; rout[i] = inner1;
                lin[i] = outer0; lout[i] = outer1;
            }
            // fill the outside of the corner
            if (join == LineJoin.Round)
            {
                double a0 = Math.Atan2(-sgn * n0y, -sgn * n0x);
                double a1 = Math.Atan2(-sgn * n1y, -sgn * n1x);
                double sweep = a1 - a0;
                while (sweep > Math.PI) sweep -= Math.PI * 2;
                while (sweep < -Math.PI) sweep += Math.PI * 2;
                int segs = ArcSegs(w, sweep);
                int prevV = outer0;
                for (int k = 1; k < segs; k++)
                {
                    double a = a0 + sweep * k / segs;
                    int v = sink.V(px + w * Math.Cos(a), py + w * Math.Sin(a));
                    sink.T(center, prevV, v);
                    prevV = v;
                }
                sink.T(center, prevV, outer1);
            }
            else if (join == LineJoin.Miter && ch > 1e-6 && 1 / ch <= miterLimit)
            {
                int tip = sink.V(px - sgn * mx * mlen, py - sgn * my * mlen);
                sink.T(center, outer0, tip);
                sink.T(center, tip, outer1);
            }
            else
            {
                sink.T(center, outer0, outer1);
            }
        }
    }
}
