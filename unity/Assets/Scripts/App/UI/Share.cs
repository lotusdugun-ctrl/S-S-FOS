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
        public static byte[] renderShareCard(string text, string author, string chapter, bool rtl, string lang = null)
        {
            // PORT: ctx.direction = "rtl" has no counterpart in Ctx; the platform text stack lays out
            // an Arabic run right to left by itself, and every line on the card is centred
            var cv = new Surface(W, H, true, "share") { isScreen = true };
            var c = cv.GetContext();
            var icon = loadIcon();
            Antique.QuoteCard(c, W, H, text, author, chapter, latinOnly(chapter), SERIF, lang, icon != null);
            double ix = Antique.iconX, iy = Antique.iconY, side = Antique.iconSide;
            return toPng(cv, icon == null ? null : (Action<Texture2D>)(tex => layIcon(tex, icon, ix, iy, side)));
        }

        static Texture2D iconTex;

        /// <summary>the store icon (Resources/UI/share-icon.bytes, a PNG), readable, once</summary>
        static Texture2D loadIcon()
        {
            if (iconTex != null) return iconTex;
            var ta = Resources.Load<TextAsset>("UI/share-icon");
            if (ta == null) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(ta.bytes, false)) { UnityEngine.Object.Destroy(t); return null; }
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            iconTex = t;
            return t;
        }

        /// <summary>
        /// the icon into the card's frame at (x, y, side) (card pixels, top row first), inside the
        /// gilt edge, its corners rounded as the store rounds them, the edges smoothed
        /// </summary>
        static void layIcon(Texture2D card, Texture2D icon, double x, double y, double side)
        {
            const double inset = 4;
            double x0 = x + inset, y0 = y + inset, s = side - inset * 2, r = side * 0.22 - inset;
            int w = card.width, h = card.height;
            int ax = Math.Max(0, (int)Math.Floor(x0)), bx = Math.Min(w - 1, (int)Math.Ceiling(x0 + s));
            int ay = Math.Max(0, (int)Math.Floor(y0)), by = Math.Min(h - 1, (int)Math.Ceiling(y0 + s));
            var px = card.GetPixels32();
            for (int yy = ay; yy <= by; yy++)
                for (int xx = ax; xx <= bx; xx++)
                {
                    double cx = xx + 0.5, cy = yy + 0.5;
                    // signed distance to the rounded square, for a one-pixel soft edge
                    double qx = Math.Abs(cx - (x0 + s / 2)) - (s / 2 - r), qy = Math.Abs(cy - (y0 + s / 2)) - (s / 2 - r);
                    double d = Math.Sqrt(Math.Max(qx, 0) * Math.Max(qx, 0) + Math.Max(qy, 0) * Math.Max(qy, 0)) + Math.Min(Math.Max(qx, qy), 0) - r;
                    double cov = Math.Max(0, Math.Min(1, 0.5 - d));
                    if (cov <= 0) continue;
                    var ic = icon.GetPixelBilinear((float)((cx - x0) / s), (float)(1 - (cy - y0) / s));
                    double a = cov * ic.a;
                    int i = (h - 1 - yy) * w + xx;
                    var dst = px[i];
                    px[i] = new Color32(
                        (byte)Math.Round(dst.r + (ic.r * 255 - dst.r) * a),
                        (byte)Math.Round(dst.g + (ic.g * 255 - dst.g) * a),
                        (byte)Math.Round(dst.b + (ic.b * 255 - dst.b) * a), 255);
                }
            card.SetPixels32(px);
            card.Apply(false, false);
        }

        /// <summary>the store pages, Google Play always, the App Store once it has an address</summary>
        public const string PLAY_URL = "https://play.google.com/store/apps/details?id=com.lotusdugun.sisyphus";
        /// <summary>fill in once the app is on the App Store (https://apps.apple.com/app/id…)</summary>
        public const string APP_STORE_URL = "";

        /// <summary>the words that travel with a shared card: what it is, then where to play</summary>
        public static string withLinks(string text)
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(text)) sb.Append(text).Append("\n\n");
            sb.Append(Antique.GAME_NAME).Append(" — free to play");
            sb.Append("\nGoogle Play: ").Append(PLAY_URL);
            if (!string.IsNullOrEmpty(APP_STORE_URL)) sb.Append("\nApp Store: ").Append(APP_STORE_URL);
            return sb.ToString();
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

        static byte[] toPng(Surface cv, Action<Texture2D> before = null)
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
                before?.Invoke(tex);
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
            Send(withLinks(text), path);
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
