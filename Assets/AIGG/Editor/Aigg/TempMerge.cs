using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    /// <summary>
    /// Utilities for writing AI output to a stable location that our watcher/separator can consume.
    /// </summary>
    internal static class TempMerge
    {
        public const string TempRoot = "Assets/AIGG/Temp";
        public const string AiOutRel  = TempRoot + "/ai_out.json";

        /// <summary>
        /// Save a raw JSON string to Assets/AIGG/Temp/ai_out.json and import it so the watcher fires.
        /// </summary>
        public static void SaveFromAI(string json)
        {
            if (json == null) json = "";
            Directory.CreateDirectory(TempRoot);
            File.WriteAllText(AiOutRel, json);
            AssetDatabase.ImportAsset(AiOutRel, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[TempMerge] Wrote AI output to: {AiOutRel}");
        }

        /// <summary>
        /// Save any object by serializing to JSON first (pretty when possible).
        /// </summary>
        public static void SaveFromAI(object ai)
        {
            string json;
            try { json = JsonUtility.ToJson(ai, true); }
            catch { json = ai?.ToString() ?? ""; }
            SaveFromAI(json);
        }
    }
}
