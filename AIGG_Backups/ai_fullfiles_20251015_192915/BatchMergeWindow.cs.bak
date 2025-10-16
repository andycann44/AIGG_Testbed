// ASCII only
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG {
  public class BatchMergeWindow : EditorWindow {
    class Tab { public string name; public string target; public string content; public string path; }
    List<Tab> tabs = new List<Tab>();
    int active = 0;
    Vector2 scroll;

    [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge (Batch Tabs)")]
    public static void OpenLatest() {
      var w = GetWindow<BatchMergeWindow>();
      w.titleContent = new GUIContent("Batch Tabs");
      w.minSize = new Vector2(680, 420);
      w.Load(TempMerge.LatestBatchDir());
      w.Show();
    }

    public static void OpenForDir(string dir) {
      var w = GetWindow<BatchMergeWindow>();
      w.titleContent = new GUIContent("Batch Tabs");
      w.minSize = new Vector2(680, 420);
      w.Load(dir);
      w.Show();
    }

    void Load(string dir) {
      tabs.Clear();
      if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
      foreach (var p in Directory.GetFiles(dir)) {
        var n = Path.GetFileName(p);
        var t = GuessTarget(n);
        var c = File.ReadAllText(p);
        tabs.Add(new Tab{ name=n, target=t, content=c, path=p });
      }
      tabs = tabs.OrderBy(t => t.name).ToList();
      active = Mathf.Clamp(active, 0, Math.Max(0, tabs.Count-1));
    }

    string GuessTarget(string name) {
      var f = name.ToLowerInvariant();
      if (f.Contains("commands")) return "Commands";
      if (f.Contains("macros")) return "Macros";
      if (f.Contains("fieldmap")) return "FieldMap";
      if (f.Contains("lexicon")) return "Lexicon";
      if (f.Contains("canonical")) return "Canonical (preview)";
      return "Unknown";
    }

    void OnGUI() {
      GUILayout.BeginHorizontal(EditorStyles.toolbar);
      if (GUILayout.Button("Reload Latest", EditorStyles.toolbarButton, GUILayout.Width(110))) {
        Load(TempMerge.LatestBatchDir());
      }
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Open Paste & Merge", EditorStyles.toolbarButton, GUILayout.Width(150))) {
        // best-effort: open the actual window; user can paste
        EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Aigg/Paste & Merge");
      }
      GUILayout.EndHorizontal();

      if (tabs.Count == 0) {
        EditorGUILayout.HelpBox("No batch found. Run 'Ask AI Now' or save a batch from Pre-Merge.", MessageType.Info);
        return;
      }

      // tabs
      GUILayout.BeginHorizontal();
      for (int i=0;i<tabs.Count;i++) {
        var style = (i==active) ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
        if (GUILayout.Toggle(i==active, tabs[i].name, style)) active = i;
      }
      GUILayout.EndHorizontal();

      // summary row
      var tab = tabs[active];
      EditorGUILayout.Space(4);
      EditorGUILayout.LabelField("Target:", tab.target);
      EditorGUILayout.LabelField("Path:", tab.path);

      // content
      EditorGUILayout.Space(4);
      using (var sv = new EditorGUILayout.ScrollViewScope(scroll)) {
        scroll = sv.scrollPosition;
        EditorGUILayout.TextArea(tab.content, GUILayout.MinHeight(200));
      }

      EditorGUILayout.Space(6);
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Copy to Clipboard")) {
        EditorGUIUtility.systemCopyBuffer = tab.content ?? "";
        EditorUtility.DisplayDialog("Copied", "Content placed on clipboard.", "OK");
      }
      if (GUILayout.Button("Open Paste & Merge + Clipboard")) {
        EditorGUIUtility.systemCopyBuffer = tab.content ?? "";
        EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Aigg/Paste & Merge");
        EditorUtility.DisplayDialog("Paste & Merge",
          "Window opened. Press ⌘V (Ctrl+V) to paste into 'Pasted Content'. Choose target '"+tab.target+"' and Apply.",
          "OK");
      }
      if (GUILayout.Button("Open Folder")) {
        EditorUtility.RevealInFinder(Path.GetDirectoryName(tab.path));
      }
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Apply ALL via Clipboard (guided)")) {
        EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Aigg/Paste & Merge");
        EditorUtility.DisplayDialog("Batch Apply (guided)",
          "Tabs window will stay open. For each tab:\n1) Click the tab\n2) Click 'Open Paste & Merge + Clipboard'\n3) Paste into 'Pasted Content'\n4) Pick the matching Target\n5) Validate, then Apply Merge.\n\nRepeat for each tab.",
          "OK");
      }
      GUILayout.EndHorizontal();
    }
  }
}
