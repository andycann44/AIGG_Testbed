using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Aim2Pro.AIGG.Workbench {
  public class WorkbenchWindow : EditorWindow {
    // Read exactly these 7 spec files (same set Paste & Merge edits)
    static readonly string SpecDir = "Assets/AIGG/Spec";
    static readonly string[] SpecFiles = { "intents","macros","lexicon","commands","fieldmap","registry","schema" };

    string _nl = "100m by 20m";
    string _diag = "Spec: " + SpecDir;
    string _json = BuildScenePlanSkeleton();

    [MenuItem("Window/Aim2Pro/Workbench", false, 100)]
    public static void ShowWindow() {
      var w = GetWindow<WorkbenchWindow>();
      w.titleContent = new GUIContent("Workbench");
      w.minSize = new Vector2(680, 520);
      w.Show();
    }

    void OnGUI() {
      EditorGUILayout.LabelField("Natural language prompt", EditorStyles.boldLabel);
      _nl = EditorGUILayout.TextArea(_nl, GUILayout.MinHeight(70));

      EditorGUILayout.Space(4);
      using (new EditorGUILayout.HorizontalScope()) {
        if (GUILayout.Button("Parse NL (local intents)", GUILayout.Height(24))) ParseLocal();
        if (GUILayout.Button("Open Paste & Merge", GUILayout.Height(24))) OpenPasteAndMerge();
        if (GUILayout.Button("Copy skeleton intent", GUILayout.Height(24))) CopySkeletonIntent();
      }

      EditorGUILayout.Space(8);
      EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);
      using (new EditorGUI.DisabledScope(true)) {
        _diag = EditorGUILayout.TextArea(_diag, GUILayout.MinHeight(120));
      }

      EditorGUILayout.Space(4);
      EditorGUILayout.LabelField("JSON Output", EditorStyles.boldLabel);
      _json = EditorGUILayout.TextArea(_json, GUILayout.MinHeight(200));

      EditorGUILayout.Space(4);
      using (new EditorGUILayout.HorizontalScope()) {
        EditorGUILayout.LabelField("Spec: " + SpecDir, EditorStyles.miniLabel);
        if (GUILayout.Button("Open Spec Folder", GUILayout.Width(160))) EditorUtility.RevealInFinder(SpecDir);
        if (GUILayout.Button("Reset to scenePlan skeleton", GUILayout.Width(220))) _json = BuildScenePlanSkeleton();
      }

      EditorGUILayout.HelpBox("Workbench uses the same 7 spec files as Paste & Merge in Assets/AIGG/Spec. No Resources/ fallback.", MessageType.Info);
    }

    void ParseLocal() {
      try {
        string normalized = Normalize(_nl);
        var sets = LoadSpecSets(); // from the 7 files only
        var matches = MatchBySource(normalized, sets);
        var toks = Tokenize(normalized).ToArray();
        var unmatched = toks.Where(t => !matches.Any(kv => kv.Value.Contains(t))).ToArray();

        var json = TryRunLocalParsers(_nl);
        if (string.IsNullOrEmpty(json)) json = BuildScenePlanSkeleton();
        _json = json;

        _diag =
          "Normalized:\n  " + normalized + "\n\n" +
          "Matched (by source):\n" +
          "  intents:   "  + J(matches,"intents")  + "\n" +
          "  macros:    "  + J(matches,"macros")   + "\n" +
          "  lexicon:   "  + J(matches,"lexicon")  + "\n" +
          "  commands:  "  + J(matches,"commands") + "\n" +
          "  fieldmap:  "  + J(matches,"fieldmap") + "\n" +
          "  registry:  "  + J(matches,"registry") + "\n" +
          "  schema:    "  + J(matches,"schema")   + "\n\n" +
          "Unmatched (" + unmatched.Length + "):\n  " + string.Join(", ", unmatched) + "\n\n" +
          (string.IsNullOrEmpty(json) ? "No JSON produced by local parsers." : "JSON ready.");
        ShowNotification(new GUIContent(string.IsNullOrEmpty(json) ? "Skeleton" : "Parsed OK"));
      } catch (Exception ex) {
        _diag = ex.GetType().Name + ": " + ex.Message;
        ShowNotification(new GUIContent("Parse error"));
      }
    }

    static string J(Dictionary<string,List<string>> m, string k) =>
      string.Join(", ", (m.ContainsKey(k)? m[k] : new List<string>()).Distinct());

    string TryRunLocalParsers(string nl) {
      var types = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } }).ToArray();

      var t = types.FirstOrDefault(tt => tt.FullName == "Aim2Pro.AIGG.NLToJson" || tt.Name == "NLToJson")
           ?? types.FirstOrDefault(tt => tt.FullName == "AIGG_NLInterpreter" || tt.Name == "AIGG_NLInterpreter");
      if (t != null) {
        var m = t.GetMethod("GenerateFromPrompt", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)
             ?? t.GetMethod("RunToJson",      System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        if (m != null) {
          var res = m.Invoke(null, new object[]{ nl }) as string;
          if (!string.IsNullOrEmpty(res)) return res;
        }
      }
      return null;
    }

    void OpenPasteAndMerge() {
      try {
        var spmw = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
          .FirstOrDefault(t => t.Name == "SpecPasteMergeWindow");
        if (spmw != null) {
          var m = spmw.GetMethod("OpenWithJson", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance, null, new Type[]{ typeof(string) }, null);
          if (m != null) { object inst = m.IsStatic? null : EditorWindow.GetWindow(spmw); m.Invoke(inst, new object[]{ _json }); ShowNotification(new GUIContent("Sent to Paste & Merge")); return; }
          EditorWindow.GetWindow(spmw).Show(); EditorGUIUtility.systemCopyBuffer = _json; ShowNotification(new GUIContent("Opened (JSON copied)")); return;
        }
        if (EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Paste & Merge")) { EditorGUIUtility.systemCopyBuffer = _json; ShowNotification(new GUIContent("Opened via menu (copied)")); return; }
        EditorGUIUtility.systemCopyBuffer = _json; ShowNotification(new GUIContent("Window not found; copied."));
      } catch { EditorGUIUtility.systemCopyBuffer = _json; ShowNotification(new GUIContent("Error; JSON copied.")); }
    }

    void CopySkeletonIntent() {
      var s = "{\n  \"intents\": [\n    {\n      \"name\": \"dimensions-by-m\",\n" +
              "      \"regex\": \"^\\\\s*(?:make|build|create)?\\\\s*(\\\\d+(?:\\\\.\\\\d+)?)\\\\s*m\\\\s*(?:x|by|\\\\u00d7)\\\\s*(\\\\d+(?:\\\\.\\\\d+)?)\\\\s*m\\\\s*$\",\n" +
              "      \"ops\": [\n" +
              "        {\"op\":\"set\",\"path\":\"$.trackTemplate.lengthUnits\",\"value\":\"$1:float\"},\n" +
              "        {\"op\":\"set\",\"path\":\"$.trackTemplate.tileWidth\",\"value\":\"$2:float\"}\n" +
              "      ]\n    }\n  ]\n}\n";
      EditorGUIUtility.systemCopyBuffer = s;
      ShowNotification(new GUIContent("Skeleton intent copied (Target: Intents)."));
    }

    // === Specs: strict load from Assets/AIGG/Spec (no Resources) ===
    static Dictionary<string, HashSet<string>> LoadSpecSets() {
      var map = new Dictionary<string, HashSet<string>>();
      foreach (var n in SpecFiles) map[n] = new HashSet<string>();
      foreach (var n in SpecFiles) {
        AddWords($"{SpecDir}/{n}.json", map[n]);
        AddWords($"{SpecDir}/{n}",      map[n]); // handle assets without .json
      }
      return map;
    }
    static void AddWords(string path, HashSet<string> set) {
      var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
      if (ta == null || string.IsNullOrEmpty(ta.text)) return;
      foreach (Match m in Regex.Matches(ta.text, @"[A-Za-z0-9_]+")) set.Add(m.Value.ToLowerInvariant());
    }
    static IEnumerable<string> Tokenize(string s) => Regex.Matches(s ?? "", @"[A-Za-z0-9_]+").Cast<Match>().Select(m => m.Value.ToLowerInvariant());
    static string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();
    static Dictionary<string, List<string>> MatchBySource(string norm, Dictionary<string, HashSet<string>> sets) {
      var toks = Tokenize(norm).ToArray();
      var outMap = sets.Keys.ToDictionary(k => k, k => new List<string>());
      foreach (var t in toks) foreach (var kv in sets) if (kv.Value.Contains(t)) outMap[kv.Key].Add(t);
      return outMap;
    }

    // === scenePlan skeleton aligned with your schema style ===
    static string BuildScenePlanSkeleton() {
      var sb = new StringBuilder();
      sb.Append("{\n  \"scenePlan\": {\n");
      sb.Append("    \"name\": \"Untitled\",\n");
      sb.Append("    \"grid\": {\"cols\":3,\"rows\":40,\"dx\":30,\"dy\":18,\"origin\":{\"x\":0,\"y\":0}},\n");
      sb.Append("    \"trackTemplate\": {\"lanes\":1,\"segments\":[\"straight\",\"straight\",\"straight\"],\"lengthUnits\":40,\"tileWidth\":1.0,\n");
      sb.Append("      \"zones\":{\"start\":{\"size\":{\"x\":2,\"y\":3}},\"end\":{\"size\":{\"x\":2,\"y\":3}}},\"killZone\":{\"y\":-5.0,\"height\":2.0}},\n");
      sb.Append("    \"difficulty\": {\"tracks\":3,\"playerSpeed\":{\"start\":5.0,\"deltaPerTrack\":0.35},\"jumpForce\":9.0,\n");
      sb.Append("      \"gapProbability\":{\"start\":0.02,\"deltaPerTrack\":0.02,\"max\":0.35},\n");
      sb.Append("      \"gapRules\":{\"noAtSpawn\":true,\"noAdjacentGaps\":true,\"maxGapWidth\":1}},\n");
      sb.Append("    \"progression\": {\"ordering\":\"snakeRows\",\"carrier\":{\"type\":\"dartboardTaxi\",\"attachKinematic\":true,\"moveSpeed\":5.0}},\n");
      sb.Append("    \"layers\": {\"track\":\"Track\",\"player\":\"Player\",\"killZone\":\"KillZone\"},\n");
      sb.Append("    \"camera\": {\"offsetX\":2.0,\"offsetY\":1.0,\"smooth\":0.15}\n");
      sb.Append("  }\n}\n");
      return sb.ToString();
    }
  }
}
