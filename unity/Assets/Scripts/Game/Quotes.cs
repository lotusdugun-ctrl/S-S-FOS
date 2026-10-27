// Ported from src/game/quotes.ts.
/*
 * A thousand aphorisms, in nine languages.
 *
 * The texts are about a megabyte across all the languages, so each language
 * lives in its own JSON file (Resources/Data/quotes-data/<lang>.json) and is read
 * only when it is chosen. The author list is shared and small, and is all the
 * game needs to know how many pages the Book of Wisdom has.
 *
 * Index order is permanent: saved progress refers to pages by index, so new
 * aphorisms are only ever appended.
 */
using System;
using System.Collections.Generic;

namespace Sisifos.Game
{
    // LangCode is a plain string in C#: "tr" | "en" | "zh" | "hi" | "es" | "fr" | "ar" | "bn" | "pt".

    public sealed class Quote
    {
        public string text;
        public string author;
        /// <summary>one line on who the author was: dates and what they are known for (or null)</summary>
        public string note;
    }

    public static class Quotes
    {
        static string[] _authors;
        static int[] _chapterOf;

        static string[] AUTHORS
        {
            get
            {
                if (_authors == null)
                {
                    var a = Json.Arr(Json.Parse(Data.Text("quotes-data/authors")));
                    var r = new string[a.Count];
                    for (int i = 0; i < r.Length; i++) r[i] = (string)a[i];
                    _authors = r;
                }
                return _authors;
            }
        }

        public static int QUOTE_COUNT { get { return AUTHORS.Length; } }

        /// <summary>which chapter of the Book of Wisdom each aphorism belongs to (0-based)</summary>
        public static int[] CHAPTER_OF
        {
            get
            {
                if (_chapterOf == null)
                {
                    var a = Json.Arr(Json.Parse(Data.Text("quotes-data/chapters")));
                    var r = new int[a.Count];
                    for (int i = 0; i < r.Length; i++) r[i] = (int)(double)a[i];
                    _chapterOf = r;
                }
                return _chapterOf;
            }
        }

        public const int CHAPTER_COUNT = 12;

        static readonly string[] FILES = { "tr", "en", "zh", "hi", "es", "fr", "ar", "bn", "pt" };
        static readonly Dictionary<string, List<Quote>> cache = new Dictionary<string, List<Quote>>();

        /// <summary>
        /// read one language's aphorisms (once); returns the list.
        /// PORT: the web fetched them asynchronously (a Promise); here they are read at once.
        /// </summary>
        public static List<Quote> loadQuotes(string lang)
        {
            lock (cache)
            {
                if (cache.TryGetValue(lang, out var have)) return have;
            }
            if (Array.IndexOf(FILES, lang) < 0) return new List<Quote>();
            var d = Json.Obj(Json.Parse(Data.Text("quotes-data/" + lang)));
            var texts = Json.Arr(d["texts"]);
            var names = Json.Obj(d["names"]);
            var notes = d.TryGetValue("notes", out var nv) ? Json.Obj(nv) : null;
            var authors = AUTHORS;
            var list = new List<Quote>(texts.Count);
            for (int i = 0; i < texts.Count; i++)
            {
                string key = i < authors.Length ? authors[i] : "";
                list.Add(new Quote
                {
                    text = (string)texts[i],
                    author = names.TryGetValue(key, out var n) && n is string ns ? ns : key,
                    note = notes != null && notes.TryGetValue(key, out var no) ? no as string : null,
                });
            }
            lock (cache) cache[lang] = list;
            return list;
        }

        /// <summary>the aphorisms for a language if they are loaded, else an empty list</summary>
        public static List<Quote> quotesFor(string lang)
        {
            lock (cache)
            {
                return cache.TryGetValue(lang, out var l) ? l : new List<Quote>();
            }
        }
    }
}
