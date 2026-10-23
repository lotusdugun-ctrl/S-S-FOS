/**
 * Seals: the game's achievements, pressed like wax seals into the Book of
 * Wisdom. Each is earned once and kept. They are worked out from what the game
 * already knows — the summit just reached, the cycle count, the pages found —
 * so nothing new has to be tracked during play beyond the hills climbed.
 */
import type { LangCode } from "@/i18n";
import type { RunStats } from "./engine";
import { CHAPTER_COUNT, CHAPTER_OF, QUOTE_COUNT } from "./quotes";

export type SealId =
  | "first"
  | "c10"
  | "c50"
  | "c100"
  | "night"
  | "storm"
  | "noslip"
  | "norest"
  | "rhythm"
  | "hills"
  | "chapter"
  | "p100"
  | "p1000"
  | "happy";

export const SEAL_ORDER: SealId[] = [
  "first",
  "c10",
  "c50",
  "c100",
  "night",
  "storm",
  "noslip",
  "norest",
  "rhythm",
  "hills",
  "chapter",
  "p100",
  "p1000",
  "happy",
];

const HILLS = 4;

export type SealContext = {
  /** completed summits, ever */
  summits: number;
  /** the climb that just ended, if one did */
  run: RunStats | null;
  /** pages found */
  unlocked: number[];
  /** hills ever summited, by level id */
  hills: string[];
  /** the story has reached its end */
  ended?: boolean;
};

/** every seal the context satisfies */
export function sealsEarned(c: SealContext): SealId[] {
  const out: SealId[] = [];
  const r = c.run;
  if (c.summits >= 1) out.push("first");
  if (c.summits >= 10) out.push("c10");
  if (c.summits >= 50) out.push("c50");
  if (c.summits >= 100) out.push("c100");
  if (r?.night) out.push("night");
  if (r?.weather === "storm") out.push("storm");
  if (r && !r.calm && r.slips === 0) out.push("noslip");
  if (r && !r.calm && r.rests === 0) out.push("norest");
  if (r && r.maxStreak >= 8) out.push("rhythm");
  if (new Set(c.hills).size >= HILLS) out.push("hills");
  const found = new Set(c.unlocked);
  const per = Array.from({ length: CHAPTER_COUNT }, () => [0, 0]);
  CHAPTER_OF.forEach((ch, i) => {
    per[ch]![1]!++;
    if (found.has(i)) per[ch]![0]!++;
  });
  if (per.some(([n, of]) => of! > 0 && n === of)) out.push("chapter");
  if (found.size >= 100) out.push("p100");
  if (found.size >= QUOTE_COUNT) out.push("p1000");
  if (c.ended) out.push("happy");
  return out;
}

export const SEAL_TEXT: Record<SealId, Record<LangCode, { name: string; desc: string }>> = {
  first: {
    tr: { name: "İlk Zirve", desc: "Zirveye ilk kez ulaş." },
    en: { name: "First Summit", desc: "Reach the summit for the first time." },
    zh: { name: "初登山顶", desc: "首次登上山顶。" },
    hi: { name: "पहली चोटी", desc: "पहली बार चोटी तक पहुँचो।" },
    es: { name: "Primera cima", desc: "Llega a la cima por primera vez." },
    fr: { name: "Premier sommet", desc: "Atteins le sommet pour la première fois." },
    ar: { name: "القمة الأولى", desc: "اصعد إلى القمة لأول مرة." },
    bn: { name: "প্রথম চূড়া", desc: "প্রথমবার চূড়ায় পৌঁছাও।" },
    pt: { name: "Primeiro cume", desc: "Chegue ao cume pela primeira vez." },
  },
  c10: {
    tr: { name: "On Döngü", desc: "Zirveye on kez ulaş." },
    en: { name: "Ten Cycles", desc: "Reach the summit ten times." },
    zh: { name: "十次轮回", desc: "登上山顶十次。" },
    hi: { name: "दस चक्र", desc: "दस बार चोटी तक पहुँचो।" },
    es: { name: "Diez ciclos", desc: "Llega a la cima diez veces." },
    fr: { name: "Dix cycles", desc: "Atteins le sommet dix fois." },
    ar: { name: "عشر دورات", desc: "اصعد إلى القمة عشر مرات." },
    bn: { name: "দশ চক্র", desc: "দশবার চূড়ায় পৌঁছাও।" },
    pt: { name: "Dez ciclos", desc: "Chegue ao cume dez vezes." },
  },
  c50: {
    tr: { name: "Elli Döngü", desc: "Zirveye elli kez ulaş." },
    en: { name: "Fifty Cycles", desc: "Reach the summit fifty times." },
    zh: { name: "五十次轮回", desc: "登上山顶五十次。" },
    hi: { name: "पचास चक्र", desc: "पचास बार चोटी तक पहुँचो।" },
    es: { name: "Cincuenta ciclos", desc: "Llega a la cima cincuenta veces." },
    fr: { name: "Cinquante cycles", desc: "Atteins le sommet cinquante fois." },
    ar: { name: "خمسون دورة", desc: "اصعد إلى القمة خمسين مرة." },
    bn: { name: "পঞ্চাশ চক্র", desc: "পঞ্চাশবার চূড়ায় পৌঁছাও।" },
    pt: { name: "Cinquenta ciclos", desc: "Chegue ao cume cinquenta vezes." },
  },
  c100: {
    tr: { name: "Yüz Döngü", desc: "Zirveye yüz kez ulaş." },
    en: { name: "A Hundred Cycles", desc: "Reach the summit a hundred times." },
    zh: { name: "百次轮回", desc: "登上山顶一百次。" },
    hi: { name: "सौ चक्र", desc: "सौ बार चोटी तक पहुँचो।" },
    es: { name: "Cien ciclos", desc: "Llega a la cima cien veces." },
    fr: { name: "Cent cycles", desc: "Atteins le sommet cent fois." },
    ar: { name: "مئة دورة", desc: "اصعد إلى القمة مئة مرة." },
    bn: { name: "একশো চক্র", desc: "একশোবার চূড়ায় পৌঁছাও।" },
    pt: { name: "Cem ciclos", desc: "Chegue ao cume cem vezes." },
  },
  night: {
    tr: { name: "Gece Yürüyüşü", desc: "Zirveye gece ulaş." },
    en: { name: "Night Walk", desc: "Reach the summit at night." },
    zh: { name: "夜行", desc: "在夜间登上山顶。" },
    hi: { name: "रात की यात्रा", desc: "रात में चोटी तक पहुँचो।" },
    es: { name: "Paseo nocturno", desc: "Llega a la cima de noche." },
    fr: { name: "Marche nocturne", desc: "Atteins le sommet de nuit." },
    ar: { name: "مسير الليل", desc: "اصعد إلى القمة ليلًا." },
    bn: { name: "রাতের পথচলা", desc: "রাতে চূড়ায় পৌঁছাও।" },
    pt: { name: "Caminhada noturna", desc: "Chegue ao cume à noite." },
  },
  storm: {
    tr: { name: "Fırtınaya Karşı", desc: "Fırtınada zirveye ulaş." },
    en: { name: "Against the Storm", desc: "Reach the summit in a storm." },
    zh: { name: "迎风暴而上", desc: "在暴风雨中登上山顶。" },
    hi: { name: "तूफ़ान के विरुद्ध", desc: "तूफ़ान में चोटी तक पहुँचो।" },
    es: { name: "Contra la tormenta", desc: "Llega a la cima durante una tormenta." },
    fr: { name: "Contre l'orage", desc: "Atteins le sommet sous l'orage." },
    ar: { name: "في وجه العاصفة", desc: "اصعد إلى القمة أثناء العاصفة." },
    bn: { name: "ঝড়ের বিরুদ্ধে", desc: "ঝড়ের মধ্যে চূড়ায় পৌঁছাও।" },
    pt: { name: "Contra a tempestade", desc: "Chegue ao cume durante uma tempestade." },
  },

  noslip: {
    tr: {
      name: "Tükenmeyen Nefes",
      desc: "Nefesin hiç tükenmeden zirveye ulaş (sakin mod dışında).",
    },
    en: {
      name: "Unbroken Breath",
      desc: "Reach the summit without running out of breath (not in calm mode).",
    },
    zh: { name: "气息不绝", desc: "在气息未耗尽的情况下登上山顶（平静模式除外）。" },
    hi: { name: "अटूट साँस", desc: "साँस खत्म हुए बिना चोटी तक पहुँचो (शांत मोड को छोड़कर)।" },
    es: {
      name: "Aliento inagotable",
      desc: "Llega a la cima sin quedarte sin aliento (no en modo tranquilo).",
    },
    fr: {
      name: "Souffle inépuisable",
      desc: "Atteins le sommet sans jamais manquer de souffle (hors mode calme).",
    },
    ar: { name: "نَفَس لا ينقطع", desc: "اصعد إلى القمة دون أن تنفد أنفاسك (خارج الوضع الهادئ)." },
    bn: { name: "অটুট দম", desc: "দম না ফুরিয়েই চূড়ায় পৌঁছাও (শান্ত মোড ছাড়া)।" },
    pt: {
      name: "Fôlego inesgotável",
      desc: "Chegue ao cume sem perder o fôlego (fora do modo calmo).",
    },
  },
  norest: {
    tr: { name: "Durmadan", desc: "Hiç dinlenmeden zirveye ulaş (sakin mod dışında)." },
    en: {
      name: "Without Pause",
      desc: "Reach the summit without resting once (not in calm mode).",
    },
    zh: { name: "永不停歇", desc: "不休息一次登上山顶（平静模式除外）。" },
    hi: { name: "बिना रुके", desc: "एक बार भी आराम किए बिना चोटी तक पहुँचो (शांत मोड को छोड़कर)।" },
    es: {
      name: "Sin pausa",
      desc: "Llega a la cima sin descansar ni una vez (no en modo tranquilo).",
    },
    fr: {
      name: "Sans répit",
      desc: "Atteins le sommet sans te reposer une seule fois (hors mode calme).",
    },
    ar: { name: "بلا توقف", desc: "اصعد إلى القمة دون أن تستريح ولو مرة (خارج الوضع الهادئ)." },
    bn: { name: "বিরতিহীন", desc: "একবারও না জিরিয়ে চূড়ায় পৌঁছাও (শান্ত মোড ছাড়া)।" },
    pt: {
      name: "Sem pausa",
      desc: "Chegue ao cume sem descansar nenhuma vez (fora do modo calmo).",
    },
  },
  rhythm: {
    tr: { name: "Kusursuz Ritim", desc: "Art arda sekiz kusursuz hamle yap." },
    en: { name: "Perfect Rhythm", desc: "Make eight perfect heaves in a row." },
    zh: { name: "完美节奏", desc: "连续完成八次完美发力。" },
    hi: { name: "बेदाग़ लय", desc: "लगातार आठ बेदाग़ धक्के लगाओ।" },
    es: { name: "Ritmo perfecto", desc: "Haz ocho empujones perfectos seguidos." },
    fr: { name: "Rythme parfait", desc: "Réussis huit poussées parfaites d'affilée." },
    ar: { name: "إيقاع مثالي", desc: "نفّذ ثماني دفعات مثالية متتالية." },
    bn: { name: "নিখুঁত ছন্দ", desc: "পরপর আটটি নিখুঁত ধাক্কা দাও।" },
    pt: { name: "Ritmo perfeito", desc: "Faça oito empurrões perfeitos seguidos." },
  },
  hills: {
    tr: { name: "Dört Tepe", desc: "Dört tepenin her birinde zirveye ulaş." },
    en: { name: "Four Hills", desc: "Reach the summit of each of the four hills." },
    zh: { name: "四座山丘", desc: "登上四座山丘各自的山顶。" },
    hi: { name: "चार पहाड़ियाँ", desc: "चारों पहाड़ियों में से हर एक की चोटी तक पहुँचो।" },
    es: { name: "Cuatro colinas", desc: "Llega a la cima de cada una de las cuatro colinas." },
    fr: { name: "Quatre collines", desc: "Atteins le sommet de chacune des quatre collines." },
    ar: { name: "أربعة تلال", desc: "اصعد إلى قمة كل تل من التلال الأربعة." },
    bn: { name: "চার পাহাড়", desc: "চারটি পাহাড়ের প্রতিটির চূড়ায় পৌঁছাও।" },
    pt: { name: "Quatro colinas", desc: "Chegue ao cume de cada uma das quatro colinas." },
  },
  chapter: {
    tr: { name: "Bir Bölüm", desc: "Bilgelik Defteri'nin bir bölümünü tamamla." },
    en: { name: "A Chapter", desc: "Complete a chapter of the Book of Wisdom." },
    zh: { name: "一个章节", desc: "完成智慧之书的一个章节。" },
    hi: { name: "एक अध्याय", desc: "ज्ञान की पुस्तक का एक अध्याय पूरा करो।" },
    es: { name: "Un capítulo", desc: "Completa un capítulo del Libro de la sabiduría." },
    fr: { name: "Un chapitre", desc: "Termine un chapitre du Livre de sagesse." },
    ar: { name: "فصل كامل", desc: "أكمل فصلًا من كتاب الحكمة." },
    bn: { name: "একটি অধ্যায়", desc: "প্রজ্ঞার বই-এর একটি অধ্যায় শেষ করো।" },
    pt: { name: "Um capítulo", desc: "Complete um capítulo do Livro da sabedoria." },
  },
  p100: {
    tr: { name: "Yüz Sayfa", desc: "Defterde yüz söz topla." },
    en: { name: "A Hundred Pages", desc: "Collect a hundred sayings in the book." },
    zh: { name: "百页", desc: "在书中收集一百条箴言。" },
    hi: { name: "सौ पन्ने", desc: "पुस्तक में सौ वचन इकट्ठा करो।" },
    es: { name: "Cien páginas", desc: "Reúne cien frases en el libro." },
    fr: { name: "Cent pages", desc: "Rassemble cent paroles dans le livre." },
    ar: { name: "مئة صفحة", desc: "اجمع مئة حكمة في الكتاب." },
    bn: { name: "একশো পাতা", desc: "বইয়ে একশোটি বাণী সংগ্রহ করো।" },
    pt: { name: "Cem páginas", desc: "Reúna cem frases no livro." },
  },
  p1000: {
    tr: { name: "Bin Söz", desc: "Defterdeki bin sözün hepsini topla." },
    en: { name: "A Thousand Sayings", desc: "Collect all thousand sayings in the book." },
    zh: { name: "千言", desc: "收集书中全部一千条箴言。" },
    hi: { name: "हज़ार वचन", desc: "पुस्तक के सभी हज़ार वचन इकट्ठा करो।" },
    es: { name: "Mil frases", desc: "Reúne las mil frases del libro." },
    fr: { name: "Mille paroles", desc: "Rassemble les mille paroles du livre." },
    ar: { name: "ألف حكمة", desc: "اجمع كل الحِكَم الألف في الكتاب." },
    bn: { name: "হাজার বাণী", desc: "বইয়ের সব হাজার বাণী সংগ্রহ করো।" },
    pt: { name: "Mil frases", desc: "Reúna todas as mil frases do livro." },
  },
  happy: {
    tr: { name: "Mutlu Sisifos", desc: "Taşı iterken gülümsemeyi öğren." },
    en: { name: "Sisyphus Happy", desc: "Learn to smile while you push." },
    zh: { name: "幸福的西西弗斯", desc: "学会在推石时微笑。" },
    hi: { name: "सुखी सिसिफ़स", desc: "पत्थर धकेलते हुए मुस्कुराना सीखो।" },
    es: { name: "Sísifo dichoso", desc: "Aprende a sonreír mientras empujas." },
    fr: { name: "Sisyphe heureux", desc: "Apprends à sourire en poussant." },
    ar: { name: "سيزيف سعيدًا", desc: "تعلّم أن تبتسم وأنت تدفع." },
    bn: { name: "সুখী সিসিফাস", desc: "ঠেলতে ঠেলতে হাসতে শেখো।" },
    pt: { name: "Sísifo feliz", desc: "Aprenda a sorrir enquanto empurra." },
  },
};
