using System;

namespace Sisifos.Paint
{
    /// <summary>
    /// A 2D affine transform in the canvas convention: x' = a·x + c·y + e, y' = b·x + d·y + f.
    /// </summary>
    public struct Mat2D
    {
        public double a, b, c, d, e, f;

        public Mat2D(double a, double b, double c, double d, double e, double f)
        {
            this.a = a; this.b = b; this.c = c; this.d = d; this.e = e; this.f = f;
        }

        public static readonly Mat2D Identity = new Mat2D(1, 0, 0, 1, 0, 0);

        /// <summary>this · m (m applied first, as canvas' transform() does).</summary>
        public Mat2D Mul(in Mat2D m)
        {
            return new Mat2D(
                a * m.a + c * m.b,
                b * m.a + d * m.b,
                a * m.c + c * m.d,
                b * m.c + d * m.d,
                a * m.e + c * m.f + e,
                b * m.e + d * m.f + f);
        }

        public Mat2D Translate(double x, double y) { return Mul(new Mat2D(1, 0, 0, 1, x, y)); }
        public Mat2D Scale(double x, double y) { return Mul(new Mat2D(x, 0, 0, y, 0, 0)); }
        public Mat2D Rotate(double ang)
        {
            double cs = Math.Cos(ang), sn = Math.Sin(ang);
            return Mul(new Mat2D(cs, sn, -sn, cs, 0, 0));
        }

        public double Det { get { return a * d - b * c; } }

        public Mat2D Inverse()
        {
            double det = Det;
            if (Math.Abs(det) < 1e-300) return Identity;
            double id = 1.0 / det;
            return new Mat2D(d * id, -b * id, -c * id, a * id, (c * f - d * e) * id, (b * e - a * f) * id);
        }

        public void Apply(double x, double y, out double ox, out double oy)
        {
            ox = a * x + c * y + e;
            oy = b * x + d * y + f;
        }

        /// <summary>How much the transform scales lengths, on average (for curve flattening and line widths).</summary>
        public double MeanScale { get { return Math.Sqrt(Math.Abs(Det)); } }

        public double MaxScale
        {
            get
            {
                double sx = Math.Sqrt(a * a + b * b), sy = Math.Sqrt(c * c + d * d);
                return Math.Max(sx, sy);
            }
        }
    }
}
