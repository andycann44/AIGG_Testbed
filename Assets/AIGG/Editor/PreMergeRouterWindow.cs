// ASCII only
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Aim2Pro.AIGG {
  public class PreMergeRouterWindow : EditorWindow {
    private string nlInput = "";
    private string normalized = "";
    private string issuesText = "";
    private string canonicalRaw = "";
    private string canonicalShown = "";
    private bool strictOK = false;
    private readonly List<string> unmatched = new List<string>();
    private readonly List<string> issues = new List<string>();

    private bool enableOpenAI = false;
    private bool aiFirst = false;
    private string apiKey = "";
    private string model = "gpt-4o-mini";

    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge")]
    public static void Open() {
      var w = GetWindow<PreMergeRouterWindow>();
      w.titleContent = new GUIContent("Pre-Merge Router");
      w.minSize = new Vector2(560, 420);
      w.Show();
    }

    private string Normalize(string s) {
      s = (s ?? "").Replace("\r"," ");
      s = Regex.Replace(s, "\\s+", " ").Trim().ToLowerInvariant();
      s = Regex.Replace(s, @"(?<=\d)\s*m\b", "m"); // join only number+m
      return s;
    }
    private int ExtractInt(string s, string pattern) { var m = Regex.Match(s, pattern); return m.Success ? int.Parse(m.Groups[1].Value) : 0; }
    private float ExtractFloat(string s, string pattern) { var m = Regex.Match(s, pattern); return m.Success ? float.Parse(m.Groups[1].Value) : 0f; }

    private void RunLocalExtract() {
      normalized = Normalize(nlInput);
      normalizedDisplay = Regex.Replace(normalized, @"\b(tiles)\s+(missing)\b", "$1/$2");
      int length = ExtractInt(normalized, @"\b(\d+)\s*m\b");
      int width  = ExtractInt(normalized, @"\bby\s+(\d+)\s*m\b");
      float missing = ExtractFloat(normalized, @"\b(\d+)\s*%\s*tiles?\s*missing\b");
      if (length <= 0 && width <= 0 && missing <= 0) { canonicalRaw = ""; return; }

      string missingLine = missing > 0 ? (",\n    \"missingTileChance\": " + missing) : "";
      string plan = "[]";
      if (Regex.IsMatch(normalized, @"\bcurve\b|\brows\b|\bdeg(rees?)?\b|\bleft\b|\bright\b")) {
        plan = "[ { \"action\": \"curveRowsOver\", \"args\": { \"side\": \"left\", \"deg\": 15, \"rows\": 10 } } ]";
      }

      canonicalRaw =
        "{\n" +
        "  \"track\": {\n" +
        "    \"length\": " + Math.Max(0,length) + ",\n" +
        "    \"width\": " + Math.Max(0,width) + ",\n" +
        "    \"tileSpacing\": 1,\n" +
        "    \"killzoneY\": -5,\n" +
        "    \"obstacles\": []" + missingLine + "\n" +
        "  },\n" +
        "  \"plan\": " + plan + "\n" +
        "}";
    }

    private bool LooksLikeCompleteJson(string s) {
      int bc=0, sc=0; foreach (var ch in s) { if (ch=='{') bc++; else if (ch=='}') bc--; else if (ch=='[') sc++; else if (ch==']') sc--; }
      if (bc != 0 || sc != 0) return false;
      return s.TrimEnd().EndsWith("}");
    }

    private void ValidateStrict() {
      unmatched.Clear(); issues.Clear(); strictOK = false; canonicalShown = "";

      if (string.IsNullOrWhiteSpace(canonicalRaw)) { issues.Add("No canonical JSON produced."); goto BUILD_ISSUES; }
      if (!LooksLikeCompleteJson(canonicalRaw)) issues.Add("Canonical JSON is syntactically incomplete (unbalanced or not closed).");
      if (!Regex.IsMatch(canonicalRaw, "\"track\"\\s*:\\s*\\{")) issues.Add("Missing 'track' object.");
      foreach (var req in new[]{"\"length\"","\"width\"","\"tileSpacing\"","\"killzoneY\"","\"obstacles\""}) {
        if (!Regex.IsMatch(canonicalRaw, req)) issues.Add("Missing required field in track: " + req.Replace("\"",""));
      }
      if (!Regex.IsMatch(canonicalRaw, "\"plan\"\\s*:\\s*\\[")) issues.Add("Missing 'plan' array.");

      // NL → canonical expectations
      var missMatch = Regex.Match(normalized, @"\b(\d+)\s*%\s*tiles?\s*missing\b");
      if (missMatch.Success) {
        var pct = missMatch.Groups[1].Value;
        var cm = Regex.Match(canonicalRaw, "\"missingTileChance\"\\s*:\\s*(\\d+(?:\\.\\d+)?)");
        if (!cm.Success) issues.Add("NL says tiles missing "+pct+"% but 'missingTileChance' is absent.");
        else if (cm.Groups[1].Value != pct) issues.Add("missingTileChance ("+cm.Groups[1].Value+") != NL "+pct+"%.");
      }

      // Unmatched tokens (allow numbers, 100m, 20%)
      var allowed = new HashSet<string>(new[]{
        "build","a","track","with","by","m","tiles","tile","missing","%","curve","rows","left","right","deg","degree","degrees"
      }, StringComparer.OrdinalIgnoreCase);
      foreach (var tok in Regex.Split(normalized, "[^a-z0-9%]+").Where(t=>!string.IsNullOrEmpty(t))) {
        if (Regex.IsMatch(tok, @"^\d+$|^\d+m$|^\d+%$")) continue;
        if (allowed.Contains(tok)) continue;
        unmatched.Add(tok);
      }

      foreach (var miss in SpecAudit.FindMissingCommands(canonicalRaw)) issues.Add("Unknown plan command: " + miss);

      if (unmatched.Count == 0 && issues.Count == 0) {
        strictOK = true;
        canonicalShown = canonicalRaw;
      } else {
        strictOK = false;
        canonicalShown = "";
      }

      BUILD_ISSUES:
      if (issues.Count == 0 && unmatched.Count == 0) issuesText = "";
      else {
        var lines = new List<string>();
        foreach (var i in issues) lines.Add("• " + i);
        if (unmatched.Count > 0) lines.Add("• Unmatched tokens: " + string.Join(", ", unmatched));
        issuesText = string.Join("\n", lines);
      }
    }

    private string BuildDiagnosticsJson() {
      var ok = strictOK ? "true" : "false";
      var arr = unmatched.Count==0 ? "" : "\"" + string.Join("\",\"", unmatched) + "\"";
      return "{ \"ok\": " + ok + ", \"unmatched\": [" + arr + "] }";
    }

    void OnGUI() {
      GUILayout.Label("Pre-Merge", EditorStyles.boldLabel);

      GUILayout.Label("Natural Language (input)");
      nlInput = EditorGUILayout.TextArea(nlInput, GUILayout.MinHeight(60));

      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Run Pre-Merge", GUILayout.Height(24))) { RunLocalExtract(); ValidateStrict(); }
      if (GUILayout.Button("Local Fix Only", GUILayout.Height(24))) { RunLocalExtract(); ValidateStrict(); }
      GUILayout.FlexibleSpace();
      using (new EditorGUI.DisabledScope(!strictOK)) {
        if (GUILayout.Button("Send \u2192 Paste & Merge", GUILayout.Height(24))) {
          PreMergeRouterAPI.Route(nlInput ?? "", canonicalRaw ?? "", BuildDiagnosticsJson());
        }
      }
      GUILayout.EndHorizontal();

      GUILayout.Space(6);
      GUILayout.Label("Normalized");
      EditorGUILayout.TextArea(normalizedDisplay, GUILayout.MinHeight(28));

      GUILayout.Label("Issues / Missing");
      EditorGUILayout.TextArea(string.IsNullOrEmpty(issuesText) ? "(none)" : issuesText, GUILayout.MinHeight(60));

      GUILayout.Label("Canonical JSON (preview)");
      if (strictOK) {
        EditorGUILayout.TextArea(canonicalShown, GUILayout.MinHeight(140));
      } else {
        EditorGUILayout.HelpBox("Preview hidden (STRICT): canonical is not complete/valid yet.", MessageType.Info);
        EditorGUILayout.TextArea("", GUILayout.MinHeight(80));
      }

      GUILayout.Space(6);
      GUILayout.Label("AI Assist");
      enableOpenAI = EditorGUILayout.ToggleLeft("Enable OpenAI", enableOpenAI);
      aiFirst = EditorGUILayout.ToggleLeft("AI-first (skip local extraction)", aiFirst);
      EditorGUI.BeginDisabledGroup(!enableOpenAI);
      apiKey = EditorGUILayout.PasswordField("API Key", apiKey);
      model  = EditorGUILayout.TextField("Model", model);
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Save Settings")) {}
      if (GUILayout.Button("Ask AI Now")) {}
      if (GUILayout.Button("Reveal AI Output Folder")) {}
      GUILayout.EndHorizontal();
      EditorGUI.EndDisabledGroup();
    }
  }
}
