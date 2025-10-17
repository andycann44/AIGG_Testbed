using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Aim2Pro.AIGG
{
    /// <summary>
    /// Fires whenever ai_out.json is imported/changed inside Unity.
    /// </summary>
    internal class AIOutWatcher_Asset : AssetPostprocessor
    {
        const string Target = "Assets/AIGG/Temp/ai_out.json";

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets == null || importedAssets.Length == 0) return;
            if (!importedAssets.Contains(Target)) return;

            Debug.Log($"[AIOutWatcher] Detected import/update: {Target}");
            int n = AISeparator.SplitAndOpen(Target);
            Debug.Log($"[AISeparator] Wrote {n} bucket file(s) from {Target}");
        }
    }
}
