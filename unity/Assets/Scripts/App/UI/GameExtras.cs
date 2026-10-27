// Port of src/components/GameExtras.tsx to UI Toolkit: the pieces around the game that are not the
// game — the settings panel, the summit's moment with a new page, the seals, the saying of the day,
// and sharing a saying as a picture.
//
// React → UI Toolkit: each component is a VisualElement built in its constructor; props that change
// go through Update(...). A React `key` remount is a new instance. Tailwind classes are written out
// as the CSS px they compute to (1 UI unit = 1 CSS px, rem = UiKit.Rem).
using System;
using System.Collections.Generic;
using Sisifos.Game;
using Sisifos.Paint;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sisifos.App.UI
{
    internal static class GX
    {
        public const string SERIF = UiKit.SERIF;
        public static readonly Color INK = UiKit.C("#2a1c0f");
        public static readonly Color SEPIA = UiKit.C("#6b4a22");
        public static readonly Color GOLD = UiKit.C("#c9a15a");
        public static readonly Color WAX = UiKit.C("#8e2b1e");
        public static readonly Color CREAM = UiKit.C("#f1e4c4");
        public static readonly Color PALE = UiKit.C("#e9d3a4");
        public static readonly Color PAGE = UiKit.C("#f6ead0");

        /// <summary>a clickable element (button / role=button)</summary>
        public static void OnClick(VisualElement e, Action fn)
        {
            e.pickingMode = PickingMode.Position;
            e.RegisterCallback<ClickEvent>(ev =>
            {
                ev.StopPropagation();
                fn?.Invoke();
            });
        }

        /// <summary>hover:bg-… for a mouse (no effect on touch)</summary>
        public static void Hover(VisualElement e, Color idle, Color hover)
        {
            e.style.backgroundColor = idle;
            e.RegisterCallback<PointerEnterEvent>(ev => { if (ev.pointerType == "mouse") e.style.backgroundColor = hover; });
            e.RegisterCallback<PointerLeaveEvent>(ev => e.style.backgroundColor = idle);
        }

        /// <summary>hover:text-… for a mouse</summary>
        public static void HoverText(VisualElement e, Action<bool> set)
        {
            e.RegisterCallback<PointerEnterEvent>(ev => { if (ev.pointerType == "mouse") set(true); });
            e.RegisterCallback<PointerLeaveEvent>(ev => set(false));
        }

        /// <summary>A rounded-full outlined pill button with small spaced capitals (the GameExtras "btn").</summary>
        public static VisualElement Pill(string text, string lang, Color border, Color color, Action onClick, float padX = 16, float padY = 6, float size = -1, double tracking = 0.28)
        {
            if (size < 0) size = UiKit.Rem(0.62);
            var b = new VisualElement();
            b.style.flexDirection = FlexDirection.Row;
            b.style.alignItems = Align.Center;
            b.style.alignSelf = Align.FlexStart;
            UiKit.Pad(b, padX, padY);
            UiKit.Radius(b, 9999);
            UiKit.BorderW(b, 1);
            UiKit.BorderC(b, border);
            var l = UiKit.Text(text, SERIF, 600, false, size, color, tracking, lang, true);
            b.Add(l);
            Hover(b, new Color(0, 0, 0, 0), new Color(1, 1, 1, 0.1f));
            if (onClick != null) OnClick(b, onClick);
            return b;
        }

        public static void Gap(VisualElement row, float gap, bool vertical = false)
        {
            // flex gap: a margin on every child but the first (UI Toolkit 6.0 has no gap)
            int i = 0;
            foreach (var c in row.Children())
            {
                if (vertical) c.style.marginTop = i == 0 ? 0 : gap;
                else
                {
                    bool rtl = row.style.flexDirection.value == FlexDirection.RowReverse;
                    if (rtl) c.style.marginRight = i == 0 ? 0 : gap;
                    else c.style.marginLeft = i == 0 ? 0 : gap;
                }
                i++;
            }
        }

        /// <summary>a paper card's background: the PAPER gradients, its drop shadow, the inset ring</summary>
        public static Action<Ctx, double, double> PaperBox(double radius, double sy, double blur, double spread, double alpha)
        {
            return (c, w, h) =>
            {
                UiKit.BoxShadow(c, w, h, radius, 0, sy, blur, spread, Col.Rgba(0, 0, 0, alpha));
                c.Save();
                UiKit.RoundRect(c, 0, 0, w, h, radius);
                c.Clip();
                UiKit.PaintPaper(c, w, h);
                c.Restore();
                UiKit.InsetRing(c, w, h, radius, 1, Col.Rgba(107, 74, 34, 0.3));
            };
        }
    }

    /// <summary>a wax seal; dull and unpressed until earned</summary>
    public sealed class SealMark : CanvasBox
    {
        const string RIM = "M16 1.5c2 0 2.6 1.6 4.3 2.1 1.8.5 3.3-.4 4.7.8 1.3 1.2.8 2.9 1.5 4.6.7 1.6 2.3 2.4 2.3 4.4s-1.6 2.6-2 4.3c-.5 1.8.4 3.3-.8 4.7-1.2 1.3-2.9.8-4.6 1.5-1.6.7-2.4 2.3-4.4 2.3s-2.7-1.6-4.4-2.2c-1.8-.5-3.3.4-4.6-.8-1.3-1.2-.8-2.9-1.5-4.6C3.8 17.9 2.2 17.2 2.2 15.2s1.6-2.7 2.1-4.4c.5-1.8-.4-3.3.8-4.6C6.3 4.9 8 5.4 9.7 4.7 11.3 4 12 1.5 16 1.5z";
        const string STAR = "M16 10.2l1.6 3.4 3.7.4-2.8 2.5.8 3.6-3.3-1.9-3.3 1.9.8-3.6-2.8-2.5 3.7-.4z";

        bool earned;

        public SealMark(bool earned, float size = 34) : base(null)
        {
            this.earned = earned;
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            SetDraw(Paint);
        }

        public void SetEarned(bool on) { if (on != earned) { earned = on; Redraw(); } }

        void Paint(Ctx c, double w, double h)
        {
            c.Scale(w / 32, h / 32);
            c.BeginPath();
            UiKit.Svg(c, RIM);
            c.FillStyle = earned ? Col.Parse("#8e2b1e") : Col.Rgba(241, 228, 196, 0.08);
            c.Fill();
            c.StrokeStyle = earned ? Col.Rgba(0, 0, 0, 0.25) : Col.Rgba(241, 228, 196, 0.25);
            c.LineWidth = 0.8;
            c.Stroke();
            c.BeginPath();
            c.Arc(16, 15.6, 8.2, 0, Math.PI * 2);
            c.StrokeStyle = earned ? Col.Rgba(0xe8, 0xc9, 0xa0, 0.8) : Col.Rgba(241, 228, 196, 0.2);
            c.LineWidth = 0.7;
            c.Stroke();
            c.BeginPath();
            UiKit.Svg(c, STAR);
            c.FillStyle = earned ? Col.Parse("#e8c9a0") : Col.Rgba(241, 228, 196, 0.18);
            c.Fill();
        }
    }

    public static class Icons
    {
        /// <summary>the share arrow (13 × 13, viewBox 16)</summary>
        public static SvgIcon Share(Color color)
        {
            return new SvgIcon(13, 13, 16, (c, col) =>
            {
                c.StrokeStyle = col;
                c.LineWidth = 1.3;
                c.BeginPath();
                UiKit.Svg(c, "M8 1.5v9M4.5 5L8 1.5 11.5 5");
                c.Stroke();
                c.BeginPath();
                UiKit.Svg(c, "M3 8.5v5h10v-5");
                c.Stroke();
            }) { color = color };
        }

        /// <summary>a closed book, drawn small enough to sit in the HUD (13 × 13)</summary>
        public static SvgIcon Book(Color color)
        {
            return new SvgIcon(13, 13, 16, (c, col) =>
            {
                c.StrokeStyle = col;
                c.LineWidth = 1.2;
                c.BeginPath();
                UiKit.Svg(c, "M3 2.5h7.5a2 2 0 0 1 2 2V14H5a2 2 0 0 1-2-2V2.5z");
                c.Stroke();
                c.BeginPath();
                UiKit.Svg(c, "M3 12a2 2 0 0 1 2-2h7.5");
                c.Stroke();
                c.LineCap = LineCap.Round;
                c.BeginPath();
                UiKit.Svg(c, "M6 5.5h4");
                c.Stroke();
            }) { color = color };
        }

        /// <summary>settings: a small cog (14 × 14)</summary>
        public static SvgIcon Gear(Color color)
        {
            return new SvgIcon(14, 14, 16, (c, col) =>
            {
                c.StrokeStyle = col;
                c.LineWidth = 1.2;
                c.BeginPath();
                c.Arc(8, 8, 2.3, 0, Math.PI * 2);
                c.Stroke();
                c.LineCap = LineCap.Round;
                c.BeginPath();
                UiKit.Svg(c, "M8 1.6v1.6M8 12.8v1.6M1.6 8h1.6M12.8 8h1.6M3.5 3.5l1.1 1.1M11.4 11.4l1.1 1.1M3.5 12.5l1.1-1.1M11.4 4.6l1.1-1.1");
                c.Stroke();
                c.LineCap = LineCap.Butt;
                c.BeginPath();
                c.Arc(8, 8, 4.6, 0, Math.PI * 2);
                c.Stroke();
            }) { color = color };
        }

        /// <summary>the settings panel's close cross (12 × 12)</summary>
        public static SvgIcon Close(Color color)
        {
            return new SvgIcon(12, 12, 12, (c, col) =>
            {
                c.StrokeStyle = col;
                c.LineWidth = 1.4;
                c.BeginPath();
                UiKit.Svg(c, "M1 1 L11 11 M11 1 L1 11");
                c.Stroke();
            }) { color = color };
        }

        /// <summary>the native select's chevron</summary>
        public static SvgIcon Chevron(Color color)
        {
            return new SvgIcon(10, 10, 10, (c, col) =>
            {
                c.StrokeStyle = col;
                c.LineWidth = 1.6;
                c.LineCap = LineCap.Round;
                c.LineJoin = LineJoin.Round;
                c.BeginPath();
                UiKit.Svg(c, "M1.5 3.5L5 7l3.5-3.5");
                c.Stroke();
            }) { color = color };
        }
    }

    public sealed class ShareButtonProps
    {
        public int index;
        public string lang;
        public UIStrings t;
        /// <summary>border colour (Tailwind's default border is the theme border)</summary>
        public Color border = UiKit.Border;
        /// <summary>text and icon colour</summary>
        public Color color = UiKit.Foreground;
        /// <summary>hover:bg-…</summary>
        public Color hoverBg = new Color(1, 1, 1, 0.1f);
        public float padX = 16, padY = 6;
    }

    /// <summary>a button that turns a saying into a picture and shares it</summary>
    public sealed class ShareButton : VisualElement
    {
        bool busy;

        public ShareButton(ShareButtonProps p)
        {
            UiKit.Row(this, p.lang);
            style.alignItems = Align.Center;
            style.alignSelf = Align.Center;
            UiKit.Pad(this, p.padX, p.padY);
            UiKit.Radius(this, 9999);
            UiKit.BorderW(this, 1);
            UiKit.BorderC(this, p.border);
            var icon = Icons.Share(p.color);
            Add(icon);
            // "<ShareIcon /> {t.share}": the space between them, then gap-1.5
            var l = UiKit.Text(p.t.share, GX.SERIF, 600, false, UiKit.Rem(0.62), p.color, 0.28, p.lang, true);
            Add(l);
            float space = UiKit.Rem(0.62) * 0.25f;
            if (p.lang == "ar") l.style.marginRight = 6 + space; else l.style.marginLeft = 6 + space;
            GX.Hover(this, new Color(0, 0, 0, 0), p.hoverBg);
            GX.OnClick(this, () =>
            {
                var q = Quotes.quotesFor(p.lang);
                if (p.index < 0 || p.index >= q.Count || busy) return;
                busy = true;
                style.opacity = 0.5f;
                // the click's frame is drawn first; the card is drawn and shared on the next
                schedule.Execute(() =>
                {
                    try
                    {
                        var quote = q[p.index];
                        var chapters = BookText.CHAPTER_NAMES[p.lang];
                        int ch = p.index < Quotes.CHAPTER_OF.Length ? Quotes.CHAPTER_OF[p.index] : 0;
                        string chapter = ch < chapters.Length ? chapters[ch] ?? "" : "";
                        var png = Share.renderShareCard(quote.text, quote.author, chapter, p.lang == "ar");
                        Share.shareCard(png, quote.text + " — " + quote.author);
                    }
                    catch (Exception e)
                    {
                        /* the share sheet was dismissed or is unavailable */
                        Debug.LogException(e);
                    }
                    finally
                    {
                        busy = false;
                        style.opacity = 1;
                    }
                });
            });
        }
    }

    /// <summary>
    /// The moment a page is found. When the walk down begins the new saying stands in the middle
    /// of the frame, large, for a few breaths — then it folds away toward the book in the corner.
    /// </summary>
    public sealed class SummitCeremony : VisualElement
    {
        static readonly Func<double, double> leaveEase = UiKit.Bezier(0.55, 0, 0.8, 0.4);

        public SummitCeremony(int index, string lang, UIStrings t, int count, int total, Action onOpen, Action onDone)
        {
            var list = Quotes.quotesFor(lang);
            UiKit.Fill(this);
            pickingMode = PickingMode.Ignore;
            if (index < 0 || index >= list.Count) { style.display = DisplayStyle.None; return; }
            var q = list[index];
            var names = BookText.CHAPTER_NAMES[lang];
            int ci = index < Quotes.CHAPTER_OF.Length ? Quotes.CHAPTER_OF[index] : 0;
            string chapter = ci < names.Length ? names[ci] : "";

            // radial-gradient(ellipse at center, rgba(8,6,10,0.62) 0%, rgba(8,6,10,0) 70%)
            var bg = new CanvasBox((c, w, h) => UiKit.CssRadial(c, w, h, false, w / 2, h / 2, (0, Col.Rgba(8, 6, 10, 0.62)), (0.7, Col.Rgba(8, 6, 10, 0)), (1, Col.Rgba(8, 6, 10, 0))));
            bg.pickingMode = PickingMode.Ignore;
            UiKit.Fill(bg);
            bg.style.flexDirection = FlexDirection.Column;
            bg.style.alignItems = Align.Center;
            bg.style.justifyContent = Justify.Center;
            bg.style.paddingLeft = 32;
            bg.style.paddingRight = 32;
            Add(bg);

            var card = new VisualElement();
            card.style.maxWidth = UiKit.Rem(42);
            card.style.alignItems = Align.Center;
            UiKit.Dir(card, lang);
            GX.OnClick(card, onOpen);
            bg.Add(card);

            var head = UiKit.Text(t.newPage + " · № " + (index + 1) + " · " + chapter, GX.SERIF, 600, false, UiKit.Rem(0.65), GX.GOLD, 0.4, lang, true);
            head.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(head);

            float qs = UiKit.MaxHeight(500) ? UiKit.Rem(1.45) : UiKit.Sm ? UiKit.Rem(2.3) : UiKit.Rem(1.7);
            var text = UiKit.Text(q.text, GX.SERIF, 500, true, qs, GX.PAGE, 0, lang, false, 1.375);
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 16;
            text.style.textShadow = new TextShadow { offset = new Vector2(0, 2), blurRadius = 18, color = new Color(0, 0, 0, 0.65f) };
            card.Add(text);

            var author = UiKit.Text(q.author, GX.SERIF, 600, false, UiKit.Rem(0.78), GX.PALE, 0.32, lang, true);
            author.style.unityTextAlign = TextAnchor.MiddleCenter;
            author.style.marginTop = 16;
            card.Add(author);

            if (!string.IsNullOrEmpty(q.note))
            {
                var note = UiKit.Text(q.note, GX.SERIF, 400, true, UiKit.Rem(0.8), UiKit.C("rgba(233,211,164,0.7)"), 0, lang);
                note.style.unityTextAlign = TextAnchor.MiddleCenter;
                note.style.marginTop = 4;
                note.style.maxWidth = UiKit.Rem(28);
                card.Add(note);
            }

            var sub = UiKit.Text(t.newPageSub + " · " + count + "/" + total, UiKit.SANS, 400, false, UiKit.Rem(0.6), UiKit.A(GX.PALE, 0.6), 0.25, lang, true);
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            sub.style.marginTop = 12;
            card.Add(sub);

            var share = new ShareButton(new ShareButtonProps { index = index, lang = lang, t = t, border = UiKit.A(GX.PALE, 0.4), color = GX.PALE });
            share.style.marginTop = 16;
            card.Add(share);

            // ceremony-enter: 0.9s cubic-bezier(0.2, 0.7, 0.2, 1) (the blur(3px) has no counterpart)
            UiKit.Tween(this, 900, 0, UiKit.Soft, k =>
            {
                style.opacity = (float)k;
                UiKit.Xf(this, 0, (float)(14 * (1 - k)), (float)(0.98 + 0.02 * k));
            });
            UiKit.After(this, 4200, () =>
            {
                // ceremony-leave: toward the book in the corner
                UiKit.Tween(this, 800, 0, leaveEase, k =>
                {
                    style.opacity = (float)(1 - k);
                    UiKit.Xf(this, (float)(UiKit.Vw(32) * k), (float)(-UiKit.Vh(38) * k), (float)(1 - 0.85 * k));
                });
            });
            UiKit.After(this, 5000, () => onDone?.Invoke());
        }
    }

    /// <summary>sisyphus-page-in: a page dropping in, turning, like paper landing</summary>
    internal static class PageIn
    {
        static readonly Func<double, double> ease = UiKit.Bezier(0.2, 0.8, 0.2, 1);

        public static void Play(VisualElement e)
        {
            UiKit.Tween(e, 700, 0, UiKit.Linear, t =>
            {
                double op = UiKit.Seg(t, ease, (0, 0), (0.6, 1), (1, 1));
                double ty = UiKit.Seg(t, ease, (0, -14), (0.6, 2), (1, 0));
                double rot = UiKit.Seg(t, ease, (0, -6), (0.6, 1.5), (1, -1));
                double sc = UiKit.Seg(t, ease, (0, 0.92), (0.6, 1.01), (1, 1));
                e.style.opacity = (float)op;
                UiKit.Xf(e, 0, (float)ty, (float)sc, (float)rot);
            });
        }
    }

    /// <summary>the toasts at the top of the frame (a seal, a chapter): a slip of paper</summary>
    internal sealed class PaperToast : VisualElement
    {
        public readonly CanvasBox box;

        public PaperToast()
        {
            // absolute top-16 left-1/2 -translate-x-1/2
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.top = 64;
            style.alignItems = Align.Center;
            box = new CanvasBox(GX.PaperBox(3, 18, 30, -12, 0.75), 48);
            box.pickingMode = PickingMode.Ignore;
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            UiKit.Pad(box, 16, 10);
            Add(box);
            PageIn.Play(box);
        }

        public void Lines(VisualElement mark, string kicker, string name, string desc, string lang)
        {
            box.Add(mark);
            var col = new VisualElement();
            col.style.marginLeft = 12;
            col.pickingMode = PickingMode.Ignore;
            col.Add(UiKit.Text(kicker, GX.SERIF, 600, false, UiKit.Rem(0.58), GX.WAX, 0.28, lang, true));
            col.Add(UiKit.Text(name, GX.SERIF, 600, false, UiKit.Rem(1.05), GX.INK, 0, lang));
            col.Add(UiKit.Text(desc, GX.SERIF, 400, true, UiKit.Rem(0.72), GX.SEPIA, 0, lang));
            box.Add(col);
        }
    }

    /// <summary>a seal just pressed: drops in at the top of the frame</summary>
    public sealed class SealToast : VisualElement
    {
        public SealToast(string id, string lang, UIStrings t)
        {
            UiKit.Fill(this);
            pickingMode = PickingMode.Ignore;
            var s = Seals.SEAL_TEXT[id][lang];
            var p = new PaperToast();
            p.Lines(new SealMark(true, 34), t.sealEarned, s.name, s.desc, lang);
            Add(p);
        }
    }

    /// <summary>a finished chapter: its keepsake now stands on the hills</summary>
    public sealed class ChapterToast : VisualElement
    {
        static readonly string[] ROMAN12 = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

        public ChapterToast(int ch, string lang, UIStrings t)
        {
            UiKit.Fill(this);
            pickingMode = PickingMode.Ignore;
            var p = new PaperToast();
            var disc = new VisualElement();
            disc.style.width = 36;
            disc.style.height = 36;
            disc.style.flexShrink = 0;
            UiKit.Radius(disc, 18);
            disc.style.backgroundColor = GX.SEPIA;
            disc.style.alignItems = Align.Center;
            disc.style.justifyContent = Justify.Center;
            var num = UiKit.Text(ch >= 0 && ch < ROMAN12.Length ? ROMAN12[ch] : "", GX.SERIF, 600, false, UiKit.Rem(0.85), GX.CREAM, 0, lang, false, 0);
            disc.Add(num);
            var names = BookText.CHAPTER_NAMES[lang];
            p.Lines(disc, t.chapterDone, ch >= 0 && ch < names.Length ? names[ch] : "", t.keepsakeDesc, lang);
            Add(p);
        }
    }

    /// <summary>every seal, earned or still to win</summary>
    public sealed class SealsGrid : VisualElement
    {
        readonly List<VisualElement> cells = new List<VisualElement>();

        public SealsGrid(ICollection<string> earned, string lang)
        {
            // grid-cols-[repeat(auto-fill,minmax(13rem,1fr))] gap-3
            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            foreach (var id in Seals.SEAL_ORDER)
            {
                bool on = earned.Contains(id);
                var s = Seals.SEAL_TEXT[id][lang];
                var cell = new VisualElement();
                cell.style.flexDirection = FlexDirection.Row;
                cell.style.alignItems = Align.Center;
                UiKit.Radius(cell, 4);
                UiKit.BorderW(cell, 1);
                UiKit.BorderC(cell, on ? UiKit.C("rgba(201,161,90,0.45)") : UiKit.C("rgba(241,228,196,0.1)"));
                cell.style.backgroundColor = on ? UiKit.C("rgba(201,161,90,0.08)") : new Color(0, 0, 0, 0);
                UiKit.Pad(cell, 12, 10);
                cell.Add(new SealMark(on, 38));
                var col = new VisualElement();
                col.style.marginLeft = 12;
                col.style.flexShrink = 1;
                col.style.minWidth = 0;
                col.Add(UiKit.Text(s.name, GX.SERIF, 600, false, UiKit.Rem(1), on ? GX.PAGE : UiKit.C("rgba(241,228,196,0.55)"), 0, lang));
                col.Add(UiKit.Text(s.desc, GX.SERIF, 400, true, UiKit.Rem(0.78), on ? UiKit.C("rgba(241,228,196,0.75)") : UiKit.C("rgba(241,228,196,0.4)"), 0, lang, false, 1.375));
                cell.Add(col);
                cells.Add(cell);
                Add(cell);
            }
            RegisterCallback<GeometryChangedEvent>(e => Layout(e.newRect.width));
        }

        void Layout(float w)
        {
            if (w <= 0 || float.IsNaN(w)) return;
            float min = UiKit.Rem(13), gap = 12;
            int cols = Math.Max(1, (int)Math.Floor((w + gap) / (min + gap)));
            float cw = (w - gap * (cols - 1)) / cols;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                c.style.width = cw;
                c.style.marginLeft = i % cols == 0 ? 0 : gap;
                c.style.marginTop = i < cols ? 0 : gap;
            }
        }
    }

    /// <summary>the saying of the day, as a small page on the start screen</summary>
    public sealed class DailyQuote : VisualElement
    {
        public readonly CanvasBox card;

        public DailyQuote(int index, string lang, UIStrings t, bool added)
        {
            var list = Quotes.quotesFor(lang);
            if (index < 0 || index >= list.Count) { style.display = DisplayStyle.None; card = null; return; }
            var q = list[index];
            bool low = UiKit.MaxHeight(500);
            card = new CanvasBox(GX.PaperBox(3, 16, 30, -14, 0.8), 48);
            card.style.maxWidth = UiKit.Rem(28);
            card.style.alignItems = Align.Center;
            UiKit.Pad(card, 24, low ? 10 : 16);
            UiKit.Dir(card, lang);
            Add(card);

            var title = UiKit.Text(t.dailyTitle, GX.SERIF, 600, false, UiKit.Rem(0.58), GX.WAX, 0.35, lang, true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(title);
            var text = UiKit.Text(q.text, GX.SERIF, 500, true, low ? UiKit.Rem(0.98) : UiKit.Rem(1.12), GX.INK, 0, lang, false, 1.375);
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 6;
            card.Add(text);
            var author = UiKit.Text(q.author, GX.SERIF, 600, false, UiKit.Rem(0.62), GX.SEPIA, 0.25, lang, true);
            author.style.unityTextAlign = TextAnchor.MiddleCenter;
            author.style.marginTop = 4;
            card.Add(author);

            var row = new VisualElement();
            UiKit.Row(row, lang);
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            row.style.marginTop = 8;
            if (added)
                row.Add(UiKit.Text("✓ " + t.dailyAdded, GX.SERIF, 400, true, UiKit.Rem(0.58), GX.SEPIA, 0.15, lang));
            row.Add(new ShareButton(new ShareButtonProps
            {
                index = index, lang = lang, t = t, padX = 12, padY = 4,
                border = UiKit.C("rgba(107,74,34,0.4)"), color = GX.SEPIA, hoverBg = UiKit.C("rgba(107,74,34,0.1)"),
            }));
            GX.Gap(row, 12);
            card.Add(row);
        }
    }

    /// <summary>a switch</summary>
    public sealed class Toggle : VisualElement
    {
        static readonly Func<double, double> ease = UiKit.Bezier(0.4, 0, 0.2, 1);
        readonly VisualElement knob;
        bool on;
        double k;
        UiKit.TweenHandle tw;

        public Toggle(bool on, Action onChange)
        {
            this.on = on;
            k = on ? 1 : 0;
            style.width = 44;
            style.height = 24;
            style.flexShrink = 0;
            UiKit.Radius(this, 12);
            UiKit.BorderW(this, 1);
            knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.style.position = Position.Absolute;
            knob.style.top = 2;
            knob.style.width = 18;
            knob.style.height = 18;
            UiKit.Radius(knob, 9);
            Add(knob);
            Apply();
            GX.OnClick(this, () => onChange?.Invoke());
        }

        public void Set(bool v)
        {
            if (v == on) return;
            on = v;
            double from = k, to = on ? 1 : 0;
            tw?.Stop();
            // transition-colors / transition-all: 150ms
            tw = UiKit.Tween(this, 150, 0, ease, e => { k = from + (to - from) * e; Apply(); });
        }

        void Apply()
        {
            var offB = UiKit.C("rgba(241,228,196,0.25)");
            UiKit.BorderC(this, Color.Lerp(offB, GX.GOLD, (float)k));
            style.backgroundColor = Color.Lerp(new Color(201 / 255f, 161 / 255f, 90 / 255f, 0), UiKit.C("rgba(201,161,90,0.35)"), (float)k);
            knob.style.left = (float)(2 + 20 * k);
            knob.style.backgroundColor = Color.Lerp(UiKit.C("rgba(241,228,196,0.5)"), UiKit.C("#f1e4c4"), (float)k);
        }
    }

    public sealed class Records
    {
        public double best, week, streak, summits, pages, seals;
    }

    /// <summary>the records worth keeping, and a card to share them</summary>
    public sealed class RecordsTable : VisualElement
    {
        static string fmtTime(double sec)
        {
            double s = Math.Max(0, Js.round(sec));
            return Js.str(Math.Floor(s / 60)) + ":" + Js.str(s % 60).PadLeft(2, '0');
        }

        bool busy;

        public RecordsTable(UIStrings t, Records r, string lang)
        {
            var rows = new List<(string k, string v)>
            {
                (t.recBest, r.best != 0 ? fmtTime(r.best) : "—"),
                (t.recWeek, r.week != 0 ? fmtTime(r.week) : "—"),
                (t.recStreak, r.streak != 0 ? "×" + Js.str(r.streak) : "—"),
                (t.recSummits, Js.str(r.summits)),
                (t.recPages, Js.str(r.pages) + " / " + Quotes.QUOTE_COUNT),
                (t.recSeals, Js.str(r.seals) + " / " + Seals.SEAL_ORDER.Length),
            };
            UiKit.Dir(this, lang);
            // a dl in two columns, gap-x-6 gap-y-2
            for (int i = 0; i < rows.Count; i += 2)
            {
                var line = new VisualElement();
                UiKit.Row(line, lang);
                line.style.marginTop = i == 0 ? 0 : 8;
                for (int j = i; j < Math.Min(rows.Count, i + 2); j++)
                {
                    var cell = new VisualElement();
                    cell.style.flexGrow = 1;
                    cell.style.flexBasis = 0;
                    cell.style.flexShrink = 1;
                    cell.style.minWidth = 0;
                    if (j > i)
                    {
                        if (lang == "ar") cell.style.marginRight = 24; else cell.style.marginLeft = 24;
                    }
                    var dt = UiKit.Text(rows[j].k, GX.SERIF, 400, true, UiKit.Rem(0.78), UiKit.A(GX.CREAM, 0.55), 0, lang);
                    var dd = UiKit.Text(rows[j].v, GX.SERIF, 400, false, UiKit.Rem(1.15), GX.PAGE, 0, lang);
                    if (lang == "ar") { dt.style.unityTextAlign = TextAnchor.UpperRight; dd.style.unityTextAlign = TextAnchor.UpperRight; }
                    cell.Add(dt);
                    cell.Add(dd);
                    line.Add(cell);
                }
                Add(line);
            }
            var btn = new VisualElement();
            UiKit.Row(btn, lang);
            btn.style.alignItems = Align.Center;
            btn.style.alignSelf = lang == "ar" ? Align.FlexEnd : Align.FlexStart;
            btn.style.marginTop = 12;
            UiKit.Pad(btn, 16, 6);
            UiKit.Radius(btn, 9999);
            UiKit.BorderW(btn, 1);
            UiKit.BorderC(btn, UiKit.A(GX.PALE, 0.4));
            btn.Add(Icons.Share(GX.PALE));
            var bl = UiKit.Text(t.shareRecords, GX.SERIF, 600, false, UiKit.Rem(0.62), GX.PALE, 0.28, lang, true);
            float space = UiKit.Rem(0.62) * 0.25f;
            if (lang == "ar") bl.style.marginRight = 6 + space; else bl.style.marginLeft = 6 + space;
            btn.Add(bl);
            GX.Hover(btn, new Color(0, 0, 0, 0), new Color(1, 1, 1, 0.1f));
            GX.OnClick(btn, () =>
            {
                if (busy) return;
                busy = true;
                btn.style.opacity = 0.5f;
                btn.schedule.Execute(() =>
                {
                    try
                    {
                        var png = Share.renderRecordsCard(t.records, rows);
                        Share.shareCard(png, "Sisyphus — " + t.records);
                    }
                    catch (Exception e) { Debug.LogException(e); /* dismissed */ }
                    finally { busy = false; btn.style.opacity = 1; }
                });
            });
            Add(btn);
        }
    }

    /// <summary>a text box styled the way GameExtras styles its textarea/input (placeholder drawn by us)</summary>
    internal sealed class Field : VisualElement
    {
        public readonly TextField input;
        readonly Label placeholder;

        public Field(string value, int maxLength, bool multiline, int rows, string placeholderText, string family, int weight, bool italic, float size, double lh, float padX, float padY,
            string phFamily = null, float phSize = -1)
        {
            UiKit.Radius(this, 4);
            UiKit.BorderW(this, 1);
            UiKit.BorderC(this, UiKit.C("rgba(241,228,196,0.2)"));
            style.backgroundColor = new Color(0, 0, 0, 0.2f);
            input = new TextField(maxLength, multiline, false, '*');
            input.value = value ?? "";
            input.style.marginLeft = 0; input.style.marginRight = 0; input.style.marginTop = 0; input.style.marginBottom = 0;
            input.style.paddingLeft = 0; input.style.paddingRight = 0; input.style.paddingTop = 0; input.style.paddingBottom = 0;
            var inner = input.Q(className: TextField.inputUssClassName) ?? input;
            foreach (var e in new[] { (VisualElement)input, inner })
            {
                e.style.backgroundColor = new Color(0, 0, 0, 0);
                UiKit.BorderW(e, 0);
                UiKit.SetFont(e, family, weight, italic);
                e.style.fontSize = size;
                e.style.color = GX.PAGE;
                e.style.whiteSpace = multiline ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            }
            UiKit.Pad(inner, padX, padY + (float)Math.Max(0, (lh - UiKit.NaturalLine(family)) * size / 2));
            float lineH = (float)(lh * size);
            if (rows > 0) inner.style.height = rows * lineH + padY * 2;
            Add(input);
            if (input.textSelection != null)
            {
                input.textSelection.cursorColor = GX.PAGE;
                input.textSelection.selectionColor = UiKit.C("rgba(201,161,90,0.35)");
            }
            placeholder = UiKit.Text(placeholderText, phFamily ?? family, 400, phFamily == null && italic, phSize > 0 ? phSize : size, UiKit.A(GX.CREAM, 0.3), 0, null, false, lh);
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = padX;
            placeholder.style.top = padY;
            placeholder.style.right = padX;
            Add(placeholder);
            input.RegisterValueChangedCallback(e => Sync());
            input.RegisterCallback<FocusInEvent>(e => { UiKit.typing = true; UiKit.BorderC(this, UiKit.C("rgba(201,161,90,0.6)")); });
            input.RegisterCallback<FocusOutEvent>(e => { UiKit.typing = false; UiKit.BorderC(this, UiKit.C("rgba(241,228,196,0.2)")); });
            RegisterCallback<DetachFromPanelEvent>(e => UiKit.typing = false);
            Sync();
        }

        void Sync()
        {
            placeholder.style.display = string.IsNullOrEmpty(input.value) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public string value { get { return input.value ?? ""; } set { input.value = value ?? ""; Sync(); } }
    }

    /// <summary>the player's own saying, set among the others</summary>
    public sealed class OwnSaying : VisualElement
    {
        bool done;

        public OwnSaying(UIStrings t, string ownText, string ownAuthor, Action<string, string> onOwn, string lang)
        {
            var desc = UiKit.Text(t.ownDesc, GX.SERIF, 400, true, UiKit.Rem(0.85), UiKit.A(GX.CREAM, 0.6), 0, lang);
            desc.style.marginBottom = 8;
            Add(desc);
            var text = new Field(ownText, 160, true, 3, t.ownPlaceholder, GX.SERIF, 400, true, UiKit.Rem(1.02), 1.5, 12, 8);
            Add(text);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;
            var author = new Field(ownAuthor, 40, false, 1, t.ownAuthorPlaceholder, GX.SERIF, 400, false, 14, 20.0 / 14, 12, 6);
            author.style.flexGrow = 1;
            author.style.flexShrink = 1;
            author.style.minWidth = 0;
            row.Add(author);
            Label saveLabel = null;
            var save = GX.Pill(t.save, lang, UiKit.A(GX.PALE, 0.4), GX.PALE, null);
            save.style.marginLeft = 8;
            saveLabel = save.Q<Label>();
            void Show() { UiKit.SetText(saveLabel, UiKit.Upper(done ? "✓ " + t.saved : t.save, lang), 1.5); }
            text.input.RegisterValueChangedCallback(e => { done = false; Show(); });
            author.input.RegisterValueChangedCallback(e => { done = false; Show(); });
            GX.OnClick(save, () =>
            {
                onOwn?.Invoke(text.value.Trim(), author.value.Trim());
                done = true;
                Show();
            });
            row.Add(save);
            Add(row);
        }
    }

    /// <summary>a backup of the save as one line of text: copied, shared, pasted back in</summary>
    public sealed class Backup : VisualElement
    {
        public Backup(UIStrings t, string lang, Action reload)
        {
            Label msg = null;
            Field code = null;
            void SetMsg(string m)
            {
                UiKit.SetText(msg, m, 1.5);
                msg.style.display = string.IsNullOrEmpty(m) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            void Copy()
            {
                var c = Save.exportSave();
                try
                {
                    GUIUtility.systemCopyBuffer = c;
                    SetMsg(t.copied);
                }
                catch (Exception)
                {
                    code.value = c;
                }
            }
            var desc = UiKit.Text(t.backupDesc, GX.SERIF, 400, true, UiKit.Rem(0.85), UiKit.A(GX.CREAM, 0.6), 0, lang);
            desc.style.marginBottom = 8;
            Add(desc);
            var btns = new VisualElement();
            btns.style.flexDirection = FlexDirection.Row;
            btns.style.flexWrap = Wrap.Wrap;
            var copy = GX.Pill(t.copyCode, lang, UiKit.A(GX.PALE, 0.4), GX.PALE, Copy);
            var share = GX.Pill(t.shareCode, lang, UiKit.A(GX.PALE, 0.4), GX.PALE, () =>
            {
                var c = Save.exportSave();
                try { Share.shareText(c); }
                catch (Exception) { /* dismissed */ }
            });
            share.style.marginLeft = 8;
            btns.Add(copy);
            btns.Add(share);
            Add(btns);
            code = new Field("", 100000, true, 2, t.restorePlaceholder, UiKit.MONO, 400, false, UiKit.Rem(0.7), 1.5, 12, 8, GX.SERIF, UiKit.Rem(0.9));
            code.style.marginTop = 12;
            Add(code);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;
            VisualElement restore = null;
            restore = GX.Pill(t.restore, lang, UiKit.A(GX.PALE, 0.4), GX.PALE, () =>
            {
                if (string.IsNullOrEmpty(code.value.Trim())) return;
                if (Save.importSave(code.value))
                {
                    SetMsg(t.restoreOk);
                    UiKit.After(this, 900, () => reload?.Invoke());
                }
                else SetMsg(t.restoreBad);
            });
            void Enable() { restore.style.opacity = string.IsNullOrEmpty(code.value.Trim()) ? 0.4f : 1f; }
            code.input.RegisterValueChangedCallback(e => Enable());
            Enable();
            row.Add(restore);
            msg = UiKit.Text("", GX.SERIF, 400, true, UiKit.Rem(0.85), UiKit.A(GX.CREAM, 0.7), 0, lang);
            msg.style.marginLeft = 12;
            row.Add(msg);
            SetMsg("");
            Add(row);
        }
    }

    /// <summary>
    /// The native &lt;select&gt;: a box showing the language with a chevron; a tap opens the list
    /// (PORT: the phone's own picker dialog becomes a list drawn over the game, in the same dark
    /// tones as the settings panel).
    /// </summary>
    public sealed class LanguageSelect : VisualElement
    {
        readonly Label label;
        readonly Action<string> onChange;
        string lang;

        public LanguageSelect(string lang, Action<string> onChange, bool settingsStyle)
        {
            this.lang = lang;
            this.onChange = onChange;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            UiKit.Radius(this, 4);
            UiKit.BorderW(this, 1);
            Color fg;
            float size;
            string fam;
            if (settingsStyle)
            {
                // rounded border border-[#f1e4c4]/25 bg-transparent px-2 py-1 text-sm text-[#efe3c6]
                UiKit.BorderC(this, UiKit.A(GX.CREAM, 0.25));
                fg = UiKit.C("#efe3c6");
                size = UiKit.Rem(0.875);
                fam = GX.SERIF;
                UiKit.Pad(this, 8, 4);
            }
            else
            {
                // rounded border border-border/60 bg-background/40 px-2 py-1 text-[0.7rem] tracking-[0.15em] text-muted-foreground
                UiKit.BorderC(this, UiKit.A(UiKit.Border, 0.6));
                style.backgroundColor = UiKit.A(UiKit.Background, 0.4);
                fg = UiKit.MutedFg;
                size = UiKit.Rem(0.7);
                fam = UiKit.SANS;
                UiKit.Pad(this, 8, 4);
            }
            label = UiKit.Text(Native(lang), fam, 400, false, size, fg, settingsStyle ? 0 : 0.15, null, false, settingsStyle ? 20.0 / 14 : 1.5);
            label.style.flexGrow = 1;
            Add(label);
            var chev = Icons.Chevron(fg);
            chev.style.marginLeft = 10;
            Add(chev);
            // the select is as wide as its longest option
            style.minWidth = settingsStyle ? 93 : 108;
            if (!settingsStyle) GX.HoverText(this, h => { label.style.color = h ? UiKit.Foreground : fg; chev.color = h ? UiKit.Foreground : fg; });
            GX.OnClick(this, Open);
        }

        static string Native(string code)
        {
            foreach (var l in I18n.LANGUAGES) if (l.code == code) return l.native;
            return code;
        }

        public void SetLang(string code)
        {
            lang = code;
            UiKit.SetText(label, Native(code), 1.5);
        }

        void Open()
        {
            var root = UiKit.root;
            if (root == null) return;
            var veil = new VisualElement();
            UiKit.Fill(veil);
            veil.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            veil.style.alignItems = Align.Center;
            veil.style.justifyContent = Justify.Center;
            veil.pickingMode = PickingMode.Position;
            veil.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            GX.OnClick(veil, () => veil.RemoveFromHierarchy());
            var sheet = new CanvasBox((c, w, h) =>
            {
                UiKit.BoxShadow(c, w, h, 6, 0, 10, 30, 0, Col.Rgba(0, 0, 0, 0.6));
                UiKit.RoundRect(c, 0, 0, w, h, 6);
                UiKit.CssLinear(170, w, h, out double x0, out double y0, out double x1, out double y1);
                var g = c.CreateLinearGradient(x0, y0, x1, y1);
                g.AddColorStop(0, "#1d1712");
                g.AddColorStop(1, "#0e0b09");
                c.FillStyle = g;
                c.Fill();
            }, 40);
            sheet.style.minWidth = 220;
            sheet.style.paddingTop = 8;
            sheet.style.paddingBottom = 8;
            sheet.pickingMode = PickingMode.Position;
            foreach (var l in I18n.LANGUAGES)
            {
                var code = l.code;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                UiKit.Pad(row, 20, 10);
                var dot = new VisualElement();
                dot.style.width = 14; dot.style.height = 14;
                UiKit.Radius(dot, 7);
                UiKit.BorderW(dot, 1.5f);
                UiKit.BorderC(dot, code == lang ? GX.GOLD : UiKit.A(GX.CREAM, 0.4f));
                if (code == lang)
                {
                    var inner = new VisualElement();
                    inner.style.width = 6; inner.style.height = 6;
                    inner.style.marginLeft = 2.5f; inner.style.marginTop = 2.5f;
                    UiKit.Radius(inner, 3);
                    inner.style.backgroundColor = GX.GOLD;
                    dot.Add(inner);
                }
                row.Add(dot);
                var name = UiKit.Text(l.native, GX.SERIF, 400, false, UiKit.Rem(1.02), UiKit.C("#efe3c6"), 0, code);
                name.style.marginLeft = 14;
                row.Add(name);
                GX.Hover(row, new Color(0, 0, 0, 0), new Color(1, 1, 1, 0.06f));
                GX.OnClick(row, () =>
                {
                    veil.RemoveFromHierarchy();
                    if (code != lang)
                    {
                        SetLang(code);
                        onChange?.Invoke(code);
                    }
                });
                sheet.Add(row);
            }
            veil.Add(sheet);
            root.Add(veil);
        }
    }

    /// <summary>the game's settings, in one place instead of strung across the top edge</summary>
    public sealed class SettingsPanel : VisualElement
    {
        public sealed class Props
        {
            public bool open;
            public Action onClose;
            public UIStrings t;
            public string lang;
            public Action<string> onLang;
            public bool muted;
            public Action onMute;
            public bool vibrates;
            public bool haptics;
            public Action onHaptics;
            public bool calm;
            public Action onCalm;
            public HashSet<string> seals;
            public bool bigText, boldRing, leftHanded;
            public Action<string, bool> onPref;
            public string ownText, ownAuthor;
            public Action<string, string> onOwn;
            public Records records;
            /// <summary>Backup's window.location.reload()</summary>
            public Action reload;
        }

        Props p;
        string builtKey;
        ScrollBox scroll;
        Toggle sound, vib, calmT, big, bold, left;
        LanguageSelect select;

        public SettingsPanel()
        {
            UiKit.Fill(this);
            style.display = DisplayStyle.None;
        }

        /// <summary>React re-render: rebuild when what the panel shows changed; toggles animate in place.</summary>
        public void Update(Props np)
        {
            var old = p;
            p = np;
            if (!np.open)
            {
                if (style.display != DisplayStyle.None) { style.display = DisplayStyle.None; Clear(); builtKey = null; }
                return;
            }
            var r = np.records;
            string key = np.lang + "|" + np.vibrates + "|" + string.Join(",", np.seals) + "|" + r.best + "|" + r.week + "|" + r.streak + "|" + r.summits + "|" + r.pages + "|" + r.seals + "|" + UiKit.rootFontSize + "|" + UiKit.viewW;
            if (builtKey == null || (old != null && !old.open))
            {
                Build(np);
                builtKey = key;
                return;
            }
            if (key != builtKey)
            {
                float off = scroll != null ? scroll.scrollOffset : 0;
                Build(np);
                builtKey = key;
                var sv = scroll;
                sv?.schedule.Execute(() => sv.scrollOffset = off);
                return;
            }
            sound?.Set(!np.muted);
            vib?.Set(np.haptics);
            calmT?.Set(np.calm);
            big?.Set(np.bigText);
            bold?.Set(np.boldRing);
            left?.Set(np.leftHanded);
        }

        void Build(Props np)
        {
            Clear();
            style.display = DisplayStyle.Flex;
            var t = np.t;
            string lang = np.lang;
            // absolute inset-0 z-40 flex justify-end bg-black/55
            var veil = new VisualElement();
            UiKit.Fill(veil);
            veil.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            veil.style.flexDirection = FlexDirection.Row;
            veil.style.justifyContent = Justify.FlexEnd;
            veil.pickingMode = PickingMode.Position;
            veil.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            GX.OnClick(veil, () => p.onClose?.Invoke());
            Add(veil);

            // h-full w-full max-w-md: linear-gradient(170deg, #1d1712, #0e0b09), box-shadow -20px 0 40px rgba(0,0,0,0.6)
            var panel = new CanvasBox((c, w, h) =>
            {
                UiKit.BoxShadow(c, w, h, 0, -20, 0, 40, 0, Col.Rgba(0, 0, 0, 0.6));
                UiKit.CssLinear(170, w, h, out double x0, out double y0, out double x1, out double y1);
                var g = c.CreateLinearGradient(x0, y0, x1, y1);
                g.AddColorStop(0, "#1d1712");
                g.AddColorStop(1, "#0e0b09");
                c.FillStyle = g;
                c.FillRect(0, 0, w, h);
            }, 100);
            panel.style.height = Length.Percent(100);
            panel.style.width = Length.Percent(100);
            panel.style.maxWidth = UiKit.Rem(28);
            panel.pickingMode = PickingMode.Position;
            panel.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            veil.Add(panel);

            scroll = new ScrollBox();
            panel.Add(scroll);
            var body = scroll.contentContainer;
            float px = UiKit.Sm ? 32 : 24;
            body.style.paddingLeft = px + UiKit.safeRight * 0;
            body.style.paddingRight = px + UiKit.safeRight;
            body.style.paddingTop = 20;
            body.style.paddingBottom = 20 + UiKit.safeBottom;
            body.style.color = UiKit.C("#efe3c6");

            // the header
            var headRow = new VisualElement();
            headRow.style.flexDirection = FlexDirection.Row;
            headRow.style.alignItems = Align.Center;
            headRow.style.justifyContent = Justify.SpaceBetween;
            var h2 = UiKit.Text(t.settings, GX.SERIF, 600, false, UiKit.Rem(1.25), UiKit.C("#efe3c6"), 0.2, lang, true, 1.4);
            headRow.Add(h2);
            var close = new VisualElement();
            close.style.width = 36; close.style.height = 36;
            close.style.alignItems = Align.Center; close.style.justifyContent = Justify.Center;
            UiKit.Radius(close, 18);
            UiKit.BorderW(close, 1);
            UiKit.BorderC(close, UiKit.A(GX.CREAM, 0.25));
            var x = Icons.Close(UiKit.A(GX.CREAM, 0.8));
            close.Add(x);
            GX.HoverText(close, hv => x.color = hv ? GX.CREAM : UiKit.A(GX.CREAM, 0.8));
            GX.OnClick(close, () => p.onClose?.Invoke());
            headRow.Add(close);
            body.Add(headRow);

            VisualElement Row(VisualElement left, VisualElement right)
            {
                // flex items-center justify-between gap-6 border-b border-[#f1e4c4]/10 py-3
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = UiKit.A(GX.CREAM, 0.1);
                row.style.paddingTop = 12;
                row.style.paddingBottom = 12;
                left.style.flexShrink = 1;
                left.style.minWidth = 0;
                row.Add(left);
                right.style.marginLeft = 24;
                row.Add(right);
                return row;
            }
            Label L(string s) { return UiKit.Text(s, GX.SERIF, 400, false, UiKit.Rem(1.02), UiKit.C("#efe3c6"), 0, lang); }
            VisualElement LD(string s, string d)
            {
                var col = new VisualElement();
                col.Add(L(s));
                col.Add(UiKit.Text(d, GX.SERIF, 400, true, UiKit.Rem(0.82), UiKit.A(GX.CREAM, 0.55), 0, lang));
                return col;
            }
            Label Head(string s)
            {
                var h = UiKit.Text(s, GX.SERIF, 600, false, UiKit.Rem(0.65), GX.GOLD, 0.32, lang, true);
                h.style.marginTop = 28;
                h.style.marginBottom = 8;
                return h;
            }

            var sec = new VisualElement();
            sec.style.marginTop = 12;
            select = new LanguageSelect(lang, c => p.onLang?.Invoke(c), true);
            sec.Add(Row(L(t.language), select));
            sound = new Toggle(!np.muted, () => p.onMute?.Invoke());
            sec.Add(Row(L(t.sound), sound));
            vib = null;
            if (np.vibrates)
            {
                vib = new Toggle(np.haptics, () => p.onHaptics?.Invoke());
                sec.Add(Row(L(t.vibration), vib));
            }
            calmT = new Toggle(np.calm, () => p.onCalm?.Invoke());
            sec.Add(Row(LD(t.calmMode, t.calmDesc), calmT));
            body.Add(sec);

            body.Add(Head(t.accessibility));
            var acc = new VisualElement();
            big = new Toggle(np.bigText, () => p.onPref?.Invoke("bigText", !p.bigText));
            acc.Add(Row(L(t.bigText), big));
            bold = new Toggle(np.boldRing, () => p.onPref?.Invoke("boldRing", !p.boldRing));
            acc.Add(Row(LD(t.boldRing, t.boldRingDesc), bold));
            left = new Toggle(np.leftHanded, () => p.onPref?.Invoke("leftHanded", !p.leftHanded));
            acc.Add(Row(LD(t.leftHanded, t.leftHandedDesc), left));
            body.Add(acc);

            body.Add(Head(t.records));
            body.Add(new RecordsTable(t, np.records, lang));

            body.Add(Head(t.sealsTitle + " · " + np.seals.Count + "/" + Seals.SEAL_ORDER.Length));
            body.Add(new SealsGrid(np.seals, lang));

            body.Add(Head(t.ownTitle));
            body.Add(new OwnSaying(t, np.ownText, np.ownAuthor, (a, b) => p.onOwn?.Invoke(a, b), lang));

            body.Add(Head(t.backup));
            body.Add(new Backup(t, lang, () => p.reload?.Invoke()));

            var privacy = UiKit.Text(t.privacy, UiKit.SANS, 400, false, UiKit.Rem(0.62), UiKit.A(GX.CREAM, 0.5), 0.25, lang, true);
            privacy.style.marginTop = 32;
            privacy.style.alignSelf = Align.FlexStart;
            GX.HoverText(privacy, hv => privacy.style.color = hv ? GX.CREAM : UiKit.A(GX.CREAM, 0.5));
            GX.OnClick(privacy, Privacy.Open);
            body.Add(privacy);
        }
    }

    /// <summary>the /privacy route: the store listings' page on the game's site</summary>
    public static class Privacy
    {
        public const string URL = "https://s-s-fos.vercel.app/privacy";
        public static void Open() { Application.OpenURL(URL); }
    }
}
