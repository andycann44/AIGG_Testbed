using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class Aim2ProCompilePing {
    static Aim2ProCompilePing() {
        Debug.Log("[Aim2Pro] Editor domain reloaded ✔ (CompilePing)");
    }
    [MenuItem("Window/Aim2Pro/Tools/Ping Log")]
    public static void Ping() => Debug.Log("[Aim2Pro] Ping menu clicked.");
}
