using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    internal static class AISeparator
    {
        public const string Root = "Assets/AIGG/Temp";
        public const string AiOut = Root + "/ai_out.json";

        static readonly string[] Buckets = new[]{
            "commands","macros","fieldmap","lexicon","registry","schema","router",
            "nl","canonical","diagnostics","aliases","shims","nullable","overrides"
        };

        [MenuItem("Window/Aim2Pro/Aigg/API/Split AI Output Now", priority = 501)]
        public static void MenuSplit() => SplitAndOpen();

        public static int Split(string aiOutPath = AiOut)
        {
            Directory.CreateDirectory(Root);
            if (!File.Exists(aiOutPath))
            {
                Debug.LogWarning($"[AISeparator] No file: {aiOutPath}");
                return 0;
            }

            string src = File.ReadAllText(aiOutPath).Trim();
            if (string.IsNullOrEmpty(src))
            {
                Debug.LogWarning("[AISeparator] ai_out.json is empty.");
                return 0;
            }

            int written = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var key in Buckets)
                {
                    var val = TryExtractTopLevelValue(src, key);
                    var path = Path.Combine(Root, $"temp_{key}.json").Replace("\\", "/");

                    if (string.IsNullOrWhiteSpace(val) || val == "[]" || val == "{}" || val == "\"\"")
                    {
                        DeleteAssetIfExists(path);
                        continue;
                    }

                    WriteTextAsset(path, val);
                    written++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            Debug.Log($"[AISeparator] Wrote {written} bucket file(s) from {aiOutPath}");
            return written;
        }

        public static int SplitAndOpen(string aiOutPath = AiOut)
        {
            int n = Split(aiOutPath);
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(x => x.Name == "SpecPasteMergeWindow" && typeof(EditorWindow).IsAssignableFrom(x));
            if (t != null) EditorWindow.GetWindow(t);
            return n;
        }

        static void WriteTextAsset(string assetPath, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllText(assetPath, contents ?? "");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null || File.Exists(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        // Extract raw JSON value for top-level "key": <value>
        static string TryExtractTopLevelValue(string json, string key)
        {
            var needle = $"\"{key}\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return null;

            i = json.IndexOf(':', i + needle.Length);
            if (i < 0) return null;

            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return null;

            char c = json[i];

            if (c == '"')
            {
                int j = i + 1; bool esc = false;
                for (; j < json.Length; j++)
                {
                    char ch = json[j];
                    if (esc) { esc = false; continue; }
                    if (ch == '\\') { esc = true; continue; }
                    if (ch == '"') { j++; break; }
                }
                return json.Substring(i, Math.Min(j, json.Length) - i);
            }

            if (c == '{' || c == '[')
            {
                int depth = 0; char open = c; char close = (c == '{') ? '}' : ']';
                int j = i; bool inStr = false; bool esc = false;
                for (; j < json.Length; j++)
                {
                    char ch = json[j];
                    if (inStr)
                    {
                        if (esc) esc = false;
                        else if (ch == '\\') esc = true;
                        else if (ch == '"') inStr = false;
                        continue;
                    }
                    else
                    {
                        if (ch == '"') { inStr = true; continue; }
                        if (ch == open) depth++;
                        else if (ch == close)
                        {
                            depth--;
                            if (depth == 0) { j++; break; }
                        }
                    }
                }
                return json.Substring(i, Math.Min(j, json.Length) - i).Trim();
            }

            int k = i;
            while (k < json.Length && ",}]".IndexOf(json[k]) == -1) k++;
            return json.Substring(i, k - i).Trim();
        }
    }
}
