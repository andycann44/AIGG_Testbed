using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    // One tiny utility: no watchers, no semantics changes, all in Temp.
    public static class PreMergeTempTools
    {
        private const string TempDir = "Assets/AIGG/Temp";
        private static readonly string[] Buckets = {
            "intents","lexicon","macros","commands","fieldmap","registry","schema"
        };

        [MenuItem("Window/Aim2Pro/Aigg/Reveal AI Output Folder")]
        public static void RevealTempFolder()
        {
            EnsureTempDir();
            EditorUtility.RevealInFinder(TempDir);
        }

        [MenuItem("Window/Aim2Pro/Aigg/Split AI Output Now")]
        public static void SplitAiOut()
        {
            EnsureTempDir();
            var aiOut = Path.Combine(TempDir, "ai_out.json");
            if (!File.Exists(aiOut))
            {
                EditorUtility.DisplayDialog("Split AI Output",
                    $"Missing file:\n{aiOut}\n\nWrite Ask-AI output there first.", "OK");
                return;
            }

            string src;
            try { src = File.ReadAllText(aiOut); }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Split AI Output", $"Failed to read ai_out.json\n{ex.Message}", "OK");
                return;
            }

            foreach (var b in Buckets)
            {
                var outPath = Path.Combine(TempDir, $"temp_{b}.json");
                try
                {
                    var payload = ExtractTopLevel(src, b);
                    if (!string.IsNullOrEmpty(payload))
                        File.WriteAllText(outPath, payload + "\n", Encoding.UTF8);
                    else if (!File.Exists(outPath))
                        File.WriteAllText(outPath, "{}\n", Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AIGG] Split for '{b}' failed: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Split AI Output", "Buckets updated in Assets/AIGG/Temp/.", "OK");
        }

        private static void EnsureTempDir()
        {
            if (!Directory.Exists(TempDir)) Directory.CreateDirectory(TempDir);
        }

        // Minimal top-level JSON extractor (expects well-formed AI output).
        private static string ExtractTopLevel(string src, string key)
        {
            string needle = $"\"{key}\"";
            int i = src.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return "";
            i = src.IndexOf(':', i); if (i < 0) return "";
            i++;
            while (i < src.Length && char.IsWhiteSpace(src[i])) i++;
            if (i >= src.Length) return "";

            if (src[i] == '{')
            {
                int depth = 0; int start = i;
                for (; i < src.Length; i++)
                {
                    if (src[i] == '{') depth++;
                    else if (src[i] == '}')
                    {
                        depth--;
                        if (depth == 0) { int end = i; return src.Substring(start, end - start + 1); }
                    }
                }
                return "";
            }
            if (src[i] == '[')
            {
                int depth = 0; int start = i;
                for (; i < src.Length; i++)
                {
                    if (src[i] == '[') depth++;
                    else if (src[i] == ']')
                    {
                        depth--;
                        if (depth == 0) { int end = i; return src.Substring(start, end - start + 1); }
                    }
                }
                return "";
            }
            // primitive
            int startPrim = i;
            while (i < src.Length && src[i] != ',' && src[i] != '\n' && src[i] != '\r' && src[i] != '}') i++;
            return src.Substring(startPrim, i - startPrim).Trim();
        }
    }
}
