using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    // Stable entry point Workbench calls: PreMergeRouterAPI.Route(json)
    public static class PreMergeRouterAPI
    {
        public static void Route(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[PreMergeRouterAPI] Empty JSON");
                return;
            }

            // Find PreMergeRouterWindow.OpenWithJson(string) via reflection
            Type t = null;
            MethodInfo m = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType("Aim2Pro.AIGG.PreMergeRouterWindow");
                    if (t != null)
                    {
                        m = t.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.Static);
                        break;
                    }
                }
                catch { /* ignore */ }
            }

            if (t != null && m != null)
            {
                try
                {
                    m.Invoke(null, new object[] { json });
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PreMergeRouterAPI] Invoke failed: " + e.Message);
                }
            }

            // Fallback: copy to clipboard so Paste & Merge can be used manually
            EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log("[PreMergeRouterAPI] Router window not found; JSON copied to clipboard.");
        }

        // Legacy overload some codepaths still call
        public static void Route(string json, bool _)
        {
            Route(json);
        }
    }
}
