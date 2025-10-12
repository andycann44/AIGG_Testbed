using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public class PreMergeRouterWindow : EditorWindow
    {
        private string _json = "";

        public static void Open()
        {
            var w = GetWindow<PreMergeRouterWindow>("Pre-Merge Router");
            w.minSize = new Vector2(520, 320);
            w.Show(); w.Focus();
        }

        private void OnEnable()
        {
            _json = PreMergeRouterAPI.LastJson ?? "";
        }

        private void OnGUI()
        {
            GUILayout.Label("OpenAI JSON -> Paste & Merge", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _json = EditorGUILayout.TextArea(_json, GUILayout.ExpandHeight(true));

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Route JSON")) PreMergeRouterAPI.Route(_json);
                if (GUILayout.Button("Paste from Clipboard")) _json = EditorGUIUtility.systemCopyBuffer ?? "";
                if (GUILayout.Button("Copy")) EditorGUIUtility.systemCopyBuffer = _json ?? "";
            }
        }
    }
}
