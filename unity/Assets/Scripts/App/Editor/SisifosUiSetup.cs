#if UNITY_EDITOR
// The one thing the UI cannot set up from player code (see UI/UiKit.cs, "Complex scripts"):
// Unity 6.0's Advanced Text Generator needs ICU data, which only the editor can put into a build —
// by assigning its built-in "icudt73l.bytes" to a PanelSettings ASSET while Project Settings >
// UI Toolkit > "Enable Advanced Text" is on. This runs whenever scripts load in the editor: it turns
// that setting on and keeps Assets/Resources/UI/SisifosPanel.asset (loaded by UiKit.Init) with the
// ICU data and Unity's default runtime theme assigned. Nothing to click.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sisifos.App.EditorTools
{
    [InitializeOnLoad]
    static class SisifosUiSetup
    {
        const string DIR = "Assets/Resources/UI";
        const string PATH = DIR + "/SisifosPanel.asset";
        const BindingFlags ANY = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static SisifosUiSetup()
        {
            EditorApplication.delayCall += Run;
        }

        static Type Find(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        [MenuItem("Sisifos/Set up UI text (ICU data)")]
        static void Run()
        {
            try
            {
                // 1. Project Settings > UI Toolkit > Enable Advanced Text
                var settings = Find("UnityEditor.UIElements.UIToolkitProjectSettings");
                var prop = settings?.GetProperty("enableAdvancedText", ANY);
                if (prop != null && !(bool)prop.GetValue(null)) prop.SetValue(null, true);
                else if (prop == null) Debug.Log("Sisifos: UIToolkitProjectSettings.enableAdvancedText not found (a newer Unity may not need it)");

                // 2. the PanelSettings asset that carries the ICU data into the build
                var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PATH);
                if (ps == null)
                {
                    Directory.CreateDirectory(DIR);
                    ps = ScriptableObject.CreateInstance<PanelSettings>();
                    AssetDatabase.CreateAsset(ps, PATH);
                }
                // Unity's default runtime theme (Assets/UI Toolkit/UnityThemes), for the inner
                // layout of the stock controls (TextField, the book's ScrollView); every look is
                // set inline by the game's code
                if (ps.themeStyleSheet == null)
                {
                    var tss = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
                    if (tss != null) { ps.themeStyleSheet = tss; EditorUtility.SetDirty(ps); AssetDatabase.SaveAssets(); }
                }
                var field = typeof(PanelSettings).GetField("m_ICUDataAsset", ANY);
                if (field != null && field.GetValue(ps) == null)
                {
                    typeof(PanelSettings).GetMethod("AssignICUData", ANY)?.Invoke(ps, null);
                    if (field.GetValue(ps) == null)
                    {
                        var icu = AssetDatabase.GetBuiltinExtraResource<UnityEngine.TextAsset>("icudt73l.bytes");
                        if (icu != null) field.SetValue(ps, icu);
                    }
                    EditorUtility.SetDirty(ps);
                    AssetDatabase.SaveAssets();
                }
                if (field != null && field.GetValue(ps) == null)
                    Debug.LogWarning("Sisifos: could not assign the ICU data to " + PATH + "; Arabic, Hindi and Bengali will not be shaped in builds.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
#endif
