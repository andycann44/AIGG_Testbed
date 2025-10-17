using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

namespace Aim2Pro.AIGG
{
    /// <summary>
    /// Triggers when Assets/AIGG/Temp/ai_out.json is imported/changed.
    /// Also exposes a force menu to reimport + split on demand.
    /// </summary>
    internal class AIOutWatcher_Asset : AssetPostprocessor
    {
        public const string Target = "Assets/AIGG/Temp/ai_out.json";

        [InitializeOnLoadMethod]
        static void Init()
        {
            Debug.Log($"[AIOutWatcher] Ready. Target = {Target}");
        }

        [MenuItem("Window/Aim2Pro/Aigg/API/Reimport ai_out.json + Split", priority = 500)]
        public static void ForceReimportAndSplit()
        {
            Debug.Log("[AIOutWatcher] Force reimport + split");
            AssetDatabase.ImportAsset(Target, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var n = AISeparator.SplitAndOpen(Target);
            Debug.Log($"[AISeparator] Wrote {n} bucket file(s) from {Target}");
        }

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets == null || importedAssets.Length == 0) return;
            if (!importedAssets.Any(p => string.Equals(p, Target, StringComparison.OrdinalIgnoreCase))) return;

            Debug.Log($"[AIOutWatcher] Detected import/update: {Target}");
            int n = AISeparator.SplitAndOpen(Target);
            Debug.Log($"[AISeparator] Wrote {n} bucket file(s) from {Target}");
        }
    }
}
