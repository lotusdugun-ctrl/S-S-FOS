// Ported from src/game/saga.ts.
/*
 * The saga's state and rules, apart from any drawing.
 *
 * The engine tells it what happens on the hill — a climb starts, the stone
 * passes someone, he slips, he reaches the top, he walks back past someone —
 * and it decides what that means for the story: who speaks, what he carries,
 * which burden he bears, what the sky should be like, when it ends. What it
 * decides goes out as events for the interface to show, and as a plain state
 * object for the save (SagaState.toJson / Saga.readSaga keep the web's JSON shape).
 */
using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    public sealed class SagaState
    {
        /// <summary>3: the long Merope story (2 told it in twelve episodes; 1 a different one)</summary>
        public double v;
        /// <summary>one per summit, one more for a climb without a slip</summary>
        public double insight;
        /// <summary>0-1: how close he is to despair; slips raise it, clean climbs lower it</summary>
        public double despair;
        /// <summary>climbs in a row without a slip</summary>
        public double clean;
        /// <summary>summits ever (the saga's own count)</summary>
        public double summits;
        /// <summary>Merope's next episode, and the summit count when the last one happened</summary>
        public double ep;
        public double epAt;
        /// <summary>what she has asked him to bring, and what he is bringing down (ItemId or null)</summary>
        public string asked;
        public string carrying;
        /// <summary>she has asked for the water, and he has said "not yet" this many times</summary>
        public double notYet;
        public bool waiting;
        /// <summary>how many times each companion has spoken to him</summary>
        public Dictionary<string, double> meet;
        /// <summary>whose burden he has carried to the top</summary>
        public Dictionary<string, bool> carried;
        /// <summary>the burden on this climb (CompanionId or null)</summary>
        public string burden;
        public List<string> passives;
        /// <summary>a wedge waiting for the next climb (from whom), and whether it is laid</summary>
        public string wedgeNext;
        public bool wedge;
        /// <summary>she has gone; the last scene at the stone is still to come</summary>
        public bool finale;
        /// <summary>the story has reached its end; he smiles from here on</summary>
        public bool ended;
        /// <summary>the prologue has been seen</summary>
        public bool prologue;
        /// <summary>her passing lines said so far: Act I, Act II, Act III, and with her back turned</summary>
        public List<double> fill;
        /// <summary>how many of each companion's passing lines have been said, by act (and after)</summary>
        public Dictionary<string, List<double>> idle;
        /// <summary>after the story: how far each companion's own has gone</summary>
        public Dictionary<string, double> post;

        /// <summary><c>{ ...state }</c>: a shallow copy (the dictionaries and lists are shared)</summary>
        public SagaState Clone() { return (SagaState)MemberwiseClone(); }

        /// <summary>the object JSON.stringify would write, keys in the web build's order</summary>
        public Dictionary<string, object> toJson()
        {
            return new Dictionary<string, object>
            {
                ["v"] = v,
                ["insight"] = insight,
                ["despair"] = despair,
                ["clean"] = clean,
                ["summits"] = summits,
                ["ep"] = ep,
                ["epAt"] = epAt,
                ["asked"] = asked,
                ["carrying"] = carrying,
                ["notYet"] = notYet,
                ["waiting"] = waiting,
                ["meet"] = NumDict(meet),
                ["carried"] = BoolDict(carried),
                ["burden"] = burden,
                ["passives"] = new List<object>(passives),
                ["wedgeNext"] = wedgeNext,
                ["wedge"] = wedge,
                ["finale"] = finale,
                ["ended"] = ended,
                ["prologue"] = prologue,
                ["fill"] = NumList(fill),
                ["idle"] = IdleDict(idle),
                ["post"] = NumDict(post),
            };
        }

        static Dictionary<string, object> NumDict(Dictionary<string, double> d)
        {
            var o = new Dictionary<string, object>();
            foreach (var kv in d) o[kv.Key] = kv.Value;
            return o;
        }

        static Dictionary<string, object> BoolDict(Dictionary<string, bool> d)
        {
            var o = new Dictionary<string, object>();
            foreach (var kv in d) o[kv.Key] = kv.Value;
            return o;
        }

        static List<object> NumList(List<double> l)
        {
            var o = new List<object>();
            foreach (var x in l) o.Add(x);
            return o;
        }

        static Dictionary<string, object> IdleDict(Dictionary<string, List<double>> d)
        {
            var o = new Dictionary<string, object>();
            foreach (var kv in d) o[kv.Key] = NumList(kv.Value);
            return o;
        }
    }

    /// <summary>
    /// a line reference the interface turns into words in the player's language.
    /// kind: "ep" (ep) | "again" | "notyet" (n) | "filler" (act 1|2|3, n) | "turned" (n) |
    /// "meet" (who, m) | "post" (who, m) | "idle" (who, g: 0-2 while the story runs (by act),
    /// 3 after it, n) | "reply" (who, accepted). Fields a kind does not have stay 0/null/false.
    /// </summary>
    public sealed class TalkRef
    {
        public string kind;
        public double ep;
        public double n;
        public double act;
        public string who;
        public double m;
        public double g;
        public bool accepted;
    }

    /// <summary>
    /// type: "talk" (@ref, cine, offer, choice) | "took" (item) | "stage" (stage) |
    /// "passive" (id) | "burden" (id) | "wedge" | "finale".
    /// </summary>
    public sealed class SagaEvent
    {
        public string type;
        public TalkRef @ref;
        /// <summary>a scene: the climb stops while it plays</summary>
        public bool cine;
        /// <summary>a burden is offered at the end of it (BurdenId or null)</summary>
        public string offer;
        /// <summary>the last line asks: bring the water now, or not yet</summary>
        public bool choice;
        public string item;
        public int stage;
        public string id;
    }

    /// <summary>where they are on the hill, as a fraction of its length</summary>
    public static class NPC_AT
    {
        public const double tan = 0.22;
        public const double merope = 0.45;
        public const double pro = 0.7;
    }

    public sealed class Saga
    {
        public static SagaState SAGA_START = new SagaState
        {
            v = 3,
            insight = 0,
            despair = 0,
            clean = 0,
            summits = 0,
            ep = 0,
            epAt = -99,
            asked = null,
            carrying = null,
            notYet = 0,
            waiting = false,
            meet = new Dictionary<string, double> { ["pro"] = 0, ["tan"] = 0 },
            carried = new Dictionary<string, bool> { ["pro"] = false, ["tan"] = false },
            burden = null,
            passives = new List<string>(),
            wedgeNext = null,
            wedge = false,
            finale = false,
            ended = false,
            prologue = false,
            fill = new List<double> { 0, 0, 0, 0 },
            idle = new Dictionary<string, List<double>>
            {
                ["pro"] = new List<double> { 0, 0, 0, 0 },
                ["tan"] = new List<double> { 0, 0, 0, 0 },
            },
            post = new Dictionary<string, double> { ["pro"] = 0, ["tan"] = 0 },
        };

        /// <summary>where Prometheus sits by his fire once he has come down from the rock</summary>
        public const double PRO_BELOW = 0.1;

        /// <summary>how many episodes a companion may fall behind before a meeting is dropped</summary>
        const double LAG = 8;

        /// <summary>where each of the twelve episodes of the earlier story now falls</summary>
        static readonly double[] FROM_V2 = { 0, 1, 2, 44, 45, 46, 69, 75, 84, 100, 148, 149, 150 };

        /// <summary>arr[i] as JS reads it: null unless i is a whole index inside the array</summary>
        internal static T At<T>(IList<T> a, double i) where T : class
        {
            if (a == null || i < 0 || i >= a.Count || i != Math.Floor(i)) return null;
            return a[(int)i];
        }

        static double? AtNum(IList<double> a, double i)
        {
            if (a == null || i < 0 || i >= a.Count || i != Math.Floor(i)) return null;
            return a[(int)i];
        }

        /// <summary>sanitise a saved saga (the parsed JSON value: Dictionary / List / double / ...)</summary>
        public static SagaState readSaga(object v)
        {
            // typeof v === "object" && v: an object or an array; an array has none of the keys
            var d = v as Dictionary<string, object> ?? new Dictionary<string, object>();
            object G(string k) { return d.TryGetValue(k, out var x) ? x : null; }
            double num(object x, double lo, double hi, double? dflt = null)
            {
                return x is double n && !double.IsNaN(n) && !double.IsInfinity(n) ? Math.Max(lo, Math.Min(hi, n)) : (dflt ?? lo);
            }
            string comp(object x) { return x is string s && (s == "pro" || s == "tan") ? s : null; }
            string item(object x) { return x is string s && Array.IndexOf(Story.ITEM_IDS, s) >= 0 ? s : null; }
            var dv = G("v");
            bool isV(double k) { return dv is double n && n == k; }
            // a save from the first story: keep what he earned, start Merope's from the beginning
            bool fresh = !isV(2) && !isV(3);
            // the twelve-episode story: carry on from the same point of the long one
            bool @short = isV(2);
            double ep = fresh ? 0 : Math.Floor(num(G("ep"), 0, @short ? 12 : Story.EPISODES.Length));
            Dictionary<string, double> pair(object x)
            {
                return new Dictionary<string, double>
                {
                    ["pro"] = num(Json.Get(x, "pro"), 0, 1e6),
                    ["tan"] = num(Json.Get(x, "tan"), 0, 1e6),
                };
            }
            List<double> quad(object x)
            {
                var l = new List<double>(4);
                var a = x as List<object>;
                for (int i = 0; i < 4; i++) l.Add(num(a != null && i < a.Count ? a[i] : null, 0, 1e6));
                return l;
            }
            var passives = new List<string>();
            if (G("passives") is List<object> ps)
                foreach (var p in ps)
                    if (p is string s && (s == "fire" || s == "patience")) passives.Add(s);
            var carriedV = G("carried");
            var idleV = G("idle");
            return new SagaState
            {
                v = 3,
                insight = num(G("insight"), 0, 1e6),
                despair = num(G("despair"), 0, 1),
                clean = num(G("clean"), 0, 1e6),
                summits = num(G("summits"), 0, 1e7),
                ep = @short ? FROM_V2[(int)ep] : ep,
                epAt = fresh ? -99 : num(G("epAt"), -99, 1e7, -99),
                asked = fresh ? null : item(G("asked")),
                carrying = fresh ? null : item(G("carrying")),
                notYet = fresh ? 0 : num(G("notYet"), 0, 1e6),
                waiting = !fresh && G("waiting") is bool w && w,
                // meetings that have been overtaken by the story are passed over on the way
                meet = fresh
                    ? new Dictionary<string, double> { ["pro"] = 0, ["tan"] = 0 }
                    : @short
                        ? new Dictionary<string, double> { ["pro"] = meetingAt("pro", FROM_V2[(int)ep]), ["tan"] = meetingAt("tan", FROM_V2[(int)ep]) }
                        : pair(G("meet")),
                carried = new Dictionary<string, bool>
                {
                    ["pro"] = Json.Get(carriedV, "pro") is bool cp && cp,
                    ["tan"] = Json.Get(carriedV, "tan") is bool ct && ct,
                },
                burden = null,
                passives = passives,
                wedgeNext = comp(G("wedgeNext")),
                wedge = false,
                finale = !fresh && G("finale") is bool f && f,
                ended = !fresh && G("ended") is bool e && e,
                // the prologue is new too: show it once more
                prologue = !fresh && G("prologue") is bool pr && pr,
                fill = quad(G("fill")),
                idle = new Dictionary<string, List<double>>
                {
                    ["pro"] = quad(Json.Get(idleV, "pro")),
                    ["tan"] = quad(Json.Get(idleV, "tan")),
                },
                post = fresh || @short ? new Dictionary<string, double> { ["pro"] = 0, ["tan"] = 0 } : pair(G("post")),
            };
        }

        /// <summary>for an old save: the meeting each companion should be at, given the story</summary>
        static double meetingAt(string who, double ep)
        {
            var ms = Story.COMPANIONS[who].meetings;
            int i = 0;
            while (i + 1 < ms.Length && (ms[i + 1].minEp ?? 0) <= ep) i++;
            return i;
        }

        public SagaState s;
        public Action<SagaEvent> onEvent = null;
        /// <summary>who has already spoken on this climb / this descent</summary>
        readonly HashSet<string> spoke = new HashSet<string>();
        /// <summary>an offer waiting for an answer (CompanionId or null)</summary>
        public string pendingOffer = null;

        public Saga(SagaState state = null)
        {
            if (state != null) this.s = state.Clone();
            else
            {
                this.s = SAGA_START.Clone();
                this.s.meet = new Dictionary<string, double> { ["pro"] = 0, ["tan"] = 0 };
                this.s.carried = new Dictionary<string, bool> { ["pro"] = false, ["tan"] = false };
                this.s.idle = new Dictionary<string, List<double>>
                {
                    ["pro"] = new List<double> { 0, 0, 0, 0 },
                    ["tan"] = new List<double> { 0, 0, 0, 0 },
                };
                this.s.fill = new List<double> { 0, 0, 0, 0 };
                this.s.post = new Dictionary<string, double> { ["pro"] = 0, ["tan"] = 0 };
                // PORT: as in the web build, `passives` is SAGA_START's own list (the spread
                // copied the reference), so it is shared, not copied.
            }
        }

        /// <summary>the stage of his mind is the act of the story he is in</summary>
        public int stage
        {
            get
            {
                if (this.s.ended || this.s.finale) return 3;
                var eps = Story.EPISODES;
                return At(eps, Math.Min(this.s.ep, eps.Length - 1)).act;
            }
        }

        /// <summary>how serene he has become, 0-1: it shows in how he holds himself</summary>
        public double serenity
        {
            get
            {
                if (this.s.ended) return 1;
                return new[] { 0, 0.4, 0.75 }[this.stage - 1];
            }
        }

        /// <summary>Merope stands with her back to him between the midpoint and Act III</summary>
        public bool meropeTurned
        {
            get { return !this.s.finale && !this.s.ended && At(Story.EPISODES, this.s.ep)?.back == true; }
        }

        /// <summary>she is there to be seen: until she walks away at the end</summary>
        public bool meropeHere
        {
            get { return !this.s.finale && !this.s.ended; }
        }

        public bool has(string p)
        {
            return this.s.passives.Contains(p);
        }

        void emit(SagaEvent e)
        {
            this.onEvent?.Invoke(e);
        }

        sealed class Next
        {
            public double m;
            public Meeting mt;
        }

        Next meetingFor(string who)
        {
            var c = Story.COMPANIONS[who];
            for (; ; )
            {
                double m = this.s.meet[who];
                var mt = At(c.meetings, m);
                if (mt == null) return null;
                if (this.s.ep < (mt.minEp ?? 0)) return null;
                // what they say before the last scene waits until he is going up for the water
                if (At(Story.EPISODES, mt.minEp ?? 0)?.finale == true && !this.fetching) return null;
                // they are met every other climb and have less to say than she does, so
                // they keep up; only when the story has run far ahead (an old save) is
                // what he would have said stale. The first meeting is never passed over.
                var nx = At(c.meetings, m + 1);
                bool stale = m > 0 && nx != null && this.s.ep >= (nx.minEp ?? 0) + LAG;
                // he refused the burden: there is nothing to thank him for; or he has
                // carried it already, and it is not offered twice
                bool moot = (mt.gift && !this.s.carried[who]) || (mt.offer != null && this.s.carried[who]);
                if (!stale && !moot) return new Next { m = m, mt = mt };
                this.s.meet[who]++;
            }
        }

        /// <summary>the weather this climb should have, given how he is ("clear" | "rain" | "storm")</summary>
        public string weatherFor(double cycles, string fallback)
        {
            double d = this.s.despair;
            if (cycles < 2) return "clear";
            // the last climb of the story is under a clear sky, whatever came before
            if (At(Story.EPISODES, this.s.ep)?.finale == true && this.s.carrying != null) return "clear";
            if (d > 0.6) return "storm";
            if (d > 0.4) return "rain";
            return "clear";
        }

        /// <summary>deep despair turns the storm to snow: winter</summary>
        public bool winter
        {
            get { return this.s.despair > 0.75; }
        }

        /// <summary>spring: how far the hill is in flower, from a run of clean climbs</summary>
        public double bloomTarget
        {
            get
            {
                if (this.s.despair > 0.3) return 0;
                double @base = Math.Min(1, this.s.clean / 3) * (1 - this.s.despair * 2);
                return this.s.ended ? Math.Max(0.6, @base) : @base;
            }
        }

        // ---------- the hill's events ----------

        public void climbStarted()
        {
            this.spoke.Clear();
            this.s.burden = null;
            this.s.wedge = this.s.wedgeNext != null;
            this.s.wedgeNext = null;
        }

        /// <summary>the stone has reached `frac` of the hill on the way up</summary>
        public void ascent(double frac, double cycles)
        {
            // on the climb for the water both of them are there to see him go
            bool last = this.fetching;
            if (frac >= NPC_AT.tan - 0.05 && cycles >= 2 && (cycles % 2 == 0 || last))
                this.companion("tan");
            if (frac >= this.proAt - 0.05 && cycles >= 3 && (cycles % 2 == 1 || last))
                this.companion("pro");
        }

        // ---- how the others look, so what is drawn agrees with what is said ----

        /// <summary>how far Prometheus's own story has gone (scenes begun), 0 before it</summary>
        public double proPost
        {
            get { return this.s.ended ? this.s.post["pro"] : 0; }
        }

        /// <summary>how far Tantalus's own story has gone (scenes begun), 0 before it</summary>
        public double tanPost
        {
            get { return this.s.ended ? this.s.post["tan"] : 0; }
        }

        /// <summary>
        /// The eagle comes every day until the day it is late: from the climb on
        /// which his own story begins (all he had to say during hers has been said),
        /// it never comes again — and three scenes later it is dead.
        /// </summary>
        public bool eagleComes
        {
            get { return !(this.s.ended && this.s.meet["pro"] >= Story.COMPANIONS["pro"].meetings.Length); }
        }

        /// <summary>the eagle no longer rides the sky over the hill</summary>
        public bool eagleDead
        {
            get { return this.proPost >= 3; }
        }

        /// <summary>
        /// Where Prometheus is: on his rock, or, once free, down by the path. He
        /// comes down after the scene in which he learns what his freedom cost
        /// (the ninth of his own), between one climb and the next.
        /// </summary>
        public double proAt
        {
            get
            {
                double p = this.proPost;
                return p >= 10 || (p == 9 && !this.spoke.Contains("pro")) ? PRO_BELOW : NPC_AT.pro;
            }
        }

        /// <summary>she has asked for the water and he has said yes: this climb is for it</summary>
        public bool fetching
        {
            get { return this.s.asked == "lethe" && At(Story.EPISODES, this.s.ep)?.finale == true; }
        }

        void companion(string who)
        {
            if (this.spoke.Contains(who)) return;
            this.spoke.Add(who);
            var next = this.meetingFor(who);
            if (next == null)
            {
                // after the story, each of them goes on with their own
                double p = this.s.post[who];
                if (this.s.ended && p < Story.COMPANIONS[who].post.Length)
                {
                    this.s.post[who]++;
                    this.emit(new SagaEvent { type = "talk", @ref = new TalkRef { kind = "post", who = who, m = p }, cine = true });
                    return;
                }
                // a word in passing, never one already heard while the story runs;
                // when they have said them all, they let him pass in silence
                int g = this.s.ended ? 3 : this.stage - 1;
                var said = this.s.idle[who];
                double n = AtNum(said, g) ?? 0;
                if (g < 3 && n >= (At(Story.storyFor("en").companions[who].idle, g)?.Length ?? 0)) return;
                while (said.Count <= g) said.Add(double.NaN); // PORT: a JS array grows (holes) on write
                said[g] = n + 1;
                this.emit(new SagaEvent { type = "talk", @ref = new TalkRef { kind = "idle", who = who, g = g, n = n }, cine = false });
                return;
            }
            this.s.meet[who]++;
            double m = next.m;
            var mt = next.mt;
            if (mt.gift) this.s.wedgeNext = who;
            var e = new SagaEvent { type = "talk", @ref = new TalkRef { kind = "meet", who = who, m = m }, cine = true };
            if (mt.offer != null && this.s.burden == null)
            {
                this.pendingOffer = who;
                e.offer = mt.offer;
            }
            this.emit(e);
        }

        /// <summary>the player's answer to a burden</summary>
        public void answer(bool accept)
        {
            var who = this.pendingOffer;
            if (who == null) return;
            this.pendingOffer = null;
            if (accept)
            {
                this.s.burden = who;
                this.emit(new SagaEvent { type = "burden", id = Story.COMPANIONS[who].burden });
            }
            this.emit(new SagaEvent { type = "talk", @ref = new TalkRef { kind = "reply", who = who, accepted = accept }, cine = false });
        }

        /// <summary>the player's answer to her request for the water</summary>
        public void choose(bool bring)
        {
            if (bring)
            {
                this.s.waiting = false;
                this.s.asked = "lethe";
                return;
            }
            this.s.waiting = true;
            this.emit(new SagaEvent
            {
                type = "talk",
                @ref = new TalkRef { kind = "notyet", n = this.s.notYet },
                cine = true,
            });
            this.s.notYet++;
        }

        public void slipped()
        {
            this.s.despair = Math.Min(1, this.s.despair + 0.14);
            this.s.clean = 0;
        }

        public void steady(double dt)
        {
            this.s.despair = Math.Max(0, this.s.despair - 0.004 * dt);
        }

        public void wedgeHeld()
        {
            this.s.wedge = false;
            this.emit(new SagaEvent { type = "wedge" });
        }

        /// <summary>he reached the top</summary>
        public void summit(double slips)
        {
            this.s.summits++;
            this.s.insight += 1 + (slips == 0 ? 1 : 0);
            if (slips == 0)
            {
                this.s.clean++;
                this.s.despair = Math.Max(0, this.s.despair - 0.25);
            }
            else this.s.despair = Math.Max(0, this.s.despair - 0.05);
            var b = this.s.burden;
            if (b != null)
            {
                this.s.carried[b] = true;
                var p = Story.COMPANIONS[b].passive;
                if (!this.s.passives.Contains(p))
                {
                    this.s.passives.Add(p);
                    this.emit(new SagaEvent { type = "passive", id = p });
                }
                this.s.burden = null;
            }
            // what she asked for, from the top
            if (this.s.asked != null && !this.s.ended)
            {
                this.s.carrying = this.s.asked;
                this.s.asked = null;
                this.emit(new SagaEvent { type = "took", item = this.s.carrying });
            }
        }

        /// <summary>walking down, at `frac` of the hill: he passes her on the way</summary>
        public void descent(double frac)
        {
            // he stops a few steps before her, so he can face her as they talk
            if (frac > NPC_AT.merope + 0.06 || this.spoke.Contains("merope")) return;
            if (!this.meropeHere) return;
            this.spoke.Add("merope");
            int before = this.stage;
            var next = At(Story.EPISODES, this.s.ep);
            // she asked for the water, and he said not yet: she asks again
            if (this.s.waiting)
            {
                this.emit(new SagaEvent { type = "talk", @ref = new TalkRef { kind = "again" }, cine = true, choice = true });
                return;
            }
            bool ready =
                next != null &&
                this.s.summits - this.s.epAt >= (next.gap ?? 1) &&
                (next.brings == null || this.s.carrying == next.brings);
            if (!ready)
            {
                TalkRef r;
                if (this.meropeTurned)
                {
                    r = new TalkRef { kind = "turned", n = this.s.fill[3] };
                    this.s.fill[3]++;
                }
                else
                {
                    int st = this.stage;
                    r = new TalkRef { kind = "filler", act = st, n = this.s.fill[st - 1] };
                    this.s.fill[st - 1]++;
                }
                this.emit(new SagaEvent { type = "talk", @ref = r, cine = false });
                return;
            }
            double ep = this.s.ep;
            var e = new SagaEvent { type = "talk", @ref = new TalkRef { kind = "ep", ep = ep }, cine = next.cine };
            if (next.choice) e.choice = true;
            this.emit(e);
            if (next.brings != null) this.s.carrying = null;
            if (next.asks != null && !next.choice) this.s.asked = next.asks;
            this.s.ep++;
            this.s.epAt = this.s.summits;
            if (next.finale)
            {
                this.s.finale = true;
                this.emit(new SagaEvent { type = "finale" });
            }
            else if (this.stage > before) this.emit(new SagaEvent { type = "stage", stage = this.stage });
        }

        /// <summary>the last scene at the stone has played: the story is over</summary>
        public void complete()
        {
            this.s.finale = false;
            this.s.ended = true;
        }

        /// <summary>pick a summit's saying from the stage he is in, among those not yet found</summary>
        public int? pickQuote(IList<int> fresh)
        {
            if (fresh.Count == 0) return null;
            int st = this.stage;
            var stageOf = Journey.STAGE_OF;
            var mine = new List<int>();
            foreach (var i in fresh) if ((i >= 0 && i < stageOf.Length ? stageOf[i] : 3) == st) mine.Add(i);
            IList<int> pool = mine.Count > 0 ? mine : fresh;
            return pool[(int)Math.Floor(Js.random() * pool.Count)];
        }
    }
}
