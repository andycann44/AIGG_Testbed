// ASCII only
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Aim2Pro.AIGG {
  public class PreMergeRouterWindow : EditorWindow {
    // --- state ---
    private string nlInput = "";
    private string normalized = "";
    private string normalizedDisplay = "";
    private string issuesText = "";
    private string canonicalRaw = "";
    private string canonicalShown = "";
    private bool strictOK = false;
    private readonly List<string> unmatched = new List<string>();
    private readonly List<string> issues = new List<string>();

    // AI
    private bool enableOpenAI = false;
    private bool aiFirst = false;
    private bool autoAiFix = false;
    private string apiKey = "";
    private string model = "gpt-4o-mini";
    private bool aiAlreadyTried = false;

    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge")]
    public static void Open() {
      var w = GetWindow<PreMergeRouterWindow>();
      w.titleContent = new GUIContent("Pre-Merge Router");
      w.minSize = new Vector2(560, 420);
      w.Show();
    }

    void OnEnable() {
      // load persisted AI settings (no-op if class absent)
      try {
        apiKey = AIGGEditorPrefs.LoadKey();
        model = AIGGEditorPrefs.LoadModel();
        autoAiFix = AIGGEditorPrefs.LoadAuto();
      } catch {}
    }

    // --- helpers ---
    private string Normalize(string s) {
      s = (s ?? "").Replace("\r"," ");
      s = Regex.Replace(s, "\\s+", " ").Trim().ToLowerInvariant();
      // join ONLY number + m (e.g., "6 m" -> "6m")
      s = Regex.Replace(s, @"(?<=\d)\s*m\b", "m");
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

      canonicalRaw = "";
      if (length <= 0 && width <= 0 && missing <= 0) return;

      string missingLine = missing > 0 ? (",\n    \"missingTileChance\": " + missing) : "";
      string plan = "[]";
      if (Regex.IsMatch(normalized, @"\bcurve\b|\brows\b|\bdeg(rees?)?\b|\bleft\b|\bright\b")) {
        // simple local inference for demo; spec will enforce via commands.json
        plan = "[ { \"action\": \"curveRowsOver\", \"args\": { \"side\": \"left\", \"deg\": 45, \"rows\": 10 } } ]";
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
      aiAlreadyTried = false; // reset guard per validation pass
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

      // Unmatched tokens (allow numbers, 100m, 20%, common words)
      var allowed = new HashSet<string>(new[]{
        "build","a","track","with","by","m","tiles","tile","missing","%","curve","rows","left","right","deg","degree","degrees","and","over"
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

      // Optional: auto AI fix if strict failed
      if (!strictOK && enableOpenAI && autoAiFix && !aiAlreadyTried) {
        aiAlreadyTried = true;
        RunAIAutofix();
      }
    }

    private string BuildDiagnosticsJson() {
      var ok = strictOK ? "true" : "false";
      var arr = unmatched.Count==0 ? "" : "\"" + string.Join("\",\"", unmatched) + "\"";
      return "{ \"ok\": " + ok + ", \"unmatched\": [" + arr + "] }";
    }

    private void RunAIAutofix() {
      if (!enableOpenAI) { EditorUtility.DisplayDialog("AI disabled","Enable OpenAI to use autofix.","OK"); return; }
      if (string.IsNullOrEmpty(apiKey)) { EditorUtility.DisplayDialog("Missing API Key","Enter your OpenAI API Key.","OK"); return; }
      try {
        var missing = SpecAudit.FindMissingCommands(canonicalRaw ?? "");
        var fix = AIAutoFix.Ask(apiKey, model, nlInput ?? "", normalized ?? "", canonicalRaw ?? "", unmatched, missing, out var err);
        if (err != null) { Debug.LogWarning("[AIAutoFix] " + err); EditorUtility.DisplayDialog("AI error", err, "OK"); return; }
        
        // If AI provided a canonical JSON, adopt it before spec apply
        if (fix != null && !string.IsNullOrEmpty(fix.canonical)) {
          canonicalRaw = fix.canonical;
        }
bool changed = AIAutoFix.Apply(fix);
        if (changed) {
          ValidateStrict();
          EditorUtility.DisplayDialog("Spec updated by AI",
            "commands: " + (fix.commands?.Count ?? 0) + ", macros: " + (fix.macros?.Count ?? 0) + ", fieldMap: " + (fix.fieldMap?.Count ?? 0),
            "OK");
        } else {
          EditorUtility.DisplayDialog("No changes", "AI suggested no new entries.", "OK");
        }
      } catch (Exception ex) {
        Debug.LogWarning("[Pre-Merge AI] " + ex.Message);
      }
    }

    // --- UI ---
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
      EditorGUILayout.TextArea((!string.IsNullOrEmpty(normalizedDisplay)? normalizedDisplay : normalized), GUILayout.MinHeight(28));

      GUILayout.Label("Issues / Missing");
      EditorGUILayout.TextArea(string.IsNullOrEmpty(issuesText) ? "(none)" : issuesText, GUILayout.MinHeight(60));

      // Spec auto-fix buttons (manual)
      try {
        var _missingCmds = SpecAudit.FindMissingCommands(canonicalRaw ?? "");
        if (_missingCmds != null && _missingCmds.Count > 0) {
          EditorGUILayout.Space(4);
          if (GUILayout.Button("Add missing commands to spec", GUILayout.Height(22))) {
            if (SpecAutoFix.EnsureCommandsExist(_missingCmds)) { EditorUtility.DisplayDialog("Spec updated", "Commands added: " + string.Join(", ", _missingCmds), "OK"); ValidateStrict(); }
          }
        }
        if (unmatched != null && unmatched.Count > 0) {
          EditorGUILayout.Space(4);
          if (GUILayout.Button($"Add {unmatched.Count} unmatched token(s) as macros", GUILayout.Height(22))) {
            if (SpecAutoFix.EnsureMacrosExist(unmatched)) { EditorUtility.DisplayDialog("Spec updated", "Macros added: " + string.Join(", ", unmatched), "OK"); ValidateStrict(); }
          }
        }
        bool tilesMissingInNL = Regex.IsMatch(normalized, @"\btiles\s+missing\b");
        if (tilesMissingInNL && !SpecAutoFix.HasFieldMapPair("tiles missing", "track.missingTileChance")) {
          EditorGUILayout.Space(4);
          if (GUILayout.Button("Add field-map: \"tiles missing\" → track.missingTileChance", GUILayout.Height(22))) {
            var ok = SpecAutoFix.EnsureFieldMapPairs(new Dictionary<string,string>{{"tiles missing","track.missingTileChance"}});
            if (ok) EditorUtility.DisplayDialog("Spec updated", "Field map added for tiles missing.", "OK");
          }
        }
      } catch (Exception ex) { Debug.LogWarning("[Pre-Merge] Spec auto-fix failed: " + ex.Message); }

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
      autoAiFix = EditorGUILayout.ToggleLeft("Auto-apply AI fixes", autoAiFix);
      EditorGUI.BeginDisabledGroup(!enableOpenAI);
      apiKey = EditorGUILayout.PasswordField("API Key", apiKey);
      model  = EditorGUILayout.TextField("Model", model);
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Save Settings")) { AIGGEditorPrefs.Save(apiKey, model, autoAiFix); EditorUtility.DisplayDialog("Saved","AI settings saved.","OK"); }
      if (GUILayout.Button("Ask AI Now")) { RunAIAutofix(); }
      if (GUILayout.Button("Reveal AI Output Folder")) {}
      GUILayout.EndHorizontal();
      EditorGUI.EndDisabledGroup();
    }
  }
}
