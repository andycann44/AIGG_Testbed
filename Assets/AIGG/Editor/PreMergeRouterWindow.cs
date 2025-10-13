// PreMergeRouterWindow.cs
// Menu: Window/Aim2Pro/Aigg/Pre-Merge Router
// Purpose: Accept JSON, forward to SpecPasteMergeWindow.OpenWithJson if available,
// else copy to clipboard as fallback.

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public class PreMergeRouterWindow : EditorWindow
    {
        private string _json = "";
        private Vector2 _scroll;

        [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge Router", priority = 2000)]
        public static void ShowWindow()
        {
            var win = GetWindow<PreMergeRouterWindow>("Pre-Merge Router");
            win.minSize = new Vector2(520, 360);
            win.Show();
        }

        /// <summary>
        /// Open the router window with JSON preloaded and auto-forward.
        /// </summary>
        public static void OpenWithJson(string json)
        {
            var win = GetWindow<PreMergeRouterWindow>("Pre-Merge Router");
            win._json = json ?? "";
            win.Show();
            win.Repaint();
            // Auto-forward once content is set
            win.ForwardToPasteAndMerge(json);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Canonical JSON Input", EditorStyles.boldLabel);
            using (var s = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.ExpandHeight(true)))
            {
                _scroll = s.scrollPosition;
                _json = EditorGUILayout.TextArea(_json, GUILayout.ExpandHeight(true));
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Forward to Paste & Merge", GUILayout.Height(28)))
                {
                    ForwardToPasteAndMerge(_json);
                }
                if (GUILayout.Button("Copy JSON", GUILayout.Height(28)))
                {
                    EditorGUIUtility.systemCopyBuffer = _json ?? "";
                    ShowStatus("JSON copied to clipboard");
                }
            }
        }

        private void ForwardToPasteAndMerge(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                ShowStatus("No JSON to forward.");
                return;
            }

            // Try to locate SpecPasteMergeWindow and call OpenWithJson(string)
            var asm = typeof(EditorWindow).Assembly; // search all loaded assemblies instead of only UnityEditor
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            Type targetType = null;
            MethodInfo openWithJson = null;

            foreach (var a in allAssemblies)
            {
                // Type name preferred: Aim2Pro.AIGG.SpecPasteMergeWindow
                targetType = a.GetType("Aim2Pro.AIGG.SpecPasteMergeWindow");
                if (targetType != null)
                {
                    openWithJson = targetType.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.Static);
                    if (openWithJson != null) break;
                    // If no static method, try instance fallback later.
                    break;
                }
            }

            if (targetType != null)
            {
                try
                {
                    if (openWithJson != null)
                    {
                        openWithJson.Invoke(null, new object[] { json });
                        ShowStatus("Routed via SpecPasteMergeWindow.OpenWithJson");
                        return;
                    }
                    else
                    {
                        // Instance fallback: open the window then try a SetJson(string) if present
                        var win = EditorWindow.GetWindow(targetType, false, "Paste & Merge", true);
                        var setJson = targetType.GetMethod("SetJson", BindingFlags.Public | BindingFlags.Instance);
                        if (setJson != null) setJson.Invoke(win, new object[] { json });
                        ShowStatus("Opened SpecPasteMergeWindow (fallback).");
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PreMergeRouter] Reflection route failed: " + e.Message);
                }
            }

            // Fallback: copy to clipboard, stay in this window
            EditorGUIUtility.systemCopyBuffer = json;
            ShowStatus("SpecPasteMergeWindow not found. JSON copied to clipboard.");
        }

        private void ShowStatus(string msg)
        {
            Debug.Log("[PreMergeRouter] " + msg);
            Repaint();
        }
    }
}
