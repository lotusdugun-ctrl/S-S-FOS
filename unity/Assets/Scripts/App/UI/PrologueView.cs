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
    public sealed class PrologueProps
    {
        public string lang;
        public Action onDone;
    }

    /// <summary>
    /// The opening (Prologue.tsx). Four cards out of the dark, each over a moving picture of
    /// its own, and then the name.
    ///
    ///   I    Corinth at night on its hill over the sea: the king who cheated Death.
    ///   II   A chain across the dark, its middle link slowly breaking open.
    ///   III  The Pleiades, seven sisters drawn in by a constellation line — and
    ///        one of them, Merope, faint.
    ///   IV   The hill: a tiny man pushes a stone up it, and it rolls back.
    ///
    /// The words of each card come in one at a time out of a blur, the quoted words in gold;
    /// a thin line under each card fills while it is read. A tap moves on; the pictures
    /// cross-fade, never cut.
    ///
    /// Add it over everything (z-45); remove it after onDone.
    /// </summary>
    public sealed class PrologueView : VisualElement
    {
        const string TITLE = UiKit.CINZEL;
        const string BODY = UiKit.MARCELLUS;
        const string CREAM = "#f1e4c4";
        const string GOLD = "#d9b46a";
        static readonly string[] NUMS = { "I", "II", "III", "IV", "V", "VI" };

        PrologueProps p;
        int i;
        bool leaving;
        // PORT: prefers-reduced-motion has no counterpart on the phones; always full motion
        readonly bool reduced = false;
        Timer cardTimer;
        readonly Backdrop backdrop;
        VisualElement card, titleBox, skip;
        UiKit.TweenHandle leaveFade;

        /// <summary>how long a card stays, by how much there is to read</summary>
        static double holdFor(string text) { return Math.Min(9500, Math.Max(5200, 2600 + text.Length * 48)); }

        public PrologueView(PrologueProps props)
        {
            p = props;
            pickingMode = PickingMode.Position;
            SU.Fill(this);
            style.overflow = Overflow.Hidden;
            style.backgroundColor = Color.black;
            focusable = true;
            tabIndex = -1;
            RegisterCallback<ClickEvent>(e => Next());
            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return) Next();
            });

            backdrop = new Backdrop(reduced);
            Add(backdrop);
            // letterbox (the first bar grows from the top; the second, not the last div, from its centre)
            for (int k = 0; k < 2; k++)
            {
                var bar = SU.Box(this);
                SU.Abs(bar, 0, k == 0 ? 0 : (float?)null, 0, k == 1 ? 0 : (float?)null);
                bar.style.height = Length.Percent(9);
                bar.style.backgroundColor = Color.black;
                bar.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(k == 0 ? 0 : 50));
                if (!reduced)
                    UiKit.Tween(bar, 2400, 0, SU.Out, e => bar.style.scale = new Scale(new Vector2(1, (float)(2.2 + (1 - 2.2) * e))));
            }
            UiKit.changed += OnResize;
            RegisterCallback<DetachFromPanelEvent>(e => UiKit.changed -= OnResize);
            RegisterCallback<AttachToPanelEvent>(e => { UiKit.changed -= OnResize; UiKit.changed += OnResize; });
            Render(true);
        }

        public void Update(PrologueProps props)
        {
            bool re = props.lang != p.lang;
            p = props;
            if (re) Render(false);
        }

        string[] cards { get { return Journey.STORY[p.lang].prologue; } }
        int n { get { return cards.Length; } }

        // a tap and the card's own timer can land together: only one may move on
        void Next()
        {
            if (leaving) return;
            if (i >= n)
            {
                if (p.onDone != null) p.onDone();
                return;
            }
            leaving = true;
            SetLeaving(true);
            Timer.After(this, reduced ? 0 : 520, () =>
            {
                leaving = false;
                i = Math.Min(n, i + 1);
                Render(true);
                // the card's own `transition: opacity .5s` brings the next one back in
                if (card != null && !reduced)
                {
                    var c = card;
                    c.style.opacity = 0;
                    leaveFade = UiKit.Tween(c, 500, 0, UiKit.Ease, k => c.style.opacity = (float)k);
                }
            });
        }

        /// <summary>`transition: opacity .5s, filter .5s` on the card (PORT: the blur is dropped)</summary>
        void SetLeaving(bool on)
        {
            if (card == null) return;
            var c = card;
            float from = c.resolvedStyle.opacity;
            if (leaveFade != null) leaveFade.Stop();
            leaveFade = UiKit.Tween(c, 500, 0, UiKit.Ease, k => c.style.opacity = (float)(from + ((on ? 0 : 1) - from) * k));
        }

        void OnResize() { Render(false); }

        void Render(bool fresh)
        {
            var S = Journey.STORY[p.lang];
            bool isTitle = i >= n;
            backdrop.scene = Math.Min(i, n - 1);
            backdrop.title = isTitle;
            if (card != null) card.RemoveFromHierarchy();
            if (titleBox != null) titleBox.RemoveFromHierarchy();
            if (skip != null) skip.RemoveFromHierarchy();
            card = titleBox = skip = null;
            if (fresh)
            {
                if (cardTimer != null) cardTimer.Cancel();
                cardTimer = null;
                if (i < n) cardTimer = Timer.After(this, holdFor(cards[i] ?? ""), Next);
            }
            bool joined = SU.Joined(p.lang);

            if (!isTitle)
            {
                string text = cards[i] ?? "";
                card = SU.Box(this);
                SU.Abs(card, 0, null, 0, null);
                card.style.bottom = Length.Percent(14);
                card.style.flexDirection = FlexDirection.Column;
                card.style.alignItems = Align.Center;
                card.style.paddingLeft = SU.Sp(6);
                card.style.paddingRight = SU.Sp(6);
                card.style.opacity = leaving ? 0 : 1;

                var nums = SU.Box(card);
                nums.style.flexDirection = FlexDirection.Row;
                nums.style.alignItems = Align.Center;
                nums.style.marginBottom = SU.Sp(4);
                var gold = SU.C(GOLD);
                var r1 = SU.Box(nums);
                r1.style.height = 1; r1.style.width = SU.Sp(10);
                r1.style.backgroundColor = SU.Alpha(gold, 0.6);
                float ns = SU.Rem(0.8);
                var num = SU.Text(NUMS[i], nums);
                SU.Font(num, TITLE);
                num.style.fontSize = ns;
                num.style.height = ns * 1.5f;
                num.style.color = gold;
                num.style.marginLeft = SU.Sp(3);
                num.style.marginRight = SU.Sp(3);
                num.style.unityTextAlign = TextAnchor.MiddleCenter;
                var r2 = SU.Box(nums);
                r2.style.height = 1; r2.style.width = SU.Sp(10);
                r2.style.backgroundColor = SU.Alpha(gold, 0.6);
                if (fresh && !reduced)
                    // .prologue-num 1.4s ease-out: the numeral draws together (normal spacing for hi/bn/ar)
                    UiKit.Tween(num, 1400, 0, UiKit.EaseOut, k =>
                    {
                        num.style.opacity = (float)k;
                        UiKit.SetSpacing(num, joined ? 0 : (float)((1.4 + (0.5 - 1.4) * k) * ns));
                    });
                else UiKit.SetSpacing(num, joined ? 0 : 0.5f * ns);

                var words = new Flow();
                words.Set(text, new Flow.Opts
                {
                    family = BODY,
                    size = SU.Rem(UiKit.Sm ? 1.8 : 1.35),
                    lineHeight = 1.65,
                    color = SU.C(CREAM),
                    gold = gold,
                    goldItalic = true,
                    shadow = new TextShadow { offset = new Vector2(0, 2), blurRadius = 18, color = SU.C("rgba(0,0,0,0.9)") },
                });
                var ww = SU.Box(card);
                ww.style.maxWidth = SU.Rem(48);
                ww.style.alignSelf = Align.Center;
                ww.Add(words);
                double step = text.Length > 120 ? 38 : 60;
                // k counts the spaces between the words too, as the web's map index does
                if (fresh && !reduced) words.Reveal((nn, k) => 180 + k * step);
                else words.Finish();

                // the time left on this card
                var bars = SU.Box(card);
                bars.style.flexDirection = FlexDirection.Row;
                bars.style.marginTop = SU.Sp(7);
                for (int k = 0; k < n; k++)
                {
                    var b = SU.Box(bars);
                    b.style.height = 2;
                    b.style.width = SU.Sp(8);
                    if (k > 0) b.style.marginLeft = SU.Sp(2);
                    b.style.overflow = Overflow.Hidden;
                    SU.Radius(b, SU.Rem(0.25));
                    b.style.backgroundColor = new Color(1, 1, 1, 0.15f);
                    var fill = SU.Box(b);
                    SU.Abs(fill, 0, 0, null, 0);
                    fill.style.backgroundColor = gold;
                    fill.style.transformOrigin = new TransformOrigin(0, Length.Percent(50));
                    if (k < i) fill.style.width = Length.Percent(100);
                    else if (k == i && !reduced)
                    {
                        fill.style.width = Length.Percent(100);
                        // prologue-fill, linear, as long as the card holds (restarts with the card)
                        double hold = holdFor(text);
                        if (fresh) UiKit.Tween(fill, hold, 0, UiKit.Linear, e => fill.style.scale = new Scale(new Vector2((float)e, 1)));
                        else fill.style.scale = new Scale(new Vector2(1, 1));
                    }
                    else fill.style.width = Length.Percent(k == i ? 100 : 0);
                }

                // skip
                float ks = SU.Rem(0.7);
                var sk = new Pill(SU.Upper(S.skip, p.lang) + " ›", () => { if (p.onDone != null) p.onDone(); });
                skip = sk;
                Add(sk);
                SU.Abs(sk, null, null, SU.Sp(5), null);
                sk.style.bottom = Length.Percent(2.6f);
                SU.Radius(sk, 0);
                SU.Font(sk.label, TITLE);
                sk.label.style.fontSize = ks;
                sk.label.style.height = ks * 1.5f;
                sk.label.style.color = SU.C(CREAM);
                UiKit.SetSpacing(sk.label, SU.Track(0.35, ks, p.lang));
                sk.baseOpacity = 0.6f;
                sk.hoverOpacity = 1;
                sk.style.opacity = 0.6f;
            }
            else
            {
                titleBox = SU.Box(this);
                SU.Fill(titleBox);
                titleBox.style.flexDirection = FlexDirection.Column;
                titleBox.style.alignItems = Align.Center;
                titleBox.style.justifyContent = Justify.Center;
                titleBox.style.paddingLeft = SU.Sp(6);
                titleBox.style.paddingRight = SU.Sp(6);
                float gap = SU.Sp(6);

                float ts = SU.Rem(UiKit.Sm ? 4.4 : 2.6);
                float spacing = 0.32f * ts;
                var parts = new List<KeyValuePair<Label, int>>();
                var h1 = EndCardView.TitleRow("SISYPHUS", true, TITLE, 700, ts, spacing, SU.C(CREAM), parts);
                // .prologue-title: letter-spacing .32em, margin-right -.32em
                h1.style.marginRight = -spacing;
                titleBox.Add(h1);
                if (fresh && !reduced) EndCardView.LetterIn(parts, ts, 110);
                else foreach (var kv in parts) kv.Key.style.textShadow = new TextShadow { blurRadius = 18, color = SU.C("rgba(217,180,106,0.35)") };

                var line = SU.Box(titleBox);
                line.style.height = 1;
                line.style.marginTop = gap;
                line.style.backgroundColor = SU.C(GOLD);
                if (reduced || !fresh)
                {
                    line.style.width = reduced ? SU.Sp(48) : Math.Min(SU.Rem(22), UiKit.Vw(60));
                    line.style.opacity = 0.8f;
                }
                else EndCardView.PrologueLine(line);

                float bs = SU.Rem(0.8);
                var begin = new Pill(SU.Upper(S.begin, p.lang), () => { if (p.onDone != null) p.onDone(); });
                titleBox.Add(begin);
                begin.style.marginTop = gap + SU.Sp(4);
                SU.Font(begin.label, TITLE);
                begin.label.style.fontSize = bs;
                begin.label.style.height = bs * 1.5f;
                begin.label.style.color = SU.C(CREAM);
                UiKit.SetSpacing(begin.label, SU.Track(0.45, bs, p.lang));
                begin.SetBorder(1, SU.C("rgba(217,180,106,0.55)"));
                SU.Pad(begin, SU.Sp(10), SU.Sp(3));
                // .prologue-begin: dark glass (PORT: backdrop blur(2px) dropped)
                begin.baseBg = SU.C("rgba(0,0,0,0.25)");
                begin.hoverBg = begin.baseBg;
                begin.style.backgroundColor = begin.baseBg;
                var pulse = new Glow(begin, 26, SU.C("rgba(217,180,106,0.35)"));
                if (fresh && !reduced)
                {
                    UiKit.Tween(begin, 1000, 2000, UiKit.EaseOut, k =>
                    {
                        begin.style.opacity = (float)k;
                        begin.style.translate = new Translate(0, (float)(10 * (1 - k)));
                    });
                    UiKit.Loop(pulse, 3000, 3000, ph => pulse.level = (float)SU.PingPong(ph));
                }
                else if (!reduced) UiKit.Loop(pulse, 3000, 0, ph => pulse.level = (float)SU.PingPong(ph));
            }
        }

        // -------------------------------------------------------------------------
        //  The moving pictures behind the cards
        // -------------------------------------------------------------------------

        struct Star { public double x, y, r, p; }
        struct Ember { public double x, y, v, s, p; }

        internal static Func<double> rand(uint seed)
        {
            uint s = seed;
            return () =>
            {
                s = unchecked(s * 1664525u + 1013904223u);
                return s / 4294967296.0;
            };
        }

        sealed class Backdrop : VisualElement
        {
            public int scene;
            public bool title;
            readonly bool reduced;
            readonly Star[] stars = new Star[220];
            readonly Ember[] embers = new Ember[46];
            int shown = -1, prev = -1;
            double since;
            readonly double t0;
            readonly PaintBox box;

            public Backdrop(bool reduced)
            {
                this.reduced = reduced;
                pickingMode = PickingMode.Ignore;
                SU.Fill(this);
                var R = rand(7);
                for (int k = 0; k < stars.Length; k++)
                    stars[k] = new Star { x = R(), y = R() * 0.8, r = R() * 1.2 + 0.2, p = R() * 6 };
                for (int k = 0; k < embers.Length; k++)
                    embers[k] = new Ember { x = R(), y = R(), v = 0.012 + R() * 0.03, s = R() * 1.6 + 0.4, p = R() * 6 };
                t0 = UiKit.Now;
                box = new PaintBox(Frame);
                SU.Fill(box);
                Add(box);
                UiKit.Loop(box, 1000, 0, ph => box.Redraw());
            }

            void Frame(Ctx ctx, double w, double h)
            {
                double t = (UiKit.Now - t0) / 1000;
                int want = scene;
                if (want != shown)
                {
                    prev = shown;
                    shown = want;
                    since = t;
                }
                double k = reduced ? 1 : Math.Min(1, (t - since) / 1.6);
                // the sky, deep and cold
                var g = ctx.CreateLinearGradient(0, 0, 0, h);
                g.AddColorStop(0, "#04050a");
                g.AddColorStop(0.7, "#0c0f1a");
                g.AddColorStop(1, "#160f12");
                ctx.FillStyle = g;
                ctx.FillRect(0, 0, w, h);
                foreach (var s in stars)
                {
                    double sn = Math.Sin(t * 1.3 + s.p);
                    double a = 0.35 + 0.45 * sn * sn;
                    ctx.FillStyle = Col.Rgba(230, 226, 255, Js.round(a * 1000) / 1000);
                    ctx.FillRect(s.x * w, s.y * h, s.r, s.r);
                }
                // a slow push in, as in a film
                double zoom = reduced ? 1 : 1 + Math.Min(1, (t - since) / 10) * 0.05;
                void drawScene(int id, double alpha, double st)
                {
                    if (id < 0 || alpha <= 0.002) return;
                    ctx.Save();
                    ctx.GlobalAlpha = alpha;
                    ctx.Translate(w / 2, h / 2);
                    ctx.Scale(zoom, zoom);
                    ctx.Translate(-w / 2, -h / 2);
                    Scenes.SCENES[Math.Min(id, Scenes.SCENES.Length - 1)](ctx, w, h, st, title);
                    ctx.Restore();
                }
                drawScene(prev, 1 - k, t);
                drawScene(shown, k, t - since);
                // embers drifting up through everything
                foreach (var e in embers)
                {
                    double y = (((e.y - t * e.v) % 1) + 1) % 1;
                    double x = e.x + Math.Sin(t * 0.6 + e.p) * 0.01;
                    double a = 0.5 * Math.Sin(y * Math.PI) * (0.6 + 0.4 * Math.Sin(t * 3 + e.p));
                    ctx.FillStyle = Col.Rgba(255, 170, 90, Js.round(a * 1000) / 1000);
                    ctx.BeginPath();
                    ctx.Arc(x * w, y * h, e.s, 0, Math.PI * 2);
                    ctx.Fill();
                }
                // vignette, and the dark at the bottom the words sit on
                var v = ctx.CreateRadialGradient(w / 2, h * 0.45, h * 0.25, w / 2, h * 0.5, Math.Max(w, h) * 0.75);
                v.AddColorStop(0, "rgba(0,0,0,0)");
                v.AddColorStop(1, "rgba(0,0,0,0.85)");
                ctx.FillStyle = v;
                ctx.FillRect(0, 0, w, h);
                var b = ctx.CreateLinearGradient(0, h * 0.5, 0, h);
                b.AddColorStop(0, "rgba(0,0,0,0)");
                b.AddColorStop(1, "rgba(0,0,0,0.7)");
                ctx.FillStyle = b;
                ctx.FillRect(0, h * 0.5, w, h * 0.5);
            }
        }

        internal delegate void SceneFn(Ctx c, double w, double h, double t, bool title);

        internal static class Scenes
        {
            /// <summary>`(x).toFixed(3)` of an alpha</summary>
            static double f3(double x) { return Js.round(x * 1000) / 1000; }

            /// <summary>a capsule (roundRect is missing before Safari 16)</summary>
            static void pill(Ctx c, double x, double y, double w, double h)
            {
                double r = Math.Min(h, w) / 2;
                c.MoveTo(x + r, y);
                c.LineTo(x + w - r, y);
                c.Arc(x + w - r, y + r, r, -Math.PI / 2, Math.PI / 2);
                c.LineTo(x + r, y + h);
                c.Arc(x + r, y + r, r, Math.PI / 2, (Math.PI * 3) / 2);
                c.ClosePath();
            }

            /// <summary>I — Corinth on its hill over the sea, at night, a few torches still burning</summary>
            static void corinth(Ctx c, double w, double h, double t, bool title)
            {
                double hz = h * 0.62;
                // the moon and its road on the water
                double mx = w * 0.74;
                double my = h * 0.2;
                var mg = c.CreateRadialGradient(mx, my, 0, mx, my, h * 0.22);
                mg.AddColorStop(0, "rgba(230, 226, 210, 0.35)");
                mg.AddColorStop(1, "rgba(230, 226, 210, 0)");
                c.FillStyle = mg;
                c.FillRect(mx - h * 0.22, my - h * 0.22, h * 0.44, h * 0.44);
                c.FillStyle = "#e8e2cf";
                c.BeginPath();
                c.Arc(mx, my, h * 0.035, 0, Math.PI * 2);
                c.Fill();
                // the sea
                var sea = c.CreateLinearGradient(0, hz, 0, h);
                sea.AddColorStop(0, "#111827");
                sea.AddColorStop(1, "#05070c");
                c.FillStyle = sea;
                c.FillRect(0, hz, w, h - hz);
                for (int i = 0; i < 26; i++)
                {
                    double yy = hz + 4 + i * i * 0.5;
                    double wob = Math.Sin(t * 1.4 + i * 1.7) * 6;
                    double len = 10 + i * 3;
                    c.FillStyle = Col.Rgba(232, 226, 205, f3(0.5 - i * 0.017));
                    c.FillRect(mx - len / 2 + wob, yy, len, 1.2);
                }
                // the acropolis: a rock, walls, a temple, roofs
                c.FillStyle = "#07080d";
                c.BeginPath();
                c.MoveTo(0, hz + 10);
                c.LineTo(w * 0.08, hz - h * 0.04);
                c.QuadraticCurveTo(w * 0.2, hz - h * 0.2, w * 0.3, hz - h * 0.22);
                c.LineTo(w * 0.42, hz - h * 0.2);
                c.QuadraticCurveTo(w * 0.52, hz - h * 0.08, w * 0.62, hz + 6);
                c.LineTo(w * 0.62, hz + 12);
                c.LineTo(0, hz + 12);
                c.Fill();
                double top = hz - h * 0.22;
                // the temple: a pediment on columns
                double tx = w * 0.33;
                double tw = w * 0.1;
                c.BeginPath();
                c.MoveTo(tx - tw / 2, top - h * 0.055);
                c.LineTo(tx, top - h * 0.085);
                c.LineTo(tx + tw / 2, top - h * 0.055);
                c.Fill();
                c.FillRect(tx - tw / 2, top - h * 0.056, tw, h * 0.008);
                for (int i = 0; i < 7; i++)
                    c.FillRect(tx - tw / 2 + 2 + (i * (tw - 6)) / 6, top - h * 0.05, 3, h * 0.05);
                // houses down the slope, and torches flickering in a few of them
                var R = rand(11);
                for (int i = 0; i < 24; i++)
                {
                    double hx = w * (0.06 + R() * 0.5);
                    double slope =
                        hx < w * 0.3
                            ? (hx / (w * 0.3)) * 0.2
                            : Math.Max(0, 0.2 - ((hx - w * 0.36) / (w * 0.26)) * 0.2);
                    double hy = hz - h * slope + h * 0.02 + R() * h * 0.03;
                    double bw = 10 + R() * 18;
                    double bh = 8 + R() * 12;
                    c.FillStyle = "#07080d";
                    c.FillRect(hx, hy - bh, bw, bh + 6);
                    if (R() < 0.45)
                    {
                        double f = 0.55 + 0.45 * Math.Sin(t * (5 + R() * 4) + i);
                        c.FillStyle = Col.Rgba(255, 170, 80, f3(0.55 * f));
                        c.FillRect(hx + bw * 0.4, hy - bh * 0.6, 3, 4);
                        var gl = c.CreateRadialGradient(hx + bw * 0.4, hy - bh * 0.5, 0, hx + bw * 0.4, hy - bh * 0.5, 16);
                        gl.AddColorStop(0, Col.Rgba(255, 150, 60, f3(0.2 * f)));
                        gl.AddColorStop(1, "rgba(255, 150, 60, 0)");
                        c.FillStyle = gl;
                        c.FillRect(hx + bw * 0.4 - 16, hy - bh * 0.5 - 16, 32, 32);
                    }
                }
            }

            /// <summary>II — a chain across the dark; its middle link is breaking</summary>
            static void chain(Ctx c, double w, double h, double t, bool title)
            {
                double y = h * 0.4 + Math.Sin(t * 0.6) * 4;
                double open = Math.Min(1, t / 6);
                double gap = open * 26;
                double linkW = Math.Max(34, w * 0.045);
                int n = (int)Math.Ceiling(w / (linkW * 0.78)) + 2;
                int mid = n / 2;
                for (int i = 0; i < n; i++)
                {
                    int side = i < mid ? -1 : i > mid ? 1 : 0;
                    double x = (i - mid) * linkW * 0.78 + w / 2 + side * gap;
                    double sag = Math.Pow((x - w / 2) / (w / 2), 2) * -18 + 18;
                    double ly = y + sag + side * open * 6 * Math.Pow(Math.Abs(i - mid), 0.5);
                    c.Save();
                    c.Translate(x, ly);
                    c.Rotate(side * open * 0.08);
                    var g = c.CreateLinearGradient(0, -linkW * 0.3, 0, linkW * 0.3);
                    g.AddColorStop(0, "#6b6762");
                    g.AddColorStop(0.5, "#2a2826");
                    g.AddColorStop(1, "#141312");
                    if (i % 2 == 0)
                    {
                        c.StrokeStyle = g;
                        c.LineWidth = linkW * 0.16;
                        c.BeginPath();
                        c.Ellipse(0, 0, linkW * 0.5, linkW * 0.26, 0, 0, Math.PI * 2);
                        c.Stroke();
                        c.StrokeStyle = "rgba(255, 230, 200, 0.18)";
                        c.LineWidth = 1.2;
                        c.BeginPath();
                        c.Ellipse(0, -1, linkW * 0.5, linkW * 0.26, 0, Math.PI * 1.15, Math.PI * 1.85);
                        c.Stroke();
                    }
                    else
                    {
                        c.FillStyle = g;
                        c.BeginPath();
                        pill(c, -linkW * 0.5, -linkW * 0.07, linkW, linkW * 0.14);
                        c.Fill();
                    }
                    c.Restore();
                }
                // where it breaks: heat in the iron, and sparks
                double hx = w / 2;
                var heat = c.CreateRadialGradient(hx, y + 18, 0, hx, y + 18, 70);
                heat.AddColorStop(0, Col.Rgba(255, 120, 40, f3(0.35 * (0.6 + 0.4 * Math.Sin(t * 9)))));
                heat.AddColorStop(1, "rgba(255, 120, 40, 0)");
                c.FillStyle = heat;
                c.FillRect(hx - 70, y + 18 - 70, 140, 140);
                for (int i = 0; i < 24; i++)
                {
                    double p = (((t * 0.9 + i * 0.137) % 1) + 1) % 1;
                    double a = i * 2.39;
                    double r = p * (60 + (i % 5) * 18);
                    double sx = hx + Math.Cos(a) * r;
                    double sy = y + 18 + Math.Sin(a) * r * 0.6 + p * p * 60;
                    c.FillStyle = Col.Rgba(255, 190 - p * 100, 90, f3(1 - p));
                    c.FillRect(sx, sy, 2, 2);
                }
            }

            static readonly double[,] PTS =
            {
                { -0.55, -0.1, 1 },
                { -0.2, -0.42, 0.9 },
                { 0.18, -0.3, 1 },
                { 0.5, -0.05, 0.85 },
                { 0.28, 0.3, 0.95 },
                { -0.12, 0.18, 0.9 },
                { -0.42, 0.4, 0.34 }, // Merope, the faint one
            };
            static readonly int[] ORDER = { 0, 1, 2, 3, 4, 5, 0, 5, 6 };

            /// <summary>III — the seven sisters; one of them faint</summary>
            static void pleiades(Ctx c, double w, double h, double t, bool title)
            {
                double cx = w / 2;
                double cy = h * 0.36;
                double S = Math.Min(w, h) * 0.32;
                double rot = t * 0.02;
                var P = new (double x, double y, double b)[7];
                for (int i = 0; i < 7; i++)
                {
                    double x = PTS[i, 0], y = PTS[i, 1];
                    double xr = x * Math.Cos(rot) - y * Math.Sin(rot);
                    double yr = x * Math.Sin(rot) + y * Math.Cos(rot);
                    P[i] = (cx + xr * S, cy + yr * S, PTS[i, 2]);
                }
                // the line of the constellation, drawn in over the first seconds
                double draw = Math.Min(1, t / 3.5);
                c.StrokeStyle = "rgba(200, 210, 255, 0.28)";
                c.LineWidth = 1;
                c.BeginPath();
                int segs = ORDER.Length - 1;
                for (int i = 0; i < segs; i++)
                {
                    double k = Math.Max(0, Math.Min(1, draw * segs - i));
                    if (k <= 0) break;
                    var a = P[ORDER[i]];
                    var b = P[ORDER[i + 1]];
                    c.MoveTo(a.x, a.y);
                    c.LineTo(a.x + (b.x - a.x) * k, a.y + (b.y - a.y) * k);
                }
                c.Stroke();
                for (int i = 0; i < 7; i++)
                {
                    double x = P[i].x, y = P[i].y, b = P[i].b;
                    double tw = i == 6 ? 0.5 + 0.5 * Math.Sin(t * 2.2) : 0.85 + 0.15 * Math.Sin(t * 1.5 + i);
                    double r = 26 * b * tw;
                    var g = c.CreateRadialGradient(x, y, 0, x, y, r);
                    g.AddColorStop(0, Col.Rgba(235, 240, 255, f3(0.95 * b * tw)));
                    g.AddColorStop(0.2, Col.Rgba(170, 195, 255, f3(0.5 * b * tw)));
                    g.AddColorStop(1, "rgba(120, 150, 255, 0)");
                    c.FillStyle = g;
                    c.FillRect(x - r, y - r, r * 2, r * 2);
                    // the cross of light on the bright ones
                    if (b > 0.5)
                    {
                        c.StrokeStyle = Col.Rgba(220, 230, 255, f3(0.35 * tw));
                        c.BeginPath();
                        c.MoveTo(x - r * 1.2, y);
                        c.LineTo(x + r * 1.2, y);
                        c.MoveTo(x, y - r * 1.2);
                        c.LineTo(x, y + r * 1.2);
                        c.Stroke();
                    }
                }
                // a soft nebula behind them
                var nb = c.CreateRadialGradient(cx, cy, 0, cx, cy, S * 1.3);
                nb.AddColorStop(0, "rgba(90, 110, 200, 0.12)");
                nb.AddColorStop(1, "rgba(90, 110, 200, 0)");
                c.FillStyle = nb;
                c.FillRect(cx - S * 1.3, cy - S * 1.3, S * 2.6, S * 2.6);
            }

            /// <summary>IV — the hill, the man, the stone; up, and back down</summary>
            internal static void hill(Ctx c, double w, double h, double t, bool title)
            {
                // a red dawn behind the mountain
                var dawn = c.CreateRadialGradient(w * 0.7, h * 0.62, 0, w * 0.7, h * 0.62, w * 0.6);
                dawn.AddColorStop(0, "rgba(200, 90, 50, 0.45)");
                dawn.AddColorStop(0.5, "rgba(120, 40, 50, 0.18)");
                dawn.AddColorStop(1, "rgba(60, 20, 40, 0)");
                c.FillStyle = dawn;
                c.FillRect(0, 0, w, h);
                // the slope
                double x0 = -w * 0.05;
                double y0 = h * 0.92;
                double x1 = w * 0.78;
                double y1 = h * 0.3;
                c.FillStyle = "#060508";
                c.BeginPath();
                c.MoveTo(x0, h);
                c.LineTo(x0, y0);
                c.LineTo(x1, y1);
                c.QuadraticCurveTo(x1 + w * 0.06, y1 - h * 0.02, x1 + w * 0.12, y1 + h * 0.04);
                c.LineTo(w * 1.05, h * 0.6);
                c.LineTo(w * 1.05, h);
                c.Fill();
                // the climb: eleven seconds up, two and a half down, again
                double cyc = 13.5;
                double ph = (t % cyc) / cyc;
                double up = 11 / cyc;
                double u;
                bool roll = false;
                if (ph < up) u = 0.12 + (ph / up) * 0.8;
                else
                {
                    roll = true;
                    double k = (ph - up) / (1 - up);
                    u = 0.92 - k * k * 0.8;
                }
                (double x, double y) along(double k) { return (x0 + (x1 - x0) * k, y0 + (y1 - y0) * k); }
                double ang = Math.Atan2(y1 - y0, x1 - x0);
                double R = Math.Max(10, h * 0.028);
                var bxy = along(u);
                double bx = bxy.x, by = bxy.y;
                double nx = Math.Sin(ang);
                double ny = -Math.Cos(ang);
                // the stone
                c.Save();
                c.Translate(bx + nx * R, by + ny * R);
                c.Rotate((u * Js.hypot(x1 - x0, y1 - y0)) / R);
                c.FillStyle = "#0b0a0c";
                c.BeginPath();
                for (int i = 0; i < 9; i++)
                {
                    double a = (i / 9.0) * Math.PI * 2;
                    double rr = R * (0.9 + ((i * 37) % 7) / 50.0);
                    if (i != 0) c.LineTo(Math.Cos(a) * rr, Math.Sin(a) * rr);
                    else c.MoveTo(rr, 0);
                }
                c.ClosePath();
                c.Fill();
                c.Restore();
                // the man: behind the stone, leaning into it — or standing near the top, watching it go
                double off = (R * 1.5) / Js.hypot(x1 - x0, y1 - y0);
                var man = along(roll ? 0.92 - off : u - off);
                c.Save();
                c.Translate(man.x, man.y);
                c.Rotate(ang);
                c.FillStyle = "#0b0a0c";
                c.StrokeStyle = "#0b0a0c";
                c.LineCap = LineCap.Round;
                double lean = roll ? -0.35 : 0.75; // forward into the stone, or upright
                double step = roll ? 0 : Math.Sin(t * 4) * 0.35;
                double H = R * 2.6;
                c.Rotate(-lean * 0.6);
                c.LineWidth = R * 0.32;
                c.BeginPath();
                c.MoveTo(0, -H * 0.45); // the legs
                c.LineTo(-R * 0.5 + step * R, 0);
                c.MoveTo(0, -H * 0.45);
                c.LineTo(R * 0.4 - step * R, 0);
                c.Stroke();
                c.LineWidth = R * 0.42;
                c.BeginPath();
                c.MoveTo(0, -H * 0.45);
                c.LineTo(R * 0.3, -H * 0.9); // the body
                c.Stroke();
                c.BeginPath();
                c.Arc(R * 0.42, -H * 1.05, R * 0.24, 0, Math.PI * 2);
                c.Fill();
                c.LineWidth = R * 0.2;
                c.BeginPath();
                c.MoveTo(R * 0.3, -H * 0.85); // the arms, to the stone
                c.LineTo(roll ? R * 0.4 : R * 1.05, roll ? -H * 0.5 : -H * 0.78);
                c.Stroke();
                c.Restore();
                // dust where it rolls
                if (roll)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        double k = i / 10.0;
                        var d = along(Math.Min(0.92, u + k * 0.06));
                        c.FillStyle = Col.Rgba(170, 120, 90, f3(0.25 * (1 - k)));
                        c.BeginPath();
                        c.Arc(d.x + nx * 2, d.y + ny * 2 - k * 6, 3 + k * 8, 0, Math.PI * 2);
                        c.Fill();
                    }
                }
            }

            public static readonly SceneFn[] SCENES = { corinth, chain, pleiades, hill };
        }
    }

    /// <summary>
    /// The hill in miniature, for the menu (HillEmblem): a man pushes his stone up a slope
    /// under a red dawn, and it rolls back, and he goes down after it. The same picture the
    /// opening ends on, so the menu picks up where the story left off. Give it a size
    /// (the menu's is 230×110); it draws nothing while it has none.
    /// </summary>
    public sealed class HillEmblemView : VisualElement
    {
        // PORT: prefers-reduced-motion has no counterpart on the phones
        readonly bool still = false;

        public HillEmblemView()
        {
            pickingMode = PickingMode.Ignore;
            double t0 = UiKit.Now;
            PaintBox box = null;
            box = new PaintBox((ctx, w, h) =>
            {
                // hidden (a short screen): do not draw
                if (w <= 0 || h <= 0) return;
                ctx.ClearRect(0, 0, w, h);
                // a low sun behind the hill, so the man and his stone stand out against it
                var sun = ctx.CreateRadialGradient(w * 0.6, h * 0.42, 0, w * 0.6, h * 0.42, w * 0.42);
                sun.AddColorStop(0, "rgba(255, 196, 120, 0.85)");
                sun.AddColorStop(0.25, "rgba(230, 120, 70, 0.55)");
                sun.AddColorStop(1, "rgba(120, 40, 50, 0)");
                ctx.FillStyle = sun;
                ctx.FillRect(0, 0, w, h);
                PrologueView.Scenes.hill(ctx, w, h, still ? 4 : (UiKit.Now - t0) / 1000, false);
                // the crest of the slope, catching the light
                ctx.StrokeStyle = "rgba(240, 200, 130, 0.7)";
                ctx.LineWidth = 1;
                ctx.BeginPath();
                ctx.MoveTo(-w * 0.05, h * 0.92);
                ctx.LineTo(w * 0.78, h * 0.3);
                ctx.Stroke();
                // fade the picture out at its edges so it floats on the menu
                ctx.GlobalCompositeOperation = Composite.DestinationIn;
                var m = ctx.CreateRadialGradient(w / 2, h * 0.6, h * 0.2, w / 2, h * 0.6, w * 0.55);
                m.AddColorStop(0, "rgba(0,0,0,1)");
                m.AddColorStop(1, "rgba(0,0,0,0)");
                ctx.FillStyle = m;
                ctx.FillRect(0, 0, w, h);
                ctx.GlobalCompositeOperation = Composite.SourceOver;
            });
            SU.Fill(box);
            Add(box);
            if (!still) UiKit.Loop(box, 1000, 0, ph => box.Redraw());
        }
    }
}
