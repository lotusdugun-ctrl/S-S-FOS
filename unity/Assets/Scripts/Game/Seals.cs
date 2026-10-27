// Ported from src/game/seals.ts.
/*
 * Seals: the game's achievements, pressed like wax seals into the Book of
 * Wisdom. Each is earned once and kept. They are worked out from what the game
 * already knows — the summit just reached, the cycle count, the pages found —
 * so nothing new has to be tracked during play beyond the hills climbed.
 *
 * SealId is a string: "first" | "c10" | "c50" | "c100" | "night" | "storm" | "noslip" |
 * "norest" | "rhythm" | "hills" | "chapter" | "p100" | "p1000" | "happy".
 */
using System;
using System.Collections.Generic;

namespace Sisifos.Game
{
    public sealed class SealContext
    {
        /// <summary>completed summits, ever</summary>
        public double summits;
        /// <summary>the climb that just ended, if one did (engine.ts' RunStats, or null)</summary>
        public RunStats run;
        /// <summary>pages found</summary>
        public IList<int> unlocked;
        /// <summary>hills ever summited, by level id</summary>
        public IList<string> hills;
        /// <summary>the story has reached its end</summary>
        public bool ended;
    }

    public static class Seals
    {
        public static readonly string[] SEAL_ORDER =
        {
            "first", "c10", "c50", "c100", "night", "storm", "noslip", "norest", "rhythm", "hills", "chapter", "p100", "p1000", "happy",
        };

        const int HILLS = 4;

        /// <summary>every seal the context satisfies</summary>
        public static List<string> sealsEarned(SealContext c)
        {
            var @out = new List<string>();
            var r = c.run;
            if (c.summits >= 1) @out.Add("first");
            if (c.summits >= 10) @out.Add("c10");
            if (c.summits >= 50) @out.Add("c50");
            if (c.summits >= 100) @out.Add("c100");
            if (r != null && r.night) @out.Add("night");
            if (r != null && r.weather == "storm") @out.Add("storm");
            if (r != null && !r.calm && r.slips == 0) @out.Add("noslip");
            if (r != null && !r.calm && r.rests == 0) @out.Add("norest");
            if (r != null && r.maxStreak >= 8) @out.Add("rhythm");
            if (new HashSet<string>(c.hills).Count >= HILLS) @out.Add("hills");
            var found = new HashSet<int>(c.unlocked);
            const int CC = Quotes.CHAPTER_COUNT;
            var per = new int[CC, 2];
            var chapterOf = Quotes.CHAPTER_OF;
            for (int i = 0; i < chapterOf.Length; i++)
            {
                int ch = chapterOf[i];
                per[ch, 1]++;
                if (found.Contains(i)) per[ch, 0]++;
            }
            bool any = false;
            for (int k = 0; k < CC; k++) if (per[k, 1] > 0 && per[k, 0] == per[k, 1]) { any = true; break; }
            if (any) @out.Add("chapter");
            if (found.Count >= 100) @out.Add("p100");
            if (found.Count >= Quotes.QUOTE_COUNT) @out.Add("p1000");
            if (c.ended) @out.Add("happy");
            return @out;
        }

        public static readonly Dictionary<string, Dictionary<string, NameDesc>> SEAL_TEXT = new Dictionary<string, Dictionary<string, NameDesc>>
        {
            ["first"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "İlk Zirve",
                    desc = "Zirveye ilk kez ulaş.",
                },
                ["en"] = new NameDesc
                {
                    name = "First Summit",
                    desc = "Reach the summit for the first time.",
                },
                ["zh"] = new NameDesc
                {
                    name = "初登山顶",
                    desc = "首次登上山顶。",
                },
                ["hi"] = new NameDesc
                {
                    name = "पहली चोटी",
                    desc = "पहली बार चोटी तक पहुँचो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Primera cima",
                    desc = "Llega a la cima por primera vez.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Premier sommet",
                    desc = "Atteins le sommet pour la première fois.",
                },
                ["ar"] = new NameDesc
                {
                    name = "القمة الأولى",
                    desc = "اصعد إلى القمة لأول مرة.",
                },
                ["bn"] = new NameDesc
                {
                    name = "প্রথম চূড়া",
                    desc = "প্রথমবার চূড়ায় পৌঁছাও।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Primeiro cume",
                    desc = "Chegue ao cume pela primeira vez.",
                },
            },
            ["c10"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "On Döngü",
                    desc = "Zirveye on kez ulaş.",
                },
                ["en"] = new NameDesc
                {
                    name = "Ten Cycles",
                    desc = "Reach the summit ten times.",
                },
                ["zh"] = new NameDesc
                {
                    name = "十次轮回",
                    desc = "登上山顶十次。",
                },
                ["hi"] = new NameDesc
                {
                    name = "दस चक्र",
                    desc = "दस बार चोटी तक पहुँचो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Diez ciclos",
                    desc = "Llega a la cima diez veces.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Dix cycles",
                    desc = "Atteins le sommet dix fois.",
                },
                ["ar"] = new NameDesc
                {
                    name = "عشر دورات",
                    desc = "اصعد إلى القمة عشر مرات.",
                },
                ["bn"] = new NameDesc
                {
                    name = "দশ চক্র",
                    desc = "দশবার চূড়ায় পৌঁছাও।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Dez ciclos",
                    desc = "Chegue ao cume dez vezes.",
                },
            },
            ["c50"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Elli Döngü",
                    desc = "Zirveye elli kez ulaş.",
                },
                ["en"] = new NameDesc
                {
                    name = "Fifty Cycles",
                    desc = "Reach the summit fifty times.",
                },
                ["zh"] = new NameDesc
                {
                    name = "五十次轮回",
                    desc = "登上山顶五十次。",
                },
                ["hi"] = new NameDesc
                {
                    name = "पचास चक्र",
                    desc = "पचास बार चोटी तक पहुँचो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Cincuenta ciclos",
                    desc = "Llega a la cima cincuenta veces.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Cinquante cycles",
                    desc = "Atteins le sommet cinquante fois.",
                },
                ["ar"] = new NameDesc
                {
                    name = "خمسون دورة",
                    desc = "اصعد إلى القمة خمسين مرة.",
                },
                ["bn"] = new NameDesc
                {
                    name = "পঞ্চাশ চক্র",
                    desc = "পঞ্চাশবার চূড়ায় পৌঁছাও।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Cinquenta ciclos",
                    desc = "Chegue ao cume cinquenta vezes.",
                },
            },
            ["c100"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Yüz Döngü",
                    desc = "Zirveye yüz kez ulaş.",
                },
                ["en"] = new NameDesc
                {
                    name = "A Hundred Cycles",
                    desc = "Reach the summit a hundred times.",
                },
                ["zh"] = new NameDesc
                {
                    name = "百次轮回",
                    desc = "登上山顶一百次。",
                },
                ["hi"] = new NameDesc
                {
                    name = "सौ चक्र",
                    desc = "सौ बार चोटी तक पहुँचो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Cien ciclos",
                    desc = "Llega a la cima cien veces.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Cent cycles",
                    desc = "Atteins le sommet cent fois.",
                },
                ["ar"] = new NameDesc
                {
                    name = "مئة دورة",
                    desc = "اصعد إلى القمة مئة مرة.",
                },
                ["bn"] = new NameDesc
                {
                    name = "একশো চক্র",
                    desc = "একশোবার চূড়ায় পৌঁছাও।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Cem ciclos",
                    desc = "Chegue ao cume cem vezes.",
                },
            },
            ["night"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Gece Yürüyüşü",
                    desc = "Zirveye gece ulaş.",
                },
                ["en"] = new NameDesc
                {
                    name = "Night Walk",
                    desc = "Reach the summit at night.",
                },
                ["zh"] = new NameDesc
                {
                    name = "夜行",
                    desc = "在夜间登上山顶。",
                },
                ["hi"] = new NameDesc
                {
                    name = "रात की यात्रा",
                    desc = "रात में चोटी तक पहुँचो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Paseo nocturno",
                    desc = "Llega a la cima de noche.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Marche nocturne",
                    desc = "Atteins le sommet de nuit.",
                },
                ["ar"] = new NameDesc
                {
                    name = "مسير الليل",
                    desc = "اصعد إلى القمة ليلًا.",
                },
                ["bn"] = new NameDesc
                {
                    name = "রাতের পথচলা",
                    desc = "রাতে চূড়ায় পৌঁছাও।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Caminhada noturna",
                    desc = "Chegue ao cume à noite.",
                },
            },
            ["storm"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Fırtınaya Karşı",
                    desc = "Fırtınada zirveye ulaş.",
                },
                ["en"] = new NameDesc
                {
                    name = "Against the Storm",
                    desc = "Reach the summit in a storm.",
                },
                ["zh"] = new NameDesc
                {
                    name = "迎风暴而上",
                    desc = "在暴风雨中登上山顶。",
                },
                ["hi"] = new NameDesc
                {
                    name = "तूफ़ान के विरुद्ध",
                    desc = "तूफ़ान में चोटी तक पहुँचो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Contra la tormenta",
                    desc = "Llega a la cima durante una tormenta.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Contre l'orage",
                    desc = "Atteins le sommet sous l'orage.",
                },
                ["ar"] = new NameDesc
                {
                    name = "في وجه العاصفة",
                    desc = "اصعد إلى القمة أثناء العاصفة.",
                },
                ["bn"] = new NameDesc
                {
                    name = "ঝড়ের বিরুদ্ধে",
                    desc = "ঝড়ের মধ্যে চূড়ায় পৌঁছাও।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Contra a tempestade",
                    desc = "Chegue ao cume durante uma tempestade.",
                },
            },
            ["noslip"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Tükenmeyen Nefes",
                    desc = "Nefesin hiç tükenmeden zirveye ulaş (sakin mod dışında).",
                },
                ["en"] = new NameDesc
                {
                    name = "Unbroken Breath",
                    desc = "Reach the summit without running out of breath (not in calm mode).",
                },
                ["zh"] = new NameDesc
                {
                    name = "气息不绝",
                    desc = "在气息未耗尽的情况下登上山顶（平静模式除外）。",
                },
                ["hi"] = new NameDesc
                {
                    name = "अटूट साँस",
                    desc = "साँस खत्म हुए बिना चोटी तक पहुँचो (शांत मोड को छोड़कर)।",
                },
                ["es"] = new NameDesc
                {
                    name = "Aliento inagotable",
                    desc = "Llega a la cima sin quedarte sin aliento (no en modo tranquilo).",
                },
                ["fr"] = new NameDesc
                {
                    name = "Souffle inépuisable",
                    desc = "Atteins le sommet sans jamais manquer de souffle (hors mode calme).",
                },
                ["ar"] = new NameDesc
                {
                    name = "نَفَس لا ينقطع",
                    desc = "اصعد إلى القمة دون أن تنفد أنفاسك (خارج الوضع الهادئ).",
                },
                ["bn"] = new NameDesc
                {
                    name = "অটুট দম",
                    desc = "দম না ফুরিয়েই চূড়ায় পৌঁছাও (শান্ত মোড ছাড়া)।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Fôlego inesgotável",
                    desc = "Chegue ao cume sem perder o fôlego (fora do modo calmo).",
                },
            },
            ["norest"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Durmadan",
                    desc = "Hiç dinlenmeden zirveye ulaş (sakin mod dışında).",
                },
                ["en"] = new NameDesc
                {
                    name = "Without Pause",
                    desc = "Reach the summit without resting once (not in calm mode).",
                },
                ["zh"] = new NameDesc
                {
                    name = "永不停歇",
                    desc = "不休息一次登上山顶（平静模式除外）。",
                },
                ["hi"] = new NameDesc
                {
                    name = "बिना रुके",
                    desc = "एक बार भी आराम किए बिना चोटी तक पहुँचो (शांत मोड को छोड़कर)।",
                },
                ["es"] = new NameDesc
                {
                    name = "Sin pausa",
                    desc = "Llega a la cima sin descansar ni una vez (no en modo tranquilo).",
                },
                ["fr"] = new NameDesc
                {
                    name = "Sans répit",
                    desc = "Atteins le sommet sans te reposer une seule fois (hors mode calme).",
                },
                ["ar"] = new NameDesc
                {
                    name = "بلا توقف",
                    desc = "اصعد إلى القمة دون أن تستريح ولو مرة (خارج الوضع الهادئ).",
                },
                ["bn"] = new NameDesc
                {
                    name = "বিরতিহীন",
                    desc = "একবারও না জিরিয়ে চূড়ায় পৌঁছাও (শান্ত মোড ছাড়া)।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Sem pausa",
                    desc = "Chegue ao cume sem descansar nenhuma vez (fora do modo calmo).",
                },
            },
            ["rhythm"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Kusursuz Ritim",
                    desc = "Art arda sekiz kusursuz hamle yap.",
                },
                ["en"] = new NameDesc
                {
                    name = "Perfect Rhythm",
                    desc = "Make eight perfect heaves in a row.",
                },
                ["zh"] = new NameDesc
                {
                    name = "完美节奏",
                    desc = "连续完成八次完美发力。",
                },
                ["hi"] = new NameDesc
                {
                    name = "बेदाग़ लय",
                    desc = "लगातार आठ बेदाग़ धक्के लगाओ।",
                },
                ["es"] = new NameDesc
                {
                    name = "Ritmo perfecto",
                    desc = "Haz ocho empujones perfectos seguidos.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Rythme parfait",
                    desc = "Réussis huit poussées parfaites d'affilée.",
                },
                ["ar"] = new NameDesc
                {
                    name = "إيقاع مثالي",
                    desc = "نفّذ ثماني دفعات مثالية متتالية.",
                },
                ["bn"] = new NameDesc
                {
                    name = "নিখুঁত ছন্দ",
                    desc = "পরপর আটটি নিখুঁত ধাক্কা দাও।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Ritmo perfeito",
                    desc = "Faça oito empurrões perfeitos seguidos.",
                },
            },
            ["hills"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Dört Tepe",
                    desc = "Dört tepenin her birinde zirveye ulaş.",
                },
                ["en"] = new NameDesc
                {
                    name = "Four Hills",
                    desc = "Reach the summit of each of the four hills.",
                },
                ["zh"] = new NameDesc
                {
                    name = "四座山丘",
                    desc = "登上四座山丘各自的山顶。",
                },
                ["hi"] = new NameDesc
                {
                    name = "चार पहाड़ियाँ",
                    desc = "चारों पहाड़ियों में से हर एक की चोटी तक पहुँचो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Cuatro colinas",
                    desc = "Llega a la cima de cada una de las cuatro colinas.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Quatre collines",
                    desc = "Atteins le sommet de chacune des quatre collines.",
                },
                ["ar"] = new NameDesc
                {
                    name = "أربعة تلال",
                    desc = "اصعد إلى قمة كل تل من التلال الأربعة.",
                },
                ["bn"] = new NameDesc
                {
                    name = "চার পাহাড়",
                    desc = "চারটি পাহাড়ের প্রতিটির চূড়ায় পৌঁছাও।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Quatro colinas",
                    desc = "Chegue ao cume de cada uma das quatro colinas.",
                },
            },
            ["chapter"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Bir Bölüm",
                    desc = "Bilgelik Defteri'nin bir bölümünü tamamla.",
                },
                ["en"] = new NameDesc
                {
                    name = "A Chapter",
                    desc = "Complete a chapter of the Book of Wisdom.",
                },
                ["zh"] = new NameDesc
                {
                    name = "一个章节",
                    desc = "完成智慧之书的一个章节。",
                },
                ["hi"] = new NameDesc
                {
                    name = "एक अध्याय",
                    desc = "ज्ञान की पुस्तक का एक अध्याय पूरा करो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Un capítulo",
                    desc = "Completa un capítulo del Libro de la sabiduría.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Un chapitre",
                    desc = "Termine un chapitre du Livre de sagesse.",
                },
                ["ar"] = new NameDesc
                {
                    name = "فصل كامل",
                    desc = "أكمل فصلًا من كتاب الحكمة.",
                },
                ["bn"] = new NameDesc
                {
                    name = "একটি অধ্যায়",
                    desc = "প্রজ্ঞার বই-এর একটি অধ্যায় শেষ করো।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Um capítulo",
                    desc = "Complete um capítulo do Livro da sabedoria.",
                },
            },
            ["p100"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Yüz Sayfa",
                    desc = "Defterde yüz söz topla.",
                },
                ["en"] = new NameDesc
                {
                    name = "A Hundred Pages",
                    desc = "Collect a hundred sayings in the book.",
                },
                ["zh"] = new NameDesc
                {
                    name = "百页",
                    desc = "在书中收集一百条箴言。",
                },
                ["hi"] = new NameDesc
                {
                    name = "सौ पन्ने",
                    desc = "पुस्तक में सौ वचन इकट्ठा करो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Cien páginas",
                    desc = "Reúne cien frases en el libro.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Cent pages",
                    desc = "Rassemble cent paroles dans le livre.",
                },
                ["ar"] = new NameDesc
                {
                    name = "مئة صفحة",
                    desc = "اجمع مئة حكمة في الكتاب.",
                },
                ["bn"] = new NameDesc
                {
                    name = "একশো পাতা",
                    desc = "বইয়ে একশোটি বাণী সংগ্রহ করো।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Cem páginas",
                    desc = "Reúna cem frases no livro.",
                },
            },
            ["p1000"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Bin Söz",
                    desc = "Defterdeki bin sözün hepsini topla.",
                },
                ["en"] = new NameDesc
                {
                    name = "A Thousand Sayings",
                    desc = "Collect all thousand sayings in the book.",
                },
                ["zh"] = new NameDesc
                {
                    name = "千言",
                    desc = "收集书中全部一千条箴言。",
                },
                ["hi"] = new NameDesc
                {
                    name = "हज़ार वचन",
                    desc = "पुस्तक के सभी हज़ार वचन इकट्ठा करो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Mil frases",
                    desc = "Reúne las mil frases del libro.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Mille paroles",
                    desc = "Rassemble les mille paroles du livre.",
                },
                ["ar"] = new NameDesc
                {
                    name = "ألف حكمة",
                    desc = "اجمع كل الحِكَم الألف في الكتاب.",
                },
                ["bn"] = new NameDesc
                {
                    name = "হাজার বাণী",
                    desc = "বইয়ের সব হাজার বাণী সংগ্রহ করো।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Mil frases",
                    desc = "Reúna todas as mil frases do livro.",
                },
            },
            ["happy"] = new Dictionary<string, NameDesc>
            {
                ["tr"] = new NameDesc
                {
                    name = "Mutlu Sisifos",
                    desc = "Taşı iterken gülümsemeyi öğren.",
                },
                ["en"] = new NameDesc
                {
                    name = "Sisyphus Happy",
                    desc = "Learn to smile while you push.",
                },
                ["zh"] = new NameDesc
                {
                    name = "幸福的西西弗斯",
                    desc = "学会在推石时微笑。",
                },
                ["hi"] = new NameDesc
                {
                    name = "सुखी सिसिफ़स",
                    desc = "पत्थर धकेलते हुए मुस्कुराना सीखो।",
                },
                ["es"] = new NameDesc
                {
                    name = "Sísifo dichoso",
                    desc = "Aprende a sonreír mientras empujas.",
                },
                ["fr"] = new NameDesc
                {
                    name = "Sisyphe heureux",
                    desc = "Apprends à sourire en poussant.",
                },
                ["ar"] = new NameDesc
                {
                    name = "سيزيف سعيدًا",
                    desc = "تعلّم أن تبتسم وأنت تدفع.",
                },
                ["bn"] = new NameDesc
                {
                    name = "সুখী সিসিফাস",
                    desc = "ঠেলতে ঠেলতে হাসতে শেখো।",
                },
                ["pt"] = new NameDesc
                {
                    name = "Sísifo feliz",
                    desc = "Aprenda a sorrir enquanto empurra.",
                },
            },
        };
    }
}
