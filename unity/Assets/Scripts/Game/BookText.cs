// Ported from src/game/bookText.ts.
using System;
using System.Collections.Generic;

namespace Sisifos.Game
{
    /// <summary>an entry of BOOK_TEXT</summary>
    public sealed class BookTextEntry
    {
        public string[] chapters;
        public string contents;
        public string complete;
        public string prev;
        public string next;
    }

    public static class BookText
    {
        /// <summary>the Book of Wisdom's own words: its twelve chapters and its controls</summary>
        public static readonly Dictionary<string, BookTextEntry> BOOK_TEXT = new Dictionary<string, BookTextEntry>
        {
            ["tr"] = new BookTextEntry
            {
                chapters = new string[]
                {
                    "İlk Bilgeler",
                    "Atina",
                    "Okullar",
                    "Sahne ve Tarih",
                    "Roma",
                    "Çin",
                    "Hint ve İran",
                    "Anadolu",
                    "Rönesans",
                    "Aydınlanma",
                    "On Dokuzuncu Yüzyıl",
                    "Modern Çağ",
                },
                contents = "İçindekiler",
                complete = "Tamamlandı",
                prev = "Önceki sayfa",
                next = "Sonraki sayfa",
            },
            ["en"] = new BookTextEntry
            {
                chapters = new string[]
                {
                    "The First Sages",
                    "Athens",
                    "The Schools",
                    "Stage and Chronicle",
                    "Rome",
                    "China",
                    "India and Persia",
                    "Anatolia",
                    "Renaissance",
                    "Enlightenment",
                    "The Nineteenth Century",
                    "The Modern Age",
                },
                contents = "Contents",
                complete = "Complete",
                prev = "Previous page",
                next = "Next page",
            },
            ["zh"] = new BookTextEntry
            {
                chapters = new string[]
                {
                    "最初的智者",
                    "雅典",
                    "学派",
                    "舞台与史书",
                    "罗马",
                    "中国",
                    "印度与波斯",
                    "安纳托利亚",
                    "文艺复兴",
                    "启蒙时代",
                    "十九世纪",
                    "现代",
                },
                contents = "目录",
                complete = "已完成",
                prev = "上一页",
                next = "下一页",
            },
            ["hi"] = new BookTextEntry
            {
                chapters = new string[]
                {
                    "प्रथम ज्ञानी",
                    "एथेंस",
                    "दार्शनिक संप्रदाय",
                    "रंगमंच और इतिहास",
                    "रोम",
                    "चीन",
                    "भारत और फ़ारस",
                    "अनातोलिया",
                    "पुनर्जागरण",
                    "प्रबोधन",
                    "उन्नीसवीं सदी",
                    "आधुनिक युग",
                },
                contents = "विषय-सूची",
                complete = "पूर्ण",
                prev = "पिछला पृष्ठ",
                next = "अगला पृष्ठ",
            },
            ["es"] = new BookTextEntry
            {
                chapters = new string[]
                {
                    "Los primeros sabios",
                    "Atenas",
                    "Las escuelas",
                    "Escena y crónica",
                    "Roma",
                    "China",
                    "India y Persia",
                    "Anatolia",
                    "Renacimiento",
                    "Ilustración",
                    "El siglo XIX",
                    "La era moderna",
                },
                contents = "Índice",
                complete = "Completo",
                prev = "Página anterior",
                next = "Página siguiente",
            },
            ["fr"] = new BookTextEntry
            {
                chapters = new string[]
                {
                    "Les premiers sages",
                    "Athènes",
                    "Les écoles",
                    "Scène et chronique",
                    "Rome",
                    "La Chine",
                    "L'Inde et la Perse",
                    "L'Anatolie",
                    "La Renaissance",
                    "Les Lumières",
                    "Le XIXᵉ siècle",
                    "L'âge moderne",
                },
                contents = "Sommaire",
                complete = "Achevé",
                prev = "Page précédente",
                next = "Page suivante",
            },
            ["ar"] = new BookTextEntry
            {
                chapters = new string[]
                {
                    "الحكماء الأوائل",
                    "أثينا",
                    "المدارس",
                    "المسرح والتاريخ",
                    "روما",
                    "الصين",
                    "الهند وفارس",
                    "الأناضول",
                    "عصر النهضة",
                    "عصر التنوير",
                    "القرن التاسع عشر",
                    "العصر الحديث",
                },
                contents = "الفهرس",
                complete = "مكتمل",
                prev = "الصفحة السابقة",
                next = "الصفحة التالية",
            },
            ["bn"] = new BookTextEntry
            {
                chapters = new string[]
                {
                    "প্রথম জ্ঞানীরা",
                    "এথেন্স",
                    "দার্শনিক ধারা",
                    "মঞ্চ ও ইতিহাস",
                    "রোম",
                    "চীন",
                    "ভারত ও পারস্য",
                    "আনাতোলিয়া",
                    "রেনেসাঁস",
                    "আলোকায়ন",
                    "উনিশ শতক",
                    "আধুনিক যুগ",
                },
                contents = "সূচিপত্র",
                complete = "সম্পূর্ণ",
                prev = "আগের পৃষ্ঠা",
                next = "পরের পৃষ্ঠা",
            },
            ["pt"] = new BookTextEntry
            {
                chapters = new string[]
                {
                    "Os primeiros sábios",
                    "Atenas",
                    "As escolas",
                    "Palco e crónica",
                    "Roma",
                    "China",
                    "Índia e Pérsia",
                    "Anatólia",
                    "Renascimento",
                    "Iluminismo",
                    "O século XIX",
                    "A era moderna",
                },
                contents = "Índice",
                complete = "Completo",
                prev = "Página anterior",
                next = "Página seguinte",
            },
        };

        /// <summary>chapter names by language</summary>
        public static readonly Dictionary<string, string[]> CHAPTER_NAMES = MakeChapterNames();

        static Dictionary<string, string[]> MakeChapterNames()
        {
            var d = new Dictionary<string, string[]>();
            foreach (var kv in BOOK_TEXT) d[kv.Key] = kv.Value.chapters;
            return d;
        }
    }
}
