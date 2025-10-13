// WorkbenchWindow.cs (lockdown minimal)
// Menu: Window/Aim2Pro/Aigg/Workbench
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

        private string _nl = "";
        private string _status = "";
        private string _lastJson = "";

        private void OnGUI()
        {
            EditorGUILayout.LabelField("NL Input", EditorStyles.boldLabel);
            _nl = EditorGUILayout.TextArea(_nl, GUILayout.Height(140));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Parse NL (local)", GUILayout.Height(26)))
                {
                    var (ok, payload) = WorkbenchSafe.TryParseLocal(_nl);
                    if (ok) { _lastJson = payload; _status = "[OK] Local parse produced JSON."; }
                    else    { _lastJson = "";      _status = payload; }
                }

                if (GUILayout.Button("Open Pre-Merge Router", GUILayout.Height(26)))
                {
                    var msg = Aim2Pro.AIGG.Workbench.WorkbenchSafe.RouteJson(
                        string.IsNullOrEmpty(_lastJson) ? "{ \"note\": \"empty payload\" }" : _lastJson
                    );
                    _status = msg;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_status ?? "", MessageType.Info);

            EditorGUILayout.LabelField("Last JSON (readonly preview)", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_lastJson ?? "", GUILayout.Height(140));
        }
    }
}

