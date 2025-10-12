#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG.Aigg
{
    public class PreMergeRouterWindow : EditorWindow
    {
        private static string _last = "";
        private static Vector2 _scroll;

        [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge Router", false, 120)]
        public static void Open()
        {
            var w = GetWindow<PreMergeRouterWindow>();
            w.titleContent = new GUIContent("Pre-Merge Router");
            w.minSize = new Vector2(560, 320);
            w.Show();
        }

        // Workbench/Runner will call this
        public static void ReceiveFromOpenAI(string json, bool focus = true)
        {
            _last = json ?? "";
            Debug.Log($"[PreMergeRouter] received {_last.Length} bytes");
            if (focus) Open();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Router inbox (from OpenAI):", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_last ?? "", GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear", GUILayout.Width(120))) _last = "";
                if (GUILayout.Button("Copy",  GUILayout.Width(120))) EditorGUIUtility.systemCopyBuffer = _last ?? "";
            }
        }
    }
}
#endif
