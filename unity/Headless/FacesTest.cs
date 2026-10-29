using System;
using Sisifos.Game;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>
    /// The companions on a plain ground: "faces out.png k" the three of them at scale k;
    /// "face who out.png k" one head, close up (who: pro, tan, merope, sis).
    /// </summary>
    public static class FacesTest
    {
        static readonly FigLight L = new FigLight { color = new RGB(255, 214, 170), strength = 0.8, lx = -0.6, ly = -0.8, t = 3.3, dim = 0, wind = 1 };

        public static void Run(CpuBackend be, string outFile, double k)
        {
            Data.loader = p => System.IO.File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            int W = (int)(1200 * k / 4), H = (int)(700 * k / 4);
            var screen = new Surface(W, H, true, "screen");
            var c = screen.GetContext();
            c.FillStyle = "#7d8fb0";
            c.FillRect(0, 0, W, H);
            Figures.drawPrometheus(c, W * 0.18, H * 0.92, k, L);
            Figures.drawTantalus(c, W * 0.5, H * 0.92, k, L);
            Figures.drawMerope(c, W * 0.8, H * 0.92, k, new MeropeOpts { act = 2, L = L });
            Gfx.Flush();
            be.SavePng(screen, outFile);
        }

        /// <summary>"eagle out.png k phase t": Prometheus at his rock with the eagle (phase: arrive, feed, leave)</summary>
        public static void Eagle(CpuBackend be, string outFile, double k, string phase, double et)
        {
            Data.loader = p => System.IO.File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            int W = (int)(170 * k), H = (int)(175 * k);
            var screen = new Surface(W, H, true, "screen");
            var c = screen.GetContext();
            c.FillStyle = "#7d8fb0";
            c.FillRect(0, 0, W, H);
            var l = new FigLight { color = L.color, strength = L.strength, lx = L.lx, ly = L.ly, t = 3.3 + et, dim = 0, wind = 1 };
            Figures.drawPrometheus(c, W * 0.45, H * 0.94, k, l, new EagleVisit(phase, et));
            Gfx.Flush();
            be.SavePng(screen, outFile);
        }

        public static void Face(CpuBackend be, string who, string outFile, double k)
        {
            Data.loader = p => System.IO.File.ReadAllText("/home/claude/sisifos/unity/Assets/Resources/Data/" + p + ".json");
            int S = 600;
            var screen = new Surface(S, S, true, "screen");
            var c = screen.GetContext();
            c.FillStyle = "#7d8fb0";
            c.FillRect(0, 0, S, S);
            // where the head is, in units of the figure's scale from its ground point
            double hx = who == "pro" ? -5 : who == "tan" ? -9 : -0.5, hy = who == "pro" ? -68.5 : who == "tan" ? -38.5 : -70;
            double x = S / 2 - hx * k, y = S / 2 - hy * k;
            if (who == "pro") Figures.drawPrometheus(c, x, y, k, L);
            else if (who == "tan") Figures.drawTantalus(c, x, y, k, L);
            else Figures.drawMerope(c, x, y, k, new MeropeOpts { act = 2, L = L });
            Gfx.Flush();
            be.SavePng(screen, outFile);
        }
    }
}
