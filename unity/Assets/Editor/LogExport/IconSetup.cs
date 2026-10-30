using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// The app's icon (Assets/Art): the man and his stone on the slope in the afternoon light,
/// painted by the game itself. Set as the default icon for every platform, and on Android
/// also as the adaptive icon (the picture as the back layer, framed so the launcher's mask
/// keeps him whole; an empty front layer). Runs when the editor loads, and only writes
/// when something is missing, so it never fights a choice made by hand.
/// </summary>
[InitializeOnLoad]
static class SisifosIconSetup
{
    const string Icon = "Assets/Art/AppIcon.png";
    const string Back = "Assets/Art/AppIconAdaptiveBack.png";
    const string Fore = "Assets/Art/AppIconAdaptiveFore.png";

    static SisifosIconSetup()
    {
        EditorApplication.delayCall += Apply;
    }

    static void Apply()
    {
        try
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Icon);
            if (icon == null) return;
            var cur = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (cur == null || cur.Length == 0 || cur[0] == null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
#if UNITY_ANDROID
            var back = AssetDatabase.LoadAssetAtPath<Texture2D>(Back);
            var fore = AssetDatabase.LoadAssetAtPath<Texture2D>(Fore);
            if (back != null && fore != null)
            {
                var kind = UnityEditor.Android.AndroidPlatformIconKind.Adaptive;
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                bool changed = false;
                foreach (var pi in icons)
                {
                    var t = pi.GetTextures();
                    if (t == null || t.Length < 2 || t[0] == null)
                    {
                        pi.SetTextures(back, fore);
                        changed = true;
                    }
                }
                if (changed) PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
                // the round and legacy launchers: the whole picture
                foreach (var k in new[] { UnityEditor.Android.AndroidPlatformIconKind.Round, UnityEditor.Android.AndroidPlatformIconKind.Legacy })
                {
                    var list = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, k);
                    bool ch = false;
                    foreach (var pi in list)
                    {
                        var t = pi.GetTextures();
                        if (t == null || t.Length < 1 || t[0] == null) { pi.SetTextures(icon); ch = true; }
                    }
                    if (ch) PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, k, list);
                }
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Icon] setup: " + e.Message);
        }
    }
}
