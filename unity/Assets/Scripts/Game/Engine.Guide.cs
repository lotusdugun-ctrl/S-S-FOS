using System;
using System.Collections.Generic;

namespace Sisifos.Game
{
    /// <summary>
    /// What keeps the climb readable and kind to a new player:
    /// <list type="bullet">
    /// <item>one message at a time: messages wait their turn instead of writing over each other,
    /// and only show while he is climbing (not over a story scene or the start card);</item>
    /// <item>the hill's systems open one by one: the gods' goals from the 2nd climb, the Moirai's
    /// altar from the 5th, the revolt from the 8th, each named the climb it opens;</item>
    /// <item>help where players get stuck: held at a barricade, told how to break it; holding
    /// on without rhythm on the first climbs, reminded of the ring;</item>
    /// <item>the rhythm made to feel like something: a bell at 5 and 10 perfect heaves in a row,
    /// a soft fall when a long run breaks;</item>
    /// <item>the book's milestones (every 50 sayings) and days in a row, celebrated.</item>
    /// </list>
    /// </summary>
    public partial class SisyphusEngine
    {
        // ---------------------------------------------------------------- one message at a time

        private struct Toast
        {
            public string text;
            public string col;
        }

        private readonly List<Toast> toastQueue = new List<Toast>();

        /// <summary>a message for the player, shown when the one before it has gone</summary>
        internal void showGameplayToast(string text, string col = "rgba(246, 232, 200, 0.95)")
        {
            if (string.IsNullOrEmpty(text)) return;
            if (this.gameplayToastText == text && this.gameplayToastT < 2.8) return;
            foreach (var q in this.toastQueue) if (q.text == text) return;
            // a burst of events: keep the latest few, not a backlog that runs for a minute
            if (this.toastQueue.Count >= 3) this.toastQueue.RemoveAt(0);
            this.toastQueue.Add(new Toast { text = text, col = col });
        }

        /// <summary>once a frame: the next message, when the screen is free for it</summary>
        private void pumpToasts()
        {
            if (this.toastQueue.Count == 0) return;
            if (this.gameplayToastText != null && this.gameplayToastT < 2.8) return;
            if (this.phase != "playing" || !this.active || this.cutscene || this.finaleOn) return;
            var q = this.toastQueue[0];
            this.toastQueue.RemoveAt(0);
            this.gameplayToastText = q.text;
            this.gameplayToastCol = q.col;
            this.gameplayToastT = 0;
        }

        // ---------------------------------------------------------------- systems, one by one

        internal const int UNLOCK_TRIALS = 2;
        internal const int UNLOCK_SHRINE = 5;
        internal const int UNLOCK_REVOLT = 8;

        private static readonly string[] INTRO_TRIALS =
        {
            "Yeni: tanrılar sana yolda küçük sınavlar verecek",
            "New: the gods will set you small trials on the way",
            "新内容：众神会在途中给你小小的试炼",
            "नया: देवता रास्ते में तुम्हें छोटी परीक्षाएँ देंगे",
            "Nuevo: los dioses te pondrán pequeñas pruebas en el camino",
            "Nouveau : les dieux te donneront de petites épreuves en chemin",
            "جديد: ستضع الآلهة أمامك اختبارات صغيرة في الطريق",
            "নতুন: দেবতারা পথে তোমাকে ছোট পরীক্ষা দেবেন",
            "Novo: os deuses vão dar-te pequenas provas pelo caminho",
        };

        private static readonly string[] INTRO_SHRINE =
        {
            "Yeni: Moiraların sunağı yolda belirebilir",
            "New: the altar of the Moirai may appear on the path",
            "新内容：命运女神的祭坛可能出现在路上",
            "नया: नियति देवियों की वेदी रास्ते में दिख सकती है",
            "Nuevo: el altar de las Moiras puede aparecer en el camino",
            "Nouveau : l'autel des Moires peut apparaître sur le chemin",
            "جديد: قد يظهر مذبح الموراي على الطريق",
            "নতুন: পথে মোইরাদের বেদি দেখা দিতে পারে",
            "Novo: o altar das Moiras pode surgir no caminho",
        };

        private static readonly string[] INTRO_REVOLT =
        {
            "Yeni: kusursuz hamleler isyanı büyütür; dolunca durdurulamazsın",
            "New: perfect heaves feed your revolt; when it fills, nothing stops you",
            "新内容：完美发力会积累反抗；满了之后无人能挡",
            "नया: सटीक प्रहार विद्रोह भरते हैं; भरने पर तुम्हें कोई नहीं रोक सकता",
            "Nuevo: los empujes perfectos alimentan la rebelión; llena, nada te detiene",
            "Nouveau : les poussées parfaites nourrissent la révolte ; pleine, rien ne t'arrête",
            "جديد: الدفعات المثالية تغذي تمردك؛ حين يمتلئ لا يوقفك شيء",
            "নতুন: নিখুঁত ঠেলা বিদ্রোহ বাড়ায়; ভরে গেলে কেউ থামাতে পারে না",
            "Novo: impulsos perfeitos alimentam a revolta; cheia, nada te para",
        };

        /// <summary>the climb a system opens on, named once</summary>
        private string unlockIntro(int li)
        {
            int c = (int)this.cycles;
            if (c == UNLOCK_TRIALS) return INTRO_TRIALS[li];
            if (c == UNLOCK_SHRINE) return INTRO_SHRINE[li];
            if (c == UNLOCK_REVOLT) return INTRO_REVOLT[li];
            return null;
        }

        // ---------------------------------------------------------------- help where players get stuck

        private double barricadeHeldT = 0;
        private double barricadeHintFor = -1;

        private static readonly string[] HINT_BARRICADE =
        {
            "Mermer engel: halka kapanırken it ya da kayayı fırlat",
            "Marble barricade: heave as the ring closes, or throw the stone",
            "大理石路障：在光环收拢时发力，或把巨石抛出",
            "संगमरमर अवरोध: छल्ला बंद होते ही धकेलो, या पत्थर फेंको",
            "Barricada de mármol: empuja cuando se cierre el anillo, o lanza la roca",
            "Barricade de marbre : pousse quand l'anneau se ferme, ou lance le rocher",
            "حاجز رخامي: ادفع حين تنغلق الحلقة، أو ارمِ الصخرة",
            "মার্বেল বাধা: বলয় বন্ধ হতেই ঠেলো, অথবা পাথর ছোড়ো",
            "Barricada de mármore: empurra quando o anel fechar, ou lança a rocha",
        };

        /// <summary>called each frame the barricade holds the stone back</summary>
        private void onBarricadeHold(double dt)
        {
            this.barricadeHeldT += dt;
            if (this.barricadeHeldT > 1.6 && this.barricadeHintFor != this.cycles)
            {
                this.barricadeHintFor = this.cycles;
                this.showGameplayToast(HINT_BARRICADE[langIdx(this.lang)], "rgba(255, 226, 170, 0.98)");
            }
        }

        private double sincePerfectT = 0;
        private double heldT = 0;
        private double rhythmHintFor = -1;

        private static readonly string[] HINT_RHYTHM =
        {
            "Sürekli basma: halka kapanırken bas, ritim nefes kazandırır",
            "Don't just hold on: press as the ring closes; rhythm wins back breath",
            "别一直按住：在光环收拢时按下，节奏能恢复气息",
            "लगातार मत दबाओ: छल्ला बंद होते ही दबाओ, लय से श्वास लौटती है",
            "No mantengas pulsado: pulsa cuando se cierre el anillo; el ritmo devuelve aliento",
            "Ne reste pas appuyé : appuie quand l'anneau se ferme ; le rythme rend le souffle",
            "لا تضغط باستمرار: اضغط حين تنغلق الحلقة، فالإيقاع يعيد النَّفَس",
            "চেপে ধরে থেকো না: বলয় বন্ধ হতেই চাপো, ছন্দে শ্বাস ফেরে",
            "Não segures sempre: carrega quando o anel fechar; o ritmo devolve o fôlego",
        };

        /// <summary>
        /// the first climbs: a player who only holds the push and never finds the beat runs out of
        /// breath and slides back without knowing why. Say it once a climb, gently.
        /// </summary>
        private void watchRhythm(double dt)
        {
            if (this.phase != "playing" || !this.active || this.cutscene || this.calm) return;
            this.sincePerfectT += dt;
            this.heldT = this.input > 0.15 ? this.heldT + dt : 0;
            if (this.cycles > 5 || this.rhythmHintFor == this.cycles || !this.tutorialDone) return;
            if (this.heldT > 6 && this.sincePerfectT > 9 && this.breath < 0.5)
            {
                this.rhythmHintFor = this.cycles;
                this.showGameplayToast(HINT_RHYTHM[langIdx(this.lang)], "rgba(255, 236, 196, 0.98)");
            }
        }

        // ---------------------------------------------------------------- the rhythm, felt

        /// <summary>after each heave: bells for a long run, a soft fall when one breaks</summary>
        private void onHeaveFelt(string q, double prevStreak)
        {
            if (q == "perfect")
            {
                this.sincePerfectT = 0;
                if (this.streak == 5 || this.streak == 10 || (this.streak > 10 && this.streak % 10 == 0))
                {
                    this.audio.streakBell(this.streak >= 10 ? 2 : 1);
                    this.haptic(new double[] { 15, 30, 25 });
                    this.shake = Math.Max(this.shake, this.streak >= 10 ? 0.28 : 0.16);
                    this.flash = Math.Max(this.flash, this.streak >= 10 ? 0.22 : 0.12);
                }
            }
            else if (prevStreak >= 4)
            {
                this.audio.chainBreak();
            }
        }

        // ---------------------------------------------------------------- milestones in the book and in days

        private static readonly string[] BOOK_MILESTONE =
        {
            "✦ {n} söz! Bilgelik Defteri büyüyor",
            "✦ {n} sayings! The Book of Wisdom grows",
            "✦ {n} 句箴言！智慧之书在增长",
            "✦ {n} सूक्तियाँ! ज्ञान की पुस्तक बढ़ रही है",
            "✦ ¡{n} frases! El Libro de la Sabiduría crece",
            "✦ {n} pensées ! Le Livre de la Sagesse grandit",
            "✦ {n} حكمة! كتاب الحكمة يكبر",
            "✦ {n}টি বাণী! জ্ঞানের বই বাড়ছে",
            "✦ {n} frases! O Livro da Sabedoria cresce",
        };

        /// <summary>a page has gone into the book: every fiftieth is a small occasion</summary>
        private void onPageAdded()
        {
            int n = this.unlocked.Count;
            if (n <= 0 || n % 50 != 0) return;
            this.showGameplayToast(BOOK_MILESTONE[langIdx(this.lang)].Replace("{n}", Js.str(n)), "rgba(255, 228, 150, 0.98)");
            this.audio.streakBell(2);
        }

        private static readonly string[] DAY_STREAK =
        {
            "🔥 {n} gün üst üste! Dağ seni bekliyordu",
            "🔥 {n} days in a row! The hill was waiting for you",
            "🔥 连续 {n} 天！山一直在等你",
            "🔥 लगातार {n} दिन! पहाड़ तुम्हारा इंतज़ार कर रहा था",
            "🔥 ¡{n} días seguidos! La colina te esperaba",
            "🔥 {n} jours d'affilée ! La colline t'attendait",
            "🔥 {n} أيام متتالية! الجبل كان ينتظرك",
            "🔥 টানা {n} দিন! পাহাড় তোমার অপেক্ষায় ছিল",
            "🔥 {n} dias seguidos! A colina esperava por ti",
        };

        /// <summary>days in a row the player has come back (said on the first climb of the day)</summary>
        public void welcomeBack(int days)
        {
            if (days < 2) return;
            this.showGameplayToast(DAY_STREAK[langIdx(this.lang)].Replace("{n}", Js.str(days)), "rgba(255, 210, 150, 0.98)");
        }
    }
}
