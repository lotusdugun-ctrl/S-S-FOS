using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// Wave-2 Gameplay & Anti-Repetition Expansion:
    /// 1. Interactive Slope Objects ("Canlı Dağ"): Hermes Wind Geysers (`updraft`), Bronze Lever Catapults (`lever`),
    ///    Smashable Marble Barricades (`barricade`), and Rolling Rockfalls (`rockfall`).
    /// 2. "Kader Çatalları" — Shrine of the Moirai (Mid-climb roguelite physics boons: Dionysus, Atlas, Icarus, Hades).
    /// 3. Summit & Descent Variations: Downhill Boulder Ride ("Kayaya Binip İniş") + Varied Summit God Interventions
    ///    (Zeus, Poseidon tidal wave, Hades underworld chains, Apollo solar flare).
    /// 4. "Tanrılara İsyan" (Revolt Fever Mode): Combo meter that ignites 10s of unstoppable crimson-gold fury.
    /// 5. Permanent World Evolution ("Dağa Kalıcı İz Bırakma"): Evolving hillside sanctuaries across cycles.
    /// 6. Mythic Companions ("Efsanevi Yoldaşlar"): Athena's Owl, Fire Fox, and Golden-Horned Goat.
    /// </summary>
    public partial class SisyphusEngine
    {
        // ================================================================ 1. INTERACTIVE SLOPE OBJECTS ("CANLI DAĞ")

        internal sealed class SlopeProp
        {
            public string kind; // "updraft" | "lever" | "barricade" | "rockfall"
            public double wx;
            public double vx;
            public bool used;
            public double animT;
        }

        internal readonly List<SlopeProp> slopeProps = new List<SlopeProp>();
        private double propSpawnCycle = -1;

        internal void spawnSlopeProps()
        {
            this.slopeProps.Clear();
            this.propSpawnCycle = this.cycles;
            this.shrineDone = false;
            this.activeBoon = null;
            this.sanctuary1Blessed = false;
            this.sanctuary2Blessed = false;
            this.boulderRide = false;

            // Choose summit god for this cycle (first climb is always Zeus; then rotates)
            int gm = (int)(((long)this.cycles) % 4);
            this.summitGod = this.cycles < 1 ? "zeus" : gm == 0 ? "zeus" : gm == 1 ? "poseidon" : gm == 2 ? "hades" : "apollo";

            if (this.cycles < 1 && !this.tutorialDone) return; // keep first tutorial climb clean

            double len = this.level.length;
            double[] slots = { 0.19, 0.33, 0.54, 0.69, 0.84 };
            string[] kinds = { "updraft", "lever", "barricade", "rockfall" };
            for (int i = 0; i < slots.Length; i++)
            {
                double h = this.hash(this.cycles * 13.7 + i * 5.9 + this.levelIndex * 3.1);
                double jitter = (h - 0.5) * 0.04;
                double wx = len * (slots[i] + jitter);
                // Keep clear of Shrine (0.41) and Companion Wedge (0.48)
                if (Math.Abs(wx - len * 0.41) < 130 || Math.Abs(wx - len * 0.48) < 110) continue;
                int ki = (int)Math.Floor(this.hash(this.cycles * 7.3 + i * 11.1) * kinds.Length) % kinds.Length;
                string k = kinds[ki];
                this.slopeProps.Add(new SlopeProp
                {
                    kind = k,
                    wx = wx,
                    vx = k == "rockfall" ? -115 - (i * 18) : 0,
                    used = false,
                    animT = 0,
                });
            }
        }

        private void updateSlopeProps(double dt)
        {
            if (this.propSpawnCycle != this.cycles) this.spawnSlopeProps();
            if (this.phase != "playing" || !this.active || this.cutscene || this.finaleOn) return;

            int li = langIdx(this.lang);
            double bx = this.x;

            for (int i = 0; i < this.slopeProps.Count; i++)
            {
                var p = this.slopeProps[i];
                if (p.used)
                {
                    p.animT += dt;
                    continue;
                }

                if (p.kind == "rockfall")
                {
                    // Rockfall starts rolling when Sisyphus is within 750 units
                    if (Math.Abs(p.wx - bx) < 750 && p.wx > bx - 120)
                    {
                        p.wx += p.vx * dt;
                    }
                    double dist = Math.Abs(p.wx - bx);
                    if (dist < 48)
                    {
                        p.used = true;
                        p.animT = 0;
                        bool counter = this.kickT >= 0 || this.revoltFeverT > 0 || this.activeBoon == "atlas" || this.activeBoon == "hades" || (this.heaveFx != null && this.heaveFx.q == "perfect" && this.heaveFx.t < 0.35);
                        if (counter)
                        {
                            this.breath = Math.Min(1, this.breath + 0.35);
                            this.addRevolt(0.24);
                            this.audio.checkpoint();
                            this.shake = Math.Max(this.shake, 0.45);
                            this.spawnShatterParticles(p.wx, 14);
                            string msg = new[]
                            {
                                "💥 Kaya Parçalandı! (+Nefes & İsyan)",
                                "💥 Rockfall Shattered! (+Breath & Revolt)",
                                "💥 击碎落石！(+气息与反抗)",
                                "💥 गिरती चट्टान चकनाचूर! (+श्वास और विद्रोह)",
                                "💥 ¡Roca Destrozada! (+Aliento y Rebelión)",
                                "💥 Éboulis Brisé ! (+Souffle et Révolte)",
                                "💥 تحطيم الصخرة! (+نَفَس وتمرد)",
                                "💥 পাথর চূর্ণ! (+শ্বাস ও বিদ্রোহ)",
                                "💥 Rocha Estilhaçada! (+Fôlego e Revolta)",
                            }[li];
                            this.showGameplayToast(msg, "rgba(255, 215, 130, 0.96)");
                        }
                        else
                        {
                            this.vx = Math.Min(this.vx, -85);
                            this.shake = Math.Max(this.shake, 0.5);
                            this.audio.impact();
                            this.haptic(35);
                            this.dustBurst(p.wx, 10);
                        }
                    }
                }
                else if (p.kind == "updraft")
                {
                    // Hermes Wind Geyser: passing through restores full breath and grants a gentle hop
                    if (Math.Abs(bx - p.wx) < 46 && (this.kickT >= 0 || this.vx > 35 || this.revoltFeverT > 0))
                    {
                        p.used = true;
                        p.animT = 0;
                        this.hopV = Math.Max(this.hopV, 1.4);
                        this.breath = Math.Min(1, this.breath + 0.55);
                        this.addRevolt(0.20);
                        this.audio.kick();
                        this.haptic(25);
                        this.spawnShatterParticles(p.wx, 16);
                        string msg = new[]
                        {
                            "🌪 Hermes Hava Akımı! (+Nefes & Ferahlama)",
                            "🌪 Hermes Updraft! (+Breath & Second Wind)",
                            "🌪 赫尔墨斯上升气流！(+气息恢复)",
                            "🌪 हर्मीस वायु धारा! (+श्वास और स्फूर्ति)",
                            "🌪 ¡Corriente de Hermes! (+Aliento)",
                            "🌪 Courant d'Hermès ! (+Souffle)",
                            "🌪 تيار هرمس الهوائي! (+نَفَس)",
                            "🌪 হার্মিসের বায়ুপ্রবাহ! (+শ্বাস পুনরুদ্ধার)",
                            "🌪 Corrente de Hermes! (+Fôlego)",
                        }[li];
                        this.showGameplayToast(msg, "rgba(190, 245, 255, 0.96)");
                    }
                }
                else if (p.kind == "lever")
                {
                    // Ancient Bronze Plate: triggers on perfect heave, kick, or steady push to grant breath & revolt
                    if (Math.Abs(bx - p.wx) < 42 && (this.kickT >= 0 || this.vx > 35 || (this.heaveFx != null && this.heaveFx.t < 0.35)))
                    {
                        p.used = true;
                        p.animT = 0;
                        this.hopV = Math.Max(this.hopV, 1.2);
                        this.breath = Math.Min(1, this.breath + 0.45);
                        this.addRevolt(0.22);
                        this.audio.checkpoint();
                        this.shake = Math.Max(this.shake, 0.35);
                        this.haptic(28);
                        this.spawnShatterParticles(p.wx, 12);
                        string msg = new[]
                        {
                            "⚙ Antik Mekanizma! (+Nefes & İsyan)",
                            "⚙ Ancient Mechanism! (+Breath & Revolt)",
                            "⚙ 远古踏板机关！(+气息与反抗)",
                            "⚙ प्राचीन तंत्र! (+श्वास और विद्रोह)",
                            "⚙ ¡Mecanismo Antiguo! (+Aliento y Rebelión)",
                            "⚙ Mécanisme Antique ! (+Souffle et Révolte)",
                            "⚙ الآلية القديمة! (+نَفَس وتمرد)",
                            "⚙ প্রাচীন যন্ত্র! (+শ্বাস ও বিদ্রোহ)",
                            "⚙ Mecanismo Antigo! (+Fôlego e Revolta)",
                        }[li];
                        this.showGameplayToast(msg, "rgba(255, 210, 125, 0.96)");
                    }
                }
                else if (p.kind == "barricade")
                {
                    if (Math.Abs(bx - p.wx) < 40)
                    {
                        bool smash = this.kickT >= 0 || this.rollRush > 0.25 || this.revoltFeverT > 0
                            || this.activeBoon == "atlas" || this.activeBoon == "hades"
                            || this.vx > 85 || (this.heaveFx != null && this.heaveFx.q == "perfect" && this.heaveFx.t < 0.4);
                        if (smash)
                        {
                            p.used = true;
                            p.animT = 0;
                            this.breath = Math.Min(1, this.breath + 0.4);
                            this.saga.s.insight += 1;
                            this.addRevolt(0.28);
                            this.shake = Math.Max(this.shake, 0.65);
                            this.audio.thunder();
                            this.haptic(new double[] { 20, 30, 35 });
                            this.spawnShatterParticles(p.wx, 22);
                            string msg = new[]
                            {
                                "🏛 Mermer Engel Yıkıldı! (+1 İçgörü & Nefes)",
                                "🏛 Marble Barricade Smashed! (+1 Insight & Breath)",
                                "🏛 撞碎大理石路障！(+1 领悟与气息)",
                                "🏛 संगमरमर अवरोध ध्वस्त! (+1 अंतर्दृष्टि और श्वास)",
                                "🏛 ¡Barricada Destrozada! (+1 Intuición y Aliento)",
                                "🏛 Barricade de Marbre Brisée ! (+1 Intuition)",
                                "🏛 تحطيم الحاجز الرخامي! (+1 بصيرة ونَفَس)",
                                "🏛 মার্বেল বাধা চূর্ণ! (+১ অন্তর্দৃষ্টি ও শ্বাস)",
                                "🏛 Barricada de Mármore Destruída! (+1 Intuição)",
                            }[li];
                            this.showGameplayToast(msg, "rgba(255, 232, 160, 0.98)");
                        }
                        else if (this.x > p.wx - 36 && this.vx > 0)
                        {
                            // Holds the boulder back until a strong heave or kick shatters it
                            this.x = p.wx - 36;
                            this.vx = 0;
                        }
                    }
                }
            }
        }

        private void spawnShatterParticles(double wx, int count)
        {
            double wy = Levels.terrainAt(this.level, wx) + 24;
            for (int i = 0; i < count; i++)
            {
                double a = -Math.PI * (0.08 + Js.random() * 0.84);
                double v = 70 + Js.random() * 190;
                this.particles.Add(new Particle
                {
                    wx = wx + (Js.random() - 0.5) * 24,
                    wy = wy + Js.random() * 22,
                    vx = Math.Cos(a) * v,
                    vy = Math.Sin(a) * v,
                    life = 0,
                    maxLife = 0.5 + Js.random() * 0.55,
                    size = (1.5 + Js.random() * 2.2) * this.scale,
                    kind = i % 2 == 0 ? "spark" : "dust",
                });
            }
        }

        private void drawSlopeProps(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (this.slopeProps.Count == 0) return;
            var c = this.ctx;
            double s = this.scale;
            for (int i = 0; i < this.slopeProps.Count; i++)
            {
                var p = this.slopeProps[i];
                if (p.used && p.animT > 0.6) continue;
                double sx = toScreenX(p.wx);
                if (sx < -80 || sx > this.w + 80) continue;
                double sy = toScreenY(Levels.terrainAt(this.level, p.wx));
                double fade = p.used ? Math.Max(0, 1 - p.animT / 0.6) : 1;

                c.Save();
                c.GlobalAlpha = fade;
                if (p.kind == "updraft")
                {
                    c.GlobalCompositeOperation = Composite.Lighter;
                    for (int k = 0; k < 4; k++)
                    {
                        double ph = ((this.t * 2.2 + k * 0.25) % 1);
                        double yy = sy - ph * 95 * s;
                        double rx = (10 + ph * 16) * s;
                        double al = Math.Sin(ph * Math.PI) * 0.55;
                        c.StrokeStyle = Col.Rgba(175, 238, 255, Css.fixedNum(al, 3));
                        c.LineWidth = 1.8 * s;
                        c.BeginPath();
                        c.Ellipse(sx + Math.Sin(this.t * 4 + k) * 4 * s, yy, rx, 4 * s, 0, 0, Math.PI * 2);
                        c.Stroke();
                    }
                }
                else if (p.kind == "lever")
                {
                    double ang = p.used ? -0.55 : 0.18;
                    c.Translate(sx, sy);
                    c.Rotate(ang);
                    c.FillStyle = "rgba(185, 138, 68, 0.92)";
                    c.FillRect(-18 * s, -6 * s, 36 * s, 6 * s);
                    c.StrokeStyle = "rgba(255, 220, 135, 0.85)";
                    c.LineWidth = 1.3 * s;
                    c.StrokeRect(-18 * s, -6 * s, 36 * s, 6 * s);
                }
                else if (p.kind == "barricade")
                {
                    c.Translate(sx, sy);
                    c.FillStyle = "rgba(218, 206, 186, 0.92)";
                    c.BeginPath();
                    c.MoveTo(-14 * s, 0);
                    c.LineTo(-11 * s, -48 * s);
                    c.LineTo(12 * s, -44 * s);
                    c.LineTo(15 * s, 0);
                    c.ClosePath();
                    c.Fill();
                    // Glowing golden fault cracks showing it can be shattered
                    c.StrokeStyle = "rgba(255, 195, 85, 0.85)";
                    c.LineWidth = 1.5 * s;
                    c.BeginPath();
                    c.MoveTo(-4 * s, -44 * s);
                    c.LineTo(2 * s, -24 * s);
                    c.LineTo(-5 * s, -8 * s);
                    c.MoveTo(2 * s, -24 * s);
                    c.LineTo(9 * s, -14 * s);
                    c.Stroke();
                }
                else if (p.kind == "rockfall")
                {
                    double r = 15 * s;
                    c.Translate(sx, sy - r);
                    c.Rotate(this.t * -6.5);
                    c.FillStyle = "rgba(135, 112, 88, 0.95)";
                    c.BeginPath();
                    c.Arc(0, 0, r, 0, Math.PI * 2);
                    c.Fill();
                    c.StrokeStyle = "rgba(255, 185, 95, 0.7)";
                    c.LineWidth = 1.4 * s;
                    c.Stroke();
                }
                c.Restore();
            }
        }

        // ================================================================ 2. "KADER ÇATALLARI" — SHRINE OF THE MOIRAI (ROGUELITE BOONS)

        /// <summary>Active single-climb boon granted by the Shrine of the Moirai ("dionysus" | "atlas" | "icarus" | "hades" | null).</summary>
        internal string activeBoon = null;
        private bool shrineDone = false;
        private double shrineFlashT = 99;

        internal double boonRadiusMult()
        {
            if (this.activeBoon == "dionysus") return 0.72;
            if (this.activeBoon == "atlas") return 1.42;
            return 1.0;
        }

        internal double boonPushMult()
        {
            return 1.0;
        }

        internal double boonKickGravityMult()
        {
            return 1.0;
        }

        private void updateShrine(double dt)
        {
            this.shrineFlashT += dt;
            if (this.shrineDone || this.cycles < 1 || this.phase != "playing") return;
            double shrineX = this.level.length * 0.41;
            if (this.x >= shrineX)
            {
                this.shrineDone = true;
                this.shrineFlashT = 0;
                // Choice depends on how Sisyphus crosses the altar:
                // Kicking / high-speed charge selects Vessel B; steady push selects Vessel A
                int pair = (int)(((long)this.cycles) % 2);
                bool aggressive = this.kickT >= 0 || this.vx > 115 || this.streak >= 3;
                if (pair == 0)
                    this.activeBoon = aggressive ? "atlas" : "dionysus";
                else
                    this.activeBoon = aggressive ? "icarus" : "hades";

                if (this.activeBoon == "icarus")
                    this.hopV = Math.Max(this.hopV, 2.2);

                this.breath = 1;
                this.addRevolt(0.22);
                this.audio.checkpoint();
                this.haptic(new double[] { 20, 40, 20 });
                this.spawnShatterParticles(shrineX, 18);

                var (bName, bDesc) = boonText(this.activeBoon, this.lang);
                this.showGameplayToast("⛩ " + bName + " · " + bDesc, "rgba(255, 222, 145, 0.98)");
            }
        }

        public static (string name, string desc) boonText(string boon, string lang)
        {
            int li = langIdx(lang);
            switch (boon)
            {
                case "dionysus":
                    return (
                        new[] { "Dionysos'un Kadehi", "Chalice of Dionysus", "狄俄尼索斯之杯", "डायोनिसस का प्याला", "Cáliz de Dioniso", "Calice de Dionysos", "كأس ديونيسوس", "দিওনিসুসের পাত্র", "Cálice de Dionísio" }[li],
                        new[] { "Kaya küçüldü (0.72×), nefes hiç tükenmez!", "Boulder shrunk (0.72×), effortless breath!", "巨石缩小 (0.72×)，气息充沛！", "पत्थर छोटा (0.72×), असीम श्वास!", "Roca reducida (0.72×), ¡aliento ligero!", "Rocher réduit (0,72×), souffle léger !", "صغرت الصخرة (0.72×)، نَفَس وفير!", "পাথর ছোট (০.৭২×), অফুরন্ত শ্বাস!", "Rocha reduzida (0.72×), fôlego leve!" }[li]
                    );
                case "atlas":
                    return (
                        new[] { "Atlas'ın Omuzu", "Shoulder of Atlas", "阿特拉斯之肩", "एटलस का कंधा", "Hombro de Atlas", "Épaule d'Atlas", "كتف أطلس", "অ্যাটলাসের কাঁধ", "Ombro de Atlas" }[li],
                        new[] { "Dev kaya (1.42×): Asla geri kaymaz ve engelleri ezer!", "Giant stone (1.42×): Never slips back & crushes barricades!", "巨型神石 (1.42×)：绝不后滑并碾碎路障！", "विशाल पत्थर (1.42×): कभी पीछे नहीं फिसलता!", "Roca gigante (1.42×): ¡Nunca retrocede y aplasta todo!", "Rocher géant (1,42×) : ne recule jamais !", "صخرة عملاقة (1.42×): لا تنزلق للخلف أبداً!", "দানব পাথর (১.৪২×): কখনো পিছলে যায় না!", "Rocha gigante (1.42×): Nunca desliza para trás!" }[li]
                    );
                case "icarus":
                    return (
                        new[] { "İkarus'un Kanatları", "Wings of Icarus", "伊卡洛斯之翼", "इकारस के पंख", "Alas de Ícaro", "Ailes d'Icare", "أجنحة إيكاروس", "ইকারাসের ডানা", "Asas de Ícaro" }[li],
                        new[] { "Düşük yerçekimi: Omuz darbeleri kayayı uçurur!", "Low gravity: Shoulder kicks launch the stone through the air!", "低重力：肩撞抛石腾空滑翔！", "कम गुरुत्वाकर्षण: प्रहार से पत्थर हवा में उड़ता है!", "Baja gravedad: ¡Los lanzamientos vuelan!", "Faible gravité : les lancers planent dans les airs !", "جاذبية منخفضة: الرميات تحلق في الهواء!", "কম মাধ্যাকর্ষণ: পাথর বাতাসে ভেসে চলে!", "Baixa gravidade: Lançamentos voam pelo ar!" }[li]
                    );
                default: // hades
                    return (
                        new[] { "Hades'in Gölgesi", "Shadow of Hades", "哈迪斯之影", "हेडीज़ की छाया", "Sombra de Hades", "Ombre d'Hadès", "ظل هاديس", "হেডিসের ছায়া", "Sombra de Hades" }[li],
                        new[] { "Hayalet form: Çarşak, bataklık ve fırtına işlemez!", "Spectral form: Immune to scree, mire, and storms!", "幽影形态：无视碎石、泥沼与风暴减速！", "छाया रूप: कंकड़, दलदल और तूफ़ान से मुक्त!", "Forma espectral: ¡Inmune a pedregal, lodo y tormenta!", "Forme spectrale : immunisé contre la boue et l'orage !", "طيف هاديس: مناعة ضد الحصى والوحل والعواصف!", "ছায়া রূপ: পাথর, কাদা ও ঝড়ে কোনো বাধা নেই!", "Forma espectral: Imune a cascalho, lama e tempestade!" }[li]
                    );
            }
        }

        private void drawShrineAndBoon(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (this.cycles < 1) return;
            var c = this.ctx;
            double s = this.scale;
            double shrineX = this.level.length * 0.41;
            double sx = toScreenX(shrineX);
            if (sx > -90 && sx < this.w + 90)
            {
                double sy = toScreenY(Levels.terrainAt(this.level, shrineX));
                c.Save();
                // Two ancient Moirai vessels/pillars
                c.FillStyle = "rgba(225, 212, 188, 0.88)";
                c.FillRect(sx - 20 * s, sy - 46 * s, 8 * s, 46 * s);
                c.FillRect(sx + 12 * s, sy - 54 * s, 8 * s, 54 * s);
                c.FillRect(sx - 24 * s, sy - 6 * s, 48 * s, 6 * s);

                if (!this.shrineDone)
                {
                    c.GlobalCompositeOperation = Composite.Lighter;
                    double p = 0.7 + 0.3 * Math.Sin(this.t * 3.8);
                    // Left flame (purple-gold)
                    var g1 = c.CreateRadialGradient(sx - 16 * s, sy - 54 * s, 0, sx - 16 * s, sy - 54 * s, 16 * s);
                    g1.AddColorStop(0, Col.Rgba(235, 165, 255, Css.fixedNum(0.9 * p, 3)));
                    g1.AddColorStop(1, "rgba(180, 90, 240, 0)");
                    c.FillStyle = g1;
                    c.BeginPath();
                    c.Arc(sx - 16 * s, sy - 54 * s, 16 * s, 0, Math.PI * 2);
                    c.Fill();

                    // Right flame (amber-cyan)
                    var g2 = c.CreateRadialGradient(sx + 16 * s, sy - 62 * s, 0, sx + 16 * s, sy - 62 * s, 16 * s);
                    g2.AddColorStop(0, Col.Rgba(255, 220, 120, Css.fixedNum(0.9 * p, 3)));
                    g2.AddColorStop(1, "rgba(255, 160, 50, 0)");
                    c.FillStyle = g2;
                    c.BeginPath();
                    c.Arc(sx + 16 * s, sy - 62 * s, 16 * s, 0, Math.PI * 2);
                    c.Fill();
                }
                c.Restore();
            }

            // Active Boon aura around the boulder
            if (!string.IsNullOrEmpty(this.activeBoon) && this.phase == "playing")
            {
                double bx = toScreenX(this.x + this.creep);
                double R = 34 * s * this.boonRadiusMult();
                double by = toScreenY(Levels.terrainAt(this.level, this.x + this.creep)) - this.hop * R * 1.4 - R;
                c.Save();
                c.GlobalCompositeOperation = Composite.Lighter;
                string col = this.activeBoon == "dionysus" ? "rgba(215, 115, 255, 0.24)"
                    : this.activeBoon == "atlas" ? "rgba(255, 185, 85, 0.26)"
                    : this.activeBoon == "icarus" ? "rgba(175, 235, 255, 0.28)"
                    : "rgba(135, 110, 235, 0.28)";
                c.StrokeStyle = col;
                c.LineWidth = 3.2 * s;
                c.BeginPath();
                c.Arc(bx, by, R * 1.14, 0, Math.PI * 2);
                c.Stroke();
                c.Restore();
            }
        }

        // ================================================================ 3. SUMMIT & DESCENT VARIATIONS ("BOULDER RIDE" & SUMMIT GODS)

        internal bool boulderRide = false;
        internal string summitGod = "zeus"; // "zeus" | "poseidon" | "hades" | "apollo"

        internal bool tryBoulderRideOrJump()
        {
            if (this.phase == "summit")
            {
                this.boulderRide = true;
                int li = langIdx(this.lang);
                string msg = new[]
                {
                    "🪨 Kaya Sörfü Hazır! (İnişte zıplamak için Fırlat/Tab)",
                    "🪨 Boulder Surf Armed! (Tap Throw/Tab while rolling to jump)",
                    "🪨 乘石冲浪已就绪！(滚落时按抛石键跳跃)",
                    "🪨 चट्टान की सवारी तैयार! (लुढ़कते समय कूदने के लिए दबाएँ)",
                    "🪨 ¡Surf de Roca Listo! (Pulsa Lanzar al rodar para saltar)",
                    "🪨 Surf sur Rocher Prêt ! (Appuyez pour sauter en roulant)",
                    "🪨 ركوب الصخرة جاهز! (اضغط للقفز أثناء التدحرج)",
                    "🪨 পাথর সার্ফ প্রস্তুত! (গড়ানোর সময় লাফাতে চাপুন)",
                    "🪨 Surf na Rocha Pronto! (Toque para saltar na descida)",
                }[li];
                this.showGameplayToast(msg, "rgba(255, 230, 145, 0.98)");
                this.haptic(25);
                return true;
            }
            if (this.phase == "rolling")
            {
                if (!this.boulderRide)
                {
                    this.boulderRide = true;
                }
                if (this.hop <= 0.15)
                {
                    this.hopV = 2.85;
                    this.audio.kick();
                    this.haptic(22);
                    this.dustBurst(this.x, 10);
                }
                return true;
            }
            return false;
        }

        private void updateBoulderRideOrbs()
        {
            if (!this.boulderRide || this.phase != "rolling") return;
            for (int i = 0; i < this.descentOrbs.Count; i++)
            {
                var o = this.descentOrbs[i];
                if (o.taken) continue;
                if (Math.Abs(this.x - o.wx) <= 75)
                {
                    o.taken = true;
                    this.descentOrbsTaken++;
                    this.audio.heave("perfect", this.descentOrbsTaken + 2);
                    this.haptic(15);
                    this.spawnShatterParticles(o.wx, 8);
                    if (this.descentOrbsTaken == 3 || this.descentOrbsTaken == 5)
                    {
                        if (this.descentOrbsTaken == 5) this.saga.s.insight += 1;
                    }
                }
            }
        }

        private void drawSummitGodEffect(Func<double, double> toScreenX, Func<double, double> toScreenY, double bx, double by)
        {
            if (this.summitGod == "zeus") return;
            if (this.zeus.state != "strike" && this.phase != "rolling") return;
            double t = this.zeus.t;
            if (t > 1.35) return;
            var c = this.ctx;
            double s = this.scale;
            double u = 1 - Math.Min(1, t / 1.35);

            c.Save();
            c.GlobalCompositeOperation = Composite.Lighter;
            if (this.summitGod == "poseidon")
            {
                // Giant turquoise-foamed tidal wave sweeping over the summit
                for (int i = 0; i < 3; i++)
                {
                    double r = (45 + i * 28 + (1 - u) * 90) * s;
                    c.StrokeStyle = Col.Rgba(75, 225, 245, Css.fixedNum(0.65 * u, 3));
                    c.LineWidth = (5 - i) * s;
                    c.BeginPath();
                    c.Arc(bx + 20 * s, by, r, Math.PI * 0.85, Math.PI * 1.95);
                    c.Stroke();
                }
            }
            else if (this.summitGod == "hades")
            {
                // Dark violet-crimson underworld chains pulling the boulder down
                c.StrokeStyle = Col.Rgba(210, 85, 255, Css.fixedNum(0.8 * u, 3));
                c.LineWidth = 3.2 * s;
                for (int i = -1; i <= 1; i++)
                {
                    c.BeginPath();
                    c.MoveTo(bx - 160 * s, by + 140 * s + i * 20 * s);
                    c.QuadraticCurveTo(bx - 60 * s, by - 30 * s * i, bx, by);
                    c.Stroke();
                }
            }
            else if (this.summitGod == "apollo")
            {
                // Blinding golden solar flare beam from the heavens
                var g = c.CreateRadialGradient(bx, by, 4 * s, bx, by, 140 * s);
                g.AddColorStop(0, Col.Rgba(255, 250, 200, Css.fixedNum(0.92 * u, 3)));
                g.AddColorStop(0.5, Col.Rgba(255, 190, 65, Css.fixedNum(0.55 * u, 3)));
                g.AddColorStop(1, "rgba(255, 120, 20, 0)");
                c.FillStyle = g;
                c.BeginPath();
                c.Arc(bx, by, 140 * s, 0, Math.PI * 2);
                c.Fill();
            }
            c.Restore();
        }

        // ================================================================ 4. "TANRILARA İSYAN" (REVOLT FEVER MODE)

        internal double revoltMeter = 0;
        internal double revoltFeverT = 0;

        internal void addRevolt(double amount)
        {
            if (this.revoltFeverT > 0 || this.phase != "playing") return;
            this.revoltMeter = Math.Min(1.0, this.revoltMeter + amount);
            if (this.revoltMeter >= 1.0)
            {
                this.revoltMeter = 0;
                this.revoltFeverT = 10.0;
                this.breath = 1;
                this.slipT = 0;
                this.flash = Math.Max(this.flash, 0.45);
                this.audio.checkpoint();
                this.haptic(new double[] { 25, 35, 50 });
                this.spawnShatterParticles(this.x, 24);
                int li = langIdx(this.lang);
                string msg = new[]
                {
                    "🔥 TANRILARA İSYAN! (10sn Sınırsız Nefes & Kusursuz Ritim)",
                    "🔥 REVOLT AGAINST THE GODS! (10s Infinite Breath & Perfect Rhythm)",
                    "🔥 向众神反抗！(10 秒无限气息与完美节奏)",
                    "🔥 देवताओं के विरुद्ध विद्रोह! (10से असीम श्वास और सटीक लय)",
                    "🔥 ¡REBELIÓN CONTRA LOS DIOSES! (10s Aliento Infinito)",
                    "🔥 RÉVOLTE CONTRE LES DIEUX ! (10s Souffle Infini)",
                    "🔥 تمرد على الآلهة! (10 ثوانٍ نَفَس لا ينفد وإيقاع مثالي)",
                    "🔥 দেবতাদের বিরুদ্ধে বিদ্রোহ! (১০ সেকেন্ড অফুরন্ত শ্বাস)",
                    "🔥 REVOLTA CONTRA OS DEUSES! (10s Fôlego Infinito)",
                }[li];
                this.showGameplayToast(msg, "rgba(255, 205, 110, 0.99)");
            }
        }

        private void updateRevolt(double dt)
        {
            if (this.revoltFeverT > 0 && this.phase == "playing" && this.active)
            {
                this.revoltFeverT = Math.Max(0, this.revoltFeverT - dt);
                this.breath = 1;
                this.slipT = 0;
                if (Js.random() < 0.45)
                {
                    this.particles.Add(new Particle
                    {
                        wx = this.x - 32 + (Js.random() - 0.5) * 44,
                        wy = Levels.terrainAt(this.level, this.x) + 12 + Js.random() * 35,
                        vx = (Js.random() - 0.5) * 60,
                        vy = -45 - Js.random() * 65,
                        life = 0,
                        maxLife = 0.45 + Js.random() * 0.35,
                        size = (1.5 + Js.random() * 1.6) * this.scale,
                        kind = "spark",
                    });
                }
            }
        }

        private void drawRevoltAuraAndHud(double fxs, double fys, double bx, double by, double R)
        {
            var c = this.ctx;
            double s = Math.Max(0.85, this.scale);

            if (this.revoltFeverT > 0 && this.phase == "playing")
            {
                c.Save();
                c.GlobalCompositeOperation = Composite.Lighter;
                double pulse = 0.75 + 0.25 * Math.Sin(this.t * 9);
                var g = c.CreateRadialGradient(bx, by, R * 0.3, bx, by, R * 2.2);
                g.AddColorStop(0, Col.Rgba(255, 215, 110, Css.fixedNum(0.42 * pulse, 3)));
                g.AddColorStop(0.55, Col.Rgba(255, 95, 45, Css.fixedNum(0.24 * pulse, 3)));
                g.AddColorStop(1, "rgba(255, 60, 20, 0)");
                c.FillStyle = g;
                c.BeginPath();
                c.Arc(bx, by, R * 2.2, 0, Math.PI * 2);
                c.Fill();
                c.Restore();
            }

            // Small, sleek Revolt gauge below the top-left progress bar when charging or active
            if (this.phase == "playing" && !this.cutscene && !this.finaleOn && (this.revoltMeter > 0.02 || this.revoltFeverT > 0))
            {
                double fill = this.revoltFeverT > 0 ? this.revoltFeverT / 10.0 : this.revoltMeter;
                double gw = 110 * s, gh = 3.5 * s;
                double gx = 24 * s, gy = 48 * s;
                c.Save();
                c.FillStyle = "rgba(255, 255, 255, 0.14)";
                c.FillRect(gx, gy, gw, gh);
                c.FillStyle = this.revoltFeverT > 0 ? "rgba(255, 125, 55, 0.95)" : "rgba(235, 185, 90, 0.85)";
                c.FillRect(gx, gy, gw * Math.Max(0, Math.Min(1, fill)), gh);
                c.Restore();
            }
        }

        // ================================================================ 5. PERMANENT WORLD EVOLUTION — SANCTUARIES ("DAĞA KALICI İZ BIRAKMA")

        private bool sanctuary1Blessed = false;
        private bool sanctuary2Blessed = false;

        internal int sanctuaryStage()
        {
            double score = this.cycles + (this.saga != null ? this.saga.s.insight : 0);
            if (score >= 14) return 3;
            if (score >= 7) return 2;
            if (score >= 3) return 1;
            return 0;
        }

        private void updateSanctuaries()
        {
            int stg = this.sanctuaryStage();
            if (stg <= 0 || this.phase != "playing") return;
            double len = this.level.length;
            if (!this.sanctuary1Blessed && Math.Abs(this.x - len * 0.30) < 42)
            {
                this.sanctuary1Blessed = true;
                this.breath = Math.Min(1, this.breath + 0.25 * stg);
                this.addRevolt(0.10);
            }
            if (stg >= 2 && !this.sanctuary2Blessed && Math.Abs(this.x - len * 0.72) < 42)
            {
                this.sanctuary2Blessed = true;
                this.breath = Math.Min(1, this.breath + 0.35);
                this.addRevolt(0.15);
            }
        }

        private void drawSanctuaries(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            int stg = this.sanctuaryStage();
            if (stg <= 0) return;
            var c = this.ctx;
            double s = this.scale;
            double len = this.level.length;
            double[] spots = stg >= 2 ? new[] { len * 0.30, len * 0.72 } : new[] { len * 0.30 };

            for (int idx = 0; idx < spots.Length; idx++)
            {
                double wx = spots[idx];
                double sx = toScreenX(wx);
                if (sx < -120 || sx > this.w + 120) continue;
                double sy = toScreenY(Levels.terrainAt(this.level, wx));

                c.Save();
                // Stepped marble stylobate base
                c.FillStyle = "rgba(214, 202, 180, 0.68)";
                c.FillRect(sx - 36 * s, sy - 6 * s, 72 * s, 6 * s);
                c.FillRect(sx - 30 * s, sy - 11 * s, 60 * s, 5 * s);

                if (stg >= 2)
                {
                    // Fluted Doric marble columns
                    c.FillStyle = "rgba(228, 218, 198, 0.72)";
                    double[] cols = { -22, -7, 7, 22 };
                    for (int k = 0; k < cols.Length; k++)
                    {
                        c.FillRect(sx + cols[k] * s - 3 * s, sy - 56 * s, 6 * s, 45 * s);
                        c.FillRect(sx + cols[k] * s - 4.5 * s, sy - 59 * s, 9 * s, 3 * s);
                    }
                }
                if (stg >= 3)
                {
                    // Full Greek temple architrave + triangular pediment + sacred golden flame
                    c.FillStyle = "rgba(235, 224, 202, 0.78)";
                    c.FillRect(sx - 28 * s, sy - 65 * s, 56 * s, 6 * s);
                    c.BeginPath();
                    c.MoveTo(sx - 31 * s, sy - 65 * s);
                    c.LineTo(sx, sy - 82 * s);
                    c.LineTo(sx + 31 * s, sy - 65 * s);
                    c.ClosePath();
                    c.Fill();

                    c.GlobalCompositeOperation = Composite.Lighter;
                    double p = 0.75 + 0.25 * Math.Sin(this.t * 4.5 + idx);
                    var fg = c.CreateRadialGradient(sx, sy - 22 * s, 0, sx, sy - 22 * s, 18 * s);
                    fg.AddColorStop(0, Col.Rgba(255, 230, 140, Css.fixedNum(0.85 * p, 3)));
                    fg.AddColorStop(1, "rgba(255, 160, 50, 0)");
                    c.FillStyle = fg;
                    c.BeginPath();
                    c.Arc(sx, sy - 22 * s, 18 * s, 0, Math.PI * 2);
                    c.Fill();
                }
                c.Restore();
            }
        }

        // ================================================================ 6. MYTHIC COMPANIONS ("EFSANEVİ YOLDAŞLAR")

        public sealed class CompanionSpec
        {
            public readonly string id;
            public readonly int unlockAt;
            public CompanionSpec(string id, int unlockAt)
            {
                this.id = id;
                this.unlockAt = unlockAt;
            }
        }

        public static readonly CompanionSpec[] COMPANIONS =
        {
            new CompanionSpec("none", 0),
            new CompanionSpec("owl",  2),
            new CompanionSpec("fox",  6),
            new CompanionSpec("goat", 11),
        };

        public string companionId = "none";

        public static bool isCompanionUnlocked(string id, double summits, double insight)
        {
            for (int i = 0; i < COMPANIONS.Length; i++)
                if (COMPANIONS[i].id == id)
                    return Math.Max(summits, insight) >= COMPANIONS[i].unlockAt;
            return true;
        }

        public void setCompanion(string id)
        {
            if (string.IsNullOrEmpty(id)) { this.companionId = "none"; return; }
            for (int i = 0; i < COMPANIONS.Length; i++)
            {
                if (COMPANIONS[i].id == id) { this.companionId = id; return; }
            }
            this.companionId = "none";
        }

        public static (string name, string desc) companionText(string id, string lang)
        {
            int li = langIdx(lang);
            switch (id)
            {
                case "owl":
                    return (
                        new[] { "Athena'nın Baykuşu", "Owl of Athena", "雅典娜之owl", "एथेना का उल्लू", "Búho de Atenea", "Chouette d'Athéna", "بومة أثينا", "অ্যাথেনার পেঁচা", "Coruja de Atena" }[li],
                        new[] { "+%15 ritim penceresi; vuruş anında altın kanat çırpar", "+15% rhythm window; flares golden wings right on the beat", "+15% 节奏窗口；在节拍点展开金翼提示", "+15% लय विंडो; सही ताल पर सुनहरे पंख फैलाता है", "+15% ventana de ritmo; abre alas doradas al compás", "+15 % fenêtre de rythme ; bat des ailes d'or sur le temps", "+15٪ نافذة إيقاع؛ ترفرف بأجنحة ذهبية مع الإيقاع", "+১৫% ছন্দের জানালা; তালের সাথে ডানা মেলে", "+15% janela de ritmo; abre asas douradas no tempo certo" }[li]
                    );
                case "fox":
                    return (
                        new[] { "Olimpos Ateş Tilkisi", "Olympus Fire Fox", "奥林匹斯火狐", "ओलंपस अग्नि लोमड़ी", "Zorro Ígneo del Olimpo", "Renard de Feu de l'Olympe", "ثعلب نار أوليمبوس", "অলিম্পাস অগ্নি শিয়াল", "Raposa de Fogo do Olimpo" }[li],
                        new[] { "+%20 nefes yenilenmesi ve iniş kıvılcımlarını uzaktan çeker", "+20% breath recovery & magnetizes descent embers from afar", "+20% 气息恢复，并自动吸引下山火花", "+20% श्वास रिकवरी और चिंगारियों को दूर से खींचता है", "+20% recuperación de aliento y atrae chispas al bajar", "+20 % récupération de souffle et attire les étincelles", "+20٪ استعادة النَفَس وجذب شرارات الهبوط عن بُعد", "+২০% শ্বাস পুনরুদ্ধার এবং দূর থেকে স্ফুলিঙ্গ টানে", "+20% recuperação de fôlego e atrai centelhas na descida" }[li]
                    );
                case "goat":
                    return (
                        new[] { "Altın Boynuzlu Dağ Keçisi", "Golden-Horned Ibex", "金角山羊", "स्वर्ण-सींग वाला आइबेक्स", "Íbice de Cuernos de Oro", "Bouquetin aux Cornes d'Or", "وعل القرون الذهبية", "সোনালি শিংয়ের পাহাড়ি ছাগল", "Íbex de Chifres Dourados" }[li],
                        new[] { "Dik yokuşlarda sağlam tutuş sağlar ve kaymayı azaltır", "Steadies footing on steep pitches & reduces slip loss", "在陡坡上稳住步伐并减少滑落", "खड़ी चढ़ाई पर कदम स्थिर रखता है", "Asegura el paso en cuestas empinadas", "Stabilise le pas dans les pentes raides", "يثبّت القدم في المنحدرات الشديدة", "খাড়া ঢালে পা স্থির রাখে", "Firma o passo nas subidas íngremes" }[li]
                    );
                default:
                    return (
                        new[] { "Yalnız Tırmanış", "Solitary Climb", "独自攀登", "एकाकी चढ़ाई", "Ascenso Solitario", "Ascension Solitaire", "صعود منفرد", "একাকী আরোহণ", "Subida Solitária" }[li],
                        new[] { "Dağda yalnızca sen ve taşın", "Only you and your stone upon the hill", "山丘上只有你与巨石", "पहाड़ी पर केवल आप और आपका पत्थर", "Solo tú y tu roca en la colina", "Seul avec votre pierre sur la colline", "أنت وصخرتك فقط على التل", "পাহাড়ে শুধু তুমি আর তোমার পাথর", "Apenas você e sua rocha na colina" }[li]
                    );
            }
        }

        private void drawCompanion(Func<double, double> toScreenX, Func<double, double> toScreenY, double fwx)
        {
            if (string.IsNullOrEmpty(this.companionId) || this.companionId == "none") return;
            var c = this.ctx;
            double s = this.scale;

            if (this.companionId == "owl")
            {
                double ox = toScreenX(fwx + 28) + Math.Sin(this.t * 2.1) * 10 * s;
                double oy = toScreenY(Levels.terrainAt(this.level, fwx + 28)) - (108 + Math.Cos(this.t * 3.4) * 8) * s;
                bool onBeat = Math.Abs(this.beatError()) <= 0.11;
                double flap = Math.Sin(this.t * 11) * (onBeat ? 11 : 6) * s;

                c.Save();
                if (onBeat)
                {
                    c.GlobalCompositeOperation = Composite.Lighter;
                    c.FillStyle = "rgba(255, 225, 130, 0.35)";
                    c.BeginPath();
                    c.Arc(ox, oy, 14 * s, 0, Math.PI * 2);
                    c.Fill();
                }
                c.StrokeStyle = onBeat ? "rgba(255, 232, 155, 0.98)" : "rgba(232, 214, 178, 0.9)";
                c.LineWidth = 2.0 * s;
                c.BeginPath();
                c.MoveTo(ox - 11 * s, oy - flap);
                c.QuadraticCurveTo(ox - 5 * s, oy - 3 * s, ox, oy);
                c.QuadraticCurveTo(ox + 5 * s, oy - 3 * s, ox + 11 * s, oy - flap);
                c.Stroke();
                c.FillStyle = "rgba(240, 220, 175, 0.95)";
                c.BeginPath();
                c.Arc(ox, oy, 3.2 * s, 0, Math.PI * 2);
                c.Fill();
                c.Restore();
            }
            else if (this.companionId == "fox")
            {
                double lead = this.phase == "descent" ? -52 : 98;
                double fx = toScreenX(fwx + lead);
                double fy = toScreenY(Levels.terrainAt(this.level, fwx + lead)) - Math.Abs(Math.Sin(this.t * 7)) * 4 * s;
                c.Save();
                c.Translate(fx, fy);
                if (this.phase == "descent") c.Scale(-1, 1);
                c.FillStyle = "rgba(235, 118, 42, 0.94)";
                c.BeginPath();
                c.Ellipse(0, -7 * s, 9 * s, 4.5 * s, 0, 0, Math.PI * 2);
                c.Fill();
                // Head & ears
                c.BeginPath();
                c.Arc(8 * s, -10 * s, 3.8 * s, 0, Math.PI * 2);
                c.MoveTo(6 * s, -13 * s); c.LineTo(8 * s, -18 * s); c.LineTo(10 * s, -13 * s);
                c.Fill();
                // Glowing golden tail
                c.FillStyle = "rgba(255, 205, 95, 0.92)";
                c.BeginPath();
                c.Ellipse(-10 * s, -9 * s + Math.Sin(this.t * 8) * 1.5 * s, 6 * s, 2.8 * s, -0.3, 0, Math.PI * 2);
                c.Fill();
                c.Restore();
            }
            else if (this.companionId == "goat")
            {
                double gx = toScreenX(fwx - 38);
                double gy = toScreenY(Levels.terrainAt(this.level, fwx - 38)) - Math.Abs(Math.Sin(this.t * 5.5)) * 3 * s;
                c.Save();
                c.Translate(gx, gy);
                c.FillStyle = "rgba(218, 206, 185, 0.92)";
                c.BeginPath();
                c.Ellipse(0, -10 * s, 10 * s, 5.5 * s, 0, 0, Math.PI * 2);
                c.Arc(8 * s, -15 * s, 4 * s, 0, Math.PI * 2);
                c.Fill();
                // Curved golden horns
                c.StrokeStyle = "rgba(245, 198, 88, 0.96)";
                c.LineWidth = 2.0 * s;
                c.BeginPath();
                c.Arc(5 * s, -19 * s, 6 * s, -Math.PI * 0.85, 0.15);
                c.Stroke();
                c.Restore();
            }
        }

        // ================================================================ 7. 2.5D DEPTH, PERSPECTIVE & VOLUMETRIC SHADING
        //
        // Transforms the flat 2D slope cut into a rich 2.5D mountainside:
        // - Back-edge retaining wall / distant mountain rim behind the path (Z = -1)
        // - Wide 3D perspective stone-paved trail deck with perspective cross-joints,
        //   twin wheel/boulder ruts, and raised curb stones (Z = -0.5 .. +0.5)
        // - Front cliff bevel, chiseled 3D rock strata & vertical cliff ribs dropping
        //   down toward the camera (Z = +0.5 .. +1.2)
        // - Multi-depth Z-plane parallax pillars, valley mist, and close foreground silhouettes
        // - 3D spherical volumetric shading on the boulder (normal-mapped specular highlight,
        //   equatorial 3D latitude/longitude rock fracture rings, deep 3D ground furrow)

        private double camTilt25D = 0;

        /// <summary>Updates the dynamic 2.5D camera perspective pitch/yaw shift based on speed and slope.</summary>
        internal double get25DCamTilt()
        {
            double slope = this.level != null ? Levels.slopeAt(this.level, this.phase == "descent" ? this.walkX : this.x) : 0.35;
            double vFactor = Math.Max(-1, Math.Min(1, this.vx / 320.0));
            double want = (slope - 0.35) * 18.0 + vFactor * 8.5 + (this.cutscene ? -6.0 : 0);
            this.camTilt25D += (want - this.camTilt25D) * Math.Min(1, this.frameDt * 3.5);
            return this.camTilt25D;
        }

        /// <summary>
        /// Draws the 2.5D far-side mountain shelf, distant retaining wall, and Z-depth valley mist
        /// behind the main trail before the main ground polygon is filled.
        /// Uses O(1) linear stroke-strips and coarse stride sampling so zero concave polygon
        /// tessellation (EarClip / LibTessDotNet) is ever triggered.
        /// </summary>
        private void draw25DBackRimAndMist(double[] tys, int tn, Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (tys == null || tn < 2) return;
            var c = this.ctx;
            double s = this.scale;
            double w = this.w, h = this.h;
            double tilt = this.get25DCamTilt() * 0.18 * s;
            double backDepth = (15.5 + tilt * 0.25) * s;
            double tsx(int i) { return Math.Min(-2 + i * 4, w + 2); }
            int stride = this.lowQuality ? 12 : 6; // every 24–48px (~35 points instead of 480)

            c.Save();

            // 1. Atmospheric 2.5D valley mist rising from behind the mountain path
            if (!this.lowQuality)
            {
                var mistG = c.CreateLinearGradient(0, this.groundY - 150 * s, 0, this.groundY + 40 * s);
                mistG.AddColorStop(0, "rgba(210, 185, 155, 0)");
                mistG.AddColorStop(0.55, "rgba(205, 175, 145, 0.13)");
                mistG.AddColorStop(1, "rgba(35, 28, 42, 0.28)");
                c.FillStyle = mistG;
                c.FillRect(0, this.groundY - 150 * s, w, 190 * s);
            }

            // 2. Far-edge receding mountain shelf (Z = -1 back rim of the 2.5D road)
            // Drawn as a fast O(k) stroke strip (zero Tessellator / LibTessDotNet cost!)
            var backG = c.CreateLinearGradient(0, this.groundY - 120 * s, 0, this.groundY + 30 * s);
            backG.AddColorStop(0, "oklch(0.48 0.055 58 / 0.88)");
            backG.AddColorStop(0.6, "oklch(0.35 0.045 50 / 0.94)");
            backG.AddColorStop(1, "oklch(0.24 0.035 44 / 0.98)");
            c.StrokeStyle = backG;
            c.LineWidth = backDepth + 8 * s;
            c.LineCap = LineCap.Butt;
            c.LineJoin = LineJoin.Bevel;
            c.BeginPath();
            for (int i = 0; i < tn; i += stride)
            {
                double px = tsx(i) - 3 * s;
                double py = tys[i] - backDepth * 0.45;
                if (i == 0) c.MoveTo(px, py);
                else c.LineTo(px, py);
            }
            if ((tn - 1) % stride != 0)
            {
                int last = tn - 1;
                c.LineTo(tsx(last) - 3 * s, tys[last] - backDepth * 0.45);
            }
            c.Stroke();

            // Sunlit top highlight along the far Z-edge of the path
            var sun = this.lightScreen();
            double sunK = Math.Max(0.25, Math.Min(1, sun.strength * 1.15));
            c.StrokeStyle = Sky.rgb(sun.color, 0.42 * sunK);
            c.LineWidth = 1.6 * s;
            c.BeginPath();
            for (int i = 0; i < tn; i += stride)
            {
                double wx = this.camX + tsx(i) / s;
                double und = Math.Sin(wx * 0.019) * 2.2 * s + Math.Cos(wx * 0.047) * 1.1 * s;
                double px = tsx(i) - 6 * s;
                double py = tys[i] - backDepth + und;
                if (i == 0) c.MoveTo(px, py);
                else c.LineTo(px, py);
            }
            if ((tn - 1) % stride != 0)
            {
                int last = tn - 1;
                double wx = this.camX + tsx(last) / s;
                double und = Math.Sin(wx * 0.019) * 2.2 * s + Math.Cos(wx * 0.047) * 1.1 * s;
                c.LineTo(tsx(last) - 6 * s, tys[last] - backDepth + und);
            }
            c.Stroke();

            // 3. Mid-depth Z-plane ancient ruins / broken colonnade silhouettes along the far edge
            const double colStep = 280;
            double startW = Math.Floor((this.camX - 100) / colStep) * colStep;
            double endW = this.camX + w / s + 140;
            for (double wx = startW; wx <= endW; wx += colStep)
            {
                double h1 = this.hash(wx * 0.71 + 9.3);
                if (h1 < 0.52) continue;
                double cwx = wx + (h1 - 0.5) * 90;
                double sx = toScreenX(cwx) - 8 * s;
                if (sx < -80 || sx > w + 80) continue;
                double sy = toScreenY(Levels.terrainAt(this.level, cwx)) - backDepth + 2 * s;
                double colH = (22 + this.hash(wx * 1.93) * 34) * s;
                double colW = (5.5 + h1 * 2.5) * s;

                // Distant weathered stone pillar on the far side of the road
                c.FillStyle = "oklch(0.32 0.035 50 / 0.72)";
                c.FillRect(sx - colW * 0.5, sy - colH, colW, colH);
                // 3D side face of the pillar (lit side vs shaded side)
                c.FillStyle = Sky.rgb(sun.color, 0.25 * sunK);
                c.FillRect(sx - colW * 0.5, sy - colH, colW * 0.32, colH);
                // Capital / broken top
                c.FillStyle = "oklch(0.38 0.04 54 / 0.78)";
                c.FillRect(sx - colW * 0.68, sy - colH - 3 * s, colW * 1.36, 3.2 * s);
            }

            c.Restore();
        }

        /// <summary>
        /// Draws the 2.5D perspective road deck, stone slab cross-joints, boulder wheel-ruts,
        /// front cliff bevel, and 3D rock strata ribs across the mountain face.
        /// Uses fast linear stroke-ribbons and 4-vertex convex quads for 60+ FPS performance.
        /// </summary>
        private void draw25DTerrainDeckAndCliff(double[] tys, int tn, Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (tys == null || tn < 2) return;
            var c = this.ctx;
            double s = this.scale;
            double w = this.w, h = this.h;
            double tilt = this.get25DCamTilt() * 0.18 * s;
            double backOffY = (-12.5 - tilt * 0.2) * s;
            double frontEdgeY = (11.5 + tilt * 0.22) * s;
            double bevelDropY = frontEdgeY + 18.0 * s;
            double tsx(int i) { return Math.Min(-2 + i * 4, w + 2); }
            var sun = this.lightScreen();
            double sunK = Math.Max(0.2, Math.Min(1, sun.strength * 1.15));
            int stride = this.lowQuality ? 12 : 6; // every 24–48px (~35 segments instead of 480)

            c.Save();
            c.LineCap = LineCap.Butt;
            c.LineJoin = LineJoin.Bevel;

            // 1. 2.5D Road Deck Surface (drawn as an O(k) thick stroke strip — zero concave Fill tessellation!)
            double deckMidY = (backOffY + frontEdgeY) * 0.5;
            double deckThick = Math.Max(4 * s, frontEdgeY - backOffY);
            var deckG = c.CreateLinearGradient(0, this.groundY - 90 * s, 0, this.groundY + 50 * s);
            deckG.AddColorStop(0, "oklch(0.46 0.055 60 / 0.72)");
            deckG.AddColorStop(0.5, "oklch(0.39 0.048 54 / 0.65)");
            deckG.AddColorStop(1, "oklch(0.31 0.04 46 / 0.78)");
            c.StrokeStyle = deckG;
            c.LineWidth = deckThick;
            c.BeginPath();
            for (int i = 0; i < tn; i += stride)
            {
                double px = tsx(i);
                double py = tys[i] + deckMidY;
                if (i == 0) c.MoveTo(px, py);
                else c.LineTo(px, py);
            }
            if ((tn - 1) % stride != 0)
            {
                int last = tn - 1;
                c.LineTo(tsx(last), tys[last] + deckMidY);
            }
            c.Stroke();

            // 2. Twin 2.5D worn ruts/grooves carved into the road by the boulder over eternity
            for (int rut = 0; rut < 2; rut++)
            {
                double rutZ = rut == 0 ? -3.5 * s : 3.2 * s;
                c.StrokeStyle = rut == 0 ? "rgba(28, 18, 14, 0.34)" : "rgba(28, 18, 14, 0.42)";
                c.LineWidth = (rut == 0 ? 3.2 : 4.4) * s;
                c.BeginPath();
                for (int i = 0; i < tn; i += stride)
                {
                    double py = tys[i] + rutZ;
                    if (i == 0) c.MoveTo(tsx(i), py);
                    else c.LineTo(tsx(i), py);
                }
                if ((tn - 1) % stride != 0)
                {
                    int last = tn - 1;
                    c.LineTo(tsx(last), tys[last] + rutZ);
                }
                c.Stroke();
            }

            // 3. Front Cliff Bevel Lip (drawn as a fast O(k) thick stroke strip + rim highlight)
            double bevelThick = Math.Max(4 * s, bevelDropY - frontEdgeY);
            double bevelMidY = (frontEdgeY + bevelDropY) * 0.5;
            var bevelG = c.CreateLinearGradient(0, this.groundY - 40 * s, 0, this.groundY + 80 * s);
            bevelG.AddColorStop(0, "oklch(0.22 0.032 44 / 0.88)");
            bevelG.AddColorStop(1, "oklch(0.14 0.024 38 / 0.94)");
            c.StrokeStyle = bevelG;
            c.LineWidth = bevelThick;
            c.BeginPath();
            for (int i = 0; i < tn; i += stride)
            {
                double px = tsx(i) + 4 * s;
                double py = tys[i] + bevelMidY;
                if (i == 0) c.MoveTo(px, py);
                else c.LineTo(px, py);
            }
            if ((tn - 1) % stride != 0)
            {
                int last = tn - 1;
                c.LineTo(tsx(last) + 4 * s, tys[last] + bevelMidY);
            }
            c.Stroke();

            // Bright rim line along the front cliff edge
            c.StrokeStyle = Sky.rgb(sun.color, 0.35 * sunK);
            c.LineWidth = 1.3 * s;
            c.BeginPath();
            for (int i = 0; i < tn; i += stride)
            {
                double px = tsx(i) + 3 * s;
                double py = tys[i] + frontEdgeY;
                if (i == 0) c.MoveTo(px, py);
                else c.LineTo(px, py);
            }
            if ((tn - 1) % stride != 0)
            {
                int last = tn - 1;
                c.LineTo(tsx(last) + 3 * s, tys[last] + frontEdgeY);
            }
            c.Stroke();

            // 4. Perspective cross-stone joints & roadside curb stones (batched into 1 path per style!)
            double slabStep = this.lowQuality ? 96 : 64;
            double firstW = Math.Floor((this.camX - 40) / slabStep) * slabStep;
            double lastW = this.camX + w / s + 40;

            c.StrokeStyle = "rgba(22, 14, 10, 0.32)";
            c.LineWidth = 1.3 * s;
            c.BeginPath();
            for (double wx = firstW; wx <= lastW; wx += slabStep)
            {
                double h1 = this.hash(wx * 1.13 + 5.1);
                if (h1 <= 0.25) continue;
                double sx = toScreenX(wx);
                if (sx < -30 || sx > w + 30) continue;
                double sy = toScreenY(Levels.terrainAt(this.level, wx));
                double sl = Levels.slopeAt(this.level, wx);
                double xFar = sx - (7.5 + sl * 3.5) * s;
                double yFar = sy + backOffY + 1.5 * s;
                double xNear = sx + (6.5 - sl * 2.5) * s;
                double yNear = sy + frontEdgeY - 1.0 * s;
                c.MoveTo(xFar, yFar);
                c.LineTo(xNear, yNear);
            }
            c.Stroke();

            // 3D beveled curb stone blocks along the front lip of the road (fast FillRect pairs)
            if (!this.lowQuality)
            {
                for (double wx = firstW; wx <= lastW; wx += slabStep)
                {
                    double h1 = this.hash(wx * 1.13 + 5.1);
                    if (h1 <= 0.42) continue;
                    double sx = toScreenX(wx);
                    if (sx < -30 || sx > w + 30) continue;
                    double sy = toScreenY(Levels.terrainAt(this.level, wx));
                    double sl = Levels.slopeAt(this.level, wx);
                    double xNear = sx + (6.5 - sl * 2.5) * s;
                    double yNear = sy + frontEdgeY - 1.0 * s;
                    double bw = (10 + h1 * 9) * s;
                    double bh = (4.2 + (h1 * 3 % 1) * 2.5) * s;
                    double bx0 = xNear - bw * 0.5;
                    double by0 = yNear - bh * 0.35;

                    c.FillStyle = "oklch(0.49 0.055 60 / 0.82)";
                    c.FillRect(bx0, by0, bw, bh * 0.6);
                    c.FillStyle = "oklch(0.24 0.032 45 / 0.88)";
                    c.FillRect(bx0 + 1.5 * s, by0 + bh * 0.6, bw, bh * 0.9);
                }
            }

            // 5. 3D Chiseled Cliff Ribs & Geological Strata descending down the front face (4-vertex convex quads)
            double ribStep = this.lowQuality ? 220 : 140;
            double rStart = Math.Floor((this.camX - 60) / ribStep) * ribStep;
            double rEnd = this.camX + w / s + 60;
            for (double wx = rStart; wx <= rEnd; wx += ribStep)
            {
                double h1 = this.hash(wx * 0.91 + 19.7);
                if (h1 < 0.35) continue;
                double rwx = wx + (h1 - 0.5) * 38;
                double sx = toScreenX(rwx);
                if (sx < -60 || sx > w + 60) continue;
                double sy = toScreenY(Levels.terrainAt(this.level, rwx)) + bevelDropY;
                double ribH = (55 + h1 * 95) * s;
                double ribW = (14 + (h1 * 7 % 1) * 18) * s;
                double lean = (h1 - 0.45) * 22 * s;

                // Sunlit left facet (strictly 4-vertex convex quad -> instant Fan)
                c.FillStyle = "oklch(0.29 0.036 52 / 0.38)";
                c.BeginPath();
                c.MoveTo(sx - ribW * 0.45, sy);
                c.LineTo(sx, sy + 2 * s);
                c.LineTo(sx + lean * 0.6, sy + ribH);
                c.LineTo(sx - ribW * 0.65 + lean * 0.5, sy + ribH);
                c.ClosePath();
                c.Fill();

                // Deep shadowed right facet (strictly 4-vertex convex quad -> instant Fan)
                c.FillStyle = "oklch(0.13 0.02 36 / 0.52)";
                c.BeginPath();
                c.MoveTo(sx, sy + 2 * s);
                c.LineTo(sx + ribW * 0.55, sy + 2 * s);
                c.LineTo(sx + ribW * 0.45 + lean * 0.6, sy + ribH);
                c.LineTo(sx + lean * 0.6, sy + ribH);
                c.ClosePath();
                c.Fill();
            }

            c.Restore();
        }

        /// <summary>
        /// Adds 3D spherical volumetric normals, latitude/longitude chiseled relief rings,
        /// and a deep 3D furrow shadow under the boulder so it reads as a true 3D sphere rolling in space.
        /// </summary>
        private void draw25DBoulderVolumetric(Ctx c, double R)
        {
            if (this.lowQuality) return;
            var sun = this.lightScreen();
            double sunK = Math.Max(0.25, Math.Min(1, sun.strength * 1.2));

            c.Save();

            // 1. 3D rotating latitude & longitude rock seams on the sphere
            double phaseA = this.roll * 1.35;
            double tilt = Math.Sin(phaseA) * 0.72;
            double minor = Math.Max(0.10, Math.Abs(tilt)) * R * 0.92;
            c.LineCap = LineCap.Butt;
            c.StrokeStyle = "rgba(18, 12, 10, 0.34)";
            c.LineWidth = 1.6 * this.scale;
            c.BeginPath();
            c.Ellipse(0, 0, R * 0.94, minor, 0.35, 0, Math.PI * 2);
            c.Stroke();

            c.StrokeStyle = Sky.rgb(sun.color, 0.22 * sunK);
            c.LineWidth = 0.9 * this.scale;
            c.BeginPath();
            c.Ellipse(0, -1.0 * this.scale, R * 0.93, minor, 0.35, 0, Math.PI);
            c.Stroke();

            // 2. Off-center 3D spherical specular highlight (upper-sunward hemisphere)
            double specX = -R * 0.28;
            double specY = -R * 0.34;
            var specG = c.CreateRadialGradient(specX, specY, R * 0.04, specX, specY, R * 0.85);
            specG.AddColorStop(0, Sky.rgb(sun.color, 0.36 * sunK));
            specG.AddColorStop(0.5, Sky.rgb(sun.color, 0.12 * sunK));
            specG.AddColorStop(1, "rgba(255, 220, 180, 0)");
            c.FillStyle = specG;
            c.FillRect(-R * 1.1, -R * 1.1, R * 2.2, R * 2.2);

            c.Restore();
        }

        /// <summary>
        /// Draws a 3D recessed ground trench/furrow directly beneath the boulder and Sisyphus's feet
        /// so they sit *inside* the 2.5D road surface rather than floating on a line.
        /// </summary>
        private void draw25DContactFurrow(double fxs, double fys, double bx, double by, double R, double ang)
        {
            var c = this.ctx;
            double s = this.scale;
            c.Save();

            // Sisyphus's 2.5D foot depression / depth shadow on the road deck
            c.FillStyle = "rgba(12, 8, 10, 0.42)";
            c.BeginPath();
            c.Ellipse(fxs, fys + 2.5 * s, 22 * s, 5.5 * s, ang * 0.7, 0, Math.PI * 2);
            c.Fill();

            // Boulder's 3D soil-displacement rim & deep ambient occlusion pit
            c.FillStyle = "rgba(10, 6, 8, 0.62)";
            c.BeginPath();
            c.Ellipse(bx, by + 3.5 * s, R * 1.08, R * 0.30, ang, 0, Math.PI * 2);
            c.Fill();

            // Raised soil berm pushed up in front of and beside the heavy boulder
            var sun = this.lightScreen();
            c.StrokeStyle = Sky.rgb(sun.color, 0.28 * Math.Min(1, sun.strength));
            c.LineWidth = 2.4 * s;
            c.BeginPath();
            c.Ellipse(bx, by + 3.5 * s, R * 1.12, R * 0.33, ang, 0.15, Math.PI - 0.15);
            c.Stroke();

            c.Restore();
        }

        /// <summary>
        /// Draws ultra-near 2.5D foreground parallax pillars, archways, and cliff ledges (scrolling at 1.85x)
        /// that pass between the camera and the action, completing the multi-plane 2.5D depth illusion.
        /// </summary>
        private void draw25DForegroundDepth()
        {
            if (this.lowQuality) return;
            var c = this.ctx;
            double w = this.w, h = this.h;
            double s = this.scale;
            double scroll = this.camX * 1.85 * s;
            double period = 680 * s;
            double first = Math.Floor(scroll / period) - 1;

            c.Save();
            for (double i = first; i * period - scroll < w + period; i++)
            {
                double h1 = this.hash(i * 5.17 + 1.9);
                if (h1 < 0.48) continue;
                double x = i * period - scroll + h1 * period * 0.5;
                double colW = (32 + h1 * 24) * s;
                double colH = (115 + (h1 * 13 % 1) * 95) * s;

                // Dark out-of-focus foreground stone monolith / broken column base
                var fgG = c.CreateLinearGradient(x - colW, h - colH, x + colW, h);
                fgG.AddColorStop(0, "rgba(14, 11, 16, 0.88)");
                fgG.AddColorStop(1, "rgba(8, 6, 10, 0.96)");
                c.FillStyle = fgG;
                c.BeginPath();
                c.MoveTo(x - colW * 0.65, h + 10);
                c.LineTo(x - colW * 0.5, h - colH);
                c.LineTo(x + colW * 0.15, h - colH - 8 * s);
                c.LineTo(x + colW * 0.55, h - colH + 12 * s);
                c.LineTo(x + colW * 0.7, h + 10);
                c.ClosePath();
                c.Fill();

                // 3D rim-lit edge on the foreground pillar
                var sun = this.lightScreen();
                c.StrokeStyle = Sky.rgb(sun.color, 0.24 * Math.Min(1, sun.strength));
                c.LineWidth = 2.0 * s;
                c.BeginPath();
                c.MoveTo(x - colW * 0.5, h - colH);
                c.LineTo(x + colW * 0.15, h - colH - 8 * s);
                c.LineTo(x + colW * 0.55, h - colH + 12 * s);
                c.Stroke();
            }
            c.Restore();
        }
    }
}
