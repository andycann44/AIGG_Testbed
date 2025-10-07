#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Text.RegularExpressions;
using System.Reflection;

namespace Aim2Pro.AIGG.Tools
{
    public static class WorkbenchDryRun
    {
        [MenuItem("Window/Aim2Pro/Workbench/Apply Canonical (Dry-Run)")]
        public static void ApplyDryRun()
        {
            var wbType = Type.GetType("Aim2Pro.AIGG.WorkbenchWindow");
            string json = "";
            if (wbType != null)
            {
                var wb = EditorWindow.GetWindow(wbType, false, "Workbench", false);
                var f = wbType.GetField("_json", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? wbType.GetField("json", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (wb != null && f != null)
                    json = f.GetValue(wb) as string ?? "";
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[AIGG] Dry-Run: No JSON in Workbench. Parse NL first.");
                return;
            }

            int straights = Count(json, "\"straight\"");
            int lefts     = Count(json, "\"left\"");
            int rights    = Count(json, "\"right\"");
            int curves    = Count(json, "\"curve\"");
            int gaps      = Count(json, "\"gap\"");
            int chicanes  = Count(json, "\"chicane\"");

            double totalLen = 0;
            foreach (Match m in Regex.Matches(json, "\"length_m\"\\s*:\\s*([0-9]+(\\.[0-9]+)?)"))
                if (double.TryParse(m.Groups[1].Value, out var v)) totalLen += v;

            Debug.Log("[AIGG Dry-Run]\n" +
                      "  Segments:\n" +
                      $"    straight x{straights}\n" +
                      $"    left     x{lefts}\n" +
                      $"    right    x{rights}\n" +
                      $"    curve    x{curves}\n" +
                      $"    gap      x{gaps}\n" +
                      $"    chicane  x{chicanes}\n" +
                      $"  Total length (m): {totalLen}");
        }

        static int Count(string s, string needle)
        {
            int c=0, i=0; 
            while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { c++; i += needle.Length; }
            return c;
        }
    }
}
#endif
