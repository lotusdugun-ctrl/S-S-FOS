using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps a copy of the editor's log inside the project (Logs/Editor-copy.log), so whoever helps
/// with the project from outside this computer can read compile errors and exceptions without
/// access to %LOCALAPPDATA%. It lives in its own assembly with no references, so it still
/// compiles while the game's scripts have errors.
/// </summary>
[InitializeOnLoad]
static class SisifosLogExport
{
    static double next;

    static SisifosLogExport()
    {
        Copy();
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < next) return;
        next = now + 3;
        Copy();
    }

    static void Copy()
    {
        try
        {
            string src = Application.consoleLogPath;
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) return;
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
            Directory.CreateDirectory(dir);
            string dst = Path.Combine(dir, "Editor-copy.log");
            using (var i = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var o = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                i.CopyTo(o);
            }
        }
        catch (Exception)
        {
            // the log is busy; the next tick tries again
        }
    }
}
