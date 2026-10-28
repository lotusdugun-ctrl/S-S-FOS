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
