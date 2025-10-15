// ASCII only
using UnityEditor;

namespace Aim2Pro.AIGG {
  // Legacy entry points expected by older tools. They just forward to the strict flow.
  public static class PreMergeWindow {
    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge (Open)")]
    public static void Open() { PreMergeRouterWindow.Open(); }

    // Some callers might try to "route" directly via this class:
    public static void Route(string nl, string canonicalJson, string diagnosticsJson) {
      PreMergeRouterAPI.Route(nl, canonicalJson, diagnosticsJson);
    }
    public static void Route(string canonicalJson) { PreMergeRouterAPI.Route(canonicalJson); }           // legacy 1-arg
    public static void Route(string nl, string canonicalJson) { PreMergeRouterAPI.Route(nl, canonicalJson); } // legacy 2-arg
  }

  // Another historical name used in older scripts.
  public static class PreMergeCenterWindow {
    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge (Center)")]
    public static void Open() { PreMergeRouterWindow.Open(); }
    public static void Route(string nl, string canonicalJson, string diagnosticsJson) {
      PreMergeRouterAPI.Route(nl, canonicalJson, diagnosticsJson);
    }
    public static void Route(string canonicalJson) { PreMergeRouterAPI.Route(canonicalJson); }
    public static void Route(string nl, string canonicalJson) { PreMergeRouterAPI.Route(nl, canonicalJson); }
  }
}
