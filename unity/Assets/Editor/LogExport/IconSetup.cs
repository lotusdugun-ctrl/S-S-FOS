using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// The app's icon (Assets/Art): the man and his stone on the slope in the afternoon light,
/// painted by the game itself. Set as the default icon for every platform, on iOS also as
/// the App Store (marketing, 1024 px) icon, and on Android also as the adaptive icon (the picture as the back layer, framed so the launcher's mask
/// keeps him whole; an empty front layer). Runs when the editor loads and again just before
/// every build (a batch-mode build starts before the editor's delayed call ever runs), and only
/// writes when something is missing, so it never fights a choice made by hand.
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

    internal static void Apply()
    {
        try
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Icon);
            if (icon == null) return;
            var cur = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (cur == null || cur.Length == 0 || cur[0] == null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
#if UNITY_IOS
            // iOS makes the iPhone and iPad sizes from the default icon but leaves the App
            // Store's own 1024 px icon empty, and the upload is refused without it (ITMS-91111)
            {
                var kind = UnityEditor.iOS.iOSPlatformIconKind.Marketing;
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                bool changed = false;
                foreach (var pi in icons)
                {
                    var t = pi.GetTextures();
                    if (t == null || t.Length < 1 || t[0] == null)
                    {
                        pi.SetTextures(icon);
                        changed = true;
                    }
                }
                if (changed)
                {
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
                    Debug.Log("[Icon] iOS App Store icon (1024 px) set from " + Icon);
                }
                else if (icons.Length == 0) Debug.LogWarning("[Icon] iOS has no App Store icon slot");
            }
#endif
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
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Icon] setup: " + e.Message);
        }
    }
}

/// <summary>the icons once more just before every build, so a build started from the command line has them too</summary>
sealed class SisifosIconBeforeBuild : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -100; } }
    public void OnPreprocessBuild(BuildReport report) { SisifosIconSetup.Apply(); }
}
