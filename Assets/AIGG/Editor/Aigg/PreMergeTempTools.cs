using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    // Minimal: one file, no watchers, no merge-semantic changes.
    public static class PreMergeTempTools
    {
        private const string TempDir = "Assets/AIGG/Temp";

        // Full 15-bucket set we agreed:
        // 7 canonical spec buckets + 8 extended buckets for track details & diagnostics.
        private static readonly string[] Buckets = {
            // canonical spec
            "intents","lexicon","macros","commands","fieldmap","registry","schema",
            // extended
            "track","hazards","materials","tiles","curves","chicanes","validation","diagnostics"
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
                        File.WriteAllText(outPath, "{}\n", Encoding.UTF8); // scaffold, non-destructive
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AIGG] Split for '{b}' failed: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Split AI Output", "Updated temp_*.json under Assets/AIGG/Temp/.", "OK");
        }

        private static void EnsureTempDir()
        {
            if (!Directory.Exists(TempDir)) Directory.CreateDirectory(TempDir);
        }

        // Minimal top-level extractor (expects well-formed JSON).
        // Returns the value text of a top-level key if it is an object {…}, array […], or primitive.
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
