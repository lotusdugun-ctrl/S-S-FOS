using System;
using System.IO;
using System.Reflection;
using Sisifos.Game;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>
    /// Films him: a run of consecutive frames at 30 fps in one situation (walking down, pushing),
    /// saved one PNG per frame for a contact sheet, plus a trace of where his hip and feet go.
    /// </summary>
    public static class GaitTest
    {
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static object F(SisyphusEngine e, string n) { return typeof(SisyphusEngine).GetField(n, BF)?.GetValue(e); }
        static void S(SisyphusEngine e, string n, object v) { typeof(SisyphusEngine).GetField(n, BF)?.SetValue(e, v); }

        public static void Run(CpuBackend be, string mode, string outDir, int w, int h, double warm, int frames, int level)
        {
            Directory.CreateDirectory(outDir);
            foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);
            Data.loader = p => File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            Work.threaded = false;
            Js.Seed(3);
            var screen = new Surface(w, h, true, "screen");
            var lv = Levels.getLevel(level);
            var init = new EngineInit { cycles = level * 3, tutorialDone = true };
            if (mode == "descent")
            {
                init.run = new RunSave
                {
                    cycles = level * 3, lv = lv.id, phase = "descent", x = 40, walkX = lv.length * 0.55, walkFrom = lv.length * 0.9,
                    hourFrom = 18.2, hourTo = 18.75, summitX = lv.length * 0.95, breath = 1, weather = "clear",
                };
            }
            else
            {
                init.run = new RunSave
                {
                    cycles = level * 3, lv = lv.id, phase = "playing", x = lv.length * 0.3, checkpoint = lv.length * 0.3 - 10,
                    maxX = lv.length * 0.3, breath = 1, weather = "clear",
                };
            }
            var e = new SisyphusEngine(screen, 0, init);
            e.hostW = w; e.hostH = h; e.hostDpr = 1;
            e.start();
            e.activate();
            var upd = typeof(SisyphusEngine).GetMethod("update", BF);
            var ren = typeof(SisyphusEngine).GetMethod("render", BF);
            // no story stops in a test
            e.cutscene = false;
            double dt = 1.0 / 30;
            int warmN = (int)(warm * 30);
            var trace = new System.Text.StringBuilder();
            for (int i = 0; i < warmN + frames; i++)
            {
                if (mode == "push") { e.input = 1; S(e, "breath", 1.0); }
                else e.input = 1;
                if (mode == "descent") { S(e, "walkFrom", lv.length * 0.9); }
                upd.Invoke(e, new object[] { dt });
                ren.Invoke(e, null);
                Work.Pump();
                be.skip = i < warmN - 3;
                Gfx.Flush();
                if (i >= warmN)
                {
                    int k = i - warmN;
                    be.SavePng(screen, Path.Combine(outDir, string.Format("g{0:000}.png", k)));
                    trace.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0} phase={1} hipSm={2:0.00} gait={3:0.000} walkV={4:0.0} walkX={5:0.0} x={6:0.0} vx={7:0.0}",
                        k, F(e, "phase"), F(e, "hipSm"), F(e, "gait"), F(e, "walkV"), F(e, "walkX"), F(e, "x"), F(e, "vx")));
                }
            }
            File.WriteAllText(Path.Combine(outDir, "trace.txt"), trace.ToString());
        }
    }
}
