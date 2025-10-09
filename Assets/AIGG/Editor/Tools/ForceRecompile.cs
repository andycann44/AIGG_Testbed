using UnityEditor;
#if UNITY_2017_1_OR_NEWER
using UnityEditor.Compilation;
#endif
public static class Aim2ProForceRecompile {
    [MenuItem("Window/Aim2Pro/Tools/Force Recompile", priority = 0)]
    public static void Force() {
        #if UNITY_2017_1_OR_NEWER
        CompilationPipeline.RequestScriptCompilation();
        #else
        AssetDatabase.Refresh();
        #endif
        UnityEngine.Debug.Log("[Aim2Pro] Requested script recompile.");
    }
}
