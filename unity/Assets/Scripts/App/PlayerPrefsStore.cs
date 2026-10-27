using Sisifos.Game;
using UnityEngine;

namespace Sisifos.App
{
    /// <summary>
    /// localStorage on PlayerPrefs (SharedPreferences on Android, NSUserDefaults on iOS).
    /// PlayerPrefs is main-thread only: calls from other threads are passed to the main thread.
    /// Writes are flushed when the app is paused or quits (GameRoot).
    /// </summary>
    public sealed class PlayerPrefsStore : IStore
    {
        // keys are namespaced so they cannot collide with Unity's own prefs
        const string Prefix = "sisifos.";

        public string Get(string key)
        {
            if (!MainThread.IsMain) return MainThread.Invoke(() => Get(key));
            string k = Prefix + key;
            return PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null;
        }

        public void Set(string key, string value)
        {
            if (!MainThread.IsMain) { MainThread.Invoke(() => { Set(key, value); return (string)null; }); return; }
            if (value == null) { Remove(key); return; }
            PlayerPrefs.SetString(Prefix + key, value);
            dirty = true;
        }

        public void Remove(string key)
        {
            if (!MainThread.IsMain) { MainThread.Invoke(() => { Remove(key); return (string)null; }); return; }
            PlayerPrefs.DeleteKey(Prefix + key);
            dirty = true;
        }

        bool dirty;

        public void Flush()
        {
            if (!dirty) return;
            dirty = false;
            PlayerPrefs.Save();
        }
    }
}
