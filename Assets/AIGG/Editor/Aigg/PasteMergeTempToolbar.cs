using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    internal class PasteMergeTempToolbar : EditorWindow
    {
        int _idx;

        [MenuItem("Window/Aim2Pro/Aigg/Temp Loader", priority = 205)]
        static void Open() => GetWindow<PasteMergeTempToolbar>("Temp Loader");

        void OnGUI()
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField("Load temp bucket into Paste & Merge", EditorStyles.boldLabel);
            GUILayout.Space(4);
            _idx = EditorGUILayout.Popup("Bucket", _idx, PasteMergeTempBridge.Buckets);
            GUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open P&M"))
                    PasteMergeTempBridge.OpenPasteMergeWindow();

                if (GUILayout.Button("Load Into P&M", GUILayout.Height(24)))
                    PasteMergeTempBridge.LoadBucketIntoPM(PasteMergeTempBridge.Buckets[Mathf.Clamp(_idx,0,PasteMergeTempBridge.Buckets.Length-1)]);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox("Temp files live in Assets/AIGG/Temp/temp_{bucket}.json", MessageType.Info);
        }
    }
}
