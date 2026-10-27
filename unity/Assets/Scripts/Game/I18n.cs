// Ported from src/i18n.ts: the interface's words in all nine languages. The TS module is
// pure data (no React or DOM in it); LangCode is a plain string here.
using System;
using System.Collections.Generic;

namespace Sisifos.Game
{
    /// <summary>an entry of LANGUAGES</summary>
    public sealed class LanguageInfo
    {
        public string code;
        public string native;
    }

    /// <summary>one language's interface strings</summary>
    public sealed class UIStrings
    {
        public string muteOn;
        public string muteOff;
        public string cycle;
        public string helpAdvance;
        public string helpPushBack;
        public string helpThrow;
        public string keySpace;
        public string throwBtn;
        public string continueBtn;
        public string stillDown;
        public string startBtn;
        public string rotate;
        public string instructions;
        public string instructions2;
        public string helpRest;
        public string helpRhythm;
        public string thisClimb;
        public string bestClimb;
        public string newBest;
        public string hintKick;
        public string hintRest;
        public string installBtn;
        public string iosInstall;
        public string streak;
        public string walkHint;
        public string walkHintTouch;
        public string bookBtn;
        public string bookTitle;
        public string bookCount;
        public string bookSub;
        public string bookLocked;
        public string bookEmpty;
        public string newPage;
        public string newPageSub;
        public string closeBtn;
        public string hapticsOn;
        public string hapticsOff;
        public string filterAll;
        public string filterFound;
        public string pageNo;
        public string privacy;
        public string touchToBegin;
        public string backup;
        public string backupDesc;
        public string copyCode;
        public string shareCode;
        public string restore;
        public string restorePlaceholder;
        public string restoreOk;
        public string restoreBad;
        public string copied;
        public string ownTitle;
        public string ownDesc;
        public string ownPlaceholder;
        public string ownAuthorPlaceholder;
        public string save;
        public string saved;
        public string accessibility;
        public string bigText;
        public string boldRing;
        public string boldRingDesc;
        public string leftHanded;
        public string leftHandedDesc;
        public string records;
        public string recBest;
        public string recWeek;
        public string recStreak;
        public string recSummits;
        public string recPages;
        public string recSeals;
        public string shareRecords;
        public string chapterDone;
        public string keepsakeDesc;
        public string instructionsTouch;
        public string hintPush;
        public string hintPushTouch;
        public string hintRhythm;
        public string hintBreathe;
        public string dailyTitle;
        public string dailyAdded;
        public string share;
        public string settings;
        public string language;
        public string sound;
        public string vibration;
        public string calmMode;
        public string calmDesc;
        public string on;
        public string off;
        public string sealsTitle;
        public string sealEarned;
        public string weatherRain;
        public string weatherFog;
        public string weatherStorm;

        /// <summary><c>t[key]</c>: a string by its field name (null when there is none)</summary>
        public string this[string key]
        {
            get
            {
                switch (key)
                {
                    case "muteOn": return muteOn;
                    case "muteOff": return muteOff;
                    case "cycle": return cycle;
                    case "helpAdvance": return helpAdvance;
                    case "helpPushBack": return helpPushBack;
                    case "helpThrow": return helpThrow;
                    case "keySpace": return keySpace;
                    case "throwBtn": return throwBtn;
                    case "continueBtn": return continueBtn;
                    case "stillDown": return stillDown;
                    case "startBtn": return startBtn;
                    case "rotate": return rotate;
                    case "instructions": return instructions;
                    case "instructions2": return instructions2;
                    case "helpRest": return helpRest;
                    case "helpRhythm": return helpRhythm;
                    case "thisClimb": return thisClimb;
                    case "bestClimb": return bestClimb;
                    case "newBest": return newBest;
                    case "hintKick": return hintKick;
                    case "hintRest": return hintRest;
                    case "installBtn": return installBtn;
                    case "iosInstall": return iosInstall;
                    case "streak": return streak;
                    case "walkHint": return walkHint;
                    case "walkHintTouch": return walkHintTouch;
                    case "bookBtn": return bookBtn;
                    case "bookTitle": return bookTitle;
                    case "bookCount": return bookCount;
                    case "bookSub": return bookSub;
                    case "bookLocked": return bookLocked;
                    case "bookEmpty": return bookEmpty;
                    case "newPage": return newPage;
                    case "newPageSub": return newPageSub;
                    case "closeBtn": return closeBtn;
                    case "hapticsOn": return hapticsOn;
                    case "hapticsOff": return hapticsOff;
                    case "filterAll": return filterAll;
                    case "filterFound": return filterFound;
                    case "pageNo": return pageNo;
                    case "privacy": return privacy;
                    case "touchToBegin": return touchToBegin;
                    case "backup": return backup;
                    case "backupDesc": return backupDesc;
                    case "copyCode": return copyCode;
                    case "shareCode": return shareCode;
                    case "restore": return restore;
                    case "restorePlaceholder": return restorePlaceholder;
                    case "restoreOk": return restoreOk;
                    case "restoreBad": return restoreBad;
                    case "copied": return copied;
                    case "ownTitle": return ownTitle;
                    case "ownDesc": return ownDesc;
                    case "ownPlaceholder": return ownPlaceholder;
                    case "ownAuthorPlaceholder": return ownAuthorPlaceholder;
                    case "save": return save;
                    case "saved": return saved;
                    case "accessibility": return accessibility;
                    case "bigText": return bigText;
                    case "boldRing": return boldRing;
                    case "boldRingDesc": return boldRingDesc;
                    case "leftHanded": return leftHanded;
                    case "leftHandedDesc": return leftHandedDesc;
                    case "records": return records;
                    case "recBest": return recBest;
                    case "recWeek": return recWeek;
                    case "recStreak": return recStreak;
                    case "recSummits": return recSummits;
                    case "recPages": return recPages;
                    case "recSeals": return recSeals;
                    case "shareRecords": return shareRecords;
                    case "chapterDone": return chapterDone;
                    case "keepsakeDesc": return keepsakeDesc;
                    case "instructionsTouch": return instructionsTouch;
                    case "hintPush": return hintPush;
                    case "hintPushTouch": return hintPushTouch;
                    case "hintRhythm": return hintRhythm;
                    case "hintBreathe": return hintBreathe;
                    case "dailyTitle": return dailyTitle;
                    case "dailyAdded": return dailyAdded;
                    case "share": return share;
                    case "settings": return settings;
                    case "language": return language;
                    case "sound": return sound;
                    case "vibration": return vibration;
                    case "calmMode": return calmMode;
                    case "calmDesc": return calmDesc;
                    case "on": return on;
                    case "off": return off;
                    case "sealsTitle": return sealsTitle;
                    case "sealEarned": return sealEarned;
                    case "weatherRain": return weatherRain;
                    case "weatherFog": return weatherFog;
                    case "weatherStorm": return weatherStorm;
                }
                return null;
            }
        }
    }

    public static class I18n
    {
        public static readonly LanguageInfo[] LANGUAGES = new LanguageInfo[]
        {
            new LanguageInfo
            {
                code = "tr",
                native = "Türkçe",
            },
            new LanguageInfo
            {
                code = "en",
                native = "English",
            },
            new LanguageInfo
            {
                code = "zh",
                native = "中文",
            },
            new LanguageInfo
            {
                code = "hi",
                native = "हिन्दी",
            },
            new LanguageInfo
            {
                code = "es",
                native = "Español",
            },
            new LanguageInfo
            {
                code = "fr",
                native = "Français",
            },
            new LanguageInfo
            {
                code = "ar",
                native = "العربية",
            },
            new LanguageInfo
            {
                code = "bn",
                native = "বাংলা",
            },
            new LanguageInfo
            {
                code = "pt",
                native = "Português",
            },
        };

        /// <summary>the hills, by level id</summary>
        public static readonly Dictionary<string, Dictionary<string, string>> LEVEL_NAMES = new Dictionary<string, Dictionary<string, string>>
        {
            ["tartarus"] = new Dictionary<string, string>
            {
                ["tr"] = "Tartaros Yamacı",
                ["en"] = "Slope of Tartarus",
                ["zh"] = "塔尔塔罗斯斜坡",
                ["hi"] = "टार्टरस की ढलान",
                ["es"] = "Ladera del Tártaro",
                ["fr"] = "Pente du Tartare",
                ["ar"] = "سفح تارتاروس",
                ["bn"] = "টার্টারাসের ঢাল",
                ["pt"] = "Encosta do Tártaro",
            },
            ["scree"] = new Dictionary<string, string>
            {
                ["tr"] = "Çakıl Yamacı",
                ["en"] = "The Scree",
                ["zh"] = "碎石坡",
                ["hi"] = "कंकड़ों की ढलान",
                ["es"] = "La Pedrera",
                ["fr"] = "L'Éboulis",
                ["ar"] = "منحدر الحصى",
                ["bn"] = "নুড়ির ঢাল",
                ["pt"] = "A Encosta de Cascalho",
            },
            ["windy"] = new Dictionary<string, string>
            {
                ["tr"] = "Rüzgârlı Sırt",
                ["en"] = "The Windy Ridge",
                ["zh"] = "风之山脊",
                ["hi"] = "हवाओं की चोटी",
                ["es"] = "La Cresta del Viento",
                ["fr"] = "La Crête des Vents",
                ["ar"] = "حافة الرياح",
                ["bn"] = "হাওয়ার শৈলশিরা",
                ["pt"] = "A Crista do Vento",
            },
            ["ruins"] = new Dictionary<string, string>
            {
                ["tr"] = "Harabeler",
                ["en"] = "The Ruins",
                ["zh"] = "废墟",
                ["hi"] = "खंडहर",
                ["es"] = "Las Ruinas",
                ["fr"] = "Les Ruines",
                ["ar"] = "الأطلال",
                ["bn"] = "ধ্বংসাবশেষ",
                ["pt"] = "As Ruínas",
            },
        };

        public static readonly Dictionary<string, string> EPIGRAPHS = new Dictionary<string, string>
        {
            ["tr"] = "Sisyphos'u mutlu hayal etmek gerekir.",
            ["en"] = "One must imagine Sisyphus happy.",
            ["zh"] = "必须想象西西弗斯是幸福的。",
            ["hi"] = "हमें सिसिफस को सुखी कल्पना करना चाहिए।",
            ["es"] = "Hay que imaginar a Sísifo feliz.",
            ["fr"] = "Il faut imaginer Sisyphe heureux.",
            ["ar"] = "يجب أن نتخيل سيزيف سعيدًا.",
            ["bn"] = "সিসিফাসকে সুখী কল্পনা করা দরকার।",
            ["pt"] = "É preciso imaginar Sísifo feliz.",
        };

        public static readonly Dictionary<string, UIStrings> T = new Dictionary<string, UIStrings>
        {
            ["tr"] = new UIStrings
            {
                muteOn = "ses açık",
                muteOff = "ses kapalı",
                cycle = "döngü",
                helpAdvance = "ilerlet",
                helpPushBack = "geri it",
                helpThrow = "kayayı fırlat",
                keySpace = "Boşluk",
                throwBtn = "fırlat",
                continueBtn = "Devam Et",
                stillDown = "taş yine aşağıda",
                startBtn = "Başla",
                rotate = "Telefonu yan çevir",
                instructions = "Kayayı zirveye taşı. Masaüstünde {k1} tuşlarıyla it, {k2} ile kayayı fırlat ve peşinden koş. Mobilde sol alttaki joystikle it, sağ alttaki {k3} tuşuyla kayayı yukarı fırlat.",
                instructions2 = "İtmek nefes harcar; bırakınca omuz verip dinlenir, kaya yerinde kalır. Başının üstündeki halka kapanırken bas: güçlü bir hamle olur. Dik basamakları ancak fırlatarak ya da ritimle aşarsın; aştığın basamaktan kaya geri kaymaz.",
                helpRest = "bırak: nefeslen",
                helpRhythm = "halka kapanınca bas",
                thisClimb = "bu tırmanış",
                bestClimb = "en iyi",
                newBest = "yeni rekor",
                hintKick = "Bu basamak itilerek aşılmaz: dibinde {k} ile fırlat",
                hintRest = "Nefesin bitti: bırak, kaya yerinde kalır, nefeslen",
                installBtn = "Uygulama olarak yükle",
                iosInstall = "iPhone'da: Paylaş ⎋ → Ana Ekrana Ekle",
                streak = "kusursuz",
                walkHint = "Bir tuşa basılı tut: taşa geri yürü",
                walkHintTouch = "Joystiği tut: taşa geri yürü",
                bookBtn = "Defter",
                bookTitle = "Bilgelik Defteri",
                bookCount = "{n} / {total} sayfa",
                bookSub = "Her zirvede yeni bir sayfa açılır.",
                bookLocked = "Henüz bulunmadı",
                bookEmpty = "İlk sayfa seni zirvede bekliyor.",
                newPage = "Yeni sayfa",
                newPageSub = "Bilgelik Defteri'ne eklendi",
                closeBtn = "Kapat",
                hapticsOn = "Titreşim açık",
                hapticsOff = "Titreşim kapalı",
                filterAll = "Tümü",
                filterFound = "Bulunanlar",
                pageNo = "Sayfa {n}",
                privacy = "Gizlilik",
                touchToBegin = "Başlamak için dokun",
                backup = "Yedek",
                backupDesc = "Defterini, mühürlerini ve ilerlemeni yeni bir telefona taşı.",
                copyCode = "Yedek kodunu kopyala",
                shareCode = "Kodu paylaş",
                restore = "Geri yükle",
                restorePlaceholder = "Yedek kodunu buraya yapıştır",
                restoreOk = "Geri yüklendi, oyun yeniden açılıyor…",
                restoreBad = "Bu bir yedek kodu değil.",
                copied = "Kopyalandı",
                ownTitle = "Senin sözün",
                ownDesc = "Kendi sözünü yaz; her yedinci zirvede gökyüzüne yazılır.",
                ownPlaceholder = "Sözün…",
                ownAuthorPlaceholder = "İmzan",
                save = "Kaydet",
                saved = "Kaydedildi",
                accessibility = "Erişilebilirlik",
                bigText = "Büyük yazı",
                boldRing = "Belirgin nefes halkası",
                boldRingDesc = "Daha büyük ve kalın; renge değil şekle dayanır.",
                leftHanded = "Kontrolleri ters çevir",
                leftHandedDesc = "Joystick sağda, Fırlat solda.",
                records = "Rekorlar",
                recBest = "En iyi tırmanış",
                recWeek = "Bu haftanın en iyisi",
                recStreak = "En uzun kusursuz seri",
                recSummits = "Toplam zirve",
                recPages = "Bulunan sayfa",
                recSeals = "Mühür",
                shareRecords = "Rekorlarını paylaş",
                chapterDone = "Bölüm tamamlandı",
                keepsakeDesc = "Tepelerde bu bölüm için bir anıt yükseldi.",
                instructionsTouch = "Kayayı zirveye taşı. Sol alttaki joystiği sağa doğru tut ve it; sağ alttaki Fırlat ile kayayı dik basamakların üstüne at ve peşinden koş.",
                hintPush = "→ ya da D tuşunu basılı tut: kayayı it",
                hintPushTouch = "Joystiği sağa doğru tut: kayayı it",
                hintRhythm = "Başının üstündeki halka kapanırken bırakıp yeniden bas: güçlü bir hamle",
                hintBreathe = "Nefesin azalıyor: bırak, omuz verip dinlen; kaya yerinde kalır",
                dailyTitle = "Günün sözü",
                dailyAdded = "Bilgelik Defteri'ne eklendi",
                share = "Paylaş",
                settings = "Ayarlar",
                language = "Dil",
                sound = "Ses",
                vibration = "Titreşim",
                calmMode = "Sakin mod",
                calmDesc = "Nefes hiç tükenmez: yalnızca tırmanış, gökyüzü ve sözler.",
                on = "Açık",
                off = "Kapalı",
                sealsTitle = "Mühürler",
                sealEarned = "Mühür kazanıldı",
                weatherRain = "Yağmur",
                weatherFog = "Sis",
                weatherStorm = "Fırtına",
            },
            ["en"] = new UIStrings
            {
                muteOn = "sound on",
                muteOff = "sound off",
                cycle = "cycle",
                helpAdvance = "advance",
                helpPushBack = "push back",
                helpThrow = "launch the boulder",
                keySpace = "Space",
                throwBtn = "throw",
                continueBtn = "Continue",
                stillDown = "the boulder is at the bottom again",
                startBtn = "Start",
                rotate = "Turn your phone sideways",
                instructions = "Roll the boulder to the summit. On desktop push with {k1}, launch it with {k2} and sprint after it. On mobile push with the joystick bottom-left and hit {k3} bottom-right to launch the boulder upward.",
                instructions2 = "Pushing costs breath; let go and he braces, resting while the stone holds. Press as the ring above his head closes for a strong heave. The steep ledges only give to a launch or a good rhythm — and once over one, the stone can't slip back below it.",
                helpRest = "release: catch breath",
                helpRhythm = "press as the ring closes",
                thisClimb = "this climb",
                bestClimb = "best",
                newBest = "new best",
                hintKick = "You can't push up this ledge: launch with {k} at its foot",
                hintRest = "Out of breath: let go — he braces, the stone holds",
                installBtn = "Install as app",
                iosInstall = "On iPhone: Share ⎋ → Add to Home Screen",
                streak = "perfect",
                walkHint = "Hold any key: walk back down to the stone",
                walkHintTouch = "Hold the joystick: walk back down to the stone",
                bookBtn = "Notebook",
                bookTitle = "Book of Wisdom",
                bookCount = "{n} / {total} pages",
                bookSub = "Every summit opens a new page.",
                bookLocked = "Not found yet",
                bookEmpty = "Your first page waits at the summit.",
                newPage = "New page",
                newPageSub = "Added to the Book of Wisdom",
                closeBtn = "Close",
                hapticsOn = "Vibration on",
                hapticsOff = "Vibration off",
                filterAll = "All",
                filterFound = "Found",
                pageNo = "Page {n}",
                privacy = "Privacy",
                touchToBegin = "Touch to begin",
                backup = "Backup",
                backupDesc = "Carry your book, seals and progress to a new phone.",
                copyCode = "Copy backup code",
                shareCode = "Share code",
                restore = "Restore",
                restorePlaceholder = "Paste your backup code here",
                restoreOk = "Restored, restarting the game…",
                restoreBad = "That is not a backup code.",
                copied = "Copied",
                ownTitle = "Your own saying",
                ownDesc = "Write your own saying; every seventh summit it is written into the sky.",
                ownPlaceholder = "Your saying…",
                ownAuthorPlaceholder = "Your name",
                save = "Save",
                saved = "Saved",
                accessibility = "Accessibility",
                bigText = "Larger text",
                boldRing = "Bold breath ring",
                boldRingDesc = "Larger and heavier; tells states apart by shape, not colour.",
                leftHanded = "Swap the controls",
                leftHandedDesc = "Joystick on the right, Throw on the left.",
                records = "Records",
                recBest = "Best climb",
                recWeek = "Best this week",
                recStreak = "Longest perfect streak",
                recSummits = "Summits",
                recPages = "Pages found",
                recSeals = "Seals",
                shareRecords = "Share your records",
                chapterDone = "Chapter complete",
                keepsakeDesc = "A keepsake for this chapter now stands on the hills.",
                instructionsTouch = "Push the boulder to the summit. Hold the joystick (bottom left) to the right to push; tap Throw (bottom right) to launch it over the steep ledges and run after it.",
                hintPush = "Hold → or D: push the boulder",
                hintPushTouch = "Hold the joystick to the right: push the boulder",
                hintRhythm = "As the ring over his head closes, let go and press again: a powerful heave",
                hintBreathe = "Breath running low: let go and lean in to rest; the stone stays put",
                dailyTitle = "Saying of the day",
                dailyAdded = "Added to the Book of Wisdom",
                share = "Share",
                settings = "Settings",
                language = "Language",
                sound = "Sound",
                vibration = "Vibration",
                calmMode = "Calm mode",
                calmDesc = "Your breath never runs out: just the climb, the sky and the sayings.",
                on = "On",
                off = "Off",
                sealsTitle = "Seals",
                sealEarned = "Seal earned",
                weatherRain = "Rain",
                weatherFog = "Fog",
                weatherStorm = "Storm",
            },
            ["zh"] = new UIStrings
            {
                muteOn = "声音开",
                muteOff = "声音关",
                cycle = "循环",
                helpAdvance = "前进",
                helpPushBack = "向后推",
                helpThrow = "发射巨石",
                keySpace = "空格",
                throwBtn = "发射",
                continueBtn = "继续",
                stillDown = "巨石又滚回了山脚",
                startBtn = "开始",
                rotate = "请将手机横过来",
                instructions = "把巨石滚到山顶。桌面端用 {k1} 推动，用 {k2} 发射巨石并追赶。移动端用左下角摇杆推动，点右下角 {k3} 将巨石抛向高处。",
                instructions2 = "推动会消耗体力；松开时他会顶住巨石休息，巨石不会滑落。在他头顶的圆环合拢时按下，可以发力猛推。陡峭的台阶只能靠发射或节奏越过——越过之后，巨石就不会再滑回台阶以下。",
                helpRest = "松开：喘口气",
                helpRhythm = "圆环合拢时按下",
                thisClimb = "本次攀登",
                bestClimb = "最佳",
                newBest = "新纪录",
                hintKick = "这道台阶推不上去：在坡脚用 {k} 发射",
                hintRest = "没气了：松开——他会顶住，巨石不会动",
                installBtn = "安装为应用",
                iosInstall = "iPhone：分享 ⎋ → 添加到主屏幕",
                streak = "完美",
                walkHint = "按住任意键：走回巨石",
                walkHintTouch = "按住摇杆：走回巨石",
                bookBtn = "笔记",
                bookTitle = "智慧之书",
                bookCount = "已得 {n} / {total} 页",
                bookSub = "每登顶一次，便翻开新的一页。",
                bookLocked = "尚未发现",
                bookEmpty = "第一页在山顶等你。",
                newPage = "新的一页",
                newPageSub = "已收入智慧之书",
                closeBtn = "关闭",
                hapticsOn = "振动：开",
                hapticsOff = "振动：关",
                filterAll = "全部",
                filterFound = "已发现",
                pageNo = "第 {n} 页",
                privacy = "隐私",
                touchToBegin = "轻触开始",
                backup = "备份",
                backupDesc = "把你的书、印章和进度带到新手机上。",
                copyCode = "复制备份码",
                shareCode = "分享代码",
                restore = "恢复",
                restorePlaceholder = "在此粘贴备份码",
                restoreOk = "已恢复，正在重启游戏…",
                restoreBad = "这不是备份码。",
                copied = "已复制",
                ownTitle = "你自己的格言",
                ownDesc = "写下你自己的格言；每第七次登顶，它都会被写上天空。",
                ownPlaceholder = "你的格言…",
                ownAuthorPlaceholder = "你的名字",
                save = "保存",
                saved = "已保存",
                accessibility = "无障碍",
                bigText = "大号文字",
                boldRing = "醒目呼吸环",
                boldRingDesc = "更大更粗；用形状而非颜色区分状态。",
                leftHanded = "左右互换操控",
                leftHandedDesc = "摇杆在右，投掷在左。",
                records = "纪录",
                recBest = "最佳攀登",
                recWeek = "本周最佳",
                recStreak = "最长完美连击",
                recSummits = "登顶次数",
                recPages = "已找到的书页",
                recSeals = "印章",
                shareRecords = "分享你的纪录",
                chapterDone = "章节完成",
                keepsakeDesc = "山丘上为本章立起了一座纪念碑。",
                instructionsTouch = "把巨石推上山顶。按住左下角的摇杆向右推；点击右下角的投掷，把巨石抛上陡峭的台阶，再追上去。",
                hintPush = "按住 → 或 D：推动巨石",
                hintPushTouch = "按住摇杆向右：推动巨石",
                hintRhythm = "头顶的圆环合拢时，松开再按：发力猛推",
                hintBreathe = "气息快耗尽了：松手，用肩抵住歇一歇；巨石不会滑落",
                dailyTitle = "每日一言",
                dailyAdded = "已加入智慧之书",
                share = "分享",
                settings = "设置",
                language = "语言",
                sound = "声音",
                vibration = "振动",
                calmMode = "平静模式",
                calmDesc = "气息永不耗尽：只有攀登、天空和箴言。",
                on = "开",
                off = "关",
                sealsTitle = "印章",
                sealEarned = "获得印章",
                weatherRain = "雨",
                weatherFog = "雾",
                weatherStorm = "暴风雨",
            },
            ["hi"] = new UIStrings
            {
                muteOn = "ध्वनि चालू",
                muteOff = "ध्वनि बंद",
                cycle = "चक्र",
                helpAdvance = "आगे बढ़ाएँ",
                helpPushBack = "पीछे धकेलें",
                helpThrow = "पत्थर फेंकें",
                keySpace = "स्पेस",
                throwBtn = "फेंकें",
                continueBtn = "जारी रखें",
                stillDown = "पत्थर फिर से नीचे है",
                startBtn = "शुरू करें",
                rotate = "अपना फ़ोन घुमाएँ",
                instructions = "बोल्डर को चोटी तक ले जाएँ। डेस्कटॉप पर {k1} से धकेलें, {k2} से पत्थर फेंकें और पीछे दौड़ें। मोबाइल पर नीचे बाएँ जॉयस्टिक से धकेलें और नीचे दाएँ {k3} बटन से पत्थर ऊपर फेंकें।",
                instructions2 = "धकेलने में साँस लगती है; छोड़ने पर वह कंधा टिकाकर आराम करता है और पत्थर अपनी जगह रुका रहता है। उसके सिर के ऊपर का घेरा बंद होते ही दबाएँ — ज़ोरदार धक्का लगेगा। खड़ी चट्टानें केवल फेंककर या लय से पार होती हैं, और पार करने के बाद पत्थर उनसे नीचे नहीं फिसलता।",
                helpRest = "छोड़ें: साँस लें",
                helpRhythm = "घेरा बंद होने पर दबाएँ",
                thisClimb = "यह चढ़ाई",
                bestClimb = "सर्वश्रेष्ठ",
                newBest = "नया रिकॉर्ड",
                hintKick = "यह चट्टान धकेलकर पार नहीं होगी: नीचे से {k} से फेंकें",
                hintRest = "साँस ख़त्म: छोड़ दें — वह टिका रहेगा, पत्थर नहीं हिलेगा",
                installBtn = "ऐप के रूप में इंस्टॉल करें",
                iosInstall = "iPhone पर: शेयर ⎋ → होम स्क्रीन में जोड़ें",
                streak = "परफ़ेक्ट",
                walkHint = "कोई भी कुंजी दबाए रखें: पत्थर तक वापस चलें",
                walkHintTouch = "जॉयस्टिक पकड़े रखें: पत्थर तक वापस चलें",
                bookBtn = "पुस्तिका",
                bookTitle = "ज्ञान की पुस्तक",
                bookCount = "{n} / {total} पृष्ठ",
                bookSub = "हर शिखर पर एक नया पृष्ठ खुलता है।",
                bookLocked = "अभी नहीं मिला",
                bookEmpty = "आपका पहला पृष्ठ शिखर पर प्रतीक्षा कर रहा है।",
                newPage = "नया पृष्ठ",
                newPageSub = "ज्ञान की पुस्तक में जोड़ा गया",
                closeBtn = "बंद करें",
                hapticsOn = "कंपन चालू",
                hapticsOff = "कंपन बंद",
                filterAll = "सभी",
                filterFound = "मिले हुए",
                pageNo = "पृष्ठ {n}",
                privacy = "गोपनीयता",
                touchToBegin = "शुरू करने के लिए छुएँ",
                backup = "बैकअप",
                backupDesc = "अपनी पुस्तक, मुहरें और प्रगति नए फ़ोन पर ले जाओ।",
                copyCode = "बैकअप कोड कॉपी करो",
                shareCode = "कोड शेयर करो",
                restore = "रीस्टोर करो",
                restorePlaceholder = "अपना बैकअप कोड यहाँ पेस्ट करो",
                restoreOk = "रीस्टोर हो गया, गेम फिर से शुरू हो रहा है…",
                restoreBad = "यह बैकअप कोड नहीं है।",
                copied = "कॉपी हो गया",
                ownTitle = "तुम्हारी अपनी सूक्ति",
                ownDesc = "अपनी सूक्ति लिखो; हर सातवें शिखर पर वह आसमान में लिखी जाएगी।",
                ownPlaceholder = "तुम्हारी सूक्ति…",
                ownAuthorPlaceholder = "तुम्हारा नाम",
                save = "सहेजो",
                saved = "सहेजा गया",
                accessibility = "सुलभता",
                bigText = "बड़ा टेक्स्ट",
                boldRing = "गाढ़ा साँस-घेरा",
                boldRingDesc = "बड़ा और मोटा; स्थितियों को रंग से नहीं, आकार से पहचानता है।",
                leftHanded = "कंट्रोल उलटो",
                leftHandedDesc = "जॉयस्टिक दाईं ओर, फेंको बाईं ओर।",
                records = "रिकॉर्ड",
                recBest = "सबसे अच्छी चढ़ाई",
                recWeek = "इस हफ़्ते की सबसे अच्छी",
                recStreak = "सबसे लंबी बेदाग़ लड़ी",
                recSummits = "शिखर",
                recPages = "मिले पन्ने",
                recSeals = "मुहरें",
                shareRecords = "अपने रिकॉर्ड शेयर करो",
                chapterDone = "अध्याय पूरा हुआ",
                keepsakeDesc = "पहाड़ियों पर अब इस अध्याय की एक निशानी खड़ी है।",
                instructionsTouch = "पत्थर को चोटी तक पहुँचाओ। धकेलने के लिए बाईं ओर नीचे का जॉयस्टिक दाईं ओर दबाए रखो; खड़ी चट्टानों के ऊपर पत्थर उछालने के लिए दाईं ओर नीचे फेंको दबाओ और उसके पीछे दौड़ो।",
                hintPush = "→ या D दबाए रखो: पत्थर धकेलो",
                hintPushTouch = "जॉयस्टिक दाईं ओर दबाए रखो: पत्थर धकेलो",
                hintRhythm = "सिर के ऊपर का घेरा बंद होते ही छोड़ो और फिर दबाओ: ज़ोरदार धक्का",
                hintBreathe = "साँस कम हो रही है: छोड़ो, कंधा टिकाकर आराम करो; पत्थर अपनी जगह रहेगा",
                dailyTitle = "आज का वचन",
                dailyAdded = "ज्ञान की पुस्तक में जोड़ा गया",
                share = "शेयर करें",
                settings = "सेटिंग्स",
                language = "भाषा",
                sound = "ध्वनि",
                vibration = "कंपन",
                calmMode = "शांत मोड",
                calmDesc = "साँस कभी खत्म नहीं होती: बस चढ़ाई, आकाश और वचन।",
                on = "चालू",
                off = "बंद",
                sealsTitle = "मुहरें",
                sealEarned = "मुहर मिली",
                weatherRain = "बारिश",
                weatherFog = "कोहरा",
                weatherStorm = "तूफ़ान",
            },
            ["es"] = new UIStrings
            {
                muteOn = "sonido activado",
                muteOff = "sonido silenciado",
                cycle = "ciclo",
                helpAdvance = "avanzar",
                helpPushBack = "empujar atrás",
                helpThrow = "lanzar la roca",
                keySpace = "Espacio",
                throwBtn = "lanzar",
                continueBtn = "Continuar",
                stillDown = "la roca volvió a estar abajo",
                startBtn = "Comenzar",
                rotate = "Gira el teléfono",
                instructions = "Lleva la roca hasta la cima. En escritorio empuja con {k1}, lánzala con {k2} y corre tras ella. En móvil empuja con el joystick abajo a la izquierda y usa {k3} abajo a la derecha para lanzarla cuesta arriba.",
                instructions2 = "Empujar gasta aliento; si sueltas, se apoya y descansa mientras la roca se queda quieta. Pulsa cuando el anillo sobre su cabeza se cierre para dar un empujón fuerte. Los escalones empinados solo se superan lanzando la roca o con buen ritmo, y una vez arriba la roca ya no resbala por debajo.",
                helpRest = "suelta: recupera aliento",
                helpRhythm = "pulsa al cerrarse el anillo",
                thisClimb = "esta subida",
                bestClimb = "mejor",
                newBest = "nuevo récord",
                hintKick = "Este escalón no se sube empujando: lánzala con {k} al pie",
                hintRest = "Sin aliento: suelta — se apoya y la roca se queda quieta",
                installBtn = "Instalar como app",
                iosInstall = "En iPhone: Compartir ⎋ → Añadir a pantalla de inicio",
                streak = "perfecto",
                walkHint = "Mantén una tecla: baja caminando hasta la roca",
                walkHintTouch = "Mantén el joystick: baja caminando hasta la roca",
                bookBtn = "Cuaderno",
                bookTitle = "Libro de la sabiduría",
                bookCount = "{n} / {total} páginas",
                bookSub = "Cada cumbre abre una página nueva.",
                bookLocked = "Aún no encontrada",
                bookEmpty = "Tu primera página te espera en la cumbre.",
                newPage = "Página nueva",
                newPageSub = "Añadida al Libro de la sabiduría",
                closeBtn = "Cerrar",
                hapticsOn = "Vibración activada",
                hapticsOff = "Vibración desactivada",
                filterAll = "Todas",
                filterFound = "Encontradas",
                pageNo = "Página {n}",
                privacy = "Privacidad",
                touchToBegin = "Toca para empezar",
                backup = "Copia de seguridad",
                backupDesc = "Lleva tu libro, tus sellos y tu progreso a un teléfono nuevo.",
                copyCode = "Copiar código de respaldo",
                shareCode = "Compartir código",
                restore = "Restaurar",
                restorePlaceholder = "Pega aquí tu código de respaldo",
                restoreOk = "Restaurado, reiniciando el juego…",
                restoreBad = "Eso no es un código de respaldo.",
                copied = "Copiado",
                ownTitle = "Tu propia frase",
                ownDesc = "Escribe tu propia frase; en cada séptima cima quedará escrita en el cielo.",
                ownPlaceholder = "Tu frase…",
                ownAuthorPlaceholder = "Tu nombre",
                save = "Guardar",
                saved = "Guardado",
                accessibility = "Accesibilidad",
                bigText = "Texto más grande",
                boldRing = "Anillo de respiración marcado",
                boldRingDesc = "Más grande y grueso; distingue los estados por la forma, no por el color.",
                leftHanded = "Invertir los controles",
                leftHandedDesc = "Joystick a la derecha, Lanzar a la izquierda.",
                records = "Récords",
                recBest = "Mejor escalada",
                recWeek = "Mejor de la semana",
                recStreak = "Racha perfecta más larga",
                recSummits = "Cimas",
                recPages = "Páginas encontradas",
                recSeals = "Sellos",
                shareRecords = "Comparte tus récords",
                chapterDone = "Capítulo completado",
                keepsakeDesc = "Un recuerdo de este capítulo se alza ahora en las colinas.",
                instructionsTouch = "Lleva la roca a la cima. Mantén el joystick (abajo a la izquierda) hacia la derecha para empujar; toca Lanzar (abajo a la derecha) para subirla por los escalones empinados y corre tras ella.",
                hintPush = "Mantén → o D: empuja la roca",
                hintPushTouch = "Mantén el joystick a la derecha: empuja la roca",
                hintRhythm = "Cuando el anillo sobre su cabeza se cierre, suelta y pulsa otra vez: un empujón potente",
                hintBreathe = "Te falta el aliento: suelta y apóyate para descansar; la roca no se moverá",
                dailyTitle = "Frase del día",
                dailyAdded = "Añadida al Libro de la sabiduría",
                share = "Compartir",
                settings = "Ajustes",
                language = "Idioma",
                sound = "Sonido",
                vibration = "Vibración",
                calmMode = "Modo tranquilo",
                calmDesc = "El aliento nunca se agota: solo la subida, el cielo y las frases.",
                on = "Sí",
                off = "No",
                sealsTitle = "Sellos",
                sealEarned = "Sello obtenido",
                weatherRain = "Lluvia",
                weatherFog = "Niebla",
                weatherStorm = "Tormenta",
            },
            ["fr"] = new UIStrings
            {
                muteOn = "son activé",
                muteOff = "son coupé",
                cycle = "cycle",
                helpAdvance = "avancer",
                helpPushBack = "pousser en arrière",
                helpThrow = "lancer le rocher",
                keySpace = "Espace",
                throwBtn = "lancer",
                continueBtn = "Continuer",
                stillDown = "le rocher est redescendu",
                startBtn = "Commencer",
                rotate = "Tourne ton téléphone",
                instructions = "Porte le rocher jusqu'au sommet. Sur ordinateur, pousse avec {k1}, lance-le avec {k2} et cours derrière. Sur mobile, pousse avec le joystick en bas à gauche et appuie sur {k3} en bas à droite pour le lancer vers le haut.",
                instructions2 = "Pousser coûte du souffle ; relâche et il s'arc-boute pour souffler, le rocher reste en place. Appuie quand l'anneau au-dessus de sa tête se referme pour une poussée puissante. Les ressauts raides ne cèdent qu'à un lancer ou à un bon rythme — et une fois franchis, le rocher ne peut plus redescendre en dessous.",
                helpRest = "relâche : reprends ton souffle",
                helpRhythm = "appuie quand l'anneau se ferme",
                thisClimb = "cette montée",
                bestClimb = "record",
                newBest = "nouveau record",
                hintKick = "Ce ressaut ne se monte pas en poussant : lance avec {k} à son pied",
                hintRest = "À bout de souffle : relâche — il s'arc-boute, le rocher tient",
                installBtn = "Installer l'application",
                iosInstall = "Sur iPhone : Partager ⎋ → Sur l'écran d'accueil",
                streak = "parfait",
                walkHint = "Maintiens une touche : redescends vers le rocher",
                walkHintTouch = "Maintiens le joystick : redescends vers le rocher",
                bookBtn = "Carnet",
                bookTitle = "Livre de sagesse",
                bookCount = "{n} / {total} pages",
                bookSub = "Chaque sommet ouvre une nouvelle page.",
                bookLocked = "Pas encore trouvée",
                bookEmpty = "Ta première page t'attend au sommet.",
                newPage = "Nouvelle page",
                newPageSub = "Ajoutée au Livre de sagesse",
                closeBtn = "Fermer",
                hapticsOn = "Vibration activée",
                hapticsOff = "Vibration désactivée",
                filterAll = "Toutes",
                filterFound = "Trouvées",
                pageNo = "Page {n}",
                privacy = "Confidentialité",
                touchToBegin = "Touchez pour commencer",
                backup = "Sauvegarde",
                backupDesc = "Emporte ton livre, tes sceaux et ta progression sur un nouveau téléphone.",
                copyCode = "Copier le code de sauvegarde",
                shareCode = "Partager le code",
                restore = "Restaurer",
                restorePlaceholder = "Colle ton code de sauvegarde ici",
                restoreOk = "Restauré, redémarrage du jeu…",
                restoreBad = "Ce n'est pas un code de sauvegarde.",
                copied = "Copié",
                ownTitle = "Ta propre maxime",
                ownDesc = "Écris ta propre maxime ; à chaque septième sommet, elle s'inscrit dans le ciel.",
                ownPlaceholder = "Ta maxime…",
                ownAuthorPlaceholder = "Ton nom",
                save = "Enregistrer",
                saved = "Enregistré",
                accessibility = "Accessibilité",
                bigText = "Texte plus grand",
                boldRing = "Anneau de souffle marqué",
                boldRingDesc = "Plus grand et plus épais ; distingue les états par la forme, pas par la couleur.",
                leftHanded = "Inverser les commandes",
                leftHandedDesc = "Joystick à droite, Lancer à gauche.",
                records = "Records",
                recBest = "Meilleure ascension",
                recWeek = "Meilleure de la semaine",
                recStreak = "Plus longue série parfaite",
                recSummits = "Sommets",
                recPages = "Pages trouvées",
                recSeals = "Sceaux",
                shareRecords = "Partage tes records",
                chapterDone = "Chapitre terminé",
                keepsakeDesc = "Un souvenir de ce chapitre se dresse désormais sur les collines.",
                instructionsTouch = "Pousse le rocher jusqu'au sommet. Maintiens le joystick (en bas à gauche) vers la droite pour pousser ; touche Lancer (en bas à droite) pour le projeter par-dessus les marches abruptes, puis cours derrière.",
                hintPush = "Maintiens → ou D : pousse le rocher",
                hintPushTouch = "Maintiens le joystick vers la droite : pousse le rocher",
                hintRhythm = "Quand l'anneau au-dessus de sa tête se referme, relâche et appuie à nouveau : une poussée puissante",
                hintBreathe = "Tu manques de souffle : relâche et appuie-toi pour te reposer ; le rocher ne bouge pas",
                dailyTitle = "Parole du jour",
                dailyAdded = "Ajoutée au Livre de sagesse",
                share = "Partager",
                settings = "Paramètres",
                language = "Langue",
                sound = "Son",
                vibration = "Vibration",
                calmMode = "Mode calme",
                calmDesc = "Le souffle ne s'épuise jamais : juste l'ascension, le ciel et les paroles.",
                on = "Activé",
                off = "Désactivé",
                sealsTitle = "Sceaux",
                sealEarned = "Sceau obtenu",
                weatherRain = "Pluie",
                weatherFog = "Brouillard",
                weatherStorm = "Orage",
            },
            ["ar"] = new UIStrings
            {
                muteOn = "الصوت مفتوح",
                muteOff = "الصوت مغلق",
                cycle = "دورة",
                helpAdvance = "تقدّم",
                helpPushBack = "ادفع للخلف",
                helpThrow = "أطلق الصخرة",
                keySpace = "مسافة",
                throwBtn = "أطلق",
                continueBtn = "متابعة",
                stillDown = "الصخرة عادت إلى الأسفل",
                startBtn = "ابدأ",
                rotate = "أدر هاتفك أفقيًا",
                instructions = "اصعد بالصخرة إلى القمة. على الحاسوب ادفع باستخدام {k1}، واقذفها بمفتاح {k2} واركض خلفها. على الجوال ادفع بالعصا السفلية اليسرى واضغط {k3} أسفل اليمين لإطلاق الصخرة للأعلى.",
                instructions2 = "الدفع يستهلك النَّفَس؛ اترك المفتاح فيستند إلى الصخرة ويستريح وهي ثابتة. اضغط عندما تنغلق الحلقة فوق رأسه لتحصل على دفعة قوية. الدرجات الشديدة لا تُعبر إلا بالإطلاق أو بالإيقاع الجيد، وبعد عبورها لا تنزلق الصخرة إلى ما دونها.",
                helpRest = "اترك: التقط أنفاسك",
                helpRhythm = "اضغط عند انغلاق الحلقة",
                thisClimb = "هذا الصعود",
                bestClimb = "الأفضل",
                newBest = "رقم قياسي جديد",
                hintKick = "لا تُدفع الصخرة فوق هذه الدرجة: أطلقها بـ {k} عند أسفلها",
                hintRest = "انقطع نَفَسك: اترك — سيستند والصخرة ثابتة",
                installBtn = "ثبّت كتطبيق",
                iosInstall = "على iPhone: مشاركة ⎋ ← إضافة إلى الشاشة الرئيسية",
                streak = "مثالي",
                walkHint = "اضغط مطولًا على أي مفتاح: انزل ماشيًا إلى الصخرة",
                walkHintTouch = "اضغط مطولًا على العصا: انزل ماشيًا إلى الصخرة",
                bookBtn = "الدفتر",
                bookTitle = "كتاب الحكمة",
                bookCount = "{n} / {total} صفحة",
                bookSub = "كل قمة تفتح صفحة جديدة.",
                bookLocked = "لم تُكتشف بعد",
                bookEmpty = "صفحتك الأولى تنتظرك عند القمة.",
                newPage = "صفحة جديدة",
                newPageSub = "أُضيفت إلى كتاب الحكمة",
                closeBtn = "إغلاق",
                hapticsOn = "الاهتزاز مفعّل",
                hapticsOff = "الاهتزاز متوقف",
                filterAll = "الكل",
                filterFound = "المكتشفة",
                pageNo = "صفحة {n}",
                privacy = "الخصوصية",
                touchToBegin = "المس للبدء",
                backup = "نسخة احتياطية",
                backupDesc = "انقل كتابك وأختامك وتقدّمك إلى هاتف جديد.",
                copyCode = "انسخ رمز النسخ الاحتياطي",
                shareCode = "شارك الرمز",
                restore = "استعادة",
                restorePlaceholder = "الصق رمز النسخ الاحتياطي هنا",
                restoreOk = "تمت الاستعادة، جارٍ إعادة تشغيل اللعبة…",
                restoreBad = "هذا ليس رمز نسخ احتياطي.",
                copied = "تم النسخ",
                ownTitle = "مقولتك الخاصة",
                ownDesc = "اكتب مقولتك الخاصة؛ ففي كل قمة سابعة تُكتب في السماء.",
                ownPlaceholder = "مقولتك…",
                ownAuthorPlaceholder = "اسمك",
                save = "حفظ",
                saved = "تم الحفظ",
                accessibility = "إمكانية الوصول",
                bigText = "نص أكبر",
                boldRing = "حلقة تنفّس بارزة",
                boldRingDesc = "أكبر وأسمك؛ يميّز الحالات بالشكل لا باللون.",
                leftHanded = "تبديل أماكن عناصر التحكم",
                leftHandedDesc = "عصا التحكم يمينًا، وزر ارمِ يسارًا.",
                records = "الأرقام القياسية",
                recBest = "أفضل تسلّق",
                recWeek = "الأفضل هذا الأسبوع",
                recStreak = "أطول سلسلة مثالية",
                recSummits = "القمم",
                recPages = "الصفحات المكتشفة",
                recSeals = "الأختام",
                shareRecords = "شارك أرقامك القياسية",
                chapterDone = "اكتمل الفصل",
                keepsakeDesc = "يقف الآن على التلال تذكارٌ لهذا الفصل.",
                instructionsTouch = "ادفع الصخرة إلى القمة. أبقِ عصا التحكم (أسفل اليسار) نحو اليمين للدفع، واضغط ارمِ (أسفل اليمين) لقذفها فوق الدرجات الشديدة الانحدار ثم اركض خلفها.",
                hintPush = "أبقِ → أو D مضغوطًا: ادفع الصخرة",
                hintPushTouch = "أبقِ عصا التحكم نحو اليمين: ادفع الصخرة",
                hintRhythm = "عندما تنغلق الحلقة فوق رأسه، اترك ثم اضغط مجددًا: دفعة قوية",
                hintBreathe = "أنفاسك تنفد: اترك واستند بكتفك لترتاح؛ الصخرة تبقى مكانها",
                dailyTitle = "حكمة اليوم",
                dailyAdded = "أُضيفت إلى كتاب الحكمة",
                share = "مشاركة",
                settings = "الإعدادات",
                language = "اللغة",
                sound = "الصوت",
                vibration = "الاهتزاز",
                calmMode = "الوضع الهادئ",
                calmDesc = "لا تنفد أنفاسك أبدًا: فقط الصعود والسماء والحِكَم.",
                on = "تشغيل",
                off = "إيقاف",
                sealsTitle = "الأختام",
                sealEarned = "حصلت على ختم",
                weatherRain = "مطر",
                weatherFog = "ضباب",
                weatherStorm = "عاصفة",
            },
            ["bn"] = new UIStrings
            {
                muteOn = "শব্দ চালু",
                muteOff = "শব্দ বন্ধ",
                cycle = "চক্র",
                helpAdvance = "এগোও",
                helpPushBack = "পেছনে ঠেলে দিন",
                helpThrow = "বোল্ডার নিক্ষেপ",
                keySpace = "স্পেস",
                throwBtn = "নিক্ষেপ",
                continueBtn = "চালিয়ে যান",
                stillDown = "পাথর আবার নীচে চলে গেছে",
                startBtn = "শুরু করুন",
                rotate = "ফোনটি পাশে ঘোরান",
                instructions = "বোল্ডারটি চূড়ায় নিয়ে যান। ডেস্কটপে {k1} দিয়ে ঠেলে দিন, {k2} দিয়ে পাথরটি নিক্ষেপ করুন ও পেছনে দৌড়ান। মোবাইলে নিচের বাঁয়ে জয়স্টিক দিয়ে ঠেলে দিন এবং নিচের ডানে {k3} বোতাম দিয়ে পাথরটি উপরে নিক্ষেপ করুন।",
                instructions2 = "ঠেলতে দম খরচ হয়; ছেড়ে দিলে সে কাঁধ ঠেকিয়ে বিশ্রাম নেয়, পাথর জায়গায় থাকে। মাথার ওপরের বৃত্তটি বন্ধ হওয়ার মুহূর্তে চাপুন — জোরালো ধাক্কা হবে। খাড়া ধাপগুলো কেবল নিক্ষেপ বা ভালো ছন্দে পার হয়, আর পার হলে পাথর আর তার নিচে গড়িয়ে যায় না।",
                helpRest = "ছাড়ুন: দম নিন",
                helpRhythm = "বৃত্ত বন্ধ হলে চাপুন",
                thisClimb = "এই আরোহণ",
                bestClimb = "সেরা",
                newBest = "নতুন রেকর্ড",
                hintKick = "এই ধাপ ঠেলে ওঠা যায় না: গোড়ায় {k} দিয়ে নিক্ষেপ করুন",
                hintRest = "দম ফুরিয়েছে: ছেড়ে দিন — সে ঠেকিয়ে রাখবে, পাথর নড়বে না",
                installBtn = "অ্যাপ হিসেবে ইনস্টল করুন",
                iosInstall = "iPhone-এ: শেয়ার ⎋ → হোম স্ক্রিনে যোগ করুন",
                streak = "নিখুঁত",
                walkHint = "যেকোনো কী চেপে ধরুন: পাথরের কাছে হেঁটে নামুন",
                walkHintTouch = "জয়স্টিক ধরে রাখুন: পাথরের কাছে হেঁটে নামুন",
                bookBtn = "খাতা",
                bookTitle = "প্রজ্ঞার বই",
                bookCount = "{n} / {total} পৃষ্ঠা",
                bookSub = "প্রতিটি চূড়ায় একটি নতুন পৃষ্ঠা খোলে।",
                bookLocked = "এখনো পাওয়া যায়নি",
                bookEmpty = "তোমার প্রথম পৃষ্ঠা চূড়ায় অপেক্ষা করছে।",
                newPage = "নতুন পৃষ্ঠা",
                newPageSub = "প্রজ্ঞার বইয়ে যোগ হলো",
                closeBtn = "বন্ধ করুন",
                hapticsOn = "কম্পন চালু",
                hapticsOff = "কম্পন বন্ধ",
                filterAll = "সব",
                filterFound = "পাওয়া গেছে",
                pageNo = "পৃষ্ঠা {n}",
                privacy = "গোপনীয়তা",
                touchToBegin = "শুরু করতে ছুঁয়ে দিন",
                backup = "ব্যাকআপ",
                backupDesc = "তোমার বই, সিলমোহর আর অগ্রগতি নতুন ফোনে নিয়ে যাও।",
                copyCode = "ব্যাকআপ কোড কপি করো",
                shareCode = "কোড শেয়ার করো",
                restore = "রিস্টোর করো",
                restorePlaceholder = "তোমার ব্যাকআপ কোড এখানে পেস্ট করো",
                restoreOk = "রিস্টোর হয়েছে, গেম আবার চালু হচ্ছে…",
                restoreBad = "এটা ব্যাকআপ কোড নয়।",
                copied = "কপি হয়েছে",
                ownTitle = "তোমার নিজের বাণী",
                ownDesc = "নিজের বাণী লেখো; প্রতি সপ্তম চূড়ায় তা আকাশে লেখা হবে।",
                ownPlaceholder = "তোমার বাণী…",
                ownAuthorPlaceholder = "তোমার নাম",
                save = "সেভ করো",
                saved = "সেভ হয়েছে",
                accessibility = "অ্যাক্সেসিবিলিটি",
                bigText = "বড় লেখা",
                boldRing = "মোটা শ্বাস-বলয়",
                boldRingDesc = "আরও বড় ও মোটা; রঙ নয়, আকার দিয়ে অবস্থা বোঝায়।",
                leftHanded = "কন্ট্রোল উল্টে দাও",
                leftHandedDesc = "জয়স্টিক ডানে, ছোড়ো বাঁয়ে।",
                records = "রেকর্ড",
                recBest = "সেরা আরোহণ",
                recWeek = "এই সপ্তাহের সেরা",
                recStreak = "দীর্ঘতম নিখুঁত ধারা",
                recSummits = "চূড়া",
                recPages = "পাওয়া পাতা",
                recSeals = "সিলমোহর",
                shareRecords = "তোমার রেকর্ড শেয়ার করো",
                chapterDone = "অধ্যায় সম্পূর্ণ",
                keepsakeDesc = "পাহাড়ে এখন এই অধ্যায়ের একটি স্মারক দাঁড়িয়ে আছে।",
                instructionsTouch = "পাথরটা চূড়ায় তোলো। ঠেলতে নিচে বাঁয়ের জয়স্টিক ডান দিকে ধরে রাখো; খাড়া ধাপের ওপর দিয়ে পাথর ছুড়তে নিচে ডানের ছোড়ো চাপো, তারপর পেছনে দৌড়াও।",
                hintPush = "→ বা D চেপে ধরো: পাথর ঠেলো",
                hintPushTouch = "জয়স্টিক ডান দিকে ধরে রাখো: পাথর ঠেলো",
                hintRhythm = "মাথার ওপরের বৃত্ত বন্ধ হওয়ার মুহূর্তে ছেড়ে আবার চাপো: জোরালো ধাক্কা",
                hintBreathe = "দম ফুরিয়ে আসছে: ছেড়ে দাও, কাঁধ ঠেকিয়ে জিরিয়ে নাও; পাথর জায়গায় থাকবে",
                dailyTitle = "আজকের বাণী",
                dailyAdded = "প্রজ্ঞার বই-এ যোগ হলো",
                share = "শেয়ার করো",
                settings = "সেটিংস",
                language = "ভাষা",
                sound = "শব্দ",
                vibration = "কম্পন",
                calmMode = "শান্ত মোড",
                calmDesc = "দম কখনো ফুরায় না: শুধু চড়াই, আকাশ আর বাণী।",
                on = "চালু",
                off = "বন্ধ",
                sealsTitle = "সিলমোহর",
                sealEarned = "সিলমোহর অর্জিত",
                weatherRain = "বৃষ্টি",
                weatherFog = "কুয়াশা",
                weatherStorm = "ঝড়",
            },
            ["pt"] = new UIStrings
            {
                muteOn = "som ligado",
                muteOff = "som desligado",
                cycle = "ciclo",
                helpAdvance = "avançar",
                helpPushBack = "empurrar para trás",
                helpThrow = "lançar a rocha",
                keySpace = "Espaço",
                throwBtn = "lançar",
                continueBtn = "Continuar",
                stillDown = "a rocha voltou ao fundo",
                startBtn = "Começar",
                rotate = "Vira o telemóvel",
                instructions = "Leve a rocha até o topo. No desktop empurre com {k1}, lance-a com {k2} e corra atrás. No celular empurre com o joystick no canto inferior esquerdo e use {k3} no canto inferior direito para lançá-la ladeira acima.",
                instructions2 = "Empurrar gasta fôlego; solte e ele se apoia para descansar enquanto a rocha fica parada. Aperte quando o anel sobre a cabeça dele se fechar para um empurrão forte. Os degraus íngremes só cedem a um lançamento ou a um bom ritmo — e, depois de superados, a rocha não escorrega mais para baixo deles.",
                helpRest = "solte: recupere o fôlego",
                helpRhythm = "aperte quando o anel fechar",
                thisClimb = "esta subida",
                bestClimb = "melhor",
                newBest = "novo recorde",
                hintKick = "Este degrau não se sobe empurrando: lance com {k} no pé dele",
                hintRest = "Sem fôlego: solte — ele se apoia e a rocha fica parada",
                installBtn = "Instalar como app",
                iosInstall = "No iPhone: Compartilhar ⎋ → Adicionar à Tela de Início",
                streak = "perfeito",
                walkHint = "Segure uma tecla: desça caminhando até a rocha",
                walkHintTouch = "Segure o joystick: desça caminhando até a rocha",
                bookBtn = "Caderno",
                bookTitle = "Livro da sabedoria",
                bookCount = "{n} / {total} páginas",
                bookSub = "Cada cume abre uma página nova.",
                bookLocked = "Ainda não encontrada",
                bookEmpty = "A tua primeira página espera-te no cume.",
                newPage = "Página nova",
                newPageSub = "Adicionada ao Livro da sabedoria",
                closeBtn = "Fechar",
                hapticsOn = "Vibração ligada",
                hapticsOff = "Vibração desligada",
                filterAll = "Todas",
                filterFound = "Encontradas",
                pageNo = "Página {n}",
                privacy = "Privacidade",
                touchToBegin = "Toque para começar",
                backup = "Backup",
                backupDesc = "Leve seu livro, seus selos e seu progresso para um celular novo.",
                copyCode = "Copiar código de backup",
                shareCode = "Compartilhar código",
                restore = "Restaurar",
                restorePlaceholder = "Cole seu código de backup aqui",
                restoreOk = "Restaurado, reiniciando o jogo…",
                restoreBad = "Isso não é um código de backup.",
                copied = "Copiado",
                ownTitle = "Sua própria frase",
                ownDesc = "Escreva sua própria frase; a cada sétimo cume, ela é escrita no céu.",
                ownPlaceholder = "Sua frase…",
                ownAuthorPlaceholder = "Seu nome",
                save = "Salvar",
                saved = "Salvo",
                accessibility = "Acessibilidade",
                bigText = "Texto maior",
                boldRing = "Anel de respiração destacado",
                boldRingDesc = "Maior e mais grosso; diferencia os estados pela forma, não pela cor.",
                leftHanded = "Inverter os controles",
                leftHandedDesc = "Joystick à direita, Lançar à esquerda.",
                records = "Recordes",
                recBest = "Melhor escalada",
                recWeek = "Melhor da semana",
                recStreak = "Maior sequência perfeita",
                recSummits = "Cumes",
                recPages = "Páginas encontradas",
                recSeals = "Selos",
                shareRecords = "Compartilhe seus recordes",
                chapterDone = "Capítulo concluído",
                keepsakeDesc = "Uma lembrança deste capítulo agora se ergue nas colinas.",
                instructionsTouch = "Leve a pedra até o cume. Segure o joystick (canto inferior esquerdo) para a direita para empurrar; toque em Lançar (canto inferior direito) para jogá-la sobre os degraus íngremes e corra atrás dela.",
                hintPush = "Segure → ou D: empurre a pedra",
                hintPushTouch = "Segure o joystick para a direita: empurre a pedra",
                hintRhythm = "Quando o anel sobre a cabeça dele fechar, solte e pressione de novo: um empurrão forte",
                hintBreathe = "O fôlego está acabando: solte e apoie o ombro para descansar; a pedra fica no lugar",
                dailyTitle = "Frase do dia",
                dailyAdded = "Adicionada ao Livro da sabedoria",
                share = "Compartilhar",
                settings = "Configurações",
                language = "Idioma",
                sound = "Som",
                vibration = "Vibração",
                calmMode = "Modo calmo",
                calmDesc = "O fôlego nunca acaba: só a subida, o céu e as frases.",
                on = "Ligado",
                off = "Desligado",
                sealsTitle = "Selos",
                sealEarned = "Selo conquistado",
                weatherRain = "Chuva",
                weatherFog = "Neblina",
                weatherStorm = "Tempestade",
            },
        };
    }
}
