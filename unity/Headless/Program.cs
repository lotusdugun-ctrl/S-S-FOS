using System;
using Sisifos.Paint;

namespace Sisifos.Headless
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var be = new CpuBackend();
            Gfx.backend = be;
            string mode = args.Length > 0 ? args[0] : "core";
            switch (mode)
            {
                case "core": CoreTest.Run(be, args.Length > 1 ? args[1] : "core.png"); break;
                case "play":
                    PlayTest.Run(be, args[1], int.Parse(args[2]), int.Parse(args[3]), double.Parse(args[4]), double.Parse(args[5]), double.Parse(args[6]));
                    break;
                case "gait":
                    GaitTest.Run(be, args[1], args[2], int.Parse(args[3]), int.Parse(args[4]), double.Parse(args[5]), int.Parse(args[6]), int.Parse(args[7]));
                    break;
                case "face": FacesTest.Face(be, args[1], args[2], double.Parse(args[3])); break;
                case "eagle": FacesTest.Eagle(be, args[1], double.Parse(args[2]), args[3], double.Parse(args[4])); break;
                case "motifs": MotifTest.Run(be, args[1], double.Parse(args[2]), double.Parse(args[3])); break;
                case "scene": SceneTest.Run(be, args[1], args[2], int.Parse(args[3]), double.Parse(args[4])); break;
                case "faces": FacesTest.Run(be, args[1], args.Length > 2 ? double.Parse(args[2]) : 4); break;
                default: Console.Error.WriteLine("unknown mode " + mode); return 1;
            }
            return 0;
        }
    }
}
