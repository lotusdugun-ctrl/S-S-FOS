using System;
using System.Collections.Generic;
using Sisifos.Game;
using Sisifos.Paint;
using UnityEngine;
using UnityEngine.UIElements;
using LineCap = Sisifos.Paint.LineCap;
using LineJoin = Sisifos.Paint.LineJoin;

namespace Sisifos.App.UI
{
    /*
     * Story.tsx: the story on screen. The subtitles of whoever is speaking on the hill
     * (with the choice when someone offers a burden), the small notes that mark what
     * happened — an item taken, a stage reached, a gift earned — the words that come at the
     * ending, and the end card. (The prologue is PrologueView.)
     *
     * Each React component is a VisualElement here. The parent adds it to its root (it
     * lays itself out over the whole frame) and removes it when React would unmount it; a
     * React `key` change is a new instance; a re-render with new props is Update(props).
     * Stacking (z-30, z-35, z-45) is the parent's insertion order.
     */

    internal static class StoryStyle
    {
        public const string SERIF = UiKit.MARCELLUS; // 'Marcellus', 'Cormorant Garamond', Georgia, serif
        public const string TITLE = UiKit.CINZEL;
        public const string CREAM = "#f1e4c4";
        public const string GOLD = "#c9a15a";

        public static string ColorOf(string who)
        {
            switch (who)
            {
                case "merope": return "#b9c8ea";
                case "sis": return GOLD;
                case "pro": return "#ef8a5a";
                case "tan": return "#8fb7e0";
                default: return "#bbb";
            }
        }

        public static string NameOf(LangStory st, string who)
        {
            string n;
            return st.names != null && st.names.TryGetValue(who, out n) ? n : who;
        }
    }

    // ===================================================================== Scene

    public sealed class SceneProps
    {
        public Block block;
        public string lang;
        public Action onDone;
        public Action<bool> onAnswer;
        /// <summary>who is speaking now (null: nobody)</summary>
        public Action<string> onSpeaker;
    }

    /// <summary>
    /// A scene. The climb has stopped; black bars close in over the top and bottom of the
    /// frame, and the lines come one at a time, at the player's own pace — a tap, a click,
    /// or the space bar. Nothing is missed. When a burden is offered, or she asks for the
    /// water, the answers wait under the last line.
    /// </summary>
    public sealed class SceneView : VisualElement
    {
        SceneProps p;
        int i;
        // whether the line has finished coming in; a tap before that shows it whole
        bool shown;
        Timer showTimer;
        readonly VisualElement top, bottom;
        VisualElement content, edge;
        string edgeWho;
        Flow reveal;
        Pill firstAnswer;

        public SceneView(SceneProps props)
        {
            p = props;
            pickingMode = PickingMode.Position;
            SU.Fill(this);
            // role="dialog": a tap anywhere moves on
            RegisterCallback<ClickEvent>(e => Next());

            // the frame narrows
            top = SU.Box(this);
            SU.Abs(top, 0, 0, 0, null);
            top.style.height = Length.Percent(12);
            top.style.backgroundColor = Color.black;
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.FlexEnd;
            top.style.justifyContent = Justify.Center;
            top.style.paddingBottom = SU.Sp(1);
            bottom = SU.Box(this);
            SU.Abs(bottom, 0, null, 0, 0);
            bottom.style.height = Length.Percent(30);
            bottom.style.minHeight = 150;
            bottom.style.backgroundColor = Color.black;
            bottom.style.flexDirection = FlexDirection.Column;
            bottom.style.alignItems = Align.Center;
            bottom.style.justifyContent = Justify.Center;
            bottom.style.paddingLeft = SU.Sp(6);
            bottom.style.paddingRight = SU.Sp(6);
            // .cine-bar-top / .cine-bar-bottom: they slide in over the frame
            UiKit.Tween(top, 700, 0, SU.Out, e => top.style.translate = new Translate(0, Length.Percent((float)(-100 * (1 - e)))));
            UiKit.Tween(bottom, 700, 0, SU.Out, e => bottom.style.translate = new Translate(0, Length.Percent((float)(100 * (1 - e)))));

            if (p.block.label != null && p.block.label != "")
            {
                var name = SU.Text(p.block.label, top);
                SU.Font(name, StoryStyle.TITLE);
                float fs = SU.Rem(0.62);
                name.style.fontSize = fs;
                name.style.color = SU.C(StoryStyle.GOLD);
                name.style.height = fs * 1.5f;
                SceneName(name, fs, p.lang);
            }

            new KeyHook(this, OnKey);
            UiKit.changed += OnResize;
            RegisterCallback<DetachFromPanelEvent>(e => UiKit.changed -= OnResize);
            RegisterCallback<AttachToPanelEvent>(e => { UiKit.changed -= OnResize; UiKit.changed += OnResize; });
            ShowLine(0);
        }

        public void Update(SceneProps props)
        {
            bool relang = props.lang != p.lang;
            p = props;
            if (relang) Render(false);
        }

        Line line { get { return p.block.lines != null && i < p.block.lines.Count ? p.block.lines[i] : null; } }
        bool last { get { return i >= p.block.lines.Count - 1; } }
        bool asking { get { return last && (!string.IsNullOrEmpty(p.block.offer) || p.block.choice); } }

        /// <summary>.scene-name: the letters draw together as it fades in</summary>
        static void SceneName(VisualElement e, float fs, string lang)
        {
            bool joined = SU.Joined(lang);
            UiKit.Tween(e, 900, 0, SU.Out, k =>
            {
                e.style.opacity = (float)k;
                e.style.letterSpacing = joined ? 0 : (float)((0.9 + (0.36 - 0.9) * k) * fs);
            });
        }

        void ShowLine(int n)
        {
            i = n;
            var ln = line;
            if (p.onSpeaker != null) p.onSpeaker(ln != null ? ln.who : null);
            shown = false;
            if (showTimer != null) showTimer.Cancel();
            showTimer = null;
            if (ln == null) { style.display = DisplayStyle.None; return; }
            showTimer = Timer.After(this, SU.RevealTime(ln.text, 42) + 250, () => SetShown());
            Render(true);
        }

        void SetShown()
        {
            if (shown) return;
            shown = true;
            if (showTimer != null) showTimer.Cancel();
            Render(false);
        }

        void Next()
        {
            if (!shown)
            {
                SetShown();
                return;
            }
            if (asking) return;
            if (last) { if (p.onDone != null) p.onDone(); }
            else ShowLine(i + 1);
        }

        void OnKey(KeyDownEvent e)
        {
            // the answers are on screen: the keys belong to the buttons
            if (asking && shown) return;
            if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.RightArrow)
            {
                e.StopImmediatePropagation();
                e.PreventDefault();
                Next();
            }
        }

        void OnResize() { if (line != null) Render(false, true); }

        int renderedLine = -1;

        /// <summary>
        /// The line on screen. `animate`: a new line (key={i} remounts it); otherwise the same
        /// line re-rendered (shown, a resize, a language) without playing its entrance again.
        /// </summary>
        void Render(bool animate, bool resize = false)
        {
            var ln = line;
            if (ln == null) return;
            var st = Story.storyFor(p.lang);
            bool narr = ln.who == "narr";
            string tint = StoryStyle.ColorOf(ln.who);

            // a line of the speaker's colour along the edge of the frame (key edge-who)
            if (edge == null || edgeWho != ln.who)
            {
                if (edge != null) edge.RemoveFromHierarchy();
                edgeWho = ln.who;
                var edgeCol = (Col)(narr ? "#777" : tint);
                edge = new PaintBox((c, w, h) =>
                {
                    var g = c.CreateLinearGradient(0, 0, w, 0);
                    g.AddColorStop(0, edgeCol.WithAlpha(0));
                    g.AddColorStop(0.5, edgeCol);
                    g.AddColorStop(1, edgeCol.WithAlpha(0));
                    c.FillStyle = g;
                    c.FillRect(0, 0, w, h);
                });
                SU.Abs(edge, 0, 0, 0, null);
                edge.style.height = 1;
                bottom.Insert(0, edge);
                var ed = edge;
                UiKit.Tween(ed, 1200, 0, SU.Out, k =>
                {
                    ed.style.scale = new Scale(new Vector2((float)k, 1));
                    ed.style.opacity = (float)(0.9 * k);
                });
            }

            bool fresh = animate || renderedLine != i;
            renderedLine = i;
            if (fresh || resize || content == null)
            {
                if (content != null) content.RemoveFromHierarchy();
                content = SU.Box(bottom);
                content.style.maxWidth = SU.Rem(48);
                content.style.flexDirection = FlexDirection.Column;
                content.style.alignItems = Align.Stretch;
                content.style.flexShrink = 1;

                if (!narr)
                {
                    var row = SU.Box(content);
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.justifyContent = Justify.Center;
                    float fs = SU.Rem(0.7);
                    var c = SU.C(tint);
                    var l1 = SU.Box(row);
                    l1.style.height = 1; l1.style.width = SU.Sp(6);
                    l1.style.backgroundColor = SU.Alpha(c, 0.5);
                    var name = SU.Text(SU.Upper(StoryStyle.NameOf(st, ln.who), p.lang), row);
                    SU.Font(name, StoryStyle.TITLE, 700);
                    name.style.fontSize = fs;
                    name.style.color = c;
                    name.style.height = fs * 1.5f;
                    name.style.marginLeft = SU.Sp(3);
                    name.style.marginRight = SU.Sp(3);
                    var l2 = SU.Box(row);
                    l2.style.height = 1; l2.style.width = SU.Sp(6);
                    l2.style.backgroundColor = SU.Alpha(c, 0.5);
                    if (fresh) SceneName(name, fs, p.lang);
                    else name.style.letterSpacing = SU.Joined(p.lang) ? 0 : 0.36f * fs;
                }

                reveal = new Flow();
                reveal.style.marginTop = SU.Sp(2);
                var o = new Flow.Opts
                {
                    family = StoryStyle.SERIF,
                    italic = narr,
                    size = narr ? SU.Rem(1.02) : SU.Rem(UiKit.Sm ? 1.45 : 1.22),
                    lineHeight = 1.375,
                    color = SU.C(narr ? "#cfc6b4" : StoryStyle.CREAM),
                    gold = SU.C("#d9b46a"),
                };
                reveal.Set(ln.text, o);
                if (narr) reveal.style.opacity = 0.8f;
                // the margins of the flow stretch it over the line, centred by its words
                var wrap = SU.Box(content);
                wrap.Add(reveal);
                double delay = narr ? 100 : 260;
                if (shown || !fresh) reveal.Finish();
                else reveal.Reveal((n, k) => delay + n * 42);
            }
            else if (shown) reveal.Finish();

            // the answers, or the hint to go on
            var oldTail = content.Q<VisualElement>("tail");
            if (oldTail != null) oldTail.RemoveFromHierarchy();
            firstAnswer = null;
            if (asking && shown)
            {
                var ans = SU.Box(content);
                ans.name = "tail";
                ans.pickingMode = PickingMode.Position;
                ans.style.marginTop = SU.Sp(4);
                ans.style.flexDirection = FlexDirection.Column;
                ans.style.alignItems = Align.Center;
                ans.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                bool offer = !string.IsNullOrEmpty(p.block.offer);
                if (offer)
                {
                    NameDesc b;
                    st.burdens.TryGetValue(p.block.offer, out b);
                    var f = new Flow();
                    f.Set((b != null ? b.name : "") + " — " + (b != null ? b.desc : ""), new Flow.Opts
                    {
                        family = StoryStyle.SERIF,
                        size = SU.Rem(0.8),
                        color = SU.C(StoryStyle.GOLD),
                    });
                    var fw = SU.Box(ans);
                    fw.style.alignSelf = Align.Stretch;
                    fw.Add(f);
                    fw.style.marginBottom = SU.Sp(2);
                    Choice(fw, 0);
                }
                var btns = SU.Box(ans);
                btns.style.flexDirection = FlexDirection.Row;
                firstAnswer = Answer(btns, offer ? st.accept : st.bring, StoryStyle.GOLD, 120, true);
                var no = Answer(btns, offer ? st.decline : st.notYet, "rgba(241,228,196,0.35)", 240, false);
                no.style.marginLeft = SU.Sp(3);
                // when the answers appear, the keyboard goes to the first of them
                var fa = firstAnswer;
                schedule.Execute(() => fa.Focus());
            }
            else
            {
                float fs = SU.Rem(0.6);
                var hint = SU.Text(SU.Upper(st.next, p.lang) + " ›", content);
                hint.name = "tail";
                SU.Font(hint, StoryStyle.TITLE);
                hint.style.fontSize = fs;
                hint.style.height = fs * 1.5f;
                hint.style.color = SU.C(StoryStyle.CREAM);
                hint.style.letterSpacing = SU.Track(0.3, fs, p.lang);
                hint.style.marginTop = SU.Sp(3);
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                if (shown)
                {
                    // .scene-next: breathing, and leaning on toward the next line
                    UiKit.Loop(hint, 1800, 0, ph =>
                    {
                        double k = SU.PingPong(ph);
                        hint.style.opacity = (float)(0.25 + 0.35 * k);
                        hint.style.translate = new Translate((float)(4 * k), 0);
                    });
                }
                else hint.style.opacity = 0;
            }
        }

        /// <summary>.scene-choice: menu-rise 0.8s</summary>
        static void Choice(VisualElement e, double delay)
        {
            // PORT: filter: blur(8px) of menu-rise is dropped
            UiKit.Tween(e, 800, delay, SU.Out, k =>
            {
                e.style.opacity = (float)k;
                e.style.translate = new Translate(0, (float)(14 * (1 - k)));
            });
        }

        Pill Answer(VisualElement parent, string text, string border, double delay, bool yes)
        {
            var b = new Pill(SU.Upper(text, p.lang), () => { if (p.onAnswer != null) p.onAnswer(yes); });
            parent.Add(b);
            float fs = SU.Rem(0.72);
            SU.Font(b.label, StoryStyle.TITLE);
            b.label.style.fontSize = fs;
            b.label.style.height = fs * 1.5f;
            b.label.style.color = SU.C(StoryStyle.CREAM);
            b.label.style.letterSpacing = SU.Track(0.25, fs, p.lang);
            b.SetBorder(1, SU.C(border));
            SU.Pad(b, SU.Sp(6), SU.Sp(2));
            // .scene-btn:hover, :focus-visible
            b.hoverBg = SU.C("rgba(217,180,106,0.14)");
            b.hoverGlow = new Glow(b, 22, SU.C("rgba(217,180,106,0.35)"));
            // opacity-80 on "no" is overridden by the scene-choice animation's fill (CSS)
            Choice(b, delay);
            return b;
        }
    }

    // ===================================================================== Subtitle

    public sealed class SubtitleProps
    {
        public Block block;
        public string lang;
        public Action onDone;
        public Action<string> onSpeaker;
        /// <summary>on a touch screen: keep clear of the joystick and the throw button</summary>
        public bool touch;
    }

    /// <summary>
    /// A word in passing, while he walks or climbs: one line at a time, slow enough to read
    /// with the hands busy, a tap to move on.
    /// </summary>
    public sealed class SubtitleView : VisualElement
    {
        SubtitleProps p;
        int i;
        Timer timer;
        VisualElement box;

        public SubtitleView(SubtitleProps props)
        {
            p = props;
            pickingMode = PickingMode.Ignore;
            style.flexDirection = FlexDirection.Row;
            style.justifyContent = Justify.Center;
            Place();
            UiKit.changed += OnResize;
            RegisterCallback<DetachFromPanelEvent>(e => UiKit.changed -= OnResize);
            RegisterCallback<AttachToPanelEvent>(e => { UiKit.changed -= OnResize; UiKit.changed += OnResize; });
            ShowLine(0, true);
        }

        public void Update(SubtitleProps props)
        {
            bool re = props.lang != p.lang || props.touch != p.touch;
            p = props;
            if (re) { Place(); ShowLine(i, false); }
        }

        void OnResize() { Place(); ShowLine(i, false); }

        void Place()
        {
            if (p.touch)
            {
                // between the two thumbs on a wide screen; above them on a narrow one
                bool wide = UiKit.Vw(100) >= 600;
                float bottomPx = wide ? Math.Max(UiKit.Vh(14), 24) : 136;
                SU.Abs(this, 0, null, 0, bottomPx);
                style.paddingLeft = wide ? 132 : SU.Sp(4);
                style.paddingRight = wide ? 132 : SU.Sp(4);
            }
            else
            {
                SU.Abs(this, 0, null, 0, null);
                style.bottom = Length.Percent(14);
                style.paddingLeft = SU.Sp(6);
                style.paddingRight = SU.Sp(6);
            }
        }

        Line line { get { return p.block.lines != null && i < p.block.lines.Count ? p.block.lines[i] : null; } }

        void Advance()
        {
            if (i >= p.block.lines.Count - 1) { if (p.onDone != null) p.onDone(); }
            else ShowLine(i + 1, true);
        }

        void ShowLine(int n, bool fresh)
        {
            i = n;
            var ln = line;
            if (fresh && p.onSpeaker != null) p.onSpeaker(ln != null ? ln.who : null);
            if (box != null) box.RemoveFromHierarchy();
            box = null;
            if (timer != null && fresh) timer.Cancel();
            if (ln == null) return;
            if (fresh) timer = Timer.After(this, 3200 + ln.text.Length * 60, Advance);

            var st = Story.storyFor(p.lang);
            bool narr = ln.who == "narr";
            string tint = StoryStyle.ColorOf(ln.who);
            box = SU.Box(this);
            box.pickingMode = PickingMode.Position;
            box.style.maxWidth = SU.Rem(42);
            box.style.flexShrink = 1;
            SU.Radius(box, 4);
            SU.Pad(box, SU.Sp(5), SU.Sp(3));
            box.style.overflow = Overflow.Hidden;
            // PORT: backdrop-filter: blur(6px) is not available; the gradient alone remains
            var bg = new PaintBox((c, w, h) =>
            {
                var g = c.CreateLinearGradient(0, 0, 0, h);
                g.AddColorStop(0, Col.Rgba(8, 7, 12, 0.55));
                g.AddColorStop(1, Col.Rgba(8, 7, 12, 0.78));
                c.FillStyle = g;
                c.FillRect(0, 0, w, h);
            });
            SU.Fill(bg);
            box.Add(bg);
            box.style.borderTopWidth = 1;
            // `${COLOR}55`: the speaker's colour at 0x55 alpha
            box.style.borderTopColor = narr ? SU.C("rgba(255,255,255,0.12)") : SU.Alpha(SU.C(tint), 0x55 / 255.0);
            box.RegisterCallback<ClickEvent>(e => Advance());

            if (!narr)
            {
                float fs = SU.Rem(0.6);
                var name = SU.Text(SU.Upper(StoryStyle.NameOf(st, ln.who), p.lang), box);
                SU.Font(name, StoryStyle.TITLE, 700);
                name.style.fontSize = fs;
                name.style.height = fs * 1.5f;
                name.style.color = SU.C(tint);
                name.style.letterSpacing = SU.Track(0.32, fs, p.lang);
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            var f = new Flow();
            f.Set(ln.text, new Flow.Opts
            {
                family = StoryStyle.SERIF,
                size = SU.Rem(UiKit.Sm ? 1.2 : 1.08),
                lineHeight = 1.375,
                color = SU.C(StoryStyle.CREAM),
                gold = SU.C("#d9b46a"),
            });
            var fw = SU.Box(box);
            fw.style.marginTop = SU.Sp(1);
            fw.Add(f);
            if (fresh)
            {
                f.Reveal((nn, k) => 150 + nn * 55);
                // .subtitle-in (PORT: its blur(4px) is dropped)
                var bx = box;
                UiKit.Tween(bx, 600, 0, SU.Out, k =>
                {
                    bx.style.opacity = (float)k;
                    bx.style.translate = new Translate(0, (float)(10 * (1 - k)));
                });
            }
            else f.Finish();
        }
    }

    // ===================================================================== StoryToast

    public sealed class StoryToastProps
    {
        public StoryNote note;
        public string lang;
    }

    /// <summary>a short note at the top of the screen</summary>
    public sealed class StoryToastView : VisualElement
    {
        StoryToastProps p;

        public StoryToastView(StoryToastProps props)
        {
            p = props;
            pickingMode = PickingMode.Ignore;
            Build(true);
        }

        public void Update(StoryToastProps props)
        {
            bool re = props.lang != p.lang;
            p = props;
            if (re) Build(false);
        }

        void Build(bool animate)
        {
            Clear();
            var st = Story.storyFor(p.lang);
            var S = Journey.STORY[p.lang];
            var note = p.note;
            string head = "", body = "", sub = "";
            NameDesc nd;
            switch (note.kind)
            {
                case "took":
                    head = st.took;
                    string item;
                    body = st.items != null && st.items.TryGetValue(note.item, out item) ? item : note.item;
                    break;
                case "stage":
                    head = S.newStage + " · " + S.act + " " + Journey.ROMAN[note.stage - 1];
                    body = Journey.STAGE_TEXT[p.lang][note.stage - 1].name;
                    sub = Journey.STAGE_TEXT[p.lang][note.stage - 1].desc;
                    break;
                case "passive":
                    head = "✦";
                    if (st.passives.TryGetValue(note.id, out nd)) { body = nd.name; sub = nd.desc; }
                    break;
                case "burden":
                    if (st.burdens.TryGetValue(note.id, out nd)) { head = nd.name; body = nd.desc; }
                    break;
                case "wedge":
                    body = st.wedgeHeld;
                    break;
                case "mire":
                    body = S.mire;
                    sub = S.mireDesc;
                    break;
            }

            // top-16, centred, w-max up to 85vw
            SU.Abs(this, 0, SU.Rem(4), 0, null);
            style.flexDirection = FlexDirection.Row;
            style.justifyContent = Justify.Center;
            var box = SU.Box(this);
            box.style.maxWidth = Length.Percent(85);
            box.style.flexShrink = 1;
            box.style.alignItems = Align.Center;
            SU.Radius(box, 3);
            SU.Pad(box, SU.Sp(5), SU.Sp(2.5));
            // PORT: backdrop-filter: blur(6px) is not available
            box.style.backgroundColor = SU.C("rgba(10, 8, 14, 0.62)");
            // box-shadow: 0 0 0 1px rgba(217,180,106,0.18), 0 8px 30px rgba(0,0,0,0.4)
            var sh = new Glow(box, 30, SU.C("rgba(0,0,0,0.4)"), 3, 8);
            sh.level = 1;
            var ring = new Glow(box, 0, SU.C("rgba(217,180,106,0.18)"), 3, 0, 1);
            ring.level = 1;

            if (head != "")
            {
                float fs = SU.Rem(0.58);
                var l = SU.Text(SU.Upper(head, p.lang), box);
                SU.Font(l, StoryStyle.TITLE, 700);
                l.style.fontSize = fs;
                l.style.color = SU.C(StoryStyle.GOLD);
                l.style.letterSpacing = SU.Track(0.3, fs, p.lang);
                Line1(l, fs);
            }
            var b = SU.Text(body, box);
            SU.Font(b, StoryStyle.SERIF, 600);
            float bs = SU.Rem(1.1);
            b.style.fontSize = bs;
            b.style.color = SU.C(StoryStyle.CREAM);
            Line1(b, bs);
            if (sub != "")
            {
                float fs = SU.Rem(0.78);
                var s = SU.Text(sub, box);
                SU.Font(s, StoryStyle.SERIF, 400, true);
                s.style.fontSize = fs;
                s.style.color = SU.C("#d8c8a4");
                Line1(s, fs);
            }
            if (animate)
            {
                // .toast-in (PORT: blur(6px) dropped)
                UiKit.Tween(box, 800, 0, SU.Out, k =>
                {
                    box.style.opacity = (float)k;
                    box.style.translate = new Translate(0, (float)(-14 * (1 - k)));
                });
            }
        }

        /// <summary>a centred block line that wraps only past the 85vw cap</summary>
        static void Line1(Label l, float fs)
        {
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.minHeight = fs * 1.5f;
            l.style.flexShrink = 1;
        }
    }

    // ===================================================================== Epiphany

    public sealed class EpiphanyProps
    {
        public string lang;
    }

    /// <summary>the words at the ending's summit, over the close-up of his face</summary>
    public sealed class EpiphanyView : VisualElement
    {
        EpiphanyProps p;

        public EpiphanyView(EpiphanyProps props)
        {
            p = props;
            pickingMode = PickingMode.Ignore;
            Build();
        }

        public void Update(EpiphanyProps props)
        {
            bool re = props.lang != p.lang;
            p = props;
            if (re) Build();
        }

        void Build()
        {
            Clear();
            var S = Journey.STORY[p.lang];
            SU.Abs(this, 0, null, 0, null);
            style.top = Length.Percent(12);
            style.flexDirection = FlexDirection.Row;
            style.justifyContent = Justify.Center;
            style.paddingLeft = SU.Sp(6);
            style.paddingRight = SU.Sp(6);

            // light breaking over him as the words come: 90vw × 90vh, centred, from 30% above
            var light = new PaintBox((c, w, h) =>
            {
                SU.CssRadialEllipse(c, w, h, 0.5, 0.3,
                    (0, Col.Rgba(255, 220, 160, 0.32)),
                    (0.35, Col.Rgba(255, 190, 120, 0.12)),
                    (0.65, Col.Rgba(255, 190, 120, 0)));
            });
            light.style.position = Position.Absolute;
            light.style.width = UiKit.Vw(90);
            light.style.height = UiKit.Vh(90);
            light.style.left = Length.Percent(50);
            light.style.marginLeft = -UiKit.Vw(90) / 2;
            Add(light);
            // 90vw × 90vh follow the screen
            Action fit = () =>
            {
                light.style.width = UiKit.Vw(90);
                light.style.height = UiKit.Vh(90);
                light.style.marginLeft = -UiKit.Vw(90) / 2;
            };
            UiKit.changed += fit;
            RegisterCallback<DetachFromPanelEvent>(e => UiKit.changed -= fit);
            RegisterCallback<GeometryChangedEvent>(e => light.style.top = -0.3f * layout.height);
            // epiphany-light 7s 2.4s ease-out: 0% {0, scale .6} 40% {opacity 1} 100% {.75, scale 1}
            var easeOut = UiKit.EaseOut;
            UiKit.Tween(light, 7000, 2400, UiKit.Linear, t =>
            {
                double op = t < 0.4 ? easeOut(t / 0.4) : 1 - 0.25 * easeOut((t - 0.4) / 0.6);
                double sc = 0.6 + 0.4 * easeOut(t);
                light.style.opacity = (float)op;
                light.style.scale = new Scale(new Vector2((float)sc, (float)sc));
            });

            var col = SU.Box(this);
            col.style.flexDirection = FlexDirection.Column;
            col.style.alignItems = Align.Center;
            col.style.flexShrink = 1;

            var words = new Flow();
            words.Set(S.camus, new Flow.Opts
            {
                family = StoryStyle.SERIF,
                size = SU.Rem(UiKit.Sm ? 2.3 : 1.6),
                lineHeight = 1.5,
                color = SU.C(StoryStyle.CREAM),
                gold = SU.C("#d9b46a"),
                shadow = new TextShadow { offset = new Vector2(0, 2), blurRadius = 18, color = SU.C("rgba(0,0,0,0.75)") },
            });
            var ww = SU.Box(col);
            ww.style.alignSelf = Align.Stretch;
            ww.Add(words);
            words.Reveal((n, k) => 3000 + n * 260);

            var rule = SU.Box(col);
            rule.style.height = 1;
            rule.style.marginTop = SU.Sp(3);
            rule.style.backgroundColor = SU.C("#d9b46a");
            float ruleW = SU.Rem(9);
            UiKit.Tween(rule, 1600, 6000, UiKit.EaseOut, k =>
            {
                rule.style.width = (float)(ruleW * k);
                rule.style.opacity = (float)(0.8 * k);
            });

            float fs = SU.Rem(0.72);
            var by = SU.Text(SU.Upper(S.camusBy, p.lang), col);
            by.style.marginTop = SU.Sp(3);
            SU.Font(by, StoryStyle.TITLE);
            by.style.fontSize = fs;
            by.style.height = fs * 1.5f;
            by.style.color = SU.C(StoryStyle.GOLD);
            by.style.letterSpacing = SU.Track(0.4, fs, p.lang);
            by.style.textShadow = new TextShadow { offset = new Vector2(0, 2), blurRadius = 12, color = SU.C("rgba(0,0,0,0.8)") };
            // .epiphany-by: epiphany-in 2s 6.6s ease (PORT: blur(3px) dropped)
            UiKit.Tween(by, 2000, 6600, UiKit.Ease, k =>
            {
                by.style.opacity = (float)k;
                by.style.translate = new Translate(0, (float)(6 * (1 - k)));
            });
        }
    }

    // ===================================================================== EndCard

    public sealed class EndCardProps
    {
        public string lang;
        public (string, string)[] stats;
        public Action onContinue;
    }

    /// <summary>the end card: the story is over, the hill is not</summary>
    public sealed class EndCardView : VisualElement
    {
        EndCardProps p;
        Pill button;

        public EndCardView(EndCardProps props)
        {
            p = props;
            pickingMode = PickingMode.Position;
            SU.Fill(this);
            style.overflow = Overflow.Hidden;
            Build();
            Timer.After(this, 3200, () => { if (button != null) button.Focus(); });
        }

        public void Update(EndCardProps props)
        {
            bool re = props.lang != p.lang || !SameStats(props.stats, p.stats);
            p = props;
            if (re) Build();
        }

        static bool SameStats((string, string)[] a, (string, string)[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i].Item1 != b[i].Item1 || a[i].Item2 != b[i].Item2) return false;
            return true;
        }

        void Build()
        {
            Clear();
            var S = Journey.STORY[p.lang];

            // .menu-veil
            var veil = new PaintBox((c, w, h) =>
            {
                SU.CssRadialEllipse(c, w, h, 0.5, 0.45, (0, Col.Rgba(8, 8, 14, 0.62)), (0.7, Col.Rgba(6, 6, 10, 0.88)));
                var g = c.CreateLinearGradient(0, 0, 0, h);
                g.AddColorStop(0, Col.Rgba(0, 0, 0, 0.3));
                g.AddColorStop(1, Col.Rgba(0, 0, 0, 0.55));
                c.FillStyle = g;
                c.FillRect(0, 0, w, h);
            });
            SU.Fill(veil);
            Add(veil);
            UiKit.Tween(this, 1200, 0, UiKit.EaseOut, k => style.opacity = (float)k);

            // the glow, breathing
            var glow = new PaintBox((c, w, h) =>
                SU.CssRadialEllipse(c, w, h, 0.5, 0.4, (0, Col.Rgba(217, 160, 90, 0.18)), (0.6, Col.Rgba(217, 160, 90, 0))));
            SU.Fill(glow);
            Add(glow);
            UiKit.Loop(glow, 6000, 0, ph => glow.style.opacity = (float)(0.6 + 0.4 * SU.PingPong(ph)));

            // embers rising
            double t0 = UiKit.Now;
            PaintBox embers = null;
            embers = new PaintBox((c, w, h) => DrawEmbers(c, w, h, (UiKit.Now - t0) / 1000));
            SU.Fill(embers);
            Add(embers);
            UiKit.Loop(embers, 1000, 0, ph => embers.Redraw());

            var col = SU.Box(this);
            SU.Fill(col);
            col.style.flexDirection = FlexDirection.Column;
            col.style.alignItems = Align.Center;
            col.style.justifyContent = Justify.Center;
            col.style.paddingLeft = SU.Sp(8);
            col.style.paddingRight = SU.Sp(8);
            float gap = SU.Sp(6);

            // the title, letter by letter (Latin) or word by word (joined scripts)
            // String.prototype.toUpperCase: not by the language's rules
            string title = S.end.ToUpperInvariant();
            bool latin = System.Text.RegularExpressions.Regex.IsMatch(title, "^[A-Za-zÀ-ɏ\\s]+$");
            float ts = SU.Rem(UiKit.Sm ? 3.4 : 2.4);
            float spacing = latin ? 0.4f * ts : 0.02f * ts;
            var parts = new List<KeyValuePair<Label, int>>();
            var tr = TitleRow(title, latin, StoryStyle.TITLE, 700, ts, spacing, SU.C(StoryStyle.CREAM), parts);
            // the spread letters give back their trailing spacing (marginRight: -0.4em)
            tr.style.marginRight = latin ? -spacing : 0;
            col.Add(tr);
            LetterIn(parts, ts, 120);

            var line = SU.Box(col);
            line.style.height = 1;
            line.style.backgroundColor = SU.C("#d9b46a");
            line.style.marginTop = gap - SU.Sp(3);
            PrologueLine(line);

            var q = new Flow();
            q.Set("“" + S.camus + "”", new Flow.Opts
            {
                family = StoryStyle.SERIF,
                size = SU.Rem(1.35),
                color = SU.C(StoryStyle.CREAM),
                gold = SU.C("#d9b46a"),
            });
            var qw = SU.Box(col);
            qw.style.maxWidth = SU.Rem(36);
            qw.style.marginTop = gap;
            qw.Add(q);
            q.Reveal((n, k) => 1500 + n * 140);

            var note = new Flow();
            note.Set(S.endNote, new Flow.Opts
            {
                family = StoryStyle.SERIF,
                size = SU.Rem(0.98),
                color = SU.C("#d8c8a4"),
            });
            var nw = SU.Box(col);
            nw.style.maxWidth = SU.Rem(36);
            nw.style.marginTop = gap;
            nw.Add(note);
            // opacity-80 is overridden by menu-rise's fill, as in the web build
            MenuRise(nw, 1100, 2600);

            var stats = SU.Box(col);
            stats.style.flexDirection = FlexDirection.Row;
            stats.style.flexWrap = Wrap.Wrap;
            stats.style.justifyContent = Justify.Center;
            stats.style.marginTop = gap + SU.Sp(2);
            // gap-x-8 gap-y-2, by margins around each and given back by the row
            float gx = SU.Sp(8), gy = SU.Sp(2);
            stats.style.marginLeft = -gx / 2;
            stats.style.marginRight = -gx / 2;
            if (p.stats != null)
                for (int i = 0; i < p.stats.Length; i++)
                {
                    var cell = SU.Box(stats);
                    cell.style.flexDirection = FlexDirection.Column;
                    cell.style.alignItems = Align.Center;
                    cell.style.marginLeft = gx / 2;
                    cell.style.marginRight = gx / 2;
                    if (i > 0) cell.style.marginTop = 0;
                    cell.style.marginBottom = gy;
                    float ks = SU.Rem(0.58);
                    var k = SU.Text(SU.Upper(p.stats[i].Item1, p.lang), cell);
                    SU.Font(k, StoryStyle.TITLE);
                    k.style.fontSize = ks;
                    k.style.height = ks * 1.5f;
                    k.style.color = SU.C(StoryStyle.GOLD);
                    k.style.letterSpacing = SU.Track(0.28, ks, p.lang);
                    float vs = SU.Rem(1.3);
                    var v = SU.Text(p.stats[i].Item2, cell);
                    SU.Font(v, StoryStyle.SERIF);
                    v.style.fontSize = vs;
                    v.style.height = vs * 1.5f;
                    v.style.color = SU.C(StoryStyle.CREAM);
                    MenuRise(cell, 1100, 3000 + i * 180);
                }
            stats.style.marginBottom = -gy;

            button = new Pill(SU.Upper(S.@continue, p.lang), () => { if (p.onContinue != null) p.onContinue(); });
            col.Add(button);
            button.style.marginTop = gap + SU.Sp(4);
            float bs = SU.Rem(0.75);
            SU.Font(button.label, StoryStyle.TITLE);
            button.label.style.fontSize = bs;
            button.label.style.height = bs * 1.5f;
            button.label.style.color = SU.C(StoryStyle.CREAM);
            button.label.style.letterSpacing = SU.Track(0.35, bs, p.lang);
            button.SetBorder(1, SU.C(StoryStyle.GOLD));
            SU.Pad(button, SU.Sp(8), SU.Sp(2.5));
            // .menu-start: dark glass, rising at 3.6s, glowing from 4.6s; lighter on hover
            button.baseBg = SU.C("rgba(0,0,0,0.3)");
            button.hoverBg = SU.C("rgba(217,180,106,0.16)");
            button.style.backgroundColor = button.baseBg;
            var pulse = new Glow(button, 26, SU.C("rgba(217,180,106,0.35)"));
            MenuRise(button, 1000, 3600);
            UiKit.Loop(pulse, 3000, 4600, ph => pulse.level = (float)SU.PingPong(ph));
        }

        /// <summary>18 embers (.menu-ember): a linear climb of 105vh with a drift of 30px</summary>
        static void DrawEmbers(Ctx c, double w, double h, double t)
        {
            for (int i = 0; i < 18; i++)
            {
                double delay = (i * 0.7) % 8;
                double dur = 9 + ((i * 5) % 6);
                double local = t - delay;
                if (local < 0) continue; // fill backwards: the 0% frame, invisible
                double ph = (local % dur) / dur;
                double op = ph < 0.15 ? 0.8 * ph / 0.15 : 0.8 * (1 - (ph - 0.15) / 0.85);
                double x = w * ((i * 53) % 100) / 100.0 + 30 * ph;
                double y = h + 10 - 3 - h * 1.05 * ph;
                c.Save();
                c.GlobalAlpha = Math.Max(0, Math.Min(1, op));
                c.ShadowColor = Col.Rgba(255, 140, 60, 0.8);
                c.ShadowBlur = 6 * SU.Scale(c);
                c.FillStyle = Col.Rgba(255, 170, 90, 0.8);
                c.BeginPath();
                c.Arc(x + 1.5, y + 1.5, 1.5, 0, Math.PI * 2);
                c.Fill();
                c.Restore();
            }
        }

        /// <summary>
        /// A title of inline-block parts (EndCard, the prologue's name): one per letter, or one
        /// per word for joined scripts; the spaces are parts too (they count in the delays).
        /// Each letter keeps the letter-spacing after it, as an inline-block does.
        /// </summary>
        internal static VisualElement TitleRow(string title, bool perLetter, string family, int weight, float size,
            float spacing, Color color, List<KeyValuePair<Label, int>> parts)
        {
            var row = SU.Box();
            row.style.flexDirection = SU.AutoRtl(title) ? FlexDirection.RowReverse : FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.Center;
            row.style.flexShrink = 1;
            var list = perLetter ? SU.CodePoints(title) : SU.SplitWords(title);
            float lh = size * 1.5f;
            for (int i = 0; i < list.Count; i++)
            {
                string ch = list[i];
                if (ch == "") continue;
                if (SU.IsWs(ch))
                {
                    var sp = SU.Box(row);
                    sp.style.width = (float)(SU.SpaceEm(family) * size + spacing) * ch.Length;
                    sp.style.height = lh;
                    continue;
                }
                var l = SU.Text(ch, row);
                SU.Font(l, family, weight);
                l.style.fontSize = size;
                l.style.height = lh;
                l.style.color = color;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                if (perLetter)
                {
                    l.style.letterSpacing = 0;
                    l.style.marginRight = spacing;
                }
                else l.style.letterSpacing = spacing;
                parts.Add(new KeyValuePair<Label, int>(l, i));
            }
            return row;
        }

        /// <summary>
        /// .prologue-letter, 1.6s cubic-bezier(.2,.7,.2,1), delay 200 + i·step: from above and
        /// larger, through a gold glow that settles.
        /// </summary>
        internal static void LetterIn(List<KeyValuePair<Label, int>> parts, float size, double step)
        {
            var glowA = SU.C("rgba(217,180,106,0.9)");
            var glowB = SU.C("rgba(217,180,106,0.35)");
            foreach (var kv in parts)
            {
                var l = kv.Key;
                UiKit.Tween(l, 1600, 200 + kv.Value * step, UiKit.Linear, t =>
                {
                    // transform: one segment 0→100%; opacity: 0→60%→100%; text-shadow 60%→100%
                    double tr = SU.Out(t);
                    double op = t < 0.6 ? SU.Out(t / 0.6) : 1;
                    l.style.opacity = (float)op;
                    float sc = (float)(1.25 + (1 - 1.25) * tr);
                    l.style.translate = new Translate(0, (float)(-0.2 * size * (1 - tr)));
                    l.style.scale = new Scale(new Vector2(sc, sc));
                    // PORT: blur(14px) dropped; the glow is a text shadow (UI Toolkit softens it less)
                    Color sh;
                    float blur;
                    if (t < 0.6)
                    {
                        double k = SU.Out(t / 0.6);
                        sh = SU.Alpha(glowA, k);
                        blur = (float)(32 * k);
                    }
                    else
                    {
                        double k = SU.Out((t - 0.6) / 0.4);
                        sh = Color.Lerp(glowA, glowB, (float)k);
                        blur = (float)(32 + (18 - 32) * k);
                    }
                    l.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = blur, color = sh };
                });
            }
        }

        /// <summary>.prologue-line: 1.8s 1.1s, width 0 → min(22rem, 60vw), opacity → 0.8</summary>
        internal static void PrologueLine(VisualElement line)
        {
            UiKit.Tween(line, 1800, 1100, SU.Out, k =>
            {
                float full = Math.Min(SU.Rem(22), UiKit.Vw(60));
                line.style.width = (float)(full * k);
                line.style.opacity = (float)(0.8 * k);
            });
        }

        /// <summary>menu-rise: up 14px into view (PORT: blur(8px) dropped)</summary>
        internal static void MenuRise(VisualElement e, double dur, double delay)
        {
            UiKit.Tween(e, dur, delay, SU.Out, k =>
            {
                e.style.opacity = (float)k;
                e.style.translate = new Translate(0, (float)(14 * (1 - k)));
            });
        }
    }
}
