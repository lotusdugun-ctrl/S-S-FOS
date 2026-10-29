// Ported from src/game/engine.ts, lines 1–1786 (part A): module types and constants,
// strideFor / weatherFor, and the SisyphusEngine class from its start through
// renderParticles. The rest of the class is in Engine.Render.cs (B), Engine.Scene.cs (C)
// and Engine.Figure.cs (D).
//
// PORT: no requestAnimationFrame, window or DOM. The Unity host sets hostW / hostH (CSS
// pixels) and hostDpr (devicePixelRatio) before resize(), calls start() once, then
// frame(nowMs) every frame with a timestamp on the Js.now() clock (performance.now()).
// Work.Pump() is called by the host, not here.
using System;
using System.Collections.Generic;
using Sisifos.Paint;
using Sisifos.Audio;

namespace Sisifos.Game
{
    /// <summary>Phase: "playing" | "summit" | "rolling" | "descent"</summary>
    public sealed class EngineState
    {
        /// <summary>"playing" | "summit" | "rolling" | "descent"</summary>
        public string phase;
        /// <summary>0..1</summary>
        public double progress;
        public string levelName;
        public string epigraph;
        public double cycles;
        /// <summary>seconds spent climbing in the current cycle (summit to summit, cinematics excluded)</summary>
        public double climbTime;
        /// <summary>the finished cycle's climb time, 0 until the first summit</summary>
        public double lastClimb;
        /// <summary>fastest climb ever, 0 if none</summary>
        public double bestClimb;
        /// <summary>true when lastClimb set a new best</summary>
        public bool newBest;
        /// <summary>a one-line nudge when the player is stuck on something the game can explain:
        /// "push" | "rhythm" | "breathe" | "kick" | "rest", or null</summary>
        public string hint;
        /// <summary>the weather on this climb: "clear" | "rain" | "storm"</summary>
        public string weather;
        /// <summary>the hill being climbed, by level id</summary>
        public string levelId;
        /// <summary>the first climb's lessons have all been seen</summary>
        public bool tutorialDone;
        /// <summary>how the last finished climb went, for the seals; null before the first summit</summary>
        public RunStats lastRun;
        /// <summary>indices of every aphorism found so far, in the order they were found</summary>
        public List<int> unlocked;
        /// <summary>the aphorism just found at this summit, -1 when it was one already known</summary>
        public double newQuote;
    }

    public sealed class EngineInit
    {
        /// <summary>completed cycles carried over from a previous visit</summary>
        public double? cycles;
        /// <summary>fastest climb carried over from a previous visit, in seconds</summary>
        public double? bestClimb;
        /// <summary>aphorisms already found on earlier visits</summary>
        public List<int> unlocked;
        /// <summary>vibration on/off</summary>
        public bool? haptics;
        /// <summary>no breath to run out of: the climb as a meditation</summary>
        public bool? calm;
        /// <summary>the first climb's lessons were seen on an earlier visit</summary>
        public bool? tutorialDone;
        /// <summary>the story so far</summary>
        public SagaState saga;
        /// <summary>a climb left half-done when the game was closed</summary>
        public RunSave run;
    }

    public partial class SisyphusEngine
    {
        /// <summary>
        /// The weather for a cycle: mostly clear, sometimes rain, fog or a storm. Fixed
        /// by the cycle number, so a cycle replayed after a reload has the same sky. The
        /// first two climbs are always clear — there is enough to learn already.
        /// </summary>
        public static string weatherFor(double cycles)
        {
            if (cycles < 2) return "clear";
            double r = Math.Abs(Math.Sin(cycles * 12.9898 + 78.233) * 43758.5453) % 1;
            return r < 0.7 ? "clear" : r < 0.88 ? "rain" : "storm";
        }

        // Stronger than it was (620): on a phone players found the stone crawled and the
        // only way up a ledge was to throw it. A pitch still wants the rhythm of the
        // heaves or a throw to go up it at any speed, but it can be pushed.
        internal const double PUSH_FORCE = 760;
        internal const double GRAVITY = 900;
        internal const double FRICTION = 1.6;
        internal const double ROLL_GRAVITY = 1500;
        /// <summary>boulder launch velocity (units/s) when the character kicks it loose</summary>
        internal const double KICK_V = 560;
        /// <summary>character free-run speed (units/s) while the boulder is away</summary>
        internal const double KICK_SPRINT = 220;

        /*
         * Breath.
         *
         * Holding the key used to be the whole game: a minute of one finger down and
         * nothing to decide. Now pushing costs breath and letting go — bracing — gives
         * it back without giving ground, so the climb is a rhythm of effort and rest.
         * Run out mid-push and his grip goes: the stone slides back until he catches it.
         */
        /// <summary>breath spent per second of full push on level ground; scales up with the slope</summary>
        internal const double BREATH_DRAIN = 0.06;
        /// <summary>breath recovered per second while braced</summary>
        internal const double BREATH_REGEN = 0.3;
        /// <summary>how long he cannot hold the stone after running out of breath, in seconds</summary>
        internal const double SLIP_TIME = 1.3;
        /// <summary>breath a launch costs; below this he cannot launch at all</summary>
        internal const double KICK_COST = 0.3;

        /*
         * Heaves. Every fresh press of the push key is a heave, and a heave timed to
         * the beat — the ring closing over his head — lands with real force and gives
         * a little breath back. Mashing lands off the beat and only tires him. Holding
         * the key down still works; it is just the expensive way up.
         */
        /// <summary>seconds between beats; close to his stride when he is working hard</summary>
        internal const double BEAT = 0.75;

        /// <summary>summit cinematic: when the bolt is thrown, and when the stone starts down</summary>
        internal const double ZEUS_STRIKE = 1.9;
        /// <summary>seconds the old climb takes to dissolve into the new one</summary>
        internal const double CROSSFADE = 2.2;
        /// <summary>where the walk down ends: behind the middle of the stone, hidden by it</summary>
        internal const double WALK_END = 42;
        /// <summary>stepping back from the stone into his stance at the start of a climb</summary>
        internal const double SET_TIME = 0.75;
        /// <summary>where the stone lies at the foot of the hill, and where every climb starts</summary>
        internal const double STONE_REST = 40;
        /// <summary>the ending's summit: the strike comes late, after he has smiled</summary>
        internal const double EPIPHANY_STRIKE = 8.5;

        /*
         * Time. Every cycle happens at a different hour, and the walk back down is
         * where the hours pass: golden hour, then the blue hour after sunset, the
         * night with its moon, dawn, sunrise, morning, noon, afternoon — and round
         * again to the golden hour the game began in.
         */
        internal static readonly double[] SCHEDULE = { 18.2, 18.75, 22.8, 5.95, 6.55, 9.5, 13, 16.4 };
        /// <summary>walking pace on the way down, on the level, units/s</summary>
        // He is about 97 units tall, so 54 to the metre. 125 with steps shortened for
        // the slope was nearly five steps a second, a scurry; 100 was a stroll players
        // found too slow. 140 on the flat, easing on the steep parts (walkSpeedFor),
        // with a longer step: a brisk walk of about three steps a second.
        internal const double WALK_SPEED = 140;

        /// <summary>
        /// How fast he walks down a slope (|rise/run|): people slow on a steep descent,
        /// they do not just take quicker steps. At the hills' typical 0.5 that is
        /// about 2 m/s, and the walk down takes some thirty seconds.
        /// </summary>
        internal static double walkSpeedFor(double slope)
        {
            return WALK_SPEED / (1 + 0.5 * Math.Abs(slope));
        }
        internal const double ZEUS_ROLL = 3.0;

        /// <summary>one heave quality: its timing window, the push it lands, the breath it gives</summary>
        internal sealed class HeaveSpec
        {
            public readonly double window, v, breath;
            public HeaveSpec(double window, double v, double breath)
            {
                this.window = window;
                this.v = v;
                this.breath = breath;
            }
        }

        /// <summary>HeaveQuality: "perfect" | "good" | "miss"</summary>
        internal sealed class HeaveTable
        {
            public readonly HeaveSpec perfect = new HeaveSpec(0.09, 205, 0.03);
            public readonly HeaveSpec good = new HeaveSpec(0.18, 120, 0);
            public readonly HeaveSpec miss = new HeaveSpec(double.PositiveInfinity, 25, -0.05);
            public HeaveSpec this[string q]
            {
                get { return q == "perfect" ? perfect : q == "good" ? good : miss; }
            }
        }

        internal static readonly HeaveTable HEAVE = new HeaveTable();

        /// <summary>
        /// Ridge silhouettes from the farthest rank to the nearest. Distance lifts the
        /// value, and this close to a setting sun the haze doing the lifting is warm —
        /// blue haze belongs to a midday sky, and up against the disc it read as a
        /// bruise sitting in front of the light.
        /// </summary>
        internal static readonly string[] RIDGE_TONES =
        {
            "oklch(0.46 0.075 44 / 0.45)",
            "oklch(0.32 0.06 26 / 0.66)",
            "oklch(0.2 0.04 30 / 0.85)",
        };
        /// <summary>how much terrain relief the cosmetic ridges are allowed to stand up</summary>
        internal const double RIDGE_RELIEF = 0.15;
        /// <summary>world units covered by one full two-step leg cycle</summary>
        internal const double STRIDE_LENGTH = 54;

        /// <summary>
        /// How long a stride (two steps) he takes, in world units, for what he is
        /// doing and how fast. The gait turns at speed / stride, and the drawn feet
        /// cover the same stride, so a foot on the ground stays put on the ground.
        /// Pushing a stone the steps are short; walking down they lengthen with the
        /// pace; running after a thrown stone they are long.
        /// mode: "walk" | "push" | "run"
        /// </summary>
        public static double strideFor(string mode, double v, double slope = 0)
        {
            if (mode == "run") return 92 + v * 0.26;
            // on a slope the steps shorten a little (a long step down is a long drop onto
            // the front foot) — but mostly he slows down (walkSpeedFor): the cadence of a
            // walk, never of a run
            if (mode == "walk") return (44 + v * 0.33) / (1 + 0.15 * Math.Abs(slope));
            return 24 + Math.Min(v, 160) * 0.2;
        }
        /// <summary>fastest believable leg turnover, in cycles per second</summary>
        internal const double MAX_CADENCE = 3.4;

        /// <summary>
        /// How many device pixels the frame is allowed to cost.
        ///
        /// `devicePixelRatio` on a desktop retina panel is 2, which on a 1920x1080 window
        /// is 8.3 million pixels to shade — and this scene shades most of them several
        /// times over (sky, ridges, ground, mist, vignette). A phone stays at its full
        /// ratio because its window is small; a large window drops toward 1 rather than
        /// the whole thing dropping frames.
        /// </summary>
        internal const double PIXEL_BUDGET = 2_600_000;

        /// <summary>
        /// The cloud bank is the single most expensive thing in the frame: twenty-one
        /// cumulus, each a stack of radial gradients covering a good fraction of the
        /// screen. It is also the slowest-moving thing in it — six to twenty pixels a
        /// second — so it is painted into its own layer at half resolution and only
        /// repainted when it has actually drifted. Soft gradients are exactly the content
        /// that survives being upscaled; nothing in that layer has an edge to lose.
        /// </summary>
        internal const double CLOUD_RES = 0.5;
        /// <summary>longest a painted cloud layer may be reused, in seconds</summary>
        internal const double CLOUD_MAX_AGE = 0.06;
        /// <summary>how far the front rank may drift, in pixels, before the layer is repainted</summary>
        internal const double CLOUD_MAX_DRIFT = 2;

        internal sealed class Bird
        {
            /// <summary>world-x offset from the player, drawn with parallax</summary>
            public double ox;
            /// <summary>vertical position as a fraction of screen height</summary>
            public double fy;
            /// <summary>parallax factor (0 = far away)</summary>
            public double par;
            /// <summary>self-drift in world units/s</summary>
            public double speed;
            /// <summary>wing-flap animation phase</summary>
            public double flap;
            public double flapSpeed;
            /// <summary>gentle path wobble</summary>
            public double wander;
            public double bob;
            public double bobFreq;
            /// <summary>relative size</summary>
            public double size;
            /// <summary>seconds left in the current glide (wings held still); 0 while flapping</summary>
            public double glide;
            /// <summary>seconds until it next stops flapping to glide</summary>
            public double nextGlide;
            /// <summary>slow bank, so the flock does not fly on rails</summary>
            public double bank;
        }

        /// <summary>a dust mote hanging in the low sun: screen fractions, parallax depth, drift phase</summary>
        internal sealed class Mote
        {
            public double fx, fy, z, ph;
        }

        internal sealed class Particle
        {
            /// <summary>world x (near the boulder)</summary>
            public double wx;
            /// <summary>world y</summary>
            public double wy;
            public double vx;
            public double vy;
            public double life;
            public double maxLife;
            public double size;
            /// <summary>"dust" | "spark"</summary>
            public string kind;
        }

        internal static readonly Sun DEFAULT_SUN = new Sun { xFrac = 0.78, yFrac = 0.6 };

        // ---------- nested types for inline-object fields ----------
        internal sealed class ZeusT
        {
            /// <summary>"gone" | "appear" | "strike" | "fade"</summary>
            public string state;
            public double t;
        }
        internal sealed class BoltFromT
        {
            public double x, y;
        }
        internal sealed class StarsT
        {
            public Surface cv;
            public List<(double x, double y, double mag)> bright;
            // PORT: lets `stars = Sky.starField(...)` be written as in the TS
            public static implicit operator StarsT(StarField f)
            {
                return f == null ? null : new StarsT { cv = f.cv, bright = f.bright };
            }
        }
        internal sealed class MeteorT
        {
            public double x, y, vx, vy, t;
        }
        internal sealed class HeaveFxT
        {
            /// <summary>HeaveQuality: "perfect" | "good" | "miss"</summary>
            public string q;
            public double t;
        }
        internal sealed class ManBoxT
        {
            public double l, r, top;
        }
        internal sealed class QuoteSafeT
        {
            public string key;
            public double top;
            public double l;
            public double r;
            public Func<double, double, double> skyline;
        }
        internal sealed class QuotePlaceT
        {
            public double fs, maxW, cx, top;
        }
        internal sealed class QuoteLayoutsT
        {
            public double w, h, anchorY;
        }
        internal sealed class GhostsT
        {
            public double wx;
            public string lv;
            public Surface cv;
            public double w;
            public double h;
            public double ox;
            public double oy;
        }

        // ---------- fields ----------
        private Ctx ctx;
        private Surface canvas;
        // PORT: `raf` was the requestAnimationFrame handle; here it only says whether the
        // loop is running (start() → true, stop() → false), so frame() is a no-op outside it.
        private bool raf = false;
        private double last = 0;
        private double dpr = 1;
        private double w = 0;
        private double h = 0;

        /// <summary>host: the canvas' size in CSS pixels and the device pixel ratio (replace
        /// getBoundingClientRect and window.devicePixelRatio); set before resize()</summary>
        public double hostW = 0, hostH = 0, hostDpr = 1;

        private Level level;
        private double levelIndex = 0;
        private double cycles = 0;

        private double x = 40;
        private double vx = 0;
        private double roll = 0; // boulder rotation
        private string phase = "playing";
        private double phaseT = 0;
        private double stepT = 0;
        private double shake = 0;

        private double t = 0; // global clock (background animations)

        private double camX = 0;
        private double camY = 0;
        /// <summary>0..1: how far the frame has come up the hill to a speaker in a scene</summary>
        private double camLift = 0;
        private double camLiftTo = 0;
        private double scale = 1;
        private double groundY = 0;
        private double shakeY = 0;
        private double frameDt = 1.0 / 60;

        /*
         * The camera as a camera operator would hold it. It pushes in slowly while
         * he is losing to the stone, punches in a touch on a perfect heave, closes
         * on him at the summit and lets the moment of the strike play in slow
         * motion, then eases back out as the stone goes. Zoom never drops below 1:
         * the frame the game is played in is the widest shot there is.
         */
        /// <summary>false turns the moves off (reduced motion)</summary>
        public bool cinematic = true;
        private double zoom = 1;
        private double zoomKick = 0;
        /// <summary>
        /// Where the kick wants to be: a push on the beat or the bolt raises it,
        /// and the camera eases toward it rather than jumping (a jump on every beat
        /// made the whole sky — the clouds most of all — hop).
        /// </summary>
        private double kickWant = 0;
        /// <summary>0-1: how far the camera has moved to hold both him and the thrown stone</summary>
        private double kickBlend = 0;
        /// <summary>the hip's height over the feet, a little smoothed (see drawFigure)</summary>
        private double hipSm = -40;
        private double strainS = 0;
        private double focusX = -1;
        private double focusY = -1;
        private double focusTX = 0;
        private double focusTY = 0;
        private double shakeX = 0;
        /// <summary>slow motion: real seconds left, its length, and how slow at its deepest</summary>
        private double slowT = 0;
        private double slowDur = 1;
        private double slowDepth = 0;

        /// <summary>pre-painted static sky (gradient + glow + disc), rebuilt only on resize / level change</summary>
        private Surface skyCache = null;
        private string skyCacheKey = "";
        /// <summary>the camera's pan toward a scene's speaker (screen units, eased)</summary>
        private double frameOffX = 0, frameOffY = 0;
        /// <summary>the sky a minute on, blended in over skyCache as the minute passes</summary>
        private Surface skyNext = null;
        private string skyNextKey = "";
        /// <summary>half-resolution cloud bank, repainted only when it has drifted (see CLOUD_RES)</summary>
        private Surface cloudCache = null;
        private string cloudCacheKey = "";
        private double cloudCacheT = -1;
        private double cloudCacheCamX = 0;
        private double cloudRes = CLOUD_RES;
        /// <summary>each cloud's volume, built once (see clouds.ts)</summary>
        private Dictionary<double, CloudShape> cloudShapes = new Dictionary<double, CloudShape>();
        /// <summary>clouds still allowed to be built, or recoloured, during this paint</summary>
        private double cloudBuildBudget = 0;
        private double cloudBudget = 0;
        private double cloudCacheHour = -1;
        /// <summary>the aphorism, rendered once per quote instead of re-wrapped and re-blurred per frame</summary>
        private Surface quoteCache = null;
        private string quoteCacheKey = "";
        /// <summary>the aphorism sprite's size in CSS pixels (the canvas itself is dpr-scaled)</summary>
        private double quoteW = 0;
        private double quoteH = 0;
        /// <summary>where the first line's centre sits inside `quoteCache`</summary>
        private double quoteAnchorY = 0;
        /// <summary>screen-y of the terrain at each 4px column, sampled once and reused by three passes</summary>
        private double[] terrainYs = null;
        /// <summary>the last state handed to the UI, so identical frames don't re-render it</summary>
        private string lastEmitKey = "";
        /// <summary>throttles the friction bus so it isn't handed sixty automation events a second</summary>
        private double frictionT = 0;
        /// <summary>distance-based walk phase, so the gait follows the feet and not the clock</summary>
        private double gait = 0;

        private ZeusT zeus = new ZeusT { state = "gone", t = 0 };
        private ZeusPainter zeusPainter = new ZeusPainter();
        /// <summary>where the bolt leaves his hand, in screen space, set while he is drawn</summary>
        private BoltFromT boltFrom = null;
        /// <summary>world x the man stands at while the stone goes back down without him</summary>
        private double summitX = 0;
        /// <summary>index of the next ledge the falling stone will drop over (rolling phase)</summary>
        private double rollLedge = -1;
        /// <summary>0..1 vertical hop the stone takes when it drops off a ledge</summary>
        private double hop = 0;
        private double hopV = 0;
        private double flash = 0;
        private double strainT = 0;

        /// <summary>&gt;= 0 while a kick is in flight; -1 when idle</summary>
        private double kickT = -1;
        /// <summary>character world position (separates from the boulder during a kick)</summary>
        private double charX = -24;

        private List<Bird> birds = new List<Bird>();
        private List<Mote> motes = new List<Mote>();
        /// <summary>the hour this cycle's climb began at, and the current hour</summary>
        private double baseHour = SCHEDULE[0];
        private double hour = SCHEDULE[0];
        /// <summary>the walk down: where he is, how fast, and the hour the walk ends at</summary>
        private double walkX = 0;
        private double walkV = 0;
        private double walkFrom = 0;
        private double hourFrom = 0;
        private double hourTo = 0;
        /// <summary>the sky for this frame</summary>
        private SkyState st = Sky.skyState(SCHEDULE[0], 0.5);
        private StarsT stars = null;
        private string starsKey = "";
        private MeteorT meteor = null;
        private double meteorT = 8;
        /// <summary>the landscape is painted into this so the hour can grade it without touching the sky</summary>
        private Surface landLayer = null;
        /*
         * Holding the stone. Bracing used to be a still pose — a statue leaning on
         * a rock. A rock on a slope is never still for a man holding it: it creeps
         * back, he feels it, digs in, shoves it home, and it creeps again. These
         * drive that loop visually; the physics underneath is unchanged.
         */
        private double holdT = 0;
        /// <summary>his breathing, for the sound of it: phase, and which half we are in</summary>
        private double breathPh = 0;
        private bool breathIn = true;
        /// <summary>gravel under the stone: time to the next grain</summary>
        private double gritT = 0;
        private double windT = 0;
        private double holdA = 0;
        /// <summary>world units the stone has crept back this frame (drawn, not simulated)</summary>
        private double creep = 0;
        /// <summary>0..1 how hard he is shoving it back into place right now</summary>
        private double holdDrive = 0;
        private double chirpT = 0;

        /// <summary>false until the start card is dismissed: the scene runs, the man waits</summary>
        private bool active = false;
        /// <summary>0..1</summary>
        private double breath = 1;
        /// <summary>&gt; 0 while he has lost his grip after running out of breath</summary>
        private double slipT = 0;
        /// <summary>a fresh press is only a heave if the key came back up first</summary>
        private bool heaveArmed = true;
        /// <summary>the last heave, for the ring's flash: quality and seconds since</summary>
        private HeaveFxT heaveFx = null;
        /// <summary>consecutive perfectly timed heaves; each one adds force, a miss resets it</summary>
        private double streak = 0;
        /// <summary>the streak shown over his head, kept a moment after it breaks</summary>
        private double streakShown = 0;
        private double streakT = 99;
        /// <summary>seconds since a launch was refused for want of breath, for the red flash</summary>
        private double denyT = 99;
        /// <summary>world x below which the stone cannot slip back (the last ledge crossed)</summary>
        private double checkpoint = 0;
        /// <summary>seconds since the last ledge was crossed, for the stele's flare</summary>
        private double checkpointT = 99;
        /// <summary>smoothed visibility of the breath ring</summary>
        private double hudA = 0;
        /// <summary>last frame's screen y of his head, so the aphorism can stay clear of him</summary>
        private double headY = double.PositiveInfinity;
        private ManBoxT manBox = new ManBoxT { l = 0, r = 0, top = double.PositiveInfinity };
        /// <summary>the saying fades where he comes up under it, rather than moving</summary>
        private double quoteFade = 1;
        private QuoteSafeT quoteSafe = null;
        /// <summary>where the saying is written: chosen once for each saying, then held still</summary>
        private QuotePlaceT quotePlace = null;
        private string quotePlaceFor = "";
        private Dictionary<string, QuoteLayoutsT> quoteLayouts = new Dictionary<string, QuoteLayoutsT>();
        private double climbT = 0;
        private double lastClimb = 0;
        private double bestClimb = 0;
        private bool newBest = false;
        /// <summary>seconds spent pushing on a ledge's pitch without getting anywhere</summary>
        private double stuckT = 0;
        /// <summary>seconds left to show the "rest" hint after a slip or a refused launch</summary>
        private double restHintT = 0;
        private double restHintsShown = 0;

        /// <summary>the weather on this climb (see weatherFor): "clear" | "rain" | "storm"</summary>
        public string weather = "clear";
        /// <summary>calm mode: breath never runs out, so he never slips</summary>
        public bool calm = false;
        /// <summary>the first climb teaches, one thing at a time</summary>
        private bool tutorialDone = false;
        /// <summary>"push" | "rhythm" | "free"</summary>
        private string tutStep = "push";
        private double tutGood = 0;
        private double tutBreatheT = 0;
        /// <summary>seconds until the storm's next strike</summary>
        private double stormT = 4;
        /// <summary>this climb so far, for the seals</summary>
        private double runSlips = 0;
        private double runRests = 0;
        private double runMaxStreak = 0;
        private bool wasBraced = false;
        private RunStats lastRun = null;
        /// <summary>once a launch has carried the stone over a ledge, the player knows how</summary>
        private bool kickLearned = false;

        /// <summary>dust / spark motes kicked up around the boulder</summary>
        private List<Particle> particles = new List<Particle>();

        /// <summary>the random aphorism currently written in the sky (assigned at each summit)</summary>
        private Quote currentQuote = null;
        /// <summary>index of currentQuote in the deck (so the sky text can follow a language change)</summary>
        private double currentQuoteIndex = -1;
        /// <summary>shuffled deck of quote indices, reshuffled when emptied</summary>
        private List<int> quoteDeck = new List<int>();
        /// <summary>every aphorism found so far, in the order found</summary>
        private List<int> unlocked = new List<int>();
        /// <summary>the one found at the last summit, if it was new</summary>
        private double newQuote = -1;
        /// <summary>vibration, which some people simply do not want</summary>
        public bool haptics = true;
        /// <summary>frozen while a menu is open over the game</summary>
        public bool paused = false;
        /// <summary>selected UI / quote language (LangCode)</summary>
        private string lang = "tr";

        /// <summary>-1..1 input from keyboard / pointer drag</summary>
        public double input = 0;

        public GameAudio audio = new GameAudio();
        public Action<EngineState> onState = s => { };
        /// <summary>the story: who speaks, what he carries, what he learns (see saga.ts)</summary>
        public Saga saga;
        public Action<SagaEvent> onSaga = e => { };
        /// <summary>how far the hill is in flower (spring), eased toward the saga's target</summary>
        private double bloom = 0;
        /// <summary>a change of weather in the middle of a climb fades in</summary>
        private double wxFade = 1;
        /// <summary>furthest the stone got on this climb (for the wedge)</summary>
        private double maxX = 0;
        /// <summary>the summit of this climb is the ending: he learns to smile</summary>
        private bool epiphany = false;
        /// <summary>0-1, how much he is smiling</summary>
        private double smile = 0;
        /// <summary>the wedge's flash when it catches</summary>
        private double wedgeT = 99;
        /// <summary>echoes of earlier slips: where, and what he looked like</summary>
        private List<GhostsT> ghosts = new List<GhostsT>();

        public SisyphusEngine(Surface canvas, double levelIndex = 0, EngineInit init = null)
        {
            if (init == null) init = new EngineInit();
            this.canvas = canvas;
            this.cycles = Math.Max(0, Math.Floor(init.cycles ?? 0));
            this.bestClimb = Math.Max(0, init.bestClimb ?? 0);
            int total = Quotes.QUOTE_COUNT;
            // [...new Set(filter)]: first occurrence kept, order kept
            this.unlocked = new List<int>();
            if (init.unlocked != null)
            {
                var seen = new HashSet<int>();
                foreach (int i in init.unlocked)
                    if (i >= 0 && i < total && seen.Add(i)) this.unlocked.Add(i);
            }
            this.haptics = init.haptics != false;
            this.baseHour = SCHEDULE[(int)(this.cycles % SCHEDULE.Length)];
            this.hour = this.baseHour;
            // The sky covers every pixel before anything else is drawn, so there is
            // nothing behind the canvas to blend with.
            // PORT: getContext("2d", { alpha: false }) — the Surface's context is used as is.
            this.ctx = canvas.GetContext();
            // three cycles on each hill, in turn; the first hill is the one the game
            // has always started on
            this.levelIndex = levelIndex + Levels.levelForCycle(this.cycles);
            this.level = Levels.getLevel((int)this.levelIndex);
            this.saga = new Saga(init.saga);
            this.saga.onEvent = e => this.onSaga(e);
            this.weather = this.saga.weatherFor(this.cycles, weatherFor(this.cycles));
            this.saga.climbStarted();
            this.bloom = this.saga.bloomTarget;
            this.calm = init.calm == true;
            this.tutorialDone = init.tutorialDone == true || this.cycles > 0;
            if (init.run != null) this.resumeRun(init.run);

            // distant bird flocks drifting across the sky
            /*
             * Flocks, not confetti. Birds used to be scattered at random across 700
             * units and drawn as a pair of strokes — an "M" from a child's drawing.
             * Now each flock keeps a loose V behind a leader, every bird on its own
             * wingbeat, and they flap, glide, flap, the way large birds actually do.
             */
            void flock(double par, double speed, int count, double fy, double size)
            {
                // where the flock starts across the frame, in pixels, through its parallax
                double lead = ((Js.random() - 0.5) * 900) / par;
                double sg = Js.sign(speed);
                double dir = sg != 0 && !double.IsNaN(sg) ? sg : 1;
                for (int i = 0; i < count; i++)
                {
                    double rank = Math.Ceiling(i / 2.0);
                    double side = i % 2 != 0 ? 1 : -1;
                    this.birds.Add(new Bird
                    {
                        // spacing in screen pixels, converted back through the flock's parallax
                        ox = lead - (dir * rank * (15 + Js.random() * 6) + side * 3) / par,
                        fy = fy + side * rank * 0.016 + (Js.random() - 0.5) * 0.006,
                        par = par,
                        speed = speed,
                        flap = Js.random() * Math.PI * 2,
                        flapSpeed = 7 + Js.random() * 2.5,
                        wander = Js.random() * Math.PI * 2,
                        bob = 1.5 + Js.random() * 2,
                        bobFreq = 0.5 + Js.random() * 0.6,
                        size = size * (0.85 + Js.random() * 0.3),
                        glide = 0,
                        nextGlide = 0.6 + Js.random() * 2.5,
                        bank = Js.random() * Math.PI * 2,
                    });
                }
            }
            flock(0.09, 30, 5, 0.15, 1);
            flock(0.06, -22, 4, 0.22, 0.75);
            flock(0.13, 38, 3, 0.09, 1.25);

            // dust in the light: a few dozen motes that only show near the sun
            for (int i = 0; i < 46; i++)
            {
                this.motes.Add(new Mote
                {
                    fx = Js.random(),
                    fy = 0.25 + Js.random() * 0.6,
                    z = 0.3 + Js.random() * 0.7,
                    ph = Js.random() * Math.PI * 2,
                });
            }
        }

        // ---------- lifecycle ----------
        /// <summary>the setup half of the TS start(): size, clouds, and the clock. The loop
        /// itself is the host calling frame(nowMs).</summary>
        public void start()
        {
            this.resize();
            this.prebuildClouds();
            this.last = Js.now();
            this.raf = true;
        }

        /// <summary>
        /// One iteration of the requestAnimationFrame loop in the TS start(). `nowMs` is
        /// the frame's timestamp on the Js.now() (performance.now()) clock.
        /// </summary>
        public void frame(double nowMs)
        {
            if (!this.raf) return;
            double t = nowMs;
            double dt = Math.Min(0.05, (t - this.last) / 1000);
            this.last = t;
            // the notebook is open: the hill holds its breath, and so does the battery
            if (this.paused) return;
            this.update(dt * this.timeScale(dt));
            double r0 = Js.now();
            this.render();
            // PORT: in the browser render() did the painting, so its cost was the frame's cost.
            // Here render() only records the frame and the GPU paints it later, so the host
            // measures whole frames instead: below ~45 fps for two seconds the quality steps
            // down, the same steps the web build took.
            if (this.hostFrameTiming)
            {
                double interval = this.prevFrameAt > 0 ? t - this.prevFrameAt : 0;
                this.prevFrameAt = t;
                if (interval > 0 && interval < 250) this.watchFrameCost(interval, t, 22, 17.5);
            }
            else this.watchFrameCost(Js.now() - r0, t, 12, 5);
        }

        /// <summary>true when the host judges speed by whole frame intervals (the Unity build)</summary>
        public bool hostFrameTiming = true;
        private double prevFrameAt = 0;

        /// <summary>start a stretch of slow motion: `dur` real seconds, `depth` 0-1</summary>
        private void slowMo(double dur, double depth)
        {
            if (!this.cinematic) return;
            this.slowDur = dur;
            this.slowT = dur;
            this.slowDepth = depth;
        }

        /// <summary>how fast game time runs this frame: eases into the slow motion and out</summary>
        private double timeScale(double realDt)
        {
            if (this.slowT <= 0) return 1;
            this.slowT = Math.Max(0, this.slowT - realDt);
            double p = 1 - this.slowT / this.slowDur;
            double env = Math.Min(1, Math.Min(p / 0.12, (1 - p) / 0.4));
            double e = env * env * (3 - 2 * env);
            return 1 - this.slowDepth * e;
        }

        /// <summary>the climb as it stands, to be kept if the game is closed now</summary>
        public RunSave snapshotRun()
        {
            if (this.finaleOn || (this.phase != "playing" && this.phase != "descent")) return null;
            // mid-throw the stone is in the air: keep where he was before it
            double x = this.kickT >= 0 ? Math.Max(STONE_REST, this.charX + 64) : this.x;
            return new RunSave
            {
                cycles = this.cycles,
                lv = this.level.id,
                phase = this.phase,
                x = x,
                checkpoint = this.checkpoint,
                maxX = this.maxX,
                climbT = this.climbT,
                breath = this.breath,
                weather = this.weather,
                walkX = this.walkX,
                walkFrom = this.walkFrom,
                hourFrom = this.hourFrom,
                hourTo = this.hourTo,
                summitX = this.summitX,
            };
        }

        /// <summary>pick up a climb where it was left, if it belongs to this cycle and hill</summary>
        private void resumeRun(RunSave r)
        {
            if (r.cycles != this.cycles || r.lv != this.level.id) return;
            var L = this.level;
            if (r.weather == "clear" || r.weather == "rain" || r.weather == "storm")
                this.weather = r.weather;
            this.breath = r.breath;
            this.climbT = r.climbT;
            if (r.phase == "descent")
            {
                this.phase = "descent";
                this.x = STONE_REST;
                this.walkX = Math.Max(-24, Math.Min(L.length, r.walkX));
                this.walkFrom = Math.Max(this.walkX, r.walkFrom);
                this.hourFrom = r.hourFrom;
                this.hourTo = r.hourTo;
                this.summitX = r.summitX;
                this.walkV = 0;
            }
            else
            {
                double x = Math.Max(STONE_REST, Math.Min(L.length - 20, r.x));
                this.x = x;
                this.charX = x - 64;
                this.checkpoint = Math.Min(x, Math.Max(0, r.checkpoint));
                this.maxX = Math.Max(x, r.maxX);
            }
            // the camera starts where he is, not at the foot of the hill
            this.camX = (this.phase == "descent" ? this.walkX + 40 : this.x) - 420;
        }

        public void stop()
        {
            this.raf = false;
            this.audio.dispose();
        }

        /// <summary>the start card is gone: from here on input moves him and the clock runs</summary>
        public void activate()
        {
            this.active = true;
        }

        public void restart()
        {
            /*
             * A new climb changes everything at once — the hill, the weather, the
             * place of the stone. Cut straight across, that read as a jump. The last
             * frame of the old climb is kept and dissolved into the new one instead.
             */
            var nextLevel = Levels.getLevel(Levels.levelForCycle(this.cycles + 1));
            string nextWeather = this.saga.weatherFor(this.cycles + 1, weatherFor(this.cycles + 1));
            bool sceneChanges = nextLevel.id != this.level.id || nextWeather != this.weather;
            // he has walked down to the stone and turns to it: same hill, same sky,
            // same place — nothing to dissolve, only a man turning round
            this.setT = this.phase == "descent" ? 0 : 99;
            this.setFrom = this.walkX;
            if (sceneChanges && this.canvas.width > 0)
            {
                var cv = this.fadeCv ?? (this.fadeCv = new Surface(this.canvas.width, this.canvas.height));
                if (cv.width != this.canvas.width || cv.height != this.canvas.height)
                    cv.SetSize(this.canvas.width, this.canvas.height);
                cv.GetContext().DrawImage(this.canvas, 0, 0);
                this.fadeT = 0;
            }
            this.breath = 1;
            this.slipT = 0;
            this.checkpoint = 0;
            this.climbT = 0;
            this.streak = 0;
            this.streakT = 99;
            this.heaveFx = null;
            this.stuckT = 0;
            this.x = STONE_REST;
            this.vx = 0;
            this.roll = 0;
            this.input = 0;
            this.phase = "playing";
            this.phaseT = 0;
            this.zeus.state = "gone";
            this.hop = 0;
            this.hopV = 0;
            this.flash = 0;
            this.kickT = -1;
            this.charX = this.x - 64;
            this.cycles += 1;
            this.baseHour = SCHEDULE[(int)(this.cycles % SCHEDULE.Length)];
            this.hour = this.baseHour;
            // a new hill every third cycle, and the weather for this one
            this.levelIndex = Levels.levelForCycle(this.cycles);
            this.level = Levels.getLevel((int)this.levelIndex);
            this.weather = this.saga.weatherFor(this.cycles, weatherFor(this.cycles));
            this.wxFade = 1;
            this.maxX = 0;
            this.epiphany = false;
            this.eagleDone = false;
            this.mireMet = false;
            this.saga.climbStarted();
            this.particles.Clear();
            this.runSlips = 0;
            this.runRests = 0;
            this.runMaxStreak = 0;
            this.stormT = 4;
            this.emit();
        }

        /// <summary>the moon's phase moves on a little every cycle</summary>
        private double moonPhase()
        {
            return (this.cycles * 0.123 + 0.38) % 1;
        }

        /// <summary>he has run out of breath and lost his grip on the stone (for the second-wind offer)</summary>
        public bool winded { get { return this.active && this.phase == "playing" && this.slipT > 0 && !this.calm; } }

        /// <summary>a second wind (the rewarded ad): breath full, the grip back at once</summary>
        public void secondWind()
        {
            if (!this.active || this.phase != "playing") return;
            this.breath = 1;
            this.slipT = 0;
            this.restHintT = 0;
            this.haptic(20);
        }

        /// <summary>Tab: kick the boulder loose up the ramp; it rolls back while the character sprints ahead</summary>
        public void kick()
        {
            if (!this.active || this.phase != "playing" || this.kickT >= 0 || this.slipT > 0) return;
            if (this.breath < KICK_COST)
            {
                this.denyT = 0;
                this.audio.strain(false);
                this.restHintT = Math.Max(this.restHintT, 2.5);
                return;
            }
            this.breath -= KICK_COST;
            this.streak = 0;
            this.haptic(30);
            this.kickT = 0;
            this.vx = KICK_V;
            // no jolt on the throw: the frame shaking as he lets go read as a glitch
            this.audio.kick();
        }

        /// <summary>set by the interface while a scene plays: the climb stops</summary>
        public bool cutscene = false;
        /// <summary>the last scene: at the bottom, his hands on the stone</summary>
        public bool finaleOn = false;
        private double finaleT = 0;
        public Action onFinale = () => { };

        /// <summary>she has gone: he walks on down to the stone, and there the story ends</summary>
        public void meropeGone()
        {
            this.meropeLeaving = true;
            this.meropeLeave = 0;
        }

        private void startFinale()
        {
            this.finaleOn = true;
            this.finaleT = 0;
            this.phase = "playing";
            this.x = 0;
            this.vx = 0;
            this.charX = -64;
            this.kickT = -1;
            this.weather = "clear";
            this.audio.swell();
            this.emit();
            this.onFinale();
        }

        /// <summary>the end card has been closed: the story is over, the climbing is not</summary>
        public void endFinale()
        {
            this.finaleOn = false;
            this.saga.complete();
            this.restart();
        }

        /// <summary>
        /// The Mire of Regret. Once he knows what he did to her, a stretch of the
        /// path below her turns to black mud: slow, heavy, sucking at his feet. It
        /// is there through the whole of the second act, and gone after.
        /// </summary>
        private (double, double)? mireSpan()
        {
            if (this.saga.stage != 2) return null;
            double L = this.level.length;
            return (L * 0.52, L * 0.64);
        }
        private bool inMire(double x)
        {
            var m = this.mireSpan();
            return m.HasValue && x > m.Value.Item1 && x < m.Value.Item2;
        }
        private bool mireMet = false;
        public Action onMire = () => { };

        /// <summary>where a companion's wedge sits on this hill</summary>
        private double wedgeX()
        {
            return this.level.length * 0.48;
        }

        /// <summary>
        /// The grip went. Despair grows — and if it grows far enough mid-climb, the
        /// sky turns with it — and the moment is kept as an echo on the hillside.
        /// </summary>
        private void onSlip()
        {
            this.saga.slipped();
            double d = this.saga.s.despair;
            if (this.cycles >= 2 && this.weather == "clear" && d > 0.55)
            {
                this.weather = "rain";
                this.wxFade = 0;
            }
            else if (this.weather == "rain" && d > 0.75)
            {
                this.weather = "storm";
                this.wxFade = 0.4;
            }
            // the echo: a copy of how he looks right now, left where it happened
            var layers = this.figLayers;
            if (layers.Count == 3 && layers[0] != null && !this.lowQuality)
            {
                var src = layers[0];
                var cv = new Surface(src.width, src.height);
                var c = cv.GetContext();
                foreach (var l in layers) c.DrawImage(l, 0, 0);
                c.GlobalCompositeOperation = Composite.SourceIn;
                c.FillStyle = "rgb(190, 210, 240)";
                c.FillRect(0, 0, cv.width, cv.height);
                double s = this.scale;
                this.ghosts.Add(new GhostsT
                {
                    wx = this.x - 64,
                    lv = this.level.id,
                    cv = cv,
                    w = 230 * s,
                    h = 190 * s,
                    ox = -110 * s,
                    oy = -170 * s,
                });
                if (this.ghosts.Count > 8) this.ghosts.RemoveAt(0);
            }
        }

        /// <summary>pull a quote at random, without repeating until the deck is exhausted</summary>
        private Quote nextQuote()
        {
            var list = Quotes.quotesFor(this.lang);
            int count = Quotes.QUOTE_COUNT;
            /*
             * Each summit turns up a page not yet found, while there are any — so the
             * collection always grows. Once every page is found, they come round again
             * at random and the summit is just the words.
             */
            var have = new HashSet<int>(this.unlocked);
            var fresh = new List<int>();
            for (int i = 0; i < count; i++)
                if (!have.Contains(i)) fresh.Add(i);
            if (fresh.Count > 0)
            {
                // the sayings follow his mind: revolt, then the void, then the absurd
                int fidx = this.saga.pickQuote(fresh) ?? fresh[0];
                this.unlocked.Add(fidx);
                this.newQuote = fidx;
                this.currentQuoteIndex = fidx;
                return quoteAt(list, fidx);
            }
            this.newQuote = -1;
            if (this.quoteDeck.Count == 0)
            {
                var deck = new List<int>(count);
                for (int i = 0; i < count; i++) deck.Add(i);
                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int j = (int)Math.Floor(Js.random() * (i + 1));
                    int a = deck[i];
                    deck[i] = deck[j];
                    deck[j] = a;
                }
                this.quoteDeck = deck;
            }
            int idx = this.quoteDeck[this.quoteDeck.Count - 1];
            this.quoteDeck.RemoveAt(this.quoteDeck.Count - 1);
            this.currentQuoteIndex = idx;
            return quoteAt(list, idx);
        }

        /// <summary>`list[idx] ?? null`</summary>
        private static Quote quoteAt(List<Quote> list, double idx)
        {
            if (list == null || idx < 0 || idx >= list.Count) return null;
            return list[(int)idx];
        }

        /// <summary>
        /// A page found away from the hill (the saying of the day). Returns whether it
        /// was new.
        /// </summary>
        public bool grantQuote(int idx)
        {
            if (idx < 0 || idx >= Quotes.QUOTE_COUNT || this.unlocked.Contains(idx)) return false;
            this.unlocked.Add(idx);
            this.emit();
            return true;
        }

        /// <summary>a language's aphorisms have arrived: put the current one into words</summary>
        public void refreshQuotes()
        {
            if (this.currentQuoteIndex >= 0)
            {
                this.currentQuote = quoteAt(Quotes.quotesFor(this.lang), this.currentQuoteIndex);
            }
            this.emit();
        }

        /// <summary>switch the UI and the aphorisms to another language</summary>
        public void setLanguage(string lang)
        {
            if (lang == this.lang) return;
            this.lang = lang;
            // keep the aphorism currently written in the sky in the new language
            if (this.currentQuoteIndex >= 0)
            {
                this.currentQuote = quoteAt(Quotes.quotesFor(lang), this.currentQuoteIndex);
            }
            this.emit();
        }

        public void resize()
        {
            // PORT: getBoundingClientRect() → hostW/hostH, devicePixelRatio → hostDpr
            double rw = this.hostW, rh = this.hostH;
            // A pixel ratio is a quality setting, not a constant. Take the device's, then
            // spend no more than the budget allows: small windows keep the full ratio,
            // large ones give some of it back rather than giving frames back.
            double want = Math.Min(2, this.hostDpr > 0 ? this.hostDpr : 1);
            double area = Math.Max(1, rw * rh);
            // and the frame-time watch may have traded some of it for smoothness
            this.dpr = Math.Max(0.8, Math.Min(want, Math.Sqrt(PIXEL_BUDGET / area)) * this.resScale);
            this.w = rw;
            this.h = rh;
            // PORT: the two separate width/height writes (each clearing) become one SetSize
            this.canvas.SetSize((int)Math.Floor(rw * this.dpr), (int)Math.Floor(rh * this.dpr));
            this.ctx.SetTransform(this.dpr, 0, 0, this.dpr, 0, 0);
        }

        private double progress()
        {
            return Math.Max(0, Math.Min(1, this.x / this.level.length));
        }

        private void emit()
        {
            var q = this.currentQuote;
            double progress = this.progress();
            this.lastEmitKey = this.stateKey(progress);
            string levelName = "";
            Dictionary<string, string> names;
            string nm;
            if (I18n.LEVEL_NAMES.TryGetValue(this.level.id, out names) && names != null
                && names.TryGetValue(this.lang, out nm) && nm != null)
                levelName = nm;
            this.onState(new EngineState
            {
                phase = this.phase,
                progress = progress,
                levelName = levelName,
                epigraph = q != null ? q.text + " — " + q.author : "",
                cycles = this.cycles,
                climbTime = this.climbT,
                lastClimb = this.lastClimb,
                bestClimb = this.bestClimb,
                newBest = this.newBest,
                hint = this.hint(),
                unlocked = new List<int>(this.unlocked),
                newQuote = this.newQuote,
                weather = this.weather,
                levelId = this.level.id,
                tutorialDone = this.tutorialDone,
                lastRun = this.lastRun,
            });
        }

        /// <summary>"push" | "rhythm" | "breathe" | "kick" | "rest", or null</summary>
        private string hint()
        {
            if (!this.active || this.phase != "playing") return null;
            // the first climb: one lesson at a time, each until it has been done
            if (!this.tutorialDone)
            {
                if (this.tutBreatheT > 0) return "breathe";
                if (this.tutStep == "push") return "push";
                if (this.tutStep == "rhythm") return "rhythm";
            }
            if (this.restHintT > 0) return "rest";
            if (!this.kickLearned && this.stuckT > 2.5 && this.kickT < 0) return "kick";
            return null;
        }

        private string stateKey(double progress)
        {
            return (this.hint() ?? "null") + "|" + this.phase + "|" + Js.str(this.cycles) + "|" + this.lang + "|"
                + Js.str(this.currentQuoteIndex) + "|" + Js.str(this.unlocked.Count) + "|"
                + Js.str(Js.round(progress * 120));
        }

        /// <summary>
        /// The HUD was being handed a new state object on every single frame. Nothing
        /// above the canvas needs to know about a change it cannot show: the bar is about a
        /// hundred and fifty pixels wide, so a hundred and twenty steps across it is already
        /// finer than the display. Everything that actually reads as a change (phase, cycle,
        /// language, which aphorism is up) is in the key, so none of them can be missed.
        /// </summary>
        private void emitIfChanged()
        {
            if (this.stateKey(this.progress()) == this.lastEmitKey) return;
            this.emit();
        }

        // ---------- simulation ----------
        private static double easeQ(double q)
        {
            return q * q * (3 - 2 * q);
        }

        private void update(double dt)
        {
            var L = this.level;
            this.t += dt;
            double slope = Levels.slopeAt(L, this.x);
            string prevPhase = this.phase;
            this.frameDt = dt;

            // the eagle keeps its own time
            if (this.eagle.phase != "away")
            {
                this.eagle.t += dt;
                var e = this.eagle;
                if (e.phase == "arrive" && e.t > 2.2) this.eagle = new EagleVisit("feed", 0);
                else if (e.phase == "feed" && e.t > 7) this.eagle = new EagleVisit("leave", 0);
                else if (e.phase == "leave" && e.t > 2.2) this.eagle = new EagleVisit("away", 0);
            }
            if (this.meropeLeaving && this.meropeLeave < 1)
                this.meropeLeave = Math.Min(1, this.meropeLeave + dt / 7);
            // a scene, or the ending: the hill holds still while the story plays
            if (this.cutscene || this.finaleOn)
            {
                if (this.finaleOn)
                {
                    this.finaleT += dt;
                    this.smile = Math.Min(1, Math.Max(this.smile, (this.finaleT - 2.5) / 3));
                }
                this.input = 0;
                this.updateBirds(dt);
                this.updateParticles(dt);
                return;
            }
            if (!this.active) this.input = 0;
            this.denyT += dt;
            bool braced =
                this.active &&
                this.phase == "playing" &&
                this.kickT < 0 &&
                this.slipT <= 0 &&
                Math.Abs(this.input) <= 0.15;
            this.holdA += ((braced ? 1 : 0) - this.holdA) * Math.Min(1, dt * 3);
            // a rest is a brace begun with breath to win back
            if (braced && !this.wasBraced && this.breath < 0.85) this.runRests++;
            this.wasBraced = braced;
            if (!this.tutorialDone && this.phase == "playing" && this.active)
            {
                if (this.tutStep == "push" && this.x > 300) this.tutStep = "rhythm";
                else if (this.tutStep == "rhythm" && (this.tutGood >= 3 || this.x > 1200))
                    this.tutStep = "free";
                // running low: show how to get it back, until he has braced for a moment
                if (this.breath < 0.35 && this.tutBreatheT <= 0 && !this.calm) this.tutBreatheT = 6;
                if (this.tutBreatheT > 0)
                    this.tutBreatheT = braced && this.holdT > 1.2 ? 0 : this.tutBreatheT - dt * 0.2;
            }
            // the storm: lightning somewhere over the hill every few seconds
            if (this.weather == "storm" && this.phase != "summit")
            {
                this.stormT -= dt;
                if (this.stormT <= 0)
                {
                    this.stormT = 6 + Js.random() * 9;
                    this.flash = Math.Max(this.flash, 0.28 + Js.random() * 0.2);
                    this.audio.thunder();
                }
            }
            if (braced) this.holdT += dt;
            {
                // one creep-and-catch every few seconds, quicker and bigger as breath goes
                double period = 3.4 - (1 - this.breath) * 1.4;
                double u = (this.holdT % period) / period;
                double amp = (2.2 + (1 - this.breath) * 3.5) * this.holdA;
                if (u < 0.72)
                {
                    this.creep = -easeQ(u / 0.72) * amp;
                    this.holdDrive = 0;
                }
                else
                {
                    double r = (u - 0.72) / 0.28;
                    this.creep = -amp * (1 - easeQ(r));
                    this.holdDrive = Math.Sin(r * Math.PI) * this.holdA;
                }
                // the shove back kicks a little grit from under the back foot
                if (this.holdDrive > 0.85 && Js.random() < 0.25) this.dustBurst(this.x - 94, 1);
            }
            this.streakT += dt;
            this.updateSoundscape(dt);
            this.checkpointT += dt;
            this.restHintT = Math.Max(0, this.restHintT - dt);
            if (this.heaveFx != null) this.heaveFx.t += dt;

            if (this.phase == "playing")
            {
                if (this.active) this.climbT += dt;
                if (this.kickT >= 0)
                {
                    this.kickT += dt;
                    // boulder sails uphill, then gravity rolls it back down the ramp
                    this.vx += (-slope * GRAVITY * L.gravityScale - this.vx * 0.12) * dt;
                    this.x += this.vx * dt;
                    // unburdened, the character sprints up the hill
                    this.charX += KICK_SPRINT * dt;
                    this.checkLedges();
                    // the boulder rolls back to the character: resume pushing
                    if (this.x <= this.charX + 64)
                    {
                        this.x = this.charX + 64;
                        this.vx = 0;
                        this.kickT = -1;
                        // whatever key is down now was pressed before the catch: not a heave
                        this.heaveArmed = this.input < 0.3;
                    }
                    this.x = Math.Max(0, this.x);
                    this.breath = Math.Min(1, this.breath + BREATH_REGEN * 0.35 * dt);
                }
                else
                {
                    this.charX = this.x - 64;
                    // a heave is the rising edge of the push, with hysteresis so a joystick
                    // wavering around the threshold does not fire a stream of them
                    if (this.input < 0.3) this.heaveArmed = true;
                    else if (this.heaveArmed && this.input >= 0.5 && this.slipT <= 0)
                    {
                        this.heaveArmed = false;
                        this.heave();
                    }

                    double push = 0;
                    double gravity = -slope * GRAVITY * L.gravityScale;
                    double friction = FRICTION;
                    // footing: loose scree and wet ground give less back for each step
                    bool onScree = L.scree.HasValue && this.x > L.scree.Value.Item1 && this.x < L.scree.Value.Item2;
                    double ramp = this.difficultyRamp();
                    // the Mire of Regret: in the void, a stretch of the path turns to mud
                    bool inMire = this.inMire(this.x);
                    if (inMire && !this.mireMet)
                    {
                        this.mireMet = true;
                        this.onMire();
                    }
                    double grip =
                        (onScree ? 1 - 0.2 * ramp : 1) *
                        (this.weather == "rain" || this.weather == "storm" ? 1 - 0.1 * ramp : 1) *
                        (inMire ? 0.72 : 1);
                    if (inMire) friction *= 1.9;
                    // gusts lean on the stone, as hard as the grass is leaning
                    gravity -= this.gustForce();
                    if (this.slipT > 0)
                    {
                        // grip gone: the stone takes him back down until he catches it
                        this.slipT -= dt;
                        // Patience (Tantalus's gift): the slip gives back less ground
                        friction = FRICTION * (this.saga.has("patience") ? 0.85 : 0.5);
                        this.breath = Math.Min(1, this.breath + BREATH_REGEN * 0.8 * dt);
                    }
                    else if (this.input > 0.15)
                    {
                        // Prometheus's pain, carried up: the stone is heavier
                        push = this.input * PUSH_FORCE * grip * (this.saga.s.burden == "pro" ? 0.84 : 1);
                        // on the steep he digs in and drives with everything he has
                        push *= 1 + 0.6 * Math.Max(0, slope - 0.55);
                        // Tantalus's thirst: breath goes faster
                        double thirst = this.saga.s.burden == "tan" ? 1.3 : 1;
                        if (!this.calm)
                            this.breath -= BREATH_DRAIN * thirst * this.input * (0.5 + Math.Max(0, slope) * 2) * dt;
                        else this.saga.steady(dt);
                        if (this.breath > 0.3 && this.vx > 30) this.saga.steady(dt);
                        if (this.breath <= 0)
                        {
                            this.runSlips++;
                            this.breath = 0;
                            this.slipT = SLIP_TIME * (this.saga.has("patience") ? 0.7 : 1);
                            this.onSlip();
                            this.shake = Math.Max(this.shake, 0.5);
                            this.audio.gasp();
                            this.streak = 0;
                            this.haptic(70);
                            if (this.restHintsShown < 3)
                            {
                                this.restHintsShown++;
                                this.restHintT = 4;
                            }
                        }
                    }
                    else if (this.input < -0.15)
                    {
                        push = this.input * PUSH_FORCE;
                    }
                    else
                    {
                        // Braced: shoulder in, legs locked. On the ordinary slope that holds the
                        // stone where it is; on a ledge's pitch it is too steep to hold, so the
                        // only safe place to rest is below one or on top of one.
                        // (on scree the stones under it give, and it creeps back)
                        gravity *= Math.Max(onScree ? 0.22 * ramp : 0, Math.Min(1, (slope - 0.6) / 0.5)) * 0.8;
                        friction = FRICTION * 4;
                        // Fire (Prometheus's gift): breath comes back faster
                        this.breath = Math.Min(
                            1,
                            this.breath + BREATH_REGEN * (this.saga.has("fire") ? 1.3 : 1) * dt);
                    }
                    double a = push + gravity - this.vx * friction;
                    this.vx += a * dt;
                    this.x += this.vx * dt;
                    // the wedge a companion laid: the first time the stone runs back past
                    // it, it stops there
                    double wedgeX = this.wedgeX();
                    if (this.saga.s.wedge && this.maxX > wedgeX + 30 && this.x < wedgeX && this.vx < 0)
                    {
                        this.x = wedgeX;
                        this.vx = 0;
                        this.slipT = 0;
                        this.wedgeT = 0;
                        this.shake = Math.Max(this.shake, 0.45);
                        this.audio.impact();
                        this.haptic(40);
                        this.dustBurst(this.x, 6);
                        this.saga.wedgeHeld();
                    }
                    this.maxX = Math.Max(this.maxX, this.x);
                    if (this.active) this.saga.ascent(this.x / L.length, this.cycles);
                    this.checkLedges();
                    bool onPitch = false;
                    var pl = this.level.ledges;
                    if (pl != null)
                    {
                        for (int i = 0; i < pl.Length; i++)
                        {
                            double lx = pl[i].Item1;
                            if (this.checkpoint < lx && this.x > lx - Levels.LEDGE_RUN - 30 && this.x < lx + 8)
                            {
                                onPitch = true;
                                break;
                            }
                        }
                    }
                    if (onPitch && this.input > 0.15 && this.vx < 40) this.stuckT += dt;
                    else if (!onPitch) this.stuckT = 0;
                    if (this.x < 0)
                    {
                        this.x = 0;
                        this.vx = Math.Max(0, this.vx);
                    }
                    // footsteps + straining while actively pushing forward
                    if (this.input > 0.15 && this.vx > 8)
                    {
                        this.stepT -= dt * (0.6 + Math.Min(1.6, this.vx / 90));
                        if (this.stepT <= 0)
                        {
                            this.stepT = 0.62;
                            this.audio.step();
                        }
                        this.strainT -= dt;
                        if (this.strainT <= 0)
                        {
                            this.strainT = 0.85;
                            this.audio.strain(this.breath < 0.25);
                        }
                    }
                }
                if (this.x >= L.length)
                {
                    this.x = L.length;
                    this.vx = 0;
                    this.kickT = -1;
                    this.lastClimb = this.climbT;
                    this.newBest = this.bestClimb == 0 || this.climbT < this.bestClimb;
                    if (this.newBest) this.bestClimb = this.climbT;
                    this.currentQuote = this.nextQuote();
                    // every seventh summit the sky carries the player's own words instead
                    if (this.ownQuote != null && this.cycles % 7 == 6) this.currentQuote = this.ownQuote;
                    // the story moves on; and once, here, he understands
                    this.saga.summit(this.runSlips);
                    this.tutorialDone = true;
                    this.lastRun = new RunStats
                    {
                        id = this.cycles,
                        seconds = this.climbT,
                        slips = this.runSlips,
                        rests = this.runRests,
                        maxStreak = this.runMaxStreak,
                        night = this.st.elev < -6,
                        weather = this.weather,
                        levelId = this.level.id,
                        calm = this.calm,
                    };
                    /*
                     * There is no last cycle. This used to stop at fifty and show a "50 / 50
                     * cycles complete" card, which is the one thing the myth does not have —
                     * a finish. Reaching the summit always sends him back down now, and the
                     * counter just keeps going up.
                     */
                    this.phase = "summit";
                    this.phaseT = 0;
                    this.zeus.state = "appear";
                    this.zeus.t = 0;
                    this.summitX = this.x - 64;
                    this.audio.toll();
                    this.haptic(new double[] { 20, 60, 20 });
                    // the summit: the world slows for a breath (the ending takes its time instead)
                    if (!this.epiphany) this.slowMo(1.2, 0.5);
                }
            }
            else if (this.phase == "summit")
            {
                this.phaseT += dt;
                this.zeus.t += dt;
                this.vx *= 0.9;
                // He takes his time now: the storm gathers, he rises out of it with the
                // bolt drawn back, and only then does he throw.
                // at the ending the god waits a long while, and he looks out and smiles
                double strikeAt = this.epiphany ? EPIPHANY_STRIKE : ZEUS_STRIKE;
                // the ending: the sky stays clear while he looks out, and only then
                // does the storm gather
                if (this.epiphany)
                {
                    if (this.phaseT < EPIPHANY_STRIKE - 2.4)
                    {
                        this.zeus.state = "gone";
                        this.zeus.t = 0;
                    }
                    else if (this.zeus.state == "gone")
                    {
                        this.zeus.state = "appear";
                        this.zeus.t = 0;
                    }
                }
                if (this.epiphany) this.smile = Math.Min(1, Math.Max(this.smile, (this.phaseT - 2.2) / 2.5));
                if (this.phaseT >= strikeAt && this.zeus.state == "appear")
                {
                    this.zeus.state = "strike";
                    this.zeus.t = 0;
                    this.flash = 0.7;
                    this.shake = Math.Max(this.shake, 1);
                    // the strike in slow motion, and the camera jolted in toward it
                    this.slowMo(1.1, 0.72);
                    this.kickWant = Math.Max(this.kickWant, 0.07);
                    this.audio.thunder();
                    this.haptic(new double[] { 0, 40, 30, 120 });
                    // the stone is hit: sparks and grit thrown off it
                    for (int i = 0; i < 26; i++)
                    {
                        double a = -Math.PI * (0.1 + Js.random() * 0.8);
                        double v = 80 + Js.random() * 220;
                        this.particles.Add(new Particle
                        {
                            wx = this.x + (Js.random() - 0.5) * 20,
                            wy = Levels.terrainAt(L, this.x) + 50 + Js.random() * 20,
                            vx = Math.Cos(a) * v,
                            vy = Math.Sin(a) * v,
                            life = 0,
                            maxLife = 0.5 + Js.random() * 0.6,
                            size = (1.2 + Js.random() * 2) * this.scale,
                            kind = i % 3 != 0 ? "spark" : "dust",
                        });
                    }
                }
                if (this.phaseT > strikeAt + (ZEUS_ROLL - ZEUS_STRIKE))
                {
                    this.phase = "rolling";
                    this.phaseT = 0;
                    this.vx = -260;
                    this.zeus.state = "fade";
                    this.zeus.t = 0;
                    this.flash = 0.18;
                    var ledges = L.ledges;
                    this.rollLedge = (ledges != null ? ledges.Length : 0) - 1;
                }
            }
            else if (this.phase == "rolling")
            {
                this.phaseT += dt;
                if (this.zeus.state == "fade")
                {
                    this.zeus.t += dt;
                    if (this.zeus.t > 1.4) this.zeus.state = "gone";
                }
                this.vx += -slope * ROLL_GRAVITY * dt - this.vx * 0.25 * dt;
                this.vx = Math.Max(this.vx, -1400);
                this.x += this.vx * dt;
                // every ledge it worked for, it now drops off: a jolt, a bounce, a cloud
                var ledges = L.ledges;
                int ri = (int)this.rollLedge;
                if (ledges != null && ri >= 0 && ri < ledges.Length && this.x < ledges[ri].Item1 - Levels.LEDGE_RUN * 0.5)
                {
                    this.rollLedge--;
                    this.hopV = 1.6 + Math.Min(1.2, Math.Abs(this.vx) / 600);
                    this.shake = Math.Max(this.shake, 0.55);
                    this.audio.impact();
                    this.haptic(30);
                    this.dustBurst(this.x, 14);
                }
                // it comes to rest where the next climb starts from, so that he walks
                // down to it and takes it up again where it lies
                if (this.x <= STONE_REST)
                {
                    this.x = STONE_REST;
                    if (this.phase == "rolling") this.audio.impact();
                    this.shake = 1;
                    this.vx = 0;
                    this.phase = "descent";
                    this.phaseT = 0;
                    // the walk down: from where he stood at the top, while the hours pass
                    this.walkX = this.summitX;
                    this.walkFrom = this.summitX;
                    this.walkV = 0;
                    this.hourFrom = this.hour;
                    double next = SCHEDULE[(int)((this.cycles + 1) % SCHEDULE.Length)];
                    while (next <= this.hourFrom) next += 24;
                    this.hourTo = next;
                }
            }
            else if (this.phase == "descent")
            {
                /*
                 * The descent. Camus thought this was the part that mattered: the hour of
                 * consciousness, the man walking back down to his stone with nothing in
                 * his hands. So the player walks it, at a walking pace, holding any
                 * direction; the light changes around him on the way, and when he reaches
                 * the stone the next climb begins.
                 */
                this.phaseT += dt;
                double want = this.active && Math.Abs(this.input) > 0.15 ? walkSpeedFor(Levels.slopeAt(L, this.walkX)) : 0;
                this.walkV += (want - this.walkV) * Math.Min(1, dt * 4);
                this.walkX -= this.walkV * dt;
                this.saga.descent(this.walkX / L.length);
                this.breath = Math.Min(1, this.breath + BREATH_REGEN * 0.5 * dt);
                double done = 1 - (this.walkX - WALK_END) / Math.Max(1, this.walkFrom - WALK_END);
                this.hour = this.hourFrom + (this.hourTo - this.hourFrom) * Math.Max(0, Math.Min(1, done));
                // he walks down to the stone itself, round behind it, and the next
                // climb begins there: he steps back from it into his stance
                if (this.walkX <= WALK_END)
                {
                    // after she has gone: he comes down to his stone, and the story ends there
                    if (this.saga.s.finale) this.startFinale();
                    else this.restart();
                }
            }
            else
            {
                this.phaseT += dt;
            }
            // the climb itself takes a little time too
            if (this.phase == "playing") this.hour = this.baseHour + this.progress() * 0.35;

            this.roll += (this.vx * dt) / 46;
            // the hop: a little ballistic arc in units of the stone's radius
            if (this.hop > 0 || this.hopV > 0)
            {
                this.hop += this.hopV * dt;
                this.hopV -= 9 * dt;
                if (this.hop <= 0)
                {
                    this.hop = 0;
                    this.hopV = this.hopV < -1.2 ? -this.hopV * 0.3 : 0;
                }
            }

            // The gait follows the feet, not the clock and not the boulder. Driving it off
            // `roll` meant the legs blurred at thirty cycles a second while the stone
            // thundered back down the hill; capping the cadence keeps him human.
            double backing = this.setT < SET_TIME ? Math.Abs(this.setFrom - (this.x - 64)) / SET_TIME : 0;
            double charSpeed =
                this.phase == "descent"
                    ? this.walkV
                    : this.kickT >= 0
                        ? KICK_SPRINT
                        : Math.Max(Math.Abs(this.vx), backing);
            string gaitMode =
                this.kickT >= 0 && this.phase == "playing"
                    ? "run"
                    : this.phase == "playing"
                        ? "push"
                        : "walk";
            double gaitSlope = Levels.slopeAt(L, this.phase == "descent" ? this.walkX : this.x);
            double gaitWas = this.gait;
            this.gait +=
                Math.Min(charSpeed / strideFor(gaitMode, charSpeed, gaitSlope), MAX_CADENCE) * Math.PI * 2 * dt;
            // walking down, a footstep is heard as each foot lands, not on a clock of its own
            if (this.phase == "descent" && this.walkV > 20 && Math.Floor(this.gait / Math.PI) != Math.Floor(gaitWas / Math.PI))
                this.audio.step();

            this.shake = Math.Max(0, this.shake - dt * 2.2);
            this.flash = Math.Max(0, this.flash - dt * 1.6);
            this.bloom += (this.saga.bloomTarget - this.bloom) * Math.Min(1, dt * 0.25);
            this.wxFade = Math.Min(1, this.wxFade + dt / 5);
            this.wedgeT += dt;
            this.fadeT += dt;
            this.setT += dt;
            if (this.saga.s.ended) this.smile = Math.Max(this.smile, 0.7);

            this.updateBirds(dt);
            this.updateParticles(dt);
            this.emitParticles();

            // The friction bus is smoothed with a time constant of ~0.1s, so feeding it
            // sixty target changes a second only piles automation events onto the audio
            // thread; a dozen is past the point where the ear can tell.
            this.frictionT -= dt;
            if (this.frictionT <= 0 || prevPhase != this.phase)
            {
                this.frictionT = 1.0 / 15;
                this.audio.updateFriction(Math.Min(1, Math.Abs(this.vx) / 420), this.phase == "rolling");
            }
            if (prevPhase != this.phase) this.emit();
            else this.emitIfChanged();
        }

        /// <summary>time to the nearest beat, signed: negative = early</summary>
        private double beatError()
        {
            double p = this.t % BEAT;
            return p < BEAT / 2 ? p : p - BEAT;
        }

        private void heave()
        {
            double err = Math.Abs(this.beatError());
            string q =
                err <= HEAVE.perfect.window ? "perfect" : err <= HEAVE.good.window ? "good" : "miss";
            var h = HEAVE[q];
            // a run of perfect heaves builds momentum: +10% per link, up to +30%
            this.streak = q == "perfect" ? this.streak + 1 : 0;
            this.runMaxStreak = Math.Max(this.runMaxStreak, this.streak);
            if (q != "miss") this.tutGood++;
            if (this.streak >= 2)
            {
                this.streakShown = this.streak;
                this.streakT = 0;
            }
            double bonus = 1 + Math.Min(0.3, Math.Max(0, this.streak - 1) * 0.1);
            this.vx = Math.Max(this.vx, 0) + h.v * bonus;
            if (q == "perfect") this.haptic(this.streak >= 3 ? 18 : 10);
            this.breath = Math.Max(0, Math.Min(1, this.breath + h.breath));
            this.heaveFx = new HeaveFxT { q = q, t = 0 };
            if (q == "perfect" && this.cinematic) this.kickWant = Math.Min(0.025, this.kickWant + 0.01);
            this.audio.heave(q, (int)this.streak);
            if (q == "perfect")
            {
                for (int i = 0; i < 6; i++)
                {
                    double wx = this.x - 30 + Js.random() * 12;
                    this.particles.Add(new Particle
                    {
                        wx = wx,
                        wy = Levels.terrainAt(this.level, wx) + 4,
                        vx = 30 + Js.random() * 50,
                        vy = -30 - Js.random() * 40,
                        life = 0,
                        maxLife = 0.4 + Js.random() * 0.3,
                        size = (1.4 + Js.random()) * this.scale,
                        kind = "spark",
                    });
                }
            }
        }

        /// <summary>ledges are one-way: cross a crest and the stone can no longer slip below it</summary>
        private void checkLedges()
        {
            var ledges = this.level.ledges;
            if (ledges != null)
            {
                for (int k = 0; k < ledges.Length; k++)
                {
                    double lx = ledges[k].Item1;
                    double hold = lx + 8;
                    if (this.x > hold && this.checkpoint < hold)
                    {
                        this.checkpoint = hold;
                        this.checkpointT = 0;
                        this.audio.checkpoint();
                        this.shake = Math.Max(this.shake, 0.35);
                        this.haptic(new double[] { 25, 40, 25 });
                        this.dustBurst(this.x, 18);
                        // gold sparks thrown up off the stele as its flame catches
                        for (int i = 0; i < 14; i++)
                        {
                            this.particles.Add(new Particle
                            {
                                wx = lx + 46 + (Js.random() - 0.5) * 8,
                                wy = Levels.terrainAt(this.level, lx + 46) + 52,
                                vx = (Js.random() - 0.5) * 90,
                                vy = -60 - Js.random() * 90,
                                life = 0,
                                maxLife = 0.7 + Js.random() * 0.6,
                                size = (1.2 + Js.random() * 1.4) * this.scale,
                                kind = "spark",
                            });
                        }
                        this.stuckT = 0;
                        if (this.kickT >= 0) this.kickLearned = true;
                    }
                }
            }
            if (this.x < this.checkpoint)
            {
                this.x = this.checkpoint;
                if (this.vx < 0) this.vx = 0;
            }
        }

        /// <summary>
        /// The sounds that follow the body and the ground rather than events: his
        /// breathing, which quickens and roughens as he works and as his breath runs
        /// low; gravel cracking under the stone in proportion to its speed; and the
        /// wind, which rises with the same gusts that lean the grass over.
        /// </summary>
        private void updateSoundscape(double dt)
        {
            if (!this.active) return;
            bool playing = this.phase == "playing";
            bool pushing = playing && this.input > 0.15 && this.kickT < 0;
            double work = Math.Max(
                Math.Max(
                    Math.Max(pushing ? 0.5 + this.input * 0.3 : 0, this.kickT >= 0 ? 0.8 : 0),
                    Math.Max(this.slipT > 0 ? 1 : 0, 1 - this.breath)),
                this.phase == "summit" ? 0.6 : 0);
            double rate = 1.4 + work * 3.2; // radians/s of the breath cycle
            this.breathPh += rate * dt;
            if (this.breathPh >= Math.PI)
            {
                this.breathPh -= Math.PI;
                this.breathIn = !this.breathIn;
                if (work > 0.3 && this.phase != "descent")
                    this.audio.breathe(this.breathIn, Math.Min(1, work));
            }

            double speed = Math.Abs(this.vx);
            if (speed > 12 && this.hop == 0 && this.phase != "descent")
            {
                this.gritT -= dt;
                if (this.gritT <= 0)
                {
                    // more grains the faster it goes, each one random
                    this.gritT = Math.Max(0.025, 0.4 - speed / 500) * (0.4 + Js.random());
                    this.audio.grit(Math.Min(1, speed / 400));
                }
            }

            this.windT -= dt;
            if (this.windT <= 0)
            {
                this.windT = 0.25;
                this.audio.setWind(Math.Min(1, Math.Abs(this.windAt(this.x)) / 0.3));
                this.audio.setRain(this.weather == "storm" ? 1 : this.weather == "rain" ? 0.65 : 0);
            }
        }

        /*
         * Quality that adapts to the device. The detail added for realism — the
         * textured hillside, the layered rim light on the man, dense grass, dust in
         * the light — is cheap on a desktop GPU and can be too much for an old
         * phone. If drawing a frame keeps costing more than about 14 ms, the
         * expensive extras step down (and stay down for the session); nothing that
         * matters to playing is ever removed.
         */
        private bool lowQuality = false;
        private double costSum = 0;
        private double costN = 0;
        private double costSince = 0;
        /// <summary>
        /// Smoothness first. Every two seconds the average frame cost is checked;
        /// over budget, the canvas resolution steps down (a slightly softer picture
        /// is far less noticeable than a stutter), and only once it is as low as it
        /// goes does the expensive lighting switch off. Well under budget for a long
        /// while, the resolution creeps back up.
        /// </summary>
        private double resScale = 1;
        /// <summary>how many times the resolution has had to come down this session</summary>
        private int stepsDown = 0;
        // PORT: `ms` is the CPU time render() took to record the frame; with a GPU backend
        // the drawing itself happens later, so this measures less than the browser did.
        /*
         * On phones this used to see-saw: a slow stretch (the first seconds while
         * everything warms up, a scene change, the phone warming up) stepped the
         * resolution down, and after enough of them switched the painted lighting,
         * the textured hill and the soft clouds off for the rest of the session. The
         * picture visibly changed half way through a climb, and each change of
         * resolution reallocated the frame and every cached layer: a freeze, sometimes
         * a black frame. Now the look never changes once the game has started; only
         * the resolution may come down, twice at most, never back up, and the first
         * seconds after the game comes up are not counted.
         */
        private double watchFrom = -1;
        private void watchFrameCost(double ms, double now, double tooSlow, double easy)
        {
            if (!this.active) { this.watchFrom = -1; return; }
            if (this.watchFrom < 0) this.watchFrom = now + 8000;
            if (now < this.watchFrom || this.stepsDown >= 2) return;
            if (this.costSince == 0) this.costSince = now;
            this.costSum += ms;
            this.costN++;
            if (now - this.costSince > 3000)
            {
                double avg = this.costSum / this.costN;
                if (avg > tooSlow && this.resScale > 0.8)
                {
                    this.resScale = Math.Max(0.75, this.resScale - 0.125);
                    this.stepsDown++;
                    this.resize();
                    // the new size settles before it is judged
                    this.watchFrom = now + 5000;
                }
                this.costSum = 0;
                this.costN = 0;
                this.costSince = 0;
            }
        }

        /// <summary>a short buzz on phones that have one; silently nothing everywhere else</summary>
        private void haptic(double pattern)
        {
            if (!this.active || !this.haptics) return;
            Native.vibrate(pattern);
        }

        private void haptic(double[] pattern)
        {
            if (!this.active || !this.haptics) return;
            Native.vibrate(pattern);
        }

        /// <summary>a cloud of grit thrown up from the ground at world x</summary>
        private void dustBurst(double wx, double n)
        {
            for (int i = 0; i < n; i++)
            {
                double a = -Math.PI * Js.random();
                double v = 30 + Js.random() * 110;
                this.particles.Add(new Particle
                {
                    wx = wx + (Js.random() - 0.5) * 50,
                    wy = Levels.terrainAt(this.level, wx) + 2,
                    vx = Math.Cos(a) * v,
                    vy = Math.Sin(a) * v * 0.6,
                    life = 0,
                    maxLife = 0.6 + Js.random() * 0.7,
                    size = (1.6 + Js.random() * 2.6) * this.scale,
                    kind = "dust",
                });
            }
        }

        private void updateBirds(double dt)
        {
            double w = this.w;
            for (int i = 0; i < this.birds.Count; i++)
            {
                var b = this.birds[i];
                b.ox += b.speed * dt;
                b.wander += dt * b.bobFreq;
                b.bank += dt * 0.4;
                if (b.glide > 0)
                {
                    b.glide -= dt;
                    // wings settle to a shallow dihedral rather than freezing mid-stroke
                    double target = Math.PI * 0.62;
                    b.flap += (target - (b.flap % (Math.PI * 2))) * Math.Min(1, dt * 6);
                    if (b.glide <= 0) b.nextGlide = 0.8 + Js.random() * 2.4;
                }
                else
                {
                    b.flap += dt * b.flapSpeed;
                    b.nextGlide -= dt;
                    if (b.nextGlide <= 0) b.glide = 0.8 + Js.random() * 2.2;
                }
                double span = w / (this.scale * b.par);
                double px = w * 0.38 + b.ox * this.scale * b.par;
                if (px < -120) b.ox += span;
                else if (px > w + 120) b.ox -= span;
            }
            this.chirpT -= dt;
            if (this.chirpT <= 0)
            {
                this.chirpT = 5 + Js.random() * 6;
                this.audio.chirp(0.6 + Js.random() * 0.7, Js.random() < 0.45);
            }
        }

        private void updateParticles(double dt)
        {
            // compacted in place: a fresh list every frame for one that usually loses one
            // or two members is garbage for nothing
            int kept = 0;
            for (int i = 0; i < this.particles.Count; i++)
            {
                var p = this.particles[i];
                p.life += dt;
                if (p.life >= p.maxLife) continue;
                p.wx += p.vx * dt;
                p.wy += p.vy * dt;
                p.vy += 40 * dt; // gentle gravity settles the dust
                p.vx *= 1 - dt * 1.5;
                this.particles[kept++] = p;
            }
            if (kept < this.particles.Count) this.particles.RemoveRange(kept, this.particles.Count - kept);
        }

        private void emitParticles()
        {
            if (this.particles.Count > 160) this.particles.RemoveRange(160, this.particles.Count - 160);
            bool rolling = this.phase == "rolling";
            bool pushing = this.phase == "playing" && this.input > 0.15 && Math.Abs(this.vx) > 8;
            if (!rolling && !pushing) return;
            int rate = rolling ? 2 : 1;
            for (int i = 0; i < rate; i++)
            {
                if (Js.random() < 0.6) continue;
                double wx = this.x + (Js.random() - 0.5) * 34;
                this.particles.Add(new Particle
                {
                    wx = wx,
                    wy = Levels.terrainAt(this.level, wx) + 2,
                    vx = (Js.random() - 0.5) * 40 - this.vx * 0.12,
                    vy = -Js.random() * 30,
                    life = 0,
                    maxLife = 0.5 + Js.random() * 0.5,
                    size = (1.2 + Js.random() * 1.7) * this.scale,
                    kind = Js.random() < 0.2 ? "spark" : "dust",
                });
            }
        }

        private void renderParticles()
        {
            var ctx = this.ctx;
            double w = this.w;
            for (int i = 0; i < this.particles.Count; i++)
            {
                var p = this.particles[i];
                double t = p.life / p.maxLife;
                double px = (p.wx - this.camX) * this.scale;
                double py = this.groundY - (p.wy - this.camY) * this.scale * 0.55 + this.shakeY;
                if (px < -20 || px > w + 20 || py < -10 || py > this.h + 10) continue;
                ctx.GlobalAlpha = (1 - t) * (p.kind == "spark" ? 0.5 : 0.32);
                if (p.kind == "spark")
                {
                    ctx.FillStyle = "oklch(0.9 0.12 75)";
                    ctx.BeginPath();
                    ctx.Arc(px, py, p.size * 0.6, 0, Math.PI * 2);
                    ctx.Fill();
                }
                else
                {
                    ctx.FillStyle = "oklch(0.52 0.03 60)";
                    ctx.BeginPath();
                    ctx.Arc(px, py, p.size, 0, Math.PI * 2);
                    ctx.Fill();
                }
            }
            ctx.GlobalAlpha = 1;
        }
    }
}
