using System;
using Sisifos.Paint;

namespace Sisifos.App
{
    /// <summary>
    /// The canvas test scene (the same as /tmp/claude-0/ucore/scene.js), shared by the headless
    /// harness (Headless/CoreTest.cs, CPU backend) and the Unity host (GameRoot, GPU backend) so
    /// both backends can be compared on the same frame. Pure C#.
    /// </summary>
    public static class TestScene
    {
        // the scene's two offscreen canvases; resizing clears them exactly as a new canvas starts
        static Surface L, M;

        /// <summary>Draw the 400×300 scene with whatever transform the context has.</summary>
        public static void Draw(Ctx c)
        {
            var g = c.CreateLinearGradient(0, 0, 0, 300);
            g.AddColorStop(0, "oklch(0.3 0.08 270)"); g.AddColorStop(0.6, "#f29a55"); g.AddColorStop(1, "rgba(255,220,180,1)");
            c.FillStyle = g; c.FillRect(0, 0, 400, 300);
            var r = c.CreateRadialGradient(300, 80, 4, 290, 90, 60);
            r.AddColorStop(0, "rgba(255,250,220,1)"); r.AddColorStop(0.3, "rgba(255,200,120,0.6)"); r.AddColorStop(1, "rgba(255,160,80,0)");
            c.FillStyle = r; c.BeginPath(); c.Arc(300, 80, 70, 0, Math.PI * 2); c.Fill();
            c.FillStyle = "oklch(0.35 0.05 40)";
            c.BeginPath(); c.MoveTo(0, 300); c.LineTo(0, 220); c.QuadraticCurveTo(120, 140, 220, 200); c.BezierCurveTo(260, 230, 330, 150, 400, 180); c.LineTo(400, 300); c.ClosePath(); c.Fill();
            c.Save(); c.Translate(120, 120); c.Rotate(0.5); c.Scale(1.5, 1);
            c.FillStyle = "rgba(40,60,90,0.7)"; c.BeginPath(); c.Ellipse(0, 0, 30, 14, 0.2, 0, Math.PI * 2); c.Fill();
            c.Restore();
            c.StrokeStyle = "rgba(255,255,255,0.5)"; c.LineWidth = 8; c.LineCap = LineCap.Round; c.LineJoin = LineJoin.Round;
            c.BeginPath(); c.MoveTo(30, 40); c.QuadraticCurveTo(90, 10, 140, 60); c.LineTo(160, 20); c.Stroke();
            c.LineCap = LineCap.Butt; c.LineJoin = LineJoin.Miter; c.StrokeStyle = "#2459ad"; c.LineWidth = 3;
            c.BeginPath(); c.MoveTo(200, 30); c.LineTo(240, 60); c.LineTo(200, 90); c.Stroke();
            c.SetLineDash(new double[] { 6, 4 }); c.StrokeStyle = "rgba(0,0,0,0.6)"; c.LineWidth = 2;
            c.BeginPath(); c.Arc(60, 240, 30, 0, Math.PI * 1.5); c.Stroke(); c.SetLineDash(null);
            if (L == null) L = new Surface(120, 120); else L.SetSize(120, 120);
            var lc = L.GetContext();
            lc.FillStyle = "oklch(0.5 0.085 50)"; lc.BeginPath(); lc.Arc(60, 60, 45, 0, Math.PI * 2); lc.Fill();
            lc.GlobalCompositeOperation = Composite.SourceAtop; lc.FillStyle = "rgba(255,255,255,0.4)"; lc.BeginPath(); lc.Arc(80, 40, 40, 0, Math.PI * 2); lc.Fill();
            lc.GlobalCompositeOperation = Composite.DestinationOut; lc.BeginPath(); lc.Arc(30, 90, 20, 0, Math.PI * 2); lc.Fill();
            lc.GlobalCompositeOperation = Composite.SourceOver;
            c.DrawImage(L, 250, 170, 100, 100);
            c.GlobalCompositeOperation = Composite.Lighter; c.FillStyle = "rgba(120,40,10,0.8)"; c.BeginPath(); c.Arc(330, 230, 25, 0, Math.PI * 2); c.Fill();
            c.GlobalCompositeOperation = Composite.SourceOver;
            c.Save(); c.ShadowColor = "rgba(255,200,80,0.9)"; c.ShadowBlur = 12; c.FillStyle = "#fff"; c.BeginPath(); c.MoveTo(160, 150); c.LineTo(175, 180); c.LineTo(150, 180); c.ClosePath(); c.Fill(); c.Restore();
            c.Save(); c.BeginPath(); c.Arc(80, 160, 28, 0, Math.PI * 2); c.Clip();
            c.FillStyle = "rgba(20,120,40,1)"; c.FillRect(50, 150, 60, 40); c.Restore();
            if (M == null) M = new Surface(80, 80); else M.SetSize(80, 80);
            var mc = M.GetContext();
            mc.FillStyle = "#000"; mc.BeginPath(); mc.Ellipse(40, 40, 35, 20, 0, 0, Math.PI * 2); mc.Fill();
            mc.GlobalCompositeOperation = Composite.SourceIn; mc.FillStyle = "rgba(200,50,90,0.8)"; mc.FillRect(0, 0, 50, 80);
            c.DrawImage(M, 180, 220);
        }

        /// <summary>A text line under the scene (GPU host only: the CPU backend draws text as bars).</summary>
        public static void DrawText(Ctx c, double t)
        {
            c.Font = "italic 500 22px 'Cormorant Garamond', Georgia, serif";
            c.TextAlign = TextAlign.Center;
            c.TextBaseline = TextBaseline.Middle;
            c.FillStyle = "rgba(255,245,230,0.95)";
            c.FillText("The struggle itself toward the heights", 200, 330);
            c.Font = "600 18px Cinzel, serif";
            c.FillStyle = "#f2c38a";
            c.FillText("SISYPHUS · سيزيف · सिसिफ़स · সিসিফাস", 200, 362);
            c.Save();
            c.Translate(200, 395); c.Rotate(Math.Sin(t) * 0.05);
            c.Font = "16px sans-serif"; c.FillStyle = "rgba(255,255,255,0.7)";
            c.FillText("measureText: " + Js.toFixed(c.MeasureText("measureText"), 1) + "px", 0, 0);
            c.Restore();
        }
    }
}
