// ASCII only
using UnityEditor;
namespace Aim2Pro.AIGG {

  public static class PreMergeCenterWindow {
    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge (Center)")]
    public static void Open(){ PreMergeRouterWindow.Open(); }
    public static void Route(string nl, string canonical, string diagnostics){ PreMergeRouterAPI.Route(nl,canonical,diagnostics); }
    public static void Route(string canonical){ PreMergeRouterAPI.Route(canonical); }
    public static void Route(string nl, string canonical){ PreMergeRouterAPI.Route(nl,canonical); }
  }

  public static class PreMergeRouter {
    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge (Router)")]
    public static void Open(){ PreMergeRouterWindow.Open(); }
  }
}
