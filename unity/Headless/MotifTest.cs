using System;
using System.IO;
using System.Reflection;
using Sisifos.Game;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>
    /// "motifs out.png k t": the far-ridge figures (Engine.Life.cs) drawn large on a plain sky,
    /// each in its silhouette colour, at time t — to look at their drawing up close.
    /// </summary>
    public static class MotifTest
    {
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void Run(CpuBackend be, string outFile, double k, double t)
        {
            Data.loader = p => File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            Work.threaded = false;
            Js.Seed(3);
            var tmp = new Surface(320, 200, true, "tmp");
            var lv = Levels.getLevel(0);
            var e = new SisyphusEngine(tmp, 0, new EngineInit { cycles = 0, tutorialDone = true });
            e.hostW = 320; e.hostH = 200; e.hostDpr = 1;
            e.start();
            e.activate();
            e.cutscene = false;
            var upd = typeof(SisyphusEngine).GetMethod("update", BF);
            var ren = typeof(SisyphusEngine).GetMethod("render", BF);
            for (int i = 0; i < 3; i++) { upd.Invoke(e, new object[] { 1.0 / 30 }); ren.Invoke(e, null); Work.Pump(); }
            typeof(SisyphusEngine).GetField("t", BF).SetValue(e, t);
            typeof(SisyphusEngine).GetField("landAir", BF).SetValue(e, new RGB(150, 130, 160));
            string[] names = { "drawTemple", "drawAtlas2", "drawGate", "drawColossus", "drawCentaur2", "drawCerberus2", "drawBattle" };
            int cols = 4, rows = 2;
            int cw = (int)(180 * k), ch = (int)(170 * k);
            var screen = new Surface(cw * cols, ch * rows, true, "screen");
            var c = screen.GetContext();
            var g = c.CreateLinearGradient(0, 0, 0, ch * rows);
            g.AddColorStop(0, "#c98f6a");
            g.AddColorStop(1, "#7d6f95");
            c.FillStyle = g;
            c.FillRect(0, 0, cw * cols, ch * rows);
            for (int i = 0; i < names.Length; i++)
            {
                var m = typeof(SisyphusEngine).GetMethod(names[i], BF);
                if (m == null) continue;
                double x = (i % cols + 0.5) * cw, y = (i / cols + 1) * ch - 14 * k;
                c.Save();
                c.FillStyle = "rgba(52, 40, 58, 0.95)";
                c.StrokeStyle = "rgba(52, 40, 58, 0.95)";
                c.FillRect(x - cw * 0.45, y, cw * 0.9, 3 * k);
                var ps = m.GetParameters();
                object last = ps[4].Name == "dir" ? (object)1.0 : 0.37;
                if (ps.Length == 5) m.Invoke(e, new object[] { c, x, y, k, last });
                c.Restore();
            }
            Gfx.Flush();
            be.SavePng(screen, outFile);
        }
    }
}
