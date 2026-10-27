// Port of src/components/Reveal.tsx and reveal-time.ts. The layout and the per-word animation live
// in StoryUiCommon's Flow (the web's inline-block word spans: one label per word, wrapping between
// words, quoted words in gold); this is the component with Reveal's own props on top of it.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sisifos.App.UI
{
    /// <summary>
    /// Words that come in one at a time out of a blur. Quoted words are set in gold. `instant`
    /// shows the whole line at once (a tap while it is still coming in). Scripts written without
    /// spaces come in a character at a time. (PORT: the blur is not available in UI Toolkit 6.0;
    /// opacity, rise and letter-spacing are.)
    /// </summary>
    public sealed class Reveal : VisualElement
    {
        public sealed class Props
        {
            public string text;
            public double step = 45;
            public double delay = 0;
            public bool instant = false;
            public string gold = "#d9b46a";
            /// <summary>the className/style the web passed: font, size, colour, line height, alignment</summary>
            public string family = UiKit.MARCELLUS;
            public int weight = 400;
            public bool italic;
            public float size = 16;
            public double lineHeight = 1.5;
            public Color color = Color.white;
            public string align = "center";
            public float letterSpacing;
        }

        readonly Flow flow = new Flow();
        Props p;

        public Reveal(Props props)
        {
            pickingMode = PickingMode.Ignore;
            Add(flow);
            Update(props);
        }

        public void Update(Props props)
        {
            bool restart = p == null || p.text != props.text;
            bool finish = props.instant && (p == null || !p.instant || restart);
            p = props;
            if (restart)
            {
                flow.Set(props.text, new Flow.Opts
                {
                    family = props.family,
                    weight = props.weight,
                    italic = props.italic,
                    size = props.size,
                    lineHeight = props.lineHeight,
                    color = props.color,
                    gold = UiKit.C(props.gold),
                    align = props.align,
                    rtl = null, // dir="auto"
                    letterSpacing = props.letterSpacing,
                });
                if (!props.instant) flow.Reveal((n, k) => props.delay + n * props.step);
            }
            if (finish) flow.Finish();
        }

        /// <summary>reveal-time.ts: how long a line of `text` takes to come in at `step` ms a word</summary>
        public static double revealTime(string text, double step = 45)
        {
            return SU.RevealTime(text, step);
        }
    }
}
