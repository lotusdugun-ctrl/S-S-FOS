using System;
using System.IO;
using System.Reflection;
using Sisifos.Game;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>"bbox": the extent of each far-ridge figure at k = 1, over a few moments</summary>
    public static class BBoxTest
    {
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        public static void Run(CpuBackend be)
        {
            Data.loader = p => File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            Work.threaded = false;
            Js.Seed(3);
            var tmp = new Surface(320, 200, true, "tmp");
            var e = new SisyphusEngine(tmp, 0, new EngineInit { cycles = 0, tutorialDone = true });
            e.hostW = 320; e.hostH = 200; e.hostDpr = 1;
            e.start(); e.activate();
            var upd = typeof(SisyphusEngine).GetMethod("update", BF);
            var ren = typeof(SisyphusEngine).GetMethod("render", BF);
            for (int i = 0; i < 3; i++) { upd.Invoke(e, new object[] { 1.0 / 30 }); ren.Invoke(e, null); Work.Pump(); }
            typeof(SisyphusEngine).GetField("landAir", BF).SetValue(e, new RGB(150, 130, 160));
            typeof(SisyphusEngine).GetField("farHaze", BF).SetValue(e, new RGB(150, 130, 160));
            typeof(SisyphusEngine).GetField("farLit", BF).SetValue(e, true);
            string[] names = { "drawAtlas2", "drawCentaur2", "drawCerberus2", "drawBattle", "drawHeracles", "drawTrojanHorse" };
            int W = 1000, H = 500, ox = 500, oy = 400;
            var bb = new Surface(W, H, true, "bb");
            foreach (var n in names)
            {
                var m = typeof(SisyphusEngine).GetMethod(n, BF);
                int x0 = W, y0 = H, x1 = 0, y1 = 0;
                foreach (double t in new[] { 0.3, 2.1, 4.7, 7.9, 10.4, 13.3 })
                foreach (double last in new[] { 1.0, -1.0, 0.37, 0.81 })
                {
                    typeof(SisyphusEngine).GetField("t", BF).SetValue(e, t);
                    var c = bb.GetContext();
                    c.SetTransform(1, 0, 0, 1, 0, 0); c.ClearRect(0, 0, W, H);
                    c.FillStyle = "#000"; c.StrokeStyle = "#000";
                    var ps = m.GetParameters();
                    double a = ps[4].Name == "dir" ? (last > 0.5 || last < 0 ? Math.Sign(last) : 1) : Math.Abs(last);
                    m.Invoke(e, new object[] { c, (double)ox, (double)oy, 1.0, a });
                    Gfx.Flush();
                    var px = be.ReadPixels(bb, out int w, out int h);
                    for (int yy = 0; yy < h; yy++) for (int xx = 0; xx < w; xx++)
                        if (px[(yy * w + xx) * 4 + 3] > 8) { if (xx < x0) x0 = xx; if (xx > x1) x1 = xx; if (yy < y0) y0 = yy; if (yy > y1) y1 = yy; }
                }
                Console.WriteLine(n + " l=" + (x0 - ox) + " t=" + (y0 - oy) + " r=" + (x1 - ox) + " b=" + (y1 - oy));
            }
        }
    }
}
