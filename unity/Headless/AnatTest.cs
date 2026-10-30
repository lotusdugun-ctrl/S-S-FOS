using System;
using System.IO;
using System.Reflection;
using Sisifos.Game;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>"anat out.png t": people and horses from Engine.Anatomy, large, lit as far figures</summary>
    public static class AnatTest
    {
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        public static void Run(CpuBackend be, string outFile, double t)
        {
            Data.loader = p => File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            Work.threaded = false;
            Js.Seed(3);
            int W = 1600, H = 520;
            var screen = new Surface(W, H, true, "screen");
            var e = new SisyphusEngine(screen, 0, new EngineInit { cycles = 0, tutorialDone = true });
            e.hostW = W; e.hostH = H; e.hostDpr = 1;
            e.start(); e.activate(); e.cutscene = false;
            var upd = typeof(SisyphusEngine).GetMethod("update", BF);
            var ren = typeof(SisyphusEngine).GetMethod("render", BF);
            for (int i = 0; i < 2; i++) { typeof(SisyphusEngine).GetField("hour", BF).SetValue(e, 16.5); upd.Invoke(e, new object[] { 1.0 / 30 }); ren.Invoke(e, null); Work.Pump(); }
            typeof(SisyphusEngine).GetField("t", BF).SetValue(e, t);
            var c = screen.GetContext();
            c.SetTransform(1, 0, 0, 1, 0, 0);
            c.FillStyle = "#9fb4d0"; c.FillRect(0, 0, W, H);
            c.FillStyle = "#6e5a4c"; c.FillRect(0, 230, W, 300); c.FillRect(0, 490, W, 40);
            void set(string f, object v) { typeof(SisyphusEngine).GetField(f, BF).SetValue(e, v); }
            set("farHaze", new RGB(160, 170, 190)); set("farAir", 0.1); set("farDark", 0.3); set("farLit", true);
            var person = typeof(SisyphusEngine).GetMethod("person", BF);
            var gait = typeof(SisyphusEngine).GetMethod("Gait", BindingFlags.Static | BindingFlags.NonPublic);
            var skin = typeof(SisyphusEngine).GetMethod("Skin", BindingFlags.Static | BindingFlags.NonPublic);
            var horse = typeof(SisyphusEngine).GetMethod("horse", BF);
            string[] looks = { "bare", "tunic", "robe", "hoplite" };
            for (int i = 0; i < 8; i++)
            {
                double g = t * 5 + i * 0.8;
                object pose = gait.Invoke(null, new object[] { g, i < 4 ? 0.0 : 1.0, 0.0 });
                object lk = skin.Invoke(null, new object[] { 176.0, 122.0, 88.0 });
                var lkT = lk.GetType();
                int d = i % 4;
                object boxed = lk;
                lkT.GetField("dress").SetValue(boxed, d == 0 ? 0 : d == 2 ? 2 : 1);
                lkT.GetField("helmet").SetValue(boxed, d == 3);
                lkT.GetField("beard").SetValue(boxed, d == 1);
                lkT.GetField("woman").SetValue(boxed, d == 2);
                lkT.GetField("cloak").SetValue(boxed, d == 3);
                person.Invoke(e, new object[] { c, 90.0 + i * 190, 228.0, 180.0, 1.0, pose, boxed, null });
            }
            var hl = Activator.CreateInstance(horse.GetParameters()[7].ParameterType);
            var hT = hl.GetType();
            foreach (var (f, v) in new (string, object)[] { ("r", 120.0), ("g", 78.0), ("b", 50.0), ("maneR", 40.0), ("maneG", 30.0), ("maneB", 26.0), ("head", true) }) hT.GetField(f).SetValue(hl, v);
            for (int i = 0; i < 3; i++)
                horse.Invoke(e, new object[] { c, 260.0 + i * 520, 490.0, 210.0, 1.0, i, t * 6 + i, hl });
            Gfx.Flush();
            be.SavePng(screen, outFile);
        }
    }
}
