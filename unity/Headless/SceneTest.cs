using System;
using System.IO;
using System.Reflection;
using Sisifos.Game;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>a story scene's camera: "scene out.png speaker level frac" — the man at
    /// `frac` of the hill, `speaker` (pro, tan, merope, sis) talking, after 4 s to settle</summary>
    public static class SceneTest
    {
        const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        public static void Run(CpuBackend be, string outFile, string speaker, int level, double frac)
        {
            Data.loader = p => File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            Work.threaded = false;
            Js.Seed(3);
            int w = 900, h = 414;
            var screen = new Surface(w * 2, h * 2, true, "screen");
            var lv = Levels.getLevel(level);
            var init = new EngineInit { cycles = 12, tutorialDone = true };
            init.run = new RunSave
            {
                cycles = 12, lv = lv.id, phase = "playing", x = lv.length * frac, checkpoint = lv.length * frac - 10,
                maxX = lv.length * frac, breath = 1, weather = "clear",
            };
            var e = new SisyphusEngine(screen, 0, init);
            e.hostW = w; e.hostH = h; e.hostDpr = 2;
            e.start();
            e.activate();
            var upd = typeof(SisyphusEngine).GetMethod("update", BF);
            var ren = typeof(SisyphusEngine).GetMethod("render", BF);
            int n = 120;
            for (int i = 0; i < n; i++)
            {
                e.cutscene = true;
                e.speaker = speaker;
                upd.Invoke(e, new object[] { 1.0 / 30 });
                ren.Invoke(e, null);
                Work.Pump();
                be.skip = i < n - 3;
                Gfx.Flush();
            }
            foreach (var f in new[] { "camX", "camY", "focusX", "focusY", "frameOffX", "frameOffY", "zoom", "camLift", "x", "scale" })
                Console.WriteLine(f + "=" + typeof(SisyphusEngine).GetField(f, BF)?.GetValue(e));
            Console.WriteLine("len=" + lv.length);
            be.SavePng(screen, outFile);
        }
    }
}
