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

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (t0 < 0) t0 = now;
            double t = now - t0;
            float dt = Time.unscaledDeltaTime;
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
                    ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name));
                    report.AppendLine(Line(t, "shot " + name));
                    break;
                }
                case "stats": report.AppendLine(Line(t, Stats())); break;
                case "stop":
                    report.AppendLine(Line(t, "fps avg " + (fpsN > 0 ? fpsSum / fpsN : 0).ToString("0.0", CultureInfo.InvariantCulture)
                        + " min " + (fpsMin == double.MaxValue ? 0 : fpsMin).ToString("0.0", CultureInfo.InvariantCulture)
                        + " errors " + errors));
                    report.AppendLine(Line(t, Stats()));
                    File.WriteAllText(Path.Combine(Dir, "report.txt"), report.ToString());
                    input = null;
                    // the screenshot of the last frame needs a frame to be written
                    Invoke(nameof(Finish), 1.0f);
                    break;
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
