#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Sisifos.App.UI;
using Sisifos.Game;
using UnityEngine;

namespace Sisifos.App
{
    /// <summary>
    /// Editor-only remote test driver. When the editor's watcher (Assets/Editor/LogExport/Autopilot.cs)
    /// enters play mode for a test run it leaves Logs/autopilot-active.txt; this then plays the
    /// script in Logs/autopilot.json — start the game, push, rest, throw, change language,
    /// take screenshots — and writes the results to Logs/autopilot/: screenshots, frame rate and
    /// the engine's quality state. Lets someone help with the project without sitting at the
    /// computer. Does nothing in builds or in ordinary play.
    /// </summary>
    public sealed class AutopilotRunner : MonoBehaviour
    {
        /// <summary>when set, overrides the keyboard's push input</summary>
        public static double? input;
        public static Action begin;
        public static Action kick;
        public static Action<string> lang;

        sealed class Step
        {
            public double at;
            public string action;
            public string arg;
        }

        static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
        static string Dir { get { return Path.Combine(Root, "Logs", "autopilot"); } }

        readonly List<Step> steps = new List<Step>();
        int next;
        double t0 = -1;
        int shots;
        readonly StringBuilder report = new StringBuilder();
        double fpsSum, fpsN, fpsMin = double.MaxValue;
        double windowSum, windowN;
        int errors;

        public static void MaybeStart(GameObject host)
        {
            string flag = Path.Combine(Root, "Logs", "autopilot-active.txt");
            if (!File.Exists(flag)) return;
            File.Delete(flag);
            host.AddComponent<AutopilotRunner>();
        }

        void Awake()
        {
            Application.runInBackground = true;
            Directory.CreateDirectory(Dir);
            foreach (var f in Directory.GetFiles(Dir)) { try { File.Delete(f); } catch (Exception) { } }
            Application.logMessageReceived += OnLog;
            string scriptFile = Path.Combine(Root, "Logs", "autopilot.json");
            try
            {
                var arr = Json.Parse(File.ReadAllText(scriptFile)) as List<object>;
                foreach (var o in arr)
                {
                    var d = o as Dictionary<string, object>;
                    steps.Add(new Step
                    {
                        at = Json.Num(Json.Get(d, "at")),
                        action = Json.Str(Json.Get(d, "do"), ""),
                        arg = Json.Str(Json.Get(d, "arg"), null),
                    });
                }
            }
            catch (Exception e)
            {
                report.AppendLine("script error: " + e.Message);
                steps.Add(new Step { at = 0, action = "shot" });
                steps.Add(new Step { at = 1, action = "stop" });
            }
            report.AppendLine("screen " + Screen.width + "x" + Screen.height + " unity " + Application.unityVersion);
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors++;
                if (errors <= 20) report.AppendLine("LOG " + type + ": " + msg + "\n" + stack);
            }
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        double start = -1;
        int calm;
        int frameNo;
        readonly List<string> pendingShots = new List<string>();
        readonly StringBuilder slow = new StringBuilder();

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = Time.unscaledDeltaTime;
            frameNo++;
            if (start < 0) start = now;
            if (dt > 0.1f && slow.Length < 2000)
                slow.Append("frame ").Append(frameNo).Append(" at ").Append((now - start).ToString("0.00", CultureInfo.InvariantCulture))
                    .Append("s took ").Append((dt * 1000).ToString("0", CultureInfo.InvariantCulture)).Append(" ms\n");
            if (pendingShots.Count > 0)
            {
                Capture(pendingShots[0]);
                pendingShots.RemoveAt(0);
            }
            // the script's clock starts once the game runs smoothly (the first frames load and build)
            if (t0 < 0)
            {
                calm = dt < 0.1f ? calm + 1 : 0;
                if (calm < 5 && now - start < 60) return;
                t0 = now;
                report.AppendLine("warm-up " + (now - start).ToString("0.00", CultureInfo.InvariantCulture) + " s, " + frameNo + " frames");
            }
            double t = now - t0;
            if (t > 1 && dt > 0)
            {
                double fps = 1 / dt;
                fpsSum += fps; fpsN++;
                windowSum += fps; windowN++;
                if (fps < fpsMin) fpsMin = fps;
            }
            while (next < steps.Count && steps[next].at <= t)
            {
                Run(steps[next], t);
                next++;
            }
        }

        void Run(Step s, double t)
        {
            switch (s.action)
            {
                case "begin": begin?.Invoke(); break;
                case "push": input = 1; break;
                case "back": input = -0.6; break;
                case "release": input = 0; break;
                case "keys": input = null; break;
                case "kick": kick?.Invoke(); break;
                case "lang": lang?.Invoke(s.arg ?? "tr"); break;
                case "shot":
                {
                    string name = "shot_" + (shots++).ToString("00") + (s.arg != null ? "_" + s.arg : "") + ".png";
                    pendingShots.Add(Path.Combine(Dir, name));
                    report.AppendLine(Line(t, "shot " + name));
                    break;
                }
                case "stats": report.AppendLine(Line(t, Stats())); break;
                case "stop":
                    report.AppendLine(Line(t, "fps avg " + (fpsN > 0 ? fpsSum / fpsN : 0).ToString("0.0", CultureInfo.InvariantCulture)
                        + " min " + (fpsMin == double.MaxValue ? 0 : fpsMin).ToString("0.0", CultureInfo.InvariantCulture)
                        + " errors " + errors));
                    report.AppendLine(Line(t, Stats()));
                    report.AppendLine("slow frames:\n" + slow);
                    File.WriteAllText(Path.Combine(Dir, "report.txt"), report.ToString());
                    input = null;
                    // the queued screenshots need a frame each, and a moment to be written
                    Invoke(nameof(Finish), 1.0f + pendingShots.Count * 0.2f);
                    break;
            }
        }

        /// <summary>
        /// The Game view with its UI, through ScreenCapture when that module is in the project;
        /// otherwise the game surface alone (no UI), read back from the drawing backend.
        /// </summary>
        static void Capture(string path)
        {
            var t = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
            var m = t != null ? t.GetMethod("CaptureScreenshot", new[] { typeof(string) }) : null;
            if (m != null) { m.Invoke(null, new object[] { path }); return; }
            try
            {
                var cv = GameRoot.screen;
                if (cv == null || Sisifos.Paint.Gfx.backend == null) return;
                Sisifos.Paint.Gfx.Flush();
                var rgba = Sisifos.Paint.Gfx.backend.ReadPixels(cv, out int w, out int h);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var flipped = new byte[rgba.Length];
                int row = w * 4;
                for (int y = 0; y < h; y++) Buffer.BlockCopy(rgba, y * row, flipped, (h - 1 - y) * row, row);
                tex.LoadRawTextureData(flipped);
                tex.Apply(false, false);
                File.WriteAllBytes(path.Replace(".png", "_game.png"), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("autopilot capture: " + e.Message);
            }
        }

        void Finish()
        {
            File.WriteAllText(Path.Combine(Dir, "done.txt"), DateTime.Now.ToString("o"));
            UnityEditor.EditorApplication.isPlaying = false;
        }

        string Line(double t, string s) { return t.ToString("0.00", CultureInfo.InvariantCulture) + "s " + s; }

        string Stats()
        {
            var sb = new StringBuilder();
            double w = windowN > 0 ? windowSum / windowN : 0;
            windowSum = 0; windowN = 0;
            sb.Append("fps(window) ").Append(w.ToString("0.0", CultureInfo.InvariantCulture));
            var e = GameRoot.CurrentEngine;
            if (e != null)
            {
                foreach (var n in new[] { "phase", "x", "vx", "breath", "resScale", "lowQuality", "dpr", "w", "h", "hour", "cycles" })
                {
                    var f = typeof(SisyphusEngine).GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    object v = f != null ? f.GetValue(e) : null;
                    sb.Append(' ').Append(n).Append('=').Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                }
            }
            return sb.ToString();
        }
    }
}
#endif
