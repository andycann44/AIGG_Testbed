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
        static string Read(string tag) { var p = PathFor(tag); return File.Exists(p) ? File.ReadAllText(p) : ""; }

        static EditorWindow OpenOriginalWindow(out Type t)
        {
            t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(x => x.Name == "SpecPasteMergeWindow" && typeof(EditorWindow).IsAssignableFrom(x));
            if (t != null) return EditorWindow.GetWindow(t);
            Debug.LogWarning("[Paste&Merge Bridge] SpecPasteMergeWindow not found.");
            return null;
        }

        static bool TrySetPastedContent(EditorWindow win, Type t, string json)
        {
            if (win == null || t == null) return false;

            // Heuristic: find a string field that likely backs the big "Pasted Content" TextArea
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = t.GetFields(BF).FirstOrDefault(f =>
                f.FieldType == typeof(string) &&
                (f.Name.IndexOf("paste", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 f.Name.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 f.Name.IndexOf("content", StringComparison.OrdinalIgnoreCase) >= 0));

            if (field != null)
            {
                field.SetValue(win, json ?? "");
                win.Repaint();
                Debug.Log($"[Paste&Merge Bridge] Injected JSON via field '{field.Name}'.");
                return true;
            }

            // Fallback 1: look for a property with similar name
            var prop = t.GetProperties(BF).FirstOrDefault(p =>
                p.CanWrite && p.PropertyType == typeof(string) &&
                (p.Name.IndexOf("paste", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 p.Name.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 p.Name.IndexOf("content", StringComparison.OrdinalIgnoreCase) >= 0));

            if (prop != null)
            {
                try { prop.SetValue(win, json ?? ""); win.Repaint(); Debug.Log($"[Paste&Merge Bridge] Injected JSON via property '{prop.Name}'."); return true; }
                catch { /* ignore */ }
            }

            // Fallback 2: look for a method like SetPastedText(string) or OpenWithJson(string)
            var m = t.GetMethods(BF).FirstOrDefault(mi =>
                mi.GetParameters().Length == 1 &&
                mi.GetParameters()[0].ParameterType == typeof(string) &&
                (mi.Name.IndexOf("OpenWithJson", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 mi.Name.IndexOf("SetPasted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 mi.Name.IndexOf("LoadJson", StringComparison.OrdinalIgnoreCase) >= 0));

            if (m != null)
            {
                try { m.Invoke(win, new object[]{ json ?? "" }); win.Repaint(); Debug.Log($"[Paste&Merge Bridge] Injected JSON via method '{m.Name}'."); return true; }
                catch { /* ignore */ }
            }

            // Final fallback: clipboard
            EditorGUIUtility.systemCopyBuffer = json ?? "";
            Debug.Log("[Paste&Merge Bridge] Copied JSON to clipboard (paste into Pasted Content).");
            return false;
        }

        static void Run(string tag)
        {
            var json = Read(tag);
            var win = OpenOriginalWindow(out var t);
            if (TrySetPastedContent(win, t, json))
            {
                // keep focus on the original window
                if (win != null) win.Focus();
            }
        }

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/intents", false, 20)]  static void M0()  => Run("intents");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/lexicon", false, 21)]  static void M1()  => Run("lexicon");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/macros", false, 22)]   static void M2()  => Run("macros");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/commands", false, 23)] static void M3()  => Run("commands");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/fieldmap", false, 24)] static void M4()  => Run("fieldmap");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/registry", false, 25)] static void M5()  => Run("registry");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/schema", false, 26)]   static void M6()  => Run("schema");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/router", false, 27)]   static void M7()  => Run("router");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/nl", false, 28)]       static void M8()  => Run("nl");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/canonical", false, 29)]static void M9()  => Run("canonical");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/diagnostics", false, 30)] static void M10() => Run("diagnostics");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/aliases", false, 31)]  static void M11() => Run("aliases");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/shims", false, 32)]    static void M12() => Run("shims");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/nullable", false, 33)] static void M13() => Run("nullable");
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/overrides", false, 34)]static void M14() => Run("overrides");
    }
}
