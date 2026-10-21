/**
 * Ione. The story the hill tells.
 *
 * Halfway up the slope a girl sits on a rock and watches him. She asks him
 * to bring her things from the top — a stone, a feather, snow, the view —
 * and on his way back down he gives them to her. The gods do not age, and
 * neither does he; she does. Act I she is a child, Act II a widow with a son,
 * Act III an old woman, and at the end her grandson comes alone. What she
 * asks for follows his mind through the three stages (see journey.ts): from
 * proof that the gods are watching, through whether anything means anything,
 * to the light on her own hands.
 *
 * Structure lives here, in one place; the words live in STORY_TEXT, one
 * entry per language, line for line in the same order.
 */
import type { LangCode } from "./quotes";

export type Speaker = "ione" | "sis" | "lykos" | "pro" | "tan";

/** what he can carry down from the summit */
export type ItemId =
  | "stone"
  | "feather"
  | "answer"
  | "sea"
  | "thyme"
  | "dust"
  | "silence"
  | "snow"
  | "hands"
  | "shard"
  | "light"
  | "poppy"
  | "view"
  | "pebble"
  | "understanding";

export type Episode = {
  /** the stage he must have reached before she asks this */
  act: 1 | 2 | 3;
  item: ItemId;
  /** who says each line on the way up (the ask) and down (the giving) */
  ask: Speaker[];
  give: Speaker[];
  /** the last episode: the summit of this climb is the ending */
  ending?: boolean;
};

export const EPISODES: Episode[] = [
  // ---- Act I: a child, and a man who is angry ----
  { act: 1, item: "stone", ask: ["ione", "sis", "ione"], give: ["sis", "ione", "sis"] },
  { act: 1, item: "feather", ask: ["ione"], give: ["sis", "ione", "sis"] },
  { act: 1, item: "answer", ask: ["ione", "sis", "ione", "ione"], give: ["sis", "ione"] },
  { act: 1, item: "sea", ask: ["ione"], give: ["sis", "ione"] },
  { act: 1, item: "thyme", ask: ["ione"], give: ["sis", "ione", "sis"] },
  // ---- Act II: years later, a widow; a man who no longer knows why ----
  { act: 2, item: "dust", ask: ["ione", "sis", "ione"], give: ["sis", "ione"] },
  { act: 2, item: "silence", ask: ["ione", "ione"], give: ["sis", "ione"] },
  { act: 2, item: "snow", ask: ["ione", "sis", "ione"], give: ["sis", "ione"] },
  { act: 2, item: "hands", ask: ["ione", "sis", "ione"], give: ["sis", "ione"] },
  { act: 2, item: "shard", ask: ["ione", "ione"], give: ["sis", "ione", "sis"] },
  // ---- Act III: an old woman; a man who has made the hill his own ----
  { act: 3, item: "light", ask: ["ione", "sis", "ione", "sis", "ione"], give: ["sis", "ione"] },
  { act: 3, item: "poppy", ask: ["ione", "sis", "ione"], give: ["sis", "ione"] },
  { act: 3, item: "view", ask: ["ione", "sis", "ione"], give: ["sis", "ione", "sis"] },
  { act: 3, item: "pebble", ask: ["ione", "lykos", "sis", "ione"], give: ["sis", "lykos", "sis"] },
  { act: 3, item: "understanding", ask: ["lykos", "lykos"], give: ["lykos", "sis"], ending: true },
];

/*
 * The other damned. Two more souls share the hill, met on alternate climbs,
 * and each meeting with the same one goes further than the last: first they
 * talk only of their own punishment, then they give advice, then they ask
 * him to carry their burden to the top — which makes the climb harder, and
 * earns something that stays with him for good — and later, when he has
 * fallen many times, they measure their fate against his.
 *
 *  Prometheus, chained to a rock for giving men fire. Burden: his pain (the
 *  stone is heavier). Gift: Fire — breath comes back faster.
 *  Tantalus, in water he can never drink. Burden: his thirst (breath drains
 *  faster). Gift: Patience — a slip gives back less ground.
 *
 * Whoever's burden he has carried wedges a stone behind his on the next
 * climb, once: the first slip past it stops there.
 */
export type CompanionId = "pro" | "tan";
export type BurdenId = "pain" | "thirst";
export type PassiveId = "fire" | "patience";

export type Meeting = {
  /** who says each line */
  who: Speaker[];
  /** this meeting offers a burden to carry */
  offer?: BurdenId;
  /** only once he has carried their burden: they leave a wedge */
  gift?: boolean;
  /** only after this many summits */
  after?: number;
};

export const COMPANIONS: Record<
  CompanionId,
  { burden: BurdenId; passive: PassiveId; meetings: Meeting[] }
> = {
  pro: {
    burden: "pain",
    passive: "fire",
    meetings: [
      { who: ["pro", "pro"] },
      { who: ["pro", "sis", "pro"] },
      { who: ["pro"] },
      { who: ["pro"], offer: "pain" },
      { who: ["pro", "pro"], gift: true },
      { who: ["pro", "sis", "pro"], after: 15 },
      { who: ["pro"] },
    ],
  },
  tan: {
    burden: "thirst",
    passive: "patience",
    meetings: [
      { who: ["tan", "tan"] },
      { who: ["tan", "sis", "tan"] },
      { who: ["tan"], offer: "thirst" },
      { who: ["tan", "tan"], gift: true },
      { who: ["tan", "sis", "tan"], after: 12 },
      { who: ["tan"] },
    ],
  },
};

type CompanionText = {
  /** each meeting's lines, in COMPANIONS order */
  meetings: string[][];
  /** what they say once the meetings have run out */
  idle: string[];
  /** said when he declines / accepts the burden */
  declined: string;
  accepted: string;
};

type LangStory = {
  names: Record<Speaker, string>;
  companions: Record<CompanionId, CompanionText>;
  burdens: Record<BurdenId, { name: string; desc: string }>;
  passives: Record<PassiveId, { name: string; desc: string }>;
  /** the two buttons, and what shows when a wedge catches the stone */
  accept: string;
  decline: string;
  wedgeHeld: string;
  /** "Taken from the summit:" */
  took: string;
  items: Record<ItemId, string>;
  /** the words of each episode, in EPISODES order */
  episodes: Array<{ ask: string[]; give: string[] }>;
  /** what she says when there is nothing to ask yet, by act; after = the grandson, after the end */
  fillers: [string[], string[], string[]];
  after: string[];
};

export const STORY_TEXT: Record<LangCode, LangStory> = {
  en: {
    names: { ione: "Ione", sis: "Sisyphus", lykos: "Lykos", pro: "Prometheus", tan: "Tantalus" },
    companions: {
      pro: {
        meetings: [
          [
            "Another one. They chained me here for giving men fire.",
            "Every day the eagle comes for my liver. Every night it grows back. Do you see? Mine is the worse.",
          ],
          [
            "You push like a man trying to reach something. Don't look at the summit while you push.",
            "Where should I look?",
            "At your hands. They are the only part of this that is yours.",
          ],
          ["They say I loved mankind. I never asked to be loved back. Just as well."],
          [
            "Will you carry my pain to the top? Once. I want to know whether it weighs anything up there.",
          ],
          [
            "You carried it. For a moment I felt lighter — I had forgotten what that was.",
            "On your next climb my chain will catch your stone, once, if it slips. A small fire for you.",
          ],
          [
            "Your stone at least moves. My chains never break.",
            "Then you have it easier. You only have to wait.",
            "No, brother. Yours is a punishment of doing; mine of waiting. Doing is the lighter one — you get to choose how.",
          ],
          [
            "I gave men fire so they could see in the dark. You are showing them something better: that one can keep walking in it.",
          ],
        ],
        idle: [
          "The eagle is late today. I almost miss it.",
          "Push, brother. I'll watch.",
          "Look at your hands, not the top.",
        ],
        declined: "Then I'll carry it myself. I always have.",
        accepted: "Ha. Heavier already, isn't it? Don't stop.",
      },
      tan: {
        meetings: [
          [
            "Water to my chin, fruit over my head. When I bend, the water flees. When I reach, the branch lifts.",
            "Forever, thirsty in a lake. And you? What is it you're denied?",
          ],
          [
            "Don't you want the top? I want this water, every moment.",
            "I don't want the top. I just get there.",
            "Then you're freer than I am. Wanting is my chain.",
          ],
          ["Carry my thirst for me, up there? Maybe at the top it is quenched."],
          [
            "Did it leave me up there? …No. But for a while I forgot it.",
            "Next climb I'll wedge a stone behind yours. Once. Wanting has taught me where things slip.",
          ],
          [
            "I think I understand you now. You don't push to reach.",
            "Then why?",
            "To push. I only ever reached to have. That was my mistake, not my sin.",
          ],
          ["If I stopped wanting, would the water still flee? Ask the top for me."],
        ],
        idle: [
          "Have you ever drunk your own sweat? Salty. Still water.",
          "Go on. Someone here should move.",
          "The fig is ripe again. Of course it is.",
        ],
        declined: "Of course. Who would want it?",
        accepted: "Feel it? That's what every moment is, for me.",
      },
    },
    burdens: {
      pain: { name: "Prometheus's pain", desc: "The stone is heavier this climb." },
      thirst: { name: "Tantalus's thirst", desc: "Your breath runs out faster this climb." },
    },
    passives: {
      fire: { name: "Fire", desc: "Your breath comes back faster. For good." },
      patience: { name: "Patience", desc: "A slip gives back less ground. For good." },
    },
    accept: "Carry it",
    decline: "Refuse",
    wedgeHeld: "The wedge held.",
    took: "From the summit:",
    items: {
      stone: "a stone from the very top",
      feather: "a feather from Zeus's eagle",
      answer: "an answer",
      sea: "a look at the sea",
      thyme: "a sprig of thyme",
      dust: "news of the hollow",
      silence: "the sky's reply",
      snow: "a handful of snow",
      hands: "what his hands know",
      shard: "a shard of his stone",
      light: "the morning light",
      poppy: "a poppy",
      view: "the view, in words",
      pebble: "a stone for the boy",
      understanding: "what he understood",
    },
    episodes: [
      {
        ask: [
          "Are you the man who tricked Death? Grandmother says you chained him in your cellar.",
          "Your grandmother talks too much. Out of the way, child.",
          "Bring me a stone from the very top. I want to see if it's different.",
        ],
        give: [
          "Here. From the top.",
          "It's… just a stone.",
          "Everything up there is just a stone.",
        ],
      },
      {
        ask: ["Is it true the gods watch you all day? Bring me something of theirs."],
        give: [
          "A feather from his eagle. It dropped it while it laughed at me.",
          "It's warm. Do the gods ever get cold?",
          "They feel nothing. That is how they can do this.",
        ],
      },
      {
        ask: [
          "Why don't you just stop? Leave it and come down with me.",
          "If I stop, they win.",
          "Win what?",
          "Think about it at the top. Tell me on the way down.",
        ],
        give: [
          "Stopping would mean agreeing with them. I will never agree.",
          "Father says that about the tax collectors. He is very tired.",
        ],
      },
      {
        ask: ["Can you see the sea from up there? I've never seen the sea."],
        give: ["It's there. Grey today, and very far away.", "Then I'll remember it grey."],
      },
      {
        ask: ["Tomorrow I go to the city with Mother. Bring me something for the road?"],
        give: [
          "Thyme, from the summit. It grows where nothing should.",
          "I'll come back, I promise. Keep pushing.",
          "Where else would I go?",
        ],
      },
      {
        ask: [
          "Sisyphus. Do you remember me? I was the girl who wanted a stone.",
          "You were small.",
          "Twelve years. You haven't changed — not even your sandals. Is the hollow still there, where you took my stone from?",
        ],
        give: [
          "Dust filled it. No one could tell a stone was ever there.",
          "Then nothing marks that we were here. Not even that.",
        ],
      },
      {
        ask: [
          "My husband died this winter. Everyone tells me it had a meaning.",
          "You're closer to the sky than I am. Ask the gods for me — what was it for?",
        ],
        give: [
          "I asked. The sky said nothing. It says nothing to me either.",
          "At least it's honest.",
        ],
      },
      {
        ask: [
          "I counted your climbs today. Thirty-one. Don't you count?",
          "I stopped. The number was the only thing that grew.",
          "My son has never seen snow. Bring him some from the top?",
        ],
        give: [
          "It melted on the way down. It always does.",
          "Then tell him what it was like. That's all any of us ever bring back.",
        ],
      },
      {
        ask: [
          "Why do you still push, if none of it matters?",
          "I don't know any more.",
          "Find out up there. I'll wait.",
        ],
        give: [
          "At the top there's only wind, and the way back. But my hands knew what to do the whole way.",
          "Maybe that's something. Maybe that's the only thing.",
        ],
      },
      {
        ask: [
          "My son wants to climb with you one day. I told him it's a punishment.",
          "Bring him something from up there. Anything.",
        ],
        give: [
          "A piece of my stone. The god's bolt broke it off.",
          "You'd give away your own burden?",
          "It's still heavy. Take it anyway.",
        ],
      },
      {
        ask: [
          "My knees ache on this path now. You climb it a hundred times a day and never complain.",
          "I used to. Every step.",
          "When did you stop?",
          "I didn't notice.",
          "Then bring me the morning light from up there. It never reaches my house.",
        ],
        give: ["It came down with me. Look — it's on your hands.", "So it is."],
      },
      {
        ask: [
          "Do you still hate them? The gods?",
          "Hate means looking up. I've been looking at the path.",
          "If anything grows up there now, bring me a flower.",
        ],
        give: [
          "A poppy. They grow wherever the stone has passed.",
          "Your punishment is making a garden.",
        ],
      },
      {
        ask: [
          "I'll be gone soon. Will you remember me?",
          "I remember every stone on this hill. You were never a stone.",
          "Then tell me what you see from the top. Today. Exactly.",
        ],
        give: [
          "The sea. The city. Your roof, and the smoke from your kitchen. And the long way down, which is mine.",
          "You sound almost happy.",
          "Do I?",
        ],
      },
      {
        ask: [
          "This is my grandson. Say hello, Lykos.",
          "Are you the man who tricked Death?",
          "His grandmother talks too much.",
          "Bring him a stone from the very top.",
        ],
        give: ["Here. From the top.", "It's just a stone.", "Yes. Isn't it wonderful?"],
      },
      {
        ask: [
          "Grandmother died in the spring.",
          'She told me to tell you: "He\'ll understand at the top."',
        ],
        give: [
          "What did you understand?",
          "That the climb was never in the way of my life. It was my life.",
        ],
      },
    ],
    fillers: [
      [
        "Faster! I'm counting.",
        "Mother says I mustn't talk to you. I'm talking to the stone.",
        "When you get to the top, wave at me!",
      ],
      [
        "I come here when the house is too quiet.",
        "You look tired today. Or maybe that's me.",
        "The goats know you now. They step aside.",
      ],
      [
        "Sit with me a while on your way down. You don't have to say anything.",
        "The path is worn where you walk. Like a riverbed.",
        "I grew old watching you. You didn't. Strange — I'm not jealous.",
      ],
    ],
    after: [
      "Grandmother said you'd be here. She was right about everything.",
      "Is the stone heavier today, or lighter?",
      "When I'm old, I'll still come.",
      "I brought bread. I'll leave it on the rock.",
    ],
  },
  tr: {
    names: { ione: "İone", sis: "Sisifos", lykos: "Likos", pro: "Prometheus", tan: "Tantalos" },
    companions: {
      pro: {
        meetings: [
          [
            "Bir tane daha. İnsanlara ateşi verdim diye beni buraya zincirlediler.",
            "Her gün kartal ciğerim için geliyor. Her gece ciğerim yeniden büyüyor. Görüyor musun? Benimki daha kötü.",
          ],
          [
            "Bir şeye ulaşmaya çalışan biri gibi itiyorsun. İterken zirveye bakma.",
            "Nereye bakayım?",
            "Ellerine. Bu işte sana ait olan tek şey onlar.",
          ],
          ["İnsanlığı sevdiğimi söylerler. Karşılığında sevilmeyi hiç istemedim. İyi de oldu."],
          [
            "Acımı tepeye taşır mısın? Bir kez. Orada bir ağırlığı olup olmadığını bilmek istiyorum.",
          ],
          [
            "Taşıdın. Bir an için hafifledim. Bunun ne olduğunu unutmuştum.",
            "Bir sonraki tırmanışında taşın kayarsa zincirim onu bir kez tutacak. Senin için küçük bir ateş.",
          ],
          [
            "Senin taşın en azından hareket ediyor. Benim zincirlerim hiç kopmuyor.",
            "O zaman seninki daha kolay. Sadece beklemen gerekiyor.",
            "Hayır kardeşim. Seninki bir eylem cezası, benimki bir bekleyiş. Eylem daha hafif olanı: nasıl yapacağını sen seçersin.",
          ],
          [
            "İnsanlara karanlıkta görsünler diye ateşi verdim. Sen onlara daha iyisini gösteriyorsun: karanlıkta da yürümeye devam edilebileceğini.",
          ],
        ],
        idle: [
          "Kartal bugün gecikti. Neredeyse özledim.",
          "İt, kardeşim. Ben izliyorum.",
          "Zirveye değil, ellerine bak.",
        ],
        declined: "O zaman kendim taşırım. Hep taşıdım.",
        accepted: "Ha. Şimdiden ağırlaştı, değil mi? Durma.",
      },
      tan: {
        meetings: [
          [
            "Su çeneme kadar, meyve başımın üstünde. Eğilince su kaçıyor. Uzanınca dal yükseliyor.",
            "Sonsuza dek, bir gölün ortasında susuz. Ya sen? Senden esirgenen ne?",
          ],
          [
            "Zirveyi istemiyor musun? Ben bu suyu her an istiyorum.",
            "Zirveyi istemiyorum. Sadece oraya varıyorum.",
            "O zaman benden özgürsün. İstemek benim zincirim.",
          ],
          ["Susuzluğumu benim için yukarı taşır mısın? Belki tepede diner."],
          [
            "Orada beni bıraktı mı? …Hayır. Ama bir süre unuttum.",
            "Bir sonraki tırmanışında taşının arkasına bir takoz koyacağım. Bir kez. İstemek bana şeylerin nerede kaydığını öğretti.",
          ],
          [
            "Sanırım artık seni anlıyorum. Ulaşmak için itmiyorsun.",
            "Peki neden?",
            "İtmek için. Ben hep sahip olmak için uzandım. Bu benim günahım değil, hatamdı.",
          ],
          ["İstemeyi bıraksam su yine kaçar mıydı? Tepeye benim için sor."],
        ],
        idle: [
          "Hiç kendi terini içtin mi? Tuzlu. Yine de su.",
          "Devam et. Burada birinin hareket etmesi gerek.",
          "İncir yine olgunlaştı. Tabii ki.",
        ],
        declined: "Tabii. Kim ister ki?",
        accepted: "Hissediyor musun? Benim için her an böyle.",
      },
    },
    burdens: {
      pain: { name: "Prometheus'un acısı", desc: "Bu tırmanışta taş daha ağır." },
      thirst: { name: "Tantalos'un susuzluğu", desc: "Bu tırmanışta nefesin daha çabuk tükenir." },
    },
    passives: {
      fire: { name: "Ateş", desc: "Nefesin daha çabuk geri gelir. Kalıcı." },
      patience: { name: "Sabır", desc: "Kayınca daha az geri gidersin. Kalıcı." },
    },
    accept: "Taşı",
    decline: "Reddet",
    wedgeHeld: "Takoz tuttu.",
    took: "Zirveden:",
    items: {
      stone: "en tepeden bir taş",
      feather: "Zeus'un kartalından bir tüy",
      answer: "bir cevap",
      sea: "denize bir bakış",
      thyme: "bir dal kekik",
      dust: "çukurdan haber",
      silence: "gökyüzünün cevabı",
      snow: "bir avuç kar",
      hands: "ellerinin bildiği",
      shard: "taşından bir parça",
      light: "sabah ışığı",
      poppy: "bir gelincik",
      view: "manzara, kelimelerle",
      pebble: "çocuk için bir taş",
      understanding: "anladığı şey",
    },
    episodes: [
      {
        ask: [
          "Ölümü kandıran adam sen misin? Büyükannem onu mahzenine zincirlediğini söylüyor.",
          "Büyükannen çok konuşuyor. Çekil yolumdan, çocuk.",
          "Bana en tepeden bir taş getir. Farklı mı diye bakmak istiyorum.",
        ],
        give: ["Al. Tepeden.", "Bu… sadece bir taş.", "Orada her şey sadece bir taş."],
      },
      {
        ask: ["Tanrıların seni bütün gün izlediği doğru mu? Bana onlardan bir şey getir."],
        give: [
          "Kartalından bir tüy. Bana gülerken düşürdü.",
          "Sıcak. Tanrılar hiç üşür mü?",
          "Hiçbir şey hissetmezler. Bunu yapabilmeleri de bu yüzden.",
        ],
      },
      {
        ask: [
          "Neden bırakmıyorsun? Taşı bırak, benimle aşağı gel.",
          "Durursam onlar kazanır.",
          "Neyi kazanırlar?",
          "Tepede düşün. İnerken bana söylersin.",
        ],
        give: [
          "Durmak, onlara hak vermek olur. Asla hak vermeyeceğim.",
          "Babam da vergi memurları için böyle diyor. Çok yorgun.",
        ],
      },
      {
        ask: ["Oradan deniz görünüyor mu? Ben hiç deniz görmedim."],
        give: ["Orada. Bugün gri, ve çok uzakta.", "O zaman onu gri hatırlarım."],
      },
      {
        ask: ["Yarın annemle şehre gidiyorum. Bana yol için bir şey getirir misin?"],
        give: [
          "Kekik, zirveden. Hiçbir şeyin yetişmemesi gereken yerde yetişiyor.",
          "Geri döneceğim, söz. İtmeye devam et.",
          "Başka nereye gidebilirim ki?",
        ],
      },
      {
        ask: [
          "Sisifos. Beni hatırlıyor musun? Taş isteyen kızdım.",
          "Küçüktün.",
          "On iki yıl geçti. Hiç değişmemişsin, sandaletlerin bile. Taşımı aldığın çukur hâlâ orada mı?",
        ],
        give: [
          "Toz doldurmuş. Orada bir taş olduğunu kimse anlayamaz.",
          "O zaman burada olduğumuza dair hiçbir iz yok. Bu bile.",
        ],
      },
      {
        ask: [
          "Kocam bu kış öldü. Herkes bunun bir anlamı olduğunu söylüyor.",
          "Sen gökyüzüne benden yakınsın. Tanrılara benim için sor: ne içindi?",
        ],
        give: ["Sordum. Gökyüzü bir şey demedi. Bana da hiçbir şey demiyor.", "En azından dürüst."],
      },
      {
        ask: [
          "Bugün tırmanışlarını saydım. Otuz bir. Sen saymıyor musun?",
          "Bıraktım. Büyüyen tek şey o sayıydı.",
          "Oğlum hiç kar görmedi. Tepeden ona biraz getirir misin?",
        ],
        give: [
          "İnerken eridi. Hep erir.",
          "O zaman ona nasıl olduğunu anlat. Hepimizin geri getirebildiği zaten bu.",
        ],
      },
      {
        ask: [
          "Hiçbiri önemli değilse neden hâlâ itiyorsun?",
          "Artık bilmiyorum.",
          "Yukarıda öğren. Ben beklerim.",
        ],
        give: [
          "Tepede yalnızca rüzgâr var, bir de dönüş yolu. Ama ellerim yol boyunca ne yapacağını biliyordu.",
          "Belki bu bir şeydir. Belki tek şey budur.",
        ],
      },
      {
        ask: [
          "Oğlum bir gün seninle tırmanmak istiyor. Ona bunun bir ceza olduğunu söyledim.",
          "Ona oradan bir şey getir. Herhangi bir şey.",
        ],
        give: [
          "Taşımdan bir parça. Tanrının yıldırımı kopardı.",
          "Kendi yükünü mü veriyorsun?",
          "Hâlâ ağır. Yine de al.",
        ],
      },
      {
        ask: [
          "Artık bu yolda dizlerim ağrıyor. Sen günde yüz kez tırmanıyorsun, hiç şikâyet etmiyorsun.",
          "Ederdim. Her adımda.",
          "Ne zaman bıraktın?",
          "Fark etmedim.",
          "O zaman bana oradan sabah ışığını getir. Benim evime hiç ulaşmıyor.",
        ],
        give: ["Benimle indi. Bak, ellerinde.", "Gerçekten de."],
      },
      {
        ask: [
          "Onlardan hâlâ nefret ediyor musun? Tanrılardan?",
          "Nefret, yukarı bakmak demek. Ben yola bakıyordum.",
          "Artık orada bir şey yetişiyorsa, bana bir çiçek getir.",
        ],
        give: ["Bir gelincik. Taşın geçtiği her yerde açıyorlar.", "Cezan bir bahçeye dönüşüyor."],
      },
      {
        ask: [
          "Yakında gideceğim. Beni hatırlayacak mısın?",
          "Bu tepedeki her taşı hatırlıyorum. Sen hiç taş olmadın.",
          "O zaman tepeden ne gördüğünü anlat. Bugün. Tam olarak.",
        ],
        give: [
          "Deniz. Şehir. Senin damın, mutfağından yükselen duman. Ve uzun iniş yolu, o da benim.",
          "Neredeyse mutlu gibisin.",
          "Öyle mi?",
        ],
      },
      {
        ask: [
          "Bu torunum. Merhaba de, Likos.",
          "Ölümü kandıran adam sen misin?",
          "Büyükannesi çok konuşuyor.",
          "Ona en tepeden bir taş getir.",
        ],
        give: ["Al. Tepeden.", "Bu sadece bir taş.", "Evet. Harika değil mi?"],
      },
      {
        ask: ["Büyükannem ilkbaharda öldü.", 'Sana söylememi istedi: "Tepede anlayacak."'],
        give: [
          "Ne anladın?",
          "Tırmanışın hayatımın önünde durmadığını. Onun, hayatımın ta kendisi olduğunu.",
        ],
      },
    ],
    fillers: [
      [
        "Daha hızlı! Sayıyorum.",
        "Annem seninle konuşmamamı söylüyor. Ben de taşla konuşuyorum.",
        "Tepeye varınca bana el salla!",
      ],
      [
        "Ev fazla sessizleşince buraya geliyorum.",
        "Bugün yorgun görünüyorsun. Ya da belki benim.",
        "Keçiler artık seni tanıyor. Kenara çekiliyorlar.",
      ],
      [
        "İnerken biraz yanımda otur. Bir şey söylemen gerekmiyor.",
        "Yürüdüğün yerde yol aşınmış. Bir dere yatağı gibi.",
        "Seni izlerken yaşlandım. Sen yaşlanmadın. Tuhaf, kıskanmıyorum.",
      ],
    ],
    after: [
      "Büyükannem burada olacağını söylemişti. Her konuda haklıydı.",
      "Taş bugün daha mı ağır, daha mı hafif?",
      "Yaşlandığımda da geleceğim.",
      "Ekmek getirdim. Kayanın üstüne bırakıyorum.",
    ],
  },
  zh: {
    names: { ione: "伊俄涅", sis: "西西弗斯", lykos: "吕科斯", pro: "普罗米修斯", tan: "坦塔罗斯" },
    companions: {
      pro: {
        meetings: [
          [
            "又来一个。他们把我锁在这里，因为我把火给了人类。",
            "每天，鹰都来啄我的肝。每夜，它又长回来。你看见了吗？我的更惨。",
          ],
          [
            "你推石头，像是想够到什么。推的时候别看山顶。",
            "那我该看哪里？",
            "看你的手。这一切里，只有它们是属于你的。",
          ],
          ["人们说我爱人类。我从没要求他们爱我。这样也好。"],
          ["你愿意把我的痛苦背上山顶吗？就一次。我想知道它在那上面有没有重量。"],
          [
            "你背上去了。有一瞬间我觉得轻了——我早忘了那是什么滋味。",
            "下次你往上推时，石头若是滑落，我的锁链会替你挡一次。一小簇给你的火。",
          ],
          [
            "你的石头至少还会动。我的锁链永远不会断。",
            "那你比我轻松。你只需要等。",
            "不，兄弟。你受的是行动之罚，我受的是等待之罚。行动更轻——你可以选择怎么做。",
          ],
          ["我把火给了人类，好让他们在黑暗中看见。你给他们看的更好：人可以在黑暗中继续走下去。"],
        ],
        idle: ["鹰今天来晚了。我差点想它了。", "推吧，兄弟。我看着。", "看你的手，别看山顶。"],
        declined: "那我自己背。我一直都是自己背。",
        accepted: "哈。已经更重了，是不是？别停。",
      },
      tan: {
        meetings: [
          [
            "水没到下巴，果子挂在头顶。我一低头，水就退去。我一伸手，树枝就升高。",
            "永远在湖里口渴。你呢？你被夺走的是什么？",
          ],
          [
            "你不想要山顶吗？我每时每刻都想要这水。",
            "我不想要山顶。我只是到那里去。",
            "那你比我自由。渴望就是我的锁链。",
          ],
          ["替我把口渴背上去，好吗？也许到了山顶，它就解了。"],
          [
            "它留在上面了吗？……没有。但有那么一会儿，我忘了它。",
            "你下次往上推时，我会在你石头后面垫一块石头。就一次。渴望教会了我东西会在哪里滑落。",
          ],
          [
            "我想我现在懂你了。你推，不是为了到达。",
            "那是为了什么？",
            "为了推。我伸手，从来只为了占有。那是我的错，不是我的罪。",
          ],
          ["要是我不再渴望，水还会退去吗？替我问问山顶。"],
        ],
        idle: [
          "你喝过自己的汗吗？咸的。可那也是水。",
          "去吧。这里总得有人在动。",
          "无花果又熟了。当然了。",
        ],
        declined: "当然。谁会想要呢？",
        accepted: "感觉到了吗？对我来说，每一刻都是这样。",
      },
    },
    burdens: {
      pain: { name: "普罗米修斯的痛苦", desc: "这一趟，石头更重。" },
      thirst: { name: "坦塔罗斯的口渴", desc: "这一趟，你的气息耗得更快。" },
    },
    passives: {
      fire: { name: "火", desc: "你的气息恢复得更快。永久。" },
      patience: { name: "耐心", desc: "滑落时退得更少。永久。" },
    },
    accept: "背上",
    decline: "拒绝",
    wedgeHeld: "垫石挡住了。",
    took: "从山顶带回：",
    items: {
      stone: "一块山顶上的石头",
      feather: "宙斯之鹰的一根羽毛",
      answer: "一个答案",
      sea: "望一眼大海",
      thyme: "一枝百里香",
      dust: "那个坑的消息",
      silence: "天空的回答",
      snow: "一捧雪",
      hands: "他的手知道的事",
      shard: "他石头的一块碎片",
      light: "清晨的光",
      poppy: "一朵罂粟花",
      view: "山顶的景色，用话说出来",
      pebble: "给孩子的一块石头",
      understanding: "他明白了什么",
    },
    episodes: [
      {
        ask: [
          "你就是那个骗过死神的人吗？奶奶说你把他锁在了你家地窖里。",
          "你奶奶话太多了。让开，孩子。",
          "给我带一块最顶上的石头。我想看看它是不是不一样。",
        ],
        give: ["给。山顶上的。", "这……只是一块石头。", "那上面的一切，都只是一块石头。"],
      },
      {
        ask: ["众神整天都看着你，是真的吗？给我带一样他们的东西。"],
        give: [
          "他那只鹰的一根羽毛。它笑我的时候掉下来的。",
          "是暖的。神也会冷吗？",
          "他们什么也感觉不到。所以他们才做得出这种事。",
        ],
      },
      {
        ask: [
          "你为什么不干脆停下？丢下它，跟我下山吧。",
          "我一停，他们就赢了。",
          "赢什么？",
          "到山顶上想想。下来时告诉我。",
        ],
        give: ["停下就等于同意他们。我永远不会同意。", "爸爸说起收税的人也这么说。他很累。"],
      },
      {
        ask: ["从那上面能看见大海吗？我从没见过大海。"],
        give: ["在那儿。今天是灰色的，很远很远。", "那我就记住灰色的它。"],
      },
      {
        ask: ["明天我要跟妈妈去城里。给我带点路上用的东西？"],
        give: [
          "百里香，山顶上的。它长在什么都不该长的地方。",
          "我会回来的，我保证。继续推吧。",
          "我还能去哪儿呢？",
        ],
      },
      {
        ask: [
          "西西弗斯。你还记得我吗？我就是那个想要石头的女孩。",
          "那时你还小。",
          "十二年了。你一点没变——连凉鞋都没换。你给我拿石头的那个坑，还在吗？",
        ],
        give: [
          "被尘土填满了。没人看得出那里曾有过一块石头。",
          "那就什么都不能证明我们来过。连这个也不能。",
        ],
      },
      {
        ask: [
          "我丈夫今年冬天死了。每个人都对我说，这是有意义的。",
          "你离天空比我近。替我问问众神——那是为了什么？",
        ],
        give: ["我问了。天空什么也没说。它对我也什么都不说。", "至少它很诚实。"],
      },
      {
        ask: [
          "我今天数了你爬了几趟。三十一趟。你不数吗？",
          "我不数了。那个数字是唯一在长大的东西。",
          "我儿子从没见过雪。从山顶给他带一点？",
        ],
        give: [
          "下山的路上化了。总是这样。",
          "那就告诉他雪是什么样的。我们谁带回来的，都不过是这个。",
        ],
      },
      {
        ask: ["既然一切都无关紧要，你为什么还在推？", "我也不知道了。", "到上面去找答案。我等你。"],
        give: [
          "山顶上只有风，还有回来的路。可我的手一路上都知道该做什么。",
          "也许那就是点什么。也许那是唯一的东西。",
        ],
      },
      {
        ask: ["我儿子想有一天跟你一起爬。我告诉他那是一种惩罚。", "给他从上面带点东西。什么都行。"],
        give: [
          "我石头的一块碎片。是神的雷霆劈下来的。",
          "你要把自己的重担送人？",
          "它还是很重。拿着吧。",
        ],
      },
      {
        ask: [
          "如今走这条路，我的膝盖会疼。你一天爬一百趟，从不抱怨。",
          "我以前抱怨。每一步都抱怨。",
          "什么时候不抱怨了？",
          "我没注意到。",
          "那就从上面给我带清晨的光吧。它从来照不到我家。",
        ],
        give: ["它跟我一起下来了。看——就在你手上。", "真的呢。"],
      },
      {
        ask: [
          "你还恨他们吗？那些神？",
          "恨，意味着抬头看。我一直在看脚下的路。",
          "要是上面现在长出了什么，给我带一朵花吧。",
        ],
        give: ["一朵罂粟。石头滚过的地方，都会长出来。", "你的惩罚，正在变成一座花园。"],
      },
      {
        ask: [
          "我很快就要走了。你会记得我吗？",
          "我记得这座山上的每一块石头。你从来都不是石头。",
          "那就告诉我你在山顶看见了什么。今天的。一点不差。",
        ],
        give: [
          "大海。城市。你家的屋顶，还有你厨房里的炊烟。还有那条长长的下山路，那是我的。",
          "你听起来几乎是快乐的。",
          "是吗？",
        ],
      },
      {
        ask: [
          "这是我孙子。跟他打个招呼，吕科斯。",
          "你就是那个骗过死神的人吗？",
          "他奶奶话太多了。",
          "给他带一块最顶上的石头。",
        ],
        give: ["给。山顶上的。", "这只是一块石头。", "是啊。是不是很美妙？"],
      },
      {
        ask: ["奶奶春天去世了。", "她让我告诉你：「到了山顶，他会明白的。」"],
        give: ["你明白了什么？", "这攀登从来没有挡在我生命的路上。它就是我的生命。"],
      },
    ],
    fillers: [
      ["快点！我在数呢。", "妈妈说我不许跟你说话。我是在跟石头说话。", "你到了山顶，要朝我挥手！"],
      [
        "家里太安静的时候，我就来这儿。",
        "你今天看起来很累。也许累的是我。",
        "山羊现在都认得你了。它们会让路。",
      ],
      [
        "下山时陪我坐一会儿吧。你什么都不用说。",
        "你走过的地方，路都磨出来了。像一道河床。",
        "我看着你变老了。你却没有。奇怪——我并不嫉妒。",
      ],
    ],
    after: [
      "奶奶说你会在这儿。她什么都说对了。",
      "石头今天是更重，还是更轻？",
      "等我老了，我还会来。",
      "我带了面包。我把它放在石头上。",
    ],
  },
  hi: {
    names: { ione: "आयोनी", sis: "सिसिफ़स", lykos: "लाइकोस", pro: "प्रोमेथियस", tan: "टैंटलस" },
    companions: {
      pro: {
        meetings: [
          [
            "एक और। इंसानों को आग देने के लिए उन्होंने मुझे यहाँ ज़ंजीरों में बाँध दिया।",
            "हर दिन बाज़ मेरा कलेजा नोचने आता है। हर रात वह फिर उग आता है। देखा? मेरी सज़ा ज़्यादा बुरी है।",
          ],
          [
            "तुम ऐसे धकेलते हो जैसे किसी चीज़ तक पहुँचना चाहते हो। धकेलते वक़्त चोटी की ओर मत देखो।",
            "तो कहाँ देखूँ?",
            "अपने हाथों को। इस सब में बस वही तुम्हारे अपने हैं।",
          ],
          ["कहते हैं मुझे इंसानों से प्यार था। मैंने बदले में प्यार कभी नहीं माँगा। अच्छा ही हुआ।"],
          [
            "क्या तुम मेरा दर्द चोटी तक ले जाओगे? बस एक बार। मैं जानना चाहता हूँ कि वहाँ ऊपर उसका कोई वज़न है या नहीं।",
          ],
          [
            "तुम ले गए। एक पल को मैं हल्का हुआ — मैं भूल ही गया था कि वह कैसा होता है।",
            "अगली चढ़ाई में तुम्हारा पत्थर फिसला तो मेरी ज़ंजीर उसे एक बार थाम लेगी। तुम्हारे लिए एक छोटी-सी आग।",
          ],
          [
            "तुम्हारा पत्थर कम से कम हिलता तो है। मेरी ज़ंजीरें कभी नहीं टूटतीं।",
            "तो तुम्हारे लिए आसान है। तुम्हें बस इंतज़ार करना है।",
            "नहीं, भाई। तुम्हारी सज़ा करने की है; मेरी इंतज़ार की। करना हल्का है — कैसे करना है, यह तुम चुन सकते हो।",
          ],
          [
            "मैंने इंसानों को आग दी ताकि वे अँधेरे में देख सकें। तुम उन्हें उससे बेहतर कुछ दिखा रहे हो: कि अँधेरे में भी चलते रहा जा सकता है।",
          ],
        ],
        idle: [
          "आज बाज़ देर से आया। मुझे लगभग उसकी याद आ गई।",
          "धकेलो, भाई। मैं देख रहा हूँ।",
          "अपने हाथों को देखो, चोटी को नहीं।",
        ],
        declined: "तो मैं ख़ुद उठा लूँगा। हमेशा उठाया है।",
        accepted: "हा। अभी से भारी लग रहा है न? रुकना मत।",
      },
      tan: {
        meetings: [
          [
            "ठोड़ी तक पानी, सिर के ऊपर फल। झुकता हूँ तो पानी भाग जाता है। हाथ बढ़ाता हूँ तो डाल ऊपर उठ जाती है।",
            "हमेशा के लिए, झील में प्यासा। और तुम? तुमसे क्या छीना गया है?",
          ],
          [
            "क्या तुम्हें चोटी नहीं चाहिए? मुझे यह पानी हर पल चाहिए।",
            "मुझे चोटी नहीं चाहिए। मैं बस वहाँ पहुँच जाता हूँ।",
            "तो तुम मुझसे ज़्यादा आज़ाद हो। चाहना ही मेरी ज़ंजीर है।",
          ],
          ["मेरी प्यास मेरे लिए ऊपर ले जाओगे? शायद चोटी पर वह बुझ जाए।"],
          [
            "क्या वह मुझे वहाँ ऊपर छोड़ गई? …नहीं। पर कुछ देर मैं उसे भूल गया।",
            "अगली चढ़ाई में मैं तुम्हारे पत्थर के पीछे एक पत्थर अटका दूँगा। एक बार। चाहने ने मुझे सिखाया है कि चीज़ें कहाँ फिसलती हैं।",
          ],
          [
            "लगता है अब मैं तुम्हें समझता हूँ। तुम पहुँचने के लिए नहीं धकेलते।",
            "तो फिर किसलिए?",
            "धकेलने के लिए। मैंने हमेशा पाने के लिए ही हाथ बढ़ाया। वह मेरी भूल थी, मेरा पाप नहीं।",
          ],
          ["अगर मैं चाहना छोड़ दूँ, तो क्या पानी फिर भी भागेगा? मेरे लिए चोटी से पूछना।"],
        ],
        idle: [
          "कभी अपना पसीना पिया है? खारा। फिर भी पानी है।",
          "चलते रहो। यहाँ किसी को तो हिलना चाहिए।",
          "अंजीर फिर पक गया। ज़ाहिर है।",
        ],
        declined: "ज़ाहिर है। इसे कौन चाहेगा?",
        accepted: "महसूस हुआ? मेरे लिए हर पल ऐसा ही है।",
      },
    },
    burdens: {
      pain: { name: "प्रोमेथियस का दर्द", desc: "इस चढ़ाई में पत्थर ज़्यादा भारी है।" },
      thirst: { name: "टैंटलस की प्यास", desc: "इस चढ़ाई में तुम्हारी साँस जल्दी ख़त्म होती है।" },
    },
    passives: {
      fire: { name: "आग", desc: "तुम्हारी साँस जल्दी लौटती है। हमेशा के लिए।" },
      patience: { name: "धीरज", desc: "फिसलने पर कम ज़मीन जाती है। हमेशा के लिए।" },
    },
    accept: "उठा लो",
    decline: "मना करो",
    wedgeHeld: "अटका पत्थर टिक गया।",
    took: "चोटी से:",
    items: {
      stone: "बिल्कुल चोटी का एक पत्थर",
      feather: "ज़ीउस के बाज़ का एक पंख",
      answer: "एक जवाब",
      sea: "समुद्र की एक झलक",
      thyme: "अजवाइन की एक टहनी",
      dust: "गड्ढे की ख़बर",
      silence: "आसमान का जवाब",
      snow: "मुट्ठी भर बर्फ़",
      hands: "जो उसके हाथ जानते हैं",
      shard: "उसके पत्थर का एक टुकड़ा",
      light: "सुबह की रोशनी",
      poppy: "एक पोस्ते का फूल",
      view: "नज़ारा, शब्दों में",
      pebble: "लड़के के लिए एक पत्थर",
      understanding: "जो उसने समझा",
    },
    episodes: [
      {
        ask: [
          "क्या तुम वही आदमी हो जिसने मौत को धोखा दिया था? दादी कहती हैं तुमने उसे अपने तहख़ाने में बाँध दिया था।",
          "तुम्हारी दादी बहुत बोलती हैं। रास्ते से हटो, बच्ची।",
          "मेरे लिए बिल्कुल चोटी से एक पत्थर लाओ। मैं देखना चाहती हूँ कि वह अलग है या नहीं।",
        ],
        give: ["लो। चोटी से।", "यह… बस एक पत्थर है।", "वहाँ ऊपर सब कुछ बस एक पत्थर है।"],
      },
      {
        ask: ["क्या सच है कि देवता दिन भर तुम्हें देखते हैं? मेरे लिए उनकी कोई चीज़ लाओ।"],
        give: [
          "उसके बाज़ का एक पंख। मुझ पर हँसते हुए उससे गिर गया।",
          "यह गरम है। क्या देवताओं को कभी ठंड लगती है?",
          "उन्हें कुछ महसूस नहीं होता। इसीलिए वे यह कर पाते हैं।",
        ],
      },
      {
        ask: [
          "तुम बस रुक क्यों नहीं जाते? इसे छोड़ो और मेरे साथ नीचे चलो।",
          "अगर मैं रुका, तो वे जीत जाएँगे।",
          "क्या जीत जाएँगे?",
          "चोटी पर इसके बारे में सोचना। लौटते हुए मुझे बताना।",
        ],
        give: [
          "रुकने का मतलब होगा उनसे सहमत होना। मैं कभी सहमत नहीं होऊँगा।",
          "पिताजी कर वसूलने वालों के बारे में यही कहते हैं। वे बहुत थके हुए हैं।",
        ],
      },
      {
        ask: ["क्या वहाँ ऊपर से समुद्र दिखता है? मैंने कभी समुद्र नहीं देखा।"],
        give: ["वहीं है। आज धूसर है, और बहुत दूर।", "तो मैं उसे धूसर ही याद रखूँगी।"],
      },
      {
        ask: ["कल मैं माँ के साथ शहर जा रही हूँ। रास्ते के लिए मेरे लिए कुछ लाओगे?"],
        give: [
          "अजवाइन, चोटी से। यह वहाँ उगती है जहाँ कुछ नहीं उगना चाहिए।",
          "मैं लौटूँगी, वादा। धकेलते रहना।",
          "मैं और जाऊँगा भी कहाँ?",
        ],
      },
      {
        ask: [
          "सिसिफ़स। मुझे पहचानते हो? मैं वही लड़की थी जिसे पत्थर चाहिए था।",
          "तुम छोटी थीं।",
          "बारह साल हो गए। तुम ज़रा नहीं बदले — तुम्हारी चप्पलें तक नहीं। जिस गड्ढे से तुमने मेरा पत्थर लिया था, क्या वह अब भी वहाँ है?",
        ],
        give: [
          "धूल ने उसे भर दिया। कोई नहीं बता सकता कि वहाँ कभी पत्थर था।",
          "तो कुछ भी नहीं बताता कि हम यहाँ थे। वह भी नहीं।",
        ],
      },
      {
        ask: [
          "इस सर्दी मेरे पति चल बसे। सब मुझसे कहते हैं कि इसका कोई अर्थ था।",
          "तुम मुझसे ज़्यादा आसमान के पास हो। मेरे लिए देवताओं से पूछना — वह किसलिए था?",
        ],
        give: [
          "मैंने पूछा। आसमान ने कुछ नहीं कहा। मुझसे भी वह कुछ नहीं कहता।",
          "कम से कम वह ईमानदार है।",
        ],
      },
      {
        ask: [
          "आज मैंने तुम्हारी चढ़ाइयाँ गिनीं। इकतीस। तुम नहीं गिनते?",
          "मैंने छोड़ दिया। बढ़ने वाली इकलौती चीज़ वह गिनती थी।",
          "मेरे बेटे ने कभी बर्फ़ नहीं देखी। चोटी से उसके लिए थोड़ी लाओगे?",
        ],
        give: [
          "नीचे आते-आते पिघल गई। हमेशा पिघल जाती है।",
          "तो उसे बताना कि वह कैसी थी। हम सब बस इतना ही तो वापस लाते हैं।",
        ],
      },
      {
        ask: [
          "अगर इसमें से कुछ भी मायने नहीं रखता, तो तुम अब भी क्यों धकेलते हो?",
          "अब मैं नहीं जानता।",
          "ऊपर जाकर पता करो। मैं इंतज़ार करूँगी।",
        ],
        give: [
          "चोटी पर बस हवा है, और लौटने का रास्ता। पर पूरे रास्ते मेरे हाथ जानते थे कि क्या करना है।",
          "शायद यह कुछ है। शायद बस यही है।",
        ],
      },
      {
        ask: [
          "मेरा बेटा एक दिन तुम्हारे साथ चढ़ना चाहता है। मैंने उसे बताया कि यह एक सज़ा है।",
          "उसके लिए वहाँ से कुछ लाओ। कुछ भी।",
        ],
        give: [
          "मेरे पत्थर का एक टुकड़ा। देवता की बिजली ने उसे तोड़ दिया था।",
          "तुम अपना ही बोझ दे दोगे?",
          "यह अब भी भारी है। फिर भी ले लो।",
        ],
      },
      {
        ask: [
          "अब इस रास्ते पर मेरे घुटने दुखते हैं। तुम दिन में सौ बार चढ़ते हो और कभी शिकायत नहीं करते।",
          "करता था। हर क़दम पर।",
          "कब छोड़ा?",
          "मुझे पता ही नहीं चला।",
          "तो वहाँ से मेरे लिए सुबह की रोशनी लाओ। वह मेरे घर तक कभी नहीं पहुँचती।",
        ],
        give: ["वह मेरे साथ नीचे आई। देखो — तुम्हारे हाथों पर है।", "सच में।"],
      },
      {
        ask: [
          "क्या तुम अब भी उनसे नफ़रत करते हो? देवताओं से?",
          "नफ़रत का मतलब है ऊपर देखना। मैं रास्ते को देखता रहा हूँ।",
          "अगर अब वहाँ ऊपर कुछ उगता है, तो मेरे लिए एक फूल लाना।",
        ],
        give: [
          "पोस्ते का फूल। जहाँ-जहाँ से पत्थर गुज़रा है, वहाँ उगते हैं।",
          "तुम्हारी सज़ा एक बग़ीचा बना रही है।",
        ],
      },
      {
        ask: [
          "मैं जल्द ही चली जाऊँगी। क्या तुम मुझे याद रखोगे?",
          "मुझे इस पहाड़ी का हर पत्थर याद है। तुम कभी पत्थर नहीं थीं।",
          "तो मुझे बताओ कि चोटी से तुम्हें क्या दिखता है। आज। ठीक-ठीक।",
        ],
        give: [
          "समुद्र। शहर। तुम्हारी छत, और तुम्हारी रसोई से उठता धुआँ। और नीचे का लंबा रास्ता, जो मेरा है।",
          "तुम लगभग ख़ुश लगते हो।",
          "सच में?",
        ],
      },
      {
        ask: [
          "यह मेरा पोता है। नमस्ते कहो, लाइकोस।",
          "क्या तुम वही आदमी हो जिसने मौत को धोखा दिया था?",
          "इसकी दादी बहुत बोलती हैं।",
          "इसके लिए बिल्कुल चोटी से एक पत्थर लाओ।",
        ],
        give: ["लो। चोटी से।", "यह बस एक पत्थर है।", "हाँ। कितना अद्भुत है न?"],
      },
      {
        ask: ["दादी वसंत में चल बसीं।", "उन्होंने कहा था तुमसे कहूँ: “चोटी पर वह समझ जाएगा।”"],
        give: [
          "तुमने क्या समझा?",
          "कि चढ़ाई कभी मेरी ज़िंदगी के रास्ते में नहीं थी। वही मेरी ज़िंदगी थी।",
        ],
      },
    ],
    fillers: [
      [
        "और तेज़! मैं गिन रही हूँ।",
        "माँ कहती हैं मुझे तुमसे बात नहीं करनी चाहिए। मैं पत्थर से बात कर रही हूँ।",
        "चोटी पर पहुँचो तो मेरी ओर हाथ हिलाना!",
      ],
      [
        "जब घर बहुत सूना हो जाता है, मैं यहाँ आ जाती हूँ।",
        "आज तुम थके लग रहे हो। या शायद मैं हूँ।",
        "बकरियाँ अब तुम्हें पहचानती हैं। वे रास्ता छोड़ देती हैं।",
      ],
      [
        "नीचे आते हुए थोड़ी देर मेरे पास बैठो। कुछ कहना ज़रूरी नहीं।",
        "जहाँ तुम चलते हो, वहाँ रास्ता घिस गया है। नदी की तलहटी जैसा।",
        "तुम्हें देखते-देखते मैं बूढ़ी हो गई। तुम नहीं हुए। अजीब है — मुझे जलन नहीं होती।",
      ],
    ],
    after: [
      "दादी ने कहा था तुम यहाँ मिलोगे। वे हर बात में सही थीं।",
      "आज पत्थर ज़्यादा भारी है, या हल्का?",
      "जब मैं बूढ़ा हो जाऊँगा, तब भी आऊँगा।",
      "मैं रोटी लाया हूँ। इसे चट्टान पर छोड़ जाऊँगा।",
    ],
  },
  es: {
    names: { ione: "Ione", sis: "Sísifo", lykos: "Lykos", pro: "Prometeo", tan: "Tántalo" },
    companions: {
      pro: {
        meetings: [
          [
            "Otro más. Me encadenaron aquí por darles el fuego a los hombres.",
            "Cada día el águila viene por mi hígado. Cada noche vuelve a crecer. ¿Lo ves? Lo mío es peor.",
          ],
          [
            "Empujas como quien intenta alcanzar algo. No mires la cima mientras empujas.",
            "¿Y adónde miro?",
            "A tus manos. Son lo único de todo esto que es tuyo.",
          ],
          ["Dicen que amé a la humanidad. Nunca pedí que me amaran a cambio. Mejor así."],
          ["¿Llevarías mi dolor hasta la cima? Una vez. Quiero saber si allá arriba pesa algo."],
          [
            "Lo llevaste. Por un momento me sentí más ligero; había olvidado lo que era eso.",
            "En tu próxima subida, si tu piedra resbala, mi cadena la detendrá una vez. Un pequeño fuego para ti.",
          ],
          [
            "Tu piedra al menos se mueve. Mis cadenas no se rompen nunca.",
            "Entonces lo tienes más fácil. Solo tienes que esperar.",
            "No, hermano. El tuyo es un castigo de hacer; el mío, de esperar. Hacer es lo más ligero: tú eliges cómo.",
          ],
          [
            "Les di el fuego a los hombres para que vieran en la oscuridad. Tú les enseñas algo mejor: que en ella se puede seguir caminando.",
          ],
        ],
        idle: [
          "Hoy el águila se retrasa. Casi la echo de menos.",
          "Empuja, hermano. Yo miro.",
          "Mira tus manos, no la cima.",
        ],
        declined: "Entonces lo cargaré yo. Siempre lo he hecho.",
        accepted: "Ja. Ya pesa más, ¿verdad? No te detengas.",
      },
      tan: {
        meetings: [
          [
            "Agua hasta la barbilla, fruta sobre la cabeza. Si me agacho, el agua huye. Si alargo la mano, la rama sube.",
            "Para siempre, sediento en un lago. ¿Y tú? ¿Qué es lo que se te niega?",
          ],
          [
            "¿No deseas la cima? Yo deseo esta agua, cada instante.",
            "No deseo la cima. Simplemente llego.",
            "Entonces eres más libre que yo. Desear es mi cadena.",
          ],
          ["¿Me llevas la sed allá arriba? Quizá en la cima se apague."],
          [
            "¿Me dejó allá arriba? …No. Pero por un rato la olvidé.",
            "En tu próxima subida calzaré una piedra detrás de la tuya. Una vez. El deseo me ha enseñado dónde resbalan las cosas.",
          ],
          [
            "Creo que ahora te entiendo. No empujas para llegar.",
            "¿Entonces para qué?",
            "Para empujar. Yo solo alargué la mano para tener. Ese fue mi error, no mi pecado.",
          ],
          ["Si dejara de desear, ¿seguiría huyendo el agua? Pregúntaselo a la cima por mí."],
        ],
        idle: [
          "¿Alguna vez bebiste tu propio sudor? Salado. Pero agua, al fin.",
          "Sigue. Alguien aquí debería moverse.",
          "El higo ya está maduro otra vez. Cómo no.",
        ],
        declined: "Claro. ¿Quién la querría?",
        accepted: "¿La sientes? Así es cada instante, para mí.",
      },
    },
    burdens: {
      pain: { name: "El dolor de Prometeo", desc: "En esta subida la piedra pesa más." },
      thirst: { name: "La sed de Tántalo", desc: "En esta subida el aliento se te acaba antes." },
    },
    passives: {
      fire: { name: "Fuego", desc: "El aliento vuelve más rápido. Para siempre." },
      patience: { name: "Paciencia", desc: "Al resbalar pierdes menos terreno. Para siempre." },
    },
    accept: "Cargarlo",
    decline: "Negarse",
    wedgeHeld: "La cuña aguantó.",
    took: "De la cima:",
    items: {
      stone: "una piedra de lo más alto",
      feather: "una pluma del águila de Zeus",
      answer: "una respuesta",
      sea: "una mirada al mar",
      thyme: "una ramita de tomillo",
      dust: "noticias del hueco",
      silence: "la respuesta del cielo",
      snow: "un puñado de nieve",
      hands: "lo que saben sus manos",
      shard: "un trozo de su piedra",
      light: "la luz de la mañana",
      poppy: "una amapola",
      view: "la vista, en palabras",
      pebble: "una piedra para el niño",
      understanding: "lo que entendió",
    },
    episodes: [
      {
        ask: [
          "¿Eres el hombre que engañó a la Muerte? La abuela dice que la encadenaste en tu sótano.",
          "Tu abuela habla demasiado. Apártate, niña.",
          "Tráeme una piedra de lo más alto. Quiero ver si es distinta.",
        ],
        give: ["Toma. De la cima.", "Es… solo una piedra.", "Allá arriba todo es solo una piedra."],
      },
      {
        ask: ["¿Es verdad que los dioses te miran todo el día? Tráeme algo suyo."],
        give: [
          "Una pluma de su águila. Se le cayó mientras se reía de mí.",
          "Está tibia. ¿Los dioses tienen frío alguna vez?",
          "No sienten nada. Por eso pueden hacer esto.",
        ],
      },
      {
        ask: [
          "¿Por qué no paras y ya? Déjala y baja conmigo.",
          "Si paro, ganan ellos.",
          "¿Ganan qué?",
          "Piénsalo en la cima. Me lo dices al bajar.",
        ],
        give: [
          "Parar sería darles la razón. Nunca se la daré.",
          "Papá dice eso de los recaudadores. Está muy cansado.",
        ],
      },
      {
        ask: ["¿Se ve el mar desde allá arriba? Nunca he visto el mar."],
        give: ["Ahí está. Gris hoy, y muy lejos.", "Entonces lo recordaré gris."],
      },
      {
        ask: ["Mañana me voy a la ciudad con mamá. ¿Me traes algo para el camino?"],
        give: [
          "Tomillo, de la cima. Crece donde nada debería crecer.",
          "Volveré, te lo prometo. Sigue empujando.",
          "¿Adónde más iba a ir?",
        ],
      },
      {
        ask: [
          "Sísifo. ¿Te acuerdas de mí? Era la niña que quería una piedra.",
          "Eras pequeña.",
          "Doce años. No has cambiado, ni siquiera las sandalias. ¿Sigue ahí el hueco de donde sacaste mi piedra?",
        ],
        give: [
          "El polvo lo llenó. Nadie diría que allí hubo nunca una piedra.",
          "Entonces nada señala que estuvimos aquí. Ni siquiera eso.",
        ],
      },
      {
        ask: [
          "Mi marido murió este invierno. Todos me dicen que tuvo un sentido.",
          "Tú estás más cerca del cielo que yo. Pregunta a los dioses por mí: ¿para qué fue?",
        ],
        give: [
          "Pregunté. El cielo no dijo nada. A mí tampoco me dice nada.",
          "Al menos es honesto.",
        ],
      },
      {
        ask: [
          "Hoy conté tus subidas. Treinta y una. ¿Tú no las cuentas?",
          "Lo dejé. El número era lo único que crecía.",
          "Mi hijo nunca ha visto la nieve. ¿Le traes un poco de la cima?",
        ],
        give: [
          "Se derritió al bajar. Siempre se derrite.",
          "Entonces cuéntale cómo era. Es lo único que cualquiera de nosotros trae de vuelta.",
        ],
      },
      {
        ask: [
          "¿Por qué sigues empujando, si nada importa?",
          "Ya no lo sé.",
          "Descúbrelo allá arriba. Te espero.",
        ],
        give: [
          "En la cima solo hay viento, y el camino de vuelta. Pero mis manos supieron qué hacer todo el trayecto.",
          "Quizá eso es algo. Quizá es lo único.",
        ],
      },
      {
        ask: [
          "Mi hijo quiere subir contigo algún día. Le dije que es un castigo.",
          "Tráele algo de allá arriba. Lo que sea.",
        ],
        give: [
          "Un pedazo de mi piedra. Lo arrancó el rayo del dios.",
          "¿Regalarías tu propia carga?",
          "Sigue pesando. Tómala igual.",
        ],
      },
      {
        ask: [
          "Ahora me duelen las rodillas en este sendero. Tú lo subes cien veces al día y nunca te quejas.",
          "Antes sí. A cada paso.",
          "¿Cuándo dejaste de hacerlo?",
          "No me di cuenta.",
          "Entonces tráeme la luz de la mañana de allá arriba. Nunca llega a mi casa.",
        ],
        give: ["Bajó conmigo. Mira: está en tus manos.", "Es verdad."],
      },
      {
        ask: [
          "¿Todavía los odias? ¿A los dioses?",
          "Odiar es mirar hacia arriba. Yo he estado mirando el camino.",
          "Si ahora crece algo allá arriba, tráeme una flor.",
        ],
        give: [
          "Una amapola. Crecen por donde ha pasado la piedra.",
          "Tu castigo está haciendo un jardín.",
        ],
      },
      {
        ask: [
          "Pronto me iré. ¿Te acordarás de mí?",
          "Recuerdo cada piedra de esta colina. Tú nunca fuiste una piedra.",
          "Entonces dime qué ves desde la cima. Hoy. Exactamente.",
        ],
        give: [
          "El mar. La ciudad. Tu tejado, y el humo de tu cocina. Y el largo camino de bajada, que es mío.",
          "Suenas casi feliz.",
          "¿Ah, sí?",
        ],
      },
      {
        ask: [
          "Este es mi nieto. Saluda, Lykos.",
          "¿Eres el hombre que engañó a la Muerte?",
          "Su abuela habla demasiado.",
          "Tráele una piedra de lo más alto.",
        ],
        give: ["Toma. De la cima.", "Es solo una piedra.", "Sí. ¿No es maravilloso?"],
      },
      {
        ask: [
          "La abuela murió en primavera.",
          "Me pidió que te dijera: “Lo entenderá en la cima”.",
        ],
        give: [
          "¿Qué entendiste?",
          "Que la subida nunca estuvo en el camino de mi vida. Era mi vida.",
        ],
      },
    ],
    fillers: [
      [
        "¡Más rápido! Estoy contando.",
        "Mamá dice que no debo hablar contigo. Estoy hablando con la piedra.",
        "¡Cuando llegues arriba, salúdame!",
      ],
      [
        "Vengo aquí cuando la casa está demasiado callada.",
        "Hoy pareces cansado. O quizá sea yo.",
        "Las cabras ya te conocen. Se apartan.",
      ],
      [
        "Siéntate un rato conmigo al bajar. No tienes que decir nada.",
        "El sendero está gastado por donde pisas. Como el lecho de un río.",
        "Envejecí mirándote. Tú no. Qué raro: no te envidio.",
      ],
    ],
    after: [
      "La abuela dijo que estarías aquí. Tenía razón en todo.",
      "¿Hoy la piedra pesa más, o menos?",
      "Cuando sea viejo, seguiré viniendo.",
      "Traje pan. Lo dejo en la roca.",
    ],
  },
  fr: {
    names: { ione: "Ioné", sis: "Sisyphe", lykos: "Lykos", pro: "Prométhée", tan: "Tantale" },
    companions: {
      pro: {
        meetings: [
          [
            "Encore un. On m'a enchaîné ici pour avoir donné le feu aux hommes.",
            "Chaque jour, l'aigle vient pour mon foie. Chaque nuit, il repousse. Tu vois ? Le mien est pire.",
          ],
          [
            "Tu pousses comme quelqu'un qui veut atteindre quelque chose. Ne regarde pas le sommet en poussant.",
            "Où dois-je regarder ?",
            "Tes mains. C'est la seule part de tout ceci qui t'appartienne.",
          ],
          [
            "On dit que j'ai aimé les hommes. Je n'ai jamais demandé à être aimé en retour. Tant mieux.",
          ],
          [
            "Porterais-tu ma douleur jusqu'en haut ? Une fois. Je veux savoir si elle pèse quelque chose, là-haut.",
          ],
          [
            "Tu l'as portée. Un instant, je me suis senti plus léger — j'avais oublié ce que c'était.",
            "À ta prochaine montée, si ta pierre glisse, ma chaîne la retiendra, une fois. Un petit feu pour toi.",
          ],
          [
            "Ta pierre, au moins, elle bouge. Mes chaînes ne cèdent jamais.",
            "Alors c'est plus facile pour toi. Tu n'as qu'à attendre.",
            "Non, frère. Ta peine est de faire ; la mienne, d'attendre. Faire est la plus légère — tu choisis comment.",
          ],
          [
            "J'ai donné le feu aux hommes pour qu'ils voient dans le noir. Tu leur montres mieux : qu'on peut continuer d'y marcher.",
          ],
        ],
        idle: [
          "L'aigle est en retard aujourd'hui. Il me manquerait presque.",
          "Pousse, frère. Je regarde.",
          "Regarde tes mains, pas le sommet.",
        ],
        declined: "Alors je la porterai moi-même. Je l'ai toujours fait.",
        accepted: "Ha. Déjà plus lourd, hein ? Ne t'arrête pas.",
      },
      tan: {
        meetings: [
          [
            "De l'eau jusqu'au menton, des fruits au-dessus de ma tête. Quand je me penche, l'eau s'enfuit. Quand je tends la main, la branche s'élève.",
            "À jamais, assoiffé dans un lac. Et toi ? Qu'est-ce qu'on te refuse ?",
          ],
          [
            "Tu ne veux pas le sommet ? Moi, je veux cette eau, à chaque instant.",
            "Je ne veux pas le sommet. J'y arrive, c'est tout.",
            "Alors tu es plus libre que moi. Désirer, c'est ma chaîne.",
          ],
          ["Tu porterais ma soif là-haut, pour moi ? Peut-être qu'au sommet elle s'apaise."],
          [
            "M'a-t-elle quitté, là-haut ? …Non. Mais un moment, je l'ai oubliée.",
            "À ta prochaine montée, je calerai une pierre derrière la tienne. Une fois. Le désir m'a appris où les choses glissent.",
          ],
          [
            "Je crois que je te comprends, maintenant. Tu ne pousses pas pour arriver.",
            "Alors pourquoi ?",
            "Pour pousser. Moi, je n'ai jamais tendu la main que pour avoir. C'était mon erreur, pas mon crime.",
          ],
          ["Si je cessais de désirer, l'eau fuirait-elle encore ? Demande au sommet, pour moi."],
        ],
        idle: [
          "Tu as déjà bu ta propre sueur ? Salée. Mais de l'eau quand même.",
          "Va. Il faut bien que quelqu'un bouge, ici.",
          "La figue est mûre à nouveau. Évidemment.",
        ],
        declined: "Bien sûr. Qui en voudrait ?",
        accepted: "Tu la sens ? C'est ça, chaque instant, pour moi.",
      },
    },
    burdens: {
      pain: {
        name: "La douleur de Prométhée",
        desc: "La pierre est plus lourde pour cette montée.",
      },
      thirst: {
        name: "La soif de Tantale",
        desc: "Ton souffle s'épuise plus vite pour cette montée.",
      },
    },
    passives: {
      fire: { name: "Feu", desc: "Ton souffle revient plus vite. Pour de bon." },
      patience: {
        name: "Patience",
        desc: "Une glissade te fait perdre moins de terrain. Pour de bon.",
      },
    },
    accept: "Porter",
    decline: "Refuser",
    wedgeHeld: "La cale a tenu.",
    took: "Du sommet :",
    items: {
      stone: "une pierre du sommet même",
      feather: "une plume de l'aigle de Zeus",
      answer: "une réponse",
      sea: "un regard sur la mer",
      thyme: "un brin de thym",
      dust: "des nouvelles du creux",
      silence: "la réponse du ciel",
      snow: "une poignée de neige",
      hands: "ce que savent ses mains",
      shard: "un éclat de sa pierre",
      light: "la lumière du matin",
      poppy: "un coquelicot",
      view: "la vue, en mots",
      pebble: "une pierre pour le garçon",
      understanding: "ce qu'il a compris",
    },
    episodes: [
      {
        ask: [
          "C'est toi, l'homme qui a trompé la Mort ? Grand-mère dit que tu l'as enchaînée dans ta cave.",
          "Ta grand-mère parle trop. Pousse-toi, petite.",
          "Rapporte-moi une pierre du sommet même. Je veux voir si elle est différente.",
        ],
        give: [
          "Tiens. Du sommet.",
          "C'est… juste une pierre.",
          "Là-haut, tout n'est qu'une pierre.",
        ],
      },
      {
        ask: [
          "C'est vrai que les dieux te regardent toute la journée ? Rapporte-moi quelque chose à eux.",
        ],
        give: [
          "Une plume de son aigle. Il l'a laissée tomber en se moquant de moi.",
          "Elle est chaude. Est-ce que les dieux ont froid, parfois ?",
          "Ils ne sentent rien. C'est pour ça qu'ils peuvent faire ça.",
        ],
      },
      {
        ask: [
          "Pourquoi tu ne t'arrêtes pas, tout simplement ? Laisse-la et descends avec moi.",
          "Si je m'arrête, ils gagnent.",
          "Ils gagnent quoi ?",
          "Penses-y au sommet. Tu me diras en redescendant.",
        ],
        give: [
          "M'arrêter, ce serait leur donner raison. Je ne leur donnerai jamais raison.",
          "Papa dit ça des percepteurs. Il est très fatigué.",
        ],
      },
      {
        ask: ["On voit la mer, de là-haut ? Je n'ai jamais vu la mer."],
        give: ["Elle est là. Grise aujourd'hui, et très loin.", "Alors je m'en souviendrai grise."],
      },
      {
        ask: [
          "Demain je pars à la ville avec Maman. Tu me rapportes quelque chose pour la route ?",
        ],
        give: [
          "Du thym, du sommet. Il pousse là où rien ne devrait pousser.",
          "Je reviendrai, promis. Continue de pousser.",
          "Où irais-je ailleurs ?",
        ],
      },
      {
        ask: [
          "Sisyphe. Tu te souviens de moi ? J'étais la fille qui voulait une pierre.",
          "Tu étais petite.",
          "Douze ans. Tu n'as pas changé — pas même tes sandales. Le creux d'où tu as pris ma pierre, il est encore là ?",
        ],
        give: [
          "La poussière l'a comblé. Personne ne devinerait qu'il y a eu une pierre.",
          "Alors rien ne montre que nous sommes passés ici. Pas même ça.",
        ],
      },
      {
        ask: [
          "Mon mari est mort cet hiver. Tout le monde me dit que cela avait un sens.",
          "Tu es plus près du ciel que moi. Demande aux dieux, pour moi : c'était pour quoi ?",
        ],
        give: [
          "J'ai demandé. Le ciel n'a rien dit. À moi non plus, il ne dit rien.",
          "Au moins, il est honnête.",
        ],
      },
      {
        ask: [
          "J'ai compté tes montées, aujourd'hui. Trente et une. Tu ne comptes pas ?",
          "J'ai arrêté. Le nombre était la seule chose qui grandissait.",
          "Mon fils n'a jamais vu la neige. Tu lui en rapportes un peu du sommet ?",
        ],
        give: [
          "Elle a fondu en descendant. Elle fond toujours.",
          "Alors raconte-lui comment c'était. C'est tout ce que chacun de nous rapporte jamais.",
        ],
      },
      {
        ask: [
          "Pourquoi pousses-tu encore, si rien de tout cela ne compte ?",
          "Je ne sais plus.",
          "Trouve là-haut. J'attendrai.",
        ],
        give: [
          "Au sommet, il n'y a que le vent, et le chemin du retour. Mais mes mains savaient quoi faire, tout du long.",
          "C'est peut-être quelque chose. C'est peut-être la seule chose.",
        ],
      },
      {
        ask: [
          "Mon fils veut monter avec toi, un jour. Je lui ai dit que c'est une punition.",
          "Rapporte-lui quelque chose de là-haut. N'importe quoi.",
        ],
        give: [
          "Un morceau de ma pierre. La foudre du dieu l'a arraché.",
          "Tu donnerais ton propre fardeau ?",
          "Il pèse toujours. Prends-le quand même.",
        ],
      },
      {
        ask: [
          "Mes genoux me font mal sur ce sentier, maintenant. Toi, tu le montes cent fois par jour sans jamais te plaindre.",
          "Avant, si. À chaque pas.",
          "Quand as-tu arrêté ?",
          "Je ne l'ai pas remarqué.",
          "Alors rapporte-moi la lumière du matin, de là-haut. Elle n'atteint jamais ma maison.",
        ],
        give: ["Elle est descendue avec moi. Regarde — elle est sur tes mains.", "C'est vrai."],
      },
      {
        ask: [
          "Tu les hais encore ? Les dieux ?",
          "Haïr, c'est regarder en haut. Moi, je regardais le chemin.",
          "Si quelque chose pousse là-haut maintenant, rapporte-moi une fleur.",
        ],
        give: [
          "Un coquelicot. Il en pousse partout où la pierre est passée.",
          "Ta punition fait un jardin.",
        ],
      },
      {
        ask: [
          "Je m'en irai bientôt. Te souviendras-tu de moi ?",
          "Je me souviens de chaque pierre de cette colline. Tu n'as jamais été une pierre.",
          "Alors dis-moi ce que tu vois du sommet. Aujourd'hui. Exactement.",
        ],
        give: [
          "La mer. La ville. Ton toit, et la fumée de ta cuisine. Et le long chemin du retour, qui est le mien.",
          "Tu as l'air presque heureux.",
          "Vraiment ?",
        ],
      },
      {
        ask: [
          "Voici mon petit-fils. Dis bonjour, Lykos.",
          "C'est toi, l'homme qui a trompé la Mort ?",
          "Sa grand-mère parle trop.",
          "Rapporte-lui une pierre du sommet même.",
        ],
        give: ["Tiens. Du sommet.", "C'est juste une pierre.", "Oui. N'est-ce pas merveilleux ?"],
      },
      {
        ask: [
          "Grand-mère est morte au printemps.",
          "Elle m'a dit de te dire : « Il comprendra au sommet. »",
        ],
        give: [
          "Qu'as-tu compris ?",
          "Que la montée n'a jamais barré le chemin de ma vie. C'était ma vie.",
        ],
      },
    ],
    fillers: [
      [
        "Plus vite ! Je compte.",
        "Maman dit que je ne dois pas te parler. Je parle à la pierre.",
        "Quand tu arrives en haut, fais-moi signe !",
      ],
      [
        "Je viens ici quand la maison est trop silencieuse.",
        "Tu as l'air fatigué aujourd'hui. Ou c'est peut-être moi.",
        "Les chèvres te connaissent, maintenant. Elles s'écartent.",
      ],
      [
        "Assieds-toi un peu avec moi en descendant. Tu n'as rien à dire.",
        "Le sentier est usé là où tu marches. Comme le lit d'une rivière.",
        "J'ai vieilli en te regardant. Pas toi. C'est étrange — je ne suis pas jalouse.",
      ],
    ],
    after: [
      "Grand-mère disait que tu serais là. Elle avait raison sur tout.",
      "La pierre est-elle plus lourde aujourd'hui, ou plus légère ?",
      "Quand je serai vieux, je viendrai encore.",
      "J'ai apporté du pain. Je le laisse sur le rocher.",
    ],
  },
  ar: {
    names: { ione: "إيوني", sis: "سيزيف", lykos: "ليكوس", pro: "بروميثيوس", tan: "تانتالوس" },
    companions: {
      pro: {
        meetings: [
          [
            "واحد آخر. قيّدوني هنا لأنني أعطيت البشر النار.",
            "كل يوم يأتي النسر من أجل كبدي. وكل ليلة تنمو من جديد. أترى؟ عقابي أسوأ.",
          ],
          [
            "تدفع كمن يحاول أن يبلغ شيئًا. لا تنظر إلى القمة وأنت تدفع.",
            "فإلى أين أنظر؟",
            "إلى يديك. هما الشيء الوحيد في هذا كله الذي هو لك.",
          ],
          ["يقولون إنني أحببت البشر. لم أطلب قط أن يحبوني في المقابل. وهذا أفضل."],
          ["هل تحمل ألمي إلى القمة؟ مرة واحدة. أريد أن أعرف إن كان له وزن هناك."],
          [
            "حملته. للحظة شعرت بخفة — كنت قد نسيت ما هي.",
            "في صعودك القادم، إن انزلقت صخرتك، ستمسكها سلسلتي مرة واحدة. نار صغيرة لك.",
          ],
          [
            "صخرتك على الأقل تتحرك. سلاسلي لا تنكسر أبدًا.",
            "إذن أمرك أهون. ليس عليك إلا أن تنتظر.",
            "لا يا أخي. عقابك عقاب فعل، وعقابي عقاب انتظار. الفعل هو الأخف — أنت تختار كيف.",
          ],
          [
            "أعطيت البشر النار ليروا في الظلام. وأنت تريهم ما هو أفضل: أن المرء يستطيع أن يواصل المشي فيه.",
          ],
        ],
        idle: [
          "تأخر النسر اليوم. كدت أشتاق إليه.",
          "ادفع يا أخي. أنا أراقب.",
          "انظر إلى يديك، لا إلى القمة.",
        ],
        declined: "إذن سأحمله بنفسي. لطالما فعلت.",
        accepted: "ها. صار أثقل بالفعل، أليس كذلك؟ لا تتوقف.",
      },
      tan: {
        meetings: [
          [
            "الماء حتى ذقني، والثمر فوق رأسي. إن انحنيت هرب الماء. وإن مددت يدي ارتفع الغصن.",
            "إلى الأبد، عطشان في بحيرة. وأنت؟ ما الذي حُرمت منه؟",
          ],
          [
            "ألا تريد القمة؟ أنا أريد هذا الماء، في كل لحظة.",
            "لا أريد القمة. أنا فقط أصل إليها.",
            "إذن أنت أحرّ مني. الرغبة هي سلسلتي.",
          ],
          ["هل تحمل عطشي إلى الأعلى من أجلي؟ لعله يرتوي عند القمة."],
          [
            "هل فارقني هناك؟ …لا. لكنني نسيته لبعض الوقت.",
            "في صعودك القادم سأسند صخرتك بحجر من خلفها. مرة واحدة. علّمتني الرغبة أين تنزلق الأشياء.",
          ],
          [
            "أظنني أفهمك الآن. أنت لا تدفع لكي تصل.",
            "فلماذا إذن؟",
            "لكي تدفع. أما أنا فلم أمدّ يدي إلا لأملك. كان ذلك خطئي، لا خطيئتي.",
          ],
          ["لو كففت عن الرغبة، هل سيظل الماء يهرب؟ اسأل القمة عني."],
        ],
        idle: [
          "هل شربت عرقك يومًا؟ مالح. لكنه ماء.",
          "امضِ. لا بد أن يتحرك أحد هنا.",
          "نضج التين مرة أخرى. بالطبع.",
        ],
        declined: "بالطبع. من ذا الذي يريده؟",
        accepted: "أتشعر به؟ هكذا هي كل لحظة عندي.",
      },
    },
    burdens: {
      pain: { name: "ألم بروميثيوس", desc: "الصخرة أثقل في هذا الصعود." },
      thirst: { name: "عطش تانتالوس", desc: "ينفد نَفَسك أسرع في هذا الصعود." },
    },
    passives: {
      fire: { name: "النار", desc: "يعود نَفَسك أسرع. إلى الأبد." },
      patience: { name: "الصبر", desc: "الانزلاق يُرجعك مسافة أقل. إلى الأبد." },
    },
    accept: "احمله",
    decline: "ارفض",
    wedgeHeld: "صمد الإسفين.",
    took: "من القمة:",
    items: {
      stone: "حجر من أعلى القمة",
      feather: "ريشة من نسر زيوس",
      answer: "جواب",
      sea: "نظرة إلى البحر",
      thyme: "غصن زعتر",
      dust: "خبر عن الحفرة",
      silence: "جواب السماء",
      snow: "حفنة من الثلج",
      hands: "ما تعرفه يداه",
      shard: "شظية من صخرته",
      light: "ضوء الصباح",
      poppy: "شقائق النعمان",
      view: "المنظر، بالكلمات",
      pebble: "حجر للصبي",
      understanding: "ما فهمه",
    },
    episodes: [
      {
        ask: [
          "هل أنت الرجل الذي خدع الموت؟ جدتي تقول إنك قيّدته في قبوك.",
          "جدتك تتكلم كثيرًا. ابتعدي عن الطريق يا صغيرة.",
          "أحضر لي حجرًا من أعلى القمة. أريد أن أرى إن كان مختلفًا.",
        ],
        give: ["خذي. من القمة.", "إنه… مجرد حجر.", "كل شيء هناك مجرد حجر."],
      },
      {
        ask: ["أصحيح أن الآلهة تراقبك طوال اليوم؟ أحضر لي شيئًا من أشيائهم."],
        give: [
          "ريشة من نسره. أسقطها وهو يضحك عليّ.",
          "إنها دافئة. هل تشعر الآلهة بالبرد أحيانًا؟",
          "لا يشعرون بشيء. لهذا يقدرون على فعل هذا.",
        ],
      },
      {
        ask: [
          "لماذا لا تتوقف ببساطة؟ اتركها وانزل معي.",
          "إن توقفت، فازوا.",
          "فازوا بماذا؟",
          "فكّر في الأمر عند القمة. وأخبرني في طريق النزول.",
        ],
        give: [
          "التوقف يعني أن أوافقهم. ولن أوافقهم أبدًا.",
          "أبي يقول هذا عن جباة الضرائب. إنه متعب جدًا.",
        ],
      },
      {
        ask: ["هل ترى البحر من هناك؟ لم أرَ البحر قط."],
        give: ["إنه هناك. رمادي اليوم، وبعيد جدًا.", "إذن سأتذكره رماديًا."],
      },
      {
        ask: ["غدًا أذهب إلى المدينة مع أمي. هل تحضر لي شيئًا للطريق؟"],
        give: [
          "زعتر، من القمة. ينمو حيث لا ينبغي أن ينمو شيء.",
          "سأعود، أعدك. واصل الدفع.",
          "وإلى أين عساي أذهب؟",
        ],
      },
      {
        ask: [
          "سيزيف. أتذكرني؟ كنت الفتاة التي أرادت حجرًا.",
          "كنتِ صغيرة.",
          "اثنا عشر عامًا. لم تتغير — ولا حتى صندلك. هل ما زالت الحفرة هناك، حيث أخذت حجري؟",
        ],
        give: [
          "ملأها الغبار. لا أحد يستطيع أن يعرف أن حجرًا كان هناك يومًا.",
          "إذن لا شيء يدل على أننا كنا هنا. ولا حتى ذلك.",
        ],
      },
      {
        ask: [
          "مات زوجي هذا الشتاء. الجميع يقولون لي إن لذلك معنى.",
          "أنت أقرب إلى السماء مني. اسأل الآلهة عني — لأي شيء كان ذلك؟",
        ],
        give: ["سألت. لم تقل السماء شيئًا. ولا تقول لي أنا أيضًا شيئًا.", "على الأقل هي صادقة."],
      },
      {
        ask: [
          "عددت صعوداتك اليوم. واحد وثلاثون. ألا تعدّ أنت؟",
          "توقفت. كان الرقم الشيء الوحيد الذي يكبر.",
          "ابني لم يرَ الثلج قط. هل تحضر له بعضه من القمة؟",
        ],
        give: [
          "ذاب في طريق النزول. دائمًا يذوب.",
          "إذن صِف له كيف كان. هذا كل ما يعود به أيّ منا.",
        ],
      },
      {
        ask: [
          "لماذا ما زلت تدفع، إن كان لا شيء من هذا يهم؟",
          "لم أعد أعرف.",
          "اكتشف ذلك هناك. سأنتظر.",
        ],
        give: [
          "عند القمة لا يوجد إلا الريح، وطريق العودة. لكن يديّ عرفتا ما تفعلان طوال الطريق.",
          "ربما هذا شيء. ربما هو الشيء الوحيد.",
        ],
      },
      {
        ask: ["ابني يريد أن يصعد معك يومًا ما. قلت له إنه عقاب.", "أحضر له شيئًا من هناك. أي شيء."],
        give: [
          "قطعة من صخرتي. كسرتها صاعقة الإله.",
          "أتعطي عبئك أنت؟",
          "ما زالت ثقيلة. خذيها على أي حال.",
        ],
      },
      {
        ask: [
          "ركبتاي تؤلمانني على هذا الدرب الآن. وأنت تصعده مئة مرة في اليوم ولا تشكو أبدًا.",
          "كنت أشكو. مع كل خطوة.",
          "متى توقفت؟",
          "لم أنتبه.",
          "إذن أحضر لي ضوء الصباح من هناك. إنه لا يبلغ بيتي أبدًا.",
        ],
        give: ["نزل معي. انظري — إنه على يديك.", "صحيح."],
      },
      {
        ask: [
          "هل ما زلت تكرههم؟ الآلهة؟",
          "الكره يعني أن تنظر إلى الأعلى. أما أنا فكنت أنظر إلى الدرب.",
          "إن كان شيء ينبت هناك الآن، فأحضر لي زهرة.",
        ],
        give: ["شقائق النعمان. تنبت حيثما مرّت الصخرة.", "عقابك يصنع حديقة."],
      },
      {
        ask: [
          "سأرحل قريبًا. هل ستتذكرني؟",
          "أتذكر كل حجر على هذا التل. وأنتِ لم تكوني حجرًا قط.",
          "إذن قل لي ماذا ترى من القمة. اليوم. بالضبط.",
        ],
        give: [
          "البحر. المدينة. سقف بيتك، والدخان من مطبخك. والطريق الطويل إلى الأسفل، وهو لي.",
          "تبدو سعيدًا تقريبًا.",
          "حقًا؟",
        ],
      },
      {
        ask: [
          "هذا حفيدي. قل مرحبًا يا ليكوس.",
          "هل أنت الرجل الذي خدع الموت؟",
          "جدته تتكلم كثيرًا.",
          "أحضر له حجرًا من أعلى القمة.",
        ],
        give: ["خذ. من القمة.", "إنه مجرد حجر.", "نعم. أليس رائعًا؟"],
      },
      {
        ask: ["ماتت جدتي في الربيع.", "طلبت مني أن أقول لك: “سيفهم عند القمة.”"],
        give: ["ماذا فهمت؟", "أن الصعود لم يكن يومًا عائقًا في طريق حياتي. كان هو حياتي."],
      },
    ],
    fillers: [
      [
        "أسرع! أنا أعدّ.",
        "أمي تقول إن عليّ ألا أكلمك. أنا أكلم الحجر.",
        "حين تصل إلى القمة، لوّح لي!",
      ],
      [
        "آتي إلى هنا حين يصير البيت صامتًا أكثر مما ينبغي.",
        "تبدو متعبًا اليوم. أو لعلني أنا المتعبة.",
        "الماعز صارت تعرفك. تتنحى عن طريقك.",
      ],
      [
        "اجلس معي قليلًا وأنت نازل. لا داعي لأن تقول شيئًا.",
        "الدرب بالٍ حيث تمشي. كمجرى نهر.",
        "شِخت وأنا أراقبك. وأنت لم تشخ. غريب — لا أشعر بالغيرة.",
      ],
    ],
    after: [
      "قالت جدتي إنك ستكون هنا. كانت محقة في كل شيء.",
      "هل الصخرة أثقل اليوم، أم أخف؟",
      "حين أكبر، سأظل آتي.",
      "أحضرت خبزًا. سأتركه على الصخرة.",
    ],
  },
  bn: {
    names: {
      ione: "আয়োনি",
      sis: "সিসিফাস",
      lykos: "লাইকোস",
      pro: "প্রমিথিউস",
      tan: "ট্যান্টালাস",
    },
    companions: {
      pro: {
        meetings: [
          [
            "আরেকজন। মানুষকে আগুন দিয়েছিলাম বলে ওরা আমাকে এখানে শিকলে বেঁধেছে।",
            "রোজ ঈগলটা আসে আমার যকৃৎ খেতে। রোজ রাতে সেটা আবার গজায়। দেখছ? আমারটাই বেশি খারাপ।",
          ],
          [
            "তুমি এমনভাবে ঠেলছ যেন কিছু একটা ছুঁতে চাও। ঠেলার সময় চূড়ার দিকে তাকিয়ো না।",
            "তাহলে কোথায় তাকাব?",
            "তোমার হাতের দিকে। এই সবকিছুর মধ্যে শুধু ওরাই তোমার নিজের।",
          ],
          ["লোকে বলে আমি মানুষকে ভালোবাসতাম। বদলে ভালোবাসা কখনো চাইনি। ভালোই হয়েছে।"],
          [
            "আমার যন্ত্রণাটা চূড়া পর্যন্ত বয়ে নেবে? একবার। জানতে চাই, ওখানে ওপরে ওর কোনো ভার আছে কি না।",
          ],
          [
            "তুমি বয়ে নিয়েছ। এক মুহূর্তের জন্য হালকা লাগল — ভুলেই গিয়েছিলাম সেটা কেমন।",
            "পরের বার ওঠার সময় তোমার পাথর পিছলে গেলে আমার শিকল একবার সেটা আটকাবে। তোমার জন্য একটুখানি আগুন।",
          ],
          [
            "তোমার পাথর অন্তত নড়ে। আমার শিকল কখনো ছেঁড়ে না।",
            "তাহলে তোমারটা সহজ। তোমাকে শুধু অপেক্ষা করতে হয়।",
            "না, ভাই। তোমার শাস্তি করার; আমার, অপেক্ষার। করাটাই হালকা — কীভাবে করবে, তা তুমি বেছে নিতে পারো।",
          ],
          [
            "মানুষকে আগুন দিয়েছিলাম যাতে তারা অন্ধকারে দেখতে পায়। তুমি তাদের আরও ভালো কিছু দেখাচ্ছ: অন্ধকারেও হেঁটে চলা যায়।",
          ],
        ],
        idle: [
          "আজ ঈগলটা দেরি করছে। প্রায় ওর অভাব বোধ করছি।",
          "ঠেলো, ভাই। আমি দেখছি।",
          "হাতের দিকে তাকাও, চূড়ার দিকে নয়।",
        ],
        declined: "তাহলে আমি নিজেই বইব। বরাবর তা-ই করেছি।",
        accepted: "হা। এখনই ভারী লাগছে, তাই না? থেমো না।",
      },
      tan: {
        meetings: [
          [
            "থুতনি পর্যন্ত জল, মাথার ওপরে ফল। ঝুঁকলে জল সরে যায়। হাত বাড়ালে ডাল উঠে যায়।",
            "চিরকাল, হ্রদের মধ্যে তৃষ্ণার্ত। আর তুমি? তোমাকে কী থেকে বঞ্চিত করা হয়েছে?",
          ],
          [
            "তুমি চূড়াটা চাও না? আমি এই জল চাই, প্রতি মুহূর্তে।",
            "আমি চূড়া চাই না। আমি শুধু সেখানে পৌঁছে যাই।",
            "তাহলে তুমি আমার চেয়ে মুক্ত। চাওয়াটাই আমার শিকল।",
          ],
          ["আমার তৃষ্ণাটা আমার হয়ে ওপরে নিয়ে যাবে? হয়তো চূড়ায় গিয়ে মিটবে।"],
          [
            "ওটা কি আমাকে ওখানে ছেড়ে গেল? …না। তবে কিছুক্ষণ ভুলে ছিলাম।",
            "পরের বার ওঠার সময় তোমার পাথরের পেছনে একটা পাথর গুঁজে দেব। একবার। চাওয়া আমাকে শিখিয়েছে জিনিস কোথায় পিছলায়।",
          ],
          [
            "মনে হয় এখন তোমাকে বুঝি। তুমি পৌঁছানোর জন্য ঠেলো না।",
            "তাহলে কীসের জন্য?",
            "ঠেলার জন্য। আমি সবসময় হাত বাড়িয়েছি পাওয়ার জন্য। সেটা আমার ভুল ছিল, পাপ নয়।",
          ],
          ["আমি যদি চাওয়া থামাই, জল কি তবুও সরে যাবে? আমার হয়ে চূড়াকে জিজ্ঞেস কোরো।"],
        ],
        idle: [
          "কখনো নিজের ঘাম খেয়েছ? নোনতা। তবু জল তো।",
          "চলো। এখানে কাউকে তো নড়তে হবে।",
          "ডুমুরটা আবার পেকেছে। তা তো পাকবেই।",
        ],
        declined: "তা তো বটেই। কে-ই বা চাইবে?",
        accepted: "টের পাচ্ছ? আমার কাছে প্রতিটা মুহূর্ত এমনই।",
      },
    },
    burdens: {
      pain: { name: "প্রমিথিউসের যন্ত্রণা", desc: "এবারের চড়াইয়ে পাথরটা বেশি ভারী।" },
      thirst: { name: "ট্যান্টালাসের তৃষ্ণা", desc: "এবারের চড়াইয়ে তোমার দম তাড়াতাড়ি ফুরোয়।" },
    },
    passives: {
      fire: { name: "আগুন", desc: "তোমার দম তাড়াতাড়ি ফেরে। চিরকালের জন্য।" },
      patience: { name: "ধৈর্য", desc: "পিছলালে কম জমি হারাও। চিরকালের জন্য।" },
    },
    accept: "বয়ে নাও",
    decline: "ফিরিয়ে দাও",
    wedgeHeld: "গোঁজটা টিকে গেল।",
    took: "চূড়া থেকে:",
    items: {
      stone: "একেবারে চূড়ার একটা পাথর",
      feather: "জিউসের ঈগলের একটা পালক",
      answer: "একটা উত্তর",
      sea: "সমুদ্রের দিকে এক নজর",
      thyme: "এক ডাল থাইম",
      dust: "গর্তটার খবর",
      silence: "আকাশের উত্তর",
      snow: "এক মুঠো বরফ",
      hands: "তার হাত যা জানে",
      shard: "তার পাথরের একটা টুকরো",
      light: "সকালের আলো",
      poppy: "একটা পপি ফুল",
      view: "দৃশ্যটা, কথায়",
      pebble: "ছেলেটার জন্য একটা পাথর",
      understanding: "সে যা বুঝেছে",
    },
    episodes: [
      {
        ask: [
          "তুমিই কি সেই লোক যে মৃত্যুকে ঠকিয়েছিল? দিদিমা বলে তুমি তাকে তোমার মাটির ঘরে শিকলে বেঁধেছিলে।",
          "তোমার দিদিমা বড্ড বেশি কথা বলে। পথ ছাড়ো, খুকি।",
          "আমার জন্য একেবারে চূড়া থেকে একটা পাথর এনো। দেখতে চাই সেটা আলাদা কি না।",
        ],
        give: [
          "এই নাও। চূড়া থেকে।",
          "এটা… শুধুই একটা পাথর।",
          "ওখানে ওপরে সবকিছুই শুধু একটা পাথর।",
        ],
      },
      {
        ask: ["সত্যি কি দেবতারা সারাদিন তোমাকে দেখে? আমার জন্য ওদের কিছু একটা এনো।"],
        give: [
          "ওর ঈগলের একটা পালক। আমাকে নিয়ে হাসতে গিয়ে ফেলে দিয়েছে।",
          "এটা গরম। দেবতাদের কি কখনো শীত করে?",
          "ওরা কিছুই অনুভব করে না। তাই তো ওরা এটা করতে পারে।",
        ],
      },
      {
        ask: [
          "তুমি থেমে যাও না কেন? ওটা ফেলে আমার সঙ্গে নিচে চলো।",
          "আমি থামলে ওরা জিতে যাবে।",
          "কী জিতবে?",
          "চূড়ায় গিয়ে ভেবে দেখো। নামার পথে আমাকে বোলো।",
        ],
        give: [
          "থামা মানে ওদের সঙ্গে একমত হওয়া। আমি কখনো একমত হব না।",
          "বাবা খাজনা আদায়কারীদের নিয়ে এটাই বলে। বাবা খুব ক্লান্ত।",
        ],
      },
      {
        ask: ["ওখান থেকে কি সমুদ্র দেখা যায়? আমি কখনো সমুদ্র দেখিনি।"],
        give: ["ওই তো আছে। আজ ধূসর, আর অনেক দূরে।", "তাহলে ওকে ধূসর হিসেবেই মনে রাখব।"],
      },
      {
        ask: ["কাল মায়ের সঙ্গে শহরে যাচ্ছি। পথের জন্য আমাকে কিছু এনে দেবে?"],
        give: [
          "থাইম, চূড়া থেকে। যেখানে কিছুই জন্মানোর কথা নয়, সেখানে জন্মায়।",
          "আমি ফিরে আসব, কথা দিলাম। ঠেলতে থাকো।",
          "আমি আর কোথায়ই বা যাব?",
        ],
      },
      {
        ask: [
          "সিসিফাস। আমাকে মনে আছে? আমি সেই মেয়েটা, যে একটা পাথর চেয়েছিল।",
          "তুমি তখন ছোট ছিলে।",
          "বারো বছর। তুমি একটুও বদলাওনি — তোমার চটিজোড়াও না। যে গর্ত থেকে আমার পাথরটা নিয়েছিলে, সেটা কি এখনো আছে?",
        ],
        give: [
          "ধুলোয় ভরে গেছে। কেউ বলতে পারবে না সেখানে কখনো পাথর ছিল।",
          "তাহলে আমরা যে এখানে ছিলাম, তার কোনো চিহ্নই নেই। ওটুকুও না।",
        ],
      },
      {
        ask: [
          "এই শীতে আমার স্বামী মারা গেছেন। সবাই আমাকে বলে এর একটা অর্থ ছিল।",
          "তুমি আমার চেয়ে আকাশের কাছাকাছি। আমার হয়ে দেবতাদের জিজ্ঞেস কোরো — এটা কীসের জন্য ছিল?",
        ],
        give: ["জিজ্ঞেস করেছি। আকাশ কিছুই বলেনি। আমাকেও সে কিছু বলে না।", "অন্তত সে সৎ।"],
      },
      {
        ask: [
          "আজ তোমার চড়াই গুনলাম। একত্রিশ। তুমি গোনো না?",
          "ছেড়ে দিয়েছি। ওই সংখ্যাটাই ছিল একমাত্র জিনিস যা বাড়ত।",
          "আমার ছেলে কখনো বরফ দেখেনি। চূড়া থেকে ওর জন্য একটু আনবে?",
        ],
        give: [
          "নামার পথে গলে গেল। সবসময়ই গলে যায়।",
          "তাহলে ওকে বোলো সেটা কেমন ছিল। আমরা সবাই তো শুধু এটুকুই ফিরিয়ে আনি।",
        ],
      },
      {
        ask: [
          "কিছুই যদি গুরুত্বপূর্ণ না হয়, তবু তুমি ঠেলছ কেন?",
          "আর জানি না।",
          "ওপরে গিয়ে খুঁজে দেখো। আমি অপেক্ষা করব।",
        ],
        give: [
          "চূড়ায় শুধু হাওয়া, আর ফেরার পথ। কিন্তু সারা পথ আমার হাত জানত কী করতে হবে।",
          "হয়তো সেটাই কিছু। হয়তো সেটাই একমাত্র।",
        ],
      },
      {
        ask: [
          "আমার ছেলে একদিন তোমার সঙ্গে উঠতে চায়। ওকে বলেছি এটা একটা শাস্তি।",
          "ওর জন্য ওখান থেকে কিছু একটা এনো। যা হোক কিছু।",
        ],
        give: [
          "আমার পাথরের একটা টুকরো। দেবতার বজ্র ওটা ভেঙে দিয়েছিল।",
          "নিজের বোঝা দিয়ে দেবে?",
          "এখনো ভারী। তবু নাও।",
        ],
      },
      {
        ask: [
          "এখন এই পথে আমার হাঁটু ব্যথা করে। তুমি দিনে একশো বার ওঠো, কখনো অভিযোগ করো না।",
          "করতাম। প্রতিটা পদক্ষেপে।",
          "কবে থামলে?",
          "খেয়াল করিনি।",
          "তাহলে ওখান থেকে আমার জন্য সকালের আলো এনো। সেটা কখনো আমার ঘরে পৌঁছায় না।",
        ],
        give: ["আমার সঙ্গে নেমে এসেছে। দেখো — তোমার হাতে।", "সত্যিই তো।"],
      },
      {
        ask: [
          "তুমি কি এখনো ওদের ঘৃণা করো? দেবতাদের?",
          "ঘৃণা মানে ওপরে তাকানো। আমি তাকিয়ে ছিলাম পথের দিকে।",
          "এখন যদি ওখানে কিছু জন্মায়, আমার জন্য একটা ফুল এনো।",
        ],
        give: [
          "একটা পপি। পাথরটা যেখান দিয়ে গেছে, সেখানেই ফোটে।",
          "তোমার শাস্তি একটা বাগান গড়ছে।",
        ],
      },
      {
        ask: [
          "আমি শিগগিরই চলে যাব। আমাকে মনে রাখবে?",
          "এই পাহাড়ের প্রতিটা পাথর আমার মনে আছে। তুমি কখনো পাথর ছিলে না।",
          "তাহলে বলো চূড়া থেকে কী দেখো। আজ। ঠিক যেমন।",
        ],
        give: [
          "সমুদ্র। শহর। তোমার ছাদ, আর তোমার রান্নাঘরের ধোঁয়া। আর নিচে নামার লম্বা পথ, যেটা আমার।",
          "তোমাকে প্রায় সুখী শোনাচ্ছে।",
          "তাই নাকি?",
        ],
      },
      {
        ask: [
          "এ আমার নাতি। নমস্কার বলো, লাইকোস।",
          "তুমিই কি সেই লোক যে মৃত্যুকে ঠকিয়েছিল?",
          "ওর দিদিমা বড্ড বেশি কথা বলে।",
          "ওর জন্য একেবারে চূড়া থেকে একটা পাথর এনো।",
        ],
        give: ["এই নাও। চূড়া থেকে।", "এটা শুধুই একটা পাথর।", "হ্যাঁ। দারুণ না?"],
      },
      {
        ask: ["দিদিমা বসন্তে মারা গেছেন।", "উনি তোমাকে বলতে বলেছিলেন: “চূড়ায় গিয়ে ও বুঝবে।”"],
        give: [
          "তুমি কী বুঝলে?",
          "যে চড়াইটা কখনো আমার জীবনের পথে বাধা ছিল না। ওটাই ছিল আমার জীবন।",
        ],
      },
    ],
    fillers: [
      [
        "আরও জোরে! আমি গুনছি।",
        "মা বলে তোমার সঙ্গে কথা বলা বারণ। আমি পাথরের সঙ্গে কথা বলছি।",
        "চূড়ায় পৌঁছে আমার দিকে হাত নেড়ো!",
      ],
      [
        "ঘর বেশি চুপচাপ হয়ে গেলে আমি এখানে আসি।",
        "আজ তোমাকে ক্লান্ত দেখাচ্ছে। নাকি ক্লান্ত আমিই।",
        "ছাগলগুলো এখন তোমাকে চেনে। ওরা সরে দাঁড়ায়।",
      ],
      [
        "নামার পথে একটু আমার পাশে বসো। কিছু বলতে হবে না।",
        "তুমি যেখানে হাঁটো, পথটা ক্ষয়ে গেছে। নদীর খাতের মতো।",
        "তোমাকে দেখতে দেখতে আমি বুড়ো হয়ে গেলাম। তুমি হলে না। অদ্ভুত — আমার হিংসে হয় না।",
      ],
    ],
    after: [
      "দিদিমা বলেছিলেন তুমি এখানে থাকবে। উনি সব ব্যাপারেই ঠিক ছিলেন।",
      "আজ পাথরটা বেশি ভারী, না হালকা?",
      "আমি বুড়ো হলেও আসব।",
      "রুটি এনেছি। পাথরের ওপর রেখে যাচ্ছি।",
    ],
  },
  pt: {
    names: { ione: "Ione", sis: "Sísifo", lykos: "Lykos", pro: "Prometeu", tan: "Tântalo" },
    companions: {
      pro: {
        meetings: [
          [
            "Mais um. Me acorrentaram aqui por dar o fogo aos homens.",
            "Todo dia a águia vem pelo meu fígado. Toda noite ele cresce de novo. Está vendo? O meu é pior.",
          ],
          [
            "Você empurra como quem tenta alcançar alguma coisa. Não olhe para o cume enquanto empurra.",
            "Para onde eu olho?",
            "Para as suas mãos. São a única parte disso tudo que é sua.",
          ],
          ["Dizem que eu amei a humanidade. Nunca pedi para ser amado de volta. Melhor assim."],
          [
            "Você levaria a minha dor até o topo? Uma vez. Quero saber se lá em cima ela pesa alguma coisa.",
          ],
          [
            "Você levou. Por um instante me senti mais leve — eu tinha esquecido como era.",
            "Na sua próxima subida, se a pedra escorregar, a minha corrente vai segurá-la, uma vez. Um pequeno fogo para você.",
          ],
          [
            "A sua pedra pelo menos se move. As minhas correntes nunca se rompem.",
            "Então é mais fácil para você. Você só precisa esperar.",
            "Não, irmão. O seu é um castigo de fazer; o meu, de esperar. Fazer é o mais leve — você escolhe como.",
          ],
          [
            "Dei o fogo aos homens para que vissem no escuro. Você está mostrando a eles algo melhor: que dá para continuar andando nele.",
          ],
        ],
        idle: [
          "A águia está atrasada hoje. Quase sinto falta dela.",
          "Empurre, irmão. Eu fico olhando.",
          "Olhe para as mãos, não para o topo.",
        ],
        declined: "Então eu mesmo carrego. Sempre carreguei.",
        accepted: "Ha. Já está mais pesada, não está? Não pare.",
      },
      tan: {
        meetings: [
          [
            "Água até o queixo, frutas sobre a cabeça. Quando me abaixo, a água foge. Quando estico a mão, o galho sobe.",
            "Para sempre, com sede num lago. E você? O que é que lhe negam?",
          ],
          [
            "Você não quer o topo? Eu quero esta água, a cada instante.",
            "Eu não quero o topo. Só chego lá.",
            "Então você é mais livre que eu. Querer é a minha corrente.",
          ],
          ["Leva a minha sede lá para cima, por mim? Talvez no topo ela passe."],
          [
            "Ela me deixou lá em cima? …Não. Mas por um tempo eu a esqueci.",
            "Na sua próxima subida vou calçar uma pedra atrás da sua. Uma vez. Querer me ensinou onde as coisas escorregam.",
          ],
          [
            "Acho que agora entendo você. Você não empurra para chegar.",
            "Então para quê?",
            "Para empurrar. Eu só estendia a mão para ter. Esse foi o meu erro, não o meu pecado.",
          ],
          ["Se eu parasse de querer, a água ainda fugiria? Pergunte ao topo por mim."],
        ],
        idle: [
          "Já bebeu o próprio suor? Salgado. Mas ainda é água.",
          "Vai. Alguém aqui precisa se mexer.",
          "O figo está maduro de novo. Claro que está.",
        ],
        declined: "Claro. Quem ia querer?",
        accepted: "Está sentindo? É assim cada instante, para mim.",
      },
    },
    burdens: {
      pain: { name: "A dor de Prometeu", desc: "A pedra está mais pesada nesta subida." },
      thirst: { name: "A sede de Tântalo", desc: "O fôlego acaba mais rápido nesta subida." },
    },
    passives: {
      fire: { name: "Fogo", desc: "O fôlego volta mais rápido. Para sempre." },
      patience: { name: "Paciência", desc: "Um escorregão devolve menos terreno. Para sempre." },
    },
    accept: "Carregar",
    decline: "Recusar",
    wedgeHeld: "O calço segurou.",
    took: "Do cume:",
    items: {
      stone: "uma pedra lá do topo",
      feather: "uma pena da águia de Zeus",
      answer: "uma resposta",
      sea: "um olhar para o mar",
      thyme: "um raminho de tomilho",
      dust: "notícias do buraco",
      silence: "a resposta do céu",
      snow: "um punhado de neve",
      hands: "o que as mãos dele sabem",
      shard: "um caco da pedra dele",
      light: "a luz da manhã",
      poppy: "uma papoula",
      view: "a vista, em palavras",
      pebble: "uma pedra para o menino",
      understanding: "o que ele entendeu",
    },
    episodes: [
      {
        ask: [
          "Você é o homem que enganou a Morte? A vovó diz que você a acorrentou no seu porão.",
          "A sua avó fala demais. Saia do caminho, menina.",
          "Me traga uma pedra lá do topo. Quero ver se ela é diferente.",
        ],
        give: ["Tome. Do topo.", "É… só uma pedra.", "Lá em cima, tudo é só uma pedra."],
      },
      {
        ask: [
          "É verdade que os deuses ficam olhando você o dia todo? Me traga alguma coisa deles.",
        ],
        give: [
          "Uma pena da águia dele. Ela deixou cair enquanto ria de mim.",
          "Está quentinha. Os deuses sentem frio, às vezes?",
          "Eles não sentem nada. É por isso que conseguem fazer isso.",
        ],
      },
      {
        ask: [
          "Por que você simplesmente não para? Larga isso e desce comigo.",
          "Se eu parar, eles ganham.",
          "Ganham o quê?",
          "Pense nisso lá no topo. Me conte na descida.",
        ],
        give: [
          "Parar seria dar razão a eles. Nunca vou dar.",
          "O papai diz isso dos cobradores de impostos. Ele está muito cansado.",
        ],
      },
      {
        ask: ["Dá para ver o mar lá de cima? Eu nunca vi o mar."],
        give: ["Está lá. Cinzento hoje, e muito longe.", "Então vou lembrar dele cinzento."],
      },
      {
        ask: ["Amanhã vou para a cidade com a mamãe. Me traz alguma coisa para a viagem?"],
        give: [
          "Tomilho, do cume. Cresce onde nada deveria crescer.",
          "Eu vou voltar, prometo. Continue empurrando.",
          "E para onde mais eu iria?",
        ],
      },
      {
        ask: [
          "Sísifo. Lembra de mim? Eu era a menina que queria uma pedra.",
          "Você era pequena.",
          "Doze anos. Você não mudou nada — nem as sandálias. O buraco de onde você tirou a minha pedra ainda está lá?",
        ],
        give: [
          "A poeira encheu. Ninguém diria que um dia houve uma pedra ali.",
          "Então nada marca que estivemos aqui. Nem isso.",
        ],
      },
      {
        ask: [
          "Meu marido morreu neste inverno. Todo mundo me diz que teve um sentido.",
          "Você está mais perto do céu do que eu. Pergunte aos deuses por mim — foi para quê?",
        ],
        give: [
          "Perguntei. O céu não disse nada. Para mim também não diz nada.",
          "Pelo menos ele é honesto.",
        ],
      },
      {
        ask: [
          "Hoje contei as suas subidas. Trinta e uma. Você não conta?",
          "Parei. O número era a única coisa que crescia.",
          "Meu filho nunca viu neve. Traz um pouco do topo para ele?",
        ],
        give: [
          "Derreteu na descida. Sempre derrete.",
          "Então conte a ele como era. É só isso que qualquer um de nós traz de volta.",
        ],
      },
      {
        ask: [
          "Por que você ainda empurra, se nada disso importa?",
          "Já não sei.",
          "Descubra lá em cima. Eu espero.",
        ],
        give: [
          "No topo só tem vento, e o caminho de volta. Mas as minhas mãos sabiam o que fazer o tempo todo.",
          "Talvez isso seja alguma coisa. Talvez seja a única.",
        ],
      },
      {
        ask: [
          "Meu filho quer subir com você um dia. Eu disse a ele que é um castigo.",
          "Traga alguma coisa lá de cima para ele. Qualquer coisa.",
        ],
        give: [
          "Um pedaço da minha pedra. O raio do deus arrancou.",
          "Você daria o seu próprio fardo?",
          "Ainda pesa. Fique com ele assim mesmo.",
        ],
      },
      {
        ask: [
          "Meus joelhos doem nesta trilha agora. Você sobe cem vezes por dia e nunca reclama.",
          "Eu reclamava. A cada passo.",
          "Quando parou?",
          "Não percebi.",
          "Então me traga a luz da manhã lá de cima. Ela nunca chega à minha casa.",
        ],
        give: ["Ela desceu comigo. Olhe — está nas suas mãos.", "É mesmo."],
      },
      {
        ask: [
          "Você ainda os odeia? Os deuses?",
          "Odiar é olhar para cima. Eu tenho olhado para o caminho.",
          "Se agora cresce alguma coisa lá em cima, me traga uma flor.",
        ],
        give: [
          "Uma papoula. Elas nascem por onde a pedra passou.",
          "O seu castigo está fazendo um jardim.",
        ],
      },
      {
        ask: [
          "Logo eu vou embora. Você vai lembrar de mim?",
          "Eu lembro de cada pedra deste morro. Você nunca foi uma pedra.",
          "Então me diga o que você vê lá do topo. Hoje. Exatamente.",
        ],
        give: [
          "O mar. A cidade. O seu telhado, e a fumaça da sua cozinha. E o longo caminho de descida, que é meu.",
          "Você parece quase feliz.",
          "Pareço?",
        ],
      },
      {
        ask: [
          "Este é o meu neto. Diga oi, Lykos.",
          "Você é o homem que enganou a Morte?",
          "A avó dele fala demais.",
          "Traga para ele uma pedra lá do topo.",
        ],
        give: ["Tome. Do topo.", "É só uma pedra.", "É. Não é maravilhoso?"],
      },
      {
        ask: [
          "A vovó morreu na primavera.",
          "Ela me pediu para te dizer: “Ele vai entender lá no topo.”",
        ],
        give: [
          "O que você entendeu?",
          "Que a subida nunca esteve no caminho da minha vida. Ela era a minha vida.",
        ],
      },
    ],
    fillers: [
      [
        "Mais rápido! Estou contando.",
        "A mamãe diz que eu não posso falar com você. Estou falando com a pedra.",
        "Quando chegar lá em cima, acene para mim!",
      ],
      [
        "Venho aqui quando a casa fica quieta demais.",
        "Você parece cansado hoje. Ou talvez seja eu.",
        "As cabras já conhecem você. Elas saem do caminho.",
      ],
      [
        "Sente um pouco comigo na descida. Não precisa dizer nada.",
        "A trilha está gasta por onde você passa. Como o leito de um rio.",
        "Envelheci olhando você. Você não. Estranho — não sinto inveja.",
      ],
    ],
    after: [
      "A vovó disse que você estaria aqui. Ela estava certa sobre tudo.",
      "A pedra está mais pesada hoje, ou mais leve?",
      "Quando eu for velho, ainda vou vir.",
      "Trouxe pão. Vou deixar na rocha.",
    ],
  },
};

/** the story in a language, falling back to English until it is translated */
export function storyFor(lang: LangCode): LangStory {
  return STORY_TEXT[lang];
}
