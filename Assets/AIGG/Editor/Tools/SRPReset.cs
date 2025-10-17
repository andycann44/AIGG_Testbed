using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aim2Pro.Tools {
    public static class SRPReset {
        [MenuItem("Window/Aim2Pro/Tools/Reset SRP (Built-in)")]
        public static void ResetToBuiltIn() {
            GraphicsSettings.renderPipelineAsset = null;
            // Per-quality reset: clearing global is usually enough; re-apply all quality levels to flush
            for (int i = 0; i < QualitySettings.names.Length; i++) {
                QualitySettings.SetQualityLevel(i, false);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Aim2Pro] SRP cleared → Built-in. If scene uses URP-only features, they'll be ignored.");
        }
    }
}
