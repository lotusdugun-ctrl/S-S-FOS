using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Sisifos.Game;
using Sisifos.Paint;
using UnityEngine;
using UnityEngine.UIElements;
using LineCap = Sisifos.Paint.LineCap;
using LineJoin = Sisifos.Paint.LineJoin;

namespace Sisifos.App.UI
{
    public sealed class WisdomBookProps
    {
        public bool open;
        public Action onClose;
        public string lang;
        public UIStrings t;
        /// <summary>indices found, in the order found</summary>
        public List<int> unlocked;
        /// <summary>indices found since the book was last opened</summary>
        public HashSet<int> fresh;
        /// <summary>bumped when this language's file has finished loading</summary>
        public int version;
    }

    /// <summary>
    /// The Book of Wisdom (WisdomBook.tsx).
    ///
    /// A thousand pages do not fit on a wall of cards, and should not: this is laid out as a
    /// book. Twelve chapters, by age and school — the first sages, Athens, Rome, China,
    /// Anatolia, the moderns — each a run of numbered entries on parchment, two pages to a
    /// spread on a wide screen and one on a phone. Entries not found yet are left as blank
    /// ruled lines with their number, so every chapter shows how much of it is still out on
    /// the hill.
    ///
    /// Keep one instance in the tree (z-40, over the game) and call Update(props) whenever
    /// the parent's state changes; it hides itself while !open, as the React one renders null.
    /// </summary>
    public sealed class WisdomBookView : VisualElement
    {
        const string SERIF = UiKit.CORMORANT; // 'Cormorant Garamond', 'EB Garamond', Garamond, Georgia, serif
        const string INK = "#2a1c0f";
        const string SEPIA = "#6b4a22";
        const string GOLD = "#c9a15a";
        const string WAX = "#8e2b1e";

        WisdomBookProps p;
        bool wasOpen;
        int chapter;
        int spread;
        int dir = 1;
        bool onlyFound;
        /// <summary>whether the filter has been set for this game yet (first opening with finds: found only)</summary>
        bool filterSet;
        int? reading;
        float stageW = 900, stageH = 600;
        string lastTurnKey;
        List<Quote> quotes;
        string quotesKey;

        readonly VisualElement header, body, nav, bookCol, stage, leaves, footer;
        readonly ScrollView navScroll;
        readonly Pill prevBtn, nextBtn;
        VisualElement overlay;
        VisualElement closeBtn;
        Vector2? swipe;

        static int[][] byChapter;

        static string toRoman(int n)
        {
            var map = new (int v, string r)[]
            {
                (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
                (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
            };
            string out_ = "";
            foreach (var m in map)
                while (n >= m.v)
                {
                    out_ += m.r;
                    n -= m.v;
                }
            return out_;
        }

        static string fmt(string s, Dictionary<string, string> vars)
        {
            return Regex.Replace(s ?? "", "\\{(\\w+)\\}", m =>
            {
                string v;
                return vars.TryGetValue(m.Groups[1].Value, out v) ? v : "";
            });
        }

        public WisdomBookView(WisdomBookProps props)
        {
            p = props;
            pickingMode = PickingMode.Position;
            SU.Fill(this);
            style.flexDirection = FlexDirection.Column;
            style.display = DisplayStyle.None;
            // the game underneath never sees a touch on the book
            RegisterCallback<PointerDownEvent>(e => e.StopPropagation());

            var bg = new PaintBox((c, w, h) =>
                SU.CssRadialEllipse(c, w, h, 0.5, 0.4,
                    (0, (Col)"#221a12"), (0.55, (Col)"#120e0a"), (1, (Col)"#080605")));
            SU.Fill(bg);
            Add(bg);

            header = SU.Box(this);
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.flexShrink = 0;

            body = SU.Box(this);
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.minHeight = 0;

            // contents
            navScroll = new ScrollView(ScrollViewMode.Vertical);
            navScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            navScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            navScroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            navScroll.style.flexShrink = 0;
            SU.Zero(navScroll);
            body.Add(navScroll);
            nav = SU.Box(navScroll);
            nav.pickingMode = PickingMode.Position;

            // the open book
            bookCol = SU.Box(body);
            bookCol.style.flexGrow = 1;
            bookCol.style.flexShrink = 1;
            bookCol.style.minWidth = 0;
            bookCol.style.flexDirection = FlexDirection.Column;

            stage = SU.Box(bookCol);
            stage.pickingMode = PickingMode.Position;
            stage.style.flexGrow = 1;
            stage.style.flexShrink = 1;
            stage.style.minHeight = 0;
            // how many entries fit on a page, from the space the book actually has
            stage.RegisterCallback<GeometryChangedEvent>(e =>
            {
                float w = stage.layout.width, h = stage.layout.height;
                if (Math.Abs(w - stageW) < 0.5f && Math.Abs(h - stageH) < 0.5f) return;
                stageW = w;
                stageH = h;
                if (p.open) schedule.Execute(() => Render());
            });
            // swipe to turn
            stage.RegisterCallback<PointerDownEvent>(e => swipe = new Vector2(e.position.x, e.position.y));
            stage.RegisterCallback<PointerUpEvent>(e =>
            {
                var s = swipe;
                swipe = null;
                if (!s.HasValue) return;
                float dx = e.position.x - s.Value.x;
                if (Math.Abs(dx) > 50 && Math.Abs(dx) > Math.Abs(e.position.y - s.Value.y) * 1.5f)
                    go((dx < 0) != rtl ? 1 : -1);
            });

            // the board: leather, a stack of page edges, then the leaves
            var board = SU.Box(stage);
            SU.Fill(board);
            var boardShadow = new Glow(board, 60, SU.C("rgba(0,0,0,0.85)"), 10, 30, -20);
            boardShadow.level = 1;
            var leather = new PaintBox(PaintBoard);
            SU.Fill(leather);
            board.Add(leather);

            leaves = SU.Box(stage);
            leaves.style.position = Position.Absolute;
            leaves.style.flexDirection = FlexDirection.Row;

            prevBtn = Turn(-1);
            nextBtn = Turn(1);

            footer = SU.Box(bookCol);

            new KeyHook(this, OnKey);
            UiKit.changed += OnResize;
            RegisterCallback<DetachFromPanelEvent>(e => UiKit.changed -= OnResize);
            RegisterCallback<AttachToPanelEvent>(e => { UiKit.changed -= OnResize; UiKit.changed += OnResize; });

            Update(props);
        }

        bool rtl { get { return p.lang == "ar"; } }

        public void Update(WisdomBookProps props)
        {
            p = props;
            if (p.open && !wasOpen)
            {
                int last = p.unlocked != null && p.unlocked.Count > 0 ? p.unlocked[p.unlocked.Count - 1] : -1;
                // a book with pages in it opens on what has been found, not on rows of blank
                // lines: the page hunted for is among a few dozen, not a thousand
                if (!filterSet && last >= 0) { onlyFound = true; filterSet = true; }
                // open on the chapter of the newest page, so the latest find is right there
                if (last >= 0 && (onlyFound || (p.fresh != null && p.fresh.Contains(last))))
                {
                    chapter = last < Quotes.CHAPTER_OF.Length ? Quotes.CHAPTER_OF[last] : 0;
                    spread = -1; // resolved to the right spread once the layout is known
                }
                lastTurnKey = null;
            }
            if (!p.open) reading = null;
            bool opening = p.open && !wasOpen;
            wasOpen = p.open;
            style.display = p.open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!p.open) return;
            // a re-render with the same inputs changes nothing (and must not cut a page turn short)
            string sig = Signature();
            if (!opening && sig == lastSig) return;
            lastSig = sig;
            Render();
            if (opening && closeBtn != null)
            {
                var cb = closeBtn;
                schedule.Execute(() => cb.Focus());
            }
        }

        string lastSig;

        string Signature()
        {
            int n = p.unlocked != null ? p.unlocked.Count : 0;
            int last = n > 0 ? p.unlocked[n - 1] : -1;
            int f = 0;
            if (p.fresh != null) foreach (int i in p.fresh) f = f * 31 + i + 1;
            return p.lang + "|" + p.version + "|" + n + "|" + last + "|" + f + "|" + (p.t != null ? p.t.GetHashCode() : 0);
        }

        void OnResize() { if (p.open) Render(); }

        void OnKey(KeyDownEvent e)
        {
            if (!p.open) return;
            if (e.keyCode == KeyCode.Escape)
            {
                e.StopImmediatePropagation();
                if (reading.HasValue) { reading = null; Render(); }
                else if (p.onClose != null) p.onClose();
            }
            else if (!reading.HasValue && (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.LeftArrow))
            {
                e.StopImmediatePropagation();
                e.PreventDefault();
                go((e.keyCode == KeyCode.RightArrow) != rtl ? 1 : -1);
            }
        }

        // ------------------------------------------------------------ layout state

        static int[][] ByChapter()
        {
            if (byChapter != null) return byChapter;
            var lists = new List<int>[Quotes.CHAPTER_COUNT];
            for (int c = 0; c < lists.Length; c++) lists[c] = new List<int>();
            for (int i = 0; i < Quotes.QUOTE_COUNT; i++)
                lists[i < Quotes.CHAPTER_OF.Length ? Quotes.CHAPTER_OF[i] : 0].Add(i);
            byChapter = new int[lists.Length][];
            for (int c = 0; c < lists.Length; c++) byChapter[c] = lists[c].ToArray();
            return byChapter;
        }

        HashSet<int> Found()
        {
            var f = new HashSet<int>();
            if (p.unlocked != null) foreach (int i in p.unlocked) if (i < Quotes.QUOTE_COUNT) f.Add(i);
            return f;
        }

        bool compact { get { return stageH < 380; } }
        bool two { get { return stageW >= 760; } }
        int perPage { get { return Math.Max(2, (int)Math.Floor((stageH - (compact ? 112 : 140)) / (compact ? 70.0 : 92.0))); } }
        int perSpread { get { return perPage * (two ? 2 : 1); } }

        /// <summary>
        /// What the book shows: one chapter, every page of it; or, only finds shown, every
        /// page found in the whole book, chapter after chapter, as one run — with a few
        /// finds, the lot fits on a spread or two and nothing has to be hunted for.
        /// </summary>
        List<int> List(HashSet<int> found)
        {
            var l = new List<int>();
            if (onlyFound)
            {
                foreach (var ch in ByChapter()) foreach (int i in ch) if (found.Contains(i)) l.Add(i);
                return l;
            }
            foreach (int i in ByChapter()[chapter]) l.Add(i);
            return l;
        }

        static int ChapterOf(int i) { return i < Quotes.CHAPTER_OF.Length ? Quotes.CHAPTER_OF[i] : 0; }

        int Spreads(List<int> list) { return Math.Max(1, (int)Math.Ceiling(list.Count / (double)perSpread)); }

        /// <summary>pages found in a chapter</summary>
        static int FoundIn(int c, HashSet<int> found)
        {
            int n = 0;
            foreach (int i in ByChapter()[c]) if (found.Contains(i)) n++;
            return n;
        }

        /// <summary>the next chapter from c in direction d that has something to show (-1: none)</summary>
        int NextChapter(int c, int d, HashSet<int> found)
        {
            for (int k = c + d; k >= 0 && k < Quotes.CHAPTER_COUNT; k += d)
                if (!onlyFound || FoundIn(k, found) > 0) return k;
            return -1;
        }

        void go(int d)
        {
            dir = d;
            var found = Found();
            var list = List(found);
            int spreads = Spreads(list);
            int cur = Math.Min(Math.Max(0, spread), spreads - 1);
            int nx = cur + d;
            int nc = onlyFound ? -1 : NextChapter(chapter, d, found);
            if (nx >= 0 && nx < spreads) spread = nx;
            // past the last spread: on to the next chapter (with finds, when only those are
            // shown), and back the same way
            else if (nc >= 0) { chapter = nc; spread = d > 0 ? 0 : 10000; } // clamped to that chapter's last spread
            else spread = cur;
            Render();
        }

        // ------------------------------------------------------------ render

        void Render()
        {
            string key = p.lang + ":" + p.version;
            if (quotes == null || quotesKey != key) { quotes = Quotes.quotesFor(p.lang); quotesKey = key; }
            var bt = BookText.BOOK_TEXT[p.lang];
            var t = p.t;
            var found = Found();
            var chapters = ByChapter();
            var list = List(found);
            // land on the newest page's spread when the book opens on it
            if (spread == -1)
            {
                int last = p.unlocked != null && p.unlocked.Count > 0 ? p.unlocked[p.unlocked.Count - 1] : -1;
                int pos = last < 0 ? 0 : list.IndexOf(last);
                spread = pos < 0 ? 0 : pos / perSpread;
            }
            int spreads = Spreads(list);
            int at = Math.Min(Math.Max(0, spread), spreads - 1);
            // the finds in one run: the contents mark the chapter the open spread starts in
            if (onlyFound && list.Count > 0) chapter = ChapterOf(list[Math.Min(list.Count - 1, at * perSpread)]);
            bool cpt = compact;
            bool sm = UiKit.Sm;

            // ---- header
            header.Clear();
            if (cpt) { header.style.paddingLeft = SU.Sp(4); header.style.paddingRight = SU.Sp(4); header.style.paddingTop = SU.Sp(2); header.style.paddingBottom = SU.Sp(1); }
            else
            {
                float px = SU.Sp(sm ? 10 : 6);
                header.style.paddingLeft = px; header.style.paddingRight = px;
                header.style.paddingTop = SU.Sp(5); header.style.paddingBottom = SU.Sp(3);
            }
            var titleCol = SU.Box(header);
            titleCol.style.flexGrow = 1;
            titleCol.style.flexShrink = 1;
            titleCol.style.minWidth = 0;
            float hs = cpt ? SU.Rem(1.125) : SU.Rem(sm ? 1.9 : 1.5);
            var h2 = Truncated(SU.Upper(t.bookTitle, p.lang), titleCol, SERIF, 600, false, hs, cpt ? 1.75 / 1.125 : 2 / 1.5, SU.C("#f1e4c4"));
            UiKit.SetSpacing(h2, SU.Track(0.18, hs, p.lang));
            if (!cpt)
            {
                float ss = SU.Rem(0.875);
                var sub = Truncated(t.bookSub, titleCol, SERIF, 400, true, ss, 1.25 / 0.875, SU.C("rgba(241,228,196,0.6)"));
                sub.style.whiteSpace = WhiteSpace.Normal;
                sub.style.marginTop = SU.Sp(0.5);
            }
            if (sm)
            {
                var cnt = SU.Box(header);
                cnt.style.flexShrink = 0;
                cnt.style.flexDirection = FlexDirection.Column;
                cnt.style.alignItems = Align.FlexEnd;
                cnt.style.marginLeft = SU.Sp(4);
                int count = found.Count, total = Quotes.QUOTE_COUNT;
                float cs = SU.Rem(0.875);
                var ct = SU.Text(fmt(t.bookCount, new Dictionary<string, string>
                {
                    { "n", count.ToString(CultureInfo.InvariantCulture) },
                    { "total", total.ToString(CultureInfo.InvariantCulture) },
                }), cnt);
                SU.Font(ct, SERIF);
                ct.style.fontSize = cs;
                ct.style.height = cs * 1.25f / 0.875f;
                ct.style.color = SU.C("#f1e4c4");
                UiKit.SetSpacing(ct, 0.12f * cs);
                double frac = total > 0 ? count / (double)total : 0;
                var bar = new PaintBox((c, w, h) =>
                {
                    c.BeginPath();
                    SU.RRect(c, 0, 0, w, h, h / 2);
                    c.Clip();
                    c.FillStyle = Col.Rgba(255, 255, 255, 0.1);
                    c.FillRect(0, 0, w, h);
                    double fw = w * frac;
                    if (fw <= 0) return;
                    var g = c.CreateLinearGradient(0, 0, fw, 0);
                    g.AddColorStop(0, "#8a6a30");
                    g.AddColorStop(0.5, GOLD);
                    g.AddColorStop(1, "#f0d49a");
                    c.FillStyle = g;
                    c.FillRect(0, 0, fw, h);
                });
                bar.style.height = 2;
                bar.style.width = SU.Sp(44);
                bar.style.marginTop = SU.Sp(1.5);
                cnt.Add(bar);
            }
            // the filter: two halves, all pages or only those found (with how many), so it reads
            // as a choice rather than a word to puzzle over
            {
                var seg = SU.Box(header);
                seg.style.flexDirection = FlexDirection.Row;
                seg.style.flexShrink = 0;
                seg.style.marginLeft = SU.Sp(4);
                SU.Radius(seg, 999);
                SU.Border(seg, 1, SU.C("rgba(241,228,196,0.25)"));
                seg.style.overflow = Overflow.Hidden;
                float fs = SU.Rem(cpt ? 0.62 : 0.68);
                for (int k = 0; k < 2; k++)
                {
                    bool mine = k == 1;
                    bool on = onlyFound == mine;
                    string label = mine ? t.filterFound + " · " + found.Count.ToString(CultureInfo.InvariantCulture) : t.filterAll;
                    var f = new Pill(SU.Upper(label, p.lang), () =>
                    {
                        if (onlyFound == mine) return;
                        onlyFound = mine;
                        filterSet = true;
                        dir = 1;
                        spread = mine ? -1 : 0;
                        if (mine) chapterOfNewest();
                        Render();
                    });
                    seg.Add(f);
                    SU.Radius(f, 0);
                    f.SetBorder(0, new Color(0, 0, 0, 0));
                    SU.Pad(f, SU.Sp(cpt ? 2.5 : 3.5), SU.Sp(cpt ? 1 : 1.5));
                    // no font of its own: the page's sans-serif
                    f.label.style.fontSize = fs;
                    f.label.style.height = fs * 1.5f;
                    UiKit.SetSpacing(f.label, SU.Track(0.16, fs, p.lang));
                    f.label.style.color = on ? SU.C("#1a120b") : SU.C("rgba(241,228,196,0.72)");
                    var bgc = on ? SU.C(GOLD) : new Color(0, 0, 0, 0);
                    f.style.backgroundColor = bgc;
                    f.baseBg = bgc;
                    f.hoverBg = on ? bgc : SU.C("rgba(241,228,196,0.08)");
                }
            }
            // close
            {
                var x = new Pill("", () => { if (p.onClose != null) p.onClose(); });
                x.label.style.display = DisplayStyle.None;
                header.Add(x);
                closeBtn = x;
                x.style.marginLeft = SU.Sp(4);
                float s = SU.Sp(9);
                x.style.width = s;
                x.style.height = s;
                x.SetBorder(1, SU.C("rgba(241,228,196,0.25)"));
                x.hoverBorder = SU.C("rgba(241,228,196,0.5)");
                double[] lvl = { 0 };
                PaintBox icon = null;
                icon = new PaintBox((c, w, h) =>
                {
                    c.Translate((w - 12) / 2, (h - 12) / 2);
                    c.StrokeStyle = Col.Rgba(241, 228, 196, 0.8 + 0.2 * lvl[0]);
                    c.LineWidth = 1.4;
                    c.BeginPath();
                    UiKit.Svg(c, "M1 1 L11 11 M11 1 L1 11");
                    c.Stroke();
                });
                SU.Fill(icon);
                x.Add(icon);
                x.RegisterCallback<PointerEnterEvent>(e => { lvl[0] = 1; icon.Redraw(); });
                x.RegisterCallback<PointerLeaveEvent>(e => { lvl[0] = 0; icon.Redraw(); });
            }

            // ---- body
            if (cpt)
            {
                body.style.paddingLeft = SU.Sp(3); body.style.paddingRight = SU.Sp(3); body.style.paddingBottom = SU.Sp(2) + Ads.BannerHeight;
                bookCol.style.marginLeft = SU.Sp(3);
            }
            else
            {
                float px = SU.Sp(sm ? 10 : 6);
                body.style.paddingLeft = px; body.style.paddingRight = px; body.style.paddingBottom = SU.Sp(6) + Ads.BannerHeight;
                bookCol.style.marginLeft = SU.Sp(6);
            }

            // ---- contents
            nav.Clear();
            navScroll.style.width = SU.Sp(cpt ? 44 : 60);
            if (!cpt)
            {
                float cs = SU.Rem(0.62);
                var head = SU.Text(SU.Upper(bt.contents, p.lang), nav);
                SU.Font(head, SERIF, 600);
                head.style.fontSize = cs;
                head.style.height = cs * 1.5f;
                head.style.color = SU.C(GOLD);
                UiKit.SetSpacing(head, SU.Track(0.32, cs, p.lang));
                head.style.marginBottom = SU.Sp(2);
            }
            for (int c = 0; c < bt.chapters.Length && c < chapters.Length; c++)
                nav.Add(ChapterRow(c, bt, chapters[c], found, cpt));

            // ---- the open book
            string HeadOf(List<int> page)
            {
                int ch = onlyFound && page.Count > 0 ? ChapterOf(page[0]) : chapter;
                return toRoman(ch + 1) + " · " + bt.chapters[ch];
            }
            string turnKey = p.lang + ":" + (onlyFound ? "found" : chapter.ToString(CultureInfo.InvariantCulture)) + ":" + at + ":" + onlyFound + ":" + perPage;
            bool turn = lastTurnKey != null && lastTurnKey != turnKey;
            if (lastTurnKey == null) turn = true;
            lastTurnKey = turnKey;
            int first = at * perSpread;
            int pp = perPage;
            var pageA = new List<int>();
            var pageB = new List<int>();
            for (int k = first; k < Math.Min(list.Count, first + pp); k++) pageA.Add(list[k]);
            if (two) for (int k = first + pp; k < Math.Min(list.Count, first + perSpread); k++) pageB.Add(list[k]);

            float ix = cpt ? SU.Sp(2) : SU.Sp(3), iy = cpt ? SU.Sp(2.5) : SU.Sp(3.5);
            SU.Abs(leaves, ix, iy, ix, iy);
            leaves.Clear();
            // page edges: a few lines of paper showing under the leaves
            var edges = new PaintBox((c, w, h) =>
            {
                double bw = w, bh = h - 16;
                SU.BoxShadow(c, 0, 0, bw, bh, 6, 0, 5, 6, 0, Col.Rgba(0, 0, 0, 0.45));
                string[] cols = { "#c7b285", "#d7c49c", "#cdb98f", "#d9c8a2" };
                for (int k = 0; k < 4; k++)
                {
                    c.FillStyle = cols[k];
                    c.BeginPath();
                    SU.RRect(c, 0, 4 - k, bw, bh, 6);
                    c.Fill();
                }
            });
            SU.Abs(edges, 0, 0, 0, -16);
            leaves.Add(edges);
            leaves.Add(Leaf(two ? "left" : "single", HeadOf(pageA), at * (two ? 2 : 1) + 1, pageA, found, cpt, turn));
            if (two)
            {
                var spine = new PaintBox((c, w, h) =>
                {
                    var g = c.CreateLinearGradient(0, 0, 0, h);
                    g.AddColorStop(0, "#8a6a40");
                    g.AddColorStop(0.5, "#5a4024");
                    g.AddColorStop(1, "#8a6a40");
                    c.FillStyle = g;
                    c.FillRect(0, 0, w, h);
                });
                spine.style.width = 1;
                spine.style.flexShrink = 0;
                leaves.Add(spine);
                leaves.Add(Leaf("right", HeadOf(pageB.Count > 0 ? pageB : pageA), at * 2 + 2, pageB, found, cpt, turn));
            }

            // turning
            SetTurn(prevBtn, onlyFound ? at > 0 : !(at == 0 && chapter == 0), bt.prev);
            SetTurn(nextBtn, onlyFound ? at < spreads - 1 : !(at >= spreads - 1 && chapter == Quotes.CHAPTER_COUNT - 1), bt.next);
            prevBtn.BringToFront();
            nextBtn.BringToFront();

            footer.Clear();
            if (!cpt)
            {
                float fs = SU.Rem(0.7);
                var f = SU.Text((onlyFound ? "" : toRoman(chapter + 1) + " · ") + (at + 1) + " / " + spreads, footer);
                SU.Font(f, SERIF);
                f.style.fontSize = fs;
                f.style.height = fs * 1.5f;
                f.style.color = SU.C("rgba(241,228,196,0.5)");
                UiKit.SetSpacing(f, SU.Track(0.3, fs, p.lang));
                f.style.unityTextAlign = TextAnchor.MiddleCenter;
                f.style.marginTop = SU.Sp(3);
            }

            // ---- one page, read up close
            if (overlay != null) { overlay.RemoveFromHierarchy(); overlay = null; }
            if (reading.HasValue && reading.Value < quotes.Count && quotes[reading.Value] != null)
                overlay = Reader(reading.Value, bt);
        }

        /// <summary>chapter of the newest find (spread resolved at render)</summary>
        void chapterOfNewest()
        {
            int last = p.unlocked != null && p.unlocked.Count > 0 ? p.unlocked[p.unlocked.Count - 1] : -1;
            if (last < 0) return;
            chapter = last < Quotes.CHAPTER_OF.Length ? Quotes.CHAPTER_OF[last] : 0;
            spread = -1;
        }

        // ------------------------------------------------------------ pieces

        /// <summary>a one-line label cut with an ellipsis (truncate)</summary>
        static Label Truncated(string text, VisualElement parent, string family, int weight, bool italic, float size, double lh, Color color)
        {
            var l = SU.Text(text, parent);
            SU.Font(l, family, weight, italic);
            l.style.fontSize = size;
            l.style.minHeight = (float)(size * lh);
            l.style.color = color;
            l.style.overflow = Overflow.Hidden;
            l.style.textOverflow = TextOverflow.Ellipsis;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.flexShrink = 1;
            return l;
        }

        /// <summary>
        /// the offset from the top of a line box (line-height 1.5) to the baseline, for
        /// `items-baseline` (UI Toolkit has no baseline alignment): Cormorant's hhea metrics,
        /// or a typical system sans-serif's
        /// </summary>
        static float Baseline(float size, bool sans)
        {
            double asc = sans ? 0.928 : 0.924, desc = sans ? 0.244 : 0.287;
            return (float)(size * ((1.5 - (asc + desc)) / 2 + asc));
        }

        VisualElement ChapterRow(int c, BookTextEntry bt, int[] items, HashSet<int> found, bool cpt)
        {
            int n = 0;
            foreach (int i in items) if (found.Contains(i)) n++;
            int of = items.Length;
            bool done = n == of;
            bool active = c == chapter;
            // only finds shown: a chapter with none is there to be counted, not opened
            bool empty = onlyFound && n == 0;
            var row = SU.Box();
            row.pickingMode = PickingMode.Position;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.borderLeftWidth = 2;
            row.style.borderLeftColor = active ? SU.C(GOLD) : new Color(0, 0, 0, 0);
            if (cpt) { row.style.paddingTop = row.style.paddingBottom = SU.Sp(1); row.style.paddingRight = SU.Sp(1); row.style.paddingLeft = SU.Sp(2); }
            else { row.style.paddingTop = row.style.paddingBottom = SU.Sp(1.5); row.style.paddingRight = SU.Sp(2); row.style.paddingLeft = SU.Sp(3); }
            if (empty) row.style.opacity = 0.38f;
            int cc = c;
            row.RegisterCallback<ClickEvent>(e =>
            {
                if (empty) return;
                dir = cc > chapter ? 1 : -1;
                if (onlyFound)
                {
                    // to the spread where this chapter's finds begin
                    var l = List(Found());
                    int pos = l.FindIndex(i => ChapterOf(i) == cc);
                    spread = pos < 0 ? 0 : pos / perSpread;
                }
                else
                {
                    chapter = cc;
                    spread = 0;
                }
                Render();
            });
            if (active)
            {
                var bg = new PaintBox((ctx, w, h) =>
                {
                    var g = ctx.CreateLinearGradient(0, 0, w, 0);
                    g.AddColorStop(0, Col.Rgba(201, 161, 90, 0.14));
                    g.AddColorStop(1, Col.Rgba(201, 161, 90, 0));
                    ctx.FillStyle = g;
                    ctx.FillRect(0, 0, w, h);
                });
                SU.Fill(bg);
                row.Add(bg);
            }
            float rs = SU.Rem(0.7), ns = SU.Rem(cpt ? 0.85 : 0.98), cs = SU.Rem(0.62);
            float baseline = Baseline(ns, false);
            var roman = SU.Text(toRoman(c + 1), row);
            SU.Font(roman, SERIF);
            roman.style.fontSize = rs;
            roman.style.height = rs * 1.5f;
            roman.style.width = SU.Sp(7);
            roman.style.flexShrink = 0;
            roman.style.unityTextAlign = TextAnchor.MiddleLeft;
            roman.style.color = active ? SU.C(GOLD) : SU.C("rgba(241,228,196,0.4)");
            roman.style.marginTop = baseline - Baseline(rs, false);
            var name = Truncated(bt.chapters[c], row, SERIF, active ? 600 : 400, false, ns, 1.5,
                active ? SU.C("#f6ead0") : SU.C("rgba(241,228,196,0.72)"));
            name.style.height = ns * 1.5f;
            name.style.flexGrow = 1;
            name.style.minWidth = 0;
            name.style.marginLeft = SU.Sp(2);
            if (done)
            {
                float size = cpt ? 12 : 14;
                var seal = new PaintBox((ctx, w, h) => Seal(ctx, size));
                seal.style.width = size;
                seal.style.height = size;
                seal.style.flexShrink = 0;
                seal.style.marginLeft = SU.Sp(2);
                seal.style.alignSelf = Align.Center;
                row.Add(seal);
            }
            else
            {
                var cnt = SU.Text(n + "/" + of, row);
                // no font of its own: the page's sans-serif
                cnt.style.fontSize = cs;
                cnt.style.height = cs * 1.5f;
                cnt.style.flexShrink = 0;
                cnt.style.color = SU.C("rgba(241,228,196,0.45)");
                cnt.style.marginLeft = SU.Sp(2);
                cnt.style.marginTop = baseline - Baseline(cs, true);
            }
            var track = SU.Box(row);
            SU.Abs(track, SU.Sp(12), null, SU.Sp(2), SU.Sp(0.5));
            track.style.height = 1;
            track.style.overflow = Overflow.Hidden;
            track.style.backgroundColor = SU.C("rgba(241,228,196,0.06)");
            var fill = SU.Box(track);
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(of > 0 ? 100f * n / of : 0);
            fill.style.backgroundColor = SU.C("rgba(201,161,90,0.55)");
            return row;
        }

        VisualElement Leaf(string side, string head, int folio, List<int> entries, HashSet<int> found, bool cpt, bool turn)
        {
            var sec = SU.Box();
            sec.style.flexGrow = 1;
            sec.style.flexShrink = 1;
            sec.style.flexBasis = 0;
            sec.style.minWidth = 0;
            sec.style.overflow = Overflow.Hidden;
            sec.style.flexDirection = FlexDirection.Column;
            float r = 6;
            if (side != "right") { sec.style.borderTopLeftRadius = r; sec.style.borderBottomLeftRadius = r; }
            if (side != "left") { sec.style.borderTopRightRadius = r; sec.style.borderBottomRightRadius = r; }
            var paper = new PaintBox((c, w, h) =>
            {
                PaintPaper(c, w, h);
                // the binding casts its shadow into the page from the spine side
                if (side == "single") return;
                var g = side == "left" ? c.CreateLinearGradient(w, 0, 0, 0) : c.CreateLinearGradient(0, 0, w, 0);
                g.AddColorStop(0, Col.Rgba(60, 35, 10, 0.32));
                g.AddColorStop(0.07, Col.Rgba(60, 35, 10, 0.08));
                g.AddColorStop(0.16, Col.Rgba(60, 35, 10, 0));
                c.FillStyle = g;
                c.FillRect(0, 0, w, h);
            });
            SU.Fill(paper);
            sec.Add(paper);

            var inner = SU.Box(sec);
            inner.style.flexGrow = 1;
            inner.style.flexDirection = FlexDirection.Column;
            if (cpt) { inner.style.paddingLeft = inner.style.paddingRight = SU.Sp(5); inner.style.paddingTop = SU.Sp(3); inner.style.paddingBottom = SU.Sp(2); }
            else { inner.style.paddingLeft = inner.style.paddingRight = SU.Sp(8); inner.style.paddingTop = SU.Sp(6); inner.style.paddingBottom = SU.Sp(4); }

            float hs = SU.Rem(0.62);
            var hl = Truncated(SU.Upper(head, p.lang), inner, SERIF, 600, false, hs, 1.5, SU.C(SEPIA));
            hl.style.height = hs * 1.5f;
            UiKit.SetSpacing(hl, SU.Track(0.32, hs, p.lang));
            hl.style.unityTextAlign = TextAnchor.MiddleCenter;
            var rule = new PaintBox((c, w, h) => Rule(c, w, h, SEPIA));
            rule.style.alignSelf = Align.Center;
            rule.style.width = SU.Sp(cpt ? 20 : 28);
            rule.style.height = SU.Sp(cpt ? 1.5 : 2);
            rule.style.marginTop = SU.Sp(cpt ? 1 : 2);
            inner.Add(rule);

            var ol = SU.Box(inner);
            ol.style.marginTop = SU.Sp(3);
            ol.style.flexGrow = 1;
            ol.style.flexDirection = FlexDirection.Column;
            for (int k = 0; k < entries.Count; k++)
            {
                var row = EntryRow(entries[k], found, cpt);
                if (k > 0) row.style.marginTop = SU.Sp(cpt ? 2 : 4);
                ol.Add(row);
            }

            float fs = SU.Rem(0.7);
            var foot = SU.Text("— " + folio + " —", inner);
            SU.Font(foot, SERIF);
            foot.style.fontSize = fs;
            foot.style.height = fs * 1.5f;
            foot.style.color = SU.C(SEPIA);
            foot.style.unityTextAlign = TextAnchor.MiddleCenter;

            if (turn)
            {
                // book-turn-next / -prev: a leaf settling (PORT: rotateY in perspective becomes a
                // horizontal squash about the same origin; UI Toolkit has no 3D transforms)
                sec.style.transformOrigin = new TransformOrigin(Length.Percent(side == "left" ? 100 : 0), Length.Percent(50));
                int d = dir;
                UiKit.Tween(sec, 420, 0, SU.Out, e =>
                {
                    double ang = (d > 0 ? -14 : 14) * (1 - e) * Math.PI / 180;
                    sec.style.opacity = (float)(0.2 + 0.8 * e);
                    sec.style.scale = new Scale(new Vector2((float)Math.Cos(ang), 1));
                    sec.style.translate = new Translate((float)((d > 0 ? 10 : -10) * (1 - e)), 0);
                });
            }
            return sec;
        }

        VisualElement EntryRow(int index, HashSet<int> found, bool cpt)
        {
            bool has = found.Contains(index) && index < quotes.Count && quotes[index] != null;
            var li = SU.Box();
            li.style.flexDirection = FlexDirection.Row;
            li.style.minHeight = cpt ? 54 : 70;
            li.style.flexShrink = 0;
            float ns = SU.Rem(0.78);
            var num = SU.Text((index + 1).ToString(CultureInfo.InvariantCulture), li);
            SU.Font(num, SERIF);
            num.style.fontSize = ns;
            num.style.height = ns * 1.5f;
            num.style.width = SU.Sp(8);
            num.style.flexShrink = 0;
            num.style.marginTop = 3;
            num.style.unityTextAlign = TextAnchor.MiddleRight;
            num.style.color = has ? SU.C(SEPIA) : SU.C("rgba(107,74,34,0.45)");
            if (has)
            {
                var q = quotes[index];
                var btn = SU.Box(li);
                btn.pickingMode = PickingMode.Position;
                btn.focusable = true;
                btn.style.flexGrow = 1;
                btn.style.flexShrink = 1;
                btn.style.minWidth = 0;
                btn.style.marginLeft = SU.Sp(3);
                btn.style.flexDirection = FlexDirection.Column;
                int idx = index;
                btn.RegisterCallback<ClickEvent>(e => { reading = idx; Render(); });
                btn.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space)
                    {
                        e.StopPropagation();
                        reading = idx;
                        Render();
                    }
                });
                var text = new Flow();
                bool xl = UiKit.Vw(100) >= SU.Rem(80);
                text.Set(q.text, new Flow.Opts
                {
                    family = SERIF,
                    weight = 500,
                    italic = true,
                    size = SU.Rem(cpt ? 0.98 : xl ? 1.2 : 1.08),
                    lineHeight = 1.375,
                    color = SU.C(INK),
                    align = "left",
                    rtl = rtl,
                    maxLines = cpt ? 2 : 3,
                });
                var tw = SU.Box(btn);
                tw.Add(text);
                // group-hover:text-black
                btn.RegisterCallback<PointerEnterEvent>(e => text.SetColor(Color.black));
                btn.RegisterCallback<PointerLeaveEvent>(e => text.SetColor(SU.C(INK)));
                float As = SU.Rem(0.62);
                // the finds in one run: each says which chapter it belongs to
                var author = SU.Text(SU.Upper(q.author, p.lang) + (onlyFound ? "  ·  " + toRoman(ChapterOf(index) + 1) : ""), btn);
                SU.Font(author, SERIF, 600);
                author.style.fontSize = As;
                author.style.minHeight = As * 1.5f;
                author.style.whiteSpace = WhiteSpace.Normal;
                author.style.unityTextAlign = TextAnchor.MiddleRight;
                author.style.color = SU.C(SEPIA);
                UiKit.SetSpacing(author, SU.Track(0.22, As, p.lang));
                author.style.marginTop = SU.Sp(0.5);
                if (p.fresh != null && p.fresh.Contains(index))
                {
                    // a drop of wax by a new page (box-shadow 0 0 0 2px as a ring)
                    var ring = SU.Box(li);
                    SU.Abs(ring, -4 - 2, 4 - 2, null, null);
                    ring.style.width = 12; ring.style.height = 12;
                    SU.Radius(ring, 6);
                    SU.Border(ring, 2, SU.C("rgba(142,43,30,0.2)"));
                    var dot = SU.Box(ring);
                    dot.style.width = 8; dot.style.height = 8;
                    SU.Radius(dot, 4);
                    dot.style.backgroundColor = SU.C(WAX);
                }
            }
            else
            {
                // not found yet: the ruled lines the words will go on
                var lines = SU.Box(li);
                lines.style.flexGrow = 1;
                lines.style.flexShrink = 1;
                lines.style.minWidth = 0;
                lines.style.marginLeft = SU.Sp(3);
                lines.style.flexDirection = FlexDirection.Column;
                lines.style.justifyContent = Justify.Center;
                lines.style.paddingTop = SU.Sp(1);
                var a = SU.Box(lines);
                a.style.height = 1; a.style.width = Length.Percent(100);
                a.style.backgroundColor = SU.C("rgba(107,74,34,0.18)");
                var b = SU.Box(lines);
                b.style.height = 1; b.style.width = Length.Percent(72);
                b.style.marginTop = 9;
                b.style.backgroundColor = SU.C("rgba(107,74,34,0.14)");
                if (!cpt)
                {
                    var c = SU.Box(lines);
                    c.style.height = 1; c.style.width = Length.Percent(38);
                    c.style.marginTop = 9;
                    c.style.alignSelf = Align.FlexEnd;
                    c.style.backgroundColor = SU.C("rgba(107,74,34,0.12)");
                }
            }
            return li;
        }

        Pill Turn(int d)
        {
            var b = new Pill("", () => go(d));
            b.label.style.display = DisplayStyle.None;
            stage.Add(b);
            float s = SU.Sp(10);
            b.style.position = Position.Absolute;
            b.style.width = s;
            b.style.height = s;
            b.style.top = Length.Percent(50);
            b.style.marginTop = -s / 2;
            if (d < 0) b.style.left = -4 - s / 2;
            else b.style.right = -4 - s / 2;
            b.SetBorder(1, SU.C("rgba(201,161,90,0.4)"));
            b.hoverBorder = SU.C(GOLD);
            b.baseBg = b.hoverBg = SU.C("rgba(26,18,11,0.9)");
            b.style.backgroundColor = b.baseBg;
            // shadow-lg
            var s1 = new Glow(b, 15, SU.C("rgba(0,0,0,0.1)"), -1, 10, -3);
            s1.level = 1;
            var s2 = new Glow(b, 6, SU.C("rgba(0,0,0,0.1)"), -1, 4, -4);
            s2.level = 1;
            var icon = new PaintBox((c, w, h) =>
            {
                c.Translate((w - 10) / 2, (h - 14) / 2);
                c.StrokeStyle = "#e9d3a4";
                c.LineWidth = 1.5;
                c.BeginPath();
                UiKit.Svg(c, d < 0 ? "M8 1 L2 7 L8 13" : "M2 1 L8 7 L2 13");
                c.Stroke();
            });
            SU.Fill(icon);
            b.Add(icon);
            return b;
        }

        static void SetTurn(Pill b, bool enabled, string label)
        {
            // disabled:opacity-25 (not SetEnabled: the runtime theme would restyle it)
            b.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
            b.focusable = enabled;
            b.style.opacity = enabled ? 1 : 0.25f;
            b.baseOpacity = b.hoverOpacity = enabled ? 1 : 0.25f;
            b.tooltip = label;
        }

        VisualElement Reader(int idx, BookTextEntry bt)
        {
            var q = quotes[idx];
            var t = p.t;
            bool sm = UiKit.Sm;
            var ov = SU.Box(this);
            ov.pickingMode = PickingMode.Position;
            SU.Fill(ov);
            ov.style.backgroundColor = SU.C("rgba(0,0,0,0.75)");
            ov.style.alignItems = Align.Center;
            ov.style.justifyContent = Justify.Center;
            float pad = SU.Sp(6);
            ov.style.paddingLeft = ov.style.paddingRight = ov.style.paddingTop = ov.style.paddingBottom = pad;
            // PORT: backdrop-blur(2px) is not available
            ov.RegisterCallback<ClickEvent>(e => { reading = null; Render(); });

            var fig = SU.Box(ov);
            fig.pickingMode = PickingMode.Position;
            fig.style.width = Length.Percent(100);
            fig.style.maxWidth = SU.Rem(32);
            fig.style.maxHeight = Length.Percent(100);
            fig.style.flexShrink = 1;
            SU.Radius(fig, 4);
            fig.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            var shadow = new Glow(fig, 80, SU.C("rgba(0,0,0,0.9)"), 4, 40, -20);
            shadow.level = 1;
            var paper = new PaintBox((c, w, h) =>
            {
                c.BeginPath();
                SU.RRect(c, 0, 0, w, h, 4);
                c.Clip();
                PaintPaper(c, w, h);
            });
            SU.Fill(paper);
            fig.Add(paper);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            SU.Zero(scroll);
            scroll.style.flexShrink = 1;
            fig.Add(scroll);
            var col = SU.Box(scroll);
            col.style.flexDirection = FlexDirection.Column;
            col.style.alignItems = Align.Stretch;
            float px = SU.Sp(sm ? 12 : 8);
            col.style.paddingLeft = col.style.paddingRight = col.style.paddingTop = col.style.paddingBottom = px;

            // the rings pressed into its edge, over the content as inset shadows are
            var rings = new PaintBox((c, w, h) =>
            {
                SU.InsetShadow(c, 0, 0, w, h, 4, 0, 0, 0, 7, Col.Rgba(107, 74, 34, 0.35));
                SU.InsetShadow(c, 0, 0, w, h, 4, 0, 0, 0, 6, Col.Rgba(236, 223, 194, 1));
                SU.InsetShadow(c, 0, 0, w, h, 4, 0, 0, 0, 1, Col.Rgba(107, 74, 34, 0.25));
            });
            SU.Fill(rings);
            fig.Insert(1, rings);

            int ch = idx < Quotes.CHAPTER_OF.Length ? Quotes.CHAPTER_OF[idx] : 0;
            float hs = SU.Rem(0.65);
            var head = SU.Text(SU.Upper(toRoman(ch + 1) + " · " + bt.chapters[ch], p.lang), col);
            SU.Font(head, SERIF, 600);
            head.style.fontSize = hs;
            head.style.minHeight = hs * 1.5f;
            head.style.whiteSpace = WhiteSpace.Normal;
            head.style.unityTextAlign = TextAnchor.MiddleCenter;
            head.style.color = SU.C(SEPIA);
            UiKit.SetSpacing(head, SU.Track(0.35, hs, p.lang));
            var rule = new PaintBox((c, w, h) => Rule(c, w, h, SEPIA));
            rule.style.alignSelf = Align.Center;
            rule.style.width = SU.Sp(32);
            rule.style.height = SU.Sp(2.5);
            rule.style.marginTop = SU.Sp(3);
            col.Add(rule);

            var bq = new Flow();
            bq.Set(q.text, new Flow.Opts
            {
                family = SERIF,
                weight = 500,
                italic = true,
                size = SU.Rem(sm ? 2 : 1.65),
                lineHeight = 1.375,
                color = SU.C(INK),
                rtl = rtl ? true : (bool?)null,
            });
            var bw = SU.Box(col);
            bw.style.marginTop = SU.Sp(6);
            bw.Add(bq);

            float cs = SU.Rem(0.8);
            var cap = SU.Text(SU.Upper(q.author, p.lang), col);
            SU.Font(cap, SERIF, 600);
            cap.style.fontSize = cs;
            cap.style.minHeight = cs * 1.5f;
            cap.style.whiteSpace = WhiteSpace.Normal;
            cap.style.unityTextAlign = TextAnchor.MiddleCenter;
            cap.style.color = SU.C(SEPIA);
            UiKit.SetSpacing(cap, SU.Track(0.3, cs, p.lang));
            cap.style.marginTop = SU.Sp(6);

            if (!string.IsNullOrEmpty(q.note))
            {
                var note = new Flow();
                note.Set(q.note, new Flow.Opts
                {
                    family = SERIF,
                    italic = true,
                    size = SU.Rem(0.86),
                    lineHeight = 1.375,
                    color = SU.C("rgba(90,61,28,0.85)"),
                    rtl = rtl ? true : (bool?)null,
                });
                var nw = SU.Box(col);
                nw.style.alignSelf = Align.Center;
                nw.style.maxWidth = SU.Rem(24);
                nw.style.width = Length.Percent(100);
                nw.style.marginTop = SU.Sp(2);
                nw.Add(note);
            }

            float ns = SU.Rem(0.7);
            var no = SU.Text("№ " + (idx + 1), col);
            SU.Font(no, SERIF);
            no.style.fontSize = ns;
            no.style.height = ns * 1.5f;
            no.style.unityTextAlign = TextAnchor.MiddleCenter;
            no.style.color = SU.C("rgba(107,74,34,0.7)");
            no.style.marginTop = SU.Sp(2);

            var btns = SU.Box(col);
            btns.style.flexDirection = FlexDirection.Row;
            btns.style.justifyContent = Justify.Center;
            btns.style.alignItems = Align.Center;
            btns.style.marginTop = SU.Sp(7);
            var share = new ShareButton(new ShareButtonProps
            {
                index = idx,
                lang = p.lang,
                t = t,
                border = SU.C("rgba(107,74,34,0.4)"),
                color = SU.C(SEPIA),
                hoverBg = SU.C("rgba(107,74,34,0.1)"),
                padX = SU.Sp(5),
            });
            btns.Add(share);
            var close = new Pill(SU.Upper(t.closeBtn, p.lang), () => { reading = null; Render(); });
            btns.Add(close);
            close.style.marginLeft = SU.Sp(3);
            float bs = SU.Rem(0.62);
            SU.Font(close.label, SERIF, 600);
            close.label.style.fontSize = bs;
            close.label.style.height = bs * 1.5f;
            close.label.style.color = SU.C(SEPIA);
            UiKit.SetSpacing(close.label, SU.Track(0.3, bs, p.lang));
            close.SetBorder(1, SU.C("rgba(107,74,34,0.4)"));
            close.hoverBg = SU.C("rgba(107,74,34,0.1)");
            SU.Pad(close, SU.Sp(6), SU.Sp(1.5));

            // book-reveal
            UiKit.Tween(fig, 350, 0, SU.Out, k =>
            {
                fig.style.opacity = (float)k;
                fig.style.translate = new Translate(0, (float)(12 * (1 - k)));
                float s = (float)(0.97 + 0.03 * k);
                fig.style.scale = new Scale(new Vector2(s, s));
            });
            return ov;
        }

        // ------------------------------------------------------------ painting

        /// <summary>parchment: warm laid paper, darker and foxed toward the edges</summary>
        static void PaintPaper(Ctx c, double w, double h)
        {
            // the layers of background-image, the last listed first
            var g = SU.CssLinear(c, 0, 0, w, h, 170);
            g.AddColorStop(0, "#f3e9d1");
            g.AddColorStop(0.55, "#e9dbb9");
            g.AddColorStop(1, "#e0cda4");
            c.FillStyle = g;
            c.FillRect(0, 0, w, h);
            SU.CssRadialEllipse(c, w, h, 0.5, 0.5, (0.58, Col.Rgba(120, 78, 30, 0)), (1, Col.Rgba(120, 78, 30, 0.30)));
            SU.CssRadialCircle(c, w, h, 0.72, 0.30, (0, Col.Rgba(255, 252, 240, 0.5)), (0.45, Col.Rgba(255, 252, 240, 0)));
            SU.CssRadialCircle(c, w, h, 0.88, 0.84, (0, Col.Rgba(150, 100, 40, 0.12)), (0.22, Col.Rgba(150, 100, 40, 0)));
            SU.CssRadialCircle(c, w, h, 0.12, 0.18, (0, Col.Rgba(150, 100, 40, 0.10)), (0.18, Col.Rgba(150, 100, 40, 0)));
            // laid lines, barely there: 1px in every 4, from the bottom up (0deg)
            c.FillStyle = Col.Rgba(120, 85, 40, 0.035);
            for (double y = h - 1; y > -1; y -= 4) c.FillRect(0, y, w, 1);
        }

        /// <summary>the board: leather, gold-pressed Greek keys, an inner shade</summary>
        static void PaintBoard(Ctx c, double w, double h)
        {
            c.Save();
            c.BeginPath();
            SU.RRect(c, 0, 0, w, h, 10);
            c.Clip();
            var g = SU.CssLinear(c, 0, 0, w, h, 160);
            g.AddColorStop(0, "#4a2f1c");
            g.AddColorStop(0.6, "#2e1c10");
            g.AddColorStop(1, "#24160c");
            c.FillStyle = g;
            c.FillRect(0, 0, w, h);
            c.Restore();
            SU.InsetShadow(c, 0, 0, w, h, 10, 0, 0, 40, 0, Col.Rgba(0, 0, 0, 0.5));
            SU.InsetShadow(c, 0, 0, w, h, 10, 0, 0, 0, 1, Col.Rgba(255, 220, 170, 0.08));
            // the Greek key, an <svg> of its intrinsic 30×6 at the left of each edge (inset-x-6 top-1)
            double mx = UiKit.Rem(1.5), my = UiKit.Rem(0.25), mh = UiKit.Rem(0.375), mw = mh * 40 / 8;
            Meander(c, mx, my, mw, mh, false);
            Meander(c, mx, h - my - mh, mw, mh, true);
        }

        static void Meander(Ctx c, double x, double y, double w, double h, bool flip)
        {
            c.Save();
            c.Translate(x, y);
            if (flip)
            {
                c.Translate(w / 2, h / 2);
                c.Rotate(Math.PI);
                c.Translate(-w / 2, -h / 2);
            }
            c.Scale(w / 40, h / 8);
            c.StrokeStyle = Col.Rgba(201, 161, 90, 0.35);
            c.LineWidth = 0.8;
            c.BeginPath();
            UiKit.Svg(c, "M0 7 H6 V1 H14 V5 H10 V3 M10 7 H20 V1 H28 V5 H24 V3 M24 7 H34 V1 H40");
            c.Stroke();
            c.Restore();
        }

        /// <summary>a small engraved ornament: a rule with a lozenge at its centre (viewBox 120×8, meet)</summary>
        static void Rule(Ctx c, double w, double h, string color)
        {
            double s = Math.Min(w / 120, h / 8);
            c.Save();
            c.Translate((w - 120 * s) / 2, (h - 8 * s) / 2);
            c.Scale(s, s);
            Col col = color;
            c.StrokeStyle = col.WithAlpha(0.6);
            c.LineWidth = 0.6;
            c.BeginPath();
            c.MoveTo(0, 4); c.LineTo(50, 4);
            c.MoveTo(70, 4); c.LineTo(120, 4);
            c.Stroke();
            c.FillStyle = col.WithAlpha(0.75);
            c.BeginPath();
            UiKit.Svg(c, "M60 0.8 L63.2 4 L60 7.2 L56.8 4 Z");
            c.Fill();
            c.FillStyle = col.WithAlpha(0.6);
            c.BeginPath();
            c.Arc(52.5, 4, 0.9, 0, Math.PI * 2);
            c.Fill();
            c.BeginPath();
            c.Arc(67.5, 4, 0.9, 0, Math.PI * 2);
            c.Fill();
            c.Restore();
        }

        /// <summary>a laurel-and-seal mark for a finished chapter (viewBox 16)</summary>
        static void Seal(Ctx c, double size)
        {
            c.Save();
            c.Scale(size / 16, size / 16);
            c.FillStyle = WAX;
            c.BeginPath();
            c.Arc(8, 8, 7, 0, Math.PI * 2);
            c.Fill();
            Col cream = "#e8c9a0";
            c.StrokeStyle = cream.WithAlpha(0.7);
            c.LineWidth = 0.6;
            c.BeginPath();
            c.Arc(8, 8, 5.2, 0, Math.PI * 2);
            c.Stroke();
            c.FillStyle = cream.WithAlpha(0.85);
            c.BeginPath();
            UiKit.Svg(c, "M8 4.2 L9.1 6.9 L12 7.1 L9.8 8.9 L10.5 11.7 L8 10.2 L5.5 11.7 L6.2 8.9 L4 7.1 L6.9 6.9 Z");
            c.Fill();
            c.Restore();
        }
    }
}
