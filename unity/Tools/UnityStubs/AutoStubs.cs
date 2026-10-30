namespace UnityEngine
{
    public enum LogType { Error = 0, Assert = 1, Warning = 2, Log = 3, Exception = 4 }
    public static partial class Application
    {
        public delegate void LogCallback(string condition, string stackTrace, LogType type);
        public static event LogCallback logMessageReceived;
        public static string consoleLogPath => null;
        public static string dataPath => null;
        public static bool runInBackground { get; set; }
        public static string unityVersion => null;
    }
    public partial class MonoBehaviour
    {
        public void Invoke(string methodName, float time) { }
    }
}
namespace UnityEditor
{
    public sealed partial class EditorApplication
    {
        public static bool isPlaying { get; set; }
        public static bool isPlayingOrWillChangePlaymode => false;
        public static bool isCompiling => false;
        public static bool isUpdating => false;
        public static double timeSinceStartup => 0;
        public static CallbackFunction update;
    }
    public static class SessionState
    {
        public static bool GetBool(string key, bool defaultValue) => defaultValue;
        public static void SetBool(string key, bool value) { }
        public static float GetFloat(string key, float defaultValue) => defaultValue;
        public static void SetFloat(string key, float value) { }
    }
    public enum ImportAssetOptions { Default = 0, ForceUpdate = 1 }
    public partial class AssetDatabase
    {
        public static void Refresh(ImportAssetOptions options) { }
        public static void SaveAssetIfDirty(UnityEngine.Object o) { }
    }
    public static class EditorPrefs
    {
        public static void SetInt(string key, int value) { }
        public static int GetInt(string key, int defaultValue) => defaultValue;
    }
    public partial class EditorUtility
    {
        public static bool scriptCompilationFailed => false;
    }
    public static partial class PlayerSettings
    {
        public static bool runInBackground { get; set; }
    }
}
namespace UnityEngine
{
    [System.Flags] public enum EventModifiers { None = 0, Shift = 1, Control = 2, Alt = 4, Command = 8, Numeric = 16, CapsLock = 32, FunctionKey = 64 }
}
namespace UnityEngine.UIElements
{
    public abstract partial class KeyboardEventBase<T>
    {
        public static T GetPooled(char c, KeyCode keyCode, EventModifiers modifiers) { return null; }
    }
    public partial interface IPanel { FocusController focusController { get; } }
    public partial class FocusController { public Focusable focusedElement => null; }
    public abstract partial class CallbackEventHandler
    {
        public virtual void SendEvent(EventBase e) { }
    }
}

namespace UnityEditor
{
    // the Android build the autopilot can run (Assets/Editor/LogExport/Autopilot.cs)
    public enum BuildTargetGroup { Unknown = 0, Standalone = 1, iOS = 4, Android = 7 }
    public enum BuildTarget { StandaloneWindows64 = 19, iOS = 9, Android = 13 }
    [System.Flags] public enum BuildOptions { None = 0 }
    public struct BuildPlayerOptions
    {
        public string[] scenes { get; set; }
        public string locationPathName { get; set; }
        public BuildTarget target { get; set; }
        public BuildTargetGroup targetGroup { get; set; }
        public BuildOptions options { get; set; }
    }
    public sealed class EditorBuildSettingsScene { public bool enabled { get; set; } public string path { get; set; } }
    public sealed class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes { get; set; } }
    public sealed class EditorUserBuildSettings { public static bool buildAppBundle { get; set; } }
    public sealed class BuildPipeline
    {
        public static bool IsBuildTargetSupported(BuildTargetGroup g, BuildTarget t) { return false; }
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o) { return null; }
    }
}
namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown = 0, Succeeded = 1, Failed = 2, Cancelled = 3 }
    public struct BuildSummary
    {
        public BuildResult result { get; }
        public ulong totalSize { get; }
        public System.TimeSpan totalTime { get; }
        public int totalErrors { get; }
        public int totalWarnings { get; }
    }
    public struct BuildStepMessage { public UnityEngine.LogType type { get; } public string content { get; } }
    public struct BuildStepInfo { public BuildStepMessage[] messages { get; } }
    public sealed class BuildReport { public BuildSummary summary { get; } public BuildStepInfo[] steps { get; } }
}

namespace UnityEditor
{
    public static partial class PlayerSettings
    {
        public static void SetUseDefaultGraphicsAPIs(BuildTarget t, bool v) { }
        public static void SetGraphicsAPIs(BuildTarget t, UnityEngine.Rendering.GraphicsDeviceType[] apis) { }
    }
}
namespace UnityEngine.Rendering
{
    public enum GraphicsDeviceType { OpenGLES3 = 11, Vulkan = 21, Metal = 16 }
}

namespace UnityEditor
{
    public static partial class PlayerSettings
    {
        public static string GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget t) { return ""; }
        public static void SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget t, string defines) { }
    }
}
namespace UnityEditor.Build
{
    public struct NamedBuildTarget
    {
        public static readonly NamedBuildTarget Android = default, iOS = default, Standalone = default, Unknown = default;
    }
}
namespace UnityEngine
{
    public class Coroutine { }
    public class CustomYieldInstruction { }
    public sealed class WaitForSecondsRealtime : CustomYieldInstruction { public WaitForSecondsRealtime(float time) { } }
    public partial class MonoBehaviour
    {
        public Coroutine StartCoroutine(System.Collections.IEnumerator routine) { return null; }
    }
}
namespace UnityEngine
{
    public sealed class WaitForEndOfFrame { }
    public sealed partial class Texture2D
    {
        public Color32[] GetPixels32() => null;
        public void SetPixels32(Color32[] c) { }
        public void Apply(bool updateMipmaps) { }
    }
}
namespace UnityEditor
{
    public enum VideoBitrateMode { Low, Medium, High }
}
namespace UnityEditor.Media
{
    public struct MediaRational { public MediaRational(int n) { } }
    public struct VideoTrackAttributes
    {
        public MediaRational frameRate; public uint width; public uint height; public bool includeAlpha; public UnityEditor.VideoBitrateMode bitRateMode;
    }
    public class MediaEncoder : System.IDisposable
    {
        public MediaEncoder(string path, VideoTrackAttributes v) { }
        public bool AddFrame(UnityEngine.Texture2D t) => true;
        public void Dispose() { }
    }
}
namespace UnityEditor
{
    public enum AndroidSdkVersions { AndroidApiLevelAuto = 0 }
    public static partial class PlayerSettings
    {
        public static string bundleVersion { get; set; }
        public static class Android
        {
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static bool useCustomKeystore { get; set; }
            public static string keystoreName { get; set; }
            public static string keystorePass { get; set; }
            public static string keyaliasName { get; set; }
            public static string keyaliasPass { get; set; }
            public static int bundleVersionCode { get; set; }
        }
    }
}

namespace UnityEditor
{
    public enum IconKind { Any = -1, Application = 0 }
    public static partial class PlayerSettings
    {
        public static UnityEngine.Texture2D[] GetIcons(UnityEditor.Build.NamedBuildTarget t, IconKind k) => null;
        public static void SetIcons(UnityEditor.Build.NamedBuildTarget t, UnityEngine.Texture2D[] i, IconKind k) { }
    }
}
