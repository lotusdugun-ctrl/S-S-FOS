using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Sisifos
{
    /// <summary>
    /// The few places where JavaScript's numbers and Math behave differently from C#'s, so code
    /// that came across from the web build keeps doing exactly what it did there.
    /// </summary>
    public static class Js
    {
        static Random rng = new Random();

        /// <summary>Math.random(): uniform in [0, 1).</summary>
        public static double random() { return rng.NextDouble(); }

        /// <summary>Re-seed Math.random (tests only: the game never needs to).</summary>
        public static void Seed(int seed) { rng = new Random(seed); }

        /// <summary>Math.round: halves go up (toward +∞), unlike Math.Round's banker's rounding.</summary>
        public static double round(double x) { return Math.Floor(x + 0.5); }

        /// <summary>Math.sign, with 0 and NaN passed through.</summary>
        public static double sign(double x) { return double.IsNaN(x) ? double.NaN : x > 0 ? 1 : x < 0 ? -1 : x; }

        /// <summary>Math.hypot.</summary>
        public static double hypot(double x, double y) { return Math.Sqrt(x * x + y * y); }
        public static double hypot(double x, double y, double z) { return Math.Sqrt(x * x + y * y + z * z); }

        /// <summary>Math.imul: 32-bit integer multiply.</summary>
        public static int imul(int a, int b) { return unchecked(a * b); }
        public static int imul(double a, double b) { return unchecked(ToInt32(a) * ToInt32(b)); }

        /// <summary>ToInt32 (what `x | 0` does to a number).</summary>
        public static int ToInt32(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return 0;
            double t = Math.Truncate(d);
            double m = t % 4294967296.0;
            if (m < 0) m += 4294967296.0;
            uint u = (uint)m;
            return unchecked((int)u);
        }

        /// <summary>ToUint32 (what `x >>> 0` does to a number).</summary>
        public static uint ToUint32(double d) { return unchecked((uint)ToInt32(d)); }

        /// <summary>`a >>> n` on a number.</summary>
        public static uint urs(double a, int n) { return ToUint32(a) >> (n & 31); }
        public static uint urs(int a, int n) { return unchecked((uint)a) >> (n & 31); }

        /// <summary>`a % b` for numbers is the same in C#; this is the positive modulo idiom ((a % b) + b) % b.</summary>
        public static double mod(double a, double b) { return ((a % b) + b) % b; }

        public static double clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }

        /// <summary>Number.prototype.toFixed, for text the player reads.</summary>
        public static string toFixed(double v, int digits)
        {
            return v.ToString("F" + digits, CultureInfo.InvariantCulture);
        }

        /// <summary>String(number) for integers and simple decimals.</summary>
        public static string str(double v)
        {
            if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>performance.now(), in milliseconds.</summary>
        public static double now() { return clock.Elapsed.TotalMilliseconds; }
        static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        /// <summary>Date.now(), in milliseconds since the epoch.</summary>
        public static double dateNow() { return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds; }

        /// <summary>A Fisher–Yates shuffle as the web build did it with Math.random.</summary>
        public static void Shuffle<T>(IList<T> a)
        {
            for (int i = a.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(random() * (i + 1));
                T t = a[i]; a[i] = a[j]; a[j] = t;
            }
        }
    }

    /// <summary>
    /// Heavy work off the main thread, the way the web build used its paint worker: the job runs
    /// on the thread pool and its result is handed back on the main thread at the next Pump().
    /// </summary>
    public static class Work
    {
        static readonly ConcurrentQueue<Action> done = new ConcurrentQueue<Action>();
        /// <summary>False runs every job at once on the calling thread (tests, or no threads).</summary>
        public static bool threaded = true;

        public static void Run<T>(Func<T> job, Action<T> then)
        {
            if (!threaded)
            {
                T r = default(T);
                bool ok = true;
                try { r = job(); } catch (Exception) { ok = false; }
                then(ok ? r : default(T));
                return;
            }
            Task.Run(() =>
            {
                T r = default(T);
                bool ok = true;
                try { r = job(); } catch (Exception) { ok = false; }
                done.Enqueue(() => then(ok ? r : default(T)));
            });
        }

        /// <summary>Deliver finished jobs. Called once a frame from the main thread.</summary>
        public static void Pump()
        {
            int n = done.Count;
            while (n-- > 0 && done.TryDequeue(out var a)) a();
        }
    }
}
