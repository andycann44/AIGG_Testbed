using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    internal static class PasteMergeTempBridge
    {
        static readonly string Root = "Assets/AIGG/Temp";
        static readonly string[] Slots = new[]{
            "intents","lexicon","macros","commands","fieldmap","registry","schema","router",
            "nl","canonical","diagnostics","aliases","shims","nullable","overrides"
        };

        static string PathFor(string tag) => System.IO.Path.Combine(Root, $"temp_{tag}.json");
        static string Read(string tag)
        {
            var p = PathFor(tag);
            try { return File.Exists(p) ? File.ReadAllText(p) : ""; }
            catch { return ""; }
        }

        static void OpenOriginal() {
            // Best-effort: find and show SpecPasteMergeWindow
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(x => x.Name == "SpecPasteMergeWindow" && typeof(EditorWindow).IsAssignableFrom(x));
            if (t != null) EditorWindow.GetWindow(t);
            else Debug.LogWarning("[Paste&Merge Bridge] Could not find SpecPasteMergeWindow. Open it manually if needed.");
        }

        static void SendJsonToOriginal(string json)
        {
            // Prefer a method called OpenWithJson(json) if present
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(x => x.Name == "SpecPasteMergeWindow");

            if (t != null)
            {
                var m = t.GetMethod("OpenWithJson", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance);
                if (m != null)
                {
                    try {
                        if (m.IsStatic) m.Invoke(null, new object[] { json });
                        else {
                            var inst = ScriptableObject.CreateInstance(t);
                            m.Invoke(inst, new object[] { json });
                        }
                        Debug.Log("[Paste&Merge Bridge] Routed JSON via OpenWithJson(json).");
                        return;
                    } catch (Exception ex) {
                        Debug.LogWarning("[Paste&Merge Bridge] OpenWithJson failed: " + ex.Message);
                    }
                }
            }

            // Fallback: put on clipboard
            EditorGUIUtility.systemCopyBuffer = json ?? "";
            Debug.Log("[Paste&Merge Bridge] Copied JSON to clipboard. Paste into the Pasted Content box.");
        }

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/Refresh List", priority=1)]
        static void RefreshMenu() {
            Debug.Log("[Paste&Merge Bridge] Menu refreshed. (Unity rebuilds menu on domain reload automatically.)");
        }

        // Generate one menu item per slot
        // (Unity requires constant paths, so we just write them all out.)
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/intents", false, 20)]  static void M0() => Run("intents");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/lexicon", false, 21)]  static void M1() => Run("lexicon");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/macros", false, 22)]   static void M2() => Run("macros");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/commands", false, 23)] static void M3() => Run("commands");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/fieldmap", false, 24)] static void M4() => Run("fieldmap");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/registry", false, 25)] static void M5() => Run("registry");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/schema", false, 26)]   static void M6() => Run("schema");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/router", false, 27)]   static void M7() => Run("router");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/nl", false, 28)]       static void M8() => Run("nl");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/canonical", false, 29)]static void M9() => Run("canonical");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/diagnostics", false, 30)] static void M10()=> Run("diagnostics");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/aliases", false, 31)]  static void M11()=> Run("aliases");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/shims", false, 32)]    static void M12()=> Run("shims");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/nullable", false, 33)] static void M13()=> Run("nullable");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/overrides", false, 34)]static void M14()=> Run("overrides");

        static void Run(string tag)
        {
            var json = Read(tag);
            OpenOriginal();
            SendJsonToOriginal(json);
        }
    }
}
