using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    /// <summary>
    /// Utilities to push text into the original Paste & Merge window's "Pasted Content" text field.
    /// Uses reflection, but is read-only/safe for the target window.
    /// </summary>
    internal static class PasteMergeTempBridge
    {
        public const string TempRoot = "Assets/AIGG/Temp";

        // Buckets we expose
        public static readonly string[] Buckets = new[]{
            "intents","lexicon","macros","commands","fieldmap","registry","schema",
            "router","nl","canonical","diagnostics","aliases","shims","nullable","overrides"
        };

        /// <summary>
        /// Loads the temp JSON for a bucket and pushes it into the P&M "Pasted Content" area.
        /// </summary>
        public static void LoadBucketIntoPM(string bucket)
        {
            var path = Path.Combine(TempRoot, $"temp_{bucket}.json").Replace("\\","/");
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Temp not found",
                    $"No file:\n{path}\n\nRun Ask AI or the splitter first.", "OK");
                return;
            }
            var json = File.ReadAllText(path);

            // Ensure window is open
            if (!OpenPasteMergeWindow())
            {
                EditorUtility.DisplayDialog("Paste & Merge not found",
                    "Could not find/open the original Paste & Merge window.", "OK");
                return;
            }

            // Find the P&M window and push the text.
            // We try common names: SpecPasteMergeWindow or any type containing 'PasteMerge'.
            var pmType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => typeof(EditorWindow).IsAssignableFrom(t) &&
                                     (t.Name == "SpecPasteMergeWindow" || t.Name.Contains("PasteMerge")));

            if (pmType == null)
            {
                Debug.LogWarning("[TempBridge] Paste & Merge type not found.");
                return;
            }

            var pm = Resources.FindObjectsOfTypeAll(pmType).FirstOrDefault() as EditorWindow;
            if (pm == null)
            {
                Debug.LogWarning("[TempBridge] Paste & Merge window instance not found.");
                return;
            }

            // Try common private field/property names for the pasted content text.
            // We'll check string fields or properties which look like the pasted box.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            string assignedTo = null;

            // Heuristic: look for fields with "pasted" or "input" in the name, type string.
            var fields = pmType.GetFields(flags)
                .Where(f => f.FieldType == typeof(string))
                .OrderBy(f => f.Name.Length)
                .ToArray();

            var props = pmType.GetProperties(flags)
                .Where(p => p.PropertyType == typeof(string) && p.CanWrite)
                .OrderBy(p => p.Name.Length)
                .ToArray();

            string[] candidates = { "pasted", "pastedContent", "input", "text", "json" };

            foreach (var f in fields)
            {
                if (candidates.Any(c => f.Name.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    f.SetValue(pm, json);
                    assignedTo = $"field {f.Name}";
                    break;
                }
            }

            if (assignedTo == null)
            {
                foreach (var p in props)
                {
                    if (candidates.Any(c => p.Name.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        p.SetValue(pm, json);
                        assignedTo = $"property {p.Name}";
                        break;
                    }
                }
            }

            // As a last resort, try a method like SetPasted(string)
            if (assignedTo == null)
            {
                var m = pmType.GetMethods(flags).FirstOrDefault(x =>
                    x.GetParameters().Length == 1 &&
                    x.GetParameters()[0].ParameterType == typeof(string) &&
                    (x.Name.IndexOf("SetPasted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     x.Name.IndexOf("LoadFromText", StringComparison.OrdinalIgnoreCase) >= 0)
                );
                if (m != null)
                {
                    m.Invoke(pm, new object[]{ json });
                    assignedTo = $"method {m.Name}(string)";
                }
            }

            if (assignedTo == null)
            {
                // Fallback: copy to clipboard so the user can Cmd+V into the box.
                EditorGUIUtility.systemCopyBuffer = json;
                EditorUtility.DisplayDialog("Could not bind field automatically",
                    "Pasted JSON copied to clipboard.\nPress Cmd+V in the P&M 'Pasted Content' box.",
                    "OK");
                assignedTo = "clipboard";
            }

            pm.Repaint();
            Debug.Log($"[TempBridge] Loaded temp_{bucket}.json into P&M ({assignedTo}).");
        }

        /// <summary>
        /// Tries to open the original Paste & Merge window using the menu path first, then type.
        /// </summary>
        public static bool OpenPasteMergeWindow()
        {
            string[] menuCandidates =
            {
                "Window/Aim2Pro/Aigg/Paste & Merge",
                "Window/Aim2Pro/Aigg/Paste & Merge (Legacy)"
            };
            foreach (var m in menuCandidates)
                if (EditorApplication.ExecuteMenuItem(m)) return true;

            var pmType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => typeof(EditorWindow).IsAssignableFrom(t) &&
                                     (t.Name == "SpecPasteMergeWindow" || t.Name.Contains("PasteMerge")));
            if (pmType != null)
            {
                EditorWindow.GetWindow(pmType, utility:false, title:"Paste & Merge");
                return true;
            }
            return false;
        }
    }
}
