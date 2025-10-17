using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public static class TempOutputHelpers
    {
        private const string TempDir = "Assets/AIGG/Temp";

        // 15 slots (extensible). First 7 are your spec buckets.
        private static readonly string[] Buckets15 = new[]
        {
            "intents","lexicon","macros","commands","fieldmap","registry","schema",
            "track","hazards","materials","tiles","curves","chicanes","validation","diagnostics"
        };

        [MenuItem("Window/Aim2Pro/Aigg/Reveal AI Output Folder")]
        public static void RevealTempFolder()
        {
            EnsureTempScaffold();
            EditorUtility.RevealInFinder(TempDir);
            EditorUtility.DisplayDialog("AI Output Folder",
                $"Ready at:\n{TempDir}\n\nScaffold files ensured. Use 'Split AI Output Now' to populate buckets from ai_out.json.",
                "OK");
        }

        [MenuItem("Window/Aim2Pro/Aigg/Split AI Output Now")]
        public static void SplitAiOut()
        {
            EnsureTempScaffold();
            var aiOut = Path.Combine(TempDir, "ai_out.json");
            if (!File.Exists(aiOut))
            {
                EditorUtility.DisplayDialog("Split AI Output",
                    $"Missing file:\n{aiOut}\n\nWrite Ask-AI output there first.", "OK");
                return;
            }

            string json;
            try { json = File.ReadAllText(aiOut); }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Split AI Output", $"Failed to read ai_out.json\n{ex.Message}", "OK");
                return;
            }

            // Very light extractor: picks top-level properties that match bucket names (arrays or objects).
            // If absent, leaves the temp file as-is.
            foreach (var bucket in Buckets15)
            {
                var outPath = Path.Combine(TempDir, $"temp_{bucket}.json");
                try
                {
                    var payload = ExtractTopLevel(json, bucket);
                    if (!string.IsNullOrEmpty(payload))
                    {
                        File.WriteAllText(outPath, payload + "\n", Encoding.UTF8);
                    }
                    else if (!File.Exists(outPath))
                    {
                        File.WriteAllText(outPath, "{}\n", Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AIGG] Split for '{bucket}' failed: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Split AI Output", "Buckets updated under Assets/AIGG/Temp/", "OK");
        }

        // Ensures Temp dir exists and all 15 files exist (without touching existing content).
        private static void EnsureTempScaffold()
        {
            if (!Directory.Exists(TempDir))
                Directory.CreateDirectory(TempDir);

            // Raw AI output file
            var aiOut = Path.Combine(TempDir, "ai_out.json");
            if (!File.Exists(aiOut))
                File.WriteAllText(aiOut, "{\n  \"intents\": [],\n  \"lexicon\": [],\n  \"macros\": [],\n  \"commands\": [],\n  \"fieldmap\": {},\n  \"registry\": {},\n  \"schema\": {}\n}\n", Encoding.UTF8);

            foreach (var b in Buckets15)
            {
                var p = Path.Combine(TempDir, $"temp_{b}.json");
                if (!File.Exists(p))
                    File.WriteAllText(p, "{}\n", Encoding.UTF8);
            }

            AssetDatabase.Refresh();
        }

        // Minimal, regex-free top-level extractor (works for simple well-formed JSON).
        // Finds `"key": { ... }` or `"key": [ ... ]` at top level and returns the value text, otherwise "".
        private static string ExtractTopLevel(string src, string key)
        {
            // Cheap scan; avoids bringing in JSON libs to keep your build clean.
            // Not a full JSON parser—expects nicely formatted AI output.
            string Needle = $"\"{key}\"";
            int i = src.IndexOf(Needle, StringComparison.Ordinal);
            if (i < 0) return "";

            // Move to colon after key
            i = src.IndexOf(':', i);
            if (i < 0) return "";
            // Move to first non-space
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
            else if (src[i] == '[')
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
            else
            {
                // Primitive—read until comma or newline
                int start = i;
                while (i < src.Length && src[i] != ',' && src[i] != '\n' && src[i] != '\r' && src[i] != '}') i++;
                return src.Substring(start, i - start).Trim();
            }
        }
    }
}
