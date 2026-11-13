using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// The myths that meet him now and then on the way up, and something to do on the way down.
    ///
    /// <para>On the way up (from the 3rd climb, on a little under half the climbs, never the same
    /// figure twice running, chosen to suit the climb's character) one figure waits by the path,
    /// speaks one line as he comes near, and changes one small thing:</para>
    /// <list type="bullet">
    /// <item>Thanatos hangs his chains across the path: break them with a perfect heave or a throw;</item>
    /// <item>Orpheus plays: while he is near, the beat slows and softens;</item>
    /// <item>Persephone gives a pomegranate: full breath and insight;</item>
    /// <item>Ixion's burning wheel rolls down at him: stop and brace, and it leaps over the stone;</item>
    /// <item>the Danaids and Odysseus only speak (and that is worth an insight).</item>
    /// </list>
    /// <para>On the way down, the climb's character decides the errand, none of them timed and
    /// none of them failed: the embers of Olympus (as before), pages of the book scattered by the
    /// wind, a weary shade to help, or a stone to leave on the cairn at the foot of the hill — a
    /// cairn that grows over all the climbs he makes.</para>
    /// </summary>
    public partial class SisyphusEngine
    {
        internal const int MYTH_FROM = 3;

        // ---------------------------------------------------------------- who meets him

        /// <summary>this climb's figure ("thanatos" | "orpheus" | "persephone" | "ixion" | "danaids" | "odysseus" | null)</summary>
        internal string encKind = null;
        /// <summary>where they stand (world x)</summary>
        internal double encAt = -1;
        private bool encSpoken, encDone;
        private double encSpeakT = 99;
        private double chainBreak = 0;
        private double orpheusK = 0;
        // Ixion's wheel
        private double wheelX = -1, wheelSpin = 0, wheelHop = 0;
        private bool wheelPassed;

        private static string[] encKindsFor(string mood)
        {
            switch (mood)
            {
                case "calm": return new[] { "orpheus", "danaids", "odysseus" };
                case "sacred": return new[] { "persephone", "orpheus", "odysseus" };
                case "storm": return new[] { "thanatos", "ixion" };
                default: return new[] { "thanatos", "ixion", "persephone", "danaids", "odysseus", "orpheus" };
            }
        }

        private static string rawEncKind(double cycles)
        {
            if (cycles < MYTH_FROM || dh(cycles, 101) >= 0.45) return null;
            var ks = encKindsFor(moodFor(cycles));
            return ks[(int)Math.Floor(dh(cycles, 103) * ks.Length) % ks.Length];
        }

        /// <summary>chooses this climb's figure and where it stands (part of planClimb)</summary>
        private void planEncounter(double c, double len)
        {
            this.encKind = null;
            this.encAt = -1;
            this.encSpoken = this.encDone = false;
            this.encSpeakT = 99;
            this.chainBreak = 0;
            this.wheelX = -1;
            this.wheelPassed = false;
            this.wheelHop = 0;
            if (this.isDailyChallenge || this.cycles < MYTH_FROM) return;
            string k = rawEncKind(this.cycles);
            if (k == null) return;
            if (k == rawEncKind(this.cycles - 1))
            {
                var ks = encKindsFor(this.climbMood);
                k = ks[(Array.IndexOf(ks, k) + 1) % ks.Length];
            }
            // somewhere in the middle of the hill, clear of the altar and of the story's own figures
            for (int tries = 0; tries < 24; tries++)
            {
                double f = 0.28 + dh(c, 105 + tries) * 0.4;
                double wx = len * f;
                if (this.shrineAt >= 0 && Math.Abs(wx - this.shrineAt) < 240) continue;
                if (Math.Abs(f - NPC_AT.tan) < 0.06 || Math.Abs(f - NPC_AT.merope) < 0.06 || Math.Abs(f - this.saga.proAt) < 0.06) continue;
                this.encAt = wx;
                break;
            }
            if (this.encAt < 0) return;
            this.encKind = k;
            // nothing else in the way where they stand
            this.slopeProps.RemoveAll(p => Math.Abs(p.wx - this.encAt) < 220);
        }

        // ---------------------------------------------------------------- what they say

        private static readonly Dictionary<string, string[]> ENC_NAME = new Dictionary<string, string[]>
        {
            ["thanatos"] = new[] { "THANATOS", "THANATOS", "塔纳托斯", "थानाटोस", "TÁNATOS", "THANATOS", "ثاناتوس", "থানাটোস", "TÂNATO" },
            ["orpheus"] = new[] { "ORFEUS", "ORPHEUS", "俄耳甫斯", "ऑर्फ़ियस", "ORFEO", "ORPHÉE", "أورفيوس", "অর্ফিয়াস", "ORFEU" },
            ["persephone"] = new[] { "PERSEPHONE", "PERSEPHONE", "珀耳塞福涅", "पर्सेफ़ोनी", "PERSÉFONE", "PERSÉPHONE", "برسيفوني", "পার্সেফোনি", "PERSÉFONE" },
            ["danaids"] = new[] { "DANAİDLER", "THE DANAIDS", "达那伊得斯姐妹", "दानाइड्स", "LAS DANAIDES", "LES DANAÏDES", "بنات داناوس", "দানাইডরা", "AS DANAIDES" },
            ["odysseus"] = new[] { "ODYSSEUS", "ODYSSEUS", "奥德修斯", "ओडिसियस", "ODISEO", "ULYSSE", "أوديسيوس", "ওডিসিয়াস", "ULISSES" },
        };

        private static readonly Dictionary<string, string[]> ENC_LINE = new Dictionary<string, string[]>
        {
            ["thanatos"] = new[]
            {
                "Beni bir kez zincirledin, Sisifos. Şimdi sıra bende.",
                "You chained me once, Sisyphus. Now it is my turn.",
                "西西弗斯，你曾锁住过我。现在轮到我了。",
                "सिसिफ़स, तुमने एक बार मुझे जंजीरों में बाँधा था। अब मेरी बारी है।",
                "Una vez me encadenaste, Sísifo. Ahora me toca a mí.",
                "Tu m'as enchaîné une fois, Sisyphe. À mon tour.",
                "قيّدتني مرةً يا سيزيف. والآن جاء دوري.",
                "সিসিফাস, একবার তুমি আমাকে শিকলে বেঁধেছিলে। এবার আমার পালা।",
                "Uma vez acorrentaste-me, Sísifo. Agora é a minha vez.",
            },
            ["orpheus"] = new[]
            {
                "Dinle... Taş da şarkıyı duyar. Ritmini benimkine uydur.",
                "Listen... even stone hears the song. Fall into my rhythm.",
                "听……连石头也听得见歌声。跟上我的节奏。",
                "सुनो... पत्थर भी गीत सुनता है। मेरी लय में चलो।",
                "Escucha... hasta la piedra oye la canción. Sigue mi ritmo.",
                "Écoute... même la pierre entend le chant. Prends mon rythme.",
                "اسمع... حتى الحجر يسمع الأغنية. اتبع إيقاعي.",
                "শোনো... পাথরও গান শোনে। আমার ছন্দে চলো।",
                "Escuta... até a pedra ouve a canção. Segue o meu ritmo.",
            },
            ["persephone"] = new[]
            {
                "Beni bir kez kandırdın. Yine de al bu narı: gücün olsun.",
                "You tricked me once. Still, take this pomegranate: let it give you strength.",
                "你曾骗过我。但还是收下这颗石榴吧：愿它给你力量。",
                "तुमने एक बार मुझे छला था। फिर भी यह अनार लो: यह तुम्हें शक्ति दे।",
                "Una vez me engañaste. Aun así, toma esta granada: que te dé fuerza.",
                "Tu m'as trompée une fois. Prends tout de même cette grenade : qu'elle te donne des forces.",
                "خدعتني مرةً. ومع ذلك، خذ هذه الرمانة: لتمنحك القوة.",
                "একবার তুমি আমাকে ঠকিয়েছিলে। তবু এই ডালিমটা নাও: শক্তি দেবে।",
                "Uma vez enganaste-me. Ainda assim, toma esta romã: que te dê força.",
            },
            ["danaids"] = new[]
            {
                "Biz de dolmayan testileri taşıyoruz. Yalnız değilsin.",
                "We too carry jars that never fill. You are not alone.",
                "我们也提着永远装不满的水罐。你并不孤单。",
                "हम भी कभी न भरने वाले घड़े ढोती हैं। तुम अकेले नहीं हो।",
                "Nosotras también cargamos cántaros que nunca se llenan. No estás solo.",
                "Nous aussi portons des jarres qui ne se remplissent jamais. Tu n'es pas seul.",
                "نحن أيضًا نحمل جرارًا لا تمتلئ أبدًا. لستَ وحدك.",
                "আমরাও এমন কলসি বই যা কখনো ভরে না। তুমি একা নও।",
                "Também nós carregamos jarros que nunca se enchem. Não estás sozinho.",
            },
            ["odysseus"] = new[]
            {
                "Seni yeraltında görmüştüm. Hâlâ itiyorsun... ve gülümsüyorsun.",
                "I saw you in the underworld. You are still pushing... and smiling.",
                "我曾在冥府见过你。你还在推……而且你在微笑。",
                "मैंने तुम्हें पाताल में देखा था। तुम अब भी धकेल रहे हो... और मुस्कुरा रहे हो।",
                "Te vi en el inframundo. Sigues empujando... y sonríes.",
                "Je t'ai vu aux Enfers. Tu pousses encore... et tu souris.",
                "رأيتك في العالم السفلي. ما زلت تدفع... وتبتسم.",
                "আমি তোমাকে পাতালে দেখেছিলাম। তুমি এখনো ঠেলছ... আর হাসছ।",
                "Vi-te no mundo dos mortos. Ainda empurras... e sorris.",
            },
        };

        private static readonly Dictionary<string, string[]> ENC_TOAST = new Dictionary<string, string[]>
        {
            ["chains"] = new[]
            {
                "⛓ Ölümün zincirleri: kusursuz bir hamleyle ya da fırlatarak kır",
                "⛓ Death's chains: break them with a perfect heave or a throw",
                "⛓ 死神的锁链：用完美发力或抛石将其击断",
                "⛓ मृत्यु की जंजीरें: सटीक प्रहार या फेंककर तोड़ो",
                "⛓ Las cadenas de la Muerte: rómpelas con un empuje perfecto o un lanzamiento",
                "⛓ Les chaînes de la Mort : brise-les d'une poussée parfaite ou d'un lancer",
                "⛓ سلاسل الموت: اكسرها بدفعة مثالية أو برمية",
                "⛓ মৃত্যুর শিকল: নিখুঁত ঠেলা বা ছোড়া দিয়ে ভাঙো",
                "⛓ As correntes da Morte: quebra-as com um impulso perfeito ou um lançamento",
            },
            ["chainsBroken"] = new[]
            {
                "⛓ Zincirler kırıldı! Ölüm bir kez daha yenildi (+1 İçgörü)",
                "⛓ The chains are broken! Death beaten once more (+1 Insight)",
                "⛓ 锁链断了！死神再次被击败（+1 领悟）",
                "⛓ जंजीरें टूट गईं! मृत्यु फिर हारी (+1 अंतर्दृष्टि)",
                "⛓ ¡Cadenas rotas! La Muerte vencida otra vez (+1 Intuición)",
                "⛓ Chaînes brisées ! La Mort vaincue une fois de plus (+1 Intuition)",
                "⛓ انكسرت السلاسل! هُزم الموت مرة أخرى (+1 بصيرة)",
                "⛓ শিকল ভাঙল! মৃত্যু আবার হারল (+১ অন্তর্দৃষ্টি)",
                "⛓ Correntes quebradas! A Morte vencida outra vez (+1 Intuição)",
            },
            ["orpheus"] = new[]
            {
                "♪ Orfeus'un liri: ritim yavaşladı",
                "♪ The lyre of Orpheus: the beat slows",
                "♪ 俄耳甫斯的竖琴：节奏放缓了",
                "♪ ऑर्फ़ियस की वीणा: लय धीमी हुई",
                "♪ La lira de Orfeo: el ritmo se calma",
                "♪ La lyre d'Orphée : le rythme ralentit",
                "♪ قيثارة أورفيوس: تباطأ الإيقاع",
                "♪ অর্ফিয়াসের বীণা: ছন্দ ধীর হলো",
                "♪ A lira de Orfeu: o ritmo abranda",
            },
            ["persephone"] = new[]
            {
                "✦ Persephone'nin narı: nefes doldu (+1 İçgörü)",
                "✦ Persephone's pomegranate: breath restored (+1 Insight)",
                "✦ 珀耳塞福涅的石榴：气息恢复（+1 领悟）",
                "✦ पर्सेफ़ोनी का अनार: श्वास लौटी (+1 अंतर्दृष्टि)",
                "✦ La granada de Perséfone: aliento renovado (+1 Intuición)",
                "✦ La grenade de Perséphone : souffle retrouvé (+1 Intuition)",
                "✦ رمانة برسيفوني: عاد النَّفَس (+1 بصيرة)",
                "✦ পার্সেফোনির ডালিম: শ্বাস ফিরল (+১ অন্তর্দৃষ্টি)",
                "✦ A romã de Perséfone: fôlego recuperado (+1 Intuição)",
            },
            ["ixion"] = new[]
            {
                "🔥 İksion'un çarkı yuvarlanıyor: dur, omuz ver, üstünden geçsin!",
                "🔥 Ixion's wheel is rolling down: stop, brace, let it leap over!",
                "🔥 伊克西翁的火轮滚下来了：停下，顶住，让它越过去！",
                "🔥 इक्सियन का चक्र लुढ़क रहा है: रुको, कंधा टिकाओ, उसे ऊपर से जाने दो!",
                "🔥 ¡La rueda de Ixión baja rodando: detente, afiánzate y deja que salte!",
                "🔥 La roue d'Ixion dévale : arrête-toi, tiens bon, laisse-la sauter !",
                "🔥 عجلة إكسيون تتدحرج: توقف، اثبت، ودعها تقفز فوقك!",
                "🔥 ইক্সিয়নের চাকা গড়িয়ে আসছে: থামো, কাঁধ দাও, ওপর দিয়ে যেতে দাও!",
                "🔥 A roda de Íxion desce a rolar: para, firma-te, deixa-a saltar!",
            },
            ["ixionDodged"] = new[]
            {
                "🔥 Çark üstünden aştı! Sabrın kazandı (+1 İçgörü)",
                "🔥 The wheel leapt over! Patience won (+1 Insight)",
                "🔥 火轮越过去了！耐心获胜（+1 领悟）",
                "🔥 चक्र ऊपर से निकल गया! धैर्य जीता (+1 अंतर्दृष्टि)",
                "🔥 ¡La rueda saltó por encima! Ganó la paciencia (+1 Intuición)",
                "🔥 La roue a sauté ! La patience l'emporte (+1 Intuition)",
                "🔥 قفزت العجلة فوقك! انتصر الصبر (+1 بصيرة)",
                "🔥 চাকা ওপর দিয়ে গেল! ধৈর্যের জয় (+১ অন্তর্দৃষ্টি)",
                "🔥 A roda saltou por cima! A paciência venceu (+1 Intuição)",
            },
        };

        // ---------------------------------------------------------------- the meeting, frame by frame

        private void updateEncounter(double dt)
        {
            this.encSpeakT += dt;
            // Orpheus' music eases in and out with distance
            bool near = this.encKind == "orpheus" && this.phase == "playing" && Math.Abs(this.x - this.encAt) < 650;
            this.orpheusK += ((near ? 1 : 0) - this.orpheusK) * Math.Min(1, dt * 1.5);
            if (this.encKind == null || this.phase != "playing" || !this.active || this.cutscene || this.finaleOn) return;
            int li = langIdx(this.lang);
            double d = this.encAt - this.x;
            if (!this.encSpoken && d < 300 && d > -120)
            {
                this.encSpoken = true;
                this.encSpeakT = 0;
                if (this.encKind == "thanatos") this.showGameplayToast(ENC_TOAST["chains"][li], "rgba(220, 214, 230, 0.98)");
                else if (this.encKind == "orpheus") this.showGameplayToast(ENC_TOAST["orpheus"][li], "rgba(200, 222, 255, 0.98)");
            }
            switch (this.encKind)
            {
                case "thanatos":
                {
                    if (this.encDone) { this.chainBreak = Math.Min(1, this.chainBreak + dt * 2); break; }
                    double cx = this.encAt + 70;
                    if (Math.Abs(this.x - cx) < 40)
                    {
                        bool smash = this.kickT >= 0 || this.rollRush > 0.25 || this.revoltFeverT > 0
                            || this.activeBoon == "atlas" || this.activeBoon == "hades"
                            || (this.heaveFx != null && this.heaveFx.q == "perfect" && this.heaveFx.t < 0.4);
                        if (smash)
                        {
                            this.encDone = true;
                            this.saga.s.insight += 1;
                            this.breath = Math.Min(1, this.breath + 0.3);
                            this.audio.thunder();
                            this.shake = Math.Max(this.shake, 0.6);
                            this.haptic(new double[] { 20, 30, 40 });
                            this.spawnShatterParticles(cx, 18);
                            this.showGameplayToast(ENC_TOAST["chainsBroken"][li], "rgba(230, 226, 240, 0.98)");
                        }
                        else if (this.x > cx - 36 && this.vx > 0)
                        {
                            this.x = cx - 36;
                            this.vx = 0;
                        }
                    }
                    break;
                }
                case "persephone":
                case "danaids":
                case "odysseus":
                {
                    if (this.encDone || this.x < this.encAt - 20) break;
                    this.encDone = true;
                    this.saga.s.insight += 1;
                    if (this.encKind == "persephone")
                    {
                        this.breath = 1;
                        this.audio.checkpoint();
                        this.haptic(new double[] { 15, 30, 15 });
                        this.showGameplayToast(ENC_TOAST["persephone"][li], "rgba(255, 200, 200, 0.98)");
                    }
                    break;
                }
                case "ixion":
                {
                    if (this.wheelPassed) break;
                    if (this.wheelX < 0)
                    {
                        if (this.x > this.encAt - 650)
                        {
                            this.wheelX = this.x + 760;
                            this.showGameplayToast(ENC_TOAST["ixion"][li], "rgba(255, 200, 140, 0.98)");
                        }
                        break;
                    }
                    this.wheelX -= 200 * dt;
                    this.wheelSpin -= dt * 6.5;
                    if (this.wheelHop > 0) this.wheelHop += dt;
                    if (this.wheelHop <= 0 && this.wheelX - this.x < 46)
                    {
                        bool braced = Math.Abs(this.input) <= 0.15 && this.kickT < 0;
                        if (braced)
                        {
                            this.wheelHop = 0.001;
                            this.saga.s.insight += 1;
                            this.audio.checkpoint();
                            this.showGameplayToast(ENC_TOAST["ixionDodged"][li], "rgba(255, 220, 160, 0.98)");
                        }
                        else
                        {
                            this.wheelHop = 0.001;
                            this.vx = Math.Min(this.vx, -150);
                            this.breath = Math.Max(0, this.breath - 0.15);
                            this.shake = Math.Max(this.shake, 0.7);
                            this.audio.impact();
                            this.haptic(40);
                            this.dustBurst(this.x, 12);
                        }
                    }
                    if (this.wheelX < this.x - 900) this.wheelPassed = true;
                    break;
                }
            }
        }

        /// <summary>how much slower the beat runs while Orpheus plays (1 = not at all)</summary>
        internal double orpheusSlow() { return 1 + 0.16 * this.orpheusK; }

        private void drawEncounter(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            if (this.encKind == null || this.encAt < 0) return;
            double s = this.scale;
            var ctx = this.ctx;
            if (this.encKind == "ixion")
            {
                if (this.wheelX < 0 || this.wheelPassed || this.phase != "playing") return;
                double sx = toScreenX(this.wheelX);
                if (sx < -80 || sx > this.w + 80) return;
                double hop = this.wheelHop > 0 ? Math.Max(0, Math.Sin(Math.Min(1, this.wheelHop / 0.7) * Math.PI)) * 110 * s : 0;
                double sy = toScreenY(Levels.terrainAt(this.level, this.wheelX)) - 30 * s - hop;
                Figures.drawIxionWheel(ctx, sx, sy, s * 1.1, this.wheelSpin, this.t);
                return;
            }
            double fx = toScreenX(this.encAt);
            if (fx < -140 || fx > this.w + 140) return;
            double fy = toScreenY(Levels.terrainAt(this.level, this.encAt)) - 5 * s;
            var L = this.npcLight(fx, fy);
            switch (this.encKind)
            {
                case "thanatos":
                {
                    Figures.drawThanatos(ctx, fx, fy + 3 * s, s * 1.3, L);
                    double cx = toScreenX(this.encAt + 70);
                    double cy = toScreenY(Levels.terrainAt(this.level, this.encAt + 70)) - 2 * s;
                    Figures.drawDeathChains(ctx, cx, cy, s * 1.2, this.t, this.chainBreak);
                    break;
                }
                case "orpheus":
                    Figures.drawOrpheus(ctx, fx, fy + 3 * s, s * 1.3, L);
                    // the notes, drifting up off the strings
                    ctx.Save();
                    ctx.FillStyle = "rgba(240, 232, 200, 0.85)";
                    ctx.Font = "600 " + Js.round(14 * s) + "px Georgia, serif";
                    for (int i = 0; i < 3; i++)
                    {
                        double ph = (this.t * 0.35 + i / 3.0) % 1;
                        ctx.GlobalAlpha = Math.Sin(ph * Math.PI) * 0.8;
                        ctx.FillText(i % 2 == 0 ? "♪" : "♫", fx - 18 * s + Math.Sin(ph * 6 + i) * 10 * s, fy - 70 * s - ph * 70 * s);
                    }
                    ctx.Restore();
                    break;
                case "persephone":
                    Figures.drawPersephone(ctx, fx, fy + 3 * s, s * 1.3, L, this.encDone);
                    break;
                case "danaids":
                    Figures.drawDanaids(ctx, fx, fy + 3 * s, s * 1.15, L);
                    break;
                case "odysseus":
                    Figures.drawOdysseus(ctx, fx, fy + 3 * s, s * 1.3, L);
                    break;
            }
            // what they say, above them, for a few seconds
            if (this.encSpoken && this.encSpeakT < 6.5 && ENC_LINE.TryGetValue(this.encKind, out var lines))
            {
                int li = langIdx(this.lang);
                double a = this.encSpeakT < 0.4 ? this.encSpeakT / 0.4 : this.encSpeakT > 5.5 ? Math.Max(0, 6.5 - this.encSpeakT) : 1;
                this.drawSpeech(fx, fy - 128 * s, ENC_NAME[this.encKind][li], lines[li], a);
            }
        }

        private readonly List<string> speechLines = new List<string>();

        /// <summary>a quiet caption over a figure: the name, and the line wrapped to two or three rows</summary>
        private void drawSpeech(double cx, double by, string name, string text, double alpha)
        {
            var c = this.ctx;
            double s = Math.Max(0.85, this.scale);
            double maxW = 250 * s;
            c.Save();
            c.GlobalAlpha = alpha;
            c.Font = "italic 500 " + Js.round(13 * s) + "px 'Cormorant Garamond', Georgia, serif";
            // wrap by words (or by characters, for scripts written without spaces)
            this.speechLines.Clear();
            string[] words = text.Contains(" ") ? text.Split(' ') : null;
            string cur = "";
            if (words != null)
            {
                foreach (var wd in words)
                {
                    string tryS = cur.Length == 0 ? wd : cur + " " + wd;
                    if (c.MeasureText(tryS) > maxW && cur.Length > 0) { this.speechLines.Add(cur); cur = wd; }
                    else cur = tryS;
                }
            }
            else
            {
                foreach (char ch in text)
                {
                    string tryS = cur + ch;
                    if (c.MeasureText(tryS) > maxW && cur.Length > 0) { this.speechLines.Add(cur); cur = ch.ToString(); }
                    else cur = tryS;
                }
            }
            if (cur.Length > 0) this.speechLines.Add(cur);
            double lh = 16 * s;
            double wMax = 0;
            foreach (var l in this.speechLines) wMax = Math.Max(wMax, c.MeasureText(l));
            double bw = Math.Max(wMax, 80 * s) + 24 * s;
            double bh = 22 * s + lh * this.speechLines.Count;
            double bx = Math.Max(8 * s, Math.Min(this.w - bw - 8 * s, cx - bw / 2));
            double top = by - bh;
            c.FillStyle = "rgba(14, 10, 8, 0.62)";
            c.BeginPath();
            UiKitRoundRect(c, bx, top, bw, bh, 8 * s);
            c.Fill();
            c.StrokeStyle = "rgba(218, 175, 92, 0.4)";
            c.LineWidth = 1;
            c.Stroke();
            c.TextAlign = TextAlign.Center;
            c.TextBaseline = TextBaseline.Middle;
            c.Font = "700 " + Js.round(9.5 * s) + "px Cinzel, Georgia, serif";
            c.FillStyle = "rgba(226, 190, 120, 0.95)";
            c.FillText(name, bx + bw / 2, top + 11 * s);
            c.Font = "italic 500 " + Js.round(13 * s) + "px 'Cormorant Garamond', Georgia, serif";
            c.FillStyle = "rgba(246, 238, 220, 0.96)";
            for (int i = 0; i < this.speechLines.Count; i++)
                c.FillText(this.speechLines[i], bx + bw / 2, top + 22 * s + lh * (i + 0.5));
            c.Restore();
        }

        // ---------------------------------------------------------------- the way down

        /// <summary>this descent's errand: "embers" | "pages" | "soul" | "cairn"</summary>
        internal string descentTask = "embers";
        private double taskAt = -1;
        private bool taskDone, taskPrompted;
        private double descentHoldT = 0;

        private sealed class Page
        {
            public double wx, ph, h;
            public bool taken;
        }

        private readonly List<Page> pages = new List<Page>();
        private int pagesTaken = 0;

        private static readonly Dictionary<string, string[]> TASK_TEXT = new Dictionary<string, string[]>
        {
            ["pages"] = new[]
            {
                "📜 Rüzgâr kitabın sayfalarını savurdu: inerken yakala",
                "📜 The wind has scattered pages of the book: catch them on the way down",
                "📜 风吹散了书页：下山时把它们接住",
                "📜 हवा ने पुस्तक के पन्ने बिखेर दिए: उतरते हुए पकड़ो",
                "📜 El viento esparció páginas del libro: atrápalas al bajar",
                "📜 Le vent a dispersé des pages du livre : attrape-les en descendant",
                "📜 بعثرت الريح صفحات الكتاب: التقطها وأنت تنزل",
                "📜 বাতাস বইয়ের পাতা উড়িয়ে দিয়েছে: নামতে নামতে ধরো",
                "📜 O vento espalhou páginas do livro: apanha-as ao descer",
            },
            ["pagesDone"] = new[]
            {
                "📜 3 sayfa toplandı: deftere yeni bir söz eklendi",
                "📜 3 pages gathered: a new saying goes into the book",
                "📜 收集了 3 页：书中多了一句新箴言",
                "📜 3 पन्ने इकट्ठे: पुस्तक में एक नई सूक्ति जुड़ी",
                "📜 3 páginas reunidas: una nueva frase entra en el libro",
                "📜 3 pages rassemblées : une nouvelle pensée entre dans le livre",
                "📜 جُمعت 3 صفحات: أُضيفت حكمة جديدة إلى الكتاب",
                "📜 ৩টি পাতা জোগাড়: বইয়ে নতুন বাণী যুক্ত হলো",
                "📜 3 páginas reunidas: uma nova frase entra no livro",
            },
            ["soul"] = new[]
            {
                "🤝 Yolda yorgun bir ruh var: yanına gelince fırlat tuşuyla yardım et",
                "🤝 A weary shade waits on the path: reach it and press throw to help",
                "🤝 路上有个疲惫的亡魂：走到身边按抛掷键帮帮他",
                "🤝 रास्ते में एक थकी आत्मा है: पास पहुँचकर फेंकने का बटन दबाओ और मदद करो",
                "🤝 Una sombra cansada espera en el camino: al llegar, pulsa lanzar para ayudarla",
                "🤝 Une ombre lasse attend sur le chemin : arrivé près d'elle, appuie sur lancer pour l'aider",
                "🤝 روح متعبة تنتظر في الطريق: حين تصلها اضغط زر الرمي لتساعدها",
                "🤝 পথে এক ক্লান্ত আত্মা: কাছে গিয়ে ছোড়ার বোতাম চেপে সাহায্য করো",
                "🤝 Uma sombra cansada espera no caminho: ao chegar, carrega em lançar para ajudar",
            },
            ["soulDone"] = new[]
            {
                "🤝 Ruhun yükünü hafiflettin: sonraki tırmanışta nefesin güçlü (+1 İçgörü)",
                "🤝 You lightened a shade's load: strong breath on the next climb (+1 Insight)",
                "🤝 你减轻了亡魂的重担：下次攀登气息充沛（+1 领悟）",
                "🤝 तुमने एक आत्मा का बोझ हल्का किया: अगली चढ़ाई में मज़बूत श्वास (+1 अंतर्दृष्टि)",
                "🤝 Aliviaste la carga de una sombra: aliento fuerte en la próxima subida (+1 Intuición)",
                "🤝 Tu as allégé le fardeau d'une ombre : souffle fort à la prochaine montée (+1 Intuition)",
                "🤝 خففت حمل روح: نَفَس قوي في الصعود القادم (+1 بصيرة)",
                "🤝 এক আত্মার বোঝা হালকা করলে: পরের আরোহণে জোরালো শ্বাস (+১ অন্তর্দৃষ্টি)",
                "🤝 Aliviaste o fardo de uma sombra: fôlego forte na próxima subida (+1 Intuição)",
            },
            ["cairn"] = new[]
            {
                "🪨 Etekteki taş yığınına bir taş bırak: yanına gelince fırlat tuşuna bas",
                "🪨 Leave a stone on the cairn at the foot: reach it and press throw",
                "🪨 在山脚的石堆上留下一块石头：走到旁边按抛掷键",
                "🪨 तलहटी के पत्थर-ढेर पर एक पत्थर रखो: पास पहुँचकर फेंकने का बटन दबाओ",
                "🪨 Deja una piedra en el montículo del pie: al llegar, pulsa lanzar",
                "🪨 Laisse une pierre sur le cairn au pied : arrivé près de lui, appuie sur lancer",
                "🪨 اترك حجرًا على الكومة عند السفح: حين تصلها اضغط زر الرمي",
                "🪨 পাদদেশের পাথরের স্তূপে একটি পাথর রাখো: কাছে গিয়ে ছোড়ার বোতাম চাপো",
                "🪨 Deixa uma pedra no montículo ao pé: ao chegar, carrega em lançar",
            },
            ["cairnDone"] = new[]
            {
                "🪨 Taş yığını büyüdü ({n}). Dağ seni hatırlıyor",
                "🪨 The cairn grows ({n}). The hill remembers you",
                "🪨 石堆又高了（{n}）。山记住了你",
                "🪨 पत्थर-ढेर बढ़ा ({n})। पहाड़ तुम्हें याद रखता है",
                "🪨 El montículo crece ({n}). La colina te recuerda",
                "🪨 Le cairn grandit ({n}). La colline se souvient de toi",
                "🪨 كبرت الكومة ({n}). الجبل يتذكرك",
                "🪨 স্তূপ বাড়ল ({n})। পাহাড় তোমাকে মনে রাখে",
                "🪨 O montículo cresce ({n}). A colina lembra-se de ti",
            },
            ["here"] = new[]
            {
                "Koş tuşuna bas", "Press run", "按奔跑键", "दौड़ने का बटन दबाओ", "Pulsa correr", "Appuie sur courir", "اضغط زر الركض", "দৌড়ের বোতাম চাপো", "Carrega em correr",
            },
        };

        /// <summary>at the summit, as the way down opens: this descent's errand</summary>
        private void spawnDescentTask()
        {
            this.pages.Clear();
            this.pagesTaken = 0;
            this.taskDone = this.taskPrompted = false;
            this.taskAt = -1;
            this.descentHoldT = 0;
            string mood = this.climbMood;
            this.descentTask = this.cycles < MYTH_FROM ? "embers"
                : mood == "storm" ? "pages" : mood == "calm" ? "soul" : mood == "sacred" ? "cairn" : "embers";
            double len = this.level.length;
            switch (this.descentTask)
            {
                case "embers":
                    this.spawnDescentOrbs();
                    return;
                case "pages":
                    this.descentOrbs.Clear();
                    this.descentOrbsTaken = 0;
                    for (int i = 0; i < 4; i++)
                        this.pages.Add(new Page { wx = len * (0.78 - i * 0.17 + (dh(this.cycles, 140 + i) - 0.5) * 0.06), ph = i * 1.7, h = 40 + dh(this.cycles, 150 + i) * 60 });
                    break;
                case "soul":
                    this.descentOrbs.Clear();
                    this.taskAt = len * (0.45 + dh(this.cycles, 160) * 0.15);
                    break;
                case "cairn":
                    this.descentOrbs.Clear();
                    this.taskAt = HILL_CAIRN_AT * len;
                    break;
            }
            this.showGameplayToast(TASK_TEXT[this.descentTask][langIdx(this.lang)], "rgba(240, 230, 205, 0.98)");
        }

        private const double HILL_CAIRN_AT = 0.16;

        private void updateDescentTask(double dt)
        {
            this.descentHoldT = Math.Max(0, this.descentHoldT - dt);
            int li = langIdx(this.lang);
            if (this.descentTask == "pages")
            {
                foreach (var p in this.pages)
                {
                    if (p.taken) continue;
                    // they drift down the hill on the wind, slower than he walks
                    p.wx -= 18 * dt;
                    if (Math.Abs(this.walkX - p.wx) < 46)
                    {
                        p.taken = true;
                        this.pagesTaken++;
                        this.audio.heave("perfect", this.pagesTaken + 1);
                        this.haptic(12);
                        if (this.pagesTaken == 3)
                        {
                            this.grantRandomFreshQuote();
                            this.showGameplayToast(TASK_TEXT["pagesDone"][li], "rgba(255, 236, 190, 0.98)");
                        }
                    }
                }
                return;
            }
            if ((this.descentTask == "soul" || this.descentTask == "cairn") && !this.taskDone && !this.taskPrompted
                && Math.Abs(this.walkX - this.taskAt) < 160)
            {
                this.taskPrompted = true;
                this.showGameplayToast(TASK_TEXT["here"][li], "rgba(255, 236, 190, 0.98)");
            }
        }

        /// <summary>the throw on the way down: the errand if he is at it, otherwise nothing (the slide)</summary>
        private bool tryDescentTask()
        {
            if (this.taskDone || this.taskAt < 0) return false;
            if (this.descentTask != "soul" && this.descentTask != "cairn") return false;
            if (Math.Abs(this.walkX - this.taskAt) > 110) return false;
            this.taskDone = true;
            this.descentHoldT = 1.4;
            int li = langIdx(this.lang);
            if (this.descentTask == "soul")
            {
                this.saga.s.insight += 1;
                this.nextClimbBoost = Math.Max(this.nextClimbBoost, 6.0);
                this.audio.checkpoint();
                this.haptic(new double[] { 15, 30, 15 });
                this.showGameplayToast(TASK_TEXT["soulDone"][li], "rgba(255, 230, 190, 0.98)");
            }
            else
            {
                this.saga.s.cairn += 1;
                this.audio.impact();
                this.haptic(25);
                this.dustBurst(this.taskAt, 6);
                this.showGameplayToast(TASK_TEXT["cairnDone"][li].Replace("{n}", Js.str(this.saga.s.cairn)), "rgba(240, 226, 200, 0.98)");
            }
            return true;
        }

        private void drawDescentTask(Func<double, double> toScreenX, Func<double, double> toScreenY)
        {
            double s = this.scale;
            var c = this.ctx;
            double len = this.level.length;
            // the cairn at the foot of the hill: always there once it has a stone, growing
            int stones = (int)Math.Min(16, this.saga.s.cairn + (this.descentTask == "cairn" && this.phase == "descent" && !this.taskDone ? 0 : 0));
            if (stones > 0 || (this.descentTask == "cairn" && this.phase == "descent"))
            {
                double wx = HILL_CAIRN_AT * len;
                double sx = toScreenX(wx);
                if (sx > -60 && sx < this.w + 60)
                {
                    double sy = toScreenY(Levels.terrainAt(this.level, wx));
                    c.Save();
                    double y = sy;
                    for (int i = 0; i < Math.Max(1, stones); i++)
                    {
                        double r = (8 - i * 0.35) * s;
                        double off = (dh(i, 3) - 0.5) * 4 * s;
                        c.FillStyle = i % 2 == 0 ? "rgba(122, 112, 100, 1)" : "rgba(146, 136, 122, 1)";
                        c.BeginPath();
                        c.Ellipse(sx + off, y - r * 0.6, r, r * 0.62, (dh(i, 5) - 0.5) * 0.4, 0, Math.PI * 2);
                        c.Fill();
                        c.FillStyle = "rgba(255, 240, 210, 0.18)";
                        c.BeginPath();
                        c.Ellipse(sx + off - r * 0.3, y - r * 0.85, r * 0.45, r * 0.22, 0, 0, Math.PI * 2);
                        c.Fill();
                        y -= r * 1.1;
                    }
                    c.Restore();
                }
            }
            if (this.phase != "descent") return;
            if (this.descentTask == "pages")
            {
                c.Save();
                foreach (var p in this.pages)
                {
                    if (p.taken) continue;
                    double sx = toScreenX(p.wx);
                    if (sx < -40 || sx > this.w + 40) continue;
                    double sy = toScreenY(Levels.terrainAt(this.level, p.wx)) - (p.h + Math.Sin(this.t * 1.7 + p.ph) * 10) * s;
                    c.Save();
                    c.Translate(sx, sy);
                    c.Rotate(Math.Sin(this.t * 2.3 + p.ph) * 0.5);
                    double flip = Math.Abs(Math.Cos(this.t * 3 + p.ph));
                    c.Scale(0.35 + 0.65 * flip, 1);
                    c.FillStyle = "rgba(246, 236, 210, 0.95)";
                    c.FillRect(-6 * s, -8 * s, 12 * s, 16 * s);
                    c.StrokeStyle = "rgba(120, 96, 66, 0.6)";
                    c.LineWidth = 0.6 * s;
                    for (int k = 0; k < 4; k++)
                    {
                        c.BeginPath();
                        c.MoveTo(-4 * s, (-5 + k * 3.4) * s);
                        c.LineTo(4 * s, (-5 + k * 3.4) * s);
                        c.Stroke();
                    }
                    c.Restore();
                }
                c.Restore();
            }
            else if (this.descentTask == "soul" && this.taskAt >= 0)
            {
                double sx = toScreenX(this.taskAt);
                if (sx < -100 || sx > this.w + 100) return;
                double sy = toScreenY(Levels.terrainAt(this.level, this.taskAt)) - 2 * s;
                var L = this.npcLight(sx, sy);
                // a shade bowed under a jar; lighter once helped
                c.Save();
                c.GlobalAlpha = this.taskDone ? 0.55 : 0.85;
                Figures.drawDanaids(c, sx, sy, s * 1.0, L);
                c.Restore();
                if (!this.taskDone) this.drawTaskMarker(sx, sy - 110 * s);
            }
            else if (this.descentTask == "cairn" && !this.taskDone)
            {
                double sx = toScreenX(this.taskAt);
                double sy = toScreenY(Levels.terrainAt(this.level, this.taskAt));
                if (sx > -60 && sx < this.w + 60) this.drawTaskMarker(sx, sy - 40 * s - Math.Min(16, this.saga.s.cairn) * 8 * s);
            }
        }

        /// <summary>a small golden marker bobbing over the errand</summary>
        private void drawTaskMarker(double x, double y)
        {
            var c = this.ctx;
            double s = this.scale;
            double bob = Math.Sin(this.t * 3) * 4 * s;
            c.Save();
            c.GlobalCompositeOperation = Composite.Lighter;
            var g = c.CreateRadialGradient(x, y + bob, 0, x, y + bob, 14 * s);
            g.AddColorStop(0, "rgba(255, 230, 150, 0.85)");
            g.AddColorStop(1, "rgba(255, 190, 80, 0)");
            c.FillStyle = g;
            c.BeginPath();
            c.Arc(x, y + bob, 14 * s, 0, Math.PI * 2);
            c.Fill();
            c.GlobalCompositeOperation = Composite.SourceOver;
            c.FillStyle = "rgba(255, 236, 180, 0.95)";
            c.BeginPath();
            c.MoveTo(x, y + bob + 7 * s);
            c.LineTo(x - 5 * s, y + bob - 2 * s);
            c.LineTo(x + 5 * s, y + bob - 2 * s);
            c.ClosePath();
            c.Fill();
            c.Restore();
        }
    }
}
