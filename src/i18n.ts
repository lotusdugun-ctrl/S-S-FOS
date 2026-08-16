export type LangCode = "tr" | "en" | "zh" | "hi" | "es" | "fr" | "ar" | "bn" | "pt";

export const LANGUAGES: { code: LangCode; native: string }[] = [
  { code: "tr", native: "Türkçe" },
  { code: "en", native: "English" },
  { code: "zh", native: "中文" },
  { code: "hi", native: "हिन्दी" },
  { code: "es", native: "Español" },
  { code: "fr", native: "Français" },
  { code: "ar", native: "العربية" },
  { code: "bn", native: "বাংলা" },
  { code: "pt", native: "Português" },
];

export const LEVEL_NAMES: Record<LangCode, string> = {
  tr: "Tartaros Yamacı",
  en: "Slope of Tartarus",
  zh: "塔尔塔罗斯斜坡",
  hi: "टार्टरस की ढलान",
  es: "Ladera del Tártaro",
  fr: "Pente du Tartare",
  ar: "سفح تارتاروس",
  bn: "টার্টারাসের ঢাল",
  pt: "Encosta do Tártaro",
};

export const EPIGRAPHS: Record<LangCode, string> = {
  tr: "Sisyphos'u mutlu hayal etmek gerekir.",
  en: "One must imagine Sisyphus happy.",
  zh: "必须想象西西弗斯是幸福的。",
  hi: "हमें सिसिफस को सुखी कल्पना करना चाहिए।",
  es: "Hay que imaginar a Sísifo feliz.",
  fr: "Il faut imaginer Sisyphe heureux.",
  ar: "يجب أن نتخيل سيزيف سعيدًا.",
  bn: "সিসিফাসকে সুখী কল্পনা করা দরকার।",
  pt: "É preciso imaginar Sísifo feliz.",
};

export type UIStrings = {
  muteOn: string;
  muteOff: string;
  cycle: string;
  helpAdvance: string;
  helpPushBack: string;
  helpThrow: string;
  keySpace: string;
  throwBtn: string;
  continueBtn: string;
  stillDown: string;
  startBtn: string;
  /** shown over the game when a phone is held upright */
  rotate: string;
  /** instructions for the start overlay; {k1} {k2} {k3} are replaced by key/button labels */
  instructions: string;
};

export const T: Record<LangCode, UIStrings> = {
  tr: {
    muteOn: "ses açık",
    muteOff: "ses kapalı",
    cycle: "döngü",
    helpAdvance: "ilerlet",
    helpPushBack: "geri it",
    helpThrow: "kayayı fırlat",
    keySpace: "Boşluk",
    throwBtn: "fırlat",
    continueBtn: "Devam Et",
    stillDown: "taş yine aşağıda",
    startBtn: "Başla",
    rotate: "Telefonu yan çevir",
    instructions:
      "Kayayı zirveye taşı. Masaüstünde {k1} tuşlarıyla it, {k2} ile kayayı fırlat ve peşinden koş. Mobilde sol alttaki joystikle it, sağ alttaki {k3} tuşuyla kayayı yukarı fırlat.",
  },
  en: {
    muteOn: "sound on",
    muteOff: "sound off",
    cycle: "cycle",
    helpAdvance: "advance",
    helpPushBack: "push back",
    helpThrow: "launch the boulder",
    keySpace: "Space",
    throwBtn: "throw",
    continueBtn: "Continue",
    stillDown: "the boulder is at the bottom again",
    startBtn: "Start",
    rotate: "Turn your phone sideways",
    instructions:
      "Roll the boulder to the summit. On desktop push with {k1}, launch it with {k2} and sprint after it. On mobile push with the joystick bottom-left and hit {k3} bottom-right to launch the boulder upward.",
  },
  zh: {
    muteOn: "声音开",
    muteOff: "声音关",
    cycle: "循环",
    helpAdvance: "前进",
    helpPushBack: "向后推",
    helpThrow: "发射巨石",
    keySpace: "空格",
    throwBtn: "发射",
    continueBtn: "继续",
    stillDown: "巨石又滚回了山脚",
    startBtn: "开始",
    rotate: "请将手机横过来",
    instructions:
      "把巨石滚到山顶。桌面端用 {k1} 推动，用 {k2} 发射巨石并追赶。移动端用左下角摇杆推动，点右下角 {k3} 将巨石抛向高处。",
  },
  hi: {
    muteOn: "ध्वनि चालू",
    muteOff: "ध्वनि बंद",
    cycle: "चक्र",
    helpAdvance: "आगे बढ़ाएँ",
    helpPushBack: "पीछे धकेलें",
    helpThrow: "पत्थर फेंकें",
    keySpace: "स्पेस",
    throwBtn: "फेंकें",
    continueBtn: "जारी रखें",
    stillDown: "पत्थर फिर से नीचे है",
    startBtn: "शुरू करें",
    rotate: "अपना फ़ोन घुमाएँ",
    instructions:
      "बोल्डर को चोटी तक ले जाएँ। डेस्कटॉप पर {k1} से धकेलें, {k2} से पत्थर फेंकें और पीछे दौड़ें। मोबाइल पर नीचे बाएँ जॉयस्टिक से धकेलें और नीचे दाएँ {k3} बटन से पत्थर ऊपर फेंकें।",
  },
  es: {
    muteOn: "sonido activado",
    muteOff: "sonido silenciado",
    cycle: "ciclo",
    helpAdvance: "avanzar",
    helpPushBack: "empujar atrás",
    helpThrow: "lanzar la roca",
    keySpace: "Espacio",
    throwBtn: "lanzar",
    continueBtn: "Continuar",
    stillDown: "la roca volvió a estar abajo",
    startBtn: "Comenzar",
    rotate: "Gira el teléfono",
    instructions:
      "Lleva la roca hasta la cima. En escritorio empuja con {k1}, lánzala con {k2} y corre tras ella. En móvil empuja con el joystick abajo a la izquierda y usa {k3} abajo a la derecha para lanzarla cuesta arriba.",
  },
  fr: {
    muteOn: "son activé",
    muteOff: "son coupé",
    cycle: "cycle",
    helpAdvance: "avancer",
    helpPushBack: "pousser en arrière",
    helpThrow: "lancer le rocher",
    keySpace: "Espace",
    throwBtn: "lancer",
    continueBtn: "Continuer",
    stillDown: "le rocher est redescendu",
    startBtn: "Commencer",
    rotate: "Tourne ton téléphone",
    instructions:
      "Porte le rocher jusqu'au sommet. Sur ordinateur, pousse avec {k1}, lance-le avec {k2} et cours derrière. Sur mobile, pousse avec le joystick en bas à gauche et appuie sur {k3} en bas à droite pour le lancer vers le haut.",
  },
  ar: {
    muteOn: "الصوت مفتوح",
    muteOff: "الصوت مغلق",
    cycle: "دورة",
    helpAdvance: "تقدّم",
    helpPushBack: "ادفع للخلف",
    helpThrow: "أطلق الصخرة",
    keySpace: "مسافة",
    throwBtn: "أطلق",
    continueBtn: "متابعة",
    stillDown: "الصخرة عادت إلى الأسفل",
    startBtn: "ابدأ",
    rotate: "أدر هاتفك أفقيًا",
    instructions:
      "اصعد بالصخرة إلى القمة. على الحاسوب ادفع باستخدام {k1}، واقذفها بمفتاح {k2} واركض خلفها. على الجوال ادفع بالعصا السفلية اليسرى واضغط {k3} أسفل اليمين لإطلاق الصخرة للأعلى.",
  },
  bn: {
    muteOn: "শব্দ চালু",
    muteOff: "শব্দ বন্ধ",
    cycle: "চক্র",
    helpAdvance: "এগোও",
    helpPushBack: "পেছনে ঠেলে দিন",
    helpThrow: "বোল্ডার নিক্ষেপ",
    keySpace: "স্পেস",
    throwBtn: "নিক্ষেপ",
    continueBtn: "চালিয়ে যান",
    stillDown: "পাথর আবার নীচে চলে গেছে",
    startBtn: "শুরু করুন",
    rotate: "ফোনটি পাশে ঘোরান",
    instructions:
      "বোল্ডারটি চূড়ায় নিয়ে যান। ডেস্কটপে {k1} দিয়ে ঠেলে দিন, {k2} দিয়ে পাথরটি নিক্ষেপ করুন ও পেছনে দৌড়ান। মোবাইলে নিচের বাঁয়ে জয়স্টিক দিয়ে ঠেলে দিন এবং নিচের ডানে {k3} বোতাম দিয়ে পাথরটি উপরে নিক্ষেপ করুন।",
  },
  pt: {
    muteOn: "som ligado",
    muteOff: "som desligado",
    cycle: "ciclo",
    helpAdvance: "avançar",
    helpPushBack: "empurrar para trás",
    helpThrow: "lançar a rocha",
    keySpace: "Espaço",
    throwBtn: "lançar",
    continueBtn: "Continuar",
    stillDown: "a rocha voltou ao fundo",
    startBtn: "Começar",
    rotate: "Vira o telemóvel",
    instructions:
      "Leve a rocha até o topo. No desktop empurre com {k1}, lance-a com {k2} e corra atrás. No celular empurre com o joystick no canto inferior esquerdo e use {k3} no canto inferior direito para lançá-la ladeira acima.",
  },
};
