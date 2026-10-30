#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// The Xcode project's Info.plist, as the App Store wants it: the app uses no encryption
/// beyond the system's own (so no export compliance questions on every upload), and it is a
/// landscape game for iPhone and iPad. The AdMob App ID and the SKAdNetwork list are written
/// by the Google Mobile Ads plugin's own post-build step.
/// </summary>
static class SisifosIosPostBuild
{
    [PostProcessBuild(200)]
    static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        root.SetBoolean("UIRequiresFullScreen", true);
        plist.WriteToFile(plistPath);
    }
}
#endif
