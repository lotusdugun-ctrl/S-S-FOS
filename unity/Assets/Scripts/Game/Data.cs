using System;
using System.Collections.Generic;

namespace Sisifos.Game
{
    /// <summary>
    /// The game's bundled text data (Assets/Resources/Data/...). The host decides how a path is
    /// read: Unity loads a TextAsset from Resources, the headless harness reads the file.
    /// Paths have no extension, e.g. "story-text/en" or "quotes-data/authors".
    /// </summary>
    public static class Data
    {
        public static Func<string, string> loader;
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        public static string Text(string path)
        {
            lock (cache)
            {
                if (cache.TryGetValue(path, out var s)) return s;
            }
            if (loader == null) throw new InvalidOperationException("Data.loader not set");
            var t = loader(path);
            if (t == null) throw new InvalidOperationException("missing data: " + path);
            lock (cache) cache[path] = t;
            return t;
        }
    }

    /// <summary>
    /// What the web build kept in localStorage: string values under string keys. The host
    /// supplies the storage (PlayerPrefs / a file in Unity, a dictionary in tests).
    /// </summary>
    public interface IStore
    {
        string Get(string key);
        void Set(string key, string value);
        void Remove(string key);
    }

    public static class Store
    {
        public static IStore impl = new MemoryStore();
        public static string Get(string key) { return impl.Get(key); }
        public static void Set(string key, string value) { impl.Set(key, value); }
        public static void Remove(string key) { impl.Remove(key); }
    }

    public sealed class MemoryStore : IStore
    {
        readonly Dictionary<string, string> d = new Dictionary<string, string>();
        public string Get(string key) { return d.TryGetValue(key, out var v) ? v : null; }
        public void Set(string key, string value) { d[key] = value; }
        public void Remove(string key) { d.Remove(key); }
    }
}
