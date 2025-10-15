// ASCII only
using UnityEditor;

namespace Aim2Pro.AIGG {
  // Old names some tools may still reference:
  public static class PreMergeWindow {
    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge (Open)")]
    public static void Open() { PreMergeRouterWindow.Open(); }
    public static void Route(string nl, string canonical, string diagnostics){ PreMergeRouterAPI.Route(nl,canonical,diagnostics); }
    public static void Route(string canonical){ PreMergeRouterAPI.Route(canonical); }
    public static void Route(string nl, string canonical){ PreMergeRouterAPI.Route(nl,canonical); }
  }
  public static class PreMergeCenterWindow {
    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge (Center)")]
    public static void Open() { PreMergeRouterWindow.Open(); }
    public static void Route(string nl, string canonical, string diagnostics){ PreMergeRouterAPI.Route(nl,canonical,diagnostics); }
    public static void Route(string canonical){ PreMergeRouterAPI.Route(canonical); }
    public static void Route(string nl, string canonical){ PreMergeRouterAPI.Route(nl,canonical); }
  }
  // Extra catch-alls for vague legacy lookups:
  public static class PreMergeRouter { public static void Open(){ PreMergeRouterWindow.Open(); } }
  public static class PreMerge { public static void Open(){ PreMergeRouterWindow.Open(); } }
  public static class RouterWindow { public static void Open(){ PreMergeRouterWindow.Open(); } }
}
