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
                default: Console.Error.WriteLine("unknown mode " + mode); return 1;
            }
            return 0;
        }
    }
}
