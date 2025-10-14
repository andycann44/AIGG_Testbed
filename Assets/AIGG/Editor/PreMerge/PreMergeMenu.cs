#if UNITY_EDITOR
using UnityEditor;
namespace Aim2Pro.AIGG.PreMerge {
  public static class PreMergeMenu {
    [MenuItem("Window/Aim2Pro/Aigg/Open: Pre-Merge → Paste & Merge", priority=690)]
    public static void Open() {
      PreMergeWindow.Open();
      Aim2Pro.AIGG.PreMergeRouterWindow.Open();
    }
  }
}
#endif
