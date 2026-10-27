// Ported from src/game/story.ts.
/*
 * Merope. The story the hill tells.
 *
 * Sisyphus did not cheat death for nothing. Dying, he made his wife Merope
 * swear to leave his body unburied; then, in the underworld, he complained to
 * its queen that his wife had shamed him, and was let go back up to punish
 * her. He never went back to her. He lived on, into old age, while Corinth
 * spat at the woman who had not buried her husband. Merope — one of the seven
 * Pleiades, the one whose star is faintest — died alone.
 *
 * Now she is a shade at the edge of his hill. Like every shade she has drunk
 * from Lethe, the river of forgetting, and she does not know him.
 *
 *   Act I   Revolt     He knows her; she does not know him. He rages at the
 *                      gods for taking her memory and tries to give it back.
 *   Act II  The Void   Memory returns in full, and with it the truth. At the
 *                      midpoint she turns her back on him.
 *   Act III The Absurd He climbs without complaint; she forgives. Then she asks
 *                      him for water from the spring of forgetting. He brings
 *                      it. She drinks, and does not know him. He smiles.
 *
 * The story runs over 150 episodes, one each time he walks down past her; the
 * words live in story-text/<lang>.json (Resources/Data), the shape of each
 * episode in story-text/shape.json. After it ends, Prometheus and Tantalus each
 * live a story of their own.
 *
 * Speaker: "merope" | "sis" | "pro" | "tan" | "narr" ("narr" is a stage direction:
 * no speaker, set in italics). ItemId, CompanionId ("pro" | "tan"), BurdenId
 * ("pain" | "thirst") and PassiveId ("fire" | "patience") are strings.
 */
using System;
using System.Collections.Generic;

namespace Sisifos.Game
{
    /// <summary>a name and a line describing it (TS: <c>{ name: string; desc: string }</c>)</summary>
    public sealed class NameDesc
    {
        public string name;
        public string desc;
    }

    public sealed class Episode
    {
        /// <summary>the act this belongs to — and so the stage of his mind while it is next (1|2|3)</summary>
        public int act;
        /// <summary>a scene: the climb stops for it</summary>
        public bool cine;
        /// <summary>who says each line</summary>
        public string[] who;
        /// <summary>he must bring this from the summit (she asked for it last time), or null</summary>
        public string brings;
        /// <summary>she asks for this, for the next time, or null</summary>
        public string asks;
        /// <summary>the last line is a choice: bring it now, or not yet</summary>
        public bool choice;
        /// <summary>summits since the last episode before this one can happen (1 if unset: null)</summary>
        public double? gap;
        /// <summary>while this one is next, she stands with her back to him</summary>
        public bool back;
        /// <summary>the last scene: after it, the ending</summary>
        public bool finale;
    }

    public sealed class Meeting
    {
        public string[] who;
        /// <summary>this meeting offers a burden to carry (BurdenId), or null</summary>
        public string offer;
        /// <summary>only once he has carried their burden: they leave a wedge</summary>
        public bool gift;
        /// <summary>only once Merope's story has reached this episode (null: unset)</summary>
        public double? minEp;
    }

    /// <summary>an entry of COMPANIONS</summary>
    public sealed class Companion
    {
        public string burden;
        public string passive;
        public Meeting[] meetings;
        public Meeting[] post;
    }

    public sealed class CompanionText
    {
        public string[][] meetings;
        /// <summary>after the story: their own, in order</summary>
        public string[][] post;
        /// <summary>a line in passing when there is nothing new: Acts I, II, III, after</summary>
        public string[][] idle;
        public string declined;
        public string accepted;
    }

    /// <summary>the words of the story, in one language (story-text/&lt;lang&gt;.json)</summary>
    public sealed class StoryLines
    {
        /// <summary>what each thing he brings down is</summary>
        public Dictionary<string, string> items;
        /// <summary>the words of each episode, line for line with EPISODES[].who</summary>
        public string[][] episodes;
        /// <summary>a line from her when there is nothing new to say, by act</summary>
        public string[][] fillers;
        /// <summary>what she says with her back to him</summary>
        public string[] turned;
        /// <summary>her answers to "not yet", in turn</summary>
        public string[] notYetReplies;
        public Dictionary<string, CompanionText> companions;
    }

    public class StoryUi
    {
        public Dictionary<string, string> names;
        /// <summary>"From the summit:"</summary>
        public string took;
        /// <summary>the answers to the choice in the request scene</summary>
        public string bring;
        public string notYet;
        public Dictionary<string, NameDesc> burdens;
        public Dictionary<string, NameDesc> passives;
        public string accept;
        public string decline;
        public string wedgeHeld;
        /// <summary>"tap to continue"</summary>
        public string next;
    }

    /// <summary>StoryUi &amp; StoryLines</summary>
    public sealed class LangStory : StoryUi
    {
        public Dictionary<string, string> items;
        public string[][] episodes;
        public string[][] fillers;
        public string[] turned;
        public string[] notYetReplies;
        public Dictionary<string, CompanionText> companions;
    }

    public static class Story
    {
        /// <summary>what he can bring down from the summit for her</summary>
        public static readonly string[] ITEM_IDS =
        {
            "stars", "wind", "snow", "thyme", "light", "poppy", "echo", "pebble", "feather", "sea", "dew", "spring", "lethe",
        };

        static Episode[] _episodes;
        static Dictionary<string, Companion> _companions;

        public static Episode[] EPISODES
        {
            get
            {
                if (_episodes == null) LoadShape();
                return _episodes;
            }
        }

        /*
         * The other damned. Prometheus, chained to a cliff beside the path for
         * giving men fire; Zeus's eagle comes every day to tear at him. Tantalus, in
         * water he can never drink, under fruit he can never reach.
         */
        public static Dictionary<string, Companion> COMPANIONS
        {
            get
            {
                if (_companions == null) LoadShape();
                return _companions;
            }
        }

        static void LoadShape()
        {
            var shape = Json.Obj(Json.Parse(Data.Text("story-text/shape")));
            var eps = Json.Arr(shape["episodes"]);
            var e = new Episode[eps.Count];
            for (int i = 0; i < e.Length; i++)
            {
                var o = Json.Obj(eps[i]);
                e[i] = new Episode
                {
                    act = (int)Json.Num(Json.Get(o, "act")),
                    cine = Json.Bool(Json.Get(o, "cine")),
                    who = Strs(Json.Get(o, "who")),
                    brings = Json.Str(Json.Get(o, "brings")),
                    asks = Json.Str(Json.Get(o, "asks")),
                    choice = Json.Bool(Json.Get(o, "choice")),
                    gap = Json.Get(o, "gap") is double g ? g : (double?)null,
                    back = Json.Bool(Json.Get(o, "back")),
                    finale = Json.Bool(Json.Get(o, "finale")),
                };
            }
            Companion Comp(string id, string burden, string passive)
            {
                var c = Json.Obj(shape[id]);
                return new Companion
                {
                    burden = burden,
                    passive = passive,
                    meetings = Meetings(c["meetings"]),
                    post = Meetings(c["post"]),
                };
            }
            var comps = new Dictionary<string, Companion>
            {
                ["pro"] = Comp("pro", "pain", "fire"),
                ["tan"] = Comp("tan", "thirst", "patience"),
            };
            _companions = comps;
            _episodes = e;
        }

        static Meeting[] Meetings(object v)
        {
            var a = Json.Arr(v);
            var r = new Meeting[a.Count];
            for (int i = 0; i < r.Length; i++)
            {
                var o = Json.Obj(a[i]);
                r[i] = new Meeting
                {
                    who = Strs(Json.Get(o, "who")),
                    offer = Json.Str(Json.Get(o, "offer")),
                    gift = Json.Bool(Json.Get(o, "gift")),
                    minEp = Json.Get(o, "minEp") is double m ? m : (double?)null,
                };
            }
            return r;
        }

        internal static string[] Strs(object v)
        {
            var a = Json.Arr(v);
            if (a == null) return null;
            var r = new string[a.Count];
            for (int i = 0; i < r.Length; i++) r[i] = a[i] as string;
            return r;
        }

        static string[][] Strss(object v)
        {
            var a = Json.Arr(v);
            if (a == null) return null;
            var r = new string[a.Count][];
            for (int i = 0; i < r.Length; i++) r[i] = Strs(a[i]);
            return r;
        }

        static StoryLines ReadLines(string text)
        {
            var o = Json.Obj(Json.Parse(text));
            var items = new Dictionary<string, string>();
            foreach (var kv in Json.Obj(o["items"])) items[kv.Key] = kv.Value as string;
            var comps = new Dictionary<string, CompanionText>();
            foreach (var kv in Json.Obj(o["companions"]))
            {
                var c = Json.Obj(kv.Value);
                comps[kv.Key] = new CompanionText
                {
                    meetings = Strss(Json.Get(c, "meetings")),
                    post = Strss(Json.Get(c, "post")),
                    idle = Strss(Json.Get(c, "idle")),
                    declined = Json.Str(Json.Get(c, "declined")),
                    accepted = Json.Str(Json.Get(c, "accepted")),
                };
            }
            return new StoryLines
            {
                items = items,
                episodes = Strss(Json.Get(o, "episodes")),
                fillers = Strss(Json.Get(o, "fillers")),
                turned = Strs(Json.Get(o, "turned")),
                notYetReplies = Strs(Json.Get(o, "notYetReplies")),
                companions = comps,
            };
        }

        static readonly Dictionary<string, StoryUi> UI = new Dictionary<string, StoryUi>
        {
            ["tr"] = new StoryUi
            {
                names = new Dictionary<string, string>
                {
                    ["merope"] = "Merope",
                    ["sis"] = "Sisifos",
                    ["pro"] = "Prometheus",
                    ["tan"] = "Tantalos",
                    ["narr"] = "",
                },
                took = "Zirveden:",
                bring = "Suyu getireceğim",
                notYet = "Henüz değil",
                burdens = new Dictionary<string, NameDesc>
                {
                    ["pain"] = new NameDesc
                    {
                        name = "Prometheus'un acısı",
                        desc = "Bu tırmanışta taş daha ağır.",
                    },
                    ["thirst"] = new NameDesc
                    {
                        name = "Tantalos'un susuzluğu",
                        desc = "Bu tırmanışta nefesin daha çabuk tükenir.",
                    },
                },
                passives = new Dictionary<string, NameDesc>
                {
                    ["fire"] = new NameDesc
                    {
                        name = "Ateş",
                        desc = "Nefesin daha çabuk geri gelir. Kalıcı.",
                    },
                    ["patience"] = new NameDesc
                    {
                        name = "Sabır",
                        desc = "Kayınca daha az geri gidersin. Kalıcı.",
                    },
                },
                accept = "Taşı",
                decline = "Reddet",
                wedgeHeld = "Takoz tuttu.",
                next = "devam",
            },
            ["en"] = new StoryUi
            {
                names = new Dictionary<string, string>
                {
                    ["merope"] = "Merope",
                    ["sis"] = "Sisyphus",
                    ["pro"] = "Prometheus",
                    ["tan"] = "Tantalus",
                    ["narr"] = "",
                },
                took = "From the summit:",
                bring = "I'll bring the water",
                notYet = "Not yet",
                burdens = new Dictionary<string, NameDesc>
                {
                    ["pain"] = new NameDesc
                    {
                        name = "Prometheus's pain",
                        desc = "The stone is heavier this climb.",
                    },
                    ["thirst"] = new NameDesc
                    {
                        name = "Tantalus's thirst",
                        desc = "Your breath runs out faster this climb.",
                    },
                },
                passives = new Dictionary<string, NameDesc>
                {
                    ["fire"] = new NameDesc
                    {
                        name = "Fire",
                        desc = "Your breath comes back faster. For good.",
                    },
                    ["patience"] = new NameDesc
                    {
                        name = "Patience",
                        desc = "A slip gives back less ground. For good.",
                    },
                },
                accept = "Carry it",
                decline = "Refuse",
                wedgeHeld = "The wedge held.",
                next = "continue",
            },
            ["zh"] = new StoryUi
            {
                names = new Dictionary<string, string>
                {
                    ["merope"] = "墨洛珀",
                    ["sis"] = "西西弗斯",
                    ["pro"] = "普罗米修斯",
                    ["tan"] = "坦塔罗斯",
                    ["narr"] = "",
                },
                took = "从山顶带来：",
                bring = "我去取水",
                notYet = "还不是时候",
                burdens = new Dictionary<string, NameDesc>
                {
                    ["pain"] = new NameDesc
                    {
                        name = "普罗米修斯的痛",
                        desc = "这次攀登，石头更重。",
                    },
                    ["thirst"] = new NameDesc
                    {
                        name = "坦塔罗斯的渴",
                        desc = "这次攀登，你的气息耗尽得更快。",
                    },
                },
                passives = new Dictionary<string, NameDesc>
                {
                    ["fire"] = new NameDesc
                    {
                        name = "火",
                        desc = "你的气息回来得更快。永久。",
                    },
                    ["patience"] = new NameDesc
                    {
                        name = "耐心",
                        desc = "滑落时退得更少。永久。",
                    },
                },
                accept = "背负",
                decline = "拒绝",
                wedgeHeld = "石楔挡住了。",
                next = "继续",
            },
            ["hi"] = new StoryUi
            {
                names = new Dictionary<string, string>
                {
                    ["merope"] = "मेरोपी",
                    ["sis"] = "सिसिफ़स",
                    ["pro"] = "प्रोमेथियस",
                    ["tan"] = "टैंटलस",
                    ["narr"] = "",
                },
                took = "चोटी से:",
                bring = "मैं पानी लाऊँगा",
                notYet = "अभी नहीं",
                burdens = new Dictionary<string, NameDesc>
                {
                    ["pain"] = new NameDesc
                    {
                        name = "प्रोमेथियस का दर्द",
                        desc = "इस चढ़ाई में पत्थर ज़्यादा भारी है।",
                    },
                    ["thirst"] = new NameDesc
                    {
                        name = "टैंटलस की प्यास",
                        desc = "इस चढ़ाई में तुम्हारी साँस जल्दी उखड़ती है।",
                    },
                },
                passives = new Dictionary<string, NameDesc>
                {
                    ["fire"] = new NameDesc
                    {
                        name = "आग",
                        desc = "तुम्हारी साँस जल्दी लौटती है। हमेशा के लिए।",
                    },
                    ["patience"] = new NameDesc
                    {
                        name = "धैर्य",
                        desc = "फिसलने पर तुम कम पीछे जाते हो। हमेशा के लिए।",
                    },
                },
                accept = "ढो लें",
                decline = "मना करें",
                wedgeHeld = "टेक ने थाम लिया।",
                next = "जारी रखें",
            },
            ["es"] = new StoryUi
            {
                names = new Dictionary<string, string>
                {
                    ["merope"] = "Mérope",
                    ["sis"] = "Sísifo",
                    ["pro"] = "Prometeo",
                    ["tan"] = "Tántalo",
                    ["narr"] = "",
                },
                took = "Desde la cima:",
                bring = "Traeré el agua",
                notYet = "Todavía no",
                burdens = new Dictionary<string, NameDesc>
                {
                    ["pain"] = new NameDesc
                    {
                        name = "El dolor de Prometeo",
                        desc = "La piedra pesa más en esta subida.",
                    },
                    ["thirst"] = new NameDesc
                    {
                        name = "La sed de Tántalo",
                        desc = "En esta subida te quedas sin aliento antes.",
                    },
                },
                passives = new Dictionary<string, NameDesc>
                {
                    ["fire"] = new NameDesc
                    {
                        name = "Fuego",
                        desc = "Recuperas el aliento más deprisa. Para siempre.",
                    },
                    ["patience"] = new NameDesc
                    {
                        name = "Paciencia",
                        desc = "Un resbalón te hace perder menos terreno. Para siempre.",
                    },
                },
                accept = "Cargarlo",
                decline = "Rechazar",
                wedgeHeld = "La cuña aguantó.",
                next = "continuar",
            },
            ["fr"] = new StoryUi
            {
                names = new Dictionary<string, string>
                {
                    ["merope"] = "Mérope",
                    ["sis"] = "Sisyphe",
                    ["pro"] = "Prométhée",
                    ["tan"] = "Tantale",
                    ["narr"] = "",
                },
                took = "Du sommet :",
                bring = "J'apporterai l'eau",
                notYet = "Pas encore",
                burdens = new Dictionary<string, NameDesc>
                {
                    ["pain"] = new NameDesc
                    {
                        name = "La douleur de Prométhée",
                        desc = "Le rocher est plus lourd pour cette montée.",
                    },
                    ["thirst"] = new NameDesc
                    {
                        name = "La soif de Tantale",
                        desc = "Ton souffle s'épuise plus vite pour cette montée.",
                    },
                },
                passives = new Dictionary<string, NameDesc>
                {
                    ["fire"] = new NameDesc
                    {
                        name = "Le feu",
                        desc = "Ton souffle revient plus vite. Pour toujours.",
                    },
                    ["patience"] = new NameDesc
                    {
                        name = "La patience",
                        desc = "Une glissade te fait perdre moins de terrain. Pour toujours.",
                    },
                },
                accept = "Porter",
                decline = "Refuser",
                wedgeHeld = "La cale a tenu.",
                next = "continuer",
            },
            ["ar"] = new StoryUi
            {
                names = new Dictionary<string, string>
                {
                    ["merope"] = "ميروبي",
                    ["sis"] = "سيزيف",
                    ["pro"] = "بروميثيوس",
                    ["tan"] = "تانتالوس",
                    ["narr"] = "",
                },
                took = "من القمة:",
                bring = "سأحضر الماء",
                notYet = "ليس بعد",
                burdens = new Dictionary<string, NameDesc>
                {
                    ["pain"] = new NameDesc
                    {
                        name = "ألم بروميثيوس",
                        desc = "الصخرة أثقل في هذا الصعود.",
                    },
                    ["thirst"] = new NameDesc
                    {
                        name = "عطش تانتالوس",
                        desc = "ينفد نفَسك أسرع في هذا الصعود.",
                    },
                },
                passives = new Dictionary<string, NameDesc>
                {
                    ["fire"] = new NameDesc
                    {
                        name = "النار",
                        desc = "يعود نفَسك أسرع. إلى الأبد.",
                    },
                    ["patience"] = new NameDesc
                    {
                        name = "الصبر",
                        desc = "تخسر أرضًا أقلّ حين تنزلق. إلى الأبد.",
                    },
                },
                accept = "احمله",
                decline = "ارفض",
                wedgeHeld = "صمد الإسفين.",
                next = "متابعة",
            },
            ["bn"] = new StoryUi
            {
                names = new Dictionary<string, string>
                {
                    ["merope"] = "মেরোপি",
                    ["sis"] = "সিসিফাস",
                    ["pro"] = "প্রমিথিউস",
                    ["tan"] = "ট্যান্টালাস",
                    ["narr"] = "",
                },
                took = "চূড়া থেকে:",
                bring = "আমি জল আনব",
                notYet = "এখনো না",
                burdens = new Dictionary<string, NameDesc>
                {
                    ["pain"] = new NameDesc
                    {
                        name = "প্রমিথিউসের যন্ত্রণা",
                        desc = "এই চড়াইয়ে পাথর বেশি ভারী।",
                    },
                    ["thirst"] = new NameDesc
                    {
                        name = "ট্যান্টালাসের তৃষ্ণা",
                        desc = "এই চড়াইয়ে তোমার দম তাড়াতাড়ি ফুরোয়।",
                    },
                },
                passives = new Dictionary<string, NameDesc>
                {
                    ["fire"] = new NameDesc
                    {
                        name = "আগুন",
                        desc = "তোমার দম তাড়াতাড়ি ফেরে। চিরকালের জন্য।",
                    },
                    ["patience"] = new NameDesc
                    {
                        name = "ধৈর্য",
                        desc = "পিছলে গেলে কম জমি হারাও। চিরকালের জন্য।",
                    },
                },
                accept = "বয়ে নিন",
                decline = "ফিরিয়ে দিন",
                wedgeHeld = "খিলটা আটকে রাখল।",
                next = "চালিয়ে যান",
            },
            ["pt"] = new StoryUi
            {
                names = new Dictionary<string, string>
                {
                    ["merope"] = "Mérope",
                    ["sis"] = "Sísifo",
                    ["pro"] = "Prometeu",
                    ["tan"] = "Tântalo",
                    ["narr"] = "",
                },
                took = "Do topo:",
                bring = "Vou trazer a água",
                notYet = "Ainda não",
                burdens = new Dictionary<string, NameDesc>
                {
                    ["pain"] = new NameDesc
                    {
                        name = "A dor de Prometeu",
                        desc = "A pedra fica mais pesada nesta subida.",
                    },
                    ["thirst"] = new NameDesc
                    {
                        name = "A sede de Tântalo",
                        desc = "Seu fôlego acaba mais depressa nesta subida.",
                    },
                },
                passives = new Dictionary<string, NameDesc>
                {
                    ["fire"] = new NameDesc
                    {
                        name = "Fogo",
                        desc = "Seu fôlego volta mais depressa. Para sempre.",
                    },
                    ["patience"] = new NameDesc
                    {
                        name = "Paciência",
                        desc = "Um escorregão custa menos terreno. Para sempre.",
                    },
                },
                accept = "Carregar",
                decline = "Recusar",
                wedgeHeld = "O calço segurou.",
                next = "continuar",
            },
        };

        static readonly string[] LANGS = { "tr", "en", "zh", "hi", "es", "fr", "ar", "bn", "pt" };

        static StoryLines _en;
        static StoryLines EN
        {
            get
            {
                if (_en == null) _en = ReadLines(Data.Text("story-text/en"));
                return _en;
            }
        }

        static readonly Dictionary<string, StoryLines> LINES = new Dictionary<string, StoryLines>();
        static readonly Dictionary<string, LangStory> cache = new Dictionary<string, LangStory>();

        /// <summary>
        /// fetch a language's words; until they arrive its story is told in English.
        /// PORT: the web imported story-text/&lt;lang&gt;.json lazily (a Promise); here the file is
        /// read through Data.Text at once, so the story is in the language as soon as this returns.
        /// A missing or unreadable file leaves the language in English, as a failed import did.
        /// </summary>
        public static void loadStory(string lang)
        {
            lock (cache)
            {
                if (LINES.ContainsKey(lang)) return;
            }
            if (Array.IndexOf(LANGS, lang) < 0) return;
            StoryLines lines;
            try
            {
                lines = lang == "en" ? EN : ReadLines(Data.Text("story-text/" + lang));
            }
            catch (Exception)
            {
                return;
            }
            lock (cache)
            {
                LINES[lang] = lines;
                cache.Remove(lang);
            }
        }

        public static LangStory storyFor(string lang)
        {
            lock (cache)
            {
                if (cache.TryGetValue(lang, out var s)) return s;
                bool have = LINES.TryGetValue(lang, out var lines);
                if (lang == "en" && !have) { lines = EN; LINES["en"] = lines; have = true; }
                if (!have) lines = EN;
                var ui = UI[lang];
                s = new LangStory
                {
                    names = ui.names,
                    took = ui.took,
                    bring = ui.bring,
                    notYet = ui.notYet,
                    burdens = ui.burdens,
                    passives = ui.passives,
                    accept = ui.accept,
                    decline = ui.decline,
                    wedgeHeld = ui.wedgeHeld,
                    next = ui.next,
                    items = lines.items,
                    episodes = lines.episodes,
                    fillers = lines.fillers,
                    turned = lines.turned,
                    notYetReplies = lines.notYetReplies,
                    companions = lines.companions,
                };
                if (have) cache[lang] = s;
                return s;
            }
        }
    }
}
