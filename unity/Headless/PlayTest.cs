using System;
using System.IO;
using Sisifos.Game;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>Runs the whole engine headless with a scripted player and saves frames.</summary>
    public static class PlayTest
    {
        static string Dbg(SisyphusEngine e)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in new[] { "x", "vx", "hour", "camX", "camY", "zoom", "dpr", "resScale", "breath", "levelIndex", "scale", "w", "h" })
            {
                var f = typeof(SisyphusEngine).GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                sb.Append(n).Append('=').Append(f == null ? "?" : Convert.ToString(f.GetValue(e), System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
            }
            return sb.ToString();
        }

        public static void Run(CpuBackend be, string outDir, int w, int h, double seconds, double every, double cycles)
        {
            Directory.CreateDirectory(outDir);
            Data.loader = p => File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            Work.threaded = false;
            Js.Seed(7);
            var screen = new Surface(w, h, true, "screen");
            var init = new EngineInit { cycles = cycles, tutorialDone = true };
            var e = new SisyphusEngine(screen, 0, init);
            e.hostW = w; e.hostH = h; e.hostDpr = 1;
            e.start();
            e.activate();
            double t = Js.now();
            var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var upd = typeof(SisyphusEngine).GetMethod("update", BF);
            var ren = typeof(SisyphusEngine).GetMethod("render", BF);
            double fps = 20;
            double dt = 1000.0 / fps;
            int frames = (int)(seconds * fps);
            int shot = 0;
            double nextShot = 0;
            string phase = "";
            e.onState = s => phase = s.phase;
            for (int i = 0; i < frames; i++)
            {
                double sec = i / fps;
                // a player who pushes in rhythm: press for most of each beat, release briefly
                double beat = (sec % 0.75) / 0.75;
                e.input = beat < 0.8 ? 1 : 0;
                t += dt;
                upd.Invoke(e, new object[] { 1.0 / fps });
                ren.Invoke(e, null);
                Work.Pump();
                bool save = sec >= nextShot;
                if (save)
                {
                    Gfx.Flush();
                    string f = Path.Combine(outDir, string.Format("f{0:000}.png", shot++));
                    be.SavePng(screen, f);
                    Console.WriteLine(f + " t=" + sec.ToString("0.0") + " phase=" + phase + " " + Dbg(e));
                    nextShot += every;
                }
                else Gfx.Flush();
            }
        }
    }
}
