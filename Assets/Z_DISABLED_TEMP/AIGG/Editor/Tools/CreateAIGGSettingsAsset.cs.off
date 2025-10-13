#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG.Tools
{
    public static class CreateAIGGSettingsAsset
    {
        [MenuItem("Window/Aim2Pro/Settings/Create Blank AIGG Settings")]
        public static void Create()
        {
            var settings = ScriptableObject.CreateInstance<AIGGSettings>();

            const string dir = "Assets/AIGG/Settings";
            const string path = dir + "/AIGG_Settings.asset";
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("AIGG", "Created blank AIGG_Settings.asset.", "OK");
            Selection.activeObject = settings;
        }
    }
}
#endif
