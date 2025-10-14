// ASCII only
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG {
  public static class PreMergeRouterAPI {
    public static void Route(string nl, string canonicalJson, string diagnosticsJson) {
      StrictRoute(nl ?? "", canonicalJson ?? "", diagnosticsJson ?? "");
    }
    public static void RoutePayload(string payloadJson) {
      if (string.IsNullOrWhiteSpace(payloadJson)) { Fail("No payload", null,null,null); return; }
      string nl = ExtractString(payloadJson, "\"nl\"\\s*:\\s*\"([^\"]*)\"");
      string diagnosticsJson = ExtractObject(payloadJson, "\"diagnostics\"\\s*:\\s*\\{", '}');
      string canonicalJson = ExtractObject(payloadJson, "\"canonical\"\\s*:\\s*\\{", '}');
      if (string.IsNullOrEmpty(canonicalJson)) { Fail("No canonical", nl,null,diagnosticsJson); return; }
      if (string.IsNullOrEmpty(diagnosticsJson)) { Fail("No diagnostics", nl,"{"+canonicalJson+"}",null); return; }
      StrictRoute(nl, "{"+canonicalJson+"}", "{"+diagnosticsJson+"}");
    }
    private static void StrictRoute(string nl, string canonicalJson, string diagnosticsJson) {
      var diag = Diagnostics.Parse(diagnosticsJson);
      if (!diag.HasValue) { Fail("Diagnostics parse failed", nl, canonicalJson, diagnosticsJson); return; }
      if (diag.unmatched.Count > 0 || diag.ok == false) { Fail("Unmatched tokens present", nl, canonicalJson, diagnosticsJson, diag); return; }
      var missingCmds = SpecAudit.FindMissingCommands(canonicalJson);
      if (missingCmds.Count > 0) { Fail("Missing commands: " + string.Join(", ", missingCmds), nl, canonicalJson, diagnosticsJson, diag); return; }
      ForwardToPasteMerge(canonicalJson);
    }
    private static void ForwardToPasteMerge(string json) {
      var asm = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var a in asm) {
        var t = a.GetType("Aim2Pro.AIGG.SpecPasteMergeWindow");
        if (t == null) continue;
        var m = t.GetMethod("OpenWithJson", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
        if (m != null) { m.Invoke(null, new object[]{ json }); return; }
      }
      EditorGUIUtility.systemCopyBuffer = json;
      EditorUtility.DisplayDialog("Pre-Merge (Fallback)", "Paste & Merge not found. JSON copied to clipboard.", "OK");
      Debug.Log("[PreMergeRouterAPI] Paste window not found. JSON copied to clipboard.");
    }
    private static void Fail(string reason, string nl, string canonicalJson, string diagnosticsJson, Diagnostics? d = null) {
      PreMergeDiagnosticsWindow.Show(reason ?? "Blocked", nl ?? "", canonicalJson ?? "", diagnosticsJson ?? "", d);
      Debug.LogWarning("[PreMerge STRICT BLOCK] " + reason);
    }
    private static string ExtractString(string src, string pattern) {
      var m = Regex.Match(src, pattern); return m.Success ? m.Groups[1].Value : "";
    }
    private static string ExtractObject(string src, string anchorPattern, char endChar) {
      var anchor = Regex.Match(src, anchorPattern); if (!anchor.Success) return "";
      int start = anchor.Index + anchor.Length - 1; int depth = 0;
      for (int i = start; i < src.Length; i++) {
        if (src[i] == '{') depth++; else if (src[i] == '}') { depth--; if (depth == 0) return src.Substring(start+1, i - (start+1)); }
      }
      return "";
    }
    public struct Diagnostics {
      public List<string> unmatched; public bool ok;
      public static Diagnostics? Parse(string json) {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var d = new Diagnostics{ unmatched = new List<string>(), ok = false };
        var um = Regex.Match(json, "\"unmatched\"\\s*:\\s*\\[(.*?)\\]");
        if (um.Success) foreach (Match m in Regex.Matches(um.Groups[1].Value, "\"([^\"]+)\"")) d.unmatched.Add(m.Groups[1].Value);
        var okm = Regex.Match(json, "\"ok\"\\s*:\\s*(true|false)"); d.ok = okm.Success && okm.Groups[1].Value == "true";
        return d;
      }
    }
  }
}
