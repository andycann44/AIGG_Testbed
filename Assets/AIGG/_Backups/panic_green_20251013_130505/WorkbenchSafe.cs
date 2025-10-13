using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG.Workbench
{
    internal static class WorkbenchSafe
    {
        // Try call Aim2Pro.AIGG.AIGG_LocalIntentEngine.RunToJson(string)
        public static (bool ok, string payload) TryParseLocal(string nl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nl)) return (false, "[WB] NL empty.");
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Aim2Pro.AIGG.AIGG_LocalIntentEngine");
                    if (t == null) continue;
                    var m = t.GetMethod("RunToJson", BindingFlags.Public | BindingFlags.Static);
                    if (m == null) break;
                    var json = m.Invoke(null, new object[]{ nl }) as string ?? "";
                    if (string.IsNullOrWhiteSpace(json)) return (false, "[WB] Local engine returned empty JSON.");
                    return (true, json);
                }
                return (false, "[WB] Local intent engine not found (AIGG_LocalIntentEngine.RunToJson).");
            }
            catch (Exception e) { return (false, "[WB] Local parse failed: " + e.Message); }
        }

        // Try forward to SpecPasteMergeWindow.OpenWithJson(json); else copy to clipboard
        public static string RouteToPasteAndMerge(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return "[WB] No JSON to route.";
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Aim2Pro.AIGG.SpecPasteMergeWindow");
                    if (t == null) continue;
                    var m = t.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.Static);
                    if (m != null)
                    {
                        m.Invoke(null, new object[]{ json });
                        return "[WB] Routed via SpecPasteMergeWindow.OpenWithJson";
                    }
                }
                EditorGUIUtility.systemCopyBuffer = json;
                return "[WB] Paste & Merge not  JSON copied to clipboard.";found 
            }
            catch (Exception e)
            {
                EditorGUIUtility.systemCopyBuffer = json;
                return "[WB] Route  JSON copied to clipboard: " + e.Message;failed 
            }
        }
    }
}
