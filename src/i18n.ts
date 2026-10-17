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
  /** second paragraph of the start overlay: breath and rhythm */
  instructions2: string;
  /** key hint: letting go of the push key */
  helpRest: string;
  /** key hint: pressing on the beat */
  helpRhythm: string;
  /** stats */
  thisClimb: string;
  bestClimb: string;
  newBest: string;
  /** nudge when stuck at the foot of a ledge; {k} is the launch key/button */
  hintKick: string;
  /** nudge after running out of breath */
  hintRest: string;
  /** start card: install as an app (Android / desktop Chrome) */
  installBtn: string;
  /** start card: how to install on iPhone / iPad */
  iosInstall: string;
  /** streak of perfectly timed heaves, shown as "{n}× perfect" */
  streak: string;
  /** the walk back down: keyboard / touch */
  walkHint: string;
  walkHintTouch: string;
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
    instructions2:
      "İtmek nefes harcar; bırakınca omuz verip dinlenir, kaya yerinde kalır. Başının üstündeki halka kapanırken bas: güçlü bir hamle olur. Dik basamakları ancak fırlatarak ya da ritimle aşarsın; aştığın basamaktan kaya geri kaymaz.",
    helpRest: "bırak: nefeslen",
    helpRhythm: "halka kapanınca bas",
    thisClimb: "bu tırmanış",
    bestClimb: "en iyi",
    newBest: "yeni rekor",
    hintKick: "Bu basamak itilerek aşılmaz: dibinde {k} ile fırlat",
    hintRest: "Nefesin bitti: bırak, kaya yerinde kalır, nefeslen",
    installBtn: "Uygulama olarak yükle",
    iosInstall: "iPhone'da: Paylaş ⎋ → Ana Ekrana Ekle",
    streak: "kusursuz",
    walkHint: "Bir tuşa basılı tut: taşa geri yürü",
    walkHintTouch: "Joystiği tut: taşa geri yürü",
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
    instructions2:
      "Pushing costs breath; let go and he braces, resting while the stone holds. Press as the ring above his head closes for a strong heave. The steep ledges only give to a launch or a good rhythm — and once over one, the stone can't slip back below it.",
    helpRest: "release: catch breath",
    helpRhythm: "press as the ring closes",
    thisClimb: "this climb",
    bestClimb: "best",
    newBest: "new best",
    hintKick: "You can't push up this ledge: launch with {k} at its foot",
    hintRest: "Out of breath: let go — he braces, the stone holds",
    installBtn: "Install as app",
    iosInstall: "On iPhone: Share ⎋ → Add to Home Screen",
    streak: "perfect",
    walkHint: "Hold any key: walk back down to the stone",
    walkHintTouch: "Hold the joystick: walk back down to the stone",
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
    instructions2:
      "推动会消耗体力；松开时他会顶住巨石休息，巨石不会滑落。在他头顶的圆环合拢时按下，可以发力猛推。陡峭的台阶只能靠发射或节奏越过——越过之后，巨石就不会再滑回台阶以下。",
    helpRest: "松开：喘口气",
    helpRhythm: "圆环合拢时按下",
    thisClimb: "本次攀登",
    bestClimb: "最佳",
    newBest: "新纪录",
    hintKick: "这道台阶推不上去：在坡脚用 {k} 发射",
    hintRest: "没气了：松开——他会顶住，巨石不会动",
    installBtn: "安装为应用",
    iosInstall: "iPhone：分享 ⎋ → 添加到主屏幕",
    streak: "完美",
    walkHint: "按住任意键：走回巨石",
    walkHintTouch: "按住摇杆：走回巨石",
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
    instructions2:
      "धकेलने में साँस लगती है; छोड़ने पर वह कंधा टिकाकर आराम करता है और पत्थर अपनी जगह रुका रहता है। उसके सिर के ऊपर का घेरा बंद होते ही दबाएँ — ज़ोरदार धक्का लगेगा। खड़ी चट्टानें केवल फेंककर या लय से पार होती हैं, और पार करने के बाद पत्थर उनसे नीचे नहीं फिसलता।",
    helpRest: "छोड़ें: साँस लें",
    helpRhythm: "घेरा बंद होने पर दबाएँ",
    thisClimb: "यह चढ़ाई",
    bestClimb: "सर्वश्रेष्ठ",
    newBest: "नया रिकॉर्ड",
    hintKick: "यह चट्टान धकेलकर पार नहीं होगी: नीचे से {k} से फेंकें",
    hintRest: "साँस ख़त्म: छोड़ दें — वह टिका रहेगा, पत्थर नहीं हिलेगा",
    installBtn: "ऐप के रूप में इंस्टॉल करें",
    iosInstall: "iPhone पर: शेयर ⎋ → होम स्क्रीन में जोड़ें",
    streak: "परफ़ेक्ट",
    walkHint: "कोई भी कुंजी दबाए रखें: पत्थर तक वापस चलें",
    walkHintTouch: "जॉयस्टिक पकड़े रखें: पत्थर तक वापस चलें",
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
    instructions2:
      "Empujar gasta aliento; si sueltas, se apoya y descansa mientras la roca se queda quieta. Pulsa cuando el anillo sobre su cabeza se cierre para dar un empujón fuerte. Los escalones empinados solo se superan lanzando la roca o con buen ritmo, y una vez arriba la roca ya no resbala por debajo.",
    helpRest: "suelta: recupera aliento",
    helpRhythm: "pulsa al cerrarse el anillo",
    thisClimb: "esta subida",
    bestClimb: "mejor",
    newBest: "nuevo récord",
    hintKick: "Este escalón no se sube empujando: lánzala con {k} al pie",
    hintRest: "Sin aliento: suelta — se apoya y la roca se queda quieta",
    installBtn: "Instalar como app",
    iosInstall: "En iPhone: Compartir ⎋ → Añadir a pantalla de inicio",
    streak: "perfecto",
    walkHint: "Mantén una tecla: baja caminando hasta la roca",
    walkHintTouch: "Mantén el joystick: baja caminando hasta la roca",
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
    instructions2:
      "Pousser coûte du souffle ; relâche et il s'arc-boute pour souffler, le rocher reste en place. Appuie quand l'anneau au-dessus de sa tête se referme pour une poussée puissante. Les ressauts raides ne cèdent qu'à un lancer ou à un bon rythme — et une fois franchis, le rocher ne peut plus redescendre en dessous.",
    helpRest: "relâche : reprends ton souffle",
    helpRhythm: "appuie quand l'anneau se ferme",
    thisClimb: "cette montée",
    bestClimb: "record",
    newBest: "nouveau record",
    hintKick: "Ce ressaut ne se monte pas en poussant : lance avec {k} à son pied",
    hintRest: "À bout de souffle : relâche — il s'arc-boute, le rocher tient",
    installBtn: "Installer l'application",
    iosInstall: "Sur iPhone : Partager ⎋ → Sur l'écran d'accueil",
    streak: "parfait",
    walkHint: "Maintiens une touche : redescends vers le rocher",
    walkHintTouch: "Maintiens le joystick : redescends vers le rocher",
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
    instructions2:
      "الدفع يستهلك النَّفَس؛ اترك المفتاح فيستند إلى الصخرة ويستريح وهي ثابتة. اضغط عندما تنغلق الحلقة فوق رأسه لتحصل على دفعة قوية. الدرجات الشديدة لا تُعبر إلا بالإطلاق أو بالإيقاع الجيد، وبعد عبورها لا تنزلق الصخرة إلى ما دونها.",
    helpRest: "اترك: التقط أنفاسك",
    helpRhythm: "اضغط عند انغلاق الحلقة",
    thisClimb: "هذا الصعود",
    bestClimb: "الأفضل",
    newBest: "رقم قياسي جديد",
    hintKick: "لا تُدفع الصخرة فوق هذه الدرجة: أطلقها بـ {k} عند أسفلها",
    hintRest: "انقطع نَفَسك: اترك — سيستند والصخرة ثابتة",
    installBtn: "ثبّت كتطبيق",
    iosInstall: "على iPhone: مشاركة ⎋ ← إضافة إلى الشاشة الرئيسية",
    streak: "مثالي",
    walkHint: "اضغط مطولًا على أي مفتاح: انزل ماشيًا إلى الصخرة",
    walkHintTouch: "اضغط مطولًا على العصا: انزل ماشيًا إلى الصخرة",
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
    instructions2:
      "ঠেলতে দম খরচ হয়; ছেড়ে দিলে সে কাঁধ ঠেকিয়ে বিশ্রাম নেয়, পাথর জায়গায় থাকে। মাথার ওপরের বৃত্তটি বন্ধ হওয়ার মুহূর্তে চাপুন — জোরালো ধাক্কা হবে। খাড়া ধাপগুলো কেবল নিক্ষেপ বা ভালো ছন্দে পার হয়, আর পার হলে পাথর আর তার নিচে গড়িয়ে যায় না।",
    helpRest: "ছাড়ুন: দম নিন",
    helpRhythm: "বৃত্ত বন্ধ হলে চাপুন",
    thisClimb: "এই আরোহণ",
    bestClimb: "সেরা",
    newBest: "নতুন রেকর্ড",
    hintKick: "এই ধাপ ঠেলে ওঠা যায় না: গোড়ায় {k} দিয়ে নিক্ষেপ করুন",
    hintRest: "দম ফুরিয়েছে: ছেড়ে দিন — সে ঠেকিয়ে রাখবে, পাথর নড়বে না",
    installBtn: "অ্যাপ হিসেবে ইনস্টল করুন",
    iosInstall: "iPhone-এ: শেয়ার ⎋ → হোম স্ক্রিনে যোগ করুন",
    streak: "নিখুঁত",
    walkHint: "যেকোনো কী চেপে ধরুন: পাথরের কাছে হেঁটে নামুন",
    walkHintTouch: "জয়স্টিক ধরে রাখুন: পাথরের কাছে হেঁটে নামুন",
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
    instructions2:
      "Empurrar gasta fôlego; solte e ele se apoia para descansar enquanto a rocha fica parada. Aperte quando o anel sobre a cabeça dele se fechar para um empurrão forte. Os degraus íngremes só cedem a um lançamento ou a um bom ritmo — e, depois de superados, a rocha não escorrega mais para baixo deles.",
    helpRest: "solte: recupere o fôlego",
    helpRhythm: "aperte quando o anel fechar",
    thisClimb: "esta subida",
    bestClimb: "melhor",
    newBest: "novo recorde",
    hintKick: "Este degrau não se sobe empurrando: lance com {k} no pé dele",
    hintRest: "Sem fôlego: solte — ele se apoia e a rocha fica parada",
    installBtn: "Instalar como app",
    iosInstall: "No iPhone: Compartilhar ⎋ → Adicionar à Tela de Início",
    streak: "perfeito",
    walkHint: "Segure uma tecla: desça caminhando até a rocha",
    walkHintTouch: "Segure o joystick: desça caminhando até a rocha",
  },
};
