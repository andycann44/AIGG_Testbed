using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public static class PreMergeRouterAPI
    {
        public static void Route(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[PreMergeRouterAPI] Empty JSON");
                return;
            }

            // Prefer new center
            if (CallStatic("Aim2Pro.AIGG.PreMergeCenterWindow", "OpenWithJson", json))
                return;

            // Or legacy router window
            if (CallStatic("Aim2Pro.AIGG.PreMergeRouterWindow", "OpenWithJson", json))
                return;

            // Fallback: clipboard
            EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log("[PreMergeRouterAPI] No Pre-Merge window found; JSON copied to clipboard.");
        }

        public static void Route(string json, bool _) { Route(json); }

        private static bool CallStatic(string typeName, string methodName, string arg)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(typeName);
                    if (t == null) continue;
                    var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                    if (m == null) continue;
                    m.Invoke(null, new object[] { arg });
                    return true;
                }
                catch { }
            }
            return false;
        }
    }
}
