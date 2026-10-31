// Port of src/game/share.ts: a saying as a picture to share — a page of parchment, 1080 × 1350,
// drawn with the canvas emulation (Sisifos.Paint.Ctx), read back, written as a PNG and handed to the
// system share sheet (Android: an ACTION_SEND intent; iOS: UIActivityViewController through
// Plugins/iOS/SisifosNative.mm). Elsewhere (the editor, desktop) the PNG is saved and opened.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Sisifos.Game;
using Sisifos.Paint;
using UnityEngine;

namespace Sisifos.App.UI
{
    public static class Share
    {
        const int W = 1080;
        const int H = 1350;
        const string SERIF = "'Cormorant Garamond', 'EB Garamond', Georgia, serif";

        static readonly Regex cjkRe = new Regex("[　-鿿]");
        static readonly Regex punctRe = new Regex("^[，。；：！？、,.;:!?」』）)]$");
        static readonly Regex wsRe = new Regex("\\s+");

        static List<string> wrap(Ctx c, string text, double max)
        {
            // scripts without spaces between words (Chinese) wrap by character
            bool cjk = cjkRe.IsMatch(text);
            var units = new List<string>();
            if (cjk)
            {
                var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
                while (e.MoveNext()) units.Add((string)e.Current);
            }
            else units.AddRange(wsRe.Split(text));
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

        /// <summary>/^[\p{Script=Latin}\s\d'’-]*$/u</summary>
        static bool latinOnly(string s)
        {
            foreach (char ch in s)
            {
                if (char.IsWhiteSpace(ch) || (ch >= '0' && ch <= '9') || ch == '\'' || ch == '’' || ch == '-') continue;
                if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z')) continue;
                if (ch == 'ª' || ch == 'º' || (ch >= 'À' && ch <= 'ɏ' && ch != '×' && ch != '÷')) continue;
                if ((ch >= 'Ḁ' && ch <= 'ỿ') || (ch >= 'Ⱡ' && ch <= 'Ɀ') || (ch >= '꜠' && ch <= 'ꟿ') || (ch >= 'Ａ' && ch <= 'Ｚ') || (ch >= 'ａ' && ch <= 'ｚ')) continue;
                return false;
            }
            return true;
        }

        static Surface page(out Ctx c)
        {
            var cv = new Surface(W, H, true, "share") { isScreen = true };
            c = cv.GetContext();
            // night-blue around the page, the game's own first colour
            var bg = c.CreateLinearGradient(0, 0, 0, H);
            bg.AddColorStop(0, "#1d2340");
            bg.AddColorStop(1, "#120e14");
            c.FillStyle = bg;
            c.FillRect(0, 0, W, H);
            return cv;
        }

        /// <summary>draw the card; returns the PNG bytes (null if it could not be read back)</summary>
        public static byte[] renderShareCard(string text, string author, string chapter, bool rtl)
        {
            var cv = page(out var c);
            // the page
            const double px = 90;
            const double py = 110;
            const double pw = W - px * 2;
            const double ph = H - py * 2;
            c.Save();
            c.ShadowColor = Col.Parse("rgba(0,0,0,0.6)");
            c.ShadowBlur = 50;
            c.ShadowOffsetY = 18;
            var paper = c.CreateLinearGradient(px, py, px + pw, py + ph);
            paper.AddColorStop(0, "#f3e9d1");
            paper.AddColorStop(0.55, "#e9dbb9");
            paper.AddColorStop(1, "#dcc69a");
            c.FillStyle = paper;
            c.FillRect(px, py, pw, ph);
            c.Restore();
            // foxing toward the edges
            var fox = c.CreateRadialGradient(W / 2.0, H / 2.0, pw * 0.35, W / 2.0, H / 2.0, pw * 0.85);
            fox.AddColorStop(0, "rgba(120,78,30,0)");
            fox.AddColorStop(1, "rgba(120,78,30,0.28)");
            c.FillStyle = fox;
            c.FillRect(px, py, pw, ph);
            // a ruled double frame
            c.StrokeStyle = "rgba(107,74,34,0.45)";
            c.LineWidth = 2;
            c.StrokeRect(px + 28, py + 28, pw - 56, ph - 56);
            c.LineWidth = 1;
            c.StrokeRect(px + 38, py + 38, pw - 76, ph - 76);

            c.TextAlign = TextAlign.Center;
            c.FillStyle = "#6b4a22";
            c.Font = "600 30px " + SERIF;
            // letter-spaced capitals for Latin scripts; others are set as they are
            bool latin = latinOnly(chapter);
            string head = chapter;
            if (latin)
            {
                var up = chapter.ToUpperInvariant();
                var parts = new string[up.Length];
                for (int i = 0; i < up.Length; i++) parts[i] = up[i].ToString();
                head = string.Join(" ", parts);
            }
            c.FillText(head, W / 2.0, py + 120);
            // a lozenge on a rule
            c.StrokeStyle = "rgba(107,74,34,0.6)";
            c.BeginPath();
            c.MoveTo(W / 2.0 - 110, py + 152);
            c.LineTo(W / 2.0 - 18, py + 152);
            c.MoveTo(W / 2.0 + 18, py + 152);
            c.LineTo(W / 2.0 + 110, py + 152);
            c.Stroke();
            c.FillStyle = "rgba(107,74,34,0.75)";
            c.BeginPath();
            c.MoveTo(W / 2.0, py + 144);
            c.LineTo(W / 2.0 + 8, py + 152);
            c.LineTo(W / 2.0, py + 160);
            c.LineTo(W / 2.0 - 8, py + 152);
            c.ClosePath();
            c.Fill();

            // the words: as large as fits
            c.FillStyle = "#2a1c0f";
            // PORT: ctx.direction = "rtl" has no counterpart in Ctx; the platform text stack lays out
            // an Arabic run right to left by itself (first-strong direction), and every line here is
            // centred, so only neutral characters at a line's ends could sit differently.
            double size = 74;
            var lines = new List<string>();
            for (; size >= 40; size -= 4)
            {
                c.Font = "italic 500 " + Js.str(size) + "px " + SERIF;
                lines = wrap(c, text, pw - 200);
                if (lines.Count * size * 1.28 < ph - 480) break;
            }
            double lh = size * 1.28;
            // the words and the name under them, centred together on the page
            double group = lines.Count * lh + 70;
            double top = H / 2.0 - group / 2 + size * 0.8;
            for (int i = 0; i < lines.Count; i++) c.FillText(lines[i], W / 2.0, top + i * lh);

            c.FillStyle = "#5a3d1c";
            c.Font = "600 34px " + SERIF;
            c.FillText("— " + author, W / 2.0, top + lines.Count * lh + 40);

            c.FillStyle = "rgba(107,74,34,0.75)";
            c.Font = "600 26px " + SERIF;
            c.FillText("S I S Y P H U S", W / 2.0, py + ph - 70);

            return toPng(cv);
        }

        /// <summary>
        /// A moment of the climb as a card: the frame he is in now, set in a black-figure
        /// border on the night-blue page, the cycle written large under it and a line of
        /// what it was (the time of the climb, the hill) under that.
        /// </summary>
        public static byte[] renderMomentCard(Surface shot, string big, string small)
        {
            if (shot == null || shot.width < 2 || shot.height < 2) return null;
            var cv = page(out var c);
            Antique.MomentCard(c, shot, W, H, big, small, SERIF);
            return toPng(cv);
        }

        /// <summary>a records card in the same parchment: the title, then label · value rows</summary>
        public static byte[] renderRecordsCard(string title, IList<(string k, string v)> rows)
        {
            var cv = page(out var c);
            const double px = 90;
            const double py = 110;
            const double pw = W - px * 2;
            const double ph = H - py * 2;
            var paper = c.CreateLinearGradient(px, py, px + pw, py + ph);
            paper.AddColorStop(0, "#f3e9d1");
            paper.AddColorStop(0.55, "#e9dbb9");
            paper.AddColorStop(1, "#dcc69a");
            c.Save();
            c.ShadowColor = Col.Parse("rgba(0,0,0,0.6)");
            c.ShadowBlur = 50;
            c.ShadowOffsetY = 18;
            c.FillStyle = paper;
            c.FillRect(px, py, pw, ph);
            c.Restore();
            c.StrokeStyle = "rgba(107,74,34,0.45)";
            c.LineWidth = 2;
            c.StrokeRect(px + 28, py + 28, pw - 56, ph - 56);
            c.TextAlign = TextAlign.Center;
            c.FillStyle = "#2a1c0f";
            c.Font = "600 64px " + SERIF;
            c.FillText("SISYPHUS", W / 2.0, py + 170);
            c.FillStyle = "#6b4a22";
            c.Font = "italic 500 40px " + SERIF;
            c.FillText(title, W / 2.0, py + 230);
            double top = py + 340;
            double step = (ph - 460) / Math.Max(1, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                double y = top + i * step;
                c.FillStyle = "#6b4a22";
                c.Font = "italic 500 34px " + SERIF;
                c.FillText(rows[i].k, W / 2.0, y);
                c.FillStyle = "#2a1c0f";
                c.Font = "600 58px " + SERIF;
                c.FillText(rows[i].v, W / 2.0, y + 62);
            }
            return toPng(cv);
        }

        static byte[] toPng(Surface cv)
        {
            try
            {
                Gfx.Flush();
                if (Gfx.backend == null) return null;
                var rgba = Gfx.backend.ReadPixels(cv, out int w, out int h);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                // Texture2D rows run bottom-up
                var flipped = new byte[rgba.Length];
                int row = w * 4;
                for (int y = 0; y < h; y++) Buffer.BlockCopy(rgba, y * row, flipped, (h - 1 - y) * row, row);
                tex.LoadRawTextureData(flipped);
                tex.Apply(false, false);
                var png = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
                return png;
            }
            finally
            {
                cv.Release();
            }
        }

        // ------------------------------------------------------------ the share sheet

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SisifosShare_Share(string text, string imagePath);
#endif

        /// <summary>hand the card to the system share sheet, or save and open it where there is none</summary>
        public static void shareCard(byte[] png, string text)
        {
            const string name = "sisyphus-wisdom.png";
            if (png == null) return;
            string path = Path.Combine(Application.temporaryCachePath, name);
            File.WriteAllBytes(path, png);
            Send(text, path);
        }

        /// <summary>share plain text (the backup code)</summary>
        public static void shareText(string text)
        {
            Send(text, null);
        }

        static void Send(string text, string imagePath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = up.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
            {
                if (imagePath != null)
                {
                    // the card goes out as content://<app id>.sisifosshare/<name>, served by
                    // SisifosShareProvider (Plugins/Android) from cache/share/: other apps cannot
                    // read a file:// path inside this app (WhatsApp: "file format not supported")
                    string name = Path.GetFileName(imagePath);
                    using (var cacheDir = activity.Call<AndroidJavaObject>("getCacheDir"))
                    {
                        string dir = Path.Combine(cacheDir.Call<string>("getAbsolutePath"), "share");
                        Directory.CreateDirectory(dir);
                        File.Copy(imagePath, Path.Combine(dir, name), true);
                    }
                    string authority = activity.Call<string>("getPackageName") + ".sisifosshare";
                    intent.Call<AndroidJavaObject>("setType", "image/png").Dispose();
                    using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                    using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "content://" + authority + "/" + name))
                    {
                        intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri).Dispose();
                        // the grant travels through the chooser to the picked app with the clip
                        using (var clipClass = new AndroidJavaClass("android.content.ClipData"))
                        using (var clip = clipClass.CallStatic<AndroidJavaObject>("newRawUri", "", uri))
                            intent.Call("setClipData", clip);
                    }
                    intent.Call<AndroidJavaObject>("addFlags", 1).Dispose(); // FLAG_GRANT_READ_URI_PERMISSION
                }
                else intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                if (!string.IsNullOrEmpty(text)) intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text).Dispose();
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, (string)null))
                    activity.Call("startActivity", chooser);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            SisifosShare_Share(text ?? "", imagePath ?? "");
#else
            // no share sheet: the web downloaded the file; here it is saved and opened
            if (imagePath != null)
            {
                string keep = Path.Combine(Application.persistentDataPath, Path.GetFileName(imagePath));
                try { File.Copy(imagePath, keep, true); } catch (Exception) { keep = imagePath; }
                Application.OpenURL("file://" + keep);
            }
            else GUIUtility.systemCopyBuffer = text;
#endif
        }
    }
}
