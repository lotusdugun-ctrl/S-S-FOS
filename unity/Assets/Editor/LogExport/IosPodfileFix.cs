#if UNITY_IOS
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// The Podfile the External Dependency Manager writes, made fit for today's Xcode, between the
/// step that writes it (40) and the one that runs `pod install` (50):
/// - only the CocoaPods CDN as a source: the GitHub Specs repo makes `pod install` fetch the
///   whole spec archive with git, which takes many minutes;
/// - every pod built for iOS 15.0 (the game's own minimum): some pods ask for 12.0, which the
///   new Xcode no longer accepts, and Archive stops on it.
/// Writes nothing when the Podfile is missing, and never adds the hook twice.
/// </summary>
static class SisifosIosPodfileFix
{
    const string Target = "15.0";
    const string Cdn = "https://cdn.cocoapods.org/";
    const string Mark = "# Sisifos: pods built for iOS " + Target;

    [PostProcessBuild(45)]
    static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string podfile = Path.Combine(path, "Podfile");
        if (!File.Exists(podfile)) return;
        try
        {
            string s = File.ReadAllText(podfile);
            string before = s;

            // the slow git source out, the CDN in (once, at the top)
            s = Regex.Replace(s, @"^\s*source\s+['""]https://github\.com/CocoaPods/Specs(\.git)?/?['""]\s*\r?\n", "", RegexOptions.Multiline);
            if (!Regex.IsMatch(s, @"^\s*source\s+['""]" + Regex.Escape(Cdn), RegexOptions.Multiline))
                s = "source '" + Cdn + "'\n" + s;

            // the platform line, if there is one, at the game's minimum
            s = Regex.Replace(s, @"^(\s*platform\s+:ios\s*,\s*)['""][0-9.]+['""]", "${1}'" + Target + "'", RegexOptions.Multiline);

            // every pod target at the same minimum
            if (!s.Contains(Mark))
            {
                string body =
                    "  installer.pods_project.targets.each do |t|\n" +
                    "    t.build_configurations.each do |c|\n" +
                    "      c.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '" + Target + "'\n" +
                    "    end\n" +
                    "  end\n";
                var hook = new Regex(@"^(\s*post_install\s+do\s*\|\s*(\w+)\s*\|\s*\r?\n)", RegexOptions.Multiline);
                var m = hook.Match(s);
                if (m.Success)
                {
                    // a hook is there already (a Podfile may have only one): add to it
                    string v = m.Groups[2].Value;
                    s = s.Insert(m.Index + m.Length, "  " + Mark + "\n" + body.Replace("installer.", v + "."));
                }
                else
                {
                    s = s.TrimEnd() + "\n\n" + Mark + "\npost_install do |installer|\n" + body + "end\n";
                }
            }

            if (s != before)
            {
                File.WriteAllText(podfile, s);
                Debug.Log("[iOS] Podfile: CDN source only, pods at iOS " + Target);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[iOS] Podfile fix: " + e.Message);
        }
    }
}
#endif
