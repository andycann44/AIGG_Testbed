#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Aim2Pro.AIGG
{
    public static class CopyOpenAIKey
    {
        static string GetKey()
        {
            var s = AIGGSettings.LoadOrCreate();   // your settings asset
            return (s && !string.IsNullOrEmpty(s.openAIKey)) ? s.openAIKey : "";
        }

        [MenuItem("Window/Aim2Pro/Aigg/API/Copy OpenAI API Key")]
        public static void CopyKey()
        {
            var key = GetKey();
            if (string.IsNullOrEmpty(key))
            {
                EditorUtility.DisplayDialog("OpenAI API Key", "No key found in AIGG Settings.", "OK");
                return;
            }
            EditorGUIUtility.systemCopyBuffer = key;
            EditorUtility.DisplayDialog("OpenAI API Key", "Key copied to clipboard.", "OK");
        }

        [MenuItem("Window/Aim2Pro/Aigg/API/Save OpenAI API Key to File…")]
        public static void SaveKeyToFile()
        {
            var key = GetKey();
            if (string.IsNullOrEmpty(key))
            {
                EditorUtility.DisplayDialog("OpenAI API Key", "No key found in AIGG Settings.", "OK");
                return;
            }

            var path = EditorUtility.SaveFilePanel("Save OpenAI API Key", "", "openai.key", "key");
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, key);
            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog("OpenAI API Key", "Key saved.", "OK");
        }

        [MenuItem("Window/Aim2Pro/Aigg/API/Show Last 4 (safe)")]
        public static void ShowLast4()
        {
            var key = GetKey();
            if (string.IsNullOrEmpty(key))
            {
                EditorUtility.DisplayDialog("OpenAI API Key", "No key found in AIGG Settings.", "OK");
                return;
            }
            var last4 = key.Length >= 4 ? key.Substring(key.Length - 4) : key;
            EditorUtility.DisplayDialog("OpenAI API Key (safe)", $"**** **** **** {last4}", "OK");
        }
    }
}
#endif
