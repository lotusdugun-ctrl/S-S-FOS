// engine.ts' RunStats, defined here because seals.ts (Seals.cs) needs it and the engine port
// came later. PORT: the Engine port uses this type and must not declare its own.
namespace Sisifos.Game
{
    /// <summary>one climb, summed up at its summit</summary>
    public sealed class RunStats
    {
        /// <summary>distinguishes one summit from the next</summary>
        public double id;
        public double seconds;
        public double slips;
        public double rests;
        public double maxStreak;
        public bool night;
        /// <summary>Weather: "clear" | "rain" | "storm"</summary>
        public string weather;
        public string levelId;
        public bool calm;
    }
}
