// ASCII only
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG {
  public class PreMergeRouterWindow : EditorWindow {
    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge")]
    public static void Open() {
      var w = GetWindow<PreMergeRouterWindow>();
      w.titleContent = new GUIContent("Pre-Merge");
      w.minSize = new Vector2(560, 420);
      w.Show();
    }
    void OnGUI() {
      GUILayout.Label("Pre-Merge (strict router) ready.", EditorStyles.boldLabel);
      GUILayout.Label("Use the Send → Paste & Merge button in this window once diagnostics are OK.", EditorStyles.wordWrappedLabel);
    }
  }
}
