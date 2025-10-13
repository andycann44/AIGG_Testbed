using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public partial class PreMergeRouterWindow : EditorWindow
    {
        private static string _lastJson = "";
        private Vector2 _scroll;

        [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge Router", priority = 2000)]
        public static void ShowWindow()
        {
            var w = GetWindow<PreMergeRouterWindow>("Pre-Merge Router");
            w.minSize = new Vector2(520, 360);
            w.Show();
        }

        public static void OpenWithJson(string json)
        {
            _lastJson = json ?? "";
            var w = GetWindow<PreMergeRouterWindow>("Pre-Merge Router");
            w.Show();
            w.Repaint();
            // Try to forward to Paste & Merge if present
            TryForwardToPasteAndMerge(_lastJson);
        }

        private static void TryForwardToPasteAndMerge(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            Type t = null;
            MethodInfo m = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType("Aim2Pro.AIGG.SpecPasteMergeWindow");
                    if (t != null)
                    {
                        m = t.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.Static);
                        break;
                    }
                }
                catch { }
            }

            try
            {
                if (t != null && m != null)
                {
                    m.Invoke(null, new object[] { json });
                    Debug.Log("[PreMergeRouter] Routed via SpecPasteMergeWindow.OpenWithJson");
                }
                else
                {
                    // Fallback: keep JSON visible here and copy to clipboard
                    EditorGUIUtility.systemCopyBuffer = json;
                    Debug.Log("[PreMergeRouter] Paste & Merge not found; JSON copied to clipboard.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PreMergeRouter] Forward failed: " + e.Message);
                EditorGUIUtility.systemCopyBuffer = json;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Incoming JSON", EditorStyles.boldLabel);
            using (var s = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.ExpandHeight(true)))
            {
                _scroll = s.scrollPosition;
                EditorGUILayout.TextArea(_lastJson ?? "", GUILayout.ExpandHeight(true));
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy JSON", GUILayout.Height(24)))
                {
                    EditorGUIUtility.systemCopyBuffer = _lastJson ?? "";
                    Debug.Log("[PreMergeRouter] JSON copied.");
                }
                if (GUILayout.Button("Forward to Paste & Merge", GUILayout.Height(24)))
                {
                    TryForwardToPasteAndMerge(_lastJson ?? "");
                }
            }
        }
    }
}
