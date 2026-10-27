using System;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    /// <summary>The same scene as /tmp/claude-0/ucore/scene.js, drawn through the C# context.</summary>
    public static class CoreTest
    {
        public static void Run(CpuBackend be, string outFile)
        {
            var screen = new Surface(400, 300, true, "screen");
            var c = screen.GetContext();
            Scene(c);
            Gfx.Flush();
            be.SavePng(screen, outFile);
        }

        // the scene itself lives in Assets/Scripts/App/TestScene.cs, shared with the Unity host
        static void Scene(Ctx c) { Sisifos.App.TestScene.Draw(c); }
    }
}
