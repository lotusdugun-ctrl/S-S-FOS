using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sisifos.Paint;
using LineCap = Sisifos.Paint.LineCap;
using LineJoin = Sisifos.Paint.LineJoin;

namespace Sisifos.App.UI
{
    public static partial class Antique
    {
        /// <summary>where the game is found, under every card and after every shared saying</summary>
        public const string GAME_NAME = "Sisyphus: Endless Climb";
        public const string STORES_LINE = "Free on Google Play & the App Store";

        static readonly Regex cjkRe = new Regex("[　-鿿]");
        static readonly Regex punctRe = new Regex("^[，。；：！？、,.;:!?」』）)]$");
        static readonly Regex wsRe = new Regex("\\s+");

        /// <summary>words into lines no wider than `max` (by character where a script has no spaces)</summary>
        public static List<string> WrapLines(Ctx c, string text, double max)
        {
            bool cjk = cjkRe.IsMatch(text);
            var units = new List<string>();
            if (cjk)
            {
                var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
                while (e.MoveNext()) units.Add((string)e.Current);
            }
            else units.AddRange(wsRe.Split(text.Trim()));
            var lines = new List<string>();
            string line = "";
            foreach (var u in units)
            {
                string next = line != "" ? (cjk ? line + u : line + " " + u) : u;
                // never start a line on a comma or a full stop
                bool punct = punctRe.IsMatch(u);
                if (c.MeasureText(next) > max && line != "" && !punct)
                {
                    lines.Add(line);
                    line = u;
                }
                else line = next;
            }
            if (line != "") lines.Add(line);
            return lines;
        }

        static string Spaced(string s, string lang)
        {
            // Turkish and Azeri dotted capitals (tarih -> TARİH)
            var up = lang == "tr" || lang == "az" ? s.ToUpper(new System.Globalization.CultureInfo("tr-TR")) : s.ToUpperInvariant();
            var parts = new string[up.Length];
            for (int i = 0; i < up.Length; i++) parts[i] = up[i].ToString();
            return string.Join(" ", parts);
        }

        /// <summary>
        /// A saying as a card to share: a night sky with a few stars, a gilt-edged page of
        /// parchment with the chapter, the words as large as fit and the sage's name, and
        /// under the page the game's seal — the stone on its slope in a gold ring — with its
        /// name and where to find it.
        /// </summary>
        public static void QuoteCard(Ctx c, double W, double H, string text, string author, string chapter, bool latinChapter, string SERIF, string lang = null)
        {
            // the night
            var bg = c.CreateLinearGradient(0, 0, 0, H);
            bg.AddColorStop(0, "#232a4d");
            bg.AddColorStop(0.62, "#161a30");
            bg.AddColorStop(1, "#0d0b12");
            c.FillStyle = bg;
            c.FillRect(0, 0, W, H);
            for (int i = 0; i < 90; i++)
            {
                double x = (J(i, 1.3) * 0.5 + 0.5) * W, y = (J(i, 7.9) * 0.5 + 0.5) * H;
                double r = 0.8 + (J(i, 4.4) * 0.5 + 0.5) * 1.6;
                // none among the lettering under the page
                if (y > H - 270 && Math.Abs(x - W / 2) < 420) continue;
                c.FillStyle = "rgba(239,226,196," + Js.str(0.18 + (J(i, 2.2) * 0.5 + 0.5) * 0.4) + ")";
                c.BeginPath();
                c.Arc(x, y, r, 0, Math.PI * 2);
                c.Fill();
            }
            // warm light behind the page
            var glow = c.CreateRadialGradient(W / 2, H * 0.42, 60, W / 2, H * 0.42, W * 0.72);
            glow.AddColorStop(0, "rgba(214,176,104,0.30)");
            glow.AddColorStop(1, "rgba(214,176,104,0)");
            c.FillStyle = glow;
            c.FillRect(0, 0, W, H);

            // the page
            double px = 78, py = 72, pw = W - px * 2, ph = H - 300 - py;
            c.Save();
            c.ShadowColor = Col.Parse("rgba(0,0,0,0.65)");
            c.ShadowBlur = 56;
            c.ShadowOffsetY = 20;
            var paper = c.CreateLinearGradient(px, py, px + pw, py + ph);
            paper.AddColorStop(0, "#f5ecd5");
            paper.AddColorStop(0.55, "#ebdcba");
            paper.AddColorStop(1, "#dcc59a");
            c.FillStyle = paper;
            c.FillRect(px, py, pw, ph);
            c.Restore();
            var fox = c.CreateRadialGradient(W / 2, py + ph / 2, pw * 0.32, W / 2, py + ph / 2, pw * 0.86);
            fox.AddColorStop(0, "rgba(120,78,30,0)");
            fox.AddColorStop(1, "rgba(120,78,30,0.30)");
            c.FillStyle = fox;
            c.FillRect(px, py, pw, ph);
            // a gilt edge, then a ruled double frame with lozenges at the corners
            c.StrokeStyle = "rgba(214,176,104,0.95)";
            c.LineWidth = 4;
            c.StrokeRect(px + 2, py + 2, pw - 4, ph - 4);
            c.StrokeStyle = "rgba(107,74,34,0.55)";
            c.LineWidth = 2;
            c.StrokeRect(px + 26, py + 26, pw - 52, ph - 52);
            c.LineWidth = 1;
            c.StrokeRect(px + 36, py + 36, pw - 72, ph - 72);
            c.FillStyle = "rgba(107,74,34,0.8)";
            foreach (var (cx, cy) in new[] { (px + 31, py + 31), (px + pw - 31, py + 31), (px + 31, py + ph - 31), (px + pw - 31, py + ph - 31) })
                Lozenge(c, cx, cy, 9);

            c.TextAlign = TextAlign.Center;
            // the chapter, with a rule and a lozenge under it
            c.FillStyle = "#6b4a22";
            c.Font = "600 28px " + SERIF;
            c.FillText(latinChapter ? Spaced(chapter, lang) : chapter, W / 2, py + 112);
            c.StrokeStyle = "rgba(107,74,34,0.6)";
            c.LineWidth = 1.5;
            c.BeginPath();
            c.MoveTo(W / 2 - 120, py + 146); c.LineTo(W / 2 - 20, py + 146);
            c.MoveTo(W / 2 + 20, py + 146); c.LineTo(W / 2 + 120, py + 146);
            c.Stroke();
            Lozenge(c, W / 2, py + 146, 8);

            // the words, as large as fit between the chapter and the name
            c.FillStyle = "#2a1c0f";
            double room = ph - 330, size = 76;
            var lines = new List<string>();
            for (; size >= 36; size -= 3)
            {
                c.Font = "italic 500 " + Js.str(size) + "px " + SERIF;
                lines = WrapLines(c, text, pw - 190);
                if (lines.Count * size * 1.3 <= room) break;
            }
            double lh = size * 1.3;
            double group = lines.Count * lh + 90;
            double mid = py + 160 + (ph - 160 - 70) / 2;
            double top = mid - group / 2 + size * 0.82 + 34;
            for (int i = 0; i < lines.Count; i++) c.FillText(lines[i], W / 2, top + i * lh);

            // the name between two short rules
            double ay = top + (lines.Count - 1) * lh + 86;
            c.FillStyle = "#5a3d1c";
            c.Font = "600 34px " + SERIF;
            c.FillText(author, W / 2, ay);
            double aw = c.MeasureText(author) / 2 + 26;
            c.StrokeStyle = "rgba(90,61,28,0.6)";
            c.LineWidth = 1.5;
            c.BeginPath();
            c.MoveTo(W / 2 - aw - 60, ay - 11); c.LineTo(W / 2 - aw, ay - 11);
            c.MoveTo(W / 2 + aw, ay - 11); c.LineTo(W / 2 + aw + 60, ay - 11);
            c.Stroke();

            StoreFooter(c, W, py + ph, H, SERIF);
        }

        static void Lozenge(Ctx c, double x, double y, double r)
        {
            c.BeginPath();
            c.MoveTo(x, y - r);
            c.LineTo(x + r, y);
            c.LineTo(x, y + r);
            c.LineTo(x - r, y);
            c.ClosePath();
            c.Fill();
        }

        /// <summary>
        /// The game's seal and name under a card, in the band from `y0` to `H`: a gold ring
        /// with the man's stone on its slope, "SISYPHUS", "ENDLESS CLIMB" and the stores.
        /// </summary>
        public static void StoreFooter(Ctx c, double W, double y0, double H, string SERIF)
        {
            double band = H - y0;
            double cy = y0 + band * 0.5 - 4;
            // the seal at the left of the lettering, the whole group centred
            c.Font = "600 50px " + SERIF;
            string title = "S I S Y P H U S";
            double tw = c.MeasureText(title);
            c.Font = "italic 500 30px " + SERIF;
            double sw = c.MeasureText(STORES_LINE);
            double textW = Math.Max(tw, sw), r = 54, gap = 34;
            double gx = W / 2 - (r * 2 + gap + textW) / 2;
            Seal(c, gx + r, cy, r);

            double tx = gx + r * 2 + gap;
            var gold = c.CreateLinearGradient(tx, cy - 40, tx, cy + 10);
            gold.AddColorStop(0, "#f4dfae");
            gold.AddColorStop(1, "#c99a52");
            c.TextAlign = TextAlign.Left;
            c.FillStyle = gold;
            c.Font = "600 50px " + SERIF;
            c.FillText(title, tx, cy - 14);
            c.FillStyle = "rgba(214,176,104,0.85)";
            c.Font = "600 22px " + SERIF;
            c.FillText("E N D L E S S   C L I M B", tx + 2, cy + 20);
            c.FillStyle = "rgba(239,226,196,0.9)";
            c.Font = "italic 500 30px " + SERIF;
            c.FillText(STORES_LINE, tx, cy + 62);
            c.TextAlign = TextAlign.Center;
        }

        /// <summary>a gold ring; inside it a slope rising to the right and the stone on it</summary>
        public static void Seal(Ctx c, double x, double y, double r)
        {
            var disc = c.CreateRadialGradient(x - r * 0.3, y - r * 0.4, r * 0.1, x, y, r);
            disc.AddColorStop(0, "#2c3358");
            disc.AddColorStop(1, "#141729");
            c.FillStyle = disc;
            c.BeginPath();
            c.Arc(x, y, r, 0, Math.PI * 2);
            c.Fill();
            c.Save();
            c.BeginPath();
            c.Arc(x, y, r - 4, 0, Math.PI * 2);
            c.Clip();
            // the hill
            c.FillStyle = "#8a5a2c";
            c.BeginPath();
            c.MoveTo(x - r, y + r * 0.62);
            c.LineTo(x + r, y - r * 0.12);
            c.LineTo(x + r, y + r);
            c.LineTo(x - r, y + r);
            c.ClosePath();
            c.Fill();
            // the stone, resting on the slope
            double bx = x + r * 0.15, by = y - r * 0.105, br = r * 0.3;
            var stone = c.CreateRadialGradient(bx - br * 0.35, by - br * 0.4, br * 0.1, bx, by, br);
            stone.AddColorStop(0, "#f4dfae");
            stone.AddColorStop(1, "#b98a46");
            c.FillStyle = stone;
            c.BeginPath();
            c.Arc(bx, by, br, 0, Math.PI * 2);
            c.Fill();
            // the man leaning into it, a black figure
            c.StrokeStyle = "#0d0b12";
            c.LineCap = LineCap.Round;
            c.LineJoin = LineJoin.Round;
            c.LineWidth = r * 0.1;
            c.BeginPath();
            c.MoveTo(x - r * 0.66, y + r * 0.49);   // back foot
            c.LineTo(x - r * 0.42, y + r * 0.12);   // hip
            c.LineTo(x - r * 0.38, y + r * 0.39);   // front foot
            c.MoveTo(x - r * 0.42, y + r * 0.12);
            c.LineTo(x - r * 0.24, y - r * 0.12);   // shoulders
            c.LineTo(bx - br * 0.98, by + br * 0.05); // arms to the stone
            c.Stroke();
            c.FillStyle = "#0d0b12";
            c.BeginPath();
            c.Arc(x - r * 0.17, y - r * 0.27, r * 0.095, 0, Math.PI * 2);
            c.Fill();
            c.Restore();
            c.StrokeStyle = "#d6b068";
            c.LineWidth = 3;
            c.BeginPath();
            c.Arc(x, y, r - 2, 0, Math.PI * 2);
            c.Stroke();
            c.StrokeStyle = "rgba(214,176,104,0.45)";
            c.LineWidth = 1;
            c.BeginPath();
            c.Arc(x, y, r + 6, 0, Math.PI * 2);
            c.Stroke();
        }
    }
}
