using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// Active gameplay systems that deepen the climb, descent, boulder styles, and Gods' Trials:
    /// 1. 5 Fated Stones ("granite" | "marble" | "iron" | "amber" | "column") with unique physics & visuals.
    /// 2. Flow Momentum & Roll Rush: high streak speeds up rhythm, boosts push, and powers over ledges.
    /// 3. Tailwind Gusts, Storm Lightning Parry, and Slip Ghost Absorption.
    /// 4. Descent Gamification: downhill slide/sprint and Golden Embers of Olympus ("Olimpos Ateşi").
    /// 5. Dynamic Hill Mini-Quests: Gods' Trials (Hermes, Athena, Ares) & Daily Challenge Climb.
    /// </summary>
    public partial class SisyphusEngine
    {
        // ================================================================ 1. FATED STONES ("KADER TAŞLARI")

        public sealed class StoneSpec
        {
            public readonly string id;
            public readonly int unlockAt;
            public readonly double pushMult;
            public readonly double frictionMult;
            public readonly double hazardResist;
            public readonly double slipMult;
            public readonly double windowMult;
            public readonly int flowAt;
            public readonly double kickMult;
            public readonly double sprintMult;

            public StoneSpec(
                string id,
                int unlockAt,
                double pushMult = 1.0,
                double frictionMult = 1.0,
                double hazardResist = 0.0,
                double slipMult = 1.0,
                double windowMult = 1.0,
                int flowAt = 5,
                double kickMult = 1.0,
                double sprintMult = 1.0)
            {
                this.id = id;
                this.unlockAt = unlockAt;
                this.pushMult = pushMult;
                this.frictionMult = frictionMult;
                this.hazardResist = hazardResist;
                this.slipMult = slipMult;
                this.windowMult = windowMult;
                this.flowAt = flowAt;
                this.kickMult = kickMult;
                this.sprintMult = sprintMult;
            }
        }

        public static readonly StoneSpec[] STONES =
        {
            new StoneSpec("granite", 0),
            new StoneSpec("marble",  3,  pushMult: 1.0,  frictionMult: 1.0,  windowMult: 1.15),
            new StoneSpec("iron",    7,  hazardResist: 0.65, slipMult: 0.55),
            new StoneSpec("amber",   12, windowMult: 1.25, flowAt: 3),
            new StoneSpec("column",  18, hazardResist: 0.40, slipMult: 0.65, windowMult: 1.15),
        };

        public string stoneId = "granite";

        public static StoneSpec getStoneSpec(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < STONES.Length; i++)
                    if (STONES[i].id == id) return STONES[i];
            }
            return STONES[0];
        }

        public StoneSpec stoneSpec { get { return getStoneSpec(this.stoneId); } }

        public static bool isStoneUnlocked(string id, double summits, double insight)
        {
            var sp = getStoneSpec(id);
            return Math.Max(summits, insight) >= sp.unlockAt;
        }

        public void setStone(string id)
        {
            var sp = getStoneSpec(id);
            this.stoneId = sp.id;
        }

        public static (string name, string desc) stoneText(string id, string lang)
        {
            int li = langIdx(lang);
            switch (id)
            {
                case "marble":
                    return (
                        new[] { "Paros Mermeri", "Parian Marble", "帕罗斯大理石", "पारोस संगमरमर", "Mármol de Paros", "Marbre de Paros", "رخام باروس", "পারোস মার্বেল", "Mármore de Paros" }[li],
                        new[] { "+%15 ritim penceresi · altın damarlı beyaz mermer", "+15% rhythm window · gold-veined white marble", "+15% 节奏判定窗口 · 金脉白大理石", "+15% लय विंडो · स्वर्ण-शिराओं वाला संगमरमर", "+15% ventana de ritmo · mármol blanco veteado", "+15 % fenêtre de rythme · marbre blanc veiné d'or", "+15٪ نافذة إيقاع · رخام أبيض بعروق ذهبية", "+১৫% ছন্দের জানালা · সোনালি শিরাযুক্ত সাদা মার্বেল", "+15% janela de ritmo · mármore branco com veios dourados" }[li]
                    );
                case "iron":
                    return (
                        new[] { "Hephaistos Demiri", "Hephaestus Iron", "赫菲斯托斯之铁", "हेफेस्टस लोहा", "Hierro de Hefesto", "Fer d'Héphaïstos", "حديد هيفايستوس", "হেফাইস্তুস লোহা", "Ferro de Hefesto" }[li],
                        new[] { "Çarşak, bataklık ve rüzgâr cezası -%65, kayma süresi -%45", "-65% scree, mire & wind penalty, -45% slip duration", "碎石、泥沼与狂风减速 -65%，滑落时间 -45%", "कंकड़, दलदल और हवा की बाधा -65%, फिसलन समय -45%", "-65% penalización por pedregal, lodo y viento; -45% deslizamiento", "-65 % pénalité d'éboulis, boue et vent, -45 % durée de glissade", "-65٪ عقوبة الحصى والوحل والرياح، -45٪ مدة الانزلاق", "পাথর, কাদা ও বাতাসের বাধা -৬৫%, পিছলে যাওয়ার সময় -৪৫%", "-65% penalidade de cascalho, lama e vento, -45% tempo de deslize" }[li]
                    );
                case "amber":
                    return (
                        new[] { "Güneş Kehribarı", "Sun Amber", "太阳琥珀", "सूर्य अंबर", "Ámbar Solar", "Ambre Solaire", "كهرمان الشمس", "সূর্য অ্যাম্বার", "Âmbar Solar" }[li],
                        new[] { "+%25 geniş ritim penceresi, 3 seride Akış (Flow) başlar", "+25% wider rhythm window, enters Flow at 3 streak", "+25% 节奏判定窗口，3 连击即可进入心流", "+25% चौड़ा लय विंडो, 3 स्ट्रीक पर प्रवाह शुरू", "+25% ventana de ritmo, entra en Flujo con racha de 3", "+25 % fenêtre de rythme, entre en Flux dès 3 parfaites", "+25٪ نافذة إيقاع أوسع، يدخل التدفق عند سلسلة 3", "+২৫% প্রশস্ত ছন্দের জানালা, ৩ স্ট্রিকেই ফ্লো শুরু", "+25% janela de ritmo, entra em Fluxo com sequência de 3" }[li]
                    );
                case "column":
                    return (
                        new[] { "Tapınak Tamburu", "Temple Drum", "神庙柱鼓", "मंदिर स्तंभ", "Tambor de Templo", "Tambour de Temple", "أسطوانة المعبد", "মন্দিরের স্তম্ভ", "Tambor de Templo" }[li],
                        new[] { "Dengeli antik sütun · -%40 engel cezası, +%15 ritim penceresi", "Balanced temple drum · -40% hazard penalty, +15% rhythm window", "均衡神庙石柱 · -40% 地形减速，+15% 节奏窗口", "संतुलित मंदिर स्तंभ · -40% बाधा, +15% लय विंडो", "Tambor de templo · -40% penalización, +15% ventana de ritmo", "Tambour de temple · -40 % pénalité, +15 % fenêtre de rythme", "أسطوانة المعبد · -40٪ عقوبة التضاريس، +15٪ نافذة إيقاع", "ভারসাম্যপূর্ণ স্তম্ভ · -৪০% বাধা, +১৫% ছন্দের জানালা", "Tambor de templo · -40% penalidade, +15% janela de ritmo" }[li]
                    );
                default:
                    return (
                        new[] { "Korint Graniti", "Corinthian Granite", "科林斯花岗岩", "कोरिंथियन ग्रेनाइट", "Granito de Corinto", "Granit de Corinthe", "غرانيت كورنثوس", "করিন্থীয় গ্রানাইট", "Granito de Corinto" }[li],
                        new[] { "Klasik, dengeli tepe kayası", "The classic balanced boulder of the hill", "经典均衡的山丘巨石", "पहाड़ी का क्लासिक संतुलित पत्थर", "La roca clásica y equilibrada de la colina", "Le rocher classique et équilibré de la colline", "صخرة التل الكلاسيكية المتوازنة", "পাহাড়ের ধ্রুপদী ভারসাম্যপূর্ণ পাথর", "A rocha clássica e equilibrada da colina" }[li]
                    );
            }
        }

        internal static int langIdx(string lang)
        {
            return lang == "tr" ? 0 : lang == "en" ? 1 : lang == "zh" ? 2 : lang == "hi" ? 3 : lang == "es" ? 4 : lang == "fr" ? 5 : lang == "ar" ? 6 : lang == "bn" ? 7 : lang == "pt" ? 8 : 1;
        }

        internal int flowThreshold()
        {
            return this.stoneSpec.flowAt;
        }

        /// <summary>Draws material-specific visual overlays inside drawBoulder's clipped local space.</summary>
        private void drawStoneStyleOverlay(Ctx ctx, double R)
        {
            string id = this.stoneId;
            double s = this.scale;
            if (id == "marble")
            {
                ctx.Save();
                ctx.FillStyle = "rgba(245, 240, 232, 0.34)";
                ctx.FillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);
                ctx.LineCap = LineCap.Round;
                for (int i = 0; i < 6; i++)
                {
                    double a = i * 1.05 + 0.3;
                    double x0 = Math.Cos(a) * R * 0.85, y0 = Math.Sin(a) * R * 0.85;
                    double x1 = Math.Cos(a + 2.2) * R * 0.85, y1 = Math.Sin(a + 2.2) * R * 0.85;
                    double cx = (this.hash(i * 4.1) - 0.5) * R * 0.6;
                    double cy = (this.hash(i * 7.3) - 0.5) * R * 0.6;
                    ctx.StrokeStyle = i % 2 == 0 ? "rgba(218, 175, 92, 0.55)" : "rgba(255, 252, 245, 0.45)";
                    ctx.LineWidth = (i % 2 == 0 ? 1.5 : 1.0) * s;
                    ctx.BeginPath();
                    ctx.MoveTo(x0, y0);
                    ctx.QuadraticCurveTo(cx, cy, x1, y1);
                    ctx.Stroke();
                }
                ctx.Restore();
            }
            else if (id == "iron")
            {
                ctx.Save();
                ctx.FillStyle = "rgba(18, 22, 30, 0.46)";
                ctx.FillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);
                double pulse = 0.65 + 0.35 * Math.Sin(this.t * 2.4);
                ctx.StrokeStyle = Col.Rgba(255, 110, 42, Css.fixedNum(0.55 * pulse, 3));
                ctx.LineWidth = 1.4 * s;
                for (int i = 0; i < 5; i++)
                {
                    double a = i * 1.25;
                    ctx.BeginPath();
                    ctx.MoveTo(Math.Cos(a) * R * 0.2, Math.Sin(a) * R * 0.2);
                    ctx.LineTo(Math.Cos(a + 0.4) * R * 0.78, Math.Sin(a + 0.4) * R * 0.78);
                    ctx.Stroke();
                }
                ctx.Restore();
            }
            else if (id == "amber")
            {
                ctx.Save();
                double pulse = 0.75 + 0.25 * Math.Sin(this.t * 2.1);
                var g = ctx.CreateRadialGradient(0, 0, R * 0.05, 0, 0, R * 0.95);
                g.AddColorStop(0, Col.Rgba(255, 210, 95, Css.fixedNum(0.52 * pulse, 3)));
                g.AddColorStop(0.6, Col.Rgba(235, 135, 35, Css.fixedNum(0.32 * pulse, 3)));
                g.AddColorStop(1, "rgba(180, 80, 20, 0.08)");
                ctx.FillStyle = g;
                ctx.FillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);
                ctx.Restore();
            }
            else if (id == "column")
            {
                ctx.Save();
                ctx.FillStyle = "rgba(232, 220, 198, 0.22)";
                ctx.FillRect(-R * 1.2, -R * 1.2, R * 2.4, R * 2.4);
                ctx.StrokeStyle = "rgba(42, 32, 24, 0.45)";
                ctx.LineWidth = 1.2 * s;
                ctx.BeginPath();
                ctx.Arc(0, 0, R * 0.72, 0, Math.PI * 2);
                ctx.Arc(0, 0, R * 0.34, 0, Math.PI * 2);
                ctx.Stroke();
                for (int i = 0; i < 16; i++)
                {
                    double a = (i / 16.0) * Math.PI * 2;
                    ctx.BeginPath();
                    ctx.MoveTo(Math.Cos(a) * R * 0.34, Math.Sin(a) * R * 0.34);
                    ctx.LineTo(Math.Cos(a) * R * 0.95, Math.Sin(a) * R * 0.95);
                    ctx.Stroke();
                }
                ctx.Restore();
            }
        }

        // ================================================================ 2. FLOW MOMENTUM & ROLL RUSH

        /// <summary>0..1 intensity of "Roll Rush" (high-streak golden momentum that powers over ledges).</summary>
        internal double rollRush = 0;

        /// <summary>Effective beat period (speeds up slightly with high streak for an exciting cadence).</summary>
        internal double currentBeat()
        {
            double s = Math.Min(10, this.streak);
            return BEAT / (1 + s * 0.015) * this.orpheusSlow();
        }

        private void drawRollRush(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (this.rollRush < 0.04 && this.nextClimbBoost <= 0.05) return;
            var c = this.ctx;
            double s = this.scale;
            double sx = this.x + this.creep;
            double bx = toScreenX(sx);
            double by = toScreenY(Levels.terrainAt(this.level, sx)) - 34 * s;
            double k = Math.Max(this.rollRush, Math.Min(1, this.nextClimbBoost / 2.0));
            c.Save();
            c.GlobalCompositeOperation = Composite.Lighter;
            // Warm golden aura around the boulder during high rhythm streak or Olympus Fire
            var g = c.CreateRadialGradient(bx, by, 8 * s, bx, by, 54 * s);
            g.AddColorStop(0, Col.Rgba(255, 224, 130, Css.fixedNum(0.26 * k, 3)));
            g.AddColorStop(0.55, Col.Rgba(255, 175, 70, Css.fixedNum(0.12 * k, 3)));
            g.AddColorStop(1, "rgba(255, 150, 50, 0)");
            c.FillStyle = g;
            c.BeginPath();
            c.Arc(bx, by, 54 * s, 0, Math.PI * 2);
            c.Fill();
            c.Restore();
        }

        // ================================================================ 3. TAILWIND, LIGHTNING PARRY & GHOST ABSORPTION

        /// <summary>Every 3rd gust cycle is a warm Tailwind (Lodos) blowing UP the hill instead of down.</summary>
        internal bool isTailwind()
        {
            double cycleIdx = Math.Floor((this.t * 0.23 + 1.3) / (Math.PI * 2));
            return (((int)cycleIdx % 3) + 3) % 3 == 2;
        }

        /// <summary>Seconds since the last storm lightning strike, used for the parry window.</summary>
        private double sinceStrikeT = 99;

        /// <summary>True when storm lightning is about to strike or just flashed, allowing a parry.</summary>
        internal bool canParryLightning()
        {
            if (this.weather != "storm" || this.phase != "playing") return false;
            return this.stormT < 0.85 || this.sinceStrikeT < 0.45;
        }

        private void triggerLightningParry()
        {
            this.stormT = 7 + Js.random() * 7;
            this.sinceStrikeT = 99;
            this.flash = Math.Max(this.flash, 0.55);
            this.breath = 1;
            this.slipT = 0;
            this.saga.s.insight += 1;
            this.addRevolt(0.35);
            this.audio.thunder();
            this.audio.checkpoint();
            this.haptic(new double[] { 20, 30, 50 });
            if (this.cinematic) this.kickWant = Math.Max(this.kickWant, 0.05);
            for (int i = 0; i < 22; i++)
            {
                double a = -Math.PI * Js.random();
                double v = 90 + Js.random() * 210;
                this.particles.Add(new Particle
                {
                    wx = this.x + (Js.random() - 0.5) * 24,
                    wy = Levels.terrainAt(this.level, this.x) + 34,
                    vx = Math.Cos(a) * v,
                    vy = Math.Sin(a) * v,
                    life = 0,
                    maxLife = 0.55 + Js.random() * 0.45,
                    size = (1.5 + Js.random() * 1.8) * this.scale,
                    kind = "spark",
                });
            }
            int li = langIdx(this.lang);
            string msg = new[]
            {
                "⚡ Şimşek Darbesi! (+1 İçgörü)",
                "⚡ Lightning Parry! (+1 Insight)",
                "⚡ 雷霆格挡！(+1 领悟)",
                "⚡ वज्र प्रहार! (+1 अंतर्दृष्टि)",
                "⚡ ¡Parada de Rayo! (+1 Intuición)",
                "⚡ Parade d'Éclair ! (+1 Intuitions)",
                "⚡ صدّ البرق! (+1 بصيرة)",
                "⚡ বজ্র প্রতিহত! (+১ অন্তর্দৃষ্টি)",
                "⚡ Aparada de Raio! (+1 Intuição)",
            }[li];
            this.showGameplayToast(msg, "rgba(185, 225, 255, 0.95)");
        }

        private void drawStormCharge(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (this.weather != "storm" || this.phase != "playing" || this.stormT >= 0.85) return;
            var c = this.ctx;
            double s = this.scale;
            double sx = this.x + this.creep;
            double bx = toScreenX(sx);
            double by = toScreenY(Levels.terrainAt(this.level, sx)) - 34 * s;
            double u = 1 - Math.Max(0, this.stormT / 0.85);
            c.Save();
            c.GlobalCompositeOperation = Composite.Lighter;
            c.StrokeStyle = Col.Rgba(175, 220, 255, Css.fixedNum(0.35 + 0.55 * u, 3));
            c.LineWidth = (1.2 + u * 1.2) * s;
            for (int i = 0; i < 4; i++)
            {
                double a0 = this.t * 8 + i * 1.57;
                double R = 36 * s;
                double x0 = bx + Math.Cos(a0) * R * 0.5, y0 = by + Math.Sin(a0) * R * 0.5;
                double x1 = bx + Math.Cos(a0 + 0.6) * R * 1.15, y1 = by + Math.Sin(a0 + 0.6) * R * 1.15;
                c.BeginPath();
                c.MoveTo(x0, y0);
                c.LineTo((x0 + x1) * 0.5 + Math.Sin(this.t * 29 + i) * 7 * s, (y0 + y1) * 0.5 - Math.Cos(this.t * 23 + i) * 7 * s);
                c.LineTo(x1, y1);
                c.Stroke();
            }
            c.Restore();
        }

        private void checkGhostAbsorb()
        {
            if (this.ghosts == null || this.ghosts.Count == 0) return;
            double fx = this.x - 64;
            for (int i = this.ghosts.Count - 1; i >= 0; i--)
            {
                var g = this.ghosts[i];
                if (g.lv != this.level.id) continue;
                if (Math.Abs(fx - g.wx) <= 72)
                {
                    double gwx = g.wx;
                    this.ghosts.RemoveAt(i);
                    this.breath = Math.Min(1, this.breath + 0.65);
                    this.saga.s.despair = Math.Max(0, this.saga.s.despair - 0.15);
                    this.addRevolt(0.25);
                    this.audio.checkpoint();
                    this.haptic(25);
                    for (int k = 0; k < 16; k++)
                    {
                        double a = -Math.PI * Js.random();
                        double v = 50 + Js.random() * 120;
                        this.particles.Add(new Particle
                        {
                            wx = gwx + (Js.random() - 0.5) * 20,
                            wy = Levels.terrainAt(this.level, gwx) + 25 + Js.random() * 35,
                            vx = Math.Cos(a) * v,
                            vy = Math.Sin(a) * v,
                            life = 0,
                            maxLife = 0.6 + Js.random() * 0.5,
                            size = (1.4 + Js.random() * 1.6) * this.scale,
                            kind = "spark",
                        });
                    }
                    int li = langIdx(this.lang);
                    string msg = new[]
                    {
                        "✦ Yankı Özümsendi! (+Nefes)",
                        "✦ Echo Absorbed! (+Breath)",
                        "✦ 吸收回响！(+气息)",
                        "✦ प्रतिध्वनि अवशोषित! (+श्वास)",
                        "✦ ¡Eco Absorbido! (+Aliento)",
                        "✦ Écho Absorbé ! (+Souffle)",
                        "✦ تم امتصاص الصدى! (+نَفَس)",
                        "✦ প্রতিধ्वনি শোষিত! (+শ্বাস)",
                        "✦ Eco Absorvido! (+Fôlego)",
                    }[li];
                    this.showGameplayToast(msg, "rgba(195, 225, 255, 0.95)");
                    break;
                }
            }
        }

        // ================================================================ 4. DESCENT GAMIFICATION & EMBER ORBS

        internal sealed class DescentOrb
        {
            public double wx;
            public bool taken;
            public double ph;
        }

        internal readonly List<DescentOrb> descentOrbs = new List<DescentOrb>();
        internal int descentOrbsTaken = 0;
        internal double descentSlideT = 0;
        internal double nextClimbBoost = 0;

        private void spawnDescentOrbs()
        {
            this.descentOrbs.Clear();
            this.descentOrbsTaken = 0;
            double len = this.level.length;
            double[] fracs = { 0.80, 0.66, 0.52, 0.37, 0.22 };
            for (int i = 0; i < fracs.Length; i++)
            {
                double jitter = (this.hash(this.cycles * 7.1 + i * 3.3) - 0.5) * 0.05;
                this.descentOrbs.Add(new DescentOrb
                {
                    wx = len * Math.Max(0.14, Math.Min(0.86, fracs[i] + jitter)),
                    taken = false,
                    ph = i * 1.3,
                });
            }
        }

        /// <summary>the run button held on the way down (touch: the throw button turns into it; keys: Tab)</summary>
        internal bool runHeld = false;
        /// <summary>how much faster he goes running than walking</summary>
        internal const double RUN_MULT = 1.85;

        /// <summary>the run button pressed or let go: he runs down while it is held</summary>
        public void setRun(bool on)
        {
            if (on && (!this.active || this.phase != "descent")) return;
            if (on && !this.runHeld)
            {
                this.haptic(14);
                this.dustBurst(this.walkX, 5);
            }
            this.runHeld = on;
        }

        /// <summary>Triggers a brisk downhill slide/sprint during the descent phase.</summary>
        public void triggerDescentSlide()
        {
            if (!this.active || this.phase != "descent") return;
            this.descentSlideT = 1.65;
            this.haptic(18);
            this.audio.step();
            this.dustBurst(this.walkX, 8);
        }

        private void updateDescentGameplay(double dt)
        {
            if (this.descentSlideT > 0)
            {
                this.descentSlideT = Math.Max(0, this.descentSlideT - dt);
                if (Js.random() < 0.35) this.dustBurst(this.walkX, 1);
            }
            else if (this.runHeld && this.walkV > 60 && Js.random() < 0.18) this.dustBurst(this.walkX, 1);
            for (int i = 0; i < this.descentOrbs.Count; i++)
            {
                var o = this.descentOrbs[i];
                if (o.taken) continue;
                double pickDist = this.companionId == "fox" ? 115 : 54;
                if (Math.Abs(this.walkX - o.wx) <= pickDist)
                {
                    o.taken = true;
                    this.descentOrbsTaken++;
                    this.audio.heave("perfect", this.descentOrbsTaken + 2);
                    this.haptic(15);
                    for (int k = 0; k < 10; k++)
                    {
                        double a = -Math.PI * Js.random();
                        double v = 45 + Js.random() * 95;
                        this.particles.Add(new Particle
                        {
                            wx = o.wx,
                            wy = Levels.terrainAt(this.level, o.wx) + 28,
                            vx = Math.Cos(a) * v,
                            vy = Math.Sin(a) * v,
                            life = 0,
                            maxLife = 0.45 + Js.random() * 0.35,
                            size = (1.3 + Js.random() * 1.5) * this.scale,
                            kind = "spark",
                        });
                    }
                    if (this.descentOrbsTaken == 3)
                    {
                        int li = langIdx(this.lang);
                        string msg = new[]
                        {
                            "🔥 Olimpos Ateşi Hazır! (Sonraki tırmanışta 6sn sınırsız nefes)",
                            "🔥 Fire of Olympus Ready! (6s infinite breath on next climb)",
                            "🔥 奥林匹斯之火已就绪！(下次攀登 6 秒无限气息)",
                            "🔥 ओलंपस की अग्नि तैयार! (अगली चढ़ाई में 6से असीम श्वास)",
                            "🔥 ¡Fuego del Olimpo Listo! (6s de aliento infinito al subir)",
                            "🔥 Feu de l'Olympe Prêt ! (6s de souffle infini à la montée)",
                            "🔥 نار أوليمبوس جاهزة! (6 ثوانٍ نَفَس لا ينفد في الصعود القادم)",
                            "🔥 অলিম্পাসের আগুন প্রস্তুত! (পরের আরোহণে ৬ সেকেন্ড অফুরন্ত শ্বাস)",
                            "🔥 Fogo do Olimpo Pronto! (6s de fôlego infinito na subida)",
                        }[li];
                        this.showGameplayToast(msg, "rgba(255, 210, 120, 0.95)");
                    }
                    else if (this.descentOrbsTaken == 5)
                    {
                        this.saga.s.insight += 1;
                        int li = langIdx(this.lang);
                        string msg = new[]
                        {
                            "🔥 5/5 Olimpos Kıvılcımı! (+1 İçgörü & Tam Ateş)",
                            "🔥 5/5 Olympus Sparks! (+1 Insight & Full Fire)",
                            "🔥 5/5 奥林匹斯火花！(+1 领悟与满额圣火)",
                            "🔥 5/5 ओलंपस चिंगारी! (+1 अंतर्दृष्टि और पूर्ण अग्नि)",
                            "🔥 ¡5/5 Chispas del Olimpo! (+1 Intuición y Fuego Total)",
                            "🔥 5/5 Étincelles de l'Olympe ! (+1 Intuition et Plein Feu)",
                            "🔥 5/5 شرارات أوليمبوس! (+1 بصيرة ونار كاملة)",
                            "🔥 ৫/৫ অলিম্পাস স্ফুলিঙ্গ! (+১ অন্তর্দৃষ্টি ও পূর্ণ আগুন)",
                            "🔥 5/5 Centelhas do Olimpo! (+1 Intuição e Fogo Total)",
                        }[li];
                        this.showGameplayToast(msg, "rgba(255, 225, 135, 0.98)");
                    }
                }
            }
        }

        private void drawDescentOrbs(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (this.phase != "descent" && this.phase != "rolling") return;
            if (this.descentOrbs.Count == 0) return;
            var c = this.ctx;
            double s = this.scale;
            c.Save();
            c.GlobalCompositeOperation = Composite.Lighter;
            for (int i = 0; i < this.descentOrbs.Count; i++)
            {
                var o = this.descentOrbs[i];
                if (o.taken) continue;
                double sx = toScreenX(o.wx);
                if (sx < -40 || sx > this.w + 40) continue;
                double bob = Math.Sin(this.t * 3.2 + o.ph) * 5 * s;
                double sy = toScreenY(Levels.terrainAt(this.level, o.wx)) - 26 * s + bob;
                double r = (12 + Math.Sin(this.t * 5 + o.ph) * 2.5) * s;
                var g = c.CreateRadialGradient(sx, sy, 0, sx, sy, r);
                g.AddColorStop(0, "rgba(255, 245, 200, 0.95)");
                g.AddColorStop(0.4, "rgba(255, 195, 85, 0.55)");
                g.AddColorStop(1, "rgba(255, 140, 40, 0)");
                c.FillStyle = g;
                c.BeginPath();
                c.Arc(sx, sy, r, 0, Math.PI * 2);
                c.Fill();
                c.FillStyle = "rgba(255, 252, 230, 0.95)";
                c.BeginPath();
                c.Arc(sx, sy, 3.2 * s, 0, Math.PI * 2);
                c.Fill();
            }
            c.Restore();
        }

        // ================================================================ 5. GODS' TRIALS ("TANRILARIN SINAVI") & DAILY CLIMB

        internal sealed class TrialState
        {
            public string kind; // "hermes" | "athena" | "ares" | "hephaestus" | "hestia"
            public int startKicks;
            public double startX;
            public double targetDist;
            public double startRests;
            public double timeLeft;
            public double maxTime;
            public int streakNeed;
            public int streakCur;
            public string status; // "active" | "won" | "lost"
            public double bannerT;
        }

        internal TrialState activeTrial = null;
        private double trialForCycle = -1;
        internal bool isDailyChallenge = false;
        private string forcedTrialKind = null;

        private string gameplayToastText = null;
        private string gameplayToastCol = "rgba(246, 232, 200, 0.95)";
        private double gameplayToastT = 99;

        private void startTrial(string kind)
        {
            this.activeTrial = new TrialState
            {
                kind = kind,
                startX = this.x,
                targetDist = kind == "hestia" ? 600 : 520,
                startRests = this.runRests,
                startKicks = this.runKicks,
                timeLeft = 22.0,
                maxTime = 22.0,
                streakNeed = 2,
                streakCur = 0,
                status = "active",
                bannerT = 0,
            };
        }

        private void updateTrials(double dt)
        {
            this.gameplayToastT += dt;
            this.pumpToasts();
            this.sinceStrikeT += dt;
            if (this.nextClimbBoost > 0 && this.phase == "playing" && this.active)
                this.nextClimbBoost = Math.Max(0, this.nextClimbBoost - dt);

            // Roll Rush intensity tracks high streak or active Olympus Fire
            bool wantRush = this.phase == "playing" && (this.streak >= this.flowThreshold() + 2 || this.nextClimbBoost > 0);
            this.rollRush += ((wantRush ? 1 : 0) - this.rollRush) * Math.Min(1, dt * 3.5);
            if (this.rollRush < 0.005) this.rollRush = 0;

            if (this.phase != "playing" || !this.active || this.cutscene || this.finaleOn) return;

            // the climb's goal, where the director put it (after the tutorial climb)
            string planned = !string.IsNullOrEmpty(this.forcedTrialKind) ? this.forcedTrialKind : this.trialPlan;
            if (this.trialForCycle != this.cycles && !string.IsNullOrEmpty(planned)
                && this.progress() >= this.trialAt && this.progress() <= this.trialAt + 0.2
                // Hestia's goal waits until he has breath to keep
                && !(planned == "hestia" && this.breath < 0.6 && !this.calm))
            {
                this.trialForCycle = this.cycles;
                this.forcedTrialKind = null;
                this.startTrial(planned);
            }

            var tr = this.activeTrial;
            if (tr == null) return;
            tr.bannerT += dt;

            if (tr.status != "active")
            {
                if (tr.bannerT > 3.2) this.activeTrial = null;
                return;
            }

            if (tr.kind == "hermes")
            {
                if (this.runRests > tr.startRests || this.slipT > 0)
                {
                    tr.status = "lost";
                    tr.bannerT = 0;
                    return;
                }
                if (this.x - tr.startX >= tr.targetDist) this.completeTrial(tr);
            }
            else if (tr.kind == "athena")
            {
                if (tr.streakCur >= tr.streakNeed) this.completeTrial(tr);
            }
            else if (tr.kind == "hephaestus")
            {
                if (this.runKicks - tr.startKicks >= 2) this.completeTrial(tr);
            }
            else if (tr.kind == "hestia")
            {
                if (this.breath < 0.33 && !this.calm)
                {
                    tr.status = "lost";
                    tr.bannerT = 0;
                    return;
                }
                if (this.x - tr.startX >= tr.targetDist) this.completeTrial(tr);
            }
            else if (tr.kind == "ares")
            {
                tr.timeLeft -= dt;
                if (this.x - tr.startX >= tr.targetDist) this.completeTrial(tr);
                else if (tr.timeLeft <= 0)
                {
                    tr.status = "lost";
                    tr.bannerT = 0;
                }
            }
        }

        private void onTrialHeave(string q)
        {
            var tr = this.activeTrial;
            if (tr == null || tr.status != "active") return;
            if (tr.kind == "athena")
            {
                if (q == "perfect")
                {
                    tr.streakCur++;
                    if (tr.streakCur >= tr.streakNeed) this.completeTrial(tr);
                }
                else if (q == "miss")
                {
                    tr.streakCur = 0;
                }
            }
        }

        private void completeTrial(TrialState tr)
        {
            tr.status = "won";
            tr.bannerT = 0;
            this.saga.s.insight += 2;
            this.breath = 1;
            this.slipT = 0;
            this.nextClimbBoost = Math.Max(this.nextClimbBoost, 4.5);
            this.grantRandomFreshQuote();
            this.audio.checkpoint();
            this.haptic(new double[] { 20, 40, 30 });
            for (int i = 0; i < 18; i++)
            {
                double a = -Math.PI * Js.random();
                double v = 60 + Js.random() * 150;
                this.particles.Add(new Particle
                {
                    wx = this.x,
                    wy = Levels.terrainAt(this.level, this.x) + 34,
                    vx = Math.Cos(a) * v,
                    vy = Math.Sin(a) * v,
                    life = 0,
                    maxLife = 0.6 + Js.random() * 0.5,
                    size = (1.4 + Js.random() * 1.6) * this.scale,
                    kind = "spark",
                });
            }
        }

        private void grantRandomFreshQuote()
        {
            int count = Quotes.QUOTE_COUNT;
            var have = new HashSet<int>(this.unlocked);
            var fresh = new List<int>();
            for (int i = 0; i < count; i++)
                if (!have.Contains(i)) fresh.Add(i);
            if (fresh.Count > 0)
            {
                int pick = fresh[(int)Math.Floor(Js.random() * fresh.Count)];
                this.grantQuote(pick);
            }
        }

        private static readonly string[] TRIAL_HERMES_TITLE = { "HERMES'İN BAHSİ", "HERMES'S WAGER", "赫尔墨斯之赌", "हर्मीस की शर्त", "APUESTA DE HERMES", "PARI D'HERMÈS", "رهان هرمس", "হার্মিসের বাজি", "APOSTA DE HERMES" };
        private static readonly string[] TRIAL_HERMES_DESC  = { "Durup dinlenmeden 520 adım tırman", "Climb 520 paces without resting", "不作停歇连续攀登 520 步", "बिना रुके 520 कदम चढ़ें", "Sube 520 pasos sin detenerte", "Grimpez 520 pas sans vous arrêter", "اصعد 520 خطوة دون توقف", "না থেমে ৫২০ কদম আরোহণ করো", "Suba 520 passos sem descansar" };
        private static readonly string[] TRIAL_ATHENA_TITLE = { "ATHENA'NIN ÖLÇÜSÜ", "ATHENA'S MEASURE", "雅典娜之尺", "एथेना का माप", "MEDIDA DE ATENEA", "MESURE D'ATHÉNA", "مقياس أثينا", "অ্যাথেনার পরিমাপ", "MEDIDA DE ATENA" };
        private static readonly string[] TRIAL_ATHENA_DESC  = { "Peş peşe 2 kusursuz fırlatış yap", "Make 2 perfect throws in a row", "连续完成 2 次完美投掷", "लगातार 2 सटीक फेंक करें", "Haz 2 lanzamientos perfectos seguidos", "Réussis 2 lancers parfaits de suite", "نفّذ رميتين مثاليتين متتاليتين", "পরপর ২টি নিখুঁত ছোড়া দাও", "Faça 2 lançamentos perfeitos seguidos" };
        private static readonly string[] TRIAL_ARES_TITLE   = { "ARES'İN HÜCUMU", "ARES'S CHARGE", "阿瑞斯之冲锋", "एरीज़ का धावा", "CARGA DE ARES", "CHARGE D'ARÈS", "هجوم آريس", "এরিসের অভিযান", "CARGA DE ARES" };
        private static readonly string[] TRIAL_ARES_DESC    = { "22 saniye içinde 520 adım yüksel", "Climb 520 paces within 22 seconds", "在 22 秒内攀登 520 步", "22 सेकंड के भीतर 520 कदम चढ़ें", "Sube 520 pasos en 22 segundos", "Grimpez 520 pas en 22 secondes", "اصعد 520 خطوة خلال 22 ثانية", "২২ সেকেন্ডের মধ্যে ৫২০ কদম ওঠো", "Suba 520 passos em 22 segundos" };
        private static readonly string[] TRIAL_WON_STATUS   =
        {
            "✓ Sınav Tamamlandı! (+2 İçgörü & Yeni Yaprak)",
            "✓ Trial Complete! (+2 Insight & New Page)",
            "✓ 试炼完成！(+2 领悟与新书页)",
            "✓ परीक्षा पूर्ण! (+2 अंतर्दृष्टि और नया पृष्ठ)",
            "✓ ¡Prueba Completada! (+2 Intuición y Página Nueva)",
            "✓ Épreuve Réussie ! (+2 Intuition et Nouvelle Page)",
            "✓ اكتمل الاختبار! (+2 بصيرة وصفحة جديدة)",
            "✓ পরীক্ষা সম্পন্ন! (+২ অন্তর্দৃষ্টি ও নতুন পাতা)",
            "✓ Prova Concluída! (+2 Intuição e Nova Página)",
        };
        private static readonly string[] TRIAL_LOST_STATUS  =
        {
            "✕ Sınav Kaçtı", "✕ Trial Failed", "✕ 试炼失败", "✕ परीक्षा विफल", "✕ Prueba Fallida", "✕ Épreuve Échouée", "✕ فشل الاختبار", "✕ পরীক্ষা ব্যর্থ", "✕ Prova Falhou"
        };

        public static (string title, string desc) trialText(string kind, string lang)
        {
            int li = langIdx(lang);
            if (kind == "hermes") return (TRIAL_HERMES_TITLE[li], TRIAL_HERMES_DESC[li]);
            if (kind == "athena") return (TRIAL_ATHENA_TITLE[li], TRIAL_ATHENA_DESC[li]);
            if (kind == "hephaestus") return (TRIAL_HEPH_TITLE[li], TRIAL_HEPH_DESC[li]);
            if (kind == "hestia") return (TRIAL_HESTIA_TITLE[li], TRIAL_HESTIA_DESC[li]);
            return (TRIAL_ARES_TITLE[li], TRIAL_ARES_DESC[li]);
        }

        private void drawTrialBanner()
        {
            var c = this.ctx;
            double w = this.w, s = Math.Max(0.85, this.scale);
            int li = langIdx(this.lang);

            // Gameplay toast (parry, echo absorb, tailwind kick, Olympus fire)
            if (this.gameplayToastText != null && this.gameplayToastT < 2.8)
            {
                double a = this.gameplayToastT < 0.25 ? this.gameplayToastT / 0.25
                    : this.gameplayToastT > 2.2 ? Math.Max(0, (2.8 - this.gameplayToastT) / 0.6) : 1;
                c.Save();
                c.GlobalAlpha = a;
                c.Font = "600 " + Js.round(13 * s) + "px 'Cormorant Garamond', Georgia, serif";
                c.TextAlign = TextAlign.Center;
                c.TextBaseline = TextBaseline.Middle;
                double ty = 86 * s;
                c.FillStyle = "rgba(12, 9, 7, 0.65)";
                c.BeginPath();
                UiKitRoundRect(c, w * 0.5 - 155 * s, ty - 15 * s, 310 * s, 30 * s, 15 * s);
                c.Fill();
                c.StrokeStyle = "rgba(218, 175, 92, 0.45)";
                c.LineWidth = 1;
                c.Stroke();
                c.FillStyle = this.gameplayToastCol;
                c.FillText(this.gameplayToastText, w * 0.5, ty);
                c.Restore();
            }

            var tr = this.activeTrial;
            if (tr == null || this.cutscene || this.finaleOn || this.phase != "playing") return;

            double fade = tr.status == "active"
                ? Math.Min(1, tr.bannerT / 0.35)
                : Math.Max(0, 1 - tr.bannerT / 3.0);
            if (fade <= 0.02) return;

            var (title, desc) = trialText(tr.kind, this.lang);
            double prog = 0;
            if (tr.kind == "hermes" || tr.kind == "ares" || tr.kind == "hestia")
                prog = Math.Max(0, Math.Min(1, (this.x - tr.startX) / tr.targetDist));
            else if (tr.kind == "athena")
                prog = Math.Max(0, Math.Min(1, (double)tr.streakCur / tr.streakNeed));
            else if (tr.kind == "hephaestus")
                prog = Math.Max(0, Math.Min(1, (this.runKicks - tr.startKicks) / 2.0));
            if (tr.status == "won") prog = 1;

            string statusLine = desc;
            if (tr.status == "won")
            {
                statusLine = TRIAL_WON_STATUS[li];
            }
            else if (tr.status == "lost")
            {
                statusLine = TRIAL_LOST_STATUS[li];
            }
            else if (tr.kind == "ares")
            {
                statusLine = desc + " (" + Js.str(Math.Ceiling(tr.timeLeft)) + "s)";
            }

            double bw = 270 * s, bh = 44 * s;
            double bx = (w - bw) * 0.5, by = 22 * s;
            c.Save();
            c.GlobalAlpha = fade * 0.92;
            c.FillStyle = tr.status == "won" ? "rgba(28, 38, 22, 0.78)" : tr.status == "lost" ? "rgba(42, 18, 16, 0.75)" : "rgba(16, 12, 9, 0.72)";
            c.BeginPath();
            UiKitRoundRect(c, bx, by, bw, bh, 8 * s);
            c.Fill();
            c.StrokeStyle = tr.status == "won" ? "rgba(180, 230, 120, 0.65)" : tr.status == "lost" ? "rgba(220, 95, 75, 0.55)" : "rgba(205, 165, 90, 0.45)";
            c.LineWidth = 1;
            c.Stroke();

            c.TextAlign = TextAlign.Center;
            c.TextBaseline = TextBaseline.Middle;
            c.Font = "700 " + Js.round(10 * s) + "px Cinzel, Georgia, serif";
            c.FillStyle = tr.status == "won" ? "rgba(210, 245, 160, 0.95)" : "rgba(218, 180, 105, 0.95)";
            c.FillText(title, w * 0.5, by + 13 * s);

            c.Font = "italic 500 " + Js.round(11.5 * s) + "px 'Cormorant Garamond', Georgia, serif";
            c.FillStyle = "rgba(244, 234, 212, 0.92)";
            c.FillText(statusLine, w * 0.5, by + 27 * s);

            // Progress bar along bottom of banner
            double pw = bw - 24 * s, px = bx + 12 * s, py = by + bh - 6 * s;
            c.FillStyle = "rgba(255, 255, 255, 0.14)";
            c.FillRect(px, py, pw, 2.5 * s);
            c.FillStyle = tr.status == "won" ? "rgba(185, 240, 130, 0.95)" : "rgba(235, 190, 100, 0.95)";
            c.FillRect(px, py, pw * prog, 2.5 * s);
            c.Restore();
        }

        private static void UiKitRoundRect(Ctx c, double x, double y, double w, double h, double r)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) * 0.5));
            c.MoveTo(x + r, y);
            c.LineTo(x + w - r, y);
            c.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
            c.LineTo(x + w, y + h - r);
            c.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
            c.LineTo(x + r, y + h);
            c.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
            c.LineTo(x, y + r);
            c.Arc(x + r, y + r, r, Math.PI, Math.PI * 1.5);
            c.ClosePath();
        }

        /// <summary>Starts today's deterministic Daily Challenge Climb immediately.</summary>
        public void startDailyClimb()
        {
            int lvIdx = Daily.dailyLevelIndex();
            string wx = Daily.dailyWeather();
            string trKind = Daily.dailyTrialKind();

            this.isDailyChallenge = true;
            this.forcedTrialKind = trKind;
            this.trialForCycle = -1;
            this.activeTrial = null;

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
            this.levelIndex = lvIdx;
            this.level = Levels.getLevel(lvIdx);
            this.weather = wx;
            this.wxFade = 1;
            this.maxX = 0;
            this.epiphany = false;
            this.particles.Clear();
            this.runSlips = 0;
            this.runRests = 0;
            this.runMaxStreak = 0;
            this.stormT = 4;
            this.revoltMeter = 0;
            this.revoltFeverT = 0;
            this.rollRush = 0;
            this.nextClimbBoost = 0;
            this.descentSlideT = 0;
            this.descentOrbs.Clear();
            this.descentOrbsTaken = 0;
            this.speaker = null;
            this.cutscene = false;
            this.finaleOn = false;
            this.active = true;
            this.paused = false;
            this.spawnSlopeProps();

            var (tTitle, _) = trialText(trKind, this.lang);
            string lvName = this.level.name;
            if (I18n.LEVEL_NAMES.TryGetValue(this.level.id, out var d) && d.TryGetValue(this.lang, out var nm))
                lvName = nm;
            this.showGameplayToast("☀ " + lvName + " · " + tTitle, "rgba(255, 224, 140, 0.98)");
            this.emit();
        }
    }
}
