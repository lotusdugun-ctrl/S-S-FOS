using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// The climb director: every climb has a character of its own, so the hill does not hand
    /// out the same things in the same places each time.
    /// <list type="bullet">
    /// <item>calm: a quiet climb: the view, the sayings, at most one wind;</item>
    /// <item>storm: rain or thunder, falling rocks and barricades, a race against the gods;</item>
    /// <item>sacred: the Moirai's altar, a gentle goal, a golden hour;</item>
    /// <item>wild: a bit of everything, close together.</item>
    /// </list>
    /// The characters come in shuffled runs of four and never twice in a row. Everything is
    /// fixed by the cycle number, so a climb picked up after a restart is the same climb.
    /// The director also sets where the slope objects and the altar stand, when the day's goal
    /// comes, the hour and the weather (the story's own weather still comes first), the god
    /// at the summit, and the marks the hill keeps of a long life on it (10, 25, 50, 100 climbs).
    /// </summary>
    public partial class SisyphusEngine
    {
        internal static readonly string[] MOODS = { "calm", "storm", "sacred", "wild" };

        /// <summary>a stable 0..1 number for (cycle, salt)</summary>
        private static double dh(double c, double salt)
        {
            double x = Math.Sin(c * 12.9898 + salt * 78.233 + 0.5) * 43758.5453;
            return x - Math.Floor(x);
        }

        /// <summary>the order of the four characters within the block of four climbs `b`</summary>
        private static int[] moodBlock(long b)
        {
            var p = new[] { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--)
            {
                int j = (int)Math.Floor(dh(b, 31 + i) * (i + 1));
                int tmp = p[i]; p[i] = p[j]; p[j] = tmp;
            }
            return p;
        }

        /// <summary>the character of climb `cycles`: "calm" | "storm" | "sacred" | "wild"</summary>
        public static string moodFor(double cycles)
        {
            if (cycles < 1) return "calm";
            long c = (long)cycles;
            long b = c / 4;
            var p = moodBlock(b);
            // never the same character twice in a row across the seam of two blocks
            if (b > 0 && p[0] == moodBlock(b - 1)[3]) { int t = p[0]; p[0] = p[1]; p[1] = t; }
            return MOODS[p[(int)(c % 4)]];
        }

        /// <summary>this climb's character (the daily climb is always "wild")</summary>
        internal string climbMood
        {
            get { return this.isDailyChallenge ? "wild" : moodFor(this.cycles); }
        }

        /// <summary>
        /// The hour a climb starts at. Time goes on from climb to climb as it always did, through
        /// the day's eight hours, but now and then a climb skips one, so the pattern does not
        /// repeat every eighth climb.
        /// </summary>
        internal static double hourFor(double cycles)
        {
            long c = Math.Max(0, (long)cycles);
            long idx = 0;
            for (long k = 1; k <= c; k++) idx += dh(k, 7.1) < 0.3 ? 2 : 1;
            return SCHEDULE[(int)(idx % SCHEDULE.Length)];
        }

        // ---------------------------------------------------------------- placing things

        /// <summary>where the Moirai's altar stands on this climb, or -1 when it has none</summary>
        internal double shrineAt = -1;
        /// <summary>at what share of the hill this climb's goal begins</summary>
        private double trialAt = 0.25;
        /// <summary>this climb's goal, chosen when the climb begins</summary>
        private string trialPlan = null;
        /// <summary>throws so far, for Hephaestus' goal</summary>
        internal int runKicks = 0;

        private static readonly string[] TRIAL_KINDS = { "hermes", "athena", "ares", "hephaestus", "hestia" };

        private static string[] trialKindsFor(string mood)
        {
            switch (mood)
            {
                case "calm": return new[] { "hestia", "athena" };
                case "sacred": return new[] { "athena", "hermes", "hestia" };
                case "storm": return new[] { "ares", "hermes", "hephaestus" };
                default: return TRIAL_KINDS;
            }
        }

        private static string rawTrialKind(double cycles)
        {
            var ks = trialKindsFor(moodFor(cycles));
            return ks[(int)Math.Floor(dh(cycles, 3.3) * ks.Length) % ks.Length];
        }

        /// <summary>the goal for climb `cycles`, never the same as the one before</summary>
        private string trialKindFor(double cycles)
        {
            var ks = trialKindsFor(this.climbMood);
            int i = (int)Math.Floor(dh(cycles, 3.3) * ks.Length) % ks.Length;
            string k = ks[i];
            if (cycles >= 2 && k == rawTrialKind(cycles - 1)) k = ks[(i + 1) % ks.Length];
            // with no breath to watch (the calm setting) Hestia's goal means nothing
            if (k == "hestia" && this.calm) k = "athena";
            return k;
        }

        /// <summary>the god who answers at the top; a storm calls the sky and the sea</summary>
        private static string summitGodFor(double cycles, string mood)
        {
            if (cycles < 1) return "zeus";
            string[] gods = mood == "storm" ? new[] { "zeus", "poseidon" } : new[] { "zeus", "poseidon", "hades", "apollo" };
            int i = (int)Math.Floor(dh(cycles, 9.7) * gods.Length) % gods.Length;
            string g = gods[i];
            string prev = gods[(int)Math.Floor(dh(cycles - 1, 9.7) * gods.Length) % gods.Length];
            if (g == prev) g = gods[(i + 1) % gods.Length];
            return g;
        }

        /// <summary>
        /// Lays out this climb: the god at the top, the altar (or none), the goal and where it
        /// begins, and the slope objects: how many, which, and where, by the climb's character.
        /// </summary>
        private void planClimb()
        {
            string mood = this.climbMood;
            double c = this.cycles + this.levelIndex * 0.37 + (this.isDailyChallenge ? 0.5 : 0);
            double len = this.level.length;
            this.summitGod = summitGodFor(this.cycles, mood);
            this.runKicks = 0;

            bool first = this.cycles < 1 && !this.tutorialDone;
            // the altar: on sacred climbs, on half the wild ones
            bool shrine = !first && this.cycles >= 1 && (mood == "sacred" || (mood == "wild" && dh(c, 5.5) < 0.5));
            this.shrineAt = shrine ? len * (0.34 + dh(c, 6.1) * 0.24) : -1;

            this.trialPlan = first || this.cycles < 1 ? null : this.trialKindFor(this.cycles);
            this.trialAt = 0.14 + dh(c, 4.4) * 0.4;

            if (first) return;

            int n;
            string[] kinds;
            switch (mood)
            {
                case "calm":
                    n = dh(c, 1.1) < 0.5 ? 0 : 1;
                    kinds = new[] { "updraft" };
                    break;
                case "sacred":
                    n = 1 + (int)Math.Floor(dh(c, 1.1) * 2);
                    kinds = new[] { "lever", "updraft" };
                    break;
                case "storm":
                    n = 3 + (int)Math.Floor(dh(c, 1.1) * 3);
                    kinds = new[] { "rockfall", "rockfall", "barricade", "updraft" };
                    break;
                default:
                    n = 4 + (int)Math.Floor(dh(c, 1.1) * 3);
                    kinds = new[] { "updraft", "lever", "barricade", "rockfall" };
                    break;
            }
            // wild climbs bunch things up; the others keep them apart
            double gap = mood == "wild" ? 0.035 : 0.07;
            var placed = new List<double>();
            for (int tries = 0; placed.Count < n && tries < 48; tries++)
            {
                double f = 0.12 + dh(c, 20 + tries * 1.7) * 0.78;
                double wx = len * f;
                bool ok = true;
                foreach (double q in placed) if (Math.Abs(q - f) < gap) { ok = false; break; }
                if (this.shrineAt >= 0 && Math.Abs(wx - this.shrineAt) < 130) ok = false;
                if (Math.Abs(wx - this.wedgeX()) < 110) ok = false;
                if (!ok) continue;
                placed.Add(f);
                string k = kinds[(int)Math.Floor(dh(c, 60 + tries * 2.3) * kinds.Length) % kinds.Length];
                this.slopeProps.Add(new SlopeProp
                {
                    kind = k,
                    wx = wx,
                    vx = k == "rockfall" ? -115 - dh(c, 90 + tries) * 70 : 0,
                    used = false,
                    animT = 0,
                });
            }
        }

        // ---------------------------------------------------------------- the two new goals

        private static readonly string[] TRIAL_HEPH_TITLE = { "HEPHAİSTOS'UN ÖRSÜ", "HEPHAESTUS'S ANVIL", "赫菲斯托斯之砧", "हेफेस्टस की निहाई", "YUNQUE DE HEFESTO", "ENCLUME D'HÉPHAÏSTOS", "سندان هيفايستوس", "হেফেস্টাসের নেহাই", "BIGORNA DE HEFESTO" };
        private static readonly string[] TRIAL_HEPH_DESC = { "Kayayı 2 kez fırlat", "Throw the stone twice", "将巨石抛出 2 次", "पत्थर को 2 बार फेंकें", "Lanza la roca 2 veces", "Lancez le rocher 2 fois", "ارمِ الصخرة مرتين", "পাথরটি ২ বার ছোড়ো", "Lance a rocha 2 vezes" };
        private static readonly string[] TRIAL_HESTIA_TITLE = { "HESTİA'NIN OCAĞI", "HESTIA'S HEARTH", "赫斯提亚之炉", "हेस्टिया का अलाव", "HOGAR DE HESTIA", "FOYER D'HESTIA", "موقد هستيا", "হেস্টিয়ার চুলা", "LAR DE HÉSTIA" };
        private static readonly string[] TRIAL_HESTIA_DESC = { "Nefesin üçte birin altına düşmeden 600 adım", "600 paces without your breath falling below a third", "气息不低于三分之一，攀登 600 步", "श्वास एक-तिहाई से नीचे गिराए बिना 600 कदम", "600 pasos sin que el aliento baje de un tercio", "600 pas sans que le souffle tombe sous un tiers", "600 خطوة دون أن ينخفض نَفَسك عن الثلث", "শ্বাস এক-তৃতীয়াংশের নিচে না নামিয়ে ৬০০ কদম", "600 passos sem o fôlego cair abaixo de um terço" };

        /// <summary>the moods' names as the player sees them at the start of a climb</summary>
        private static readonly Dictionary<string, string[]> MOOD_NAMES = new Dictionary<string, string[]>
        {
            ["calm"] = new[] { "Dingin bir tırmanış", "A quiet climb", "宁静的攀登", "एक शांत चढ़ाई", "Una subida serena", "Une montée paisible", "صعود هادئ", "একটি শান্ত আরোহণ", "Uma subida serena" },
            ["storm"] = new[] { "Tanrılar öfkeli", "The gods are angry", "众神震怒", "देवता क्रोधित हैं", "Los dioses están furiosos", "Les dieux sont en colère", "الآلهة غاضبة", "দেবতারা ক্রুদ্ধ", "Os deuses estão furiosos" },
            ["sacred"] = new[] { "Kutsal bir gün", "A holy day", "神圣之日", "एक पवित्र दिन", "Un día sagrado", "Un jour sacré", "يوم مقدس", "একটি পবিত্র দিন", "Um dia sagrado" },
            ["wild"] = new[] { "Dağ kaynıyor", "The hill is restless", "山岭躁动", "पहाड़ बेचैन है", "La colina está inquieta", "La colline s'agite", "الجبل مضطرب", "পাহাড় অস্থির", "A colina está agitada" },
        };

        internal static string moodName(string mood, string lang)
        {
            return MOOD_NAMES.TryGetValue(mood, out var a) ? a[langIdx(lang)] : "";
        }

        /// <summary>the climb's character, named once early on (not on the first climbs, which teach)</summary>
        private double moodToastFor = -1;

        // ---------------------------------------------------------------- what the hill keeps

        internal static readonly int[] MILESTONES = { 10, 25, 50, 100 };

        private static readonly string[][] MILESTONE_TEXT =
        {
            new[] { "Dağ seni hatırlıyor: etekte bir ateş yandı", "The hill remembers you: a fire burns at its foot", "山记住了你：山脚燃起一堆火", "पहाड़ तुम्हें याद रखता है: तलहटी में आग जली", "La colina te recuerda: arde un fuego al pie", "La colline se souvient : un feu brûle à son pied", "الجبل يتذكرك: نار تشتعل عند السفح", "পাহাড় তোমাকে মনে রাখে: পাদদেশে আগুন জ্বলল", "A colina lembra-se de ti: um fogo arde ao pé" },
            new[] { "Dağ seni hatırlıyor: yol meşalelerle aydınlandı", "The hill remembers you: torches light the path", "山记住了你：火炬照亮了小路", "पहाड़ तुम्हें याद रखता है: मशालों से राह रोशन हुई", "La colina te recuerda: antorchas iluminan el camino", "La colline se souvient : des torches éclairent le chemin", "الجبل يتذكرك: المشاعل تضيء الطريق", "পাহাড় তোমাকে মনে রাখে: মশালে পথ আলোকিত", "A colina lembra-se de ti: tochas iluminam o caminho" },
            new[] { "Dağ seni hatırlıyor: zirveye bir dikilitaş yükseldi", "The hill remembers you: an obelisk rises by the summit", "山记住了你：山顶旁竖起一座方尖碑", "पहाड़ तुम्हें याद रखता है: शिखर पर एक स्तंभ उठा", "La colina te recuerda: un obelisco se alza en la cima", "La colline se souvient : un obélisque se dresse au sommet", "الجبل يتذكرك: مسلّة ترتفع عند القمة", "পাহাড় তোমাকে মনে রাখে: চূড়ায় এক স্তম্ভ উঠল", "A colina lembra-se de ti: um obelisco ergue-se no cume" },
            new[] { "Dağ seni hatırlıyor: zirvede altın bir defne çelengi", "The hill remembers you: a golden laurel crowns the summit", "山记住了你：金色桂冠悬于山顶", "पहाड़ तुम्हें याद रखता है: शिखर पर स्वर्ण लॉरेल", "La colina te recuerda: un laurel de oro corona la cima", "La colline se souvient : un laurier d'or couronne le sommet", "الجبل يتذكرك: إكليل غار ذهبي يتوّج القمة", "পাহাড় তোমাকে মনে রাখে: চূড়ায় সোনালি লরেল", "A colina lembra-se de ti: um louro dourado coroa o cume" },
        };

        /// <summary>how many of the hill's marks he has earned (0-4)</summary>
        internal int milestoneStage()
        {
            int n = 0;
            foreach (int m in MILESTONES) if (this.cycles >= m) n++;
            return n;
        }

        /// <summary>
        /// Once a frame while climbing: name the climb's character early on, and on the climb a
        /// mark is earned, say so.
        /// </summary>
        private void updateDirector()
        {
            if (this.phase != "playing" || !this.active || this.cutscene || this.finaleOn) return;
            if (this.moodToastFor == this.cycles) return;
            if (this.x < STONE_REST + 140) return;
            this.moodToastFor = this.cycles;
            int li = langIdx(this.lang);
            int mi = Array.IndexOf(MILESTONES, (int)this.cycles);
            if (mi >= 0 && this.cycles == Math.Floor(this.cycles))
            {
                this.showGameplayToast("✦ " + MILESTONE_TEXT[mi][li], "rgba(255, 226, 150, 0.98)");
                return;
            }
            if (this.cycles >= 2 && !this.isDailyChallenge)
                this.showGameplayToast(moodName(this.climbMood, this.lang), "rgba(240, 228, 205, 0.92)");
        }

        private static readonly double[] TORCH_AT = { 0.16, 0.27, 0.38, 0.49, 0.6, 0.71, 0.82 };

        /// <summary>the marks: a fire at the foot, torches up the path, an obelisk and a laurel at the top</summary>
        private void drawMilestones(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            int stg = this.milestoneStage();
            if (stg <= 0) return;
            var c = this.ctx;
            double s = this.scale;
            double len = this.level.length;
            // night makes the fires matter: brighter after dark
            double night = this.st.elev < 4 ? 1 : this.st.elev < 14 ? 1 - (this.st.elev - 4) / 10 : 0;

            // 10: a brazier at the foot of the hill
            this.drawFire(toScreenX, toScreenY, len * 0.035, 1.25, night, true);

            // 25: torches along the path
            if (stg >= 2)
                for (int i = 0; i < TORCH_AT.Length; i++)
                    this.drawFire(toScreenX, toScreenY, len * TORCH_AT[i], 0.7, night, false);

            // 50: an obelisk by the summit
            if (stg >= 3)
            {
                double wx = len * 0.965;
                double sx = toScreenX(wx);
                if (sx > -60 && sx < this.w + 60)
                {
                    double sy = toScreenY(Levels.terrainAt(this.level, wx));
                    c.Save();
                    c.FillStyle = "rgba(212, 198, 172, 0.92)";
                    c.BeginPath();
                    c.MoveTo(sx - 9 * s, sy);
                    c.LineTo(sx - 6 * s, sy - 92 * s);
                    c.LineTo(sx, sy - 104 * s);
                    c.LineTo(sx + 6 * s, sy - 92 * s);
                    c.LineTo(sx + 9 * s, sy);
                    c.ClosePath();
                    c.Fill();
                    c.FillStyle = "rgba(120, 100, 76, 0.35)";
                    c.BeginPath();
                    c.MoveTo(sx + 2 * s, sy);
                    c.LineTo(sx + 1.5 * s, sy - 96 * s);
                    c.LineTo(sx + 6 * s, sy - 92 * s);
                    c.LineTo(sx + 9 * s, sy);
                    c.ClosePath();
                    c.Fill();
                    c.FillStyle = "rgba(240, 196, 92, 0.95)";
                    c.BeginPath();
                    c.MoveTo(sx - 6 * s, sy - 92 * s);
                    c.LineTo(sx, sy - 104 * s);
                    c.LineTo(sx + 6 * s, sy - 92 * s);
                    c.ClosePath();
                    c.Fill();
                    c.Restore();
                }
            }

            // 100: a golden laurel over the summit
            if (stg >= 4)
            {
                double sx = toScreenX(len);
                if (sx > -80 && sx < this.w + 80)
                {
                    double sy = toScreenY(Levels.terrainAt(this.level, len)) - 92 * s;
                    double r = 17 * s;
                    c.Save();
                    c.GlobalCompositeOperation = Composite.Lighter;
                    var g = c.CreateRadialGradient(sx, sy, 0, sx, sy, r * 2.4);
                    g.AddColorStop(0, Col.Rgba(255, 220, 120, Css.fixedNum(0.35 + 0.1 * Math.Sin(this.t * 2), 3)));
                    g.AddColorStop(1, "rgba(255, 200, 90, 0)");
                    c.FillStyle = g;
                    c.BeginPath();
                    c.Arc(sx, sy, r * 2.4, 0, Math.PI * 2);
                    c.Fill();
                    c.GlobalCompositeOperation = Composite.SourceOver;
                    c.FillStyle = "rgba(236, 192, 88, 0.95)";
                    for (int k = 0; k < 14; k++)
                    {
                        // leaves round the ring, open at the top like a wreath
                        double a = Math.PI * 0.62 + k * (Math.PI * 1.76 / 13);
                        double lx = sx + Math.Cos(a) * r, ly = sy + Math.Sin(a) * r;
                        c.Save();
                        c.Translate(lx, ly);
                        c.Rotate(a + Math.PI / 2);
                        c.BeginPath();
                        c.Ellipse(0, 0, 2.4 * s, 5.2 * s, 0, 0, Math.PI * 2);
                        c.Fill();
                        c.Restore();
                    }
                    c.Restore();
                }
            }
        }

        /// <summary>a fire on a post (a torch) or in a bowl on a tripod (a brazier)</summary>
        private void drawFire(Func<double, double> toScreenX, Func<double, double> toScreenY, double wx, double size, double night, bool brazier)
        {
            double sx = toScreenX(wx);
            if (sx < -50 || sx > this.w + 50) return;
            var c = this.ctx;
            double s = this.scale * size;
            double sy = toScreenY(Levels.terrainAt(this.level, wx));
            double top = sy - (brazier ? 26 : 40) * s;
            c.Save();
            c.StrokeStyle = "rgba(70, 52, 36, 0.92)";
            c.LineWidth = 2.2 * s;
            c.BeginPath();
            if (brazier)
            {
                c.MoveTo(sx - 9 * s, sy); c.LineTo(sx, top + 6 * s);
                c.MoveTo(sx + 9 * s, sy); c.LineTo(sx, top + 6 * s);
                c.MoveTo(sx, sy); c.LineTo(sx, top + 6 * s);
            }
            else
            {
                c.MoveTo(sx, sy); c.LineTo(sx, top + 4 * s);
            }
            c.Stroke();
            if (brazier)
            {
                c.FillStyle = "rgba(150, 104, 52, 0.95)";
                c.BeginPath();
                c.MoveTo(sx - 11 * s, top);
                c.LineTo(sx + 11 * s, top);
                c.LineTo(sx + 6 * s, top + 7 * s);
                c.LineTo(sx - 6 * s, top + 7 * s);
                c.ClosePath();
                c.Fill();
            }
            // the flame flickers, each one in its own time
            double fl = 0.82 + 0.18 * Math.Sin(this.t * 9 + wx * 0.013) * Math.Sin(this.t * 5.3 + wx * 0.007);
            double fh = (brazier ? 14 : 9) * s * fl;
            c.GlobalCompositeOperation = Composite.Lighter;
            var g = c.CreateRadialGradient(sx, top - fh * 0.4, 0, sx, top - fh * 0.4, fh * (2.2 + 2.2 * night));
            g.AddColorStop(0, Col.Rgba(255, 196, 96, Css.fixedNum(0.35 + 0.35 * night, 3)));
            g.AddColorStop(1, "rgba(255, 140, 40, 0)");
            c.FillStyle = g;
            c.BeginPath();
            c.Arc(sx, top - fh * 0.4, fh * (2.2 + 2.2 * night), 0, Math.PI * 2);
            c.Fill();
            c.FillStyle = "rgba(255, 214, 120, 0.9)";
            c.BeginPath();
            c.MoveTo(sx - fh * 0.38, top);
            c.QuadraticCurveTo(sx - fh * 0.3, top - fh * 0.6, sx, top - fh);
            c.QuadraticCurveTo(sx + fh * 0.3, top - fh * 0.6, sx + fh * 0.38, top);
            c.ClosePath();
            c.Fill();
            c.Restore();
        }
    }
}
