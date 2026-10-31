using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Wires up the Google Mobile Ads plugin (Packages/manifest.json: com.google.ads.mobile,
/// from OpenUPM) without anyone opening a settings window:
///  - SISIFOS_ADS is defined for Android, iOS and the editor only while the plugin's API is
///    actually compiled in, so the game builds the same with or without it;
///  - the AdMob App IDs below are written into the plugin's settings asset.
/// Lives in this dependency-free assembly so it runs even while the game does not compile.
///
/// The App IDs here are the owner's own AdMob apps (Android and iOS); the ad unit ids are
/// in Assets/Scripts/App/Ads/Ads.cs.
/// </summary>
[InitializeOnLoad]
static class SisifosAdsSetup
{
    const string AndroidAppId = "ca-app-pub-5494310464478030~9093316918";
    const string IosAppId = "ca-app-pub-5494310464478030~1925273331";
    const string Define = "SISIFOS_ADS";

    static SisifosAdsSetup()
    {
        EditorApplication.delayCall += Apply;
    }

    static Type FindType(string name)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = null;
            try { t = a.GetType(name, false); } catch (Exception) { }
            if (t != null) return t;
        }
        return null;
    }

    internal static void Apply()
    {
        // never in Play mode: a define changed there recompiles mid-game and the running
        // game is lost (its statics go with the domain)
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            bool plugin = FindType("GoogleMobileAds.Api.MobileAds") != null && FindType("GoogleMobileAds.Ump.Api.ConsentInformation") != null;
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
            {
                var defs = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(d => d.Length > 0).ToList();
                bool has = defs.Contains(Define);
                if (plugin && !has) defs.Add(Define);
                else if (!plugin && has) defs.Remove(Define);
                else continue;
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defs));
            }
            if (plugin) WriteAppIds();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Ads] setup: " + e.Message);
        }
    }

    /// <summary>the plugin's own settings asset (Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset)</summary>
    static void WriteAppIds()
    {
        var st = FindType("GoogleMobileAds.Editor.GoogleMobileAdsSettings");
        var load = st?.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var inst = load != null ? load.Invoke(null, null) as UnityEngine.Object : null;
        if (inst == null)
        {
            // the plugin's settings class could not be reached: write the asset's two lines
            WriteAppIdsToFile();
            return;
        }
        bool changed = false;
        changed |= Set(st, inst, "GoogleMobileAdsAndroidAppId", "adMobAndroidAppId", AndroidAppId);
        changed |= Set(st, inst, "GoogleMobileAdsIOSAppId", "adMobIOSAppId", IosAppId);
        if (changed)
        {
            EditorUtility.SetDirty(inst);
            // only this asset: SaveAssets() also tried to write the plugin's own placeholder
            // prefabs inside its read-only package, three errors every time
            AssetDatabase.SaveAssetIfDirty(inst);
            Debug.Log("[Ads] AdMob App IDs written to the plugin settings");
        }
    }

    const string SettingsAsset = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

    static void WriteAppIdsToFile()
    {
        if (!System.IO.File.Exists(SettingsAsset))
        {
            Debug.LogWarning("[Ads] no " + SettingsAsset + "; set the App IDs in Assets > Google Mobile Ads > Settings");
            return;
        }
        string text = System.IO.File.ReadAllText(SettingsAsset);
        string next = System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^(\s*adMobAndroidAppId:).*$", "$1 " + AndroidAppId);
        next = System.Text.RegularExpressions.Regex.Replace(next, @"(?m)^(\s*adMobIOSAppId:).*$", "$1 " + IosAppId);
        if (next == text) return;
        System.IO.File.WriteAllText(SettingsAsset, next);
        AssetDatabase.ImportAsset(SettingsAsset, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Ads] AdMob App IDs written to " + SettingsAsset);
    }

    static bool Set(Type st, object inst, string prop, string field, string value)
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var p = st.GetProperty(prop, BF);
        if (p != null && p.CanRead && p.CanWrite)
        {
            if ((p.GetValue(inst) as string) == value) return false;
            p.SetValue(inst, value);
            return true;
        }
        var f = st.GetField(field, BF);
        if (f != null)
        {
            if ((f.GetValue(inst) as string) == value) return false;
            f.SetValue(inst, value);
            return true;
        }
        Debug.LogWarning("[Ads] could not find " + prop + " in the plugin settings; set the App ID in Assets > Google Mobile Ads > Settings");
        return false;
    }
}
