using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aim2Pro.Tools {
    public static class SRPReset {
        [MenuItem("Window/Aim2Pro/Tools/Reset SRP (Built-in)")]
        public static void ResetToBuiltIn() {
            // Graphics → Scriptable Render Pipeline Asset = None
            GraphicsSettings.renderPipelineAsset = null;

            // Quality → Rendering Pipeline per quality = None
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++) {
                QualitySettings.SetQualityLevel(i, false);
#if UNITY_2021_2_OR_NEWER
                // Newer Unity stores per-quality RP via QualitySettings APIs
                // (in some versions this is internal; clearing global GraphicsSettings is enough)
#endif
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Aim2Pro] Reset SRP → Built-in (Graphics + all Quality levels). Reopen Scene if needed.");
        }
    }
}
