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
            Work.threaded = Environment.GetEnvironmentVariable("THREADED") == "1";
            Js.Seed(7);
            var screen = new Surface(w, h, true, "screen");
            var init = new EngineInit { cycles = cycles, tutorialDone = true };
            var e = new SisyphusEngine(screen, 0, init);
            e.hostW = w; e.hostH = h; e.hostDpr = 1;
            var dd = Environment.GetEnvironmentVariable("HL_DPR");
            if (dd != null) e.hostDpr = double.Parse(dd);
            e.start();
            e.activate();
            double t = Js.now();
            var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var upd = typeof(SisyphusEngine).GetMethod("update", BF);
            var ren = typeof(SisyphusEngine).GetMethod("render", BF);
            var zz = Environment.GetEnvironmentVariable("ZOOM");
            if (zz != null) typeof(SisyphusEngine).GetField("dbgZoom", BF | System.Reflection.BindingFlags.Public).SetValue(e, double.Parse(zz));
            var x0 = Environment.GetEnvironmentVariable("X0");
            if (x0 != null) { typeof(SisyphusEngine).GetField("x", BF | System.Reflection.BindingFlags.Public).SetValue(e, double.Parse(x0)); }
            double fps = 20;
            double dt = 1000.0 / fps;
            int frames = (int)(seconds * fps);
            int shot = 0;
            double nextShot = 0;
            string phase = "";
            e.onState = s => phase = s.phase;
            bool perfMode = Environment.GetEnvironmentVariable("PERF") == "1";
            double renSum = 0, renMax = 0; int renN = 0;
            for (int i = 0; i < frames; i++)
            {
                double sec = i / fps;
                // a player who pushes in rhythm: press for most of each beat, release briefly
                double beat = (sec % 0.75) / 0.75;
                e.input = beat < 0.8 ? 1 : 0;
                t += dt;
                upd.Invoke(e, new object[] { 1.0 / fps });
                SisyphusEngine.dbgRainbow = Environment.GetEnvironmentVariable("RAINBOW") == "1";
                var mom = Environment.GetEnvironmentVariable("MOM");
                if (mom != null)
                {
                    var tt = typeof(SisyphusEngine);
                    var ul = (System.Collections.Generic.List<int>)tt.GetField("unlocked", BF).GetValue(e);
                    while (ul.Count < 60) ul.Add(ul.Count);
                    tt.GetField("flow", BF | System.Reflection.BindingFlags.Public).SetValue(e, 1.0);
                    var we = tt.GetField("wayEvent", BF).GetValue(e);
                    if (i == 10)
                    {
                        var wt = tt.GetNestedType("WayEvent", BF | System.Reflection.BindingFlags.Public);
                        var o = Activator.CreateInstance(wt);
                        wt.GetField("kind").SetValue(o, mom);
                        wt.GetField("at").SetValue(o, (double)tt.GetField("x", BF | System.Reflection.BindingFlags.Public).GetValue(e) + 60);
                        wt.GetField("t").SetValue(o, 0.0);
                        tt.GetField("wayEvent", BF).SetValue(e, o);
                        tt.GetField("eventForCycle", BF).SetValue(e, (double)tt.GetField("cycles", BF | System.Reflection.BindingFlags.Public).GetValue(e));
                    }
                }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                ren.Invoke(e, null);
                double rms = sw.Elapsed.TotalMilliseconds;
                if (perfMode) { renSum += rms; renN++; if (rms > renMax) renMax = rms; }
                if (perfMode && rms > 25) Console.WriteLine("SPIKE " + i + " t=" + sec.ToString("0.0") + " " + rms.ToString("0") + " ms phase=" + phase + " x=" + Dbg(e));
                Work.Pump();
                if (perfMode)
                {
                    if (i % 20 == 19) { Console.WriteLine("render avg " + (renSum / renN).ToString("0.0") + " ms max " + renMax.ToString("0.0") + " verts " + Gfx.frame.vertCount + " cmds " + Gfx.frame.cmds.Count); renSum = 0; renN = 0; renMax = 0; }
                    Gfx.frame.Reset(Gfx.frame.id + 1);
                    continue;
                }
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
            if (Environment.GetEnvironmentVariable("CARD") == "1")
            {
                var card = new Surface(1080, 1350, true, "card");
                var cc = card.GetContext();
                cc.FillStyle = "#171a2c"; cc.FillRect(0, 0, 1080, 1350);
                Sisifos.App.UI.Antique.MomentCard(cc, screen, 1080, 1350, "Döngü 42", "Bu tırmanış 2:31  ·  Rüzgârlı Sırt", "'Cormorant Garamond', serif");
                Gfx.Flush();
                be.SavePng(card, Path.Combine(outDir, "card.png"));
            }
        }
    }
}
