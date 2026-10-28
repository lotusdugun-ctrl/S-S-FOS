using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lets a test run be started from outside the editor: when Logs/autopilot-go.txt appears, the
/// editor picks up changed scripts (as if it had been focused), waits for them to compile and,
/// if they did, enters play mode with Logs/autopilot-active.txt set so the game's
/// AutopilotRunner plays Logs/autopilot.json and writes its results to Logs/autopilot/.
/// The outcome of the compile is written to Logs/autopilot/compile.txt either way.
/// Lives in this dependency-free assembly so it keeps working while the game does not compile.
/// </summary>
[InitializeOnLoad]
static class SisifosAutopilotWatcher
{
    const string Pending = "sisifos.autopilot.pending";
    static double next;

    static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static string Logs { get { return Path.Combine(Root, "Logs"); } }

    static SisifosAutopilotWatcher()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < next) return;
        next = now + 1;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string go = Path.Combine(Logs, "autopilot-go.txt");
            if (File.Exists(go))
            {
                File.Delete(go);
                SessionState.SetBool(Pending, true);
                SessionState.SetFloat(Pending + ".at", (float)now);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                return;
            }
            if (!SessionState.GetBool(Pending, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            // give the refresh a moment to start a compile it found work for
            if (now - SessionState.GetFloat(Pending + ".at", 0) < 3) return;
            SessionState.SetBool(Pending, false);
            string dir = Path.Combine(Logs, "autopilot");
            Directory.CreateDirectory(dir);
            if (EditorUtility.scriptCompilationFailed)
            {
                File.WriteAllText(Path.Combine(dir, "compile.txt"), "failed " + DateTime.Now.ToString("o"));
                return;
            }
            File.WriteAllText(Path.Combine(dir, "compile.txt"), "ok " + DateTime.Now.ToString("o"));
            if (!File.Exists(Path.Combine(Logs, "autopilot.json"))) return;
            PlayerSettings.runInBackground = true;
            File.WriteAllText(Path.Combine(Logs, "autopilot-active.txt"), "1");
            EditorApplication.isPlaying = true;
        }
        catch (Exception)
        {
            // try again next second
        }
    }
}
