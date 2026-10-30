// Ported from src/game/save.ts. localStorage is Store (same key, same JSON).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// The climb in progress, so that closing the game in the middle of one does
    /// not send him back to the foot of the hill: where the stone was, how far it
    /// had been worked up, or how far down the way back he had walked.
    /// </summary>
    public sealed class RunSave
    {
        /// <summary>the cycle and hill it belongs to: anything else is stale</summary>
        public double cycles;
        public string lv;
        /// <summary>"playing" | "descent"</summary>
        public string phase;
        public double x;
        public double checkpoint;
        public double maxX;
        public double climbT;
        public double breath;
        public string weather;
        /// <summary>on the way down</summary>
        public double walkX;
        public double walkFrom;
        public double hourFrom;
        public double hourTo;
        public double summitX;

        public RunSave Clone() { return (RunSave)MemberwiseClone(); }

        public Dictionary<string, object> toJson()
        {
            return new Dictionary<string, object>
            {
                ["cycles"] = cycles,
                ["lv"] = lv,
                ["phase"] = phase,
                ["x"] = x,
                ["checkpoint"] = checkpoint,
                ["maxX"] = maxX,
                ["climbT"] = climbT,
                ["breath"] = breath,
                ["weather"] = weather,
                ["walkX"] = walkX,
                ["walkFrom"] = walkFrom,
                ["hourFrom"] = hourFrom,
                ["hourTo"] = hourTo,
                ["summitX"] = summitX,
            };
        }
    }

    /// <summary>
    /// What the hill remembers between visits: how many times he has been down it,
    /// his fastest way up, and the two choices a player should only have to make
    /// once. Storage can be missing, full or throw outright, so every access is
    /// guarded and the game simply starts fresh when it is not there.
    /// </summary>
    public sealed class SaveData
    {
        public double cycles;
        public double bestClimb;
        /// <summary>LangCode or null</summary>
        public string lang;
        public bool muted;
        /// <summary>vibration on touch devices</summary>
        public bool haptics;
        /// <summary>aphorisms found, by index, in the order they were found</summary>
        public List<int> quotes;
        /// <summary>calm mode: breath never runs out</summary>
        public bool calm;
        /// <summary>the first climb's lessons have been seen</summary>
        public bool tutorialDone;
        /// <summary>seals earned</summary>
        public List<string> seals;
        /// <summary>hills summited, by level id</summary>
        public List<string> hills;
        /// <summary>the last day (YYYY-MM-DD) the saying of the day was added to the book</summary>
        public string daily;
        /// <summary>the player's own saying, and the name to sign it with</summary>
        public string ownText;
        public string ownAuthor;
        /// <summary>accessibility: larger interface text, the bold breath ring, controls swapped</summary>
        public bool bigText;
        /// <summary>a lighter background for slower phones: no far figures, ridge life or season drift</summary>
        public bool lightBg;
        public bool boldRing;
        public bool leftHanded;
        /// <summary>records: longest run of perfect heaves, and the best climb of this week</summary>
        public double bestStreak;
        public string weekKey;
        public double weekBest;
        /// <summary>the story so far</summary>
        public SagaState saga;
        /// <summary>the climb in progress, if the game was closed during one (or null)</summary>
        public RunSave run;
        /// <summary>when this was written (ms), to tell the newer of two copies</summary>
        public double savedAt;

        /// <summary><c>{ ...data }</c>: a shallow copy</summary>
        public SaveData Clone() { return (SaveData)MemberwiseClone(); }

        /// <summary>the object JSON.stringify would write, keys in the web build's order</summary>
        public Dictionary<string, object> toJson()
        {
            var q = new List<object>();
            foreach (var n in quotes) q.Add((double)n);
            return new Dictionary<string, object>
            {
                ["cycles"] = cycles,
                ["bestClimb"] = bestClimb,
                ["lang"] = lang,
                ["muted"] = muted,
                ["haptics"] = haptics,
                ["quotes"] = q,
                ["calm"] = calm,
                ["tutorialDone"] = tutorialDone,
                ["seals"] = new List<object>(seals),
                ["hills"] = new List<object>(hills),
                ["daily"] = daily,
                ["ownText"] = ownText,
                ["ownAuthor"] = ownAuthor,
                ["bigText"] = bigText,
                ["lightBg"] = lightBg,
                ["boldRing"] = boldRing,
                ["leftHanded"] = leftHanded,
                ["bestStreak"] = bestStreak,
                ["weekKey"] = weekKey,
                ["weekBest"] = weekBest,
                ["saga"] = saga?.toJson(),
                ["run"] = run?.toJson(),
                ["savedAt"] = savedAt,
            };
        }
    }

    public static class Save
    {
        const string KEY = "sisyphus.save.v1";

        static SaveData EMPTY
        {
            // PORT: a fresh copy each time (the web spread EMPTY, whose arrays were never
            // mutated in place); saga is SAGA_START itself, as in the web build.
            get
            {
                return new SaveData
                {
                    cycles = 0,
                    bestClimb = 0,
                    lang = null,
                    muted = false,
                    haptics = true,
                    quotes = new List<int>(),
                    calm = false,
                    tutorialDone = false,
                    seals = new List<string>(),
                    hills = new List<string>(),
                    daily = "",
                    ownText = "",
                    ownAuthor = "",
                    bigText = false,
                    boldRing = false,
                    leftHanded = false,
                    bestStreak = 0,
                    weekKey = "",
                    weekBest = 0,
                    saga = Saga.SAGA_START,
                    run = null,
                    savedAt = 0,
                };
            }
        }

        static bool isLang(object v)
        {
            if (!(v is string s)) return false;
            foreach (var l in I18n.LANGUAGES) if (l.code == s) return true;
            return false;
        }

        public static SaveData loadSave()
        {
            try
            {
                var raw = Store.Get(KEY);
                if (string.IsNullOrEmpty(raw))
                {
                    var e = EMPTY;
                    e.saga = Saga.readSaga(null);
                    return e;
                }
                return parseSave(raw);
            }
            catch (Exception)
            {
                return EMPTY;
            }
        }

        static bool finite(object v, out double d)
        {
            d = 0;
            if (v is double x && !double.IsNaN(x) && !double.IsInfinity(x)) { d = x; return true; }
            return false;
        }

        static double num(object v, double d = 0) { return finite(v, out var x) ? x : d; }

        static RunSave readRun(object v)
        {
            var r = v as Dictionary<string, object>;
            // !v || typeof v !== "object": an array has no lv, so it is rejected below as well
            if (r == null) return null;
            object G(string k) { return r.TryGetValue(k, out var x) ? x : null; }
            var lv = G("lv") as string;
            var phase = G("phase") as string;
            if (lv == null || (phase != "playing" && phase != "descent")) return null;
            return new RunSave
            {
                cycles = Math.Max(0, Math.Floor(num(G("cycles")))),
                lv = lv,
                phase = phase,
                x = num(G("x"), 40),
                checkpoint = num(G("checkpoint")),
                maxX = num(G("maxX")),
                climbT = Math.Max(0, num(G("climbT"))),
                breath = Math.Max(0, Math.Min(1, num(G("breath"), 1))),
                weather = G("weather") is string w ? w : "clear",
                walkX = num(G("walkX")),
                walkFrom = num(G("walkFrom")),
                hourFrom = num(G("hourFrom")),
                hourTo = num(G("hourTo")),
                summitX = num(G("summitX")),
            };
        }

        /// <summary>JS String.prototype.slice(0, n) (UTF-16 units, as C# strings are)</summary>
        static string slice(string s, int n) { return s.Length > n ? s.Substring(0, n) : s; }

        static SaveData parseSave(string raw)
        {
            try
            {
                var parsed = Json.Parse(raw);
                // JSON.parse("null").cycles throws: the catch below gives EMPTY
                if (parsed == null) return EMPTY;
                var d = parsed as Dictionary<string, object> ?? new Dictionary<string, object>();
                object G(string k) { return d.TryGetValue(k, out var x) ? x : null; }
                var quotes = new List<int>();
                // PORT: whole numbers beyond int range (never written by the game) are dropped
                if (G("quotes") is List<object> qs)
                    foreach (var q in qs)
                        if (q is double n && n == Math.Floor(n) && !double.IsInfinity(n) && n >= 0 && n <= int.MaxValue) quotes.Add((int)n);
                List<string> strs(object v)
                {
                    var l = new List<string>();
                    if (v is List<object> a) foreach (var x in a) if (x is string s) l.Add(s);
                    return l;
                }
                double f;
                return new SaveData
                {
                    cycles = finite(G("cycles"), out f) ? Math.Max(0, Math.Floor(f)) : 0,
                    bestClimb = finite(G("bestClimb"), out f) ? Math.Max(0, f) : 0,
                    lang = isLang(G("lang")) ? (string)G("lang") : null,
                    muted = G("muted") is bool mu && mu,
                    haptics = !(G("haptics") is bool h && !h),
                    quotes = quotes,
                    calm = G("calm") is bool ca && ca,
                    tutorialDone = G("tutorialDone") is bool td && td,
                    seals = strs(G("seals")),
                    hills = strs(G("hills")),
                    daily = G("daily") is string dl ? dl : "",
                    ownText = G("ownText") is string ot ? slice(ot, 160) : "",
                    ownAuthor = G("ownAuthor") is string oa ? slice(oa, 40) : "",
                    bigText = G("bigText") is bool bt && bt,
                    lightBg = G("lightBg") is bool lb && lb,
                    boldRing = G("boldRing") is bool br && br,
                    leftHanded = G("leftHanded") is bool lh && lh,
                    bestStreak = finite(G("bestStreak"), out f) ? Math.Max(0, Math.Floor(f)) : 0,
                    weekKey = G("weekKey") is string wk ? wk : "",
                    weekBest = finite(G("weekBest"), out f) ? Math.Max(0, f) : 0,
                    saga = Saga.readSaga(G("saga")),
                    run = readRun(G("run")),
                    savedAt = num(G("savedAt")),
                };
            }
            catch (Exception)
            {
                return EMPTY;
            }
        }

        /// <summary>
        /// <c>writeSave({ ... })</c>: <paramref name="patch"/> sets the fields the web build's
        /// partial object had on a copy of the loaded save, e.g.
        /// <c>Save.writeSave(d => { d.cycles = c; d.bestClimb = b; })</c>.
        /// </summary>
        public static void writeSave(Action<SaveData> patch)
        {
            try
            {
                var next = loadSave().Clone();
                patch?.Invoke(next);
                next.savedAt = Math.Floor(Js.dateNow());
                var json = Json.Stringify(next.toJson());
                Store.Set(KEY, json);
                mirrorNative(json);
            }
            catch (Exception)
            {
                /* storage unavailable: the session still plays, it just won't be remembered */
            }
        }

        /*
         * In the store apps the save was also kept in the app's own preferences, because a
         * phone may clear a web view's storage. PORT: in Unity the Store already is the app's
         * own storage; a host that wants a second copy sets nativeStore (used only when
         * Native.isNative()). The web wrote it 400 ms after the last change; here it is
         * written at once (there is no web view to outlive).
         */
        public static IStore nativeStore = null;
        static string mirrorJson = "";

        static void mirrorNative(string json)
        {
            if (!Native.isNative()) return;
            mirrorJson = json;
            try { nativeStore?.Set(KEY, mirrorJson); } catch (Exception) { }
        }

        /// <summary>write any pending copy at once (the app is going to the background)</summary>
        public static void flushNativeSave()
        {
            if (!Native.isNative() || mirrorJson == "") return;
            try { nativeStore?.Set(KEY, mirrorJson); } catch (Exception) { }
        }

        /// <summary>
        /// At start, in the store apps: if the app's own copy is newer than what the
        /// Store kept (or the Store kept nothing), put it back. True when it did — the
        /// game should then load again from it. PORT: synchronous (was a Promise).
        /// </summary>
        public static bool restoreNativeSave()
        {
            if (!Native.isNative() || nativeStore == null) return false;
            try
            {
                var value = nativeStore.Get(KEY);
                if (string.IsNullOrEmpty(value))
                {
                    // first run of this version: keep a copy of what there is
                    var cur0 = Store.Get(KEY);
                    if (!string.IsNullOrEmpty(cur0)) nativeStore.Set(KEY, cur0);
                    return false;
                }
                var kept = parseSave(value);
                var cur = Store.Get(KEY);
                var here = !string.IsNullOrEmpty(cur) ? parseSave(cur) : null;
                if (here != null && here.savedAt >= kept.savedAt) return false;
                Store.Set(KEY, value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// the player's preferred languages, most preferred first (navigator.languages). PORT:
        /// the host sets this (e.g. from Application.systemLanguage); by default the UI culture.
        /// </summary>
        public static Func<string[]> preferredLanguages = () => new[] { CultureInfo.CurrentUICulture.Name };

        /// <summary>the first of the player's preferred languages the game speaks, else English</summary>
        public static string browserLang()
        {
            try
            {
                var prefs = preferredLanguages?.Invoke();
                if (prefs != null)
                    foreach (var p in prefs)
                    {
                        var @base = p?.ToLowerInvariant().Split('-')[0];
                        if (isLang(@base)) return @base;
                    }
            }
            catch (Exception)
            {
                /* nothing to ask */
            }
            return "en";
        }

        /// <summary>83.4 -> "1:23"</summary>
        public static string formatTime(double sec)
        {
            double s = Math.Max(0, Js.round(sec));
            return Js.str(Math.Floor(s / 60)) + ":" + Js.str(s % 60).PadLeft(2, '0');
        }

        /*
         * A backup of everything above as one line of text, to carry the book and the
         * seals to a new phone: copied, shared, pasted back in. Nothing leaves the
         * device unless the player sends it somewhere.
         */
        const string CODE_PREFIX = "SISYPHUS1:";

        public static string exportSave()
        {
            var json = Json.Stringify(loadSave().toJson());
            var bytes = Encoding.UTF8.GetBytes(json);
            return CODE_PREFIX + Convert.ToBase64String(bytes);
        }

        /// <summary>atob's forgiving base64: padding optional, whitespace already gone</summary>
        static byte[] atob(string s)
        {
            if (s.Length % 4 == 0)
            {
                if (s.EndsWith("==")) s = s.Substring(0, s.Length - 2);
                else if (s.EndsWith("=")) s = s.Substring(0, s.Length - 1);
            }
            if (s.Length % 4 == 1) throw new FormatException("atob");
            foreach (char c in s)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/'))
                    throw new FormatException("atob");
            while (s.Length % 4 != 0) s += "=";
            return Convert.FromBase64String(s);
        }

        /// <summary>restore from a backup code; false if it is not one</summary>
        public static bool importSave(string code)
        {
            try
            {
                var raw = Regex.Replace(code.Trim(), "\\s+", "");
                if (!raw.StartsWith(CODE_PREFIX, StringComparison.Ordinal)) return false;
                var bytes = atob(raw.Substring(CODE_PREFIX.Length));
                // TextDecoder drops a leading byte-order mark
                int off = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                var text = new UTF8Encoding(false, false).GetString(bytes, off, bytes.Length - off);
                var data = Json.Parse(text) as Dictionary<string, object>;
                if (data == null || !(Json.Get(data, "quotes") is List<object>)) return false;
                Store.Set(KEY, Json.Stringify(data));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>the ISO week a date falls in, as "2026-W43": the weekly record resets on Mondays</summary>
        public static string weekKey(DateTime? date = null)
        {
            var d = date ?? DateTime.Now;
            var t = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
            int day = (int)t.DayOfWeek;
            if (day == 0) day = 7;
            t = t.AddDays(4 - day);
            int y = t.Year;
            double wk = Math.Ceiling(((t - new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds / 86400000 + 1) / 7);
            return y.ToString(CultureInfo.InvariantCulture) + "-W" + Js.str(wk).PadLeft(2, '0');
        }
    }
}
