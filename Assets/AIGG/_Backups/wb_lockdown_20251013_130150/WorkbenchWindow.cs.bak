using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG.Workbench
{
    public class WorkbenchWindow : EditorWindow
    {
        [MenuItem("Window/Aim2Pro/Aigg/Workbench", priority = 100)]
        public static void ShowWindow()
        {
            var w = GetWindow<WorkbenchWindow>("Workbench");
            w.minSize = new Vector2(600, 400);
            w.Show();
        }

        string nlInput = "";

        void OnGUI()
        {
            EditorGUILayout.LabelField("NL Input", EditorStyles.boldLabel);
            nlInput = EditorGUILayout.TextArea(nlInput, GUILayout.Height(120));

            EditorGUILayout.Space();

            if (GUILayout.Button("Parse NL (local)", GUILayout.Height(26)))
            {
                Debug.Log("[Workbench] Parse NL pressed");
            }

            if (GUILayout.Button("Open Pre-Merge Router", GUILayout.Height(26)))
            {
                Aim2Pro.AIGG.PreMergeRouterAPI.Route("{\"note\":\"ok\"}");
            }
        }
    }
}

