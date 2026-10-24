/**
 * Merope. The story the hill tells.
 *
 * Sisyphus did not cheat death for nothing. Dying, he made his wife Merope
 * swear to leave his body unburied; then, in the underworld, he complained to
 * its queen that his wife had shamed him, and was let go back up to punish
 * her. He never went back to her. He lived on, into old age, while Corinth
 * spat at the woman who had not buried her husband. Merope — one of the seven
 * Pleiades, the one whose star is faintest, dimmed, the story goes, by the
 * shame of having married a mortal — died alone.
 *
 * Now she is a shade at the edge of his hill. Like every shade she has drunk
 * from Lethe, the river of forgetting, and she does not know him.
 *
 *   Act I   Revolt     He knows her; she does not know him. He rages at the
 *                      gods for taking her memory and tries to give it back, a
 *                      piece at a time. It comes back — and the first thing
 *                      that returns is what he did to her.
 *   Act II  The Void   Memory returns in full, and with it the truth: it was
 *                      not the gods who made his stone. At the midpoint she
 *                      turns her back on him. There is no one left to blame.
 *   Act III The Absurd He climbs without complaint; she watches, and forgives
 *                      — herself first. Then she asks him for the one thing
 *                      that will cost him her: water from the spring of
 *                      forgetting at the top of the hill. He brings it. She
 *                      drinks, and asks him the question she asked the first
 *                      time they met. He walks down to his stone alone, and
 *                      smiles.
 *
 * The big moments are scenes: the climb stops, the frame narrows, and the
 * player reads at their own pace. The small ones are spoken on the way down,
 * while the hands are free.
 *
 * The story runs over 150 episodes, one each time he walks down past her;
 * the words live in story-text/<lang>.json, loaded for the player's language.
 * After it ends, Prometheus and Tantalus each live a story of their own.
 *
 * Prometheus and Tantalus share the hill. They are met on the way up, and
 * each meeting with the same one goes further — they speak of their own
 * punishment, then of his, then ask him to carry their burden to the top
 * (a harder climb, and a gift that stays), and later measure their fate
 * against his.
 */

/** "narr" is a stage direction: no speaker, set in italics */
import type { LangCode } from "./quotes";
import SHAPE from "./story-text/shape.json";
import EN from "./story-text/en.json";

/** "narr" is a stage direction: no speaker, set in italics */
export type Speaker = "merope" | "sis" | "pro" | "tan" | "narr";

/** what he can bring down from the summit for her */
export const ITEM_IDS = [
  "stars",
  "wind",
  "snow",
  "thyme",
  "light",
  "poppy",
  "echo",
  "pebble",
  "feather",
  "sea",
  "dew",
  "spring",
  "lethe",
] as const;
export type ItemId = (typeof ITEM_IDS)[number];

export type Episode = {
  /** the act this belongs to — and so the stage of his mind while it is next */
  act: 1 | 2 | 3;
  /** a scene: the climb stops for it */
  cine: boolean;
  /** who says each line */
  who: Speaker[];
  /** he must bring this from the summit (she asked for it last time) */
  brings?: ItemId;
  /** she asks for this, for the next time */
  asks?: ItemId;
  /** the last line is a choice: bring it now, or not yet */
  choice?: boolean;
  /** summits since the last episode before this one can happen (1 if unset) */
  gap?: number;
  /** while this one is next, she stands with her back to him */
  back?: boolean;
  /** the last scene: after it, the ending */
  finale?: boolean;
};

export const EPISODES = SHAPE.episodes as Episode[];

/*
 * The other damned. Prometheus, chained to a cliff beside the path for
 * giving men fire; Zeus's eagle comes every day to tear at him. Tantalus, in
 * water he can never drink, under fruit he can never reach.
 */
export type CompanionId = "pro" | "tan";
export type BurdenId = "pain" | "thirst";
export type PassiveId = "fire" | "patience";

export type Meeting = {
  who: Speaker[];
  /** this meeting offers a burden to carry */
  offer?: BurdenId;
  /** only once he has carried their burden: they leave a wedge */
  gift?: boolean;
  /** only once Merope's story has reached this episode */
  minEp?: number;
};

export const COMPANIONS: Record<
  CompanionId,
  { burden: BurdenId; passive: PassiveId; meetings: Meeting[]; post: Meeting[] }
> = {
  pro: {
    burden: "pain",
    passive: "fire",
    meetings: SHAPE.pro.meetings as Meeting[],
    post: SHAPE.pro.post as Meeting[],
  },
  tan: {
    burden: "thirst",
    passive: "patience",
    meetings: SHAPE.tan.meetings as Meeting[],
    post: SHAPE.tan.post as Meeting[],
  },
};

type CompanionText = {
  meetings: string[][];
  /** after the story: their own, in order */
  post: string[][];
  /** a line in passing when there is nothing new: Acts I, II, III, after */
  idle: string[][];
  declined: string;
  accepted: string;
};

/** the words of the story, in one language (story-text/<lang>.json) */
export type StoryLines = {
  /** what each thing he brings down is */
  items: Record<ItemId, string>;
  /** the words of each episode, line for line with EPISODES[].who */
  episodes: string[][];
  /** a line from her when there is nothing new to say, by act */
  fillers: string[][];
  /** what she says with her back to him */
  turned: string[];
  /** her answers to "not yet", in turn */
  notYetReplies: string[];
  companions: Record<CompanionId, CompanionText>;
};

type StoryUi = {
  names: Record<Speaker, string>;
  /** "From the summit:" */
  took: string;
  /** the answers to the choice in the request scene */
  bring: string;
  notYet: string;
  burdens: Record<BurdenId, { name: string; desc: string }>;
  passives: Record<PassiveId, { name: string; desc: string }>;
  accept: string;
  decline: string;
  wedgeHeld: string;
  /** "tap to continue" */
  next: string;
};

export type LangStory = StoryUi & StoryLines;

const UI: Record<LangCode, StoryUi> = {
  tr: {
    names: {
      merope: "Merope",
      sis: "Sisifos",
      pro: "Prometheus",
      tan: "Tantalos",
      narr: "",
    },
    took: "Zirveden:",
    bring: "Suyu getireceğim",
    notYet: "Henüz değil",
    burdens: {
      pain: {
        name: "Prometheus'un acısı",
        desc: "Bu tırmanışta taş daha ağır.",
      },
      thirst: {
        name: "Tantalos'un susuzluğu",
        desc: "Bu tırmanışta nefesin daha çabuk tükenir.",
      },
    },
    passives: {
      fire: {
        name: "Ateş",
        desc: "Nefesin daha çabuk geri gelir. Kalıcı.",
      },
      patience: {
        name: "Sabır",
        desc: "Kayınca daha az geri gidersin. Kalıcı.",
      },
    },
    accept: "Taşı",
    decline: "Reddet",
    wedgeHeld: "Takoz tuttu.",
    next: "devam",
  },
  en: {
    names: {
      merope: "Merope",
      sis: "Sisyphus",
      pro: "Prometheus",
      tan: "Tantalus",
      narr: "",
    },
    took: "From the summit:",
    bring: "I'll bring the water",
    notYet: "Not yet",
    burdens: {
      pain: {
        name: "Prometheus's pain",
        desc: "The stone is heavier this climb.",
      },
      thirst: {
        name: "Tantalus's thirst",
        desc: "Your breath runs out faster this climb.",
      },
    },
    passives: {
      fire: {
        name: "Fire",
        desc: "Your breath comes back faster. For good.",
      },
      patience: {
        name: "Patience",
        desc: "A slip gives back less ground. For good.",
      },
    },
    accept: "Carry it",
    decline: "Refuse",
    wedgeHeld: "The wedge held.",
    next: "continue",
  },
  zh: {
    names: {
      merope: "墨洛珀",
      sis: "西西弗斯",
      pro: "普罗米修斯",
      tan: "坦塔罗斯",
      narr: "",
    },
    took: "从山顶带来：",
    bring: "我去取水",
    notYet: "还不是时候",
    burdens: {
      pain: {
        name: "普罗米修斯的痛",
        desc: "这次攀登，石头更重。",
      },
      thirst: {
        name: "坦塔罗斯的渴",
        desc: "这次攀登，你的气息耗尽得更快。",
      },
    },
    passives: {
      fire: {
        name: "火",
        desc: "你的气息回来得更快。永久。",
      },
      patience: {
        name: "耐心",
        desc: "滑落时退得更少。永久。",
      },
    },
    accept: "背负",
    decline: "拒绝",
    wedgeHeld: "石楔挡住了。",
    next: "继续",
  },
  hi: {
    names: {
      merope: "मेरोपी",
      sis: "सिसिफ़स",
      pro: "प्रोमेथियस",
      tan: "टैंटलस",
      narr: "",
    },
    took: "चोटी से:",
    bring: "मैं पानी लाऊँगा",
    notYet: "अभी नहीं",
    burdens: {
      pain: {
        name: "प्रोमेथियस का दर्द",
        desc: "इस चढ़ाई में पत्थर ज़्यादा भारी है।",
      },
      thirst: {
        name: "टैंटलस की प्यास",
        desc: "इस चढ़ाई में तुम्हारी साँस जल्दी उखड़ती है।",
      },
    },
    passives: {
      fire: {
        name: "आग",
        desc: "तुम्हारी साँस जल्दी लौटती है। हमेशा के लिए।",
      },
      patience: {
        name: "धैर्य",
        desc: "फिसलने पर तुम कम पीछे जाते हो। हमेशा के लिए।",
      },
    },
    accept: "ढो लें",
    decline: "मना करें",
    wedgeHeld: "टेक ने थाम लिया।",
    next: "जारी रखें",
  },
  es: {
    names: {
      merope: "Mérope",
      sis: "Sísifo",
      pro: "Prometeo",
      tan: "Tántalo",
      narr: "",
    },
    took: "Desde la cima:",
    bring: "Traeré el agua",
    notYet: "Todavía no",
    burdens: {
      pain: {
        name: "El dolor de Prometeo",
        desc: "La piedra pesa más en esta subida.",
      },
      thirst: {
        name: "La sed de Tántalo",
        desc: "En esta subida te quedas sin aliento antes.",
      },
    },
    passives: {
      fire: {
        name: "Fuego",
        desc: "Recuperas el aliento más deprisa. Para siempre.",
      },
      patience: {
        name: "Paciencia",
        desc: "Un resbalón te hace perder menos terreno. Para siempre.",
      },
    },
    accept: "Cargarlo",
    decline: "Rechazar",
    wedgeHeld: "La cuña aguantó.",
    next: "continuar",
  },
  fr: {
    names: {
      merope: "Mérope",
      sis: "Sisyphe",
      pro: "Prométhée",
      tan: "Tantale",
      narr: "",
    },
    took: "Du sommet :",
    bring: "J'apporterai l'eau",
    notYet: "Pas encore",
    burdens: {
      pain: {
        name: "La douleur de Prométhée",
        desc: "Le rocher est plus lourd pour cette montée.",
      },
      thirst: {
        name: "La soif de Tantale",
        desc: "Ton souffle s'épuise plus vite pour cette montée.",
      },
    },
    passives: {
      fire: {
        name: "Le feu",
        desc: "Ton souffle revient plus vite. Pour toujours.",
      },
      patience: {
        name: "La patience",
        desc: "Une glissade te fait perdre moins de terrain. Pour toujours.",
      },
    },
    accept: "Porter",
    decline: "Refuser",
    wedgeHeld: "La cale a tenu.",
    next: "continuer",
  },
  ar: {
    names: {
      merope: "ميروبي",
      sis: "سيزيف",
      pro: "بروميثيوس",
      tan: "تانتالوس",
      narr: "",
    },
    took: "من القمة:",
    bring: "سأحضر الماء",
    notYet: "ليس بعد",
    burdens: {
      pain: {
        name: "ألم بروميثيوس",
        desc: "الصخرة أثقل في هذا الصعود.",
      },
      thirst: {
        name: "عطش تانتالوس",
        desc: "ينفد نفَسك أسرع في هذا الصعود.",
      },
    },
    passives: {
      fire: {
        name: "النار",
        desc: "يعود نفَسك أسرع. إلى الأبد.",
      },
      patience: {
        name: "الصبر",
        desc: "تخسر أرضًا أقلّ حين تنزلق. إلى الأبد.",
      },
    },
    accept: "احمله",
    decline: "ارفض",
    wedgeHeld: "صمد الإسفين.",
    next: "متابعة",
  },
  bn: {
    names: {
      merope: "মেরোপি",
      sis: "সিসিফাস",
      pro: "প্রমিথিউস",
      tan: "ট্যান্টালাস",
      narr: "",
    },
    took: "চূড়া থেকে:",
    bring: "আমি জল আনব",
    notYet: "এখনো না",
    burdens: {
      pain: {
        name: "প্রমিথিউসের যন্ত্রণা",
        desc: "এই চড়াইয়ে পাথর বেশি ভারী।",
      },
      thirst: {
        name: "ট্যান্টালাসের তৃষ্ণা",
        desc: "এই চড়াইয়ে তোমার দম তাড়াতাড়ি ফুরোয়।",
      },
    },
    passives: {
      fire: {
        name: "আগুন",
        desc: "তোমার দম তাড়াতাড়ি ফেরে। চিরকালের জন্য।",
      },
      patience: {
        name: "ধৈর্য",
        desc: "পিছলে গেলে কম জমি হারাও। চিরকালের জন্য।",
      },
    },
    accept: "বয়ে নিন",
    decline: "ফিরিয়ে দিন",
    wedgeHeld: "খিলটা আটকে রাখল।",
    next: "চালিয়ে যান",
  },
  pt: {
    names: {
      merope: "Mérope",
      sis: "Sísifo",
      pro: "Prometeu",
      tan: "Tântalo",
      narr: "",
    },
    took: "Do topo:",
    bring: "Vou trazer a água",
    notYet: "Ainda não",
    burdens: {
      pain: {
        name: "A dor de Prometeu",
        desc: "A pedra fica mais pesada nesta subida.",
      },
      thirst: {
        name: "A sede de Tântalo",
        desc: "Seu fôlego acaba mais depressa nesta subida.",
      },
    },
    passives: {
      fire: {
        name: "Fogo",
        desc: "Seu fôlego volta mais depressa. Para sempre.",
      },
      patience: {
        name: "Paciência",
        desc: "Um escorregão custa menos terreno. Para sempre.",
      },
    },
    accept: "Carregar",
    decline: "Recusar",
    wedgeHeld: "O calço segurou.",
    next: "continuar",
  },
};

const LINES: Partial<Record<LangCode, StoryLines>> = { en: EN as StoryLines };

const LOAD: Record<LangCode, () => Promise<{ default: unknown }>> = {
  tr: () => import("./story-text/tr.json"),
  en: () => Promise.resolve({ default: EN }),
  zh: () => import("./story-text/zh.json"),
  hi: () => import("./story-text/hi.json"),
  es: () => import("./story-text/es.json"),
  fr: () => import("./story-text/fr.json"),
  ar: () => import("./story-text/ar.json"),
  bn: () => import("./story-text/bn.json"),
  pt: () => import("./story-text/pt.json"),
};

const loading: Partial<Record<LangCode, Promise<void>>> = {};

/** fetch a language's words; until they arrive its story is told in English */
export function loadStory(lang: LangCode): Promise<void> {
  if (LINES[lang]) return Promise.resolve();
  loading[lang] ??= LOAD[lang]()
    .then((m) => {
      LINES[lang] = m.default as StoryLines;
      cache.delete(lang);
    })
    .catch(() => {
      delete loading[lang];
    });
  return loading[lang];
}

const cache = new Map<LangCode, LangStory>();

export function storyFor(lang: LangCode): LangStory {
  let s = cache.get(lang);
  if (!s) {
    s = { ...UI[lang], ...(LINES[lang] ?? (EN as StoryLines)) };
    if (LINES[lang]) cache.set(lang, s);
  }
  return s;
}
