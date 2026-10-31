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
        // a script change while playing stops the game first and then recompiles:
        // recompiled under a running game, GameRoot lost its screen and threw every frame
        // (and the editor, busy logging, never got to a build). 2 = Stop Playing and Recompile.
        EditorPrefs.SetInt("ScriptCompilationDuringPlay", 2);
        EditorApplication.update += Tick;
    }

    /// <summary>
    /// Logs/autopilot-build.txt ("apk" or "aab"): an Android build into Builds/, the outcome in
    /// Logs/autopilot/build.txt. Signed with Unity's debug key unless the project's Player
    /// Settings carry a keystore (the store key is the owner's and never leaves their machine).
    /// </summary>
    static void Build(bool bundle)
    {
        string dir = Path.Combine(Logs, "autopilot");
        Directory.CreateDirectory(dir);
        string outFile = Path.Combine(dir, "build.txt");
        try
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                File.WriteAllText(outFile, "no-android-module " + DateTime.Now.ToString("o") +
                    "\nUnity Hub > Installs > " + Application.unityVersion + " > Add modules > Android Build Support (OpenJDK, Android SDK & NDK Tools)");
                return;
            }
            File.WriteAllText(outFile, "building " + DateTime.Now.ToString("o"));
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var sc in EditorBuildSettings.scenes) if (sc.enabled) scenes.Add(sc.path);
            if (scenes.Count == 0) scenes.Add("Assets/Scenes/Main.unity");
            string builds = Path.Combine(Root, "Builds");
            Directory.CreateDirectory(builds);
            EditorUserBuildSettings.buildAppBundle = bundle;
            // the store's floor: the newest Android API the editor has installed
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            if (bundle)
            {
                // a store bundle must carry the owner's upload key, never the debug one: the
                // keystore's passwords are typed into Publishing Settings by the owner (Unity
                // keeps them for the session only); without them the build stops here
                // the key file itself lives beside the project, outside git (Magaza/ANAHTAR-GIZLI);
                // the settings in git carry no path to it, so it is found here
                if (string.IsNullOrEmpty(PlayerSettings.Android.keystoreName) || !File.Exists(PlayerSettings.Android.keystoreName))
                {
                    string keyDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Magaza", "ANAHTAR-GIZLI"));
                    string jks = Path.Combine(keyDir, "sisyphus-upload.jks");
                    if (File.Exists(jks))
                    {
                        PlayerSettings.Android.useCustomKeystore = true;
                        PlayerSettings.Android.keystoreName = jks.Replace('\\', '/');
                        if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasName)) PlayerSettings.Android.keyaliasName = "upload";
                    }
                }
                if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(PlayerSettings.Android.keystorePass)
                    || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
                {
                    File.WriteAllText(outFile, "need-keystore " + DateTime.Now.ToString("o") +
                        "\nProject Settings > Player > Android > Publishing Settings: Custom Keystore, the .jks, its password, alias and alias password");
                    return;
                }
                // each upload needs a higher version code than the last
                string vc = Path.Combine(Logs, "autopilot-versioncode.txt");
                if (File.Exists(vc) && int.TryParse(File.ReadAllText(vc).Trim(), out int code)) PlayerSettings.Android.bundleVersionCode = code;
            }
            // OpenGL ES 3 first, Vulkan only where it is missing: on some phones the frame,
            // presented by a command buffer at the end of the camera, flickered to black
            // under Vulkan
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3, UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            var opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Path.Combine(builds, bundle ? "Sisifos.aab" : "Sisifos.apk"),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(sum.result + " " + DateTime.Now.ToString("o"));
            sb.AppendLine("file " + opts.locationPathName);
            sb.AppendLine("version " + PlayerSettings.bundleVersion + " (" + PlayerSettings.Android.bundleVersionCode + "), target sdk " + PlayerSettings.Android.targetSdkVersion + ", keystore " + (PlayerSettings.Android.useCustomKeystore ? Path.GetFileName(PlayerSettings.Android.keystoreName) : "debug"));
            sb.AppendLine("size " + sum.totalSize + " bytes, " + sum.totalTime.TotalSeconds.ToString("0") + " s, errors " + sum.totalErrors + ", warnings " + sum.totalWarnings);
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) sb.AppendLine("ERROR " + m.content);
            File.WriteAllText(outFile, sb.ToString());
        }
        catch (Exception e)
        {
            File.WriteAllText(outFile, "exception " + e);
        }
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < next) return;
        next = now + 1;
        try
        {
            // Logs/autopilot-stop.txt: leave Play mode (a game left playing, or one that broke)
            string stop = Path.Combine(Logs, "autopilot-stop.txt");
            if (File.Exists(stop))
            {
                File.Delete(stop);
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string build = Path.Combine(Logs, "autopilot-build.txt");
            if (File.Exists(build) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                string kind = File.ReadAllText(build).Trim();
                File.Delete(build);
                // straight away: an unfocused editor can go minutes between delayed calls
                Build(kind == "aab");
                return;
            }
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
