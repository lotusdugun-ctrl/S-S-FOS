using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    // engine.ts lines 5199–6861: drawFigureLit and drawFigure (Sisyphus himself).
    public partial class SisyphusEngine
    {
        // small value types for drawFigure's local closures (the TS returned object literals)
        private struct FigFoot
        {
            public double ax, ay, fa;
            public bool stance;
            public FigFoot(double ax, double ay, double fa, bool stance) { this.ax = ax; this.ay = ay; this.fa = fa; this.stance = stance; }
        }

        private struct FigPt
        {
            public double x, y;
            public FigPt(double x, double y) { this.x = x; this.y = y; }
        }

        private struct FigJoint
        {
            public double ex, ey, hx, hy;
            public FigJoint(double ex, double ey, double hx, double hy) { this.ex = ex; this.ey = ey; this.hx = hx; this.hy = hy; }
        }

        private struct FigKnee
        {
            public double kx, ky, ex, ey;
        }

        private static readonly double[] FIG_FINGER_LEN = { 2.7, 3.1, 3, 2.4 };
        private static readonly double[] FIG_ABS_BANDS = { 0.56, 0.42, 0.28 };
        private static readonly double[] FIG_FOLDS = { -5.5, -1, 3.5, 7.5 };

        /**
         * The man, with a rim light that is actually his edge.
         *
         * A rim stroke was tried and taken out long ago: hand-fitted curves never
         * sat on the outline of thirty overlapping, moving fills. This does it the
         * way a compositor would. He is painted into three depth layers — the far
         * leg and arm, the body, the near arm — and for each, the silhouette is
         * shifted a pixel or two away from the sun and cut out of itself. What is
         * left is exactly the sunward edge of whatever shape that layer is in this
         * frame, laid back over it in gold. Because the layers are separate, the
         * near arm gets its own edge where it crosses the chest, and throws a soft
         * shadow onto it: the limbs read as round things in front of a body instead
         * of one flat cut-out.
         */
        private void drawFigureLit(double x, double groundY, double scale, double ang, double bcx, double bcy, double R)
        {
            Ctx main = this.ctx;
            double dpr = this.dpr;
            double s = scale;
            double left = x - 110 * s;
            double top = groundY - 170 * s;
            double bw = 230 * s;
            double bh = 190 * s;
            int pw = (int)Math.Max(1, Math.Ceiling(bw * dpr));
            int ph = (int)Math.Max(1, Math.Ceiling(bh * dpr));
            Surface mk(Surface cv)
            {
                Surface c = cv ?? new Surface(pw, ph);
                if (c.width != pw || c.height != ph)
                {
                    c.SetSize(pw, ph);
                }
                return c;
            }
            for (int i = 0; i < 3; i++)
            {
                if (this.figLayers.Count <= i) this.figLayers.Add(mk(null));
                else this.figLayers[i] = mk(this.figLayers[i]);
            }
            Surface rim = (this.rimLayer = mk(this.rimLayer));
            Ctx[] ctxs = new Ctx[] { this.figLayers[0].GetContext(), this.figLayers[1].GetContext(), this.figLayers[2].GetContext() };
            Ctx rc = rim.GetContext();
            if (this.lowQuality)
            {
                this.drawFigure(x, groundY, scale, ang, bcx, bcy, R, this.kickT >= 0);
                return;
            }
            foreach (Ctx c in ctxs)
            {
                c.SetTransform(1, 0, 0, 1, 0, 0);
                c.GlobalCompositeOperation = Composite.SourceOver;
                c.ClearRect(0, 0, pw, ph);
                c.SetTransform(dpr, 0, 0, dpr, -left * dpr, -top * dpr);
            }
            this.figCtxs = ctxs;
            try
            {
                this.drawFigure(x, groundY, scale, ang, bcx, bcy, R, this.kickT >= 0);
            }
            finally
            {
                this.figCtxs = null;
                this.ctx = main;
            }

            // the sun's side, and how strongly it reaches him (the storm takes it away)
            var sun = this.lightScreen();
            // which side of him the rim is on follows the sun smoothly, never flips
            double dx = Math.Max(-1, Math.Min(1, (x - sun.x) / (this.w * 0.12)));
            double e = Math.Max(1.2, 1.5 * s * dpr);
            double storm = this.zeus.state == "gone" ? 0 : this.zeus.state == "fade" ? 0.5 : 1;
            /*
             * Under a high sun a crisp rim on the crown of his head and the tops of
             * his shoulders read as white flares on the skin. The higher the sun, the
             * more the light comes from above and spreads: the rim softens and the
             * colour of it is warmed into the skin instead of laid on as near-white.
             */
            double high = Math.Max(0, Math.Min(1, (sun.elev - 8) / 35));
            double strength = 0.7 * (1 - storm * 0.7) * Math.Min(1, sun.strength * 1.1) * (1 - high * 0.55);
            RGB warm = new RGB(
                sun.color[0] * 0.5 + 255 * 0.5,
                sun.color[1] * 0.5 + 186 * 0.5,
                sun.color[2] * 0.5 + 132 * 0.5);

            /** paint `src`'s silhouette in `color` into the scratch layer, minus a shifted copy */
            void edge(Surface src, Style color, double ox, double oy)
            {
                rc.SetTransform(1, 0, 0, 1, 0, 0);
                rc.GlobalCompositeOperation = Composite.SourceOver;
                rc.ClearRect(0, 0, pw, ph);
                rc.DrawImage(src, 0, 0);
                rc.GlobalCompositeOperation = Composite.SourceIn;
                rc.FillStyle = color;
                rc.FillRect(0, 0, pw, ph);
                rc.GlobalCompositeOperation = Composite.DestinationOut;
                rc.DrawImage(src, ox, oy);
            }
            /*
             * Form. He is painted now, not cut out, and a painted body is round: each
             * layer gets a broad soft light on the side facing the sun, a core shadow
             * turning away from it, and warm light bounced up off the ground under him.
             * All three come from the layer's own silhouette, shifted and blurred, so
             * they follow every pose without anyone having to place them.
             */
            double fy0 = groundY - 60 * s;
            double lx = sun.x - x;
            double ly = sun.y - fy0;
            double ln = Js.hypot(lx, ly);
            if (ln == 0) ln = 1;
            lx /= ln;
            ly /= ln;
            double K = 7 * s * dpr;
            /*
             * The soft light and shade are worked out at a fifth of the layer's size
             * and stretched back over it: the stretch is the blur. A blur filter on
             * three full-size layers, three times each, every frame, had made him
             * the most expensive thing on screen.
             */
            Surface lo = (this.figLo ??= new Surface(1, 1));
            int loW = (int)Math.Max(1, Math.Ceiling(pw / 5.0));
            int loH = (int)Math.Max(1, Math.Ceiling(ph / 5.0));
            if (lo.width != loW || lo.height != loH)
            {
                lo.SetSize(loW, loH);
            }
            Ctx lc = lo.GetContext();
            void soft(int i, Style color, double ox, double oy, double _blur)
            {
                Surface src = this.figLayers[i];
                Ctx c = ctxs[i];
                double k = (double)loW / pw;
                lc.SetTransform(1, 0, 0, 1, 0, 0);
                lc.GlobalCompositeOperation = Composite.SourceOver;
                lc.ClearRect(0, 0, loW, loH);
                lc.DrawImage(src, 0, 0, loW, loH);
                lc.GlobalCompositeOperation = Composite.SourceIn;
                lc.FillStyle = color;
                lc.FillRect(0, 0, loW, loH);
                lc.GlobalCompositeOperation = Composite.DestinationOut;
                lc.DrawImage(src, ox * k, oy * k, loW, loH);
                c.SetTransform(1, 0, 0, 1, 0, 0);
                c.GlobalCompositeOperation = Composite.SourceAtop;
                c.ImageSmoothingEnabled = true;
                // PORT: imageSmoothingQuality = "low" (the canvas default; Ctx has no such property)
                c.DrawImage(lo, 0, 0, pw, ph);
                c.GlobalCompositeOperation = Composite.SourceOver;
            }
            double keyA = (0.34 * strength + 0.05) * (1 - high * 0.3);
            double dim = Math.Max(0, Math.Min(0.62, (2 - this.st.elev) / 13));
            for (int i = 0; i < 3; i++)
            {
                double far = i == 0 ? 0.6 : 1;
                // the core shadow first, on the side turned from the light
                soft(i, Col.Rgba(22, 10, 16, Css.fixedNum(0.5 * far, 3)), lx * K * 1.4, ly * K * 1.4, K * 0.7);
                // the lit side, broad and soft
                soft(i, Css.rgb(warm, keyA * far), -lx * K, -ly * K, K * 0.55);
                // bounce from the sunlit ground: warm, from below
                soft(i, Col.Rgba(255, 168, 105, Css.fixedNum(0.2 * far * (1 - dim), 3)), 0, -K * 0.9, K * 0.5);
                // after dark the colour drains out of him, and what is left is moonlit blue
                if (dim > 0.01)
                {
                    Ctx c = ctxs[i];
                    c.SetTransform(1, 0, 0, 1, 0, 0);
                    c.GlobalCompositeOperation = Composite.SourceAtop;
                    c.FillStyle = Col.Rgba(12, 16, 34, Css.fixedNum(dim, 3));
                    c.FillRect(0, 0, pw, ph);
                    c.GlobalCompositeOperation = Composite.SourceOver;
                }
            }

            void rimOnto(int i, double a)
            {
                edge(this.figLayers[i], Css.rgb(warm, strength * a), dx * e, e * 0.55);
                Ctx c = ctxs[i];
                c.SetTransform(1, 0, 0, 1, 0, 0);
                c.GlobalCompositeOperation = Composite.SourceAtop;
                c.DrawImage(rim, 0, 0);
                c.GlobalCompositeOperation = Composite.SourceOver;
            }
            rimOnto(0, 0.55);
            rimOnto(1, 1);
            rimOnto(2, 1);

            // the near arm's shadow on the body: its silhouette, darkened, nudged away
            // from the sun and clipped to the body layer
            rc.SetTransform(1, 0, 0, 1, 0, 0);
            rc.GlobalCompositeOperation = Composite.SourceOver;
            rc.ClearRect(0, 0, pw, ph);
            rc.DrawImage(this.figLayers[2], 0, 0);
            rc.GlobalCompositeOperation = Composite.SourceIn;
            rc.FillStyle = "rgba(0, 0, 0, 0.55)";
            rc.FillRect(0, 0, pw, ph);
            Ctx body = ctxs[1];
            body.SetTransform(1, 0, 0, 1, 0, 0);
            body.GlobalCompositeOperation = Composite.SourceAtop;
            body.DrawImage(rim, -dx * e * 1.2, e * 0.8);
            body.GlobalCompositeOperation = Composite.SourceOver;

            foreach (Surface layer in this.figLayers) main.DrawImage(layer, left, top, bw, bh);
        }

        private void drawFigure(double x, double groundY, double scale, double ang, double bcx, double bcy, double R, bool kicking = false)
        {
            // Three depth layers when drawFigureLit provides them — the far limbs, the
            // body, the near arm — so each can get its own rim light and the near arm a
            // shadow on the chest. Without them everything lands on the one context.
            Ctx[] layers = this.figCtxs;
            Ctx ctx = layers != null ? layers[1] : this.ctx;
            void layer(int n)
            {
                if (layers == null || layers[n] == ctx) return;
                Mat2D m = ctx.GetTransform();
                ctx = layers[n];
                ctx.SetTransform(m);
            }
            double s = scale;
            /*
             * What he is doing, beyond walking and pushing:
             *  - heave: the half-second after a well-timed press, an explosive drive
             *  - slip: out of breath, the stone winning, being shoved back down
             *  - rest: braced with a shoulder in the stone, breathing
             *  - awe: at the summit, still holding it, looking up at the sky
             *  - left: the stone is gone; he stands at the top and watches it go
             */
            // at the ending he lets go of the stone, stands, and turns to the light
            // and at the very end, at the bottom, he stands beside his stone and looks out
            bool lettingGo =
                (this.finaleOn && this.finaleT > 1.2) ||
                (this.epiphany &&
                 this.phase == "summit" &&
                 this.phaseT > 1.4 &&
                 this.phaseT < EPIPHANY_STRIKE - 0.5);
            bool left = this.phase == "rolling" || this.phase == "descent" || lettingGo;
            double walking = this.phase == "descent" ? Math.Min(1, this.walkV / 100) : 0;
            bool awe = this.phase == "summit";
            /*
             * The strike. He used to keep pushing through a bolt of lightning, then
             * flip round on the spot in a single frame while the stone rolled straight
             * through his body. Now he flinches when it hits — recoils, ducks, throws
             * an arm up over his face — staggers as the stone goes, and only then
             * turns to watch it. The stone passes in front of him (see render), not
             * through him.
             */
            double struck = this.zeus.state == "strike" && awe ? Math.Min(1, this.zeus.t / 0.12) : 0;
            double stagger = this.phase == "rolling" ? Math.Max(0, 1 - this.phaseT / 0.5) : 0;
            double flinch = Math.Max(struck, stagger);
            // in a scene on the way down he turns back up the hill, to face her
            bool turned = left && stagger == 0 && !(this.cutscene && this.phase == "descent");
            double slip = this.phase == "playing" && this.slipT > 0 ? Math.Min(1, this.slipT / 0.4) : 0;
            var fx = this.heaveFx;
            double heave =
                this.phase == "playing" && fx != null && fx.q != "miss" && fx.t < 0.35
                    ? (1 - fx.t / 0.35) * (fx.q == "perfect" ? 1 : 0.6)
                    : 0;
            double resting =
                this.phase == "playing" && !kicking && slip == 0 && Math.Abs(this.input) <= 0.15
                    ? Math.Max(0, 1 - this.breath) * 0.6 + 0.4
                    : 0;
            bool pushing = this.phase == "playing" && !kicking;
            // holding: the stone giving ground (0..1), and him shoving it back (0..1)
            double giving = resting > 0 ? Math.Min(1, -this.creep / 6) : 0;
            double drive = resting > 0 ? this.holdDrive : 0;
            // How hard he is actually travelling, 0..1. Stride, bob, arm swing and the
            // hem of the tunic all scale off this, so standing still looks like standing
            // still instead of marching in place.
            // (once the stone is gone, its speed is not his: he is standing still)
            // stepping back from the stone into his stance at the start of a climb
            double backing =
                this.phase == "playing" && this.setT < SET_TIME
                    ? (Math.Abs(this.setFrom - (this.x - 64)) / SET_TIME) *
                      (1 - Math.Min(1, this.setT / SET_TIME))
                    : 0;
            double speed =
                this.phase == "descent"
                    ? this.walkV
                    : left
                        ? 0
                        : kicking
                            ? KICK_SPRINT
                            : Math.Max(Math.Abs(this.vx), backing);
            double effort = Math.Min(1, speed / 130);

            // Load is not the same as effort, and it is the one that shows. Leaning on
            // the stone and getting nowhere is the hardest he ever works: full input
            // against no speed. That is the moment the body has to look like it is
            // losing, so this peaks exactly there and falls away as the stone starts to
            // give.
            double load = Math.Max(
                Math.Max(
                    pushing
                        ? Math.Max(0, Math.Min(1, this.input)) * (1 - Math.Min(1, Math.Abs(this.vx) / 95))
                        : 0,
                    heave),
                Math.Max(
                    Math.Max(slip, awe ? 0.35 : 0),
                    // holding is work too: the muscles are never slack, and they spike as
                    // he drives the stone back to where it was
                    resting > 0 ? 0.4 + giving * 0.25 + drive * 0.6 : 0));
            // Capped lower than it was. Past a certain point the shoulders get far
            // enough ahead of the hips that the spine reads as horizontal, and a
            // horizontal spine with a head out front is a quadruped no matter how well
            // the legs are moving.
            double leanBase = left
                ? 0.12
                : kicking
                    ? 0.78
                    : slip > 0
                        ? 0.85 - slip * 0.5
                        : pushing || awe
                            ? 0.85 + load * 0.22 + heave * 0.2 + resting * 0.08 + drive * 0.18 - giving * 0.12
                            : 0.35;
            // recoiling from the bolt: upright, weight back on the heels
            double lean = leanBase * (1 - flinch) - 0.15 * flinch;
            // he sinks into it: hips drop and the knees take the weight
            double sink = (load * 7 + heave * 4 + resting * 3) * s;
            // where his head points: up at the god, down at the stone going away, bowed
            // over the stone while he gets his breath back
            double headTiltBase = awe
                ? -0.42
                : left
                    ? 0.3
                    : resting * 0.32 * (1 - drive * 0.7) + giving * 0.1 + slip * 0.2;
            // ducking from the flash
            /*
             * Walking down he is not a man staring at his feet. Every few steps he
             * lifts his head to the sky, now and then looks back over his shoulder at
             * the summit he has just left, and between times lets his gaze drift.
             */
            double pulse(double t, double period, double offset, double dur)
            {
                double l = (((t - offset) % period) + period) % period;
                if (l >= dur) return 0;
                double e = Math.Min(1, Math.Min(l / 0.45, (dur - l) / 0.45));
                return e * e * (3 - 2 * e);
            }
            bool descending = this.phase == "descent";
            double lookUp = descending ? pulse(this.phaseT, 7.3, 1.6, 2.4) : 0;
            double glance = descending ? pulse(this.phaseT, 9.7, 4.8, 1.9) : 0;
            double drift = descending ? Math.Sin(this.t * 0.8) * 0.06 + Math.Sin(this.t * 1.9) * 0.025 : 0;
            double walkTilt = 0.22 + drift - lookUp * 0.72 - glance * 0.3;
            double serene = this.saga.serenity;
            // at the ending he does not look up at the god: he looks out at the world
            double epi = this.epiphany && awe ? Math.Min(1, this.phaseT / 1.5) : 0;
            double headTiltCore = headTiltBase * (1 - epi) - 0.04 * epi - (pushing ? 0.1 * serene : 0);
            double headTilt = descending ? walkTilt : headTiltCore * (1 - flinch) + 0.45 * flinch;
            // turning the head about the neck: narrows through the turn, then faces back
            double headTurn = glance > 0 ? Math.Cos(Math.Min(1, glance * 1.15) * Math.PI) : 1;
            // the fine shake of a muscle held near its limit, too fast to count. Two of
            // them, out of phase, so the two sides of him do not shiver as one object.
            // the further he has come in his mind, the less he fights the stone: the
            // same work, done without the shaking
            double calmK = 1 - serene * 0.6;
            double tremble = Math.Sin(this.t * 31) * load * 0.9 * s * calmK;
            double shudder = Math.Sin(this.t * 23.4 + 1.7) * load * 0.75 * s * calmK;
            /**
             * Breathing. Shallow and fast when he is straining, deep and slow when he is
             * not, and hard and ragged at a sprint. It drives the chest, the shoulders and
             * how far his mouth is open — one number so they cannot fall out of step.
             */
            double breath = Math.Sin(
                this.t * (1.6 + load * 4.2 + (kicking ? 3.4 : 0) + resting * (1 - this.breath) * 3));
            // out of breath shows whether or not he is pushing right now
            double spent = this.phase == "playing" ? Math.Max(0, 1 - this.breath * 1.6) : 0;
            double winded = Math.Max(Math.Max(load, kicking ? 0.75 : 0), Math.Max(spent, this.slipT > 0 ? 1 : 0));

            // The body rides highest over the planted leg at mid-stance and dips as the
            // weight passes from one foot to the other: twice a stride, and it is the
            // dip, not the rise, that is quick.
            // (the rise and fall of the body over each step now comes from the legs: see the hip below)
            double bob = 0;

            /*
             * A man stands plumb, whatever the ground does. Tilting the whole figure
             * with the slope only worked while he faced uphill into the stone (where it
             * read as a lean into the work); turned round and walking down, the same
             * tilt laid him back against the hill like a man about to fall over. Once
             * the stone is gone he keeps only a trace of the slope, and his feet find
             * the ground instead: the rest of the slope goes into where each foot lands.
             */
            // (and pushing too: a man leaning into a stone on a slope leans against
            // gravity, not against the hill — tilting him with the ground took a
            // third of his lean away and stood him upright against the stone)
            double plumb = left ? 0.82 * (1 - flinch) : 0.92;
            double bodyAng = ang * (1 - plumb);
            // The ground under his feet, in his own frame. On screen the hill rises at
            // -0.55 x the terrain's slope (the world is squashed vertically), and `ang`
            // is half that slope; whatever is left of the body's tilt is taken off.
            double groundTilt = Math.Tan(Math.Atan(-1.1 * ang) - bodyAng) * (turned ? -1 : 1);
            ctx.Save();
            ctx.Translate(x, groundY - bob);
            ctx.Rotate(bodyAng);
            // he turns round to watch it go back down
            if (turned) ctx.Scale(-1, 1);

            /*
             * He is painted: sun-bronzed skin, with the light, shade and bounce laid
             * over it afterwards by drawFigureLit, and the muscle drawn in, because a
             * hero you are meant to watch for a thousand climbs has to be a body and
             * not a hole in the sky. (He was a backlit silhouette once; the only
             * gradient left from then is the faint lift toward his feet, where light
             * bouncing off the sunlit ground genuinely does reach him.)
             */
            bool painted = layers != null; // the drawn-in detail needs the layers to clip to
            LinearGradient body = ctx.CreateLinearGradient(0, -104 * s, 0, 0);
            body.AddColorStop(0, "oklch(0.5 0.085 50)");
            body.AddColorStop(0.62, "oklch(0.47 0.082 47)");
            body.AddColorStop(1, "oklch(0.42 0.075 44)");
            Style bodyS = body;
            /**
             * The limbs on his far side. Barely separated from the near ones — enough that
             * two crossing legs do not fuse into one slab, not so much that he reads as two
             * different colours of man.
             */
            Style bodyFar = "oklch(0.35 0.065 42)";
            /** The one thing he is wearing, and the only value break on him. */
            Style cloth = "oklch(0.63 0.035 80)";
            Style hairC = "oklch(0.17 0.025 40)";
            Style leather = "oklch(0.3 0.05 45)";
            /**
             * Muscle and bone drawn on top of a shape: only ever onto what is already
             * painted in that layer (source-atop), so a crease can never stray off his
             * body into the sky. (TS: ink(fn) — here inkBegin() ... inkEnd(), so no
             * closure is allocated per frame.)
             */
            bool inkBegin()
            {
                if (!painted) return false;
                ctx.Save();
                ctx.GlobalCompositeOperation = Composite.SourceAtop;
                ctx.LineCap = LineCap.Round;
                ctx.LineJoin = LineJoin.Round;
                return true;
            }
            void inkEnd() { ctx.Restore(); }
            void crease(double w0, double a = 0.3)
            {
                ctx.StrokeStyle = Col.Rgba(40, 16, 10, a);
                ctx.LineWidth = w0 * s;
            }
            void sheen(double w0, double a = 0.22)
            {
                // sweat on the skin: warm and low, never a white highlight
                ctx.StrokeStyle = Col.Rgba(236, 170, 120, Css.fixedNum(a * 0.6, 3));
                ctx.LineWidth = w0 * s;
            }

            // soft shadow under feet
            ctx.Save();
            ctx.GlobalAlpha = 0.3;
            ctx.FillStyle = "oklch(0.06 0 0)";
            ctx.BeginPath();
            ctx.Ellipse(0, -1 * s, 15 * s, 3.5 * s, 0, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Restore();

            // ---- legs: a real gait, one foot planted while the other swings ----
            // Through the stance half the foot stays down and travels backwards under
            // him; since the cycle is driven by distance covered, that plant holds still
            // against the ground instead of skating. Only the swing half lifts.
            /**
             * A limb as a tapered shape with a muscle belly on one side, not a round-capped
             * line of constant width. This is most of the difference between a stick man and
             * a body: a thigh is thick at the hip, swells at the quadriceps and narrows hard
             * above the knee, and a line cannot do any of that.
             *
             * `belly` is signed along +x, so a positive belly bulges forward (quadriceps,
             * biceps) and a negative one bulges back (hamstring, calf, triceps).
             */
            void seg(double ax, double ay, double bx, double by, double wa, double wb, double belly, double at, Style fill)
            {
                double dx = bx - ax;
                double dy = by - ay;
                double len = Js.hypot(dx, dy);
                if (len == 0) len = 1;
                // normal, flipped so +belly always means "toward the direction he faces"
                double nx = -dy / len;
                double ny = dx / len;
                if (nx < 0)
                {
                    nx = -nx;
                    ny = -ny;
                }
                double mx = ax + dx * at;
                double my = ay + dy * at;
                double wm = wa + (wb - wa) * at;
                // A quadratic through a wanted mid point D needs C = 2D − (P0+P2)/2.
                double fExtra = Math.Max(0, belly);
                double f0x = ax + nx * wa, f0y = ay + ny * wa;
                double f2x = bx + nx * wb, f2y = by + ny * wb;
                double fdx = mx + nx * (wm + fExtra), fdy = my + ny * (wm + fExtra);
                double fcx = 2 * fdx - 0.5 * (f0x + f2x), fcy = 2 * fdy - 0.5 * (f0y + f2y);
                double bExtra = Math.Max(0, -belly);
                double b0x = ax - nx * wa, b0y = ay - ny * wa;
                double b2x = bx - nx * wb, b2y = by - ny * wb;
                double bdx = mx - nx * (wm + bExtra), bdy = my - ny * (wm + bExtra);
                double bcx2 = 2 * bdx - 0.5 * (b0x + b2x), bcy2 = 2 * bdy - 0.5 * (b0y + b2y);
                ctx.FillStyle = fill;
                ctx.BeginPath();
                ctx.MoveTo(f0x, f0y);
                ctx.QuadraticCurveTo(fcx, fcy, f2x, f2y);
                ctx.LineTo(b2x, b2y);
                ctx.QuadraticCurveTo(bcx2, bcy2, b0x, b0y);
                ctx.ClosePath();
                ctx.Fill();
                // joints, so the segments meet in a rounded socket instead of a corner
                ctx.BeginPath();
                ctx.Arc(ax, ay, wa, 0, Math.PI * 2);
                ctx.Fill();
                ctx.BeginPath();
                ctx.Arc(bx, by, wb, 0, Math.PI * 2);
                ctx.Fill();
            }

            /**
             * A line along a limb: a muscle's edge, a bone under the skin. `off` is how
             * far from the axis, in limb widths, toward the way he faces (+) or away;
             * `bow` bends it outward like the belly it runs along.
             */
            void along(double ax, double ay, double bx, double by, double off, double t0, double t1, double bow)
            {
                double dx = bx - ax;
                double dy = by - ay;
                double len = Js.hypot(dx, dy);
                if (len == 0) len = 1;
                double nx = -dy / len;
                double ny = dx / len;
                if (nx < 0)
                {
                    nx = -nx;
                    ny = -ny;
                }
                double p0x = ax + dx * t0 + nx * off;
                double p0y = ay + dy * t0 + ny * off;
                double p1x = ax + dx * t1 + nx * off;
                double p1y = ay + dy * t1 + ny * off;
                double tm = (t0 + t1) / 2;
                double cx = ax + dx * tm + nx * (off + bow);
                double cy = ay + dy * tm + ny * (off + bow);
                ctx.BeginPath();
                ctx.MoveTo(p0x, p0y);
                ctx.QuadraticCurveTo(cx, cy, p1x, p1y);
                ctx.Stroke();
            }

            // the spine's lean from vertical, and the hips going back to balance it: a
            // man pushing hard does not fold over his feet, he lays his whole body out
            // behind his hands
            double theta = left
                ? 0.04 - flinch * 0.08
                : lean * (pushing && !awe ? 0.8 : kicking ? 0.68 : 0.45) + heave * 0.1 - (awe ? 0.12 : 0);
            // leaning into the stone, his hips go back to balance him, far enough that
            // his arms reach it nearly straight instead of folding up against it
            double HX = -theta * (pushing && !awe ? 21 : 14) * s;
            // A grown man's proportions: the hip at a little over half his height, the
            // legs as long as the rest of him. (At 24 and 22 the legs were a child's,
            // and every stride looked like a waddle.)
            double THIGH = 27 * s;
            double SHIN = 24.5 * s;

            /*
             * The legs, rebuilt from how a body actually moves.
             *
             * Each foot spends part of the cycle on the ground and the rest in the
             * air. On the ground it does not move against the hill: it slides back
             * under him at exactly the speed he goes forward, because the stride he
             * takes and the rate the cycle turns at come from the same number
             * (strideFor) — so a planted foot is planted. Walking it strikes with the
             * heel, rolls flat, and peels off the heel onto the ball of the foot;
             * pushing he is up on the balls of his feet the whole time, the stride
             * short and set back behind his hips, where the drive comes from; at a
             * run he is on the forefoot and both feet leave the ground.
             *
             * The hips are not placed: they go as high as the legs on the ground let
             * them. Over a planted leg the hip rides up, between steps it drops — the
             * rise and fall of a real gait comes out of the geometry instead of being
             * painted on with a sine wave.
             */
            string mode = kicking
                ? "run"
                : this.phase == "descent" || left
                    ? "walk"
                    : "push";
            double STRIDE = strideFor(mode, speed, 2 * ang) * s;
            double beta = mode == "run" ? 0.38 : mode == "walk" ? 0.6 : 0.7;
            // the feet travel along his own (tilted) frame, the hill along the level
            double slopeK = 1 / Math.Max(0.5, Math.Cos(bodyAng));
            double half = (beta * STRIDE * slopeK) / 2;
            // where the stride sits under him
            double centre =
                mode == "push"
                    ? HX - (9 + load * 7) * s - 0.1 * STRIDE
                    : mode == "run"
                        ? -0.06 * STRIDE
                        // going down he puts each foot down nearer under him: reaching far
                        // out to a foot that lands well below, he had to drop onto it
                        : (0.05 - 0.22 * Math.Min(0.6, Math.Max(0, groundTilt))) * STRIDE;
            double TOE = 7.5 * s;
            double HEEL = 2.6 * s;
            // standing still the swing never starts: a foot in the air comes down
            double moving = Math.Min(1, speed / 22);
            double liftH = (mode == "run" ? 12 : mode == "walk" ? 6.5 : 3.6) * s * moving;
            /** where a foot is at a point of its cycle: the sole under the ankle, its angle, and whether it bears weight */
            FigFoot footAt(double phase)
            {
                double ph = (((phase / (Math.PI * 2)) % 1) + 1) % 1;
                // toe down (+) / toe up (-), and how the sole meets the ground
                double ax;
                double ay;
                double fa;
                bool stance = false;
                if (ph < beta)
                {
                    stance = true;
                    double u = ph / beta;
                    double gx = centre + half * (1 - 2 * u);
                    if (mode == "walk")
                    {
                        fa = u < 0.12 ? -0.24 * (1 - u / 0.12) : u < 0.6 ? 0 : ((u - 0.6) / 0.4) * 0.8;
                    }
                    else if (mode == "run")
                    {
                        fa = u < 0.45 ? 0.12 : 0.12 + ((u - 0.45) / 0.55) * 0.75;
                    }
                    else
                    {
                        // up on the ball of the foot, more so as the leg goes back and drives
                        fa = 0.22 + load * 0.12 + u * (0.45 + load * 0.15);
                    }
                    if (fa >= 0)
                    {
                        // the toes stay where they are; the heel comes up off them
                        double toeX = gx + TOE;
                        ax = toeX - TOE * Math.Cos(fa);
                        ay = -TOE * Math.Sin(fa);
                    }
                    else
                    {
                        // heel strike: the heel on the ground, the toes still up
                        double heelX = gx - HEEL;
                        ax = heelX + HEEL * Math.Cos(fa);
                        ay = HEEL * Math.Sin(fa);
                    }
                }
                else
                {
                    double v = (ph - beta) / (1 - beta);
                    double e = v * v * (3 - 2 * v);
                    // the knee folds and the heel comes up behind first; then the foot
                    // swings through low and reaches out
                    double lift = Math.Sin(Math.PI * Math.Pow(v, 0.62));
                    double endA = mode == "walk" ? 0.8 : mode == "run" ? 0.87 : 0.67 + load * 0.27;
                    double startA = mode == "walk" ? -0.24 : mode == "run" ? 0.12 : 0.22 + load * 0.12;
                    fa = endA + (startA - endA) * Math.Min(1, v * 1.25);
                    double x0 = centre - half + TOE * (1 - Math.Cos(endA));
                    double x1 = centre + half;
                    ax = x0 + (x1 - x0) * e;
                    ay = -TOE * Math.Sin(endA) * (1 - e) - lift * liftH;
                }
                // on a slope (walking down, standing plumb) the ground is not level under him
                ay += ax * groundTilt;
                return new FigFoot(ax, ay, fa, stance);
            }
            // sliding back, or stepping back into his stance, the cycle runs backwards
            double dir = slip > 0 || backing > 0 ? -1 : 1;
            FigFoot footN = footAt(this.gait * dir);
            FigFoot footF = footAt((this.gait + Math.PI) * dir);
            /*
             * Braced. When the stone stops moving — he is holding it, getting his
             * breath back against it, or driving with everything into a stone that
             * will not go — a man does not stand with his feet together under him.
             * He splits his stance: the front foot flat under his hips taking the
             * weight, the back leg long and straight behind him, up on the ball of
             * the foot with the heel off the ground, driving. Whichever foot was
             * ahead when he stopped becomes the front one.
             */
            // (and at the top, still holding it while he looks up at the god)
            double brace = pushing || (awe && !left) ? (1 - moving) * (1 - flinch) : 0;
            if (brace > 0.001)
            {
                bool nFront = footN.ax >= footF.ax;
                FigFoot braced(bool front)
                {
                    // the feet are behind his hips, so the whole of him — heel, hip,
                    // shoulder, hands — makes one long line into the stone
                    double gx = HX + (front ? (-3 - giving * 1.5) * s : (-26 + giving * 3.5 - drive * 2) * s);
                    double fa = front ? 0.1 + load * 0.14 : 0.7 + load * 0.22;
                    double ax = gx + TOE - TOE * Math.Cos(fa);
                    return new FigFoot(ax, -TOE * Math.Sin(fa) + ax * groundTilt, fa, true);
                }
                FigFoot mix(FigFoot f, FigFoot b)
                {
                    return new FigFoot(
                        f.ax + (b.ax - f.ax) * brace,
                        f.ay + (b.ay - f.ay) * brace,
                        f.fa + (b.fa - f.fa) * brace,
                        f.stance || brace > 0.5);
                }
                footN = mix(footN, braced(nFront));
                footF = mix(footF, braced(!nFront));
            }
            /*
             * Standing — at the top watching the stone go, stopped on the way down to
             * talk to her: both feet come down flat, a little apart, the weight on
             * them both. (Stopped in the middle of a step, the swinging foot used to
             * stay in the air, as if he were standing on a stair.)
             */
            double standing = brace > 0.001 ? 0 : 1 - moving;
            if (standing > 0.001)
            {
                bool nFront = footN.ax >= footF.ax;
                FigFoot stood(bool front)
                {
                    double ax = HX + (front ? 4.5 : -4.5) * s;
                    return new FigFoot(ax, ax * groundTilt, 0, true);
                }
                FigFoot settle(FigFoot f, FigFoot b)
                {
                    return new FigFoot(
                        f.ax + (b.ax - f.ax) * standing,
                        f.ay + (b.ay - f.ay) * standing,
                        f.fa + (b.fa - f.fa) * standing,
                        f.stance || standing > 0.5);
                }
                footN = settle(footN, stood(nFront));
                footF = settle(footF, stood(!nFront));
            }
            double LEG = THIGH + SHIN;
            double straight =
                mode == "walk"
                    ? 0.965 - (this.phase == "descent" ? 0.015 : 0)
                    : mode == "run"
                        ? 0.92
                        : 0.95 - load * 0.05 - resting * 0.03 - heave * 0.03;
            // the ankle joint, a little above the sole and turning with the foot
            FigPt ankle(FigFoot f)
            {
                return new FigPt(f.ax + 2.2 * s * Math.Sin(f.fa), f.ay - 2.2 * s * Math.Cos(f.fa));
            }
            FigPt aN = ankle(footN);
            FigPt aF = ankle(footF);
            double hipTarget = double.NegativeInfinity;
            if (mode == "walk")
            {
                /*
                 * Walking, the body is carried the way a body is: an inverted pendulum.
                 * Over each planted foot the hip rides up the arc of the leg and comes
                 * down again as the weight passes to the next foot, a few centimetres
                 * each way. The height it rides at is measured from the ground under
                 * the hips themselves, which runs smoothly down the hill; measuring it
                 * from whichever foot happened to be planted made him drop a whole
                 * step's height at every step on a slope, and walking down he hopped.
                 * Each foot's pull on the hip fades in as it lands and out as it peels
                 * off, so nothing changes in a single frame. Going down a slope he
                 * also bends more at the knee and shortens his step (see strideFor),
                 * as people do, and the legs never have to stretch past their length.
                 */
                double L = LEG * straight;
                double gHip = HX * groundTilt;
                double Lb = L - Math.Min(6 * s, Math.Abs(groundTilt) * 9 * s);
                double hold = Math.Max(brace, standing);
                double pend = 0, wsum = 0;
                for (int fi = 0; fi < 2; fi++)
                {
                    FigPt aj = fi == 0 ? aN : aF;
                    double ph = ((((this.gait + (fi == 0 ? 0 : Math.PI)) * dir / (Math.PI * 2)) % 1) + 1) % 1;
                    double wgt = ph < beta ? Math.Sin(Math.PI * ph / beta) : 0;
                    wgt = wgt * (1 - hold) + hold;
                    double dx = aj.x - HX;
                    double drop = L - Math.Sqrt(Math.Max(0, L * L - dx * dx));
                    pend += drop * wgt;
                    wsum += wgt;
                }
                pend = wsum > 1e-6 ? pend / wsum : 0;
                hipTarget = gHip - Lb + pend * 0.85;
                // the legs on the ground can only reach so far: never higher than that.
                // A foot about to land counts already, fading in over the end of its
                // swing: switched on at the moment of landing, the reach of the front
                // foot (far out and well below him on a slope) dropped the hips a whole
                // three units in one frame at every step.
                for (int fi = 0; fi < 2; fi++)
                {
                    FigFoot f = fi == 0 ? footN : footF;
                    FigPt aj = fi == 0 ? aN : aF;
                    double reachW = 1;
                    if (!f.stance)
                    {
                        double ph = ((((this.gait + (fi == 0 ? 0 : Math.PI)) * dir / (Math.PI * 2)) % 1) + 1) % 1;
                        double v = ph < beta ? 1 : (ph - beta) / (1 - beta);
                        double r = Math.Max(0, Math.Min(1, (v - 0.15) / 0.85));
                        reachW = r * r * (3 - 2 * r) * (1 - hold) + hold;
                        if (reachW <= 0) continue;
                    }
                    double dx = aj.x - HX;
                    double reach = LEG * 0.995;
                    double lim = aj.y - Math.Sqrt(Math.Max(0, reach * reach - dx * dx));
                    // a soft maximum: the limit takes over gradually, never with a corner
                    double k = 1.6 * s;
                    double capped = (hipTarget + lim + Math.Sqrt((hipTarget - lim) * (hipTarget - lim) + k * k)) / 2;
                    hipTarget += (capped - hipTarget) * reachW;
                }
            }
            else
            {
                /*
                 * Pushing and running the hips go as high as the legs on the ground let
                 * them — but a foot's say in it fades in as it comes down and out as it
                 * peels off the ground. Taking the lowest of the planted legs outright,
                 * the back leg (driving, far behind him) pulled the hips down eight units
                 * over a step and let go of them in a single frame as it lifted: a hop
                 * at every step. Fading, the hips rise before the foot leaves and he
                 * comes up onto his toes instead.
                 */
                double hold = Math.Max(brace, standing);
                double rFree = LEG * straight;
                // in the air, mid-stride (at a run): legs a little bent under him
                hipTarget = HX * groundTilt - rFree * (mode == "run" ? 0.97 : 1);
                if (mode == "run")
                {
                    // a runner is lowest at mid-stance, the knee taking his weight, and
                    // highest in the air: the bounce comes from the foot on the ground
                    double give = 0;
                    for (int fi = 0; fi < 2; fi++)
                    {
                        double ph = ((((this.gait + (fi == 0 ? 0 : Math.PI)) * dir / (Math.PI * 2)) % 1) + 1) % 1;
                        if (ph < beta) give = Math.Max(give, Math.Sin(Math.PI * ph / beta));
                    }
                    hipTarget += give * 4.5 * s * moving;
                }
                for (int fi = 0; fi < 2; fi++)
                {
                    FigFoot f = fi == 0 ? footN : footF;
                    FigPt aj = fi == 0 ? aN : aF;
                    double ph = ((((this.gait + (fi == 0 ? 0 : Math.PI)) * dir / (Math.PI * 2)) % 1) + 1) % 1;
                    double w;
                    if (ph < beta)
                    {
                        double u = ph / beta;
                        double a = Math.Min(1, u / 0.12), b = Math.Min(1, (1 - u) / 0.3);
                        a = a * a * (3 - 2 * a);
                        b = b * b * (3 - 2 * b);
                        w = a * b;
                    }
                    else
                    {
                        double v = (ph - beta) / (1 - beta);
                        double r0 = Math.Max(0, Math.Min(1, (v - 0.7) / 0.3));
                        w = r0 * r0 * (3 - 2 * r0) * 0.6;
                    }
                    w = f.stance && hold > 0.5 ? 1 : w * (1 - hold) + (f.stance ? hold : 0);
                    if (w <= 0) continue;
                    double dx = aj.x - HX;
                    // pushing, the leg out behind him is the one driving: it goes straight
                    double back = mode == "push" ? Math.Max(0, Math.Min(1, (-dx - 8 * s) / (8 * s))) : 0;
                    double r = LEG * (straight + (Math.Max(straight, 0.975) - straight) * back);
                    double lim = aj.y - Math.Sqrt(Math.Max(0, r * r - dx * dx));
                    double k = 1.2 * s;
                    double capped = (hipTarget + lim + Math.Sqrt((hipTarget - lim) * (hipTarget - lim) + k * k)) / 2;
                    hipTarget += (capped - hipTarget) * w;
                }
            }
            if (double.IsInfinity(hipTarget) || double.IsNaN(hipTarget)) hipTarget = -LEG * straight * 0.97; // in the air, mid-stride
            // (a little smoothing, so the hand-over from one foot to the other is a dip, not a kink)
            double hk = Math.Min(1, this.frameDt * 22);
            this.hipSm =
                Math.Abs(this.hipSm - hipTarget) > 20 * s && mode != "walk"
                    ? hipTarget
                    : this.hipSm + (hipTarget - this.hipSm) * hk;
            double hipY0 = this.hipSm;
            /*
             * A planted foot the hips have risen out of reach of — the back foot at the
             * end of a pushing step, now that the hips no longer wait for it to lift —
             * does not leave the ground: the heel comes up and he rolls onto his toes
             * until the leg reaches again. (Left alone, the ankle hung at the end of
             * the shin and the whole foot floated a few units over the ground.)
             */
            FigFoot onToes(FigFoot f)
            {
                if (!f.stance || f.fa < 0) return f;
                double reach = THIGH + SHIN - 0.6 * s;
                FigPt a0 = ankle(f);
                if (Js.hypot(a0.x - HX, a0.y - hipY0) <= reach) return f;
                double tx = f.ax + TOE * Math.Cos(f.fa), ty = f.ay + TOE * Math.Sin(f.fa);
                var g = f;
                for (double fa = f.fa + 0.04; fa <= 1.35; fa += 0.04)
                {
                    g = new FigFoot(tx - TOE * Math.Cos(fa), ty - TOE * Math.Sin(fa), fa, true);
                    FigPt a1 = ankle(g);
                    if (Js.hypot(a1.x - HX, a1.y - hipY0) <= reach) break;
                }
                return g;
            }
            footN = onToes(footN);
            footF = onToes(footF);
            aN = ankle(footN);
            aF = ankle(footF);

            /** Two-bone solve, the knee always breaking forward. */
            FigKnee knee(double fx2, double fy2)
            {
                double dx = fx2 - HX;
                double dy = fy2 - hipY0;
                double raw = Js.hypot(dx, dy);
                if (raw == 0) raw = 1;
                double d = Math.Min(raw, THIGH + SHIN - 0.4 * s);
                double ux = dx / raw;
                double uy = dy / raw;
                double a = (THIGH * THIGH - SHIN * SHIN + d * d) / (2 * d);
                double h = Math.Sqrt(Math.Max(0, THIGH * THIGH - a * a));
                double px = -uy >= 0 ? -uy : uy;
                double py = -uy >= 0 ? ux : -ux;
                FigKnee kn;
                kn.kx = HX + ux * a + px * h;
                kn.ky = hipY0 + uy * a + py * h;
                kn.ex = HX + ux * d;
                kn.ey = hipY0 + uy * d;
                return kn;
            }

            void leg(FigFoot f, FigPt aj, bool front, Style shade)
            {
                // the quads shake under a load they cannot move
                double jitter = (front ? tremble : shudder) * 0.6;
                FigKnee k = knee(aj.x, aj.y + jitter * 0.2);
                double kx = k.kx + jitter;
                double ky = k.ky;
                // out of reach (in the air at a run), the ankle hangs at the end of the shin
                double jx = k.ex;
                double jy = k.ey;
                // thigh: heavy at the hip, quadriceps forward, narrow above the knee
                seg(HX, hipY0, kx, ky, 7.6 * s, 4.6 * s, (2 + load * 1.7) * s, 0.42, shade);
                // shin: calf belly behind, ankle thin
                seg(
                    kx,
                    ky,
                    jx,
                    jy,
                    4.4 * s,
                    2.5 * s,
                    -(1.7 + load * 1.1 + (f.fa > 0.3 ? 0.6 : 0)) * s,
                    0.34,
                    shade);
                if (inkBegin())
                {
                    crease(0.75, 0.24 + load * 0.16);
                    along(HX, hipY0, kx, ky, 1.6 * s, 0.12, 0.86, 1.4 * s);
                    along(HX, hipY0, kx, ky, -2.4 * s, 0.2, 0.9, -1 * s);
                    sheen(1.6, 0.16 + load * 0.08);
                    along(HX, hipY0, kx, ky, 3.6 * s, 0.25, 0.7, 1.2 * s);
                    ctx.FillStyle = "rgba(255, 210, 165, 0.2)";
                    ctx.BeginPath();
                    ctx.Ellipse(kx + 2.4 * s, ky - 0.6 * s, 1.9 * s, 2.4 * s, 0, 0, Math.PI * 2);
                    ctx.Fill();
                    sheen(1, 0.2);
                    along(kx, ky, jx, jy, 2.6 * s, 0.12, 0.8, 0);
                    crease(0.7, 0.22 + load * 0.15);
                    along(kx, ky, jx, jy, -1.4 * s, 0.1, 0.6, -1.2 * s);
                    // the Achilles tendon standing out when he is up on his toes
                    if (f.fa > 0.3)
                    {
                        crease(0.6, 0.18 + load * 0.12);
                        along(kx, ky, jx, jy, -2 * s, 0.62, 0.98, -0.3 * s);
                    }
                    inkEnd();
                }
                // the foot: drawn from the sole under the ankle, turned by its angle
                ctx.Save();
                ctx.Translate(jx + (f.ax - aj.x), jy + (f.ay - aj.y));
                ctx.Rotate(f.fa);
                ctx.FillStyle = shade;
                // the foot itself, heel to toe, and the heel's round
                ctx.BeginPath();
                ctx.Ellipse(
                    ((TOE - HEEL) / 2) * 0.9,
                    -1.5 * s,
                    ((TOE + HEEL) / 2) * 0.95,
                    1.9 * s,
                    0,
                    0,
                    Math.PI * 2);
                ctx.Fill();
                ctx.BeginPath();
                ctx.Ellipse(-0.4 * s, -2.8 * s, 2.1 * s, 2 * s, 0, 0, Math.PI * 2);
                ctx.Fill();
                if (f.fa > 0.15)
                {
                    // the toes bent back flat against the ground, taking the weight
                    ctx.Save();
                    ctx.Translate(TOE - 1.2 * s, -0.8 * s);
                    ctx.Rotate(-f.fa * 0.85);
                    ctx.BeginPath();
                    ctx.Ellipse(1.4 * s, 0, 2.2 * s, 1.1 * s, 0, 0, Math.PI * 2);
                    ctx.Fill();
                    ctx.Restore();
                }
                {
                    ctx.FillStyle = leather;
                    ctx.BeginPath();
                    ctx.Ellipse(
                        ((TOE - HEEL) / 2) * 0.9,
                        0.2 * s,
                        ((TOE + HEEL) / 2) * 0.98,
                        0.85 * s,
                        0,
                        0,
                        Math.PI * 2);
                    ctx.Fill();
                    ctx.StrokeStyle = leather;
                    ctx.LineWidth = 1.1 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(-1.8 * s, -4.4 * s);
                    ctx.LineTo(1 * s, -3.4 * s);
                    ctx.MoveTo(1.8 * s, -3 * s);
                    ctx.LineTo(3.6 * s, -0.4 * s);
                    ctx.MoveTo(-2.6 * s, -2.2 * s);
                    ctx.LineTo(0.4 * s, -5.4 * s);
                    ctx.Stroke();
                }
                ctx.Restore();
            }

            // far leg first and slightly dimmed, so the two read as depth not as a blur.
            // Under load the far one is the leg driving out behind him.
            layer(0);
            leg(footF, aF, false, bodyFar);
            layer(1);
            leg(footN, aN, true, bodyS);

            // ---- torso: drawn in the spine's own frame, so a lean is a real lean ----
            /*
             * The trunk used to stay almost upright and slide its shoulders forward,
             * about sixteen degrees at the hardest push — a man leaning on a counter,
             * not one putting his weight through a rock. It now pivots at the hip. At
             * full load the spine is near thirty degrees from vertical, the back leg is
             * a straight line from heel to shoulder, and that diagonal is what reads as
             * effort before any detail does.
             */
            layer(1);
            double hipX = HX;
            double hipY = hipY0;
            double SPINE = 32 * s;
            double sinT = Math.Sin(theta);
            double cosT = Math.Cos(theta);
            /*
             * Pushing, the body surges with each step: the back leg drives, the chest
             * goes into the stone and the arms fold under it; the next foot lands,
             * the arms lock out again. With the hands fixed on the stone, that is what
             * makes the elbows work — and under a load he cannot move, the shoulders
             * ride up toward the ears.
             */
            double surge =
                pushing && !kicking
                    ? Math.Sin(this.gait * 2 - 0.6) * (1.2 + load * 1.4) * s * Math.Max(effort, load * 0.6)
                    : 0;
            double hunch = pushing ? (load * 1.6 + heave * 0.8) * s : 0;
            double shX = hipX + sinT * SPINE + tremble + surge;
            double shY = hipY - cosT * SPINE - hunch * 0.4;
            // The chest fills and empties. Shallow and quick under load, slow at rest.
            double chest = (1 + breath * 0.5) * s;
            // the lats flare and the back rounds under a load it cannot move
            double flare = (load * 1.6 + heave * 0.8) * s;

            ctx.Save();
            ctx.Translate(hipX + tremble * 0.3, hipY);
            ctx.Rotate(theta);
            double Ls = SPINE;
            ctx.FillStyle = bodyS;
            ctx.BeginPath();
            // back of the neck out along the trapezius
            ctx.MoveTo(-2 * s, -Ls - 6 * s);
            ctx.QuadraticCurveTo(-8 * s, -Ls - 4 * s, -10.5 * s, -Ls + 1 * s);
            // lat: wide under the armpit, drawn in hard at the waist
            ctx.QuadraticCurveTo(-13.5 * s - flare, -Ls * 0.6, -8 * s, -Ls * 0.24);
            // the small of the back, curving into the sacrum
            ctx.QuadraticCurveTo(-9.8 * s, -6 * s, -8.5 * s, 2 * s);
            ctx.LineTo(9 * s, 2 * s);
            // hip flexor up into the oblique
            ctx.QuadraticCurveTo(10 * s, -6 * s, 8.5 * s, -Ls * 0.32);
            // the belly and the arch of the ribs, breathing
            ctx.QuadraticCurveTo(12.5 * s + chest, -Ls * 0.58, 12.5 * s + chest, -Ls + 4 * s);
            // pectoral shelf into the front of the shoulder
            ctx.QuadraticCurveTo(12 * s, -Ls - 3 * s, 5 * s, -Ls - 6 * s);
            ctx.ClosePath();
            ctx.Fill();
            // glutes: a rounded mass behind the pelvis that the skirt drapes over
            // (low, behind the hip joint, under the belt — set higher it read as a
            // hump at the small of his back)
            ctx.BeginPath();
            ctx.Ellipse(-6.4 * s, 2.6 * s, 5.2 * s, 6.4 * s, 0.25, 0, Math.PI * 2);
            ctx.Fill();

            // Deltoids. A shoulder is a cap of muscle sitting over the joint, wider than
            // the arm it feeds, and it swells when the arm is loaded.
            // In profile there is one shoulder to see, capping the joint the arm
            // hangs from — not one at each edge of the chest, which put a hump on his
            // back. Behind it, the blade of the scapula lifts the line of the back.
            double delt = (5.4 + load * 1.2) * s;
            ctx.BeginPath();
            ctx.Ellipse(-5.5 * s, -Ls + 5 * s, 4.2 * s, 5.5 * s, -0.25, 0, Math.PI * 2);
            ctx.Fill();
            ctx.BeginPath();
            ctx.Ellipse(3.4 * s, -Ls + 2.2 * s, delt * 1.1, delt, 0.15, 0, Math.PI * 2);
            ctx.Fill();
            // trapezius, rising toward the neck — the muscle that shows when a man braces
            ctx.BeginPath();
            ctx.MoveTo(-8 * s, -Ls - 1 * s);
            ctx.QuadraticCurveTo(1 * s, -Ls - (7 + load * 1.5) * s, 9 * s, -Ls - 1 * s);
            ctx.QuadraticCurveTo(1 * s, -Ls + 2 * s, -8 * s, -Ls - 1 * s);
            ctx.Fill();

            /*
             * The anatomy of a man who has pushed a stone forever: the shelf of the
             * chest, the ridged belly, the serratus along the ribs, the long groove of
             * the spine between the back muscles and the blade of the scapula. All of
             * it sharpens as the load comes on — the body is a diagram of effort.
             */
            double def = 0.22 + load * 0.2;
            if (inkBegin())
            {
                // the deltoids, parted from the chest and the back
                crease(0.8, def);
                ctx.BeginPath();
                ctx.MoveTo(6.5 * s, -Ls - 3 * s);
                ctx.QuadraticCurveTo(4.6 * s, -Ls + 2 * s, 7.4 * s, -Ls + 6.8 * s);
                ctx.MoveTo(-6 * s, -Ls - 3 * s);
                ctx.QuadraticCurveTo(-4.4 * s, -Ls + 2.4 * s, -7.6 * s, -Ls + 6.4 * s);
                ctx.Stroke();
                // the lower edge of the pectoral
                crease(1.1, def + 0.06);
                ctx.BeginPath();
                ctx.MoveTo(4 * s, -Ls + 8.5 * s);
                ctx.QuadraticCurveTo(10 * s, -Ls + 11.5 * s, 12.6 * s + chest, -Ls + 6.5 * s);
                ctx.Stroke();
                // the belly: the line down the middle and the three bands across it
                crease(0.75, def * 0.85);
                ctx.BeginPath();
                ctx.MoveTo(10.2 * s + chest * 0.6, -Ls + 12.5 * s);
                ctx.QuadraticCurveTo(10.6 * s, -Ls * 0.4, 9 * s, -5 * s);
                foreach (double yy in FIG_ABS_BANDS)
                {
                    ctx.MoveTo(7 * s, -Ls * yy);
                    ctx.QuadraticCurveTo(
                        9.8 * s,
                        -Ls * yy - 1.3 * s,
                        11.8 * s + chest * 0.5,
                        -Ls * yy + 0.3 * s);
                }
                // serratus: the fingers of muscle over the ribs
                for (int i = 0; i < 3; i++)
                {
                    double yy = -Ls * (0.66 - i * 0.08);
                    ctx.MoveTo(3.4 * s, yy);
                    ctx.LineTo(6.2 * s, yy + 1.6 * s);
                }
                // the oblique into the hip
                ctx.MoveTo(4.5 * s, -Ls * 0.32);
                ctx.QuadraticCurveTo(8 * s, -4 * s, 8.6 * s, 0);
                ctx.Stroke();
                // the spine's groove between the long back muscles, and the scapula
                crease(1, def);
                ctx.BeginPath();
                ctx.MoveTo(-6.4 * s, -Ls + 3 * s);
                ctx.QuadraticCurveTo(-8.6 * s - flare * 0.5, -Ls * 0.5, -6.4 * s, -3 * s);
                ctx.MoveTo(-2 * s, -Ls + 3 * s);
                ctx.QuadraticCurveTo(-5.5 * s, -Ls + 7 * s, -3.4 * s, -Ls + 12 * s);
                ctx.Stroke();
                // the light along the top of the back and over the chest
                sheen(2.2, 0.16);
                ctx.BeginPath();
                ctx.MoveTo(-9 * s, -Ls + 2 * s);
                ctx.QuadraticCurveTo(-11 * s - flare, -Ls * 0.62, -8.4 * s, -Ls * 0.3);
                ctx.MoveTo(6 * s, -Ls + 4 * s);
                ctx.QuadraticCurveTo(9.5 * s, -Ls + 5.5 * s, 11.4 * s + chest, -Ls + 3 * s);
                ctx.Stroke();
                inkEnd();
            }

            /**
             * The only garment: a labourer's skirt knotted at the waist and falling to
             * mid-thigh, a touch lighter than the skin so the waist reads as a break.
             * It belongs to the pelvis, so it tips with it, and its hem answers to the
             * gait, because a hem that never moves is cardboard.
             */
            ctx.FillStyle = cloth;
            double hem(double i) { return Math.Sin(this.gait * 1.0 + i) * 1.8 * s * effort; }
            ctx.Rotate(-theta * 0.55);
            ctx.BeginPath();
            ctx.MoveTo(-9.5 * s, -4 * s);
            ctx.LineTo(10 * s, -4 * s);
            ctx.QuadraticCurveTo(11.5 * s, 6 * s, 9 * s, 12 * s + hem(0));
            ctx.LineTo(5 * s, 9 * s + hem(1.1));
            ctx.LineTo(1 * s, 13 * s + hem(2.2));
            ctx.LineTo(-3 * s, 9 * s + hem(3.3));
            ctx.LineTo(-8 * s, 12 * s + hem(4.4));
            ctx.QuadraticCurveTo(-13 * s, 4 * s, -9.5 * s, -4 * s);
            ctx.ClosePath();
            ctx.Fill();
            // folds in the linen, falling from the belt
            if (inkBegin())
            {
                ctx.StrokeStyle = "rgba(70, 50, 30, 0.32)";
                ctx.LineWidth = 0.9 * s;
                ctx.BeginPath();
                foreach (double fx0 in FIG_FOLDS)
                {
                    ctx.MoveTo(fx0 * s, -2.5 * s);
                    ctx.QuadraticCurveTo((fx0 + 0.8) * s, 4 * s, (fx0 + (hem(fx0) / s) * 0.3) * s, 10 * s);
                }
                ctx.Stroke();
                inkEnd();
            }
            // a knotted belt
            ctx.FillStyle = leather;
            ctx.FillRect(-9.5 * s, -5 * s, 19.5 * s, 2.4 * s);
            ctx.Restore();

            /*
             * Arms. Every pose uses the same two bones now. The braced arm used to be
             * drawn from its shoulder straight to the stone with the elbow placed on
             * the line between them, and the running arm from two short fixed bones —
             * so the moment he launched the stone his arms visibly shrank by a third.
             * One upper arm, one forearm, the same length everywhere, solved for the
             * elbow: an arm cannot change size, only fold.
             */
            // the arm from shoulder to wrist is a third of his height
            double UPPER = 16.5 * s;
            double FORE = 15 * s;
            FigJoint solve(double sx, double sy, double tx, double ty)
            {
                double dx = tx - sx;
                double dy = ty - sy;
                double d = Js.hypot(dx, dy);
                if (d == 0) d = 1;
                double reach = UPPER + FORE - 0.3 * s;
                if (d > reach)
                {
                    tx = sx + (dx / d) * reach;
                    ty = sy + (dy / d) * reach;
                    dx = tx - sx;
                    dy = ty - sy;
                    d = reach;
                }
                double a = (UPPER * UPPER - FORE * FORE + d * d) / (2 * d);
                double hh = Math.Sqrt(Math.Max(0, UPPER * UPPER - a * a));
                double ux = dx / d;
                double uy = dy / d;
                // the elbow breaks downward and back, never up
                double px = -uy;
                double py = ux;
                if (py < 0)
                {
                    px = -px;
                    py = -py;
                }
                return new FigJoint(sx + ux * a + px * hh, sy + uy * a + py * hh, tx, ty);
            }
            /** one arm from a shoulder to a hand already solved, with its muscle bellies */
            void drawArm(double sx, double sy, FigJoint j, Style shade, string hand)
            {
                // upper arm: triceps behind, biceps in front, thickening under load
                seg(
                    sx,
                    sy,
                    j.ex,
                    j.ey,
                    (4.8 + load * 1) * s,
                    (3.5 + load * 0.5) * s,
                    -(1.6 + load * 1.4) * s,
                    0.55,
                    shade);
                // forearm: the mass sits up near the elbow, the wrist is bone
                seg(
                    j.ex,
                    j.ey,
                    j.hx,
                    j.hy,
                    (3.6 + load * 0.6) * s,
                    2.2 * s,
                    (1.3 + load * 1) * s,
                    0.3,
                    shade);
                if (inkBegin())
                {
                    // biceps and triceps parted down the upper arm; the forearm's flexors
                    crease(0.75, 0.22 + load * 0.18);
                    along(sx, sy, j.ex, j.ey, -0.4 * s, 0.22, 0.85, 0.6 * s);
                    along(j.ex, j.ey, j.hx, j.hy, 0.6 * s, 0.08, 0.55, 0.8 * s);
                    sheen(1.4, 0.16 + load * 0.06);
                    along(sx, sy, j.ex, j.ey, 2.4 * s, 0.3, 0.75, 0.8 * s);
                    if (load > 0.3 && hand == "press")
                    {
                        // under a load he cannot move: the veins stand up along the forearm
                        // and the shoulder cap bunches
                        double k = (load - 0.3) / 0.7;
                        ctx.StrokeStyle = Col.Rgba(70, 30, 26, Css.fixedNum(0.32 * k, 3));
                        ctx.LineWidth = 0.55 * s;
                        along(j.ex, j.ey, j.hx, j.hy, 0.2 * s, 0.15, 0.85, 0.5 * s);
                        along(j.ex, j.ey, j.hx, j.hy, -0.8 * s, 0.25, 0.75, -0.4 * s);
                        sheen(1.2, 0.2 * k);
                        along(sx, sy, j.ex, j.ey, 0.6 * s, 0.02, 0.35, 1.6 * s);
                        crease(0.8, 0.3 * k);
                        along(sx, sy, j.ex, j.ey, -2.6 * s, 0.05, 0.4, -0.8 * s);
                    }
                    inkEnd();
                }
                drawHand(j, shade, hand);
            }
            /**
             * Hands with fingers. A hand was an ellipse — at this size a blob that
             * could have been a mitten or a club. Pressed on the stone the fingers now
             * spread across its surface with the thumb braced along the top, the way
             * a hand actually takes weight; at a run they close into fists with the
             * knuckles showing; hanging, they curl loosely. Drawn in the forearm's own
             * frame, +x pointing out of the wrist.
             */
            void drawHand(FigJoint j, Style shade, string hand)
            {
                void finger(double x0, double y0, double a, double len, double wdt, double curlA)
                {
                    // two phalanx segments; the tip turns by `curl`
                    double x1 = x0 + Math.Cos(a) * len * 0.55;
                    double y1 = y0 + Math.Sin(a) * len * 0.55;
                    double x2 = x1 + Math.Cos(a + curlA) * len * 0.45;
                    double y2 = y1 + Math.Sin(a + curlA) * len * 0.45;
                    ctx.LineWidth = wdt;
                    ctx.BeginPath();
                    ctx.MoveTo(x0, y0);
                    ctx.LineTo(x1, y1);
                    ctx.LineTo(x2, y2);
                    ctx.Stroke();
                }
                ctx.Save();
                ctx.FillStyle = shade;
                ctx.StrokeStyle = shade;
                ctx.LineCap = LineCap.Round;
                ctx.LineJoin = LineJoin.Round;
                ctx.Translate(j.hx, j.hy);
                ctx.Rotate(Math.Atan2(j.hy - j.ey, j.hx - j.ex));
                if (hand == "press")
                {
                    // palm flat on the stone, the heel of the hand driving
                    ctx.BeginPath();
                    ctx.Ellipse(1.6 * s, 0, 2.6 * s, 2.3 * s, 0, 0, Math.PI * 2);
                    ctx.Fill();
                    // four fingers fanned out across the surface, tips gripping
                    double spread = 0.12 + load * 0.08;
                    for (int i = 0; i < 4; i++)
                    {
                        double off = (i - 1.5) * 1.15 * s;
                        double a = (i - 1.5) * spread * 2.2;
                        finger(3.5 * s, off, a, FIG_FINGER_LEN[i] * s, 0.95 * s, 0.25 + load * 0.15);
                    }
                    // the thumb along the top, braced
                    finger(1.6 * s, -1.9 * s, -0.95, 2.6 * s, 1.15 * s, 0.35);
                }
                else if (hand == "fist")
                {
                    ctx.BeginPath();
                    ctx.Ellipse(1 * s, 0, 2.7 * s, 2.4 * s, 0, 0, Math.PI * 2);
                    ctx.Fill();
                    // knuckles: a row of bumps across the front of the fist
                    for (int i = 0; i < 4; i++)
                    {
                        ctx.BeginPath();
                        ctx.Arc(3.3 * s, (i - 1.5) * 1.05 * s, 0.8 * s, 0, Math.PI * 2);
                        ctx.Fill();
                    }
                    // the thumb wrapped across the fingers
                    ctx.BeginPath();
                    ctx.Ellipse(2.2 * s, -1.7 * s, 1.5 * s, 0.75 * s, 0.5, 0, Math.PI * 2);
                    ctx.Fill();
                }
                else
                {
                    // hanging: palm, then fingers curling in toward it
                    ctx.BeginPath();
                    ctx.Ellipse(1.5 * s, 0, 2.3 * s, 1.9 * s, 0, 0, Math.PI * 2);
                    ctx.Fill();
                    for (int i = 0; i < 4; i++)
                    {
                        finger(3.3 * s, (i - 1.5) * 0.8 * s, 0.1 + i * 0.05, 2.6 * s, 0.85 * s, 0.9);
                    }
                    finger(1.4 * s, -1.6 * s, -0.4, 2.2 * s, 1 * s, 0.6);
                }
                ctx.Restore();
            }
            // shoulder joints, in world space, from the tilted trunk
            FigPt shoulderAt(double alongK)
            {
                return new FigPt(
                    shX + cosT * alongK * s + sinT * 1 * s,
                    shY + sinT * alongK * s - cosT * -1 * s);
            }
            FigPt nearSh = shoulderAt(3);
            FigPt farSh = shoulderAt(-1.5);

            if (left)
            {
                // Arms hanging, shoulders down. He has nothing to hold.
                // Walking, each arm swings against its own side's leg — from the
                // shoulder, opposite to the leg on its side: forward as that leg goes
                // back. The elbow is never straight — it bends a little at the back of
                // the swing and more as the arm comes forward.
                FigJoint armFK(FigPt sh, double phase, double k)
                {
                    double sw = Math.Sin(phase) * walking;
                    double idle = Math.Sin(this.t * 0.8 + k) * 0.03;
                    double up = Math.PI / 2 - sw * 0.36 + idle - theta * 0.4;
                    double fore = up - (0.2 + Math.Max(0, sw) * 0.5 + walking * 0.08);
                    double ex = sh.x + Math.Cos(up) * UPPER;
                    double ey = sh.y + Math.Sin(up) * UPPER;
                    return new FigJoint(ex, ey, ex + Math.Cos(fore) * FORE, ey + Math.Sin(fore) * FORE);
                }
                layer(0);
                drawArm(farSh.x, farSh.y, armFK(farSh, this.gait, 1), bodyFar, "relaxed");
                layer(2);
                FigJoint jn = armFK(nearSh, this.gait + Math.PI, 2);
                // flinching from the bolt, the near hand comes up over his face
                FigJoint jFl =
                    flinch > 0
                        ? solve(
                            nearSh.x,
                            nearSh.y,
                            jn.hx + (nearSh.x + 7 * s - jn.hx) * flinch,
                            jn.hy + (nearSh.y - 15 * s - jn.hy) * flinch)
                        : jn;
                drawArm(nearSh.x, nearSh.y, jFl, bodyS, "relaxed");
            }
            else if (kicking)
            {
                /*
                 * The launch and the run after it. For the first instant the arms are
                 * still out in front where the stone left them — the follow-through of a
                 * shove — and then they come down into a runner's swing: elbows near a
                 * right angle, a short arc at the shoulder, opposite to the legs.
                 */
                double release = Math.Max(0, 1 - this.kickT / 0.35);
                void arm(FigPt sh, double theta2, Style shade)
                {
                    // runner: upper arm hangs and swings ±0.35 rad, forearm forward
                    double sw = Math.Sin(theta2) * effort;
                    double upAng = Math.PI / 2 - 0.35 * sw;
                    double foreAng = upAng - 1.35;
                    // follow-through: pointing forward and a little up, arm nearly straight
                    double up = upAng * (1 - release) + -0.15 * release;
                    double fore = foreAng * (1 - release) + -0.2 * release;
                    double ex = sh.x + Math.Cos(up) * UPPER;
                    double ey = sh.y + Math.Sin(up) * UPPER;
                    drawArm(
                        sh.x,
                        sh.y,
                        new FigJoint(ex, ey, ex + Math.Cos(fore) * FORE, ey + Math.Sin(fore) * FORE),
                        shade,
                        "fist");
                }
                // arms counter the legs: near leg is at `gait`, so the near arm is opposite
                layer(0);
                arm(farSh, this.gait, bodyFar);
                layer(2);
                arm(nearSh, this.gait + Math.PI, bodyS);
            }
            else
            {
                // ---- arms braced against the boulder ----
                // The hands go on the face of the stone that is turned to him — where a
                // line from his shoulders to its centre meets it — one a little above
                // the other. (Fixed points high on the stone had him reaching up over
                // his head whenever the hill steepened and the stone stood above him.)
                double scx = bcx - x;
                double scy = bcy - groundY;
                double toSh = Math.Atan2(nearSh.y - scy, nearSh.x - scx);
                FigPt onStone(double a, double r)
                {
                    return new FigPt(scx + Math.Cos(a) * R * r + tremble, scy + Math.Sin(a) * R * r);
                }
                FigPt press1 = onStone(toSh - 0.32, 0.97);
                // flinching, the near hand comes off the stone and up over his face
                FigPt shield = new FigPt(nearSh.x + 7 * s, nearSh.y - 15 * s);
                FigPt h1 = new FigPt(
                    press1.x + (shield.x - press1.x) * flinch,
                    press1.y + (shield.y - press1.y) * flinch);
                FigPt h2 = onStone(toSh + 0.2, 0.98);
                FigJoint j2 = solve(farSh.x, farSh.y, h2.x, h2.y);
                FigJoint j1 = solve(nearSh.x, nearSh.y, h1.x, h1.y);
                // leather wrist bracers on the forearms
                void bracer(FigJoint j)
                {
                    double bx = j.ex + (j.hx - j.ex) * 0.72;
                    double by = j.ey + (j.hy - j.ey) * 0.72;
                    double dx = j.hx - j.ex;
                    double dy = j.hy - j.ey;
                    double len = Js.hypot(dx, dy);
                    if (len == 0) len = 1;
                    double px = -dy / len;
                    double py = dx / len;
                    double bw = 2.7 * s;
                    ctx.StrokeStyle = leather;
                    ctx.LineWidth = 3.2 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(bx + px * bw, by + py * bw);
                    ctx.LineTo(bx - px * bw, by - py * bw);
                    ctx.Stroke();
                    ctx.StrokeStyle = "oklch(0.42 0.04 55 / 0.5)";
                    ctx.LineWidth = 1 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(bx + px * bw, by + py * bw);
                    ctx.LineTo(bx - px * bw, by - py * bw);
                    ctx.Stroke();
                }
                // the far arm behind everything, the near one in front of the chest
                layer(0);
                drawArm(farSh.x, farSh.y, j2, bodyFar, "press");
                bracer(j2);
                layer(2);
                drawArm(nearSh.x, nearSh.y, j1, bodyS, flinch > 0.5 ? "relaxed" : "press");
                bracer(j1);
            }

            // ---- neck + head (profile facing the boulder) ----
            layer(1);
            // The head does not follow the spine all the way down: a man pushing keeps
            // his eyes up and forward, so the neck bends back against the lean.
            double nk = theta * 0.55;
            double hx = shX + Math.Sin(nk) * 13.5 * s + 2 * s;
            // the head gives back part of the hip bob, and drops toward the stone as
            // the load comes on, because that is what a neck does under weight
            double hy = shY - Math.Cos(nk) * 13.5 * s + bob * 0.3 + load * 2.4 * s + shudder * 0.3;
            // A neck is not a rectangle. It leaves the collarbones wide, narrows, and
            // runs up at an angle into the base of the skull; under load it thickens and
            // the sternocleidomastoid stands out of the side of it.
            seg(
                shX + 0.5 * s,
                shY + 1 * s,
                hx - 1.2 * s,
                hy + 5 * s,
                5.6 * s,
                (4.2 + load * 0.5) * s,
                (0.8 + load * 1.2) * s,
                0.5,
                bodyS);
            if (inkBegin())
            {
                // the sternocleidomastoid, behind the ear down to the collarbone
                crease(0.8, 0.2 + load * 0.2);
                ctx.BeginPath();
                ctx.MoveTo(hx - 2.5 * s, hy + 4 * s);
                ctx.QuadraticCurveTo(shX + 2 * s, shY - 4 * s, shX + 5 * s, shY + 1 * s);
                ctx.Stroke();
                inkEnd();
            }

            // The head was a seventh of his height when it should be nearer an eighth.
            // Scaling the whole group about its own centre fixes the proportion without
            // letting the nose, hair and beard drift off the face.
            ctx.Save();
            ctx.Translate(hx - 1 * s, hy + 6 * s);
            ctx.Rotate(headTilt);
            if (headTurn < 1)
            {
                double k = Js.sign(headTurn != 0 ? headTurn : 1) * Math.Max(0.3, Math.Abs(headTurn));
                ctx.Scale(k, 1);
            }
            ctx.Translate(-(hx - 1 * s), -(hy + 6 * s));
            ctx.Translate(hx, hy);
            ctx.Scale(0.78, 0.78);
            ctx.Translate(-hx, -hy);

            /**
             * The skull in profile, as one path: cranium, brow ridge, the set-back of the
             * eye, the nose, the mouth shelf, the chin, and the jaw running back to the ear.
             * Two overlapping circles gave a snowman with a triangle on it, and no amount of
             * good animation survives that.
             */
            ctx.BeginPath();
            ctx.MoveTo(hx - 6.6 * s, hy - 1 * s); // back of the skull
            ctx.QuadraticCurveTo(hx - 6.2 * s, hy - 8.4 * s, hx + 0.6 * s, hy - 8.6 * s); // crown
            ctx.QuadraticCurveTo(hx + 5.6 * s, hy - 8.2 * s, hx + 6.4 * s, hy - 3.4 * s); // forehead
            ctx.QuadraticCurveTo(hx + 7.4 * s, hy - 2.2 * s, hx + 6.5 * s, hy - 1.2 * s); // brow ridge
            ctx.QuadraticCurveTo(hx + 6.2 * s, hy - 0.4 * s, hx + 6.9 * s, hy + 0.2 * s); // eye socket
            ctx.LineTo(hx + 10.2 * s, hy + 2.4 * s); // bridge out to the tip
            ctx.QuadraticCurveTo(hx + 10.4 * s, hy + 3.4 * s, hx + 7.6 * s, hy + 3.6 * s); // nostril
            ctx.QuadraticCurveTo(hx + 6.6 * s, hy + 4.4 * s, hx + 7.2 * s, hy + 5.4 * s); // upper lip
            ctx.QuadraticCurveTo(hx + 7.8 * s, hy + 7.6 * s, hx + 5.2 * s, hy + 8.6 * s); // chin
            ctx.QuadraticCurveTo(hx + 0.5 * s, hy + 9.4 * s, hx - 4.4 * s, hy + 5.2 * s); // jaw
            ctx.QuadraticCurveTo(hx - 7 * s, hy + 2.4 * s, hx - 6.6 * s, hy - 1 * s); // back to the ear
            ctx.ClosePath();
            ctx.Fill();

            // the face, in profile, now that he is lit
            bool facing = headTurn >= 0.95;
            if (inkBegin())
            {
                if (facing)
                {
                    // the ear
                    ctx.FillStyle = "rgba(60, 24, 14, 0.35)";
                    ctx.BeginPath();
                    ctx.Ellipse(hx - 1.6 * s, hy + 0.6 * s, 1.7 * s, 2.5 * s, 0.15, 0, Math.PI * 2);
                    ctx.Fill();
                    ctx.StrokeStyle = "rgba(255, 205, 160, 0.3)";
                    ctx.LineWidth = 0.6 * s;
                    ctx.BeginPath();
                    ctx.Arc(hx - 1.6 * s, hy + 0.6 * s, 1.5 * s, -1.4, 1.6);
                    ctx.Stroke();
                    // the eye under a heavy brow, narrowed with the effort
                    ctx.FillStyle = "rgba(30, 12, 8, 0.75)";
                    ctx.BeginPath();
                    ctx.Ellipse(
                        hx + 4.6 * s,
                        hy - 0.5 * s,
                        1.3 * s,
                        (0.75 - load * 0.3) * s,
                        0.1,
                        0,
                        Math.PI * 2);
                    ctx.Fill();
                    // the brow: knotted down at the nose in his anger, smoothed out later
                    double anger = (1 - serene) * (0.5 + load * 0.5) * (1 - this.smile);
                    ctx.StrokeStyle = "rgba(25, 10, 6, 0.7)";
                    ctx.LineWidth = 1.1 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(hx + 2.8 * s, hy - (2.4 - load * 0.4 + anger * 0.3) * s);
                    ctx.QuadraticCurveTo(
                        hx + 5 * s,
                        hy - (3.2 - load * 0.7 - anger * 0.2) * s,
                        hx + 6.7 * s,
                        hy - (2 - anger * 0.9) * s);
                    ctx.Stroke();
                    // a glint in the eye
                    ctx.FillStyle = "rgba(255, 240, 220, 0.6)";
                    ctx.BeginPath();
                    ctx.Arc(hx + 5 * s, hy - 0.7 * s, 0.3 * s, 0, Math.PI * 2);
                    ctx.Fill();
                    // cheekbone, nostril and the line of the mouth
                    ctx.StrokeStyle = "rgba(255, 210, 165, 0.25)";
                    ctx.LineWidth = 1.2 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(hx + 2.6 * s, hy + 1.6 * s);
                    ctx.QuadraticCurveTo(hx + 4.6 * s, hy + 1 * s, hx + 6 * s, hy + 2 * s);
                    ctx.Stroke();
                    ctx.StrokeStyle = "rgba(40, 14, 8, 0.5)";
                    ctx.LineWidth = 0.7 * s;
                    ctx.BeginPath();
                    ctx.MoveTo(hx + 8.2 * s, hy + 3 * s);
                    ctx.QuadraticCurveTo(hx + 7.4 * s, hy + 2.6 * s, hx + 7.2 * s, hy + 3.3 * s);
                    // the mouth: a hard line, or — once he has understood — a smile
                    double sm = this.smile;
                    ctx.MoveTo(hx + 7.4 * s, hy + (5.4 - sm * 0.2) * s);
                    ctx.QuadraticCurveTo(
                        hx + 6.4 * s,
                        hy + (5.6 + sm * 0.9) * s,
                        hx + 5.3 * s,
                        hy + (5.6 - sm * 1.1) * s);
                    ctx.Stroke();
                    if (sm > 0.2)
                    {
                        // the cheek lifts with it
                        ctx.StrokeStyle = Col.Rgba(255, 210, 165, Css.fixedNum(0.3 * sm, 3));
                        ctx.LineWidth = 1 * s;
                        ctx.BeginPath();
                        ctx.Arc(hx + 4.6 * s, hy + 3.4 * s, 1.4 * s, 3.6, 5.6);
                        ctx.Stroke();
                    }
                }
                inkEnd();
            }

            // hair: a heavy mass over the crown and down the back of the neck, not a ball
            ctx.FillStyle = hairC;
            ctx.BeginPath();
            ctx.MoveTo(hx + 6.2 * s, hy - 4 * s);
            ctx.QuadraticCurveTo(hx + 4 * s, hy - 10.6 * s, hx - 2 * s, hy - 9.6 * s);
            ctx.QuadraticCurveTo(hx - 8.4 * s, hy - 8 * s, hx - 8.6 * s, hy - 0.5 * s);
            ctx.QuadraticCurveTo(hx - 9 * s, hy + 5 * s, hx - 5.6 * s, hy + 6.4 * s);
            ctx.QuadraticCurveTo(hx - 7.2 * s, hy + 1 * s, hx - 5.4 * s, hy - 3.4 * s);
            ctx.QuadraticCurveTo(hx - 2.4 * s, hy - 7.6 * s, hx + 6.2 * s, hy - 4 * s);
            ctx.ClosePath();
            ctx.Fill();
            // Curls. A smooth cap of hair over a smooth skull is a helmet; a man who
            // has spent eternity on a mountain has a mass of curls, and in silhouette
            // that is exactly the edge that shows.
            void curl(double cx0, double cy0, double r0)
            {
                ctx.BeginPath();
                ctx.Arc(hx + cx0 * s, hy + cy0 * s, r0 * s, 0, Math.PI * 2);
                ctx.Fill();
            }
            curl(4.6, -8.6, 1.9);
            curl(1.6, -10.4, 2.1);
            curl(-1.8, -10.6, 2.2);
            curl(-5, -9.2, 2.1);
            curl(-7.6, -6.6, 2);
            curl(-9, -3.2, 1.9);
            curl(-9.2, 0.6, 1.8);
            curl(-8.4, 4.2, 1.7);
            curl(6.6, -6.2, 1.5);
            // thick dark beard
            ctx.BeginPath();
            ctx.MoveTo(hx + 6.2 * s, hy + 1 * s);
            ctx.QuadraticCurveTo(hx + 7 * s, hy + 8 * s, hx + 2 * s, hy + 10 * s);
            ctx.QuadraticCurveTo(hx - 2 * s, hy + 10 * s, hx - 3.4 * s, hy + 5 * s);
            ctx.QuadraticCurveTo(hx - 3 * s, hy + 3 * s, hx + 1 * s, hy + 4 * s);
            ctx.QuadraticCurveTo(hx + 3.5 * s, hy + 4.5 * s, hx + 4.5 * s, hy + 3 * s);
            ctx.ClosePath();
            ctx.Fill();
            curl(4.6, 9.6, 1.5);
            curl(1.6, 10.4, 1.6);
            curl(-1.6, 9.2, 1.5);
            curl(6.2, 7.4, 1.2);

            /**
             * The face is gone — brow, eye and mouth all of it — from the silhouette
             * days; the jaw carries it instead. It drops as he drags a breath in, which
             * is the one facial movement an outline can actually show.
             */
            if (pushing || kicking)
            {
                ctx.FillStyle = hairC;
                double gape = (1.4 + winded * 1.2 + Math.Max(0, breath) * winded * 1.6) * s;
                ctx.BeginPath();
                ctx.MoveTo(hx + 7.2 * s, hy + 5.4 * s);
                ctx.QuadraticCurveTo(hx + 8 * s, hy + (7.6 + gape) * s, hx + 5.2 * s, hy + (8.6 + gape) * s);
                ctx.QuadraticCurveTo(hx + 0.5 * s, hy + (9.4 + gape * 0.5) * s, hx - 4.4 * s, hy + 5.2 * s);
                ctx.QuadraticCurveTo(hx + 0.5 * s, hy + 9.4 * s, hx + 5.2 * s, hy + 8.6 * s);
                ctx.QuadraticCurveTo(hx + 7.8 * s, hy + 7.6 * s, hx + 7.2 * s, hy + 5.4 * s);
                ctx.ClosePath();
                ctx.Fill();
            }

            ctx.Restore(); // end of the head group's scale

            // (There is no rim stroke any more, and there should never have been one:
            // hand-fitted curves could not track the outline of thirty overlapping
            // fills. drawFigureLit cuts the real edge out of the layers instead.)

            /**
             * Sweat. Four drops on a staggered loop, thrown off the brow and the jaw and
             * falling away behind him — the cheapest possible cue and the one that reads
             * fastest, because nothing else on screen falls at that speed. Only when he is
             * genuinely working, and it thins out as the effort does.
             */
            layer(2);
            if (winded > 0.3)
            {
                ctx.FillStyle = Col.Oklch(0.92, 0.03, 220, Css.fixedNum(0.55 * winded, 3));
                for (int i = 0; i < 4; i++)
                {
                    // deterministic, staggered, and always falling — no per-frame randomness
                    double p = (this.t * (0.9 + i * 0.17) + i * 0.41) % 1;
                    double sx0 = hx + (3.4 - i * 2.6) * s;
                    double sy0 = hy + (i % 2 != 0 ? 4 : -3) * s;
                    ctx.GlobalAlpha = (1 - p) * 0.8;
                    ctx.BeginPath();
                    ctx.Ellipse(
                        sx0 - p * 7 * s,
                        sy0 + p * 26 * s,
                        0.9 * s,
                        1.7 * s + p * 1.2 * s,
                        -0.25,
                        0,
                        Math.PI * 2);
                    ctx.Fill();
                }
                ctx.GlobalAlpha = 1;
            }

            /**
             * The breath itself. Under a real load he is not silently straining; he is
             * emptying his lungs into the cold, and the plume leaves his mouth on the same
             * cycle the chest is running on.
             */
            if (winded > 0.45 && breath > 0.2)
            {
                double puff = breath * winded;
                ctx.FillStyle = Col.Oklch(0.95, 0.01, 250, Css.fixedNum(0.16 * puff, 3));
                ctx.BeginPath();
                ctx.Ellipse(
                    hx + (11 + puff * 7) * s,
                    hy + 5 * s,
                    (4 + puff * 5) * s,
                    (2.4 + puff * 2.4) * s,
                    -0.2,
                    0,
                    Math.PI * 2);
                ctx.Fill();
            }

            layer(1);
            ctx.Restore();
        }
    }
}
