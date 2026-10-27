// Ported from src/game/dialogue.ts.
using System;
using System.Collections.Generic;
using Sisifos.Paint;

namespace Sisifos.Game
{
    /// <summary>
    /// kind: "took" (item) | "stage" (stage) | "passive" (id) | "burden" (id) | "wedge" | "mire"
    /// </summary>
    public sealed class StoryNote
    {
        public string kind;
        public string item;
        public int stage;
        public string id;
    }

    public sealed class Line
    {
        public string who;
        public string text;
    }

    /// <summary>one exchange: a scene that stops the climb, or words in passing</summary>
    public sealed class Block
    {
        public List<Line> lines;
        public bool cine;
        /// <summary>BurdenId or null</summary>
        public string offer;
        public bool choice;
        /// <summary>where this is in the story, e.g. "37 / 150" (or null)</summary>
        public string label;
    }

    public static class Dialogue
    {
        /// <summary>the n-th of a pool, going round once all have been said</summary>
        static string nth(string[] pool, double n)
        {
            if (pool == null || pool.Length == 0) return "";
            // n % pool.length, then pool[..] ?? "" (n is a whole count; anything else reads undefined)
            double i = n % pool.Length;
            return Saga.At(pool, i) ?? "";
        }

        /// <summary>where a scene stands in its story (null: undefined)</summary>
        public static string labelFor(TalkRef @ref)
        {
            if (@ref.kind == "ep") return Js.str(@ref.ep + 1) + " / " + Story.EPISODES.Length;
            if (@ref.kind == "post") return Js.str(@ref.m + 1) + " / " + Story.COMPANIONS[@ref.who].post.Length;
            return null;
        }

        static List<Line> zip(string[] who, string[] texts)
        {
            var r = new List<Line>(texts.Length);
            for (int i = 0; i < texts.Length; i++)
                r.Add(new Line { who = (who != null && i < who.Length ? who[i] : null) ?? "sis", text = texts[i] });
            return r;
        }

        /// <summary>the words for a reference, in a language</summary>
        public static List<Line> linesFor(TalkRef @ref, string lang)
        {
            var st = Story.storyFor(lang);
            switch (@ref.kind)
            {
                case "ep":
                    return zip(Saga.At(Story.EPISODES, @ref.ep)?.who ?? new string[0], Saga.At(st.episodes, @ref.ep) ?? new string[0]);
                case "again":
                {
                    // she asks again, with the request scene's last line
                    int ask = Array.FindIndex(Story.EPISODES, e => e.choice);
                    var lines = Saga.At(st.episodes, ask) ?? new string[0];
                    return new List<Line> { new Line { who = "merope", text = (lines.Length > 0 ? lines[lines.Length - 1] : null) ?? "" } };
                }
                case "notyet":
                    return new List<Line>
                    {
                        new Line
                        {
                            who = "merope",
                            text = Saga.At(st.notYetReplies, Math.Min(@ref.n, st.notYetReplies.Length - 1)) ?? "",
                        },
                    };
                case "filler":
                    return new List<Line> { new Line { who = "merope", text = nth(Saga.At(st.fillers, @ref.act - 1), @ref.n) } };
                case "turned":
                    return new List<Line> { new Line { who = "merope", text = nth(st.turned, @ref.n) } };
                case "meet":
                    return zip(
                        Saga.At(Story.COMPANIONS[@ref.who].meetings, @ref.m)?.who ?? new string[0],
                        Saga.At(st.companions[@ref.who].meetings, @ref.m) ?? new string[0]);
                case "post":
                    return zip(
                        Saga.At(Story.COMPANIONS[@ref.who].post, @ref.m)?.who ?? new string[0],
                        Saga.At(st.companions[@ref.who].post, @ref.m) ?? new string[0]);
                case "idle":
                    return new List<Line> { new Line { who = @ref.who, text = nth(Saga.At(st.companions[@ref.who].idle, @ref.g), @ref.n) } };
                case "reply":
                    return new List<Line>
                    {
                        new Line
                        {
                            who = @ref.who,
                            text = @ref.accepted ? st.companions[@ref.who].accepted : st.companions[@ref.who].declined,
                        },
                    };
            }
            return null;
        }

        /// <summary>
        /// PORT: useNotes() was a React hook (state + a 4600 ms timeout). This is the same queue
        /// without React: <c>note</c> is the one on screen, <c>push</c> adds one, and the host calls
        /// <c>tick(ms)</c> every frame so the first note leaves 4600 ms after it was shown.
        /// </summary>
        public sealed class Notes
        {
            const double SHOW_MS = 4600;
            readonly List<StoryNote> notes = new List<StoryNote>();
            double shown;

            public StoryNote note { get { return notes.Count > 0 ? notes[0] : null; } }

            public void push(StoryNote n)
            {
                // the hook's effect restarted its timeout whenever the queue changed
                shown = 0;
                notes.Add(n);
            }

            public void tick(double ms)
            {
                if (notes.Count == 0) return;
                shown += ms;
                if (shown >= SHOW_MS)
                {
                    notes.RemoveAt(0);
                    shown = 0;
                }
            }
        }

        public static Notes useNotes() { return new Notes(); }
    }
}
