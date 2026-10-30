using System;
using System.IO;
using System.Reflection;
using Sisifos.Game;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>
    /// "far out.png k hour [t]": every far-ridge figure painted as the game paints it now —
    /// in colour, lit by the sun at that hour — in a row on the game's own sky, to look at
    /// them up close. Top row the far ridge, bottom row the near one.
    /// </summary>
    public static partial class FarTest
    {
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void Run(CpuBackend be, string outFile, double k, double hour, double t)
        {
            Data.loader = p => File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            Work.threaded = false;
            Js.Seed(3);
            int cw = (int)(200 * k), ch = (int)(215 * k);
            int cols = 4, rows = 2;
            var screen = new Surface(cw * cols, ch * rows, true, "screen");
            var e = new SisyphusEngine(screen, 0, new EngineInit { cycles = 0, tutorialDone = true });
            e.hostW = cw * cols; e.hostH = ch * rows; e.hostDpr = 1;
            e.start(); e.activate();
            e.cutscene = false;
            var upd = typeof(SisyphusEngine).GetMethod("update", BF);
            var ren = typeof(SisyphusEngine).GetMethod("render", BF);
            for (int i = 0; i < 3; i++)
            {
                typeof(SisyphusEngine).GetField("hour", BF).SetValue(e, hour);
                typeof(SisyphusEngine).GetField("baseHour", BF)?.SetValue(e, hour);
                upd.Invoke(e, new object[] { 1.0 / 30 });
                typeof(SisyphusEngine).GetField("hour", BF).SetValue(e, hour);
                ren.Invoke(e, null); Work.Pump();
            }
            typeof(SisyphusEngine).GetField("t", BF).SetValue(e, t);
            var st = (SkyState)typeof(SisyphusEngine).GetField("st", BF).GetValue(e);
            var hzc = st.ramp[st.ramp.Length - 1];
            var hz = new RGB(hzc.r, hzc.g, hzc.b);
            typeof(SisyphusEngine).GetField("landAir", BF).SetValue(e, hz);
            var c = screen.GetContext();
            c.SetTransform(1, 0, 0, 1, 0, 0);
            // the game's own sky behind them, then a plain band of ridge under each row
            var sky = c.CreateLinearGradient(0, 0, 0, ch * rows);
            for (int i = 0; i < st.ramp.Length; i++)
            {
                var q = st.ramp[i];
                sky.AddColorStop((double)i / (st.ramp.Length - 1), Css.rgb(new RGB(q.r, q.g, q.b), 1));
            }
            c.FillStyle = sky;
            c.FillRect(0, 0, cw * cols, ch * rows);
            var m = typeof(SisyphusEngine).GetMethod("paintFarFigure", BF);
            for (int i = 0; i < 7; i++)
            {
                for (int layer = 0; layer < 2; layer++)
                {
                    int col = i % cols, row = layer;
                    if (i >= cols) { col = i - cols + (layer == 0 ? 0 : 0); }
                }
            }
            for (int layer = 0; layer < 2; layer++)
            {
                double dk = layer == 0 ? 0.55 : 0.7;
                var rc = Css.rgb(new RGB(hz.r * (1 - dk) + 26 * dk, hz.g * (1 - dk) + 22 * dk, hz.b * (1 - dk) + 36 * dk), 1);
                double gy = (layer + 1) * ch - 18 * k;
                c.FillStyle = rc;
                c.FillRect(0, gy, cw * cols, ch * rows);
            }
            int[] order = { 1, 4, 5, 6, 0, 2, 3 };
            for (int j = 0; j < order.Length; j++)
            {
                int kind = order[j];
                int layer = j < 4 ? 0 : 1;
                double x = ((j % 4) + 0.5) * cw + (kind == 6 ? 0 : 0);
                double gy = (layer + 1) * ch - 18 * k;
                double kk = k * (layer == 0 ? 0.95 : 1.15) / 1.15;
                m.Invoke(e, new object[] { c, kind, x, gy, kk, 0.37, 1.0, layer, hz, layer == 0 ? 0.55 : 0.7, j % 3 });
            }
            Gfx.Flush();
            be.SavePng(screen, outFile);
        }
    }
}
