#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;

namespace Aim2Pro.AIGG.Tools
{
    /// Routes JSON to the existing SpecPasteMergeWindow if it exposes OpenWithJson(json).
    /// If not, it opens the window and copies JSON to the clipboard for a manual paste.
    public static class SpecMergeRouter
    {
        public static void OpenExistingMergeWithJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[AIGG] No JSON to send to SpecPasteMergeWindow.");
                return;
            }

            var t = Type.GetType("SpecPasteMergeWindow");
            if (t == null)
            {
                Debug.LogWarning("[AIGG] SpecPasteMergeWindow type not found.");
                return;
            }

            // Preferred: call a public static OpenWithJson(string) if present
            var openWith = t.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.Static);
            if (openWith != null && openWith.GetParameters().Length == 1 &&
                openWith.GetParameters()[0].ParameterType == typeof(string))
            {
                openWith.Invoke(null, new object[] { json });
                return;
            }

            // Fallback: open the existing window and put JSON on clipboard for paste
            EditorGUIUtility.systemCopyBuffer = json;
            EditorWindow.GetWindow(t, false, "Spec Paste & Merge");
            Debug.Log("[AIGG] JSON copied to clipboard → paste into your existing SpecPasteMergeWindow.");
        }
    }
}
#endif
