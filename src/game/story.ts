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
 * Prometheus and Tantalus share the hill. They are met on the way up, and
 * each meeting with the same one goes further — they speak of their own
 * punishment, then of his, then ask him to carry their burden to the top
 * (a harder climb, and a gift that stays), and later measure their fate
 * against his.
 */
import type { LangCode } from "./quotes";

/** "narr" is a stage direction: no speaker, set in italics */
export type Speaker = "merope" | "sis" | "pro" | "tan" | "narr";

/** what he can bring down from the summit for her */
export type ItemId = "stars" | "wind" | "spring" | "lethe";

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
  /** summits since the last episode before this one can happen (2 if unset) */
  gap?: number;
  /** after this she stands with her back to him, until the next act */
  turns?: boolean;
  /** the last scene: after it, the ending */
  finale?: boolean;
};

const M = "merope" as const;
const S = "sis" as const;
const N = "narr" as const;

export const EPISODES: Episode[] = [
  // ---- Act I: Revolt ----
  { act: 1, cine: true, who: [M, S, M, S, M, S, M, S, M], asks: "stars" },
  { act: 1, cine: false, who: [S, M, S, M, M], brings: "stars", asks: "wind" },
  { act: 1, cine: false, who: [S, S, M, M], brings: "wind" },
  { act: 1, cine: true, gap: 2, who: [M, M, M, M, S, M, S, M, S] },
  // ---- Act II: The Void ----
  { act: 2, cine: false, who: [M, S, M, M, S, M], asks: "spring" },
  { act: 2, cine: false, who: [S, M, S, M], brings: "spring" },
  { act: 2, cine: false, who: [M, S, M, S, M] },
  { act: 2, cine: true, gap: 2, who: [M, M, S, M, M, S, M, N], turns: true },
  { act: 2, cine: false, gap: 2, who: [S, S, S] },
  // ---- Act III: The Absurd ----
  { act: 3, cine: false, who: [M, S, M, M] },
  { act: 3, cine: true, gap: 2, who: [M, S, M, M, S, M], asks: "lethe", choice: true },
  { act: 3, cine: true, who: [S, M, M, S, M, N, M, S, N], brings: "lethe", finale: true },
];

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
  /** only after this many summits */
  after?: number;
  /** only once Merope's story has reached this episode */
  minEp?: number;
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
      { who: ["pro", S, "pro"], minEp: 1 },
      { who: ["pro", S, "pro"] },
      { who: ["pro"], offer: "pain" },
      { who: ["pro", "pro"], gift: true },
      { who: ["pro", S, "pro"], after: 15 },
      { who: ["pro"], minEp: 12 },
    ],
  },
  tan: {
    burden: "thirst",
    passive: "patience",
    meetings: [
      { who: ["tan", "tan"] },
      { who: ["tan"], offer: "thirst" },
      { who: ["tan", "tan"], gift: true },
      { who: ["tan", S, "tan"], minEp: 7 },
      { who: ["tan", S, "tan"], minEp: 8 },
      { who: ["tan", S, "tan"], minEp: 9, after: 12 },
      { who: ["tan"], minEp: 10 },
      { who: ["tan"], minEp: 12 },
    ],
  },
};

type CompanionText = {
  meetings: string[][];
  idle: string[];
  declined: string;
  accepted: string;
};

export type LangStory = {
  names: Record<Speaker, string>;
  /** "From the summit:" — and what each thing is */
  took: string;
  items: Record<ItemId, string>;
  /** the words of each episode, line for line with EPISODES[].who */
  episodes: string[][];
  /** the answers to the choice in the request scene, and her reply to "not yet" */
  bring: string;
  notYet: string;
  /** her answers to "not yet": the first time, the second, and after */
  notYetReplies: [string, string, string];
  /** a line from her when there is nothing new to say, by act */
  fillers: [string[], string[], string[]];
  /** what she says with her back to him, after the midpoint */
  turned: string[];
  companions: Record<CompanionId, CompanionText>;
  burdens: Record<BurdenId, { name: string; desc: string }>;
  passives: Record<PassiveId, { name: string; desc: string }>;
  accept: string;
  decline: string;
  wedgeHeld: string;
  /** "tap to continue" */
  next: string;
};

export const STORY_TEXT: Record<LangCode, LangStory> = {
  tr: {
    names: { merope: "Merope", sis: "Sisifos", pro: "Prometheus", tan: "Tantalos", narr: "" },
    took: "Zirveden:",
    items: {
      stars: "Ülker yıldızları",
      wind: "denizden gelen rüzgâr",
      spring: "unutuş pınarı",
      lethe: "Lethe'nin suyu, avuçlarında",
    },
    episodes: [
      [
        "Yolcu… Bu tepenin bir sonu var mı?",
        "…Merope?",
        "Merope. Güzel bir ad. Benim mi?",
        "Senin. Korint'te kırk yıl bu adla seslendim sana.",
        "Hatırlamıyorum. Buraya gelen herkes ırmaktan içer. Ben de içmiş olmalıyım.",
        "Lethe. Seni de benden çaldılar.",
        "Kimse beni senden çalmadı. Yalnızca bir boşluk var; adımın olması gereken yerde.",
        "Zeus! Taşını da tepeni de geri al, ne istersen al. Yeter ki onu bana geri ver!",
        "Bağırma. Burada sesler uzağa gitmez. Zirvede gök açılıyor mu? Burada hep kül rengi. Çıkınca bana yıldızları anlat.",
      ],
      [
        "Açılıyor. Yıldızlar var. Yedi kız kardeş: Ülker. Biri ötekilerden sönük.",
        "Neden sönük?",
        "Bir ölümlüyle evlendi. Utancından ışığını kıstı, derler. Adı Merope.",
        "Merope… Benim adım. Bir ölümlü… Sen misin o ölümlü?",
        "Cevap verme. Korint adını duyunca içim sızlıyor, nedenini bilmiyorum. Burada hava hiç kıpırdamaz. Zirveden bana rüzgâr getir.",
      ],
      [
        "Zirvede rüzgâr denizden esiyor. Tuz kokuyor. Korint'in iki denizi vardı; sarayımız ikisine de bakardı.",
        "Ben denizden dönerdim. Sen her sabah doğu kapısında durup limana giren gemileri sayardın.",
        "Gemiler… Birini bekliyordum. Hep birini.",
        "Kimi beklediğimi hatırlayınca sana söylerim. Ama o sabahlar hiç bitmemiş gibi geliyor.",
      ],
      [
        "Ölüler rüya görmez derler. Sen Korint'i anlattıktan sonra ben gördüm.",
        "Bir adam ölüm döşeğinde fısıldıyordu: “Beni gömme. Ne olursa olsun, gömme.”",
        "Ben de gömmedim. Bütün Korint bana tükürdü. Kocasını köpeklere bırakan kadın, dediler.",
        "O adam sendin. Değil mi?",
        "Evet.",
        "Neden?",
        "Ölüm'ü bir kez daha kandırmak için. Yeraltının kraliçesine “karım beni gömmedi” diyecek, onu cezalandırmak için yukarı dönecektim.",
        "Döndün mü?",
        "Döndüm. Yaşamaya. Sana değil.",
      ],
      [
        "Şimdi hatırlıyorum: seni bekledim. Kapıda, her sabah. Ben yaşlandım, sen hiç gelmedin.",
        "Yaşıyordum. Ölüm'den çaldığım her günü yaşamak istedim.",
        "Ben o günlerin bedelini ödedim. Her birini.",
        "Hatırlamak bu kadar ağırmış. Bir söylenti var: zirvenin ardında Lethe'nin kaynağı, bir pınar varmış.",
        "Hiç bakmadım. Hep taşa baktım.",
        "Bak. Benim için bak.",
      ],
      [
        "Var. Taşın durduğu yerin hemen ardında. Suyu öyle durgun ki hiçbir şeyi yansıtmıyor.",
        "Oradan bir yudum içsem acım diner. Seni de yeniden unuturum.",
        "İçme.",
        "Neden? Benim için mi, senin için mi?",
      ],
      [
        "Cevap vermedin. Başka bir şey hatırlıyorum: atların arasında gülen bir çocuk.",
        "Glaukos. Oğlumuz.",
        "Ne oldu ona?",
        "Kendi atları onu parçaladı, yarışlarda. O gün de orada değildim.",
        "Sen hep başka bir yerdeydin. Ben hep kapıda.",
      ],
      [
        "Sana bir şey soracağım ve bu kez doğruyu söyleyeceksin.",
        "Ölüm'ü kandırırken, bana veda ederken… benim ne olacağımı hiç düşündün mü?",
        "Hayır.",
        "Tanrılara öfkeleniyorsun. Taşın için, tepen için. Taşı onlar verdi, evet. Ama onu sen kazandın.",
        "Bu taşı sen yonttun. Her yalanla biraz daha.",
        "Merope—",
        "O adla seslenme bana. O kadını sen öldürdün. Lethe yalnızca gömdü.",
        "Merope arkasını döner.",
      ],
      [
        "Bugün zirvede durdum ve tanrılara bağırmadım. Bağıracak kimse kalmadı; yalnızca ben varım.",
        "“İçme” demiştim sana. Senin için değil, kendim için demiştim.",
        "Özür dilerim, Merope. Hiçbir şeyi geri getirmeyeceğini biliyorum. Yine de dilerim.",
      ],
      [
        "Seni izliyordum. Artık lanet okumuyorsun.",
        "Bu taşı sırtıma ben yükledim. Kendi taşına lanet okumak… boşuna.",
        "Dün gece gök bir an yarıldı. Ülker'i gördüm: sönük yıldız biraz parlıyordu.",
        "Seni gömmediğim için kendimi suçlardım. Oysa yalnızca sözümü tutmuştum. Artık utanmıyorum; ne senden, ne kendimden. Yakında senden bir şey isteyeceğim. Kolay olmayacak.",
      ],
      [
        "Sisifos. Pınardan bana su getir. Gölgeler zirveye çıkamaz.",
        "Beni yeniden unutursun.",
        "Biliyorum. Ama bu kez kaçmak için değil. Bu yükü artık taşımam gerekmediği için.",
        "Sen taşını taşımayı öğrendin. Ben de yükümü bırakmayı öğrenmeliyim.",
        "Seni bir kez daha kaybedeceğim.",
        "Hayır. Kaybetmeyeceksin; beni salıvereceksin. Arada büyük fark var. Getirecek misin?",
      ],
      [
        "Getirdim.",
        "Ellerin titriyor. Taşı iterken hiç titremiyorlar.",
        "Son bir şey söyle bana. Doğru bir şey.",
        "Seni sevdim. Kötü bir sevgiydi, ama sevgiydi.",
        "Biliyorum. Hatırlamasam bile bileceğim. …Ver bana.",
        "Suyu içer. Uzun bir sessizlik.",
        "Yolcu… Bu tepenin bir sonu var mı?",
        "Hayır. Ama yolu güzel.",
        "Merope kül rengi ovaya doğru yürür. Bir kez bile dönüp bakmaz. Sisifos taşına iner.",
      ],
    ],
    bring: "Suyu getireceğim",
    notYet: "Henüz değil",
    notYetReplies: [
      "O zaman yarın. Acele yok; burada zaman bol.",
      "Ne istediğini biliyorum: beni daha uzun hatırlamak. Hatırlamak sevgidir, Sisifos. Tutmak değil.",
      "Taş bekliyor. Ben de. İkimiz de bu işte ustayız.",
    ],
    fillers: [
      [
        "Yine sen. Taşın bugün ağır mı?",
        "Adımı unuttum ama ellerimi hatırlıyorum. Sanırım dokurdum.",
        "Zirveden dönünce yüzüme bak. Belki bir şey hatırlarım.",
      ],
      [
        "Bugün konuşmak istemiyorum.",
        "Tepenin gölgesi uzadıkça hatırladıklarım da uzuyor.",
        "Glaukos'un gülüşü kulağımda. Keşke onu da unutsaydım.",
      ],
      [
        "Güzel bir yol bu, Sisifos. Bunu sana kimse söylemedi mi?",
        "Taşı iterken yüzün değişiyor. Kızgın değilsin artık.",
        "Pınar bekler. Acele etme.",
      ],
    ],
    turned: ["…", "Git, Sisifos.", "Taşın bekliyor."],
    companions: {
      pro: {
        meetings: [
          [
            "Neye bakıyorsun? Asıl gösteri kartal gelince başlar.",
            "Prometheus. İnsanlara ateşi ben verdim. Zeus bütün cezalarını bu yamaca dizdi. Benimki bu kaya ve o kuş.",
          ],
          [
            "Aşağıdaki kadını gördüm. Unutuş suyundan içmiş.",
            "Karım o.",
            "O halde unutmak onun cezası değil, Sisifos. Senin cezan.",
          ],
          [
            "Neden hâlâ gözün zirvede, öyle itiyorsun?",
            "Başka nereye bakayım?",
            "Ellerine. Bu işte sana ait olan tek şey onlar.",
          ],
          [
            "Kartal bugün ciğerimin yarısını götürdü. Bu acıyı bir tırmanışlığına taşır mısın? Zirvede acının bir ağırlığı var mı, bilmek istiyorum.",
          ],
          [
            "Taşıdın. Bir an hafifledim; bunun ne olduğunu unutmuştum. Ateşimden bir kıvılcım artık sende: nefesin daha çabuk dönecek.",
            "Bir dahaki tırmanışta zincirimin ucu da taşını bir kez tutacak.",
          ],
          [
            "Senin taşın hiç değilse hareket ediyor. Benim zincirlerim hiç kopmuyor.",
            "O zaman seninki daha kolay. Yalnızca beklemen gerekiyor.",
            "Hayır. Beklemek, hiçbir şey yapamamaktır. Sen hiç değilse nasıl iteceğini seçiyorsun.",
          ],
          [
            "İnsanlar karanlıkta görsün diye ateşi verdim. Sen o gölgeye bir ateş bıraktın, Sisifos. Aşağıda biri ilk kez ısındı.",
          ],
        ],
        idle: [
          "Kartal geç kaldı bugün. Neredeyse özledim.",
          "İt, kardeşim. Ben izliyorum.",
          "Zirveye değil, ellerine bak.",
        ],
        declined: "O halde kendim taşırım. Hep taşıdım.",
        accepted: "Ağırlaştı, değil mi? Durma.",
      },
      tan: {
        meetings: [
          [
            "Su çeneme kadar, meyve başımın üstünde. Eğilince su çekiliyor, uzanınca dal yükseliyor.",
            "Tanrıları soframa çağırdım ve onları sınamak için kendi oğlumu pişirdim. Bu yüzden açım, susuzum. Ya sen?",
          ],
          [
            "Susuzluğumu bir tırmanışlığına taşır mısın? Tepede dinerse, tanrıların da bir merhameti var demektir.",
          ],
          [
            "Dinmedi, değil mi? Biliyordum. Ama bir süre unuttum onu. Karşılığında sabrımı al: artık kayınca daha az geri gideceksin.",
            "Bir dahaki tırmanışta taşının arkasına bir takoz koyacağım. Her şey kayar, ben bilirim. Ama bir kısmı kalır.",
          ],
          [
            "Kadın bir çocuktan söz ediyormuş. Glaukos.",
            "Kendi atları onu parçaladı. Ben orada değildim.",
            "Ben oğlumu kendi ellerimle sofraya koydum ve oradaydım. Hangimiz daha kötü baba, bilmiyorum. Ama sen hiç değilse utanıyorsun.",
          ],
          [
            "Kadına baktım. Sana sırtını dönmüş ama bir şey arıyor.",
            "Beni arıyor.",
            "Hayır. Seni aramayı bırakmanın yolunu arıyor. İkisi aynı şey değil.",
          ],
          [
            "Seni artık anlıyorum. Sen ulaşmak için itmiyorsun.",
            "Ne için o zaman?",
            "İtmek için. Ben hep sahip olmak için uzandım. Cezam bu değil; hatam buydu.",
          ],
          ["İstemeyi bıraksam su yine kaçar mıydı? Bunu bir gün zirveye sor benim için."],
          ["Bugün su çenemde durdu. Ben uzanmadım. Kaçmadı. Bir an için."],
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
    next: "devam",
  },
  en: {
    names: { merope: "Merope", sis: "Sisyphus", pro: "Prometheus", tan: "Tantalus", narr: "" },
    took: "From the summit:",
    items: {
      stars: "the Pleiades",
      wind: "the wind off the sea",
      spring: "the spring of forgetting",
      lethe: "the water of Lethe, in his hands",
    },
    episodes: [
      [
        "Traveller… does this hill have an end?",
        "…Merope?",
        "Merope. A beautiful name. Is it mine?",
        "Yours. In Corinth I called you by it for forty years.",
        "I don't remember. Everyone who comes here drinks from the river. I must have drunk too.",
        "Lethe. They stole you from me too.",
        "No one stole me. There is only an emptiness where my name should be.",
        "Zeus! Take back your stone, take back your hill, take anything. Only give her back to me!",
        "Don't shout. Voices don't carry here. Does the sky open at the top? Down here it is always ash-grey. When you climb, tell me about the stars.",
      ],
      [
        "It does. There are stars. Seven sisters: the Pleiades. One is fainter than the rest.",
        "Why fainter?",
        "She married a mortal. She dimmed her light for shame, they say. Her name is Merope.",
        "Merope… My name. A mortal… Are you that mortal?",
        "Don't answer. When I hear “Corinth” something aches in me, and I don't know why. The air here never moves. Bring me the wind from the top.",
      ],
      [
        "At the top the wind comes off the sea. It smells of salt. Corinth had two seas, and our palace looked out on both.",
        "I used to come home by sea. Every morning you stood at the eastern gate and counted the ships into harbour.",
        "The ships… I was waiting for someone. Always someone.",
        "When I remember who, I'll tell you. But those mornings feel as if they never ended.",
      ],
      [
        "They say the dead don't dream. After you told me about Corinth, I did.",
        "A man on his deathbed was whispering to me: “Don't bury me. Whatever happens, don't bury me.”",
        "So I didn't. And all Corinth spat at me. The woman who left her husband to the dogs, they called me.",
        "That man was you. Wasn't it?",
        "Yes.",
        "Why?",
        "To cheat Death a second time. I would tell the queen of the dead that my wife had not buried me, and beg to go back up to punish her.",
        "And did you come back?",
        "I went back. To live. Not to you.",
      ],
      [
        "I remember now: I waited for you. At the gate, every morning. I grew old. You never came.",
        "I was living. I wanted to live every day I had stolen from Death.",
        "I paid for those days. Every one of them.",
        "Remembering is this heavy. There's a rumour: behind the summit is the source of Lethe, a spring.",
        "I never looked. I only ever looked at the stone.",
        "Look. Look for me.",
      ],
      [
        "It's there. Just past the place where the stone stops. The water is so still it reflects nothing.",
        "One sip, and the pain would stop. I'd forget you again.",
        "Don't.",
        "Why? For my sake, or for yours?",
      ],
      [
        "You didn't answer. I remember something else: a child laughing among horses.",
        "Glaucus. Our son.",
        "What happened to him?",
        "His own horses tore him apart, at the games. I wasn't there that day either.",
        "You were always somewhere else. I was always at the gate.",
      ],
      [
        "I'm going to ask you something, and this time you'll tell me the truth.",
        "When you were cheating Death, when you said goodbye to me… did you ever think what would become of me?",
        "No.",
        "You rage at the gods. For your stone, for your hill. They gave you the stone, yes. But you earned it.",
        "You carved this stone yourself. A little more with every lie.",
        "Merope—",
        "Don't call me that. You killed that woman. Lethe only buried her.",
        "Merope turns her back on him.",
      ],
      [
        "Today I stood at the top and didn't shout at the gods. There's no one left to shout at. There's only me.",
        "I told you “Don't.” Not for your sake. For mine.",
        "I'm sorry, Merope. I know it brings nothing back. I'm sorry anyway.",
      ],
      [
        "I've been watching you. You don't curse any more.",
        "I put this stone on my own back. Cursing your own stone is… pointless.",
        "Last night the sky split open for a moment. I saw the Pleiades. The faint star was a little brighter.",
        "I used to blame myself for not burying you. But I had only kept my word. I'm not ashamed any more: not of you, not of myself. Soon I'll ask you for something. It won't be easy.",
      ],
      [
        "Sisyphus. Bring me water from the spring. Shades cannot climb.",
        "You'll forget me again.",
        "I know. But not to escape, this time. Because I don't have to carry this weight any more.",
        "You learned to carry your stone. I have to learn to put down my burden.",
        "I'll lose you again.",
        "No. You won't lose me. You'll let me go. There's a great difference. Will you bring it?",
      ],
      [
        "I brought it.",
        "Your hands are shaking. They never shake when you push the stone.",
        "Tell me one last thing. Something true.",
        "I loved you. It was a bad kind of love. But it was love.",
        "I know. Even if I don't remember, I'll know. …Give it to me.",
        "She drinks. A long silence.",
        "Traveller… does this hill have an end?",
        "No. But the way is beautiful.",
        "Merope walks away across the ash-grey plain. She does not look back once. Sisyphus goes down to his stone.",
      ],
    ],
    bring: "I'll bring the water",
    notYet: "Not yet",
    notYetReplies: [
      "Tomorrow, then. There's no hurry. Time is the one thing we have here.",
      "I know what you want: to remember me longer. Remembering is love, Sisyphus. Holding on is not.",
      "The stone waits. So do I. We are both good at this.",
    ],
    fillers: [
      [
        "You again. Is the stone heavy today?",
        "I've forgotten my name, but I remember my hands. I think I used to weave.",
        "Look at my face when you come back down. Maybe I'll remember something.",
      ],
      [
        "I don't want to talk today.",
        "The longer the hill's shadow grows, the more I remember.",
        "Glaucus's laugh is in my ears. I wish I'd forgotten that too.",
      ],
      [
        "Your road is beautiful, Sisyphus. Has no one ever told you?",
        "Your face changes when you push the stone. You're not angry any more.",
        "The spring will wait. Don't hurry.",
      ],
    ],
    turned: ["…", "Go, Sisyphus.", "Your stone is waiting."],
    companions: {
      pro: {
        meetings: [
          [
            "What are you staring at? The real show begins when the eagle comes.",
            "Prometheus. I gave men fire. Zeus strung all his punishments along this one slope. Mine is this rock, and that bird.",
          ],
          [
            "I saw the woman down the path. She has drunk the water of forgetting.",
            "She's my wife.",
            "Then forgetting isn't her punishment, Sisyphus. It's yours.",
          ],
          [
            "Why do you still push with your eyes on the top?",
            "Where else should I look?",
            "At your hands. They're the only part of this that is yours.",
          ],
          [
            "The eagle took half my liver today. Will you carry the pain of it, for one climb? I want to know whether pain weighs anything at the top.",
          ],
          [
            "You carried it. For a moment I felt lighter; I had forgotten what that was. A spark of my fire is yours now: your breath will come back faster.",
            "And on your next climb, the end of my chain will catch your stone. Once.",
          ],
          [
            "Your stone at least moves. My chains never break.",
            "Then yours is easier. All you have to do is wait.",
            "No. Waiting is being unable to do anything. You, at least, choose how to push.",
          ],
          [
            "I gave men fire so they could see in the dark. You left a fire for that shade, Sisyphus. Down there, someone is warm for the first time.",
          ],
        ],
        idle: [
          "The eagle is late today. I almost miss it.",
          "Push, brother. I'm watching.",
          "Not the top. Your hands.",
        ],
        declined: "Then I'll carry it myself. I always have.",
        accepted: "Heavier already, isn't it? Don't stop.",
      },
      tan: {
        meetings: [
          [
            "Water to my chin, fruit above my head. When I bend, the water sinks. When I reach, the branch lifts.",
            "I invited the gods to my table and cooked my own son to test them. That is why I hunger and thirst. And you?",
          ],
          [
            "Will you carry my thirst, for one climb? If it eases at the top, then the gods have some mercy after all.",
          ],
          [
            "It didn't ease, did it? I knew. But for a while I forgot it. Take my patience in return: from now on a slip will cost you less ground.",
            "And on your next climb I'll wedge a stone behind yours. Everything slips; I know that well. But some of it stays.",
          ],
          [
            "They say the woman spoke of a child. Glaucus.",
            "His own horses tore him apart. I wasn't there.",
            "I set my own son on the table with my own hands, and I was there. I don't know which of us was the worse father. But you, at least, are ashamed.",
          ],
          [
            "I looked at the woman. Her back is turned to you, but she's searching for something.",
            "For me.",
            "No. For a way to stop searching for you. They're not the same.",
          ],
          [
            "I understand you now. You don't push to reach.",
            "Then why?",
            "To push. I only ever reached to have. That wasn't my punishment. It was my mistake.",
          ],
          ["If I stopped wanting, would the water still flee? Ask the top for me, one day."],
          ["Today the water stayed at my chin. I did not reach. It didn't flee. For a moment."],
        ],
        idle: [
          "Ever drunk your own sweat? Salty. Still water.",
          "Go on. Someone here should move.",
          "The figs are ripe again. Of course they are.",
        ],
        declined: "Of course. Who would want it?",
        accepted: "Do you feel it? That is every moment, for me.",
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
    items: {
      stars: "昴星团",
      wind: "海上吹来的风",
      spring: "遗忘之泉",
      lethe: "勒忒河的水，捧在他手中",
    },
    episodes: [
      [
        "旅人……这座山有尽头吗？",
        "……墨洛珀？",
        "墨洛珀。很美的名字。是我的吗？",
        "是你的。在科林斯，我这样叫了你四十年。",
        "我不记得了。来到这里的人都要喝那条河的水。我一定也喝过。",
        "勒忒河。他们把你也从我身边偷走了。",
        "没有人偷走我。只是在我名字该在的地方，有一片空白。",
        "宙斯！收回你的石头，收回你的山，拿走什么都行。只要把她还给我！",
        "别喊。这里的声音传不远。山顶的天会开吗？这下面永远是灰烬的颜色。你上去的时候，回来给我讲讲星星。",
      ],
      [
        "会开。有星星。七姐妹：昴星团。其中一颗比别的暗。",
        "为什么暗？",
        "她嫁给了一个凡人。人们说，她因为羞愧，收敛了自己的光。她的名字叫墨洛珀。",
        "墨洛珀……我的名字。一个凡人……你就是那个凡人吗？",
        "别回答。一听到「科林斯」，我心里就隐隐作痛，却不知道为什么。这里的空气从来不动。从山顶给我带些风来。",
      ],
      [
        "山顶的风从海上吹来。带着盐的味道。科林斯有两片海，我们的宫殿面朝着两片海。",
        "我从前总是从海上回家。每天早晨，你站在东门，数着进港的船。",
        "船……我在等一个人。总是在等一个人。",
        "等我想起是谁，我会告诉你。可那些早晨，好像从来没有结束过。",
      ],
      [
        "人们说死者不会做梦。你讲了科林斯以后，我做梦了。",
        "一个垂死的男人对我低语：「别埋葬我。无论发生什么，都别埋葬我。」",
        "所以我没有埋。整个科林斯都朝我吐唾沫。他们叫我：那个把丈夫丢给野狗的女人。",
        "那个男人就是你。对吗？",
        "是。",
        "为什么？",
        "为了再骗死神一次。我要告诉冥后，我的妻子没有埋葬我，求她放我回到上面去惩罚她。",
        "那你回来了吗？",
        "我回去了。回去活着。不是回到你身边。",
      ],
      [
        "我现在想起来了：我一直在等你。每天早晨，在城门口。我老了。你一直没有来。",
        "我在活着。从死神那里偷来的每一天，我都想活。",
        "那些日子的代价，是我付的。每一天。",
        "原来记起来是这么沉重。有个传说：山顶后面是勒忒河的源头，一眼泉。",
        "我从来没看过。我眼里只有石头。",
        "去看看。为我去看看。",
      ],
      [
        "它在那儿。就在石头停下的地方再过去一点。泉水静得什么也映不出来。",
        "喝一口，痛就会停。我会再一次忘了你。",
        "别喝。",
        "为什么？为了我，还是为了你？",
      ],
      [
        "你没有回答。我又想起一件事：一个孩子在马群里笑。",
        "格劳科斯。我们的儿子。",
        "他怎么了？",
        "他自己的马在赛会上把他撕碎了。那天我也不在。",
        "你总是在别处。我总是在城门口。",
      ],
      [
        "我要问你一件事，这一次你要说实话。",
        "你欺骗死神的时候，你跟我告别的时候……你有没有想过我会怎么样？",
        "没有。",
        "你对诸神发怒。为了你的石头，为了你的山。石头是他们给的，没错。但那是你挣来的。",
        "这块石头是你自己凿出来的。每说一个谎，就多凿一分。",
        "墨洛珀——",
        "别这样叫我。那个女人是你杀的。勒忒河只是埋葬了她。",
        "墨洛珀转过身去，背对着他。",
      ],
      [
        "今天我站在山顶，没有对诸神喊叫。已经没有人可喊了。只剩我自己。",
        "我对你说过「别喝」。不是为了你。是为了我。",
        "对不起，墨洛珀。我知道这挽回不了什么。可我还是对不起。",
      ],
      [
        "我一直在看着你。你不再诅咒了。",
        "这块石头是我自己放到背上的。诅咒自己的石头……没有意义。",
        "昨夜天空裂开了一瞬。我看见了昴星团。那颗暗星亮了一点。",
        "我曾经怪自己没有埋葬你。可我只是守了我的诺言。我不再羞愧了：不为你，也不为我自己。不久我会向你要一样东西。不会容易。",
      ],
      [
        "西西弗斯。从泉里给我带些水来。亡魂上不了山顶。",
        "你会再一次忘了我。",
        "我知道。但这一次不是为了逃避。是因为我不必再背着这份重担了。",
        "你学会了背负你的石头。我也得学会放下我的重担。",
        "我会再一次失去你。",
        "不。你不会失去我。你会放我走。这两者大不相同。你会带来吗？",
      ],
      [
        "我带来了。",
        "你的手在发抖。推石头的时候，它们从不发抖。",
        "最后再告诉我一件事。一件真的事。",
        "我爱过你。那是一种不好的爱。但那是爱。",
        "我知道。即使我不记得，我也会知道。……给我吧。",
        "她喝下了水。长久的沉默。",
        "旅人……这座山有尽头吗？",
        "没有。但这条路很美。",
        "墨洛珀穿过灰烬色的平原走远了。她一次也没有回头。西西弗斯走下山，回到他的石头旁。",
      ],
    ],
    bring: "我去取水",
    notYet: "还不是时候",
    notYetReplies: [
      "那就明天吧。不急。在这里，我们唯一拥有的就是时间。",
      "我知道你想要什么：多记住我一会儿。记得是爱，西西弗斯。抓住不放不是。",
      "石头在等。我也在等。我们都很擅长这个。",
    ],
    fillers: [
      [
        "又是你。今天石头重吗？",
        "我忘了自己的名字，却记得自己的手。我想我从前是织布的。",
        "你下山的时候，看看我的脸。也许我会想起什么。",
      ],
      [
        "今天我不想说话。",
        "山的影子拉得越长，我记起的就越多。",
        "格劳科斯的笑声还在我耳边。真希望连这个也忘了。",
      ],
      [
        "你的路很美，西西弗斯。从来没有人告诉过你吗？",
        "你推石头的时候，脸变了。你不再愤怒了。",
        "泉水会等。别着急。",
      ],
    ],
    turned: ["……", "走吧，西西弗斯。", "你的石头在等你。"],
    companions: {
      pro: {
        meetings: [
          [
            "你在看什么？等老鹰来了，好戏才开场。",
            "普罗米修斯。我把火给了人类。宙斯把他所有的惩罚都排在这一道山坡上。我的是这块岩石，和那只鸟。",
          ],
          [
            "我看见路下面那个女人了。她喝了遗忘之水。",
            "她是我的妻子。",
            "那么遗忘不是她的惩罚，西西弗斯。是你的。",
          ],
          [
            "你推石头的时候，为什么眼睛还盯着山顶？",
            "不然该看哪里？",
            "看你的手。这件事里，只有它们是你的。",
          ],
          [
            "今天老鹰吃掉了我半个肝。你愿意替我背着这份痛，爬一趟吗？我想知道，痛在山顶有没有重量。",
          ],
          [
            "你背上去了。有一瞬间我觉得轻了；我早已忘了那是什么感觉。我的一点火星现在归你了：你的气息会回来得更快。",
            "下一次攀登时，我锁链的末端会替你挡住石头。一次。",
          ],
          [
            "你的石头至少还在动。我的锁链永远不会断。",
            "那你的更轻松。你只需要等。",
            "不。等待就是什么也做不了。你至少能选择怎么推。",
          ],
          [
            "我把火给人类，好让他们在黑暗中看得见。你为那个亡魂留下了一团火，西西弗斯。在那下面，有人第一次感到了温暖。",
          ],
        ],
        idle: [
          "老鹰今天来晚了。我几乎有点想它。",
          "推吧，兄弟。我看着呢。",
          "别看山顶。看你的手。",
        ],
        declined: "那我自己背。我一直都是自己背。",
        accepted: "已经更重了，是吧？别停。",
      },
      tan: {
        meetings: [
          [
            "水没到我的下巴，果子挂在我头顶。我一弯腰，水就退去。我一伸手，树枝就升高。",
            "我请诸神赴宴，煮了自己的儿子来试探他们。所以我又饥又渴。那你呢？",
          ],
          ["你愿意替我背着这份渴，爬一趟吗？如果到了山顶能缓解，那诸神终究还有些慈悲。"],
          [
            "没有缓解，对吧？我早知道。但有一阵子，我忘了它。拿我的耐心作为回报吧：从今以后，滑落时你会少退一些。",
            "下一次攀登时，我会在你的石头后面垫一块石楔。一切都会滑落；这我最清楚。但总有一些会留下。",
          ],
          [
            "听说那个女人说起一个孩子。格劳科斯。",
            "他自己的马把他撕碎了。我不在场。",
            "我亲手把自己的儿子摆上餐桌，而我就在场。我不知道我们谁是更坏的父亲。但你至少感到羞愧。",
          ],
          [
            "我看了看那个女人。她背对着你，却在寻找什么。",
            "找我。",
            "不。她在找一种不再找你的办法。这不是一回事。",
          ],
          [
            "我现在懂你了。你推，不是为了到达。",
            "那是为了什么？",
            "为了推。我伸手，从来只为了占有。那不是我的惩罚。那是我的过错。",
          ],
          ["如果我不再渴望，水还会逃走吗？哪天替我问问山顶。"],
          ["今天水停在了我的下巴边。我没有伸手。它没有逃走。有那么一瞬。"],
        ],
        idle: [
          "喝过自己的汗吗？咸的。可那也是水。",
          "继续走。这里总得有人动一动。",
          "无花果又熟了。当然了。",
        ],
        declined: "当然。谁会想要呢？",
        accepted: "感觉到了吗？对我来说，每一刻都是这样。",
      },
    },
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
    items: {
      stars: "कृत्तिका",
      wind: "समुद्र से आती हवा",
      spring: "विस्मृति का झरना",
      lethe: "लेथी का पानी, उसकी हथेलियों में",
    },
    episodes: [
      [
        "मुसाफ़िर… क्या इस पहाड़ का कोई अंत है?",
        "…मेरोपी?",
        "मेरोपी। सुंदर नाम है। क्या यह मेरा है?",
        "तुम्हारा। कोरिंथ में चालीस बरस मैंने तुम्हें इसी नाम से पुकारा।",
        "मुझे याद नहीं। यहाँ जो भी आता है, नदी का पानी पीता है। मैंने भी पिया होगा।",
        "लेथी। उन्होंने तुम्हें भी मुझसे चुरा लिया।",
        "किसी ने मुझे नहीं चुराया। बस जहाँ मेरा नाम होना चाहिए, वहाँ एक ख़ालीपन है।",
        "ज़्यूस! अपना पत्थर वापस ले लो, अपना पहाड़ वापस ले लो, जो चाहो ले लो। बस उसे मुझे लौटा दो!",
        "चिल्लाओ मत। यहाँ आवाज़ें दूर तक नहीं जातीं। क्या चोटी पर आसमान खुलता है? यहाँ नीचे सब कुछ हमेशा राख-सा धूसर है। ऊपर जाओ, तो मुझे तारों के बारे में बताना।",
      ],
      [
        "खुलता है। तारे हैं। सात बहनें: कृत्तिका। उनमें से एक बाक़ियों से धुँधली है।",
        "धुँधली क्यों?",
        "उसने एक मनुष्य से ब्याह किया। कहते हैं, लाज के मारे उसने अपनी रोशनी मंद कर ली। उसका नाम मेरोपी है।",
        "मेरोपी… मेरा नाम। एक मनुष्य… क्या तुम वही मनुष्य हो?",
        "जवाब मत दो। “कोरिंथ” सुनती हूँ तो भीतर कुछ दुखता है, और मुझे नहीं पता क्यों। यहाँ हवा कभी नहीं हिलती। चोटी से मेरे लिए हवा ले आना।",
      ],
      [
        "चोटी पर हवा समुद्र से आती है। उसमें नमक की गंध है। कोरिंथ के दो समुद्र थे, और हमारा महल दोनों की ओर देखता था।",
        "मैं समुद्र के रास्ते घर लौटता था। तुम हर सुबह पूर्वी द्वार पर खड़ी होकर बंदरगाह में आते जहाज़ गिनती थीं।",
        "जहाज़… मैं किसी की राह देख रही थी। हमेशा किसी की।",
        "जब याद आएगा कि किसकी, तो तुम्हें बताऊँगी। पर वे सुबहें ऐसी लगती हैं जैसे कभी ख़त्म ही न हुई हों।",
      ],
      [
        "कहते हैं मुर्दे सपने नहीं देखते। तुमने कोरिंथ के बारे में बताया, उसके बाद मैंने देखा।",
        "मृत्यु-शय्या पर एक आदमी मुझसे फुसफुसा रहा था: “मुझे दफ़नाना मत। चाहे जो हो, मुझे दफ़नाना मत।”",
        "तो मैंने नहीं दफ़नाया। और पूरा कोरिंथ मुझ पर थूका। वह औरत जिसने अपने पति को कुत्तों के आगे छोड़ दिया, यही कहते थे मुझे।",
        "वह आदमी तुम थे। है न?",
        "हाँ।",
        "क्यों?",
        "मृत्यु को दूसरी बार छलने के लिए। मैं मृतकों की रानी से कहता कि मेरी पत्नी ने मुझे दफ़नाया नहीं, और उसे दंड देने के लिए ऊपर लौटने की भीख माँगता।",
        "और क्या तुम लौटे?",
        "लौटा। जीने के लिए। तुम्हारे पास नहीं।",
      ],
      [
        "अब याद आया: मैं तुम्हारी राह देखती रही। द्वार पर, हर सुबह। मैं बूढ़ी हो गई। तुम कभी नहीं आए।",
        "मैं जी रहा था। मृत्यु से चुराया हर दिन मैं जीना चाहता था।",
        "उन दिनों की क़ीमत मैंने चुकाई। हर एक की।",
        "याद करना इतना भारी होता है। एक अफ़वाह है: चोटी के पीछे लेथी का स्रोत है, एक झरना।",
        "मैंने कभी नहीं देखा। मैं हमेशा बस पत्थर को देखता रहा।",
        "देखो। मेरे लिए देखो।",
      ],
      [
        "वह वहाँ है। जहाँ पत्थर रुकता है, ठीक उसके आगे। पानी इतना ठहरा हुआ है कि उसमें कुछ भी नहीं झलकता।",
        "एक घूँट, और दर्द थम जाएगा। मैं तुम्हें फिर भूल जाऊँगी।",
        "मत पीना।",
        "क्यों? मेरे लिए, या अपने लिए?",
      ],
      [
        "तुमने जवाब नहीं दिया। मुझे कुछ और याद आता है: घोड़ों के बीच हँसता एक बच्चा।",
        "ग्लॉकस। हमारा बेटा।",
        "उसे क्या हुआ?",
        "खेलों में उसके अपने घोड़ों ने उसे चीर डाला। उस दिन भी मैं वहाँ नहीं था।",
        "तुम हमेशा कहीं और होते थे। मैं हमेशा द्वार पर।",
      ],
      [
        "मैं तुमसे कुछ पूछूँगी, और इस बार तुम सच बोलोगे।",
        "जब तुम मृत्यु को छल रहे थे, जब तुमने मुझसे विदा ली… क्या तुमने कभी सोचा कि मेरा क्या होगा?",
        "नहीं।",
        "तुम देवताओं पर क्रोध करते हो। अपने पत्थर के लिए, अपने पहाड़ के लिए। पत्थर उन्होंने दिया, हाँ। पर कमाया तुमने।",
        "यह पत्थर तुमने ख़ुद तराशा। हर झूठ के साथ थोड़ा और।",
        "मेरोपी—",
        "मुझे उस नाम से मत पुकारो। उस औरत को तुमने मारा। लेथी ने तो बस उसे दफ़नाया।",
        "मेरोपी उसकी ओर पीठ फेर लेती है।",
      ],
      [
        "आज मैं चोटी पर खड़ा रहा और देवताओं पर नहीं चिल्लाया। चिल्लाने को कोई बचा नहीं। बस मैं हूँ।",
        "मैंने तुमसे कहा था, “मत पीना।” तुम्हारे लिए नहीं। अपने लिए।",
        "मुझे माफ़ करना, मेरोपी। जानता हूँ, इससे कुछ लौटता नहीं। फिर भी माफ़ी माँगता हूँ।",
      ],
      [
        "मैं तुम्हें देखती रही हूँ। अब तुम कोसते नहीं।",
        "यह पत्थर मैंने ख़ुद अपनी पीठ पर रखा। अपने ही पत्थर को कोसना… बेकार है।",
        "कल रात आसमान एक पल को फटा। मैंने कृत्तिका देखी। वह धुँधला तारा थोड़ा ज़्यादा चमक रहा था।",
        "मैं ख़ुद को दोष देती थी कि मैंने तुम्हें दफ़नाया नहीं। पर मैंने तो बस अपना वचन निभाया था। अब मुझे लाज नहीं: न तुमसे, न ख़ुद से। जल्द ही मैं तुमसे कुछ माँगूँगी। आसान नहीं होगा।",
      ],
      [
        "सिसिफ़स। झरने से मेरे लिए पानी ले आओ। छायाएँ ऊपर नहीं चढ़ सकतीं।",
        "तुम मुझे फिर भूल जाओगी।",
        "जानती हूँ। पर इस बार भागने के लिए नहीं। इसलिए कि अब मुझे यह बोझ और नहीं ढोना।",
        "तुमने अपना पत्थर ढोना सीख लिया। मुझे अपना बोझ उतारना सीखना है।",
        "मैं तुम्हें फिर खो दूँगा।",
        "नहीं। तुम मुझे खोओगे नहीं। तुम मुझे जाने दोगे। दोनों में बड़ा फ़र्क़ है। लाओगे?",
      ],
      [
        "ले आया।",
        "तुम्हारे हाथ काँप रहे हैं। पत्थर धकेलते हुए वे कभी नहीं काँपते।",
        "मुझे एक आख़िरी बात बताओ। कुछ सच।",
        "मैंने तुमसे प्रेम किया। वह बुरा प्रेम था। पर प्रेम था।",
        "जानती हूँ। याद न भी रहे, तब भी जानूँगी। …मुझे दो।",
        "वह पानी पी लेती है। एक लंबी चुप्पी।",
        "मुसाफ़िर… क्या इस पहाड़ का कोई अंत है?",
        "नहीं। पर रास्ता सुंदर है।",
        "मेरोपी राख-से धूसर मैदान के पार चली जाती है। एक बार भी पीछे मुड़कर नहीं देखती। सिसिफ़स अपने पत्थर के पास उतर जाता है।",
      ],
    ],
    bring: "मैं पानी लाऊँगा",
    notYet: "अभी नहीं",
    notYetReplies: [
      "तो कल सही। कोई जल्दी नहीं। यहाँ हमारे पास बस समय ही है।",
      "मैं जानती हूँ तुम क्या चाहते हो: मुझे थोड़ा और याद रखना। याद रखना प्रेम है, सिसिफ़स। पकड़े रहना नहीं।",
      "पत्थर प्रतीक्षा करता है। मैं भी। हम दोनों इसमें माहिर हैं।",
    ],
    fillers: [
      [
        "फिर तुम। आज पत्थर भारी है?",
        "अपना नाम भूल गई हूँ, पर अपने हाथ याद हैं। शायद मैं बुनती थी।",
        "नीचे लौटो तो मेरा चेहरा देखना। शायद मुझे कुछ याद आ जाए।",
      ],
      [
        "आज मैं बात नहीं करना चाहती।",
        "पहाड़ की परछाईं जितनी लंबी होती है, उतना ज़्यादा मुझे याद आता है।",
        "ग्लॉकस की हँसी मेरे कानों में है। काश वह भी भूल जाती।",
      ],
      [
        "तुम्हारा रास्ता सुंदर है, सिसिफ़स। किसी ने तुमसे कभी कहा नहीं?",
        "पत्थर धकेलते हुए तुम्हारा चेहरा बदल जाता है। अब तुम क्रोधित नहीं हो।",
        "झरना प्रतीक्षा करेगा। जल्दी मत करो।",
      ],
    ],
    turned: ["…", "जाओ, सिसिफ़स।", "तुम्हारा पत्थर राह देख रहा है।"],
    companions: {
      pro: {
        meetings: [
          [
            "क्या घूर रहे हो? असली तमाशा तो उक़ाब के आने पर शुरू होता है।",
            "प्रोमेथियस। मैंने मनुष्यों को आग दी। ज़्यूस ने अपनी सारी सज़ाएँ इसी एक ढलान पर सजा दीं। मेरी सज़ा है यह चट्टान, और वह पंछी।",
          ],
          [
            "मैंने रास्ते के नीचे उस औरत को देखा। उसने विस्मृति का पानी पी लिया है।",
            "वह मेरी पत्नी है।",
            "तब भूलना उसकी सज़ा नहीं है, सिसिफ़स। तुम्हारी है।",
          ],
          [
            "तुम अब भी चोटी पर नज़र गड़ाए क्यों धकेलते हो?",
            "और कहाँ देखूँ?",
            "अपने हाथों को। इस सब में बस वही तुम्हारे हैं।",
          ],
          [
            "आज उक़ाब मेरा आधा जिगर ले गया। क्या तुम एक चढ़ाई भर यह दर्द ढोओगे? मैं जानना चाहता हूँ कि चोटी पर दर्द का कोई वज़न होता है या नहीं।",
          ],
          [
            "तुमने ढोया। एक पल को मैं हल्का हुआ; मैं भूल चुका था कि वह क्या होता है। मेरी आग की एक चिंगारी अब तुम्हारी है: तुम्हारी साँस जल्दी लौटेगी।",
            "और तुम्हारी अगली चढ़ाई में, मेरी ज़ंजीर का सिरा तुम्हारे पत्थर को थाम लेगा। एक बार।",
          ],
          [
            "तुम्हारा पत्थर कम से कम हिलता तो है। मेरी ज़ंजीरें कभी नहीं टूटतीं।",
            "तो तुम्हारी सज़ा आसान है। तुम्हें बस इंतज़ार करना है।",
            "नहीं। इंतज़ार का मतलब है कुछ भी न कर पाना। तुम कम से कम चुनते हो कि कैसे धकेलना है।",
          ],
          [
            "मैंने मनुष्यों को आग दी ताकि वे अँधेरे में देख सकें। तुमने उस छाया के लिए एक आग छोड़ दी, सिसिफ़स। वहाँ नीचे कोई पहली बार गर्माहट महसूस कर रहा है।",
          ],
        ],
        idle: [
          "आज उक़ाब देर से आया। मुझे लगभग उसकी याद आने लगी थी।",
          "धकेलो, भाई। मैं देख रहा हूँ।",
          "चोटी को नहीं। अपने हाथों को।",
        ],
        declined: "तो मैं ख़ुद ढो लूँगा। हमेशा ढोया है।",
        accepted: "अभी से भारी हो गया, है न? रुको मत।",
      },
      tan: {
        meetings: [
          [
            "पानी मेरी ठोड़ी तक, फल मेरे सिर के ऊपर। झुकता हूँ तो पानी उतर जाता है। हाथ बढ़ाता हूँ तो डाल ऊपर उठ जाती है।",
            "मैंने देवताओं को अपनी दावत पर बुलाया और उन्हें परखने के लिए अपने ही बेटे को पका दिया। इसीलिए मैं भूखा-प्यासा हूँ। और तुम?",
          ],
          [
            "क्या तुम एक चढ़ाई भर मेरी प्यास ढोओगे? अगर चोटी पर वह कम हो, तो आख़िर देवताओं में कुछ दया है।",
          ],
          [
            "कम नहीं हुई, है न? मैं जानता था। पर कुछ देर मैं उसे भूल गया। बदले में मेरा धैर्य ले लो: अब से फिसलने पर तुम कम पीछे जाओगे।",
            "और तुम्हारी अगली चढ़ाई में मैं तुम्हारे पत्थर के पीछे एक टेक लगा दूँगा। सब कुछ फिसलता है; यह मैं ख़ूब जानता हूँ। पर कुछ टिका रहता है।",
          ],
          [
            "कहते हैं वह औरत एक बच्चे की बात कर रही थी। ग्लॉकस।",
            "उसके अपने घोड़ों ने उसे चीर डाला। मैं वहाँ नहीं था।",
            "मैंने अपने ही हाथों से अपने बेटे को थाली में परोसा, और मैं वहाँ था। पता नहीं हम दोनों में बुरा पिता कौन था। पर तुम कम से कम लज्जित तो हो।",
          ],
          [
            "मैंने उस औरत को देखा। उसकी पीठ तुम्हारी ओर है, पर वह कुछ खोज रही है।",
            "मुझे।",
            "नहीं। तुम्हें खोजना बंद करने का कोई रास्ता। दोनों एक बात नहीं हैं।",
          ],
          [
            "अब मैं तुम्हें समझता हूँ। तुम पहुँचने के लिए नहीं धकेलते।",
            "तो फिर किसलिए?",
            "धकेलने के लिए। मैंने हमेशा पाने के लिए ही हाथ बढ़ाया। वह मेरी सज़ा नहीं थी। वह मेरी भूल थी।",
          ],
          ["अगर मैं चाहना छोड़ दूँ, तो क्या पानी फिर भी भागेगा? किसी दिन मेरे लिए चोटी से पूछना।"],
          ["आज पानी मेरी ठोड़ी पर ठहरा रहा। मैंने हाथ नहीं बढ़ाया। वह नहीं भागा। एक पल के लिए।"],
        ],
        idle: [
          "कभी अपना पसीना पिया है? नमकीन। फिर भी पानी।",
          "चलते रहो। यहाँ किसी को तो हिलना चाहिए।",
          "अंजीर फिर पक गए हैं। ज़ाहिर है।",
        ],
        declined: "ज़ाहिर है। कौन चाहेगा?",
        accepted: "महसूस हो रहा है? मेरे लिए हर पल ऐसा ही है।",
      },
    },
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
    items: {
      stars: "las Pléyades",
      wind: "el viento del mar",
      spring: "el manantial del olvido",
      lethe: "el agua del Leteo, en sus manos",
    },
    episodes: [
      [
        "Viajero… ¿tiene fin esta colina?",
        "…¿Mérope?",
        "Mérope. Un nombre hermoso. ¿Es mío?",
        "Tuyo. En Corinto te llamé así durante cuarenta años.",
        "No lo recuerdo. Todos los que llegan aquí beben del río. Yo también debí de beber.",
        "El Leteo. También a ti te arrancaron de mi lado.",
        "Nadie me arrancó de ningún sitio. Solo hay un vacío donde debería estar mi nombre.",
        "¡Zeus! Quédate con tu piedra, quédate con tu colina, llévate lo que quieras. ¡Pero devuélvemela!",
        "No grites. Aquí las voces no llegan lejos. ¿Se abre el cielo allá arriba? Aquí abajo siempre es gris ceniza. Cuando subas, háblame de las estrellas.",
      ],
      [
        "Se abre. Hay estrellas. Siete hermanas: las Pléyades. Una brilla menos que las demás.",
        "¿Por qué menos?",
        "Se casó con un mortal. Dicen que apagó su luz de vergüenza. Se llama Mérope.",
        "Mérope… Mi nombre. Un mortal… ¿Eres tú ese mortal?",
        "No contestes. Cuando oigo “Corinto” algo me duele por dentro, y no sé por qué. Aquí el aire nunca se mueve. Tráeme el viento de la cima.",
      ],
      [
        "Arriba el viento viene del mar. Huele a sal. Corinto tenía dos mares, y nuestro palacio miraba a los dos.",
        "Yo volvía a casa por mar. Cada mañana te quedabas en la puerta del este contando los barcos que entraban en el puerto.",
        "Los barcos… Esperaba a alguien. Siempre a alguien.",
        "Cuando recuerde a quién, te lo diré. Pero esas mañanas parecen no haber terminado nunca.",
      ],
      [
        "Dicen que los muertos no sueñan. Después de que me hablaras de Corinto, soñé.",
        "Un hombre en su lecho de muerte me susurraba: “No me entierres. Pase lo que pase, no me entierres.”",
        "Y no lo enterré. Y todo Corinto me escupió. La mujer que dejó a su marido a los perros, me llamaban.",
        "Ese hombre eras tú. ¿Verdad?",
        "Sí.",
        "¿Por qué?",
        "Para engañar a la Muerte por segunda vez. Le diría a la reina de los muertos que mi esposa no me había enterrado, y le suplicaría volver arriba para castigarla.",
        "¿Y volviste?",
        "Volví. A vivir. No a ti.",
      ],
      [
        "Ahora lo recuerdo: te esperé. En la puerta, cada mañana. Envejecí. Nunca llegaste.",
        "Estaba viviendo. Quería vivir cada día que le había robado a la Muerte.",
        "Esos días los pagué yo. Todos y cada uno.",
        "Recordar pesa tanto. Hay un rumor: detrás de la cima está la fuente del Leteo, un manantial.",
        "Nunca miré. Solo miraba la piedra.",
        "Mira. Mira por mí.",
      ],
      [
        "Está ahí. Justo pasado el lugar donde se detiene la piedra. El agua está tan quieta que no refleja nada.",
        "Un sorbo, y el dolor cesaría. Volvería a olvidarte.",
        "No bebas.",
        "¿Por qué? ¿Por mí, o por ti?",
      ],
      [
        "No me has contestado. Recuerdo otra cosa: un niño riendo entre caballos.",
        "Glauco. Nuestro hijo.",
        "¿Qué le pasó?",
        "Sus propios caballos lo despedazaron, en los juegos. Ese día tampoco estaba yo.",
        "Tú siempre estabas en otra parte. Yo siempre en la puerta.",
      ],
      [
        "Voy a preguntarte algo, y esta vez me dirás la verdad.",
        "Cuando engañabas a la Muerte, cuando te despediste de mí… ¿pensaste alguna vez qué sería de mí?",
        "No.",
        "Te enfureces con los dioses. Por tu piedra, por tu colina. Ellos te dieron la piedra, sí. Pero tú te la ganaste.",
        "Esta piedra la tallaste tú. Un poco más con cada mentira.",
        "Mérope—",
        "No me llames así. A esa mujer la mataste tú. El Leteo solo la enterró.",
        "Mérope le da la espalda.",
      ],
      [
        "Hoy me detuve en la cima y no les grité a los dioses. Ya no queda nadie a quien gritar. Solo quedo yo.",
        "Te dije “No bebas”. No por ti. Por mí.",
        "Lo siento, Mérope. Sé que no devuelve nada. Lo siento de todos modos.",
      ],
      [
        "Te he estado observando. Ya no maldices.",
        "Esta piedra me la cargué yo a la espalda. Maldecir tu propia piedra es… inútil.",
        "Anoche el cielo se abrió un instante. Vi las Pléyades. La estrella pálida brillaba un poco más.",
        "Me culpaba por no haberte enterrado. Pero solo había cumplido mi palabra. Ya no me avergüenzo: ni de ti, ni de mí. Pronto te pediré algo. No será fácil.",
      ],
      [
        "Sísifo. Tráeme agua del manantial. Las sombras no pueden subir.",
        "Volverás a olvidarme.",
        "Lo sé. Pero esta vez no es para huir. Es porque ya no tengo que cargar con este peso.",
        "Tú aprendiste a cargar tu piedra. Yo tengo que aprender a soltar mi carga.",
        "Volveré a perderte.",
        "No. No me perderás. Me dejarás ir. Hay una gran diferencia. ¿Me la traerás?",
      ],
      [
        "La he traído.",
        "Te tiemblan las manos. Nunca tiemblan cuando empujas la piedra.",
        "Dime una última cosa. Algo verdadero.",
        "Te amé. Fue un amor malo. Pero fue amor.",
        "Lo sé. Aunque no lo recuerde, lo sabré. …Dámela.",
        "Ella bebe. Un largo silencio.",
        "Viajero… ¿tiene fin esta colina?",
        "No. Pero el camino es hermoso.",
        "Mérope se aleja por la llanura gris ceniza. No mira atrás ni una sola vez. Sísifo baja hacia su piedra.",
      ],
    ],
    bring: "Traeré el agua",
    notYet: "Todavía no",
    notYetReplies: [
      "Mañana, entonces. No hay prisa. El tiempo es lo único que tenemos aquí.",
      "Sé lo que quieres: recordarme más tiempo. Recordar es amar, Sísifo. Aferrarse no.",
      "La piedra espera. Yo también. A los dos se nos da bien.",
    ],
    fillers: [
      [
        "Tú otra vez. ¿Pesa hoy la piedra?",
        "He olvidado mi nombre, pero recuerdo mis manos. Creo que tejía.",
        "Mírame a la cara cuando bajes. Quizá recuerde algo.",
      ],
      [
        "Hoy no quiero hablar.",
        "Cuanto más se alarga la sombra de la colina, más recuerdo.",
        "Tengo la risa de Glauco en los oídos. Ojalá también la hubiera olvidado.",
      ],
      [
        "Tu camino es hermoso, Sísifo. ¿Nunca te lo ha dicho nadie?",
        "Te cambia la cara cuando empujas la piedra. Ya no estás enfadado.",
        "El manantial esperará. No te apresures.",
      ],
    ],
    turned: ["…", "Vete, Sísifo.", "Tu piedra te espera."],
    companions: {
      pro: {
        meetings: [
          [
            "¿Qué miras? El espectáculo de verdad empieza cuando llega el águila.",
            "Prometeo. Les di el fuego a los hombres. Zeus colocó todos sus castigos a lo largo de esta misma ladera. El mío es esta roca, y ese pájaro.",
          ],
          [
            "Vi a la mujer, camino abajo. Ha bebido el agua del olvido.",
            "Es mi esposa.",
            "Entonces el olvido no es su castigo, Sísifo. Es el tuyo.",
          ],
          [
            "¿Por qué sigues empujando con los ojos en la cima?",
            "¿Adónde quieres que mire?",
            "A tus manos. Son lo único de todo esto que es tuyo.",
          ],
          [
            "Hoy el águila se llevó la mitad de mi hígado. ¿Cargarás con ese dolor durante una subida? Quiero saber si el dolor pesa algo en la cima.",
          ],
          [
            "Lo cargaste. Por un momento me sentí más ligero; había olvidado lo que era. Una chispa de mi fuego es tuya ahora: recuperarás el aliento más deprisa.",
            "Y en tu próxima subida, el extremo de mi cadena detendrá tu piedra. Una vez.",
          ],
          [
            "Tu piedra al menos se mueve. Mis cadenas nunca se rompen.",
            "Entonces lo tuyo es más fácil. Solo tienes que esperar.",
            "No. Esperar es no poder hacer nada. Tú, al menos, eliges cómo empujar.",
          ],
          [
            "Les di el fuego a los hombres para que vieran en la oscuridad. Tú le dejaste un fuego a esa sombra, Sísifo. Allá abajo, alguien tiene calor por primera vez.",
          ],
        ],
        idle: [
          "Hoy el águila se retrasa. Casi la echo de menos.",
          "Empuja, hermano. Te estoy mirando.",
          "No la cima. Tus manos.",
        ],
        declined: "Entonces lo cargaré yo. Siempre lo he hecho.",
        accepted: "Ya pesa más, ¿verdad? No te detengas.",
      },
      tan: {
        meetings: [
          [
            "Agua hasta la barbilla, fruta sobre la cabeza. Cuando me inclino, el agua baja. Cuando alargo la mano, la rama sube.",
            "Invité a los dioses a mi mesa y cociné a mi propio hijo para ponerlos a prueba. Por eso tengo hambre y sed. ¿Y tú?",
          ],
          [
            "¿Cargarás con mi sed durante una subida? Si en la cima se calma, es que los dioses tienen algo de piedad, después de todo.",
          ],
          [
            "No se calmó, ¿verdad? Lo sabía. Pero durante un rato la olvidé. Toma a cambio mi paciencia: desde ahora, un resbalón te costará menos terreno.",
            "Y en tu próxima subida calzaré tu piedra con otra. Todo resbala; eso lo sé bien. Pero algo se queda.",
          ],
          [
            "Dicen que la mujer habló de un niño. Glauco.",
            "Sus propios caballos lo despedazaron. Yo no estaba.",
            "Yo serví a mi propio hijo en la mesa con mis propias manos, y estaba allí. No sé cuál de los dos fue peor padre. Pero tú, al menos, te avergüenzas.",
          ],
          [
            "Miré a la mujer. Te da la espalda, pero busca algo.",
            "A mí.",
            "No. Una manera de dejar de buscarte. No es lo mismo.",
          ],
          [
            "Ahora te entiendo. No empujas para llegar.",
            "¿Entonces para qué?",
            "Para empujar. Yo solo alargué la mano para tener. Ese no fue mi castigo. Fue mi error.",
          ],
          [
            "Si dejara de desear, ¿seguiría huyendo el agua? Pregúntaselo a la cima por mí, algún día.",
          ],
          ["Hoy el agua se quedó en mi barbilla. No alargué la mano. No huyó. Por un momento."],
        ],
        idle: [
          "¿Has bebido alguna vez tu propio sudor? Salado. Pero es agua.",
          "Sigue. Alguien aquí tiene que moverse.",
          "Los higos han vuelto a madurar. Cómo no.",
        ],
        declined: "Claro. ¿Quién iba a quererla?",
        accepted: "¿Lo sientes? Así es cada momento, para mí.",
      },
    },
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
    items: {
      stars: "les Pléiades",
      wind: "le vent de la mer",
      spring: "la source de l'oubli",
      lethe: "l'eau du Léthé, dans ses mains",
    },
    episodes: [
      [
        "Voyageur… cette colline a-t-elle une fin ?",
        "…Mérope ?",
        "Mérope. Un beau nom. Est-ce le mien ?",
        "Le tien. À Corinthe, je t'ai appelée ainsi pendant quarante ans.",
        "Je ne m'en souviens pas. Tous ceux qui viennent ici boivent au fleuve. J'ai dû boire, moi aussi.",
        "Le Léthé. Ils t'ont volée à moi, toi aussi.",
        "Personne ne m'a volée. Il n'y a qu'un vide, là où devrait être mon nom.",
        "Zeus ! Reprends ton rocher, reprends ta colline, prends tout. Mais rends-la-moi !",
        "Ne crie pas. Ici, les voix ne portent pas. Le ciel s'ouvre-t-il, là-haut ? Ici, tout est toujours gris de cendre. Quand tu monteras, parle-moi des étoiles.",
      ],
      [
        "Il s'ouvre. Il y a des étoiles. Sept sœurs : les Pléiades. L'une est plus pâle que les autres.",
        "Pourquoi plus pâle ?",
        "Elle a épousé un mortel. Elle a voilé sa lumière par honte, dit-on. Elle s'appelle Mérope.",
        "Mérope… Mon nom. Un mortel… Est-ce toi, ce mortel ?",
        "Ne réponds pas. Quand j'entends « Corinthe », quelque chose me fait mal, et je ne sais pas pourquoi. Ici, l'air ne bouge jamais. Rapporte-moi le vent du sommet.",
      ],
      [
        "Là-haut, le vent vient de la mer. Il sent le sel. Corinthe avait deux mers, et notre palais regardait les deux.",
        "Je rentrais par la mer. Chaque matin, tu te tenais à la porte de l'est et tu comptais les navires qui entraient au port.",
        "Les navires… J'attendais quelqu'un. Toujours quelqu'un.",
        "Quand je me rappellerai qui, je te le dirai. Mais ces matins-là semblent ne s'être jamais achevés.",
      ],
      [
        "On dit que les morts ne rêvent pas. Après que tu m'as parlé de Corinthe, j'ai rêvé.",
        "Un homme sur son lit de mort me chuchotait : « Ne m'enterre pas. Quoi qu'il arrive, ne m'enterre pas. »",
        "Alors je ne l'ai pas enterré. Et tout Corinthe m'a craché dessus. La femme qui a laissé son mari aux chiens, disait-on de moi.",
        "Cet homme, c'était toi. N'est-ce pas ?",
        "Oui.",
        "Pourquoi ?",
        "Pour tromper la Mort une seconde fois. J'irais dire à la reine des morts que ma femme ne m'avait pas enterré, et je la supplierais de me laisser remonter pour la punir.",
        "Et tu es revenu ?",
        "Je suis remonté. Pour vivre. Pas pour toi.",
      ],
      [
        "Je me souviens maintenant : je t'ai attendu. À la porte, chaque matin. J'ai vieilli. Tu n'es jamais venu.",
        "Je vivais. Je voulais vivre chaque jour que j'avais volé à la Mort.",
        "Ces jours-là, c'est moi qui les ai payés. Chacun d'eux.",
        "Se souvenir pèse si lourd. On raconte que derrière le sommet naît le Léthé, d'une source.",
        "Je n'ai jamais regardé. Je ne regardais que le rocher.",
        "Regarde. Regarde pour moi.",
      ],
      [
        "Elle est là. Juste après l'endroit où le rocher s'arrête. L'eau est si immobile qu'elle ne reflète rien.",
        "Une gorgée, et la douleur cesserait. Je t'oublierais de nouveau.",
        "Ne bois pas.",
        "Pourquoi ? Pour moi, ou pour toi ?",
      ],
      [
        "Tu n'as pas répondu. Je me souviens d'autre chose : un enfant qui rit parmi les chevaux.",
        "Glaucos. Notre fils.",
        "Qu'est-il devenu ?",
        "Ses propres chevaux l'ont mis en pièces, aux jeux. Ce jour-là non plus, je n'étais pas là.",
        "Tu étais toujours ailleurs. J'étais toujours à la porte.",
      ],
      [
        "Je vais te demander quelque chose, et cette fois tu me diras la vérité.",
        "Quand tu trompais la Mort, quand tu m'as dit adieu… as-tu jamais pensé à ce que je deviendrais ?",
        "Non.",
        "Tu en veux aux dieux. Pour ton rocher, pour ta colline. Ils t'ont donné le rocher, oui. Mais tu l'as mérité.",
        "Ce rocher, tu l'as taillé toi-même. Un peu plus à chaque mensonge.",
        "Mérope—",
        "Ne m'appelle pas ainsi. Cette femme, c'est toi qui l'as tuée. Le Léthé n'a fait que l'enterrer.",
        "Mérope lui tourne le dos.",
      ],
      [
        "Aujourd'hui, je me suis tenu au sommet et je n'ai pas crié contre les dieux. Il n'y a plus personne contre qui crier. Il n'y a que moi.",
        "Je t'ai dit « Ne bois pas ». Pas pour toi. Pour moi.",
        "Je te demande pardon, Mérope. Je sais que cela ne ramène rien. Je te le demande quand même.",
      ],
      [
        "Je t'observe depuis un moment. Tu ne maudis plus.",
        "Ce rocher, je l'ai mis moi-même sur mon dos. Maudire son propre rocher… c'est vain.",
        "Cette nuit, le ciel s'est déchiré un instant. J'ai vu les Pléiades. L'étoile pâle brillait un peu plus.",
        "Je me reprochais de ne pas t'avoir enterré. Mais je n'avais fait que tenir parole. Je n'ai plus honte : ni de toi, ni de moi. Bientôt, je te demanderai quelque chose. Ce ne sera pas facile.",
      ],
      [
        "Sisyphe. Rapporte-moi de l'eau de la source. Les ombres ne peuvent pas monter.",
        "Tu m'oublieras encore.",
        "Je sais. Mais pas pour fuir, cette fois. Parce que je n'ai plus à porter ce poids.",
        "Tu as appris à porter ton rocher. Je dois apprendre à poser mon fardeau.",
        "Je vais te perdre encore.",
        "Non. Tu ne me perdras pas. Tu me laisseras partir. Ce n'est pas du tout la même chose. Tu me l'apporteras ?",
      ],
      [
        "Je l'ai apportée.",
        "Tes mains tremblent. Elles ne tremblent jamais quand tu pousses le rocher.",
        "Dis-moi une dernière chose. Une chose vraie.",
        "Je t'ai aimée. C'était un mauvais amour. Mais c'était de l'amour.",
        "Je sais. Même si je ne m'en souviens pas, je le saurai. …Donne.",
        "Elle boit. Un long silence.",
        "Voyageur… cette colline a-t-elle une fin ?",
        "Non. Mais le chemin est beau.",
        "Mérope s'éloigne dans la plaine gris de cendre. Elle ne se retourne pas une seule fois. Sisyphe redescend vers son rocher.",
      ],
    ],
    bring: "J'apporterai l'eau",
    notYet: "Pas encore",
    notYetReplies: [
      "Demain, alors. Rien ne presse. Le temps, ici, c'est tout ce que nous avons.",
      "Je sais ce que tu veux : te souvenir de moi plus longtemps. Se souvenir, c'est aimer, Sisyphe. Retenir, non.",
      "Le rocher attend. Moi aussi. Nous savons faire, tous les deux.",
    ],
    fillers: [
      [
        "Encore toi. Le rocher est lourd, aujourd'hui ?",
        "J'ai oublié mon nom, mais je me souviens de mes mains. Je crois que je tissais.",
        "Regarde mon visage quand tu redescendras. Peut-être que je me souviendrai de quelque chose.",
      ],
      [
        "Je ne veux pas parler aujourd'hui.",
        "Plus l'ombre de la colline s'allonge, plus je me souviens.",
        "J'ai le rire de Glaucos dans les oreilles. J'aurais voulu l'oublier, lui aussi.",
      ],
      [
        "Ton chemin est beau, Sisyphe. Personne ne te l'a jamais dit ?",
        "Ton visage change quand tu pousses le rocher. Tu n'es plus en colère.",
        "La source attendra. Ne te presse pas.",
      ],
    ],
    turned: ["…", "Va, Sisyphe.", "Ton rocher t'attend."],
    companions: {
      pro: {
        meetings: [
          [
            "Qu'est-ce que tu regardes ? Le vrai spectacle commence quand l'aigle arrive.",
            "Prométhée. J'ai donné le feu aux hommes. Zeus a aligné tous ses châtiments sur cette seule pente. Le mien, c'est ce rocher, et cet oiseau.",
          ],
          [
            "J'ai vu la femme, plus bas sur le chemin. Elle a bu l'eau de l'oubli.",
            "C'est ma femme.",
            "Alors l'oubli n'est pas son châtiment, Sisyphe. C'est le tien.",
          ],
          [
            "Pourquoi pousses-tu encore les yeux fixés sur le sommet ?",
            "Où veux-tu que je regarde ?",
            "Tes mains. C'est la seule part de tout ceci qui t'appartienne.",
          ],
          [
            "L'aigle m'a pris la moitié du foie aujourd'hui. Porteras-tu cette douleur, le temps d'une montée ? Je veux savoir si la douleur pèse quelque chose au sommet.",
          ],
          [
            "Tu l'as portée. Un instant, je me suis senti plus léger ; j'avais oublié ce que c'était. Une étincelle de mon feu est à toi maintenant : ton souffle reviendra plus vite.",
            "Et à ta prochaine montée, le bout de ma chaîne retiendra ton rocher. Une fois.",
          ],
          [
            "Ton rocher, au moins, bouge. Mes chaînes ne cèdent jamais.",
            "Alors ton sort est plus facile. Tu n'as qu'à attendre.",
            "Non. Attendre, c'est ne rien pouvoir faire. Toi, au moins, tu choisis comment pousser.",
          ],
          [
            "J'ai donné le feu aux hommes pour qu'ils voient dans le noir. Tu as laissé un feu à cette ombre, Sisyphe. En bas, quelqu'un a chaud pour la première fois.",
          ],
        ],
        idle: [
          "L'aigle est en retard aujourd'hui. Il me manquerait presque.",
          "Pousse, mon frère. Je te regarde.",
          "Pas le sommet. Tes mains.",
        ],
        declined: "Alors je la porterai moi-même. Je l'ai toujours fait.",
        accepted: "Ça pèse déjà plus, hein ? Ne t'arrête pas.",
      },
      tan: {
        meetings: [
          [
            "De l'eau jusqu'au menton, des fruits au-dessus de la tête. Quand je me penche, l'eau baisse. Quand je tends la main, la branche s'élève.",
            "J'ai invité les dieux à ma table et j'ai fait cuire mon propre fils pour les mettre à l'épreuve. Voilà pourquoi j'ai faim et soif. Et toi ?",
          ],
          [
            "Porteras-tu ma soif, le temps d'une montée ? Si elle s'apaise au sommet, c'est que les dieux ont un peu de pitié, après tout.",
          ],
          [
            "Elle ne s'est pas apaisée, hein ? Je le savais. Mais un moment, je l'ai oubliée. Prends ma patience en échange : désormais, une glissade te coûtera moins de terrain.",
            "Et à ta prochaine montée, je calerai une pierre derrière la tienne. Tout glisse ; je le sais bien. Mais une part reste.",
          ],
          [
            "On dit que la femme a parlé d'un enfant. Glaucos.",
            "Ses propres chevaux l'ont mis en pièces. Je n'étais pas là.",
            "Moi, j'ai servi mon propre fils à table, de mes propres mains, et j'étais là. Je ne sais pas lequel de nous deux fut le pire père. Mais toi, au moins, tu as honte.",
          ],
          [
            "J'ai regardé la femme. Elle te tourne le dos, mais elle cherche quelque chose.",
            "Moi.",
            "Non. Un moyen de cesser de te chercher. Ce n'est pas la même chose.",
          ],
          [
            "Je te comprends, maintenant. Tu ne pousses pas pour arriver.",
            "Pour quoi, alors ?",
            "Pour pousser. Moi, je n'ai jamais tendu la main que pour avoir. Ce n'était pas mon châtiment. C'était mon erreur.",
          ],
          [
            "Si je cessais de désirer, l'eau fuirait-elle encore ? Demande-le au sommet pour moi, un jour.",
          ],
          [
            "Aujourd'hui, l'eau est restée à mon menton. Je n'ai pas tendu la main. Elle n'a pas fui. Un instant.",
          ],
        ],
        idle: [
          "Tu as déjà bu ta propre sueur ? Salée. Mais c'est de l'eau.",
          "Continue. Il faut bien que quelqu'un bouge, ici.",
          "Les figues sont de nouveau mûres. Évidemment.",
        ],
        declined: "Évidemment. Qui en voudrait ?",
        accepted: "Tu la sens ? C'est chaque instant, pour moi.",
      },
    },
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
    items: {
      stars: "الثريا",
      wind: "ريح البحر",
      spring: "نبع النسيان",
      lethe: "ماء ليثي، بين كفّيه",
    },
    episodes: [
      [
        "أيها المسافر… هل لهذا التلّ من نهاية؟",
        "…ميروبي؟",
        "ميروبي. اسم جميل. أهو اسمي؟",
        "اسمكِ. في كورنثة ناديتكِ به أربعين عامًا.",
        "لا أذكر. كلّ من يأتي إلى هنا يشرب من النهر. لا بدّ أنني شربت أنا أيضًا.",
        "ليثي. سرقوكِ منّي أنتِ أيضًا.",
        "لم يسرقني أحد. ليس هناك إلا فراغ حيث ينبغي أن يكون اسمي.",
        "يا زيوس! استرجع صخرتك، استرجع تلّك، خذ ما شئت. فقط أعِدها إليّ!",
        "لا تصرخ. الأصوات لا تبلغ بعيدًا هنا. هل تنفتح السماء في الأعلى؟ هنا في الأسفل كلّ شيء رماديّ كالرماد دائمًا. حين تصعد، حدّثني عن النجوم.",
      ],
      [
        "تنفتح. هناك نجوم. سبع أخوات: الثريا. إحداهنّ أخفت من البقية.",
        "لماذا أخفت؟",
        "تزوّجت فانيًا. يقولون إنها أطفأت نورها خجلًا. اسمها ميروبي.",
        "ميروبي… اسمي. فانٍ… أأنت ذلك الفاني؟",
        "لا تُجب. حين أسمع “كورنثة” يوجعني شيء في داخلي، ولا أدري لماذا. الهواء هنا لا يتحرّك أبدًا. أحضر لي الريح من القمة.",
      ],
      [
        "في القمة تهبّ الريح من البحر. رائحتها ملح. كان لكورنثة بحران، وكان قصرنا يطلّ على كليهما.",
        "كنت أعود إلى البيت بحرًا. وكنتِ كلّ صباح تقفين عند البوابة الشرقية وتعدّين السفن الداخلة إلى الميناء.",
        "السفن… كنت أنتظر أحدًا. دائمًا أحدًا.",
        "حين أتذكّر من، سأخبرك. لكن تلك الصباحات تبدو كأنها لم تنتهِ قطّ.",
      ],
      [
        "يقولون إن الموتى لا يحلمون. بعدما حدّثتني عن كورنثة، حلمت.",
        "رجل على فراش الموت كان يهمس لي: “لا تدفنيني. مهما حدث، لا تدفنيني.”",
        "فلم أدفنه. وبصقت عليّ كورنثة كلّها. المرأة التي تركت زوجها للكلاب، هكذا سمّوني.",
        "ذلك الرجل كان أنت. أليس كذلك؟",
        "نعم.",
        "لماذا؟",
        "لأخدع الموت مرة ثانية. كنت سأقول لملكة الموتى إن زوجتي لم تدفنّي، وأتوسّل إليها أن أعود إلى الأعلى لأعاقبها.",
        "وهل عدت؟",
        "عدت. لأعيش. لا إليكِ.",
      ],
      [
        "أتذكّر الآن: انتظرتك. عند البوابة، كلّ صباح. شِخت. ولم تأتِ قطّ.",
        "كنت أعيش. أردت أن أعيش كلّ يوم سرقته من الموت.",
        "أنا التي دفعت ثمن تلك الأيام. يومًا يومًا.",
        "التذكّر ثقيل إلى هذا الحدّ. ثمّة إشاعة: خلف القمة منبع ليثي، نبع ماء.",
        "لم أنظر قطّ. لم أنظر إلا إلى الصخرة.",
        "انظر. انظر من أجلي.",
      ],
      [
        "إنه هناك. بعد الموضع الذي تتوقّف فيه الصخرة بقليل. ماؤه ساكن حتى إنه لا يعكس شيئًا.",
        "رشفة واحدة، ويتوقّف الألم. وأنساك من جديد.",
        "لا تشربي.",
        "لماذا؟ من أجلي، أم من أجلك؟",
      ],
      [
        "لم تُجب. أتذكّر شيئًا آخر: طفلًا يضحك بين الخيول.",
        "غلاوكوس. ابننا.",
        "ماذا جرى له؟",
        "مزّقته خيوله، في الألعاب. ولم أكن هناك ذلك اليوم أيضًا.",
        "كنت دائمًا في مكان آخر. وكنت أنا دائمًا عند البوابة.",
      ],
      [
        "سأسألك شيئًا، وهذه المرة ستقول لي الحقيقة.",
        "حين كنت تخدع الموت، حين ودّعتني… هل فكّرت يومًا فيما سيحلّ بي؟",
        "لا.",
        "أنت غاضب على الآلهة. من أجل صخرتك، من أجل تلّك. هم أعطوك الصخرة، نعم. لكنك استحققتها.",
        "هذه الصخرة نحتّها بيديك. أكثر قليلًا مع كلّ كذبة.",
        "ميروبي—",
        "لا تنادني بهذا الاسم. تلك المرأة قتلتها أنت. ليثي لم يفعل سوى أن دفنها.",
        "تدير ميروبي ظهرها له.",
      ],
      [
        "اليوم وقفت على القمة ولم أصرخ في الآلهة. لم يبقَ أحد أصرخ فيه. لم يبقَ إلا أنا.",
        "قلت لكِ: “لا تشربي.” لا من أجلكِ. من أجلي.",
        "أنا آسف يا ميروبي. أعرف أن هذا لا يعيد شيئًا. لكنني آسف مع ذلك.",
      ],
      [
        "كنت أراقبك. لم تعد تلعن.",
        "أنا من وضع هذه الصخرة على ظهري. أن يلعن المرء صخرته… لا جدوى منه.",
        "البارحة انشقّت السماء لحظة. رأيت الثريا. كانت النجمة الخافتة أسطع قليلًا.",
        "كنت ألوم نفسي لأنني لم أدفنك. لكنني لم أفعل سوى أن وفيت بوعدي. لم أعد أخجل: لا منك، ولا من نفسي. قريبًا سأطلب منك شيئًا. لن يكون سهلًا.",
      ],
      [
        "سيزيف. أحضر لي ماءً من النبع. الظلال لا تستطيع الصعود.",
        "ستنسينني من جديد.",
        "أعرف. لكن ليس هربًا هذه المرة. بل لأنني لم أعد مضطرّة إلى حمل هذا الثقل.",
        "أنت تعلّمت أن تحمل صخرتك. وعليّ أنا أن أتعلّم كيف أضع حملي.",
        "سأفقدكِ من جديد.",
        "لا. لن تفقدني. ستُطلقني. وبين الأمرين فرق كبير. هل ستحضره؟",
      ],
      [
        "أحضرته.",
        "يداك ترتجفان. لا ترتجفان أبدًا وأنت تدفع الصخرة.",
        "قل لي شيئًا أخيرًا. شيئًا صادقًا.",
        "أحببتكِ. كان حبًّا سيّئًا. لكنه كان حبًّا.",
        "أعرف. حتى لو لم أتذكّر، سأعرف. …أعطني إياه.",
        "تشرب. صمت طويل.",
        "أيها المسافر… هل لهذا التلّ من نهاية؟",
        "لا. لكن الطريق جميل.",
        "تمضي ميروبي عبر السهل الرماديّ. لا تلتفت مرة واحدة. ينزل سيزيف إلى صخرته.",
      ],
    ],
    bring: "سأحضر الماء",
    notYet: "ليس بعد",
    notYetReplies: [
      "غدًا إذن. لا عجلة. الوقت هو الشيء الوحيد الذي نملكه هنا.",
      "أعرف ما تريد: أن تتذكّرني أطول. التذكّر حبّ يا سيزيف. أما التشبّث فلا.",
      "الصخرة تنتظر. وأنا أيضًا. كلانا بارع في هذا.",
    ],
    fillers: [
      [
        "أنت مجدّدًا. أثقيلة الصخرة اليوم؟",
        "نسيت اسمي، لكنني أتذكّر يديّ. أظنني كنت أنسج.",
        "انظر إلى وجهي حين تنزل. لعلّي أتذكّر شيئًا.",
      ],
      [
        "لا أريد أن أتكلّم اليوم.",
        "كلّما طال ظلّ التلّ، تذكّرت أكثر.",
        "ضحكة غلاوكوس في أذنيّ. ليتني نسيتها هي أيضًا.",
      ],
      [
        "طريقك جميل يا سيزيف. ألم يقل لك أحد ذلك قطّ؟",
        "يتغيّر وجهك حين تدفع الصخرة. لم تعد غاضبًا.",
        "النبع سينتظر. لا تستعجل.",
      ],
    ],
    turned: ["…", "اذهب يا سيزيف.", "صخرتك تنتظرك."],
    companions: {
      pro: {
        meetings: [
          [
            "إلامَ تحدّق؟ العرض الحقيقي يبدأ حين يأتي النسر.",
            "بروميثيوس. أعطيت البشر النار. صفّ زيوس كلّ عقوباته على هذا المنحدر الواحد. عقوبتي هذه الصخرة، وذلك الطائر.",
          ],
          [
            "رأيت المرأة أسفل الطريق. لقد شربت ماء النسيان.",
            "إنها زوجتي.",
            "إذن فالنسيان ليس عقوبتها يا سيزيف. إنه عقوبتك.",
          ],
          [
            "لماذا ما زلت تدفع وعيناك على القمة؟",
            "وإلى أين أنظر غيرها؟",
            "إلى يديك. هما الشيء الوحيد في هذا كلّه الذي يخصّك.",
          ],
          [
            "أخذ النسر اليوم نصف كبدي. هل تحمل ألمه عنّي، صعودًا واحدًا؟ أريد أن أعرف إن كان للألم وزن في القمة.",
          ],
          [
            "حملته. للحظة شعرت بالخفّة؛ كنت قد نسيت ما هي. شرارة من ناري صارت لك الآن: سيعود إليك نفَسك أسرع.",
            "وفي صعودك القادم، سيمسك طرف سلسلتي صخرتك. مرة واحدة.",
          ],
          [
            "صخرتك على الأقل تتحرّك. سلاسلي لا تنكسر أبدًا.",
            "إذن فقدرك أهون. كلّ ما عليك هو الانتظار.",
            "لا. الانتظار هو العجز عن فعل أيّ شيء. أما أنت فتختار على الأقل كيف تدفع.",
          ],
          [
            "أعطيت البشر النار ليبصروا في الظلام. وأنت تركت نارًا لذلك الظلّ يا سيزيف. هناك في الأسفل، ثمّة من يشعر بالدفء لأول مرة.",
          ],
        ],
        idle: ["تأخّر النسر اليوم. كدت أشتاق إليه.", "ادفع يا أخي. أنا أراقب.", "لا القمة. يداك."],
        declined: "إذن سأحمله بنفسي. لطالما حملته.",
        accepted: "صار أثقل، أليس كذلك؟ لا تتوقّف.",
      },
      tan: {
        meetings: [
          [
            "الماء حتى ذقني، والثمر فوق رأسي. إذا انحنيت انحسر الماء. وإذا مددت يدي ارتفع الغصن.",
            "دعوت الآلهة إلى مائدتي وطبخت ابني لأمتحنهم. لهذا أجوع وأعطش. وأنت؟",
          ],
          [
            "هل تحمل عطشي عنّي، صعودًا واحدًا؟ إن خفّ في القمة، ففي الآلهة شيء من الرحمة بعد كلّ شيء.",
          ],
          [
            "لم يخفّ، أليس كذلك؟ كنت أعرف. لكنني نسيته بعض الوقت. خذ صبري مقابل ذلك: من الآن، ستخسر أرضًا أقلّ حين تنزلق.",
            "وفي صعودك القادم سأدسّ إسفينًا من حجر خلف صخرتك. كلّ شيء ينزلق؛ أعرف هذا جيدًا. لكن بعضه يبقى.",
          ],
          [
            "يقولون إن المرأة تحدّثت عن طفل. غلاوكوس.",
            "مزّقته خيوله. لم أكن هناك.",
            "أنا وضعت ابني على المائدة بيديّ، وكنت هناك. لا أدري أيّنا كان الأب الأسوأ. لكنك على الأقل تخجل.",
          ],
          [
            "نظرت إلى المرأة. ظهرها إليك، لكنها تبحث عن شيء.",
            "عنّي.",
            "لا. عن طريقة تكفّ بها عن البحث عنك. وليس الأمران سواء.",
          ],
          [
            "فهمتك الآن. أنت لا تدفع لتصل.",
            "فلماذا إذن؟",
            "لتدفع. أنا لم أمدّ يدي يومًا إلا لأملك. لم تكن تلك عقوبتي. كانت خطئي.",
          ],
          ["لو كففت عن الرغبة، أكان الماء سيهرب أيضًا؟ اسأل القمة عنّي يومًا ما."],
          ["اليوم بقي الماء عند ذقني. لم أمدّ يدي. ولم يهرب. للحظة."],
        ],
        idle: [
          "هل شربت عرقك يومًا؟ مالح. لكنه ماء.",
          "امضِ. على أحدٍ هنا أن يتحرّك.",
          "نضج التين من جديد. بالطبع.",
        ],
        declined: "بالطبع. ومن يريده؟",
        accepted: "أتشعر به؟ هكذا هي كلّ لحظة عندي.",
      },
    },
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
    items: {
      stars: "কৃত্তিকা",
      wind: "সমুদ্রের হাওয়া",
      spring: "বিস্মৃতির ঝরনা",
      lethe: "লিথির জল, তার আঁজলায়",
    },
    episodes: [
      [
        "পথিক… এই পাহাড়ের কি কোনো শেষ আছে?",
        "…মেরোপি?",
        "মেরোপি। সুন্দর নাম। এটা কি আমার?",
        "তোমার। করিন্থে চল্লিশ বছর আমি তোমাকে এই নামে ডেকেছি।",
        "আমার মনে নেই। এখানে যে-ই আসে, নদীর জল খায়। আমিও নিশ্চয়ই খেয়েছি।",
        "লিথি। ওরা তোমাকেও আমার কাছ থেকে চুরি করে নিয়েছে।",
        "কেউ আমাকে চুরি করেনি। শুধু যেখানে আমার নাম থাকার কথা, সেখানে একটা শূন্যতা।",
        "জিউস! তোমার পাথর ফিরিয়ে নাও, তোমার পাহাড় ফিরিয়ে নাও, যা খুশি নাও। শুধু ওকে আমায় ফিরিয়ে দাও!",
        "চেঁচিয়ো না। এখানে আওয়াজ দূরে যায় না। চূড়ায় কি আকাশ খোলে? এখানে নিচে সবসময় ছাইরঙা। ওপরে উঠলে আমাকে তারাদের কথা বোলো।",
      ],
      [
        "খোলে। তারা আছে। সাত বোন: কৃত্তিকা। একজন বাকিদের চেয়ে ম্লান।",
        "ম্লান কেন?",
        "সে এক মরণশীল মানুষকে বিয়ে করেছিল। লোকে বলে, লজ্জায় সে নিজের আলো কমিয়ে দিয়েছে। তার নাম মেরোপি।",
        "মেরোপি… আমার নাম। এক মরণশীল… তুমিই কি সেই মানুষ?",
        "উত্তর দিয়ো না। “করিন্থ” শুনলে ভেতরে কী যেন ব্যথা করে, জানি না কেন। এখানে বাতাস কখনো নড়ে না। চূড়া থেকে আমার জন্য হাওয়া এনো।",
      ],
      [
        "চূড়ায় হাওয়া আসে সমুদ্র থেকে। নোনা গন্ধ। করিন্থের দুটো সমুদ্র ছিল, আর আমাদের প্রাসাদ দুটোর দিকেই চেয়ে থাকত।",
        "আমি সমুদ্রপথে বাড়ি ফিরতাম। তুমি রোজ সকালে পুবের দরজায় দাঁড়িয়ে বন্দরে ঢোকা জাহাজ গুনতে।",
        "জাহাজ… আমি কারও অপেক্ষায় ছিলাম। সবসময় কারও।",
        "কার, মনে পড়লে তোমাকে বলব। কিন্তু সেই সকালগুলো যেন কখনো শেষই হয়নি।",
      ],
      [
        "লোকে বলে মৃতেরা স্বপ্ন দেখে না। তুমি করিন্থের কথা বলার পর আমি দেখেছি।",
        "মৃত্যুশয্যায় একজন মানুষ আমাকে ফিসফিস করে বলছিল: “আমাকে সমাধি দিয়ো না। যা-ই ঘটুক, সমাধি দিয়ো না।”",
        "তাই আমি দিইনি। আর গোটা করিন্থ আমার গায়ে থুতু দিয়েছিল। যে মেয়ে স্বামীকে কুকুরের মুখে ফেলে রাখে, এই বলে ডাকত আমায়।",
        "সেই মানুষটা তুমি ছিলে। তাই না?",
        "হ্যাঁ।",
        "কেন?",
        "মৃত্যুকে দ্বিতীয়বার ফাঁকি দিতে। আমি মৃতদের রানিকে বলতাম, আমার স্ত্রী আমাকে সমাধি দেয়নি, আর তাকে শাস্তি দিতে ওপরে ফিরে যাওয়ার অনুমতি ভিক্ষা চাইতাম।",
        "আর তুমি কি ফিরেছিলে?",
        "ফিরেছিলাম। বাঁচতে। তোমার কাছে নয়।",
      ],
      [
        "এখন মনে পড়ছে: আমি তোমার অপেক্ষা করেছি। দরজায়, রোজ সকালে। আমি বুড়ি হয়ে গেলাম। তুমি কখনো এলে না।",
        "আমি বাঁচছিলাম। মৃত্যুর কাছ থেকে চুরি করা প্রতিটা দিন আমি বাঁচতে চেয়েছিলাম।",
        "সেই দিনগুলোর দাম আমি দিয়েছি। প্রত্যেকটার।",
        "মনে পড়া এত ভারী। একটা গুজব আছে: চূড়ার ওপারে লিথির উৎস, একটা ঝরনা।",
        "আমি কখনো দেখিনি। আমি শুধু পাথরের দিকেই তাকিয়েছি।",
        "দেখো। আমার জন্য দেখো।",
      ],
      [
        "আছে। পাথর যেখানে থামে, ঠিক তার পরেই। জল এত স্থির যে তাতে কিছুরই ছায়া পড়ে না।",
        "এক চুমুক, আর ব্যথা থেমে যাবে। আমি তোমাকে আবার ভুলে যাব।",
        "খেয়ো না।",
        "কেন? আমার জন্য, না তোমার জন্য?",
      ],
      [
        "তুমি উত্তর দিলে না। আরেকটা কিছু মনে পড়ছে: ঘোড়াদের মাঝে একটা শিশু হাসছে।",
        "গ্লকাস। আমাদের ছেলে।",
        "ওর কী হয়েছিল?",
        "খেলার আসরে ওর নিজের ঘোড়ারাই ওকে ছিঁড়ে ফেলেছিল। সেদিনও আমি সেখানে ছিলাম না।",
        "তুমি সবসময় অন্য কোথাও ছিলে। আমি সবসময় দরজায়।",
      ],
      [
        "তোমাকে একটা কথা জিজ্ঞেস করব, আর এবার তুমি সত্যি বলবে।",
        "যখন তুমি মৃত্যুকে ফাঁকি দিচ্ছিলে, যখন আমার কাছে বিদায় নিলে… কখনো ভেবেছিলে আমার কী হবে?",
        "না।",
        "তুমি দেবতাদের ওপর রাগ করো। তোমার পাথরের জন্য, তোমার পাহাড়ের জন্য। পাথরটা ওরা দিয়েছে, হ্যাঁ। কিন্তু সেটা তুমি অর্জন করেছ।",
        "এই পাথর তুমি নিজেই খোদাই করেছ। প্রতিটা মিথ্যের সঙ্গে আরেকটু।",
        "মেরোপি—",
        "ওই নামে আমায় ডেকো না। সেই মেয়েটাকে তুমি মেরেছ। লিথি শুধু তাকে সমাধি দিয়েছে।",
        "মেরোপি তার দিকে পিঠ ফেরায়।",
      ],
      [
        "আজ আমি চূড়ায় দাঁড়িয়েছিলাম, দেবতাদের দিকে চেঁচাইনি। চেঁচানোর মতো কেউ আর নেই। আছি শুধু আমি।",
        "তোমাকে বলেছিলাম, “খেয়ো না।” তোমার জন্য নয়। আমার জন্য।",
        "আমি দুঃখিত, মেরোপি। জানি এতে কিছুই ফেরে না। তবু আমি দুঃখিত।",
      ],
      [
        "আমি তোমাকে দেখছিলাম। তুমি আর অভিশাপ দাও না।",
        "এই পাথর আমি নিজেই নিজের পিঠে চাপিয়েছি। নিজের পাথরকে অভিশাপ দেওয়া… অর্থহীন।",
        "কাল রাতে আকাশ এক মুহূর্তের জন্য চিরে গেল। আমি কৃত্তিকা দেখলাম। ম্লান তারাটা একটু উজ্জ্বল ছিল।",
        "তোমাকে সমাধি দিইনি বলে নিজেকে দোষ দিতাম। কিন্তু আমি তো শুধু কথা রেখেছিলাম। আমার আর লজ্জা নেই: না তোমাকে নিয়ে, না নিজেকে নিয়ে। শিগগিরই তোমার কাছে একটা জিনিস চাইব। সহজ হবে না।",
      ],
      [
        "সিসিফাস। ঝরনা থেকে আমার জন্য জল এনো। ছায়ারা ওপরে উঠতে পারে না।",
        "তুমি আবার আমাকে ভুলে যাবে।",
        "জানি। কিন্তু এবার পালানোর জন্য নয়। কারণ এই ভার আমাকে আর বইতে হবে না।",
        "তুমি তোমার পাথর বইতে শিখেছ। আমাকে আমার বোঝা নামাতে শিখতে হবে।",
        "আমি তোমাকে আবার হারাব।",
        "না। তুমি আমাকে হারাবে না। তুমি আমাকে যেতে দেবে। দুটোর মধ্যে অনেক তফাত। আনবে?",
      ],
      [
        "এনেছি।",
        "তোমার হাত কাঁপছে। পাথর ঠেলার সময় ওরা কখনো কাঁপে না।",
        "শেষ একটা কথা বলো। সত্যি কিছু।",
        "আমি তোমাকে ভালোবেসেছিলাম। সেটা ছিল খারাপ ভালোবাসা। কিন্তু ভালোবাসাই।",
        "জানি। মনে না থাকলেও, জানব। …দাও।",
        "সে জল পান করে। দীর্ঘ নীরবতা।",
        "পথিক… এই পাহাড়ের কি কোনো শেষ আছে?",
        "না। কিন্তু পথটা সুন্দর।",
        "মেরোপি ছাইরঙা প্রান্তর পেরিয়ে চলে যায়। একবারও পেছন ফিরে তাকায় না। সিসিফাস তার পাথরের কাছে নেমে যায়।",
      ],
    ],
    bring: "আমি জল আনব",
    notYet: "এখনো না",
    notYetReplies: [
      "তাহলে কাল। তাড়া নেই। এখানে সময়ই আমাদের একমাত্র সম্বল।",
      "জানি তুমি কী চাও: আমাকে আরও কিছুক্ষণ মনে রাখতে। মনে রাখা ভালোবাসা, সিসিফাস। আঁকড়ে ধরা নয়।",
      "পাথর অপেক্ষা করে। আমিও। আমরা দুজনেই এতে পাকা।",
    ],
    fillers: [
      [
        "আবার তুমি। আজ পাথরটা ভারী?",
        "নিজের নাম ভুলে গেছি, কিন্তু হাত দুটো মনে আছে। মনে হয় আমি তাঁত বুনতাম।",
        "নিচে নামার সময় আমার মুখের দিকে তাকিয়ো। হয়তো কিছু মনে পড়বে।",
      ],
      [
        "আজ কথা বলতে ইচ্ছে করছে না।",
        "পাহাড়ের ছায়া যত লম্বা হয়, তত বেশি মনে পড়ে।",
        "গ্লকাসের হাসি কানে বাজছে। ওটাও যদি ভুলে যেতাম।",
      ],
      [
        "তোমার পথটা সুন্দর, সিসিফাস। কেউ কখনো বলেনি তোমাকে?",
        "পাথর ঠেলার সময় তোমার মুখ বদলে যায়। তুমি আর রেগে নেই।",
        "ঝরনা অপেক্ষা করবে। তাড়াহুড়ো কোরো না।",
      ],
    ],
    turned: ["…", "যাও, সিসিফাস।", "তোমার পাথর অপেক্ষা করছে।"],
    companions: {
      pro: {
        meetings: [
          [
            "কী দেখছ অমন করে? আসল খেলা শুরু হয় ঈগলটা এলে।",
            "প্রমিথিউস। আমি মানুষকে আগুন দিয়েছিলাম। জিউস তার সব শাস্তি এই একটা ঢালেই সাজিয়ে রেখেছে। আমারটা এই শিলা, আর ওই পাখি।",
          ],
          [
            "পথের নিচে মেয়েটাকে দেখলাম। সে বিস্মৃতির জল খেয়েছে।",
            "ও আমার স্ত্রী।",
            "তাহলে ভুলে যাওয়া ওর শাস্তি নয়, সিসিফাস। তোমার।",
          ],
          [
            "এখনো চূড়ার দিকে চোখ রেখে ঠেলছ কেন?",
            "আর কোথায় তাকাব?",
            "তোমার হাতের দিকে। এই সবকিছুর মধ্যে শুধু ওরাই তোমার।",
          ],
          [
            "আজ ঈগলটা আমার অর্ধেক যকৃৎ নিয়ে গেছে। একটা চড়াইয়ের জন্য এই যন্ত্রণাটা বইবে? জানতে চাই, চূড়ায় যন্ত্রণার কোনো ওজন আছে কি না।",
          ],
          [
            "তুমি বয়েছ। এক মুহূর্ত নিজেকে হালকা লাগল; ভুলেই গিয়েছিলাম সেটা কেমন। আমার আগুনের একটা স্ফুলিঙ্গ এখন তোমার: তোমার দম তাড়াতাড়ি ফিরবে।",
            "আর তোমার পরের চড়াইয়ে আমার শিকলের ডগা তোমার পাথরটা ধরে ফেলবে। একবার।",
          ],
          [
            "তোমার পাথর তো অন্তত নড়ে। আমার শিকল কখনো ছেঁড়ে না।",
            "তাহলে তোমারটা সহজ। তোমাকে শুধু অপেক্ষা করতে হয়।",
            "না। অপেক্ষা মানে কিছুই করতে না পারা। তুমি অন্তত বেছে নাও কীভাবে ঠেলবে।",
          ],
          [
            "মানুষ যাতে অন্ধকারে দেখতে পায়, তাই আমি আগুন দিয়েছিলাম। তুমি ওই ছায়ার জন্য একটা আগুন রেখে গেছ, সিসিফাস। ওখানে নিচে কেউ প্রথমবার উষ্ণতা পেয়েছে।",
          ],
        ],
        idle: [
          "আজ ঈগলটা দেরি করছে। প্রায় ওর অভাব বোধ করছি।",
          "ঠেলো, ভাই। আমি দেখছি।",
          "চূড়া নয়। তোমার হাত।",
        ],
        declined: "তাহলে আমি নিজেই বইব। সবসময় বয়েছি।",
        accepted: "এর মধ্যেই ভারী লাগছে, তাই না? থেমো না।",
      },
      tan: {
        meetings: [
          [
            "জল আমার চিবুক পর্যন্ত, ফল আমার মাথার ওপরে। ঝুঁকলে জল নেমে যায়। হাত বাড়ালে ডাল উঠে যায়।",
            "আমি দেবতাদের আমার ভোজে ডেকেছিলাম, আর তাদের পরীক্ষা করতে নিজের ছেলেকে রেঁধেছিলাম। তাই আমি ক্ষুধার্ত, তৃষ্ণার্ত। আর তুমি?",
          ],
          [
            "একটা চড়াইয়ের জন্য আমার তৃষ্ণাটা বইবে? চূড়ায় যদি তা কমে, তবে দেবতাদের কিছুটা দয়া আছে বলতে হবে।",
          ],
          [
            "কমেনি, তাই না? জানতাম। কিন্তু কিছুক্ষণ ওটাকে ভুলে ছিলাম। বদলে আমার ধৈর্য নাও: এখন থেকে পিছলে গেলে কম জমি হারাবে।",
            "আর তোমার পরের চড়াইয়ে তোমার পাথরের পেছনে একটা পাথরের খিল গুঁজে দেব। সবকিছুই পিছলায়; এটা আমি ভালোই জানি। কিন্তু কিছুটা থেকে যায়।",
          ],
          [
            "শুনলাম মেয়েটা একটা শিশুর কথা বলছিল। গ্লকাস।",
            "ওর নিজের ঘোড়ারাই ওকে ছিঁড়ে ফেলেছিল। আমি সেখানে ছিলাম না।",
            "আমি নিজের হাতে নিজের ছেলেকে ভোজের থালায় সাজিয়েছিলাম, আর আমি সেখানে ছিলাম। জানি না আমাদের মধ্যে কে বেশি খারাপ বাবা। কিন্তু তুমি অন্তত লজ্জিত।",
          ],
          [
            "মেয়েটাকে দেখলাম। তোমার দিকে পিঠ ফিরিয়ে আছে, কিন্তু কিছু একটা খুঁজছে।",
            "আমাকে।",
            "না। তোমাকে খোঁজা থামানোর একটা উপায়। দুটো এক জিনিস নয়।",
          ],
          [
            "এখন তোমাকে বুঝি। তুমি পৌঁছনোর জন্য ঠেলো না।",
            "তাহলে কীসের জন্য?",
            "ঠেলার জন্য। আমি সবসময় শুধু পাওয়ার জন্যই হাত বাড়িয়েছি। সেটা আমার শাস্তি ছিল না। সেটা ছিল আমার ভুল।",
          ],
          ["চাওয়া যদি থামাই, জল কি তবুও পালাবে? কোনোদিন আমার হয়ে চূড়াকে জিজ্ঞেস কোরো।"],
          ["আজ জল আমার চিবুকেই থেমে ছিল। আমি হাত বাড়াইনি। জল পালায়নি। এক মুহূর্তের জন্য।"],
        ],
        idle: [
          "কখনো নিজের ঘাম খেয়েছ? নোনা। তবু জল।",
          "চলতে থাকো। এখানে কারও তো নড়া উচিত।",
          "ডুমুর আবার পেকেছে। পাকবেই তো।",
        ],
        declined: "স্বাভাবিক। কে-ই বা চাইবে?",
        accepted: "টের পাচ্ছ? আমার কাছে প্রতিটা মুহূর্তই এমন।",
      },
    },
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
    items: {
      stars: "as Plêiades",
      wind: "o vento do mar",
      spring: "a fonte do esquecimento",
      lethe: "a água do Lete, nas mãos dele",
    },
    episodes: [
      [
        "Viajante… esta colina tem fim?",
        "…Mérope?",
        "Mérope. Um nome bonito. É meu?",
        "Seu. Em Corinto, eu chamei você assim durante quarenta anos.",
        "Não me lembro. Todos que chegam aqui bebem do rio. Eu também devo ter bebido.",
        "O Lete. Roubaram você de mim também.",
        "Ninguém me roubou. Só há um vazio onde meu nome deveria estar.",
        "Zeus! Pegue de volta sua pedra, pegue de volta sua colina, leve o que quiser. Só a devolva para mim!",
        "Não grite. Aqui as vozes não vão longe. O céu se abre lá em cima? Aqui embaixo é sempre cor de cinza. Quando subir, me fale das estrelas.",
      ],
      [
        "Abre. Há estrelas. Sete irmãs: as Plêiades. Uma é mais apagada que as outras.",
        "Por que mais apagada?",
        "Ela se casou com um mortal. Dizem que diminuiu a própria luz de vergonha. O nome dela é Mérope.",
        "Mérope… Meu nome. Um mortal… Você é esse mortal?",
        "Não responda. Quando ouço “Corinto”, alguma coisa dói em mim, e não sei por quê. Aqui o ar nunca se move. Traga para mim o vento lá de cima.",
      ],
      [
        "Lá em cima o vento vem do mar. Tem cheiro de sal. Corinto tinha dois mares, e nosso palácio dava para os dois.",
        "Eu voltava para casa pelo mar. Toda manhã você ficava no portão leste contando os navios que entravam no porto.",
        "Os navios… Eu esperava alguém. Sempre alguém.",
        "Quando eu lembrar quem, conto a você. Mas aquelas manhãs parecem não ter terminado nunca.",
      ],
      [
        "Dizem que os mortos não sonham. Depois que você me falou de Corinto, eu sonhei.",
        "Um homem no leito de morte sussurrava para mim: “Não me enterre. Aconteça o que acontecer, não me enterre.”",
        "E eu não enterrei. E Corinto inteira cuspiu em mim. A mulher que deixou o marido para os cães, era assim que me chamavam.",
        "Aquele homem era você. Não era?",
        "Era.",
        "Por quê?",
        "Para enganar a Morte uma segunda vez. Eu diria à rainha dos mortos que minha esposa não tinha me enterrado, e imploraria para voltar lá para cima e castigá-la.",
        "E você voltou?",
        "Voltei. Para viver. Não para você.",
      ],
      [
        "Agora eu me lembro: esperei por você. No portão, toda manhã. Envelheci. Você nunca veio.",
        "Eu estava vivendo. Queria viver cada dia que tinha roubado da Morte.",
        "Quem pagou por esses dias fui eu. Cada um deles.",
        "Lembrar pesa tanto assim. Há um boato: atrás do topo fica a nascente do Lete, uma fonte.",
        "Nunca olhei. Só olhava para a pedra.",
        "Olhe. Olhe por mim.",
      ],
      [
        "Ela está lá. Logo depois do lugar onde a pedra para. A água é tão parada que não reflete nada.",
        "Um gole, e a dor pararia. Eu esqueceria você de novo.",
        "Não beba.",
        "Por quê? Por mim, ou por você?",
      ],
      [
        "Você não respondeu. Lembro de outra coisa: uma criança rindo no meio dos cavalos.",
        "Glauco. Nosso filho.",
        "O que aconteceu com ele?",
        "Os próprios cavalos o despedaçaram, nos jogos. Naquele dia eu também não estava lá.",
        "Você estava sempre em outro lugar. Eu, sempre no portão.",
      ],
      [
        "Vou perguntar uma coisa, e desta vez você vai me dizer a verdade.",
        "Quando você enganava a Morte, quando se despediu de mim… alguma vez pensou no que seria de mim?",
        "Não.",
        "Você se enfurece com os deuses. Pela sua pedra, pela sua colina. Eles lhe deram a pedra, sim. Mas você a mereceu.",
        "Você mesmo talhou esta pedra. Um pouco mais a cada mentira.",
        "Mérope—",
        "Não me chame assim. Aquela mulher, foi você quem a matou. O Lete só a enterrou.",
        "Mérope lhe dá as costas.",
      ],
      [
        "Hoje parei no topo e não gritei com os deuses. Não sobrou ninguém com quem gritar. Só sobrei eu.",
        "Eu disse a você: “Não beba.” Não por você. Por mim.",
        "Me perdoe, Mérope. Sei que isso não traz nada de volta. Peço perdão mesmo assim.",
      ],
      [
        "Tenho observado você. Você não amaldiçoa mais.",
        "Fui eu que pus esta pedra nas minhas costas. Amaldiçoar a própria pedra é… inútil.",
        "Ontem à noite o céu se abriu por um instante. Vi as Plêiades. A estrela apagada estava um pouco mais brilhante.",
        "Eu me culpava por não ter enterrado você. Mas só tinha cumprido minha palavra. Não tenho mais vergonha: nem de você, nem de mim. Logo vou lhe pedir uma coisa. Não vai ser fácil.",
      ],
      [
        "Sísifo. Traga para mim água da fonte. As sombras não conseguem subir.",
        "Você vai me esquecer de novo.",
        "Eu sei. Mas não para fugir, desta vez. É que eu não preciso mais carregar este peso.",
        "Você aprendeu a carregar sua pedra. Eu preciso aprender a largar meu fardo.",
        "Vou perder você de novo.",
        "Não. Você não vai me perder. Vai me deixar ir. Há uma grande diferença. Você vai trazer?",
      ],
      [
        "Eu trouxe.",
        "Suas mãos estão tremendo. Elas nunca tremem quando você empurra a pedra.",
        "Me diga uma última coisa. Uma coisa verdadeira.",
        "Eu amei você. Foi um amor ruim. Mas foi amor.",
        "Eu sei. Mesmo que eu não me lembre, vou saber. …Me dê.",
        "Ela bebe. Um longo silêncio.",
        "Viajante… esta colina tem fim?",
        "Não. Mas o caminho é bonito.",
        "Mérope se afasta pela planície cor de cinza. Não olha para trás nem uma vez. Sísifo desce até sua pedra.",
      ],
    ],
    bring: "Vou trazer a água",
    notYet: "Ainda não",
    notYetReplies: [
      "Amanhã, então. Não há pressa. O tempo é a única coisa que temos aqui.",
      "Sei o que você quer: lembrar de mim por mais tempo. Lembrar é amor, Sísifo. Prender não é.",
      "A pedra espera. Eu também. Nós dois somos bons nisso.",
    ],
    fillers: [
      [
        "Você de novo. A pedra está pesada hoje?",
        "Esqueci meu nome, mas lembro das minhas mãos. Acho que eu tecia.",
        "Olhe para o meu rosto quando descer. Talvez eu me lembre de alguma coisa.",
      ],
      [
        "Hoje não quero conversar.",
        "Quanto mais cresce a sombra da colina, mais eu me lembro.",
        "A risada de Glauco está nos meus ouvidos. Queria ter esquecido isso também.",
      ],
      [
        "Seu caminho é bonito, Sísifo. Ninguém nunca lhe disse?",
        "Seu rosto muda quando você empurra a pedra. Você não está mais com raiva.",
        "A fonte vai esperar. Não se apresse.",
      ],
    ],
    turned: ["…", "Vá, Sísifo.", "Sua pedra está esperando."],
    companions: {
      pro: {
        meetings: [
          [
            "O que você está olhando? O espetáculo de verdade começa quando a águia chega.",
            "Prometeu. Dei o fogo aos homens. Zeus enfileirou todos os seus castigos nesta mesma encosta. O meu é esta rocha, e aquele pássaro.",
          ],
          [
            "Vi a mulher lá embaixo no caminho. Ela bebeu a água do esquecimento.",
            "É minha esposa.",
            "Então o esquecimento não é o castigo dela, Sísifo. É o seu.",
          ],
          [
            "Por que você ainda empurra com os olhos no topo?",
            "Para onde mais eu olharia?",
            "Para as suas mãos. São a única parte disso tudo que é sua.",
          ],
          [
            "A águia levou metade do meu fígado hoje. Você carrega essa dor por uma subida? Quero saber se a dor pesa alguma coisa no topo.",
          ],
          [
            "Você carregou. Por um momento me senti mais leve; tinha esquecido como era. Uma faísca do meu fogo agora é sua: seu fôlego vai voltar mais depressa.",
            "E na sua próxima subida, a ponta da minha corrente vai segurar sua pedra. Uma vez.",
          ],
          [
            "Sua pedra pelo menos se move. Minhas correntes nunca se rompem.",
            "Então a sua sorte é mais fácil. Você só precisa esperar.",
            "Não. Esperar é não poder fazer nada. Você, pelo menos, escolhe como empurrar.",
          ],
          [
            "Dei o fogo aos homens para que enxergassem no escuro. Você deixou um fogo para aquela sombra, Sísifo. Lá embaixo, alguém está aquecido pela primeira vez.",
          ],
        ],
        idle: [
          "A águia está atrasada hoje. Quase sinto falta dela.",
          "Empurre, irmão. Estou olhando.",
          "Não o topo. Suas mãos.",
        ],
        declined: "Então eu mesmo carrego. Sempre carreguei.",
        accepted: "Já está mais pesado, não está? Não pare.",
      },
      tan: {
        meetings: [
          [
            "Água até o queixo, frutas sobre a cabeça. Quando me inclino, a água baixa. Quando estendo a mão, o galho sobe.",
            "Convidei os deuses à minha mesa e cozinhei meu próprio filho para testá-los. Por isso tenho fome e sede. E você?",
          ],
          [
            "Você carrega minha sede por uma subida? Se ela passar no topo, então os deuses têm alguma piedade, afinal.",
          ],
          [
            "Não passou, não é? Eu sabia. Mas por um tempo eu a esqueci. Leve minha paciência em troca: de agora em diante, um escorregão vai lhe custar menos terreno.",
            "E na sua próxima subida vou calçar uma pedra atrás da sua. Tudo escorrega; isso eu sei bem. Mas uma parte fica.",
          ],
          [
            "Dizem que a mulher falou de uma criança. Glauco.",
            "Os próprios cavalos o despedaçaram. Eu não estava lá.",
            "Eu servi meu próprio filho à mesa com minhas próprias mãos, e estava lá. Não sei qual de nós foi o pior pai. Mas você, pelo menos, sente vergonha.",
          ],
          [
            "Olhei para a mulher. Ela está de costas para você, mas procura alguma coisa.",
            "Procura a mim.",
            "Não. Procura um jeito de parar de procurar você. Não é a mesma coisa.",
          ],
          [
            "Agora entendo você. Você não empurra para chegar.",
            "Então para quê?",
            "Para empurrar. Eu só estendi a mão para ter. Esse não foi meu castigo. Foi meu erro.",
          ],
          ["Se eu parasse de desejar, a água ainda fugiria? Pergunte ao topo por mim, um dia."],
          ["Hoje a água ficou no meu queixo. Não estendi a mão. Ela não fugiu. Por um momento."],
        ],
        idle: [
          "Já bebeu o próprio suor? Salgado. Mas é água.",
          "Continue. Alguém aqui precisa se mexer.",
          "Os figos amadureceram de novo. Claro que sim.",
        ],
        declined: "Claro. Quem ia querer?",
        accepted: "Está sentindo? É assim todo momento, para mim.",
      },
    },
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

/** the story in a language */
export function storyFor(lang: LangCode): LangStory {
  return STORY_TEXT[lang];
}
