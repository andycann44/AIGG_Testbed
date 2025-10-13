// WorkbenchWindow. minimal, compile-safecs 
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
            w.minSize = new Vector2(640, 420);
            w.Show();
        }

        string nl = "";
        string status = "Ready.";

        void OnGUI()
        {
            EditorGUILayout.LabelField("NL Input", EditorStyles.boldLabel);
            nl = EditorGUILayout.TextArea(nl, GUILayout.Height(140));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Parse NL (noop)", GUILayout.Height(26)))
                    status = "Parse skipped (baseline).";

                if (GUILayout.Button("Export JSON (clipboard)", GUILayout.Height(26)))
                {
                    var json = string.IsNullOrWhiteSpace(nl) ? "{ \"note\": \"empty\" }" :
                        "{ \"nl\": \"" + nl.Replace("\\","\\\\").Replace("\"","\\\"") + "\" }";
                    EditorGUIUtility.systemCopyBuffer = json;
                    status = "JSON copied to clipboard.";
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(status, MessageType.None);
        }
    }
}
