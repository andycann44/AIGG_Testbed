#if UNITY_EDITOR
namespace Aim2Pro.AIGG {
  public static class PreMergeRouterAPI {
    // Route JSON into Paste & Merge (handles wrapper or raw)
    public static void Route(string json) { SpecPasteMergeWindow.OpenWithJson(json); }
  }
}
#endif
