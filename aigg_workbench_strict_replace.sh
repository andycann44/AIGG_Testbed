#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
mkdir -p "Assets/AIGG/Editor/Workbench"
if [[ -f "$F" ]]; then
  BK="Assets/AIGG/_Backups/StrictReplace_$(date +%Y%m%d_%H%M%S)"
  mkdir -p "$BK"
  cp "$F" "$BK/WorkbenchWindow.cs.bak"
  echo "Backup: $BK/WorkbenchWindow.cs.bak"
fi

cat > "$F" <<'CS'
// Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;
using System.Diagnostics;

namespace Aim2Pro.AIGG.Workbench
{
  // no tuples
  internal class CurveSpec {
    public string side;
    public float degrees;
    public float? rows;
    public CurveSpec(string side, float degrees, float? rows) {
      this.side = side; this.degrees = degrees; this.rows = rows;
    }
  }

  public class WorkbenchWindow : EditorWindow
  {
    // ===== Config =====
    static readonly string SpecDir = "Assets/AIGG/Spec";
    static readonly string[] SpecFiles = { "intents","macros","lexicon","commands","fieldmap","registry","schema" };
    static readonly string[] BlocklistedParsers = { "AutoScene", "DefaultScene", "ScenePlanSkeleton" };

    // Ignorables (do NOT include 'under', 'sloping', etc — they must cause coverage failure)
    static readonly HashSet<string> IgnorableTokens = new HashSet<string> {
      "m","meter","meters","by","x","wide","every","tile","tiles",
      "the","a","an","of","in","on","at","to","for","and","or","with","per","long",
      "make","build","create","random","randomly",
      "degree","degrees","deg",
      "over","across","through","around","between","along","then","from","going","into","onto","via"
    };

    // Allowed filler words that don't carry semantics but are common
    static readonly HashSet<string> AllowedFillers = new HashSet<string>{
      "with","and","then","a","the","track","tracks"
    };

    // ===== prefs / scripts =====
    const string PrefAutoPatch = "AIGG.Workbench.AutoPatchLexicon";
    bool _autoPatch = true;
    bool _isReparsing = false;
    List<string> _lastUnmatched = new List<string>();

    static string ProjectRoot => Directory.GetCurrentDirectory();
    static string LocalHealScript  => Path.Combine(ProjectRoot, "aigg_self_heal_local.zsh");
    static string OpenAIHealScript => Path.Combine(ProjectRoot, "aigg_self_heal_openai.zsh");

    // ===== UI =====
    string _nl   = "100m by 20 m with left45 degree curve and right35 degree chicane over 20 rows";
    string _diag = "Spec: " + SpecDir;
    string _json = "";
    Vector2 _diagScroll = Vector2.zero;

    [MenuItem("Window/Aim2Pro/Workbench", false, 100)]
    public static void ShowWindow()
    {
      var w = GetWindow<WorkbenchWindow>();
      w.titleContent = new GUIContent("Workbench");
      w.minSize = new Vector2(720, 560);
      w.Show();
    }

    void OnEnable() { _autoPatch = EditorPrefs.GetBool(PrefAutoPatch, true); }

    void OnGUI()
    {
      EditorGUILayout.LabelField("Natural language prompt", EditorStyles.boldLabel);
      _nl = EditorGUILayout.TextArea(_nl, GUILayout.MinHeight(70));

      EditorGUILayout.Space(4);
      using (new EditorGUILayout.HorizontalScope())
      {
        if (GUILayout.Button("Parse NL (local intents)", GUILayout.Height(24))) ParseLocal();
        if (GUILayout.Button("Open Paste & Merge",      GUILayout.Height(24))) OpenPasteAndMerge();
        if (GUILayout.Button("Copy skeleton intent",    GUILayout.Height(24))) CopySkeletonIntent();
      }

      EditorGUILayout.Space(4);
      using (new EditorGUILayout.HorizontalScope())
      {
        GUI.enabled = File.Exists(LocalHealScript);
        if (GUILayout.Button("Self-heal (local)", GUILayout.Height(22)))
        {
          var ec = SelfHealLocal(_nl);
          if (ec == 0) { _isReparsing = true; EditorApplication.delayCall += () => { _isReparsing = false; ParseLocal(); }; }
          else ShowNotification(new GUIContent("Local self-heal not found / failed"));
        }
        GUI.enabled = File.Exists(OpenAIHealScript);
        if (GUILayout.Button("Self-heal (OpenAI)", GUILayout.Height(22)))
        {
          var ok = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
          if (!ok) EditorUtility.DisplayDialog("OpenAI key missing", "Set OPENAI_API_KEY in your shell/session.", "OK");
          var ec = ok ? SelfHealOpenAI(_nl) : -1;
          if (ec == 0) ShowNotification(new GUIContent("AI patch created → Paste & Merge opened"));
        }
        GUI.enabled = true;
      }

      bool newAuto = EditorGUILayout.ToggleLeft("Self-update lexicon on unmatched (auto-reparse)", _autoPatch);
      if (newAuto != _autoPatch) { _autoPatch = newAuto; EditorPrefs.SetBool(PrefAutoPatch, _autoPatch); }

      EditorGUILayout.Space(8);
      EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);
      _diagScroll = EditorGUILayout.BeginScrollView(_diagScroll, GUILayout.Height(160));
      using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextArea(_diag, GUI.skin.textArea, GUILayout.ExpandHeight(false));
      EditorGUILayout.EndScrollView();

      EditorGUILayout.Space(4);
      EditorGUILayout.LabelField("JSON Output", EditorStyles.boldLabel);
      _json = EditorGUILayout.TextArea(_json, GUILayout.MinHeight(240));

      EditorGUILayout.Space(4);
      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.LabelField("Spec: " + SpecDir, EditorStyles.miniLabel);
        if (GUILayout.Button("Open Spec Folder", GUILayout.Width(160))) EditorUtility.RevealInFinder(SpecDir);
        if (GUILayout.Button("Reset to scenePlan skeleton", GUILayout.Width(220))) _json = BuildScenePlanSkeleton();
      }

      EditorGUILayout.HelpBox(
        "If parsing fails OR leaves unmatched/unaccounted tokens, output stays EMPTY.",
        MessageType.Info);
    }

    void ParseLocal()
    {
      try
      {
        _json = "";

        string normalized = Normalize(_nl);
        var sets = LoadSpecSets();
        var matches = MatchBySource(normalized, sets);
        var toks = Tokenize(normalized).ToArray();
        var unmatchedRaw = toks.Where(t => !matches.Any(kv => kv.Value.Contains(t))).ToArray();
        var ignored = unmatchedRaw.Where(IsIgnorableToken).Distinct().ToArray();
        var unmatched = unmatchedRaw.Where(t => !IsIgnorableToken(t)).Distinct().ToArray();

        _lastUnmatched = unmatched.ToList();

        // ---- Gate 1: Unmatched → EMPTY (optionally auto-heal then reparse)
        if (unmatched.Length > 0)
        {
          if (_autoPatch && !_isReparsing)
          {
            int added = ApplyLexiconPatch(unmatched);
            if (added > 0)
            {
              AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
              _isReparsing = true;
              EditorApplication.delayCall += () => { try { ParseLocal(); } finally { _isReparsing = false; } };
            }
          }
          ShowNotification(new GUIContent("Unmatched tokens → output EMPTY"));
          // diagnostics are built below
          BuildDiagnostics(normalized, matches, unmatched, ignored, "Output cleared due to unmatched tokens.");
          return;
        }

        // ---- Gate 2: Residual semantics not covered by known patterns → EMPTY
        var residual = GetResidualMeaningfulTokens(normalized);
        if (residual.Length > 0)
        {
          ShowNotification(new GUIContent("Unaccounted tokens → output EMPTY"));
          BuildDiagnostics(normalized, matches, unmatched, ignored, "Unaccounted tokens: " + string.Join(", ", residual));
          return;
        }

        // ---- If clean → try canonical; else try project interpreters
        var canonical = TryConvertNLToCanonical(_nl);
        if (!string.IsNullOrEmpty(canonical))
        {
          _json = canonical;
          ShowNotification(new GUIContent("Parsed OK (local)"));
          BuildDiagnostics(normalized, matches, unmatched, ignored, null);
          return;
        }

        var json = TryRunLocalParsers(_nl);
        if (!string.IsNullOrEmpty(json))
        {
          if (LooksLikeFallback(json)) ShowNotification(new GUIContent("scenePlan suppressed → EMPTY"));
          else { _json = json; ShowNotification(new GUIContent("Parsed OK")); }
          BuildDiagnostics(normalized, matches, unmatched, ignored, null);
          return;
        }

        ShowNotification(new GUIContent("No parser / no JSON → EMPTY"));
        BuildDiagnostics(normalized, matches, unmatched, ignored, null);
      }
      catch (Exception ex)
      {
        _json = "";
        _diag = ex.GetType().Name + ": " + ex.Message + "\n" + _diag;
        ShowNotification(new GUIContent("Parse error → EMPTY"));
      }
    }

    void BuildDiagnostics(string normalized, Dictionary<string,List<string>> matches,
                          string[] unmatched, string[] ignored, string extraNote)
    {
      var sb = new StringBuilder();
      sb.AppendLine("Normalized:");
      sb.AppendLine("  " + normalized);
      sb.AppendLine();
      sb.AppendLine("Matched (by source):");
      foreach (var k in new[] { "intents","macros","lexicon","commands","fieldmap","registry","schema" })
        sb.AppendLine("  " + k + ": " + J(matches, k));
      sb.AppendLine();
      sb.AppendLine("Unmatched (" + unmatched.Length + "):");
      if (unmatched.Length == 0) sb.AppendLine("  (none)");
      else foreach (var u in unmatched) sb.AppendLine("  - " + u);
      if (ignored.Length > 0)
      {
        sb.AppendLine();
        sb.AppendLine("Ignored tokens (" + ignored.Length + "):");
        foreach (var u in ignored) sb.AppendLine("  - " + u);
      }
      if (!string.IsNullOrEmpty(extraNote))
      {
        sb.AppendLine();
        sb.AppendLine(extraNote);
      }
      _diag = sb.ToString();
    }

    // ---------- helpers ----------
    static string J(Dictionary<string,List<string>> m, string k) =>
      string.Join(", ", (m != null && m.ContainsKey(k) ? m[k] : new List<string>()).Distinct());

    // Identify tokens we didn't turn into JSON (even if they matched lexicon)
    static string[] GetResidualMeaningfulTokens(string norm)
    {
      var toks = Tokenize(norm).ToArray();
      var covered = new HashSet<string>();

      // Dimensions
      if (Regex.IsMatch(norm, @"\d+(?:\.\d+)?\s*m\s*(?:x|by|×)\s*\d+(?:\.\d+)?\s*m"))
        covered.UnionWith(new[]{"m","by","x"});

      // Curves/chicanes (both orders) + optional "over N rows"
      foreach (Match m in Regex.Matches(
        norm,
        @"\b(left|right)\b\s*(\d+(?:\.\d+)?)\s*(?:degrees?|deg)\s*(?:curve|chicane)\b(?:\s*over\s*(\d+(?:\.\d+)?)\s*rows?)?",
        RegexOptions.Multiline))
      {
        covered.UnionWith(new[]{"left","right","degree","degrees","deg","curve","chicane"});
        if (m.Groups[3].Success) covered.UnionWith(new[]{"over","rows","row"});
      }
      foreach (Match m in Regex.Matches(
        norm,
        @"\b(\d+(?:\.\d+)?)\s*(?:degrees?|deg)\s*(left|right)\b\s*(?:curve|chicane)\b(?:\s*over\s*(\d+(?:\.\d+)?)\s*rows?)?",
        RegexOptions.Multiline))
      {
        covered.UnionWith(new[]{"left","right","degree","degrees","deg","curve","chicane"});
        if (m.Groups[3].Success) covered.UnionWith(new[]{"over","rows","row"});
      }

      // tiles missing / gaps %
      if (Regex.IsMatch(norm, @"(?:random(?:ly)?\s+)?(?:tiles?\s+missing|missing\s+tiles)"))
        covered.UnionWith(new[]{"tiles","tile","missing","random","randomly"});
      if (Regex.IsMatch(norm, @"\b(?:gaps?|gap\s*(?:chance|prob(?:ability)?))\b"))
        covered.UnionWith(new[]{"gap","gaps","chance","prob","probability"});

      var residual = new List<string>();
      foreach (var t in toks)
      {
        if (IsIgnorableToken(t)) continue;
        if (AllowedFillers.Contains(t)) continue;
        if (covered.Contains(t)) continue;
        residual.Add(t);
      }
      return residual.Distinct().ToArray();
    }

    // Build JSON (supports multi-curves)
    static string BuildSimpleTrackJson(
      float length, float width,
      float? missingChance = null,
      float? gapChance = null,
      List<CurveSpec> curves = null)
    {
      var sb = new StringBuilder();
      sb.Append("{\n");
      sb.Append("  \"track\": {\n");
      sb.Append($"    \"length\": {length.ToString(CultureInfo.InvariantCulture)},\n");
      sb.Append($"    \"width\": {width.ToString(CultureInfo.InvariantCulture)},\n");
      sb.Append("    \"tileSpacing\": 1,\n");
      if (missingChance.HasValue)
        sb.Append($"    \"missingTileChance\": {missingChance.Value.ToString(CultureInfo.InvariantCulture)},\n");
      if (gapChance.HasValue)
        sb.Append($"    \"gapChance\": {gapChance.Value.ToString(CultureInfo.InvariantCulture)},\n");
      sb.Append("    \"obstacles\": [\n    ],\n");
      sb.Append("    \"killzoneY\": -5\n");
      sb.Append("  }");
      if (curves != null && curves.Count > 0)
      {
        sb.Append(",\n  \"curves\": [\n");
        for (int i = 0; i < curves.Count; i++)
        {
          var c = curves[i];
          sb.Append("    { ");
          sb.Append($"\"side\": \"{c.side}\", \"degrees\": {c.degrees.ToString(CultureInfo.InvariantCulture)}");
          if (c.rows.HasValue) sb.Append($", \"rows\": {c.rows.Value.ToString(CultureInfo.InvariantCulture)}");
          sb.Append(" }");
          if (i < curves.Count - 1) sb.Append(",");
          sb.Append("\n");
        }
        sb.Append("  ]");
      }
      sb.Append("\n}");
      return sb.ToString();
    }

    // Canonical extraction (dims + curves/chicanes)
    static string TryConvertNLToCanonical(string nl)
    {
      if (string.IsNullOrWhiteSpace(nl)) return null;
      var s = nl.ToLowerInvariant();
      s = Regex.Replace(s, @"(?<=[a-z])(?=\d)", " ");
      s = Regex.Replace(s, @"(?<=\d)(?=[a-z])", " ");

      var dim = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*m\s*(?:x|by|×)\s*(\d+(?:\.\d+)?)\s*m");
      if (!dim.Success) return null;

      float length = float.Parse(dim.Groups[1].Value, CultureInfo.InvariantCulture);
      float width  = float.Parse(dim.Groups[2].Value, CultureInfo.InvariantCulture);

      float? missingChance = null;
      float? gapChance = null;

      var miss1 = Regex.Match(s, @"(?:random(?:ly)?\s+)?(?:tiles?\s+missing|missing\s+tiles)(?:\s*(?:at|of|about|around)?\s*(\d+(?:\.\d+)?)\s*%)?");
      if (miss1.Success) {
        missingChance = miss1.Groups[1].Success
          ? Math.Max(0f, Math.Min(1f, float.Parse(miss1.Groups[1].Value, CultureInfo.InvariantCulture)/100f))
          : 0.15f;
      }
      var miss2 = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*%\s*(?:tiles?\s+missing|missing\s+tiles)");
      if (miss2.Success)
        missingChance = Math.Max(0f, Math.Min(1f, float.Parse(miss2.Groups[1].Value, CultureInfo.InvariantCulture)/100f));

      var gaps = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*%\s*(?:gaps?|gap\s*(?:chance|prob(?:ability)?))");
      if (gaps.Success)
        gapChance = Math.Max(0f, Math.Min(1f, float.Parse(gaps.Groups[1].Value, CultureInfo.InvariantCulture)/100f));

      var curves = new List<CurveSpec>();
      foreach (Match m in Regex.Matches(
        s,
        @"\b(left|right)\b\s*(\d+(?:\.\d+)?)\s*(?:degrees?|deg)\s*(?:curve|chicane)\b(?:\s*over\s*(\d+(?:\.\d+)?)\s*rows?)?",
        RegexOptions.Multiline))
      {
        var side = m.Groups[1].Value;
        var deg  = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        float? rows = null;
        if (m.Groups[3].Success) rows = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        curves.Add(new CurveSpec(side, deg, rows));
      }
      foreach (Match m in Regex.Matches(
        s,
        @"\b(\d+(?:\.\d+)?)\s*(?:degrees?|deg)\s*(left|right)\b\s*(?:curve|chicane)\b(?:\s*over\s*(\d+(?:\.\d+)?)\s*rows?)?",
        RegexOptions.Multiline))
      {
        var deg  = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var side = m.Groups[2].Value;
        float? rows = null;
        if (m.Groups[3].Success) rows = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        curves.Add(new CurveSpec(side, deg, rows));
      }

      return BuildSimpleTrackJson(length, width, missingChance, gapChance, curves);
    }

    static bool LooksLikeFallback(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return true;
      var low = s.ToLowerInvariant();
      if (Regex.IsMatch(low, "\"scene\\s*plan\"\\s*:\\s*\\{")) return true;
      if (Regex.IsMatch(low, "\"type\"\\s*:\\s*\"scene\\s*plan\"")) return true;
      if (low.Contains("sceneplan")) return true;
      return false;
    }

    static bool BlocklistedMember(Type t, System.Reflection.MethodInfo m)
    {
      string tn = t.FullName ?? t.Name ?? "";
      foreach (var b in BlocklistedParsers)
      {
        if (tn.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (m.Name.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0) return true;
      }
      return false;
    }

    string TryRunLocalParsers(string nl)
    {
      var asms = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => (a.GetName().Name ?? "").Contains("Assembly-CSharp"));

      foreach (var t in asms.SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } }))
      {
        var tn = (t.FullName ?? t.Name ?? "").ToLowerInvariant();
        if (!(tn.Contains("aigg") || tn.Contains("nl") || tn.Contains("interpreter") || tn.Contains("nltojson")))
          continue;

        foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
        {
          if (m.ReturnType != typeof(string)) continue;
          if (BlocklistedMember(t, m)) continue;

          var mn = m.Name.ToLowerInvariant();
          if (!(mn.Contains("json") || mn.Contains("prompt") || mn.Contains("interpret") || mn.Contains("run") || mn.Contains("generate") || mn.Contains("compile")))
            continue;

          var ps = m.GetParameters();
          try
          {
            string r = null;
            if (ps.Length == 1 && ps[0].ParameterType == typeof(string)) {
              r = m.Invoke(null, new object[]{ nl }) as string;
            } else if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(string)) {
              r = m.Invoke(null, new object[]{ nl, SpecDir }) as string;
            } else continue;

            if (string.IsNullOrWhiteSpace(r)) continue;
            var s = r.TrimStart();
            if (!(s.StartsWith("{") || s.StartsWith("["))) continue;

            if (LooksLikeFallback(s)) {
              _diag = $"Suppressed fallback from {t.FullName}.{m.Name}\n" + _diag;
              continue;
            }

            _diag = $"Parsed via {t.FullName}.{m.Name}\n" + _diag;
            return s;
          }
          catch { continue; }
        }
      }
      return null;
    }

    // ---- Spec scan, tokenization, matching ----
    static Dictionary<string, HashSet<string>> LoadSpecSets()
    {
      var map = new Dictionary<string, HashSet<string>>();
      foreach (var n in SpecFiles) map[n] = new HashSet<string>();
      foreach (var n in SpecFiles)
      {
        AddWords($"{SpecDir}/{n}.json", map[n]);
        AddWords($"{SpecDir}/{n}",      map[n]);
      }
      return map;
    }

    static void AddWords(string path, HashSet<string> set)
    {
      string text = null;
      var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
      if (ta == null) ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path + ".txt");
      if (ta == null) ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path + ".json");
      if (ta != null) text = ta.text;

      if (text == null) {
        if (File.Exists(path)) text = File.ReadAllText(path);
        else if (File.Exists(path + ".txt")) text = File.ReadAllText(path + ".txt");
        else if (File.Exists(path + ".json")) text = File.ReadAllText(path + ".json");
      }

      if (string.IsNullOrEmpty(text)) return;
      foreach (Match m in Regex.Matches(text, @"[A-Za-z0-9_]+")) set.Add(m.Value.ToLowerInvariant());
    }

    static IEnumerable<string> Tokenize(string s) =>
      Regex.Matches(s ?? "", @"\d+(?:\.\d+)?|[A-Za-z_]+").Cast<Match>().Select(m => m.Value.ToLowerInvariant());

    static string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();

    static Dictionary<string, List<string>> MatchBySource(string norm, Dictionary<string, HashSet<string>> sets)
    {
      var toks = Tokenize(norm).ToArray();
      var outMap = sets.Keys.ToDictionary(k => k, k => new List<string>());
      foreach (var t in toks) foreach (var kv in sets) if (kv.Value.Contains(t)) outMap[kv.Key].Add(t);
      return outMap;
    }

    static bool IsIgnorableToken(string t)
    {
      if (string.IsNullOrEmpty(t)) return true;
      if (IgnorableTokens.Contains(t)) return true;
      if (Regex.IsMatch(t, @"^\d+(?:\.\d+)?$")) return true; // numbers
      return false;
    }

    // ---- Convenience outputs ----
    static string BuildScenePlanSkeleton()
    {
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

    void OpenPasteAndMerge()
    {
      try
      {
        var spmw = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
          .FirstOrDefault(t => t.Name == "SpecPasteMergeWindow" || t.FullName?.EndsWith(".SpecPasteMergeWindow") == true);
        if (spmw != null)
        {
          var m = spmw.GetMethod("OpenWithJson",
            System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|
            System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance,
            null, new Type[]{ typeof(string) }, null);
          if (m != null)
          {
            object inst = m.IsStatic ? null : EditorWindow.GetWindow(spmw);
            m.Invoke(inst, new object[]{ _json ?? "" });
            ShowNotification(new GUIContent("Sent to Paste & Merge"));
            return;
          }
          EditorWindow.GetWindow(spmw).Show();
          if (!string.IsNullOrEmpty(_json)) EditorGUIUtility.systemCopyBuffer = _json;
          ShowNotification(new GUIContent("Opened (JSON copied)"));
          return;
        }

        bool opened =
          EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Paste & Merge") ||
          EditorApplication.ExecuteMenuItem("Window/Paste & Merge") ||
          EditorApplication.ExecuteMenuItem("Tools/Paste & Merge");
        if (!string.IsNullOrEmpty(_json)) EditorGUIUtility.systemCopyBuffer = _json;
        ShowNotification(new GUIContent(opened ? "Opened via menu (JSON copied)" : "Window not found; JSON copied"));
      }
      catch
      {
        if (!string.IsNullOrEmpty(_json)) EditorGUIUtility.systemCopyBuffer = _json;
        ShowNotification(new GUIContent("Error; JSON copied"));
      }
    }

    void CopySkeletonIntent()
    {
      var s = "{\n  \"intents\": [\n    {\n      \"name\": \"dimensions-by-m\",\n" +
              "      \"regex\": \"^\\\\s*(?:make|build|create)?\\\\s*(\\\\d+(?:\\\\.\\\\d+)?)\\\\s*m\\\\s*(?:x|by|\\\\u00d7)\\\\s*(\\\\d+(?:\\\\.\\\\d+)?)\\\\s*m\\\\s*$\",\n" +
              "      \"ops\": [\n" +
              "        {\"op\":\"set\",\"path\":\"$.track.length\",\"value\":\"$1:float\"},\n" +
              "        {\"op\":\"set\",\"path\":\"$.track.width\",\"value\":\"$2:float\"},\n" +
              "        {\"op\":\"set\",\"path\":\"$.track.tileSpacing\",\"value\":1}\n" +
              "      ]\n    }\n  ]\n}\n";
      EditorGUIUtility.systemCopyBuffer = s;
      ShowNotification(new GUIContent("Skeleton intent copied (Target: Intents)."));
    }

    // ----- Lexicon self-patch -----
    static string ResolveLexiconWritePath()
    {
      var pJson = Path.Combine(SpecDir, "lexicon.json");
      var pTxt  = Path.Combine(SpecDir, "lexicon.txt");
      var pRaw  = Path.Combine(SpecDir, "lexicon");
      if (File.Exists(pJson) || AssetDatabase.LoadAssetAtPath<TextAsset>(pJson) != null) return pJson;
      if (File.Exists(pTxt)  || AssetDatabase.LoadAssetAtPath<TextAsset>(pTxt)  != null) return pTxt;
      if (File.Exists(pRaw)  || AssetDatabase.LoadAssetAtPath<TextAsset>(pRaw)  != null) return pRaw;
      return pTxt;
    }

    static int ApplyLexiconPatch(string[] tokens)
    {
      if (tokens == null || tokens.Length == 0) return 0;
      var path = ResolveLexiconWritePath();
      Directory.CreateDirectory(Path.GetDirectoryName(path));

      var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (File.Exists(path))
        foreach (var line in File.ReadAllLines(path)) existing.Add(line.Trim());

      int added = 0;
      using (var sw = new StreamWriter(path, true))
      {
        foreach (var t in tokens.Distinct())
        {
          if (!Regex.IsMatch(t, @"^[A-Za-z0-9_]+$")) continue;
          var line = $"{t} => {t}";
          if (existing.Contains(line)) continue;
          sw.WriteLine(line);
          existing.Add(line);
          added++;
        }
      }
      return added;
    }

    // ----- Safe process runner -----
    static int RunScript(string scriptPath, string args, out string output, out string error)
    {
      output = string.Empty;
      error  = string.Empty;

      if (!File.Exists(scriptPath)) { error = "Script not found: " + scriptPath; return -1; }

      try
      {
        using (var p = new Process())
        {
          p.StartInfo = new ProcessStartInfo("/bin/zsh", $"\"{scriptPath}\" {args}")
          {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = ProjectRoot
          };

          p.Start();
          string o = p.StandardOutput.ReadToEnd();
          string e = p.StandardError.ReadToEnd();
          p.WaitForExit();
          int code = p.ExitCode;

          output = o;
          error  = e;
          return code;
        }
      }
      catch (Exception ex)
      {
        error = ex.GetType().Name + ": " + ex.Message;
        output = string.Empty;
        return -1;
      }
    }

    // ----- Self-heal -----
    int SelfHealLocal(string nl)
    {
      int added = 0;
      if (_lastUnmatched != null && _lastUnmatched.Count > 0)
        added = ApplyLexiconPatch(_lastUnmatched.ToArray());

      string o = "", e = "";
      if (File.Exists(LocalHealScript))
      {
        string tmp = Path.Combine(Path.GetTempPath(), "aigg_nl_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(tmp, nl ?? "");
        try { RunScript(LocalHealScript, $"--file \"{tmp}\"", out o, out e); }
        finally { try { File.Delete(tmp); } catch { } }
      }

      if (added > 0) _diag = $"Self-heal (local): added {added} token(s) to lexicon.\n" + _diag;
      if (!string.IsNullOrEmpty(o) || !string.IsNullOrEmpty(e))
        _diag = (o + (string.IsNullOrEmpty(e) ? "" : "\n" + e)).Trim() + "\n" + _diag;

      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
      return 0;
    }

    int SelfHealOpenAI(string nl)
    {
      string tmp = Path.Combine(Path.GetTempPath(), "aigg_nl_" + Guid.NewGuid().ToString("N") + ".txt");
      File.WriteAllText(tmp, nl ?? "");
      try
      {
        var model = EditorPrefs.GetString("AIGG.OpenAI.Model", "gpt-4o-mini");
        var ec = RunScript(OpenAIHealScript, $"--file \"{tmp}\" {model}", out var o, out var e);
        _diag = (o + (string.IsNullOrEmpty(e) ? "" : "\n" + e)).Trim() + "\n" + _diag;
        if (ec == 0)
        {
          AssetDatabase.Refresh();
          var aiDir = Path.Combine(SpecDir, "_AI");
          if (Directory.Exists(aiDir))
          {
            var patch = Directory.GetFiles(aiDir, "patch_*.json").OrderByDescending(f => f).FirstOrDefault();
            if (!string.IsNullOrEmpty(patch))
            {
              _json = File.ReadAllText(patch);
              OpenPasteAndMerge();
            }
          }
        }
        return ec;
      }
      finally { try { File.Delete(tmp); } catch { } }
    }
  }
}
#endif
CS

touch "$F"
echo "Replaced with strict-gated version. Switch to Unity to recompile, then click Parse NL."
