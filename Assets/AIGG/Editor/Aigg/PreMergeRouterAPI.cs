// ASCII only
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG {
  public static class PreMergeRouterAPI {
    // === Strict canonical entry ===
    public static void Route(string nl, string canonicalJson, string diagnosticsJson) {
      StrictRoute(nl ?? "", canonicalJson ?? "", diagnosticsJson ?? "");
    }

    // === Payload entry (NL + canonical + diagnostics in one JSON) ===
    public static void RoutePayload(string payloadJson) {
      if (string.IsNullOrWhiteSpace(payloadJson)) { Fail("No payload", null, null, null); return; }
      string nl = RX("\"nl\"\\s*:\\s*\"([^\"]*)\"", payloadJson);
      string dj = Obj("\"diagnostics\"\\s*:\\s*\\{", payloadJson);
      string cj = Obj("\"canonical\"\\s*:\\s*\\{", payloadJson);
      if (string.IsNullOrEmpty(cj)) { Fail("No canonical", nl, null, dj); return; }
      if (string.IsNullOrEmpty(dj)) { Fail("No diagnostics", nl, "{"+cj+"}", null); return; }
      StrictRoute(nl, "{"+cj+"}", "{"+dj+"}");
    }

    // === Compatibility overload for legacy callers ===
    // Accepts 0/1/2 strings and blocks (shows diagnostics window) if required parts are missing.
    public static void Route(params string[] args) {
      if (args == null || args.Length == 0) { Fail("Legacy Route() with no arguments.", "", null, null); return; }
      if (args.Length == 1) {
        // Could be canonical-only -> block (no diagnostics provided)
        var s0 = args[0] ?? "";
        if (LooksLikeJsonObject(s0)) StrictRoute("", s0, "");
        else Fail("Legacy Route(arg) not understood (need nl, canonical, diagnostics).", s0, null, null);
        return;
      }
      if (args.Length == 2) {
        // Try to guess ordering; still block if diagnostics is missing.
        var a = args[0] ?? "";
        var b = args[1] ?? "";
        bool bIsDiag = LooksLikeDiagnostics(b);
        bool aIsCanonical = LooksLikeJsonObject(a);
        bool bIsCanonical = LooksLikeJsonObject(b) && !bIsDiag;

        if (aIsCanonical && bIsDiag) { StrictRoute("", a, b); return; }   // canonical, diagnostics
        if (!aIsCanonical && bIsCanonical) { StrictRoute(a, b, ""); return; } // nl, canonical (no diagnostics -> block)
        // Unknown combination -> block with hint
        Fail("Legacy Route(a,b): supply nl, canonical, diagnostics.", a, bIsCanonical?b:null, bIsDiag?b:null);
        return;
      }
      // 3+ -> treat as (nl, canonical, diagnostics) and ignore extras
      StrictRoute(args[0] ?? "", args[1] ?? "", args[2] ?? "");
    }

    // === Core strict routing ===
    private static void StrictRoute(string nl, string canonicalJson, string diagnosticsJson) {
      var d = Diagnostics.Parse(diagnosticsJson);
      if (!d.HasValue) { Fail("Diagnostics parse failed (strict).", nl, canonicalJson, diagnosticsJson); return; }
      if (d.unmatched.Count > 0 || d.ok == false) { Fail("Unmatched tokens present (strict).", nl, canonicalJson, diagnosticsJson, d); return; }

      var missing = SpecAudit.FindMissingCommands(canonicalJson);
      if (missing.Count > 0) { Fail("Missing commands: " + string.Join(", ", missing), nl, canonicalJson, diagnosticsJson, d); return; }

      Forward(canonicalJson);
    }

    private static void Forward(string json) {
      var asm = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var a in asm) {
        var t = a.GetType("Aim2Pro.AIGG.SpecPasteMergeWindow"); if (t == null) continue;
        var m = t.GetMethod("OpenWithJson", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (m != null) { m.Invoke(null, new object[] { json }); return; }
      }
      EditorGUIUtility.systemCopyBuffer = json;
      EditorUtility.DisplayDialog("Pre-Merge (Fallback)", "Paste & Merge not found. Canonical JSON copied to clipboard.", "OK");
      Debug.Log("[PreMergeRouterAPI] Paste window not found. JSON copied to clipboard.");
    }

    private static void Fail(string reason, string nl, string cj, string dj, Diagnostics? d = null) {
      PreMergeDiagnosticsWindow.Show(reason ?? "Blocked", nl ?? "", cj ?? "", dj ?? "", d);
      Debug.LogWarning("[PreMerge STRICT BLOCK] " + reason);
    }

    // === Helpers ===
    private static string RX(string pat, string src) { var m = Regex.Match(src ?? "", pat); return m.Success ? m.Groups[1].Value : ""; }
    private static string Obj(string anchor, string src) {
      var a = Regex.Match(src ?? "", anchor); if (!a.Success) return "";
      int start = a.Index + a.Length - 1, depth = 0;
      for (int i = start; i < src.Length; i++) { char c = src[i];
        if (c == '{') depth++; else if (c == '}') { depth--; if (depth == 0) return src.Substring(start + 1, i - (start + 1)); } }
      return "";
    }
    private static bool LooksLikeDiagnostics(string s) {
      if (string.IsNullOrWhiteSpace(s)) return false;
      return Regex.IsMatch(s, "\"unmatched\"\\s*:\\s*\\[") || Regex.IsMatch(s, "\"ok\"\\s*:\\s*(true|false)");
    }
    private static bool LooksLikeJsonObject(string s) {
      if (string.IsNullOrWhiteSpace(s)) return false;
      s = s.TrimStart(); return s.Length > 0 && s[0] == '{';
    }

    // Minimal DTO for diagnostics parsing
    public struct Diagnostics {
      public System.Collections.Generic.List<string> unmatched;
      public bool ok;
      public static Diagnostics? Parse(string json) {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var d = new Diagnostics { unmatched = new System.Collections.Generic.List<string>(), ok = false };
        var um = Regex.Match(json, "\"unmatched\"\\s*:\\s*\\[(.*?)\\]");
        if (um.Success)
          foreach (Match m in Regex.Matches(um.Groups[1].Value, "\"([^\"]+)\"")) d.unmatched.Add(m.Groups[1].Value);
        var okm = Regex.Match(json, "\"ok\"\\s*:\\s*(true|false)");
        d.ok = okm.Success && okm.Groups[1].Value == "true";
        return d;
      }
    }
  }
}
