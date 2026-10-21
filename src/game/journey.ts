/**
 * Sisyphus's journey: a beginning, three acts of mind, and an ending.
 *
 * The sayings at the summit are no longer drawn at random. Each belongs to one
 * of three stages (stages.json, every one of the thousand read and placed by
 * hand), and which stage he is in follows how far he has come:
 *
 *   I.   Revolt — the gods are unjust and he says so (Nietzsche, the tragedians)
 *   II.  The Void — the anger is spent, the hill is only a hill (Ecclesiastes'
 *        heirs: Schopenhauer, Pascal, Leopardi, Pessoa)
 *   III. The Absurd — he stops asking why, and the effort itself becomes his
 *        (the Stoics, Epicurus, Lao Tzu, Rumi)
 *
 * Progress is "insight": one for every summit, one more for a climb without a
 * slip. A fall is not a failure here, it is the story moving on. The true
 * ending is not the top of the hill but the moment he learns to smile there.
 */
import STAGES from "./quotes-data/stages.json";
import type { LangCode } from "./quotes";

export type Stage = 1 | 2 | 3;

/** the stage each saying belongs to, by index */
export const STAGE_OF: readonly number[] = STAGES as number[];

/** insight needed to reach stage II and III */
export const STAGE_AT: readonly [number, number] = [12, 30];
/** insight from which the ending can come, on a climb good enough to earn it */
export const ENDING_AT = 44;

export function stageFor(insight: number): Stage {
  return insight >= STAGE_AT[1] ? 3 : insight >= STAGE_AT[0] ? 2 : 1;
}

/** 0-1 through the whole journey, for the progress line in the book */
export function journeyProgress(insight: number): number {
  return Math.max(0, Math.min(1, insight / ENDING_AT));
}

type StageText = { name: string; desc: string };

export const STAGE_TEXT: Record<LangCode, [StageText, StageText, StageText]> = {
  tr: [
    { name: "İsyan", desc: "Tanrılar onun hafızasını aldı. Sisifos geri istiyor." },
    { name: "Boşluk", desc: "Suçlayacak kimse kalmadı. Yalnızca kendisi." },
    { name: "Absürd", desc: "Taş onun. Ve bırakmayı öğreniyor." },
  ],
  en: [
    { name: "Revolt", desc: "The gods took her memory. He wants it back." },
    { name: "The Void", desc: "There is no one left to blame. Only himself." },
    { name: "The Absurd", desc: "The stone is his. And he is learning to let go." },
  ],
  zh: [
    { name: "反抗", desc: "诸神夺走了她的记忆。他要把它夺回来。" },
    { name: "虚无", desc: "再没有人可以责怪。只剩他自己。" },
    { name: "荒诞", desc: "石头是他的。他正在学着放手。" },
  ],
  hi: [
    { name: "विद्रोह", desc: "देवताओं ने उसकी स्मृति छीन ली। वह उसे वापस चाहता है।" },
    { name: "शून्य", desc: "दोष देने को कोई नहीं बचा। बस वह ख़ुद।" },
    { name: "बेतुकापन", desc: "पत्थर उसका है। और वह छोड़ना सीख रहा है।" },
  ],
  es: [
    { name: "Rebelión", desc: "Los dioses le quitaron a ella la memoria. Él la quiere de vuelta." },
    { name: "El vacío", desc: "Ya no queda nadie a quien culpar. Solo a sí mismo." },
    { name: "Lo absurdo", desc: "La piedra es suya. Y está aprendiendo a soltar." },
  ],
  fr: [
    { name: "Révolte", desc: "Les dieux lui ont pris sa mémoire. Il la réclame." },
    { name: "Le néant", desc: "Il n'y a plus personne à blâmer. Que lui-même." },
    { name: "L'absurde", desc: "Le rocher est à lui. Et il apprend à lâcher prise." },
  ],
  ar: [
    { name: "التمرّد", desc: "أخذت الآلهة ذاكرتها. وهو يريد استعادتها." },
    { name: "الفراغ", desc: "لم يبقَ أحدٌ يُلقي عليه اللوم. سوى نفسه." },
    { name: "العبث", desc: "الصخرة صخرته. وهو يتعلّم التخلّي." },
  ],
  bn: [
    { name: "বিদ্রোহ", desc: "দেবতারা তার স্মৃতি কেড়ে নিয়েছে। সে তা ফিরে চায়।" },
    { name: "শূন্যতা", desc: "দোষ দেওয়ার মতো আর কেউ নেই। শুধু সে নিজে।" },
    { name: "অযৌক্তিকতা", desc: "পাথরটা তার। আর সে ছেড়ে দিতে শিখছে।" },
  ],
  pt: [
    { name: "Revolta", desc: "Os deuses tiraram a memória dela. Ele a quer de volta." },
    { name: "O vazio", desc: "Não resta ninguém a quem culpar. Só ele mesmo." },
    { name: "O absurdo", desc: "A pedra é dele. E ele está aprendendo a deixar ir." },
  ],
};

/**
 * The raven. It comes down out of the sky now and then, settles on a rock a
 * little way up the slope, says one thing and goes. Four things per stage:
 * mocking in the revolt, weary in the void, something like fond at the end.
 */
export const RAVEN_LINES: Record<LangCode, [string[], string[], string[]]> = {
  tr: [
    [
      "Gökyüzüne istediğin kadar bağır. Daha kötülerini duydu.",
      "Lanet ettiğin her tanrı hâlâ uykuda.",
      "Öfken taşı hızlandırmıyor.",
      "Kralların daha küçük taşları itip daha yüksek sesle ağladığını gördüm.",
    ],
    [
      "Yukarı, aşağı. Yukarı, aşağı. Hâlâ sayıyor musun?",
      "Tepe seni hatırlamıyor. Bir tek ben hatırlıyorum.",
      "Zirvede seni dönüş yolundan başka bir şey beklemiyor.",
      "Yıldızlar bile kendini tekrar ediyor, Sisifos.",
    ],
    [
      "Bir kayaya gülümsüyorsun. Güzel.",
      "Mesele hiçbir zaman zirve değildi, değil mi?",
      "Yamaçtaki ışığa bak. O da senin.",
      "Tanrılar seni bununla cezalandırdı. Sen burayı yuva yaptın.",
    ],
  ],
  en: [
    [
      "Shout at the sky if you like. It has heard worse.",
      "Every god you curse is still asleep.",
      "Your anger moves the stone no faster.",
      "I have watched kings push smaller stones and weep louder.",
    ],
    [
      "Up, down. Up, down. Do you still count?",
      "The hill does not remember you. Only I do.",
      "Nothing waits at the top but the way back.",
      "Even the stars repeat themselves, Sisyphus.",
    ],
    [
      "You are smiling at a rock. Good.",
      "The top was never the point, was it?",
      "Look at the light on the slope. It is yours too.",
      "The gods punished you with this. You made it a home.",
    ],
  ],
  zh: [
    [
      "你尽管对着天空喊吧。它听过更糟的。",
      "你诅咒的每一位神都还在沉睡。",
      "你的愤怒不会让石头走得更快。",
      "我见过国王推着更小的石头，哭得更响。",
    ],
    [
      "上去，下来。上去，下来。你还在数吗？",
      "山不记得你。只有我记得。",
      "山顶上等着你的，只有回去的路。",
      "连星辰也在重复自己，西西弗斯。",
    ],
    [
      "你在对一块石头微笑。很好。",
      "重点从来不是山顶，对吧？",
      "看看山坡上的光。它也属于你。",
      "诸神用它惩罚你。你却把这里变成了家。",
    ],
  ],
  hi: [
    [
      "चाहो तो आसमान पर चिल्लाओ। उसने इससे बुरा सुना है।",
      "जिस-जिस देवता को तुम कोसते हो, वह अब भी सो रहा है।",
      "तुम्हारा क्रोध पत्थर को तेज़ नहीं चलाता।",
      "मैंने राजाओं को इससे छोटे पत्थर धकेलते और ज़ोर से रोते देखा है।",
    ],
    [
      "ऊपर, नीचे। ऊपर, नीचे। क्या तुम अब भी गिनते हो?",
      "पहाड़ तुम्हें याद नहीं रखता। बस मैं रखता हूँ।",
      "चोटी पर लौटने के रास्ते के सिवा कुछ नहीं है।",
      "तारे भी ख़ुद को दोहराते हैं, सिसिफ़स।",
    ],
    [
      "तुम एक चट्टान पर मुस्कुरा रहे हो। अच्छा है।",
      "बात कभी चोटी की थी ही नहीं, है न?",
      "ढलान पर पड़ती रोशनी देखो। वह भी तुम्हारी है।",
      "देवताओं ने इससे तुम्हें दंड दिया। तुमने इसे घर बना लिया।",
    ],
  ],
  es: [
    [
      "Grítale al cielo si quieres. Ha oído cosas peores.",
      "Todos los dioses que maldices siguen dormidos.",
      "Tu ira no mueve la piedra más deprisa.",
      "He visto reyes empujar piedras más pequeñas y llorar más fuerte.",
    ],
    [
      "Arriba, abajo. Arriba, abajo. ¿Todavía cuentas?",
      "La colina no te recuerda. Solo yo.",
      "En la cima no te espera nada más que el camino de vuelta.",
      "Hasta las estrellas se repiten, Sísifo.",
    ],
    [
      "Le estás sonriendo a una roca. Bien.",
      "La cima nunca fue lo importante, ¿verdad?",
      "Mira la luz en la ladera. También es tuya.",
      "Los dioses te castigaron con esto. Tú lo hiciste tu hogar.",
    ],
  ],
  fr: [
    [
      "Crie contre le ciel si tu veux. Il a entendu pire.",
      "Tous les dieux que tu maudis dorment encore.",
      "Ta colère ne fait pas avancer le rocher plus vite.",
      "J'ai vu des rois pousser de plus petites pierres et pleurer plus fort.",
    ],
    [
      "Monter, descendre. Monter, descendre. Tu comptes encore ?",
      "La colline ne se souvient pas de toi. Moi seul.",
      "Au sommet, rien ne t'attend que le chemin du retour.",
      "Même les étoiles se répètent, Sisyphe.",
    ],
    [
      "Tu souris à un rocher. C'est bien.",
      "Le sommet n'a jamais été le but, n'est-ce pas ?",
      "Regarde la lumière sur la pente. Elle est à toi aussi.",
      "Les dieux t'ont puni avec ceci. Tu en as fait une demeure.",
    ],
  ],
  ar: [
    [
      "اصرخ في السماء إن شئت. لقد سمعت ما هو أسوأ.",
      "كل إله تلعنه ما زال نائمًا.",
      "غضبك لا يُسرع الصخرة.",
      "رأيت ملوكًا يدفعون صخورًا أصغر ويبكون بصوت أعلى.",
    ],
    [
      "صعود، هبوط. صعود، هبوط. أما زلت تعدّ؟",
      "التلّ لا يتذكّرك. وحدي أتذكّرك.",
      "لا شيء ينتظرك في القمة سوى طريق العودة.",
      "حتى النجوم تكرّر نفسها يا سيزيف.",
    ],
    [
      "إنك تبتسم لصخرة. حسنًا.",
      "لم تكن القمة هي الغاية أبدًا، أليس كذلك؟",
      "انظر إلى الضوء على المنحدر. إنه لك أيضًا.",
      "عاقبتك الآلهة بهذا. وأنت جعلته بيتًا.",
    ],
  ],
  bn: [
    [
      "চাইলে আকাশের দিকে চিৎকার করো। সে এর চেয়েও খারাপ শুনেছে।",
      "যে দেবতাকেই অভিশাপ দাও, সে এখনো ঘুমিয়ে।",
      "তোমার রাগ পাথরটাকে দ্রুত চালায় না।",
      "আমি রাজাদের এর চেয়ে ছোট পাথর ঠেলতে আর জোরে কাঁদতে দেখেছি।",
    ],
    [
      "উপরে, নিচে। উপরে, নিচে। এখনো গুনছ?",
      "পাহাড় তোমাকে মনে রাখে না। শুধু আমি রাখি।",
      "চূড়ায় ফেরার পথ ছাড়া আর কিছুই অপেক্ষা করে না।",
      "তারারাও নিজেদের পুনরাবৃত্তি করে, সিসিফাস।",
    ],
    [
      "তুমি একটা পাথরের দিকে তাকিয়ে হাসছ। ভালো।",
      "আসল কথা কখনো চূড়া ছিল না, তাই না?",
      "ঢালের ওপর আলোটা দেখো। ওটাও তোমার।",
      "দেবতারা এ দিয়ে তোমাকে শাস্তি দিয়েছিল। তুমি একে ঘর বানিয়েছ।",
    ],
  ],
  pt: [
    [
      "Grite para o céu, se quiser. Ele já ouviu coisa pior.",
      "Todos os deuses que você amaldiçoa continuam dormindo.",
      "Sua raiva não faz a pedra andar mais depressa.",
      "Já vi reis empurrarem pedras menores e chorarem mais alto.",
    ],
    [
      "Sobe, desce. Sobe, desce. Você ainda conta?",
      "A colina não se lembra de você. Só eu me lembro.",
      "No topo, nada o espera além do caminho de volta.",
      "Até as estrelas se repetem, Sísifo.",
    ],
    [
      "Você está sorrindo para uma pedra. Que bom.",
      "O topo nunca foi o que importava, foi?",
      "Olhe a luz na encosta. Ela também é sua.",
      "Os deuses o castigaram com isto. Você fez disto um lar.",
    ],
  ],
};

type StoryText = {
  /** the prologue, one card at a time */
  prologue: string[];
  skip: string;
  begin: string;
  /** "Act I" etc. and the label over a stage change */
  act: string;
  newStage: string;
  raven: string;
  /** the ending */
  camus: string;
  camusBy: string;
  end: string;
  endNote: string;
  continue: string;
  journey: string;
};

export const STORY: Record<LangCode, StoryText> = {
  tr: {
    prologue: [
      "Korint kralı Sisifos, Ölüm'ü iki kez kandırdı.",
      "Birincisinde onu zincire vurdu. İkincisinde karısı Merope'ye tek bir şey söyledi: “Ölürsem beni gömme.”",
      "Nedenini söylemedi. Merope sözünü tuttu. Tanrılar ise hiçbir şeyi unutmadı.",
      "Bunun için ona bir taş verdiler. Ve bir tepe. Taşı zirveye it. Taş geri yuvarlanacak. Sonsuza dek.",
    ],
    skip: "Geç",
    begin: "Başla",
    act: "Perde",
    newStage: "Yeni bir kavrayış",
    raven: "Kuzgun",
    camus: "Sisifos'u mutlu olarak düşlemek gerekir.",
    camusBy: "Albert Camus",
    end: "Son",
    endNote: "Ve yine de taş bekliyor. Tepe hep orada olacak.",
    continue: "Tırmanmaya devam et",
    journey: "Yolculuk",
  },
  en: {
    prologue: [
      "Sisyphus, king of Corinth, cheated Death twice.",
      "The first time, he chained him. The second time, he told his wife Merope one thing: “When I die, do not bury me.”",
      "He did not say why. Merope kept her word. The gods forgot nothing.",
      "For this they gave him a stone. And a hill. Push it to the top. It will roll back down. Forever.",
    ],
    skip: "Skip",
    begin: "Begin",
    act: "Act",
    newStage: "A new understanding",
    raven: "The Raven",
    camus: "One must imagine Sisyphus happy.",
    camusBy: "Albert Camus",
    end: "The End",
    endNote: "And yet the stone waits. The hill will always be there.",
    continue: "Keep climbing",
    journey: "The journey",
  },
  zh: {
    prologue: [
      "科林斯国王西西弗斯，两次欺骗了死神。",
      "第一次，他用锁链锁住了死神。第二次，他只对妻子墨洛珀说了一句话：「我死后，不要埋葬我。」",
      "他没有说为什么。墨洛珀信守了诺言。诸神什么也没有忘记。",
      "为此，他们给了他一块石头。还有一座山。把它推上山顶。它会滚下来。永远如此。",
    ],
    skip: "跳过",
    begin: "开始",
    act: "第幕",
    newStage: "新的领悟",
    raven: "渡鸦",
    camus: "我们必须想象西西弗斯是幸福的。",
    camusBy: "阿尔贝·加缪",
    end: "终",
    endNote: "然而石头仍在等待。山永远都在那里。",
    continue: "继续攀登",
    journey: "旅程",
  },
  hi: {
    prologue: [
      "कोरिंथ के राजा सिसिफ़स ने दो बार मृत्यु को छला।",
      "पहली बार उसने मृत्यु को ज़ंजीरों में जकड़ दिया। दूसरी बार उसने अपनी पत्नी मेरोपी से बस एक बात कही: “जब मैं मरूँ, मुझे दफ़नाना मत।”",
      "उसने कारण नहीं बताया। मेरोपी ने अपना वचन निभाया। देवताओं ने कुछ नहीं भुलाया।",
      "इसके लिए उन्होंने उसे एक पत्थर दिया। और एक पहाड़। इसे चोटी तक धकेलो। यह लुढ़ककर वापस आएगा। हमेशा।",
    ],
    skip: "छोड़ें",
    begin: "शुरू करें",
    act: "अंक",
    newStage: "एक नई समझ",
    raven: "काला कौआ",
    camus: "हमें सिसिफ़स को सुखी कल्पना करना चाहिए।",
    camusBy: "आल्बेयर कामू",
    end: "समाप्त",
    endNote: "फिर भी पत्थर प्रतीक्षा करता है। पहाड़ सदा वहीं रहेगा।",
    continue: "चढ़ते रहें",
    journey: "यात्रा",
  },
  es: {
    prologue: [
      "Sísifo, rey de Corinto, engañó dos veces a la Muerte.",
      "La primera vez, la encadenó. La segunda, le dijo una sola cosa a su esposa, Mérope: “Cuando muera, no me entierres.”",
      "No le dijo por qué. Mérope cumplió su palabra. Los dioses no olvidaron nada.",
      "Por eso le dieron una piedra. Y una colina. Empújala hasta la cima. Rodará de vuelta. Para siempre.",
    ],
    skip: "Saltar",
    begin: "Empezar",
    act: "Acto",
    newStage: "Una nueva comprensión",
    raven: "El cuervo",
    camus: "Hay que imaginarse a Sísifo dichoso.",
    camusBy: "Albert Camus",
    end: "Fin",
    endNote: "Y sin embargo la piedra espera. La colina siempre estará ahí.",
    continue: "Seguir subiendo",
    journey: "El viaje",
  },
  fr: {
    prologue: [
      "Sisyphe, roi de Corinthe, a trompé la Mort deux fois.",
      "La première fois, il l'a enchaînée. La seconde, il n'a dit qu'une chose à sa femme Mérope : « Quand je serai mort, ne m'enterre pas. »",
      "Il n'a pas dit pourquoi. Mérope a tenu parole. Les dieux n'ont rien oublié.",
      "Pour cela, ils lui ont donné un rocher. Et une colline. Pousse-le jusqu'au sommet. Il redescendra. Pour toujours.",
    ],
    skip: "Passer",
    begin: "Commencer",
    act: "Acte",
    newStage: "Une compréhension nouvelle",
    raven: "Le corbeau",
    camus: "Il faut imaginer Sisyphe heureux.",
    camusBy: "Albert Camus",
    end: "Fin",
    endNote: "Et pourtant le rocher attend. La colline sera toujours là.",
    continue: "Continuer à grimper",
    journey: "Le voyage",
  },
  ar: {
    prologue: [
      "سيزيف، ملك كورنثة، خدع الموت مرتين.",
      "في المرة الأولى قيّده بالسلاسل. وفي الثانية قال لزوجته ميروبي شيئًا واحدًا: “إذا متُّ فلا تدفنيني.”",
      "لم يقل لماذا. وفت ميروبي بوعدها. أما الآلهة فلم تنسَ شيئًا.",
      "لذلك أعطته الآلهة صخرة. وتلًّا. ادفعها إلى القمة. ستتدحرج عائدة. إلى الأبد.",
    ],
    skip: "تخطٍّ",
    begin: "ابدأ",
    act: "الفصل",
    newStage: "فهمٌ جديد",
    raven: "الغراب",
    camus: "علينا أن نتخيّل سيزيف سعيدًا.",
    camusBy: "ألبير كامو",
    end: "النهاية",
    endNote: "ومع ذلك تنتظر الصخرة. سيبقى التلّ هناك دائمًا.",
    continue: "واصل الصعود",
    journey: "الرحلة",
  },
  bn: {
    prologue: [
      "করিন্থের রাজা সিসিফাস দুবার মৃত্যুকে ফাঁকি দিয়েছিল।",
      "প্রথমবার সে মৃত্যুকে শিকলে বেঁধেছিল। দ্বিতীয়বার সে তার স্ত্রী মেরোপিকে শুধু একটা কথা বলেছিল: “আমি মরলে আমাকে সমাধি দিয়ো না।”",
      "কেন, তা সে বলেনি। মেরোপি কথা রেখেছিল। দেবতারা কিছুই ভোলেনি।",
      "এর জন্য দেবতারা তাকে একটা পাথর দিল। আর একটা পাহাড়। এটাকে চূড়ায় ঠেলে তোলো। এটা গড়িয়ে নামবে। চিরকাল।",
    ],
    skip: "এড়িয়ে যান",
    begin: "শুরু করুন",
    act: "অঙ্ক",
    newStage: "এক নতুন উপলব্ধি",
    raven: "দাঁড়কাক",
    camus: "সিসিফাসকে সুখী বলেই কল্পনা করতে হবে।",
    camusBy: "আলবেয়ার কামু",
    end: "সমাপ্ত",
    endNote: "তবু পাথরটা অপেক্ষা করে। পাহাড় সবসময় থাকবে।",
    continue: "চড়তে থাকুন",
    journey: "যাত্রা",
  },
  pt: {
    prologue: [
      "Sísifo, rei de Corinto, enganou a Morte duas vezes.",
      "Da primeira vez, ele a acorrentou. Da segunda, disse uma única coisa à esposa, Mérope: “Quando eu morrer, não me enterre.”",
      "Não disse por quê. Mérope cumpriu a palavra. Os deuses não esqueceram nada.",
      "Por isso lhe deram uma pedra. E uma colina. Empurre-a até o topo. Ela rolará de volta. Para sempre.",
    ],
    skip: "Pular",
    begin: "Começar",
    act: "Ato",
    newStage: "Uma nova compreensão",
    raven: "O corvo",
    camus: "É preciso imaginar Sísifo feliz.",
    camusBy: "Albert Camus",
    end: "Fim",
    endNote: "E ainda assim a pedra espera. A colina estará sempre lá.",
    continue: "Continuar subindo",
    journey: "A jornada",
  },
};

/** Roman numerals for the acts */
export const ROMAN = ["I", "II", "III"] as const;
