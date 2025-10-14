using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
  public class DiagnosticsAsWorkbench : EditorWindow
  {
    private string _nl = "";
    private string _status = "Ready.";
    private string _lastJson = "";
    private readonly List<string> _matched = new List<string>();
    private readonly List<string> _unmatched = new List<string>();
    private Vector2 _scrollL, _scrollR, _scrollFix;
    private bool _autoCorrectBeforeScan = true;
    private List<NL_Correction.Suggestion> _fixes = new List<NL_Correction.Suggestion>();

    private static readonly string SpecDir = "Assets/AIGG/Spec";

    [MenuItem("Window/Aim2Pro/Aigg/Diagnostics (Full)", priority = 120)]
    public static void ShowDiag() {
      var w = GetWindow<DiagnosticsAsWorkbench>("Diagnostics");
      w.minSize = new Vector2(860, 560);
      w.Show();
    }

    [MenuItem("Window/Aim2Pro/Aigg/Workbench (Diagnostics Alias)", priority = 100)]
    public static void ShowAsWorkbench() { ShowDiag(); }

    private void OnGUI()
    {
      EditorGUILayout.LabelField("Natural Language", EditorStyles.boldLabel);
      _nl = EditorGUILayout.TextArea(_nl, GUILayout.Height(100));

      using (new EditorGUILayout.HorizontalScope())
      {
        _autoCorrectBeforeScan = EditorGUILayout.ToggleLeft("Auto-correct typos before Scan/Parse", _autoCorrectBeforeScan, GUILayout.Width(270));
        if (GUILayout.Button("Suggest fixes", GUILayout.Height(24))) SuggestFixes();
        if (GUILayout.Button("Apply fixes", GUILayout.Height(24))) ApplyFixes();
      }

      if (_fixes != null && _fixes.Count > 0)
      {
        using (new EditorGUILayout.VerticalScope("box"))
        {
          EditorGUILayout.LabelField("Suggested fixes ("+_fixes.Count+")", EditorStyles.boldLabel);
          using (var s = new EditorGUILayout.ScrollViewScope(_scrollFix, GUILayout.Height(110)))
          {
            _scrollFix = s.scrollPosition;
            for (int i=0;i<_fixes.Count;i++)
            {
              var f = _fixes[i];
              EditorGUILayout.LabelField("• " + f.Original + " → " + f.Replacement + "  [" + f.Reason + "]");
            }
          }
        }
      }

      using (new EditorGUILayout.HorizontalScope())
      {
        if (GUILayout.Button("Parse NL (local)", GUILayout.Height(26))) { if (_autoCorrectBeforeScan) { SuggestFixes(); ApplyFixes(); } RunLocal(); }
        if (GUILayout.Button("Scan (Matched / Unmatched)", GUILayout.Height(26))) { if (_autoCorrectBeforeScan) { SuggestFixes(); ApplyFixes(); } ScanNow(); }
        if (GUILayout.Button("Self-Fix (Local Patch → Router)", GUILayout.Height(26)))
          PreMergeRouterAPI.Route(BuildLocalPatchJSON(_unmatched));
        if (GUILayout.Button("Self-Fix (AI → Router)", GUILayout.Height(26)))
          PreMergeRouterAPI.Route(BuildAIEnvelope(_nl, _unmatched));
      }

      EditorGUILayout.Space();
      using (new EditorGUILayout.HorizontalScope())
      {
        using (new EditorGUILayout.VerticalScope("box"))
        {
          EditorGUILayout.LabelField("Matched ("+_matched.Count+")", EditorStyles.boldLabel);
          using (var s = new EditorGUILayout.ScrollViewScope(_scrollL, GUILayout.Height(160))) {
            _scrollL = s.scrollPosition;
            for (int i=0;i<_matched.Count;i++) EditorGUILayout.LabelField("• " + _matched[i]);
          }
        }
        using (new EditorGUILayout.VerticalScope("box"))
        {
          EditorGUILayout.LabelField("Unmatched ("+_unmatched.Count+")", EditorStyles.boldLabel);
          using (var s = new EditorGUILayout.ScrollViewScope(_scrollR, GUILayout.Height(160))) {
            _scrollR = s.scrollPosition;
            for (int i=0;i<_unmatched.Count;i++) EditorGUILayout.LabelField("• " + _unmatched[i]);
          }
        }
      }

      EditorGUILayout.Space();
      using (new EditorGUILayout.HorizontalScope())
      {
        if (GUILayout.Button("Send Last JSON → Pre-Merge", GUILayout.Height(26))) {
          var json = string.IsNullOrEmpty(_lastJson) ? "{ \"note\": \"empty payload\" }" : _lastJson;
          PreMergeRouterAPI.Route(json);
        }
        if (GUILayout.Button("Copy Last JSON", GUILayout.Height(26))) {
          EditorGUIUtility.systemCopyBuffer = _lastJson ?? "";
          _status = "JSON copied to clipboard.";
        }
      }

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
      EditorGUILayout.HelpBox(_status ?? "", MessageType.Info);

      EditorGUILayout.LabelField("Last JSON (readonly preview)", EditorStyles.boldLabel);
      _lastJson = EditorGUILayout.TextArea(_lastJson ?? "", GUILayout.Height(140));
    }

    // === Correction actions ===
    private void SuggestFixes()
    {
      _fixes = NL_Correction.Suggest(_nl);
      _status = (_fixes.Count == 0) ? "No changes suggested." : ("Suggested " + _fixes.Count + " change(s).");
    }

    private void ApplyFixes()
    {
      if (_fixes == null || _fixes.Count == 0) { _status = "No fixes to apply."; return; }
      _nl = NL_Correction.Apply(_nl, _fixes);
      _status = "Applied " + _fixes.Count + " change(s).";
    }

    // === Workbench-equivalent actions ===
    private void RunLocal()
    {
      try {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
          var t = asm.GetType("Aim2Pro.AIGG.AIGG_LocalIntentEngine");
          if (t == null) continue;
          var m = t.GetMethod("RunToJson", BindingFlags.Public | BindingFlags.Static);
          if (m == null) continue;
          _lastJson = (string)m.Invoke(null, new object[]{ _nl }) ?? "";
          _status = string.IsNullOrEmpty(_lastJson) ? "Local engine returned empty JSON." : "Local parse OK.";
          return;
        }
        _lastJson = "{\"nl\":\""+Esc(_nl)+"\",\"note\":\"local engine not found\"}";
        _status = "Local engine missing; produced fallback JSON.";
      } catch (Exception e) {
        _lastJson = "{\"error\":\"Local engine exception: "+Esc(e.Message)+"\"}";
        _status = "Local parse threw: " + e.Message;
      }
    }

    // ===== improved scanner: recognizes quantities like 100m, 45 degrees, 10 rows =====
    private void ScanNow()
    {
      _matched.Clear(); _unmatched.Clear();
      string text = (_nl ?? "").Trim().ToLowerInvariant();
      if (string.IsNullOrEmpty(text)) { _status="NL empty."; return; }

      // Pre-accept specials so they never show as unmatched
      var specials = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

      foreach (Match m in Regex.Matches(text, @"\b\d+(\.\d+)?\s*(m|cm|mm|meter|meters|metre|metres)\b"))
      {
        string v = m.Value.Replace("metre","meter").Replace("metres","meters").Replace(" ", "");
        specials.Add(v);
      }

      foreach (Match m in Regex.Matches(text, @"\b\d+(\.\d+)?\s*(degree|degrees|deg)\b"))
      {
        string v = m.Value.Replace("deg","degrees").Trim();
        // normalize "45 degree" -> "45 degrees"
        v = Regex.Replace(v, @"\bdegree\b", "degrees");
        specials.Add(v);
      }

      foreach (Match m in Regex.Matches(text, @"\b\d+\s*rows?\b"))
      {
        string v = m.Value.Replace(" ", "");
        specials.Add(v);
      }

      // Show specials nicely in Matched
      foreach (var sp in specials) _matched.Add(sp);

      // Normal scan
      HashSet<string> lex = LoadLexicon();
      List<string> rx = LoadIntentRegexes();

      foreach (Match m in Regex.Matches(text, "[a-z0-9]+"))
      {
        string w = m.Value;

        // already handled as special quantity?
        if (specials.Contains(w)) continue;

        if (IsStop(w)) { _matched.Add(w); continue; }

        bool ok = lex.Contains(w);

        if (!ok)
        {
          // number+unit compact tokens (e.g., "100m")
          if (Regex.IsMatch(w, @"^\d+(\.\d+)?(m|cm|mm)$")) ok = true;

          // try intent regexes
          if (!ok)
          {
            for (int i=0;i<rx.Count;i++)
            {
              string pat = rx[i];
              try {
                string one = pat.Replace("\\\\","\\");
                if (Regex.IsMatch(text, one, RegexOptions.IgnoreCase)) { ok = true; break; }
              } catch {}
            }
          }
        }

        if (ok) _matched.Add(w); else _unmatched.Add(w);
      }

      _status = "Scan complete.";
    }

    // === spec helpers ===
    private static bool IsStop(string w)
    {
      switch (w) {
        case "a": case "an": case "the": case "and": case "or": case "to": case "of":
        case "by": case "over": case "across": case "with": case "without": case "for":
        case "in": case "on": case "at": case "is": case "are": case "left": case "right":
        case "deg": case "degree": case "degrees": case "m": case "meter": case "meters":
        case "metre": case "metres": case "mm": case "cm": case "percent": case "percentage":
          return true;
      } return false;
    }

    private static HashSet<string> LoadLexicon()
    {
      var set = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
      try{
        string p = Path.Combine(SpecDir, "lexicon.json");
        if (File.Exists(p)) {
          string s = File.ReadAllText(p);
          foreach (Match m in Regex.Matches(s, "\"([^\"]+)\"")) {
            string v = m.Groups[1].Value.Trim(); if (!string.IsNullOrEmpty(v)) set.Add(v);
          }
        }
      } catch {}
      return set;
    }

    private static List<string> LoadIntentRegexes()
    {
      var list = new List<string>();
      try{
        string p = Path.Combine(SpecDir, "intents.json");
        if (File.Exists(p)) {
          string s = File.ReadAllText(p);
          foreach (Match m in Regex.Matches(s, "\"regex\"\\s*:\\s*\"([^\"]+)\"")) {
            string v = m.Groups[1].Value.Trim(); if (!string.IsNullOrEmpty(v)) list.Add(v);
          }
        }
      } catch {}
      return list;
    }

    private static string BuildLocalPatchJSON(List<string> unmatched)
    {
      var items = new List<string>();
      for (int i=0;i<unmatched.Count;i++){
        string u = unmatched[i]; if (string.IsNullOrWhiteSpace(u)) continue;
        string rx="\\\\b"+Regex.Escape(u)+"\\\\b";
        items.Add("{\"name\":\"auto-"+u+"\",\"regex\":\""+rx+"\",\"ops\":[{\"op\":\"custom\",\"path\":\"$.\",\"value\":\"define-"+u+"\"}]}");
      }
      return "{ \"intents\": ["+string.Join(",", items.ToArray())+"] }";
    }

    private static string BuildAIEnvelope(string nl, List<string> unmatched)
    {
      var q = new List<string>();
      for (int i=0;i<unmatched.Count;i++) q.Add("\""+Esc(unmatched[i])+"\"");
      return "{\"nl\":\""+Esc(nl)+"\",\"issues\":{\"unmatched\":["+string.Join(",", q.ToArray())+"]},\"request\":\"propose intents\",\"schema\":\"intents\"}";
    }

    private static string Esc(string s){ if (s==null) return ""; return s.Replace("\\","\\\\").Replace("\"","\\\""); }
  }
}
