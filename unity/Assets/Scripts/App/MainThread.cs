using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Sisifos.App
{
    /// <summary>
    /// Runs Unity-only calls (Resources, PlayerPrefs) for code on other threads (Work jobs):
    /// the caller blocks until the main thread's next Pump. The main thread never waits on a
    /// job (Work.Pump only collects finished ones), so this cannot deadlock.
    /// </summary>
    public static class MainThread
    {
        static int mainId = -1;
        static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        public static void Init() { mainId = Thread.CurrentThread.ManagedThreadId; }

        public static bool IsMain { get { return mainId < 0 || Thread.CurrentThread.ManagedThreadId == mainId; } }

        public static T Invoke<T>(Func<T> f)
        {
            if (IsMain) return f();
            T result = default(T);
            Exception error = null;
            using (var done = new ManualResetEventSlim(false))
            {
                queue.Enqueue(() =>
                {
                    try { result = f(); }
                    catch (Exception e) { error = e; }
                    finally { done.Set(); }
                });
                done.Wait();
            }
            if (error != null) throw new InvalidOperationException("main-thread call failed", error);
            return result;
        }

        public static void Pump()
        {
            while (queue.TryDequeue(out var a)) a();
        }
    }
}
