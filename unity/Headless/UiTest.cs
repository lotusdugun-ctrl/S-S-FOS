using System;
using Sisifos.App.UI;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>"ui out.png": the menu's antique ornaments (frame, rule, tablet, frieze) on the dark of the menu</summary>
    public static class UiTest
    {
        public static void Run(CpuBackend be, string outFile)
        {
            int W = 900, H = 414, k = 2;
            var s = new Surface(W * k, H * k, true, "ui");
            var c = s.GetContext();
            c.SetTransform(k, 0, 0, k, 0, 0);
            var g = c.CreateRadialGradient(W / 2, H * 0.45, 0, W / 2, H * 0.45, W * 0.6);
            g.AddColorStop(0, "#2b2430"); g.AddColorStop(1, "#0c0a10");
            c.FillStyle = g; c.FillRect(0, 0, W, H);
            Antique.FramePaint(c, W, H, 3.7);
            c.Save(); c.Translate(W / 2 - 150, 120); Antique.Rule(c, 300, 14, 1, 2.3); c.Restore();
            c.Save(); c.Translate(W / 2 - 80, 200); Antique.Tablet(c, 160, 44, 0, 0.8, 5.1); c.Restore();
            c.Save(); c.Translate(W / 2 - 80, 260); Antique.Tablet(c, 160, 44, 1, 0, 5.1); c.Restore();
            var fz = new Surface(W * k, 44 * k, true, "fz");
            var fc = fz.GetContext(); fc.SetTransform(k, 0, 0, k, 0, 0);
            Antique.Frieze(fc, W, 44, 3.3, 0.85);
            c.SetTransform(1, 0, 0, 1, 0, 0);
            c.DrawImage(fz, 0, (H - 82) * k);
            Gfx.Flush();
            be.SavePng(s, outFile);
        }
    }
}
