using System;
using System.IO;
using System.Reflection;
using Sisifos.Game;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>"life out.png k hour": the ridge-life scenes (hamlet, flock, traveller, water carriers, plough) on a flat ridge</summary>
    public static class LifeTest
    {
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        public static void Run(CpuBackend be, string outFile, double k, double hour)
        {
            Data.loader = p => File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            Work.threaded = false;
            Js.Seed(3);
            int cw = (int)(150 * k), ch = (int)(70 * k);
            var screen = new Surface(cw * 5, ch, true, "screen");
            var e = new SisyphusEngine(screen, 0, new EngineInit { cycles = 0, tutorialDone = true });
            e.hostW = cw * 5; e.hostH = ch; e.hostDpr = 1;
            e.start(); e.activate(); e.cutscene = false;
            var upd = typeof(SisyphusEngine).GetMethod("update", BF);
            var ren = typeof(SisyphusEngine).GetMethod("render", BF);
            for (int i = 0; i < 3; i++)
            {
                typeof(SisyphusEngine).GetField("hour", BF).SetValue(e, hour);
                upd.Invoke(e, new object[] { 1.0 / 30 });
                typeof(SisyphusEngine).GetField("hour", BF).SetValue(e, hour);
                ren.Invoke(e, null); Work.Pump();
            }
            typeof(SisyphusEngine).GetField("t", BF).SetValue(e, 7.7);
            var st = (SkyState)typeof(SisyphusEngine).GetField("st", BF).GetValue(e);
            var c = screen.GetContext();
            c.SetTransform(1, 0, 0, 1, 0, 0);
            var sky = c.CreateLinearGradient(0, 0, 0, ch);
            for (int i = 0; i < st.ramp.Length; i++) { var q = st.ramp[i]; sky.AddColorStop((double)i / (st.ramp.Length - 1), Css.rgb(new RGB(q.r, q.g, q.b), 1)); }
            c.FillStyle = sky; c.FillRect(0, 0, cw * 5, ch);
            double gy0 = ch - 10 * k;
            c.FillStyle = "#6e5a4c"; c.FillRect(0, gy0, cw * 5, ch);
            var hz = st.ramp[st.ramp.Length - 1];
            void set(string f, object v) { typeof(SisyphusEngine).GetField(f, BF).SetValue(e, v); }
            set("farHaze", new RGB(hz.r, hz.g, hz.b)); set("farAir", 0.16); set("farDark", 0.7); set("farLit", true);
            set("lifeRim", Col.Rgba(255, 220, 170, 0.5)); set("lifeLit", 1.0);
            Func<double, double> ridge = x => gy0;
            string[] names = { "lifeHamlet", "lifeFlock", "lifeTraveller", "lifeWaterCarriers", "lifePlough" };
            for (int i = 0; i < 5; i++)
            {
                var m = typeof(SisyphusEngine).GetMethod(names[i], BF);
                double x = (i + 0.5) * cw;
                var ps = m.GetParameters();
                object[] a = ps.Length == 6
                    ? (names[i] == "lifeHamlet" ? new object[] { c, ridge, x, k, 0.37, 0.0 } : new object[] { c, ridge, x, k, 0.37, 120.0 * k })
                    : new object[] { c, ridge, x, k, 0.37 };
                m.Invoke(e, a);
            }
            Gfx.Flush();
            be.SavePng(screen, outFile);
        }
    }
}
