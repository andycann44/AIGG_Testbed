#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Aim2Pro.AIGG.PreMerge
{
  // Result object we pass to the UI
  public class PreMergeResult {
    public string original = "";
    public string normalized = "";
    public List<string> issues = new List<string>();
    public Dictionary<string, object> canonical = new Dictionary<string, object>();
    public List<string> missing = new List<string>();
    public bool locallyFixed = false;
    public string canonicalJson = "{}";
  }

  // Lightweight Spec loader from Assets/AIGG/Spec
  static class Spec {
    public static string Root = "Assets/AIGG/Spec";
    static Dictionary<string, object> Load(string name) {
      var p = Path.Combine(Root, name);
      if (!File.Exists(p)) return new Dictionary<string, object>();
      try {
        object o;
        if (MiniJSON.TryDeserialize(File.ReadAllText(p, Encoding.UTF8), out o))
          return o as Dictionary<string, object> ?? new Dictionary<string, object>();
      } catch {}
      return new Dictionary<string, object>();
    }
    public static Dictionary<string, object> Lexicon()    => Load("lexicon.json");
    public static Dictionary<string, object> Fieldmap()   => Load("fieldmap.json");
    public static Dictionary<string, object> Intents()    => Load("intents.json");
    public static Dictionary<string, object> Validators() => Load("validators.json");
    public static Dictionary<string, object> Patterns()   => Load("patterns.json");
  }

  public class PreMergeEngine {
    readonly Dictionary<string,string> _synonyms;
    readonly Dictionary<string,string> _fieldmap;
    readonly List<RegexPattern> _patterns;
    readonly List<string> _required;

    struct RegexPattern {
      public string name;
      public Regex re;
      public List<Op> ops;
    }
    struct Op {
      public string op;    // "set"
      public string path;  // "$.track.length"
      public string value; // "$1:int" or literal
    }

    public PreMergeEngine() {
      _synonyms = ReadSynonyms();
      _fieldmap = ReadFieldmap();
      _patterns = ReadPatterns();
      _required = ReadRequired();
    }

    // ---- Public API
    public PreMergeResult Process(string nl) {
      var r = new PreMergeResult { original = nl ?? "" };
      var s = r.original;

      // 1) Normalize
      s = BasicNormalize(s);
      s = ApplySynonyms(s);
      s = NormalizeUnits(s);
      r.normalized = s;

      // 2) Extract using patterns (from patterns.json if present; else built-ins)
      var canonical = NewTrack();
      ApplyPatterns(s, canonical);

      // 3) Validate (local)
      r.canonical = canonical;
      var miss = FindMissing(canonical);
      r.missing.AddRange(miss);

      // 4) Try local fills if trivial (e.g., width defaults)
      if (miss.Count > 0) {
        TryLocalDefaults(canonical, miss, r.issues);
        miss = FindMissing(canonical);
      }

      r.locallyFixed = miss.Count == 0;
      r.missing = miss;

      r.canonicalJson = MiniJSON.Serialize(canonical, pretty:true);
      return r;
    }

    // ---- Normalization helpers
    string BasicNormalize(string s) {
      if (string.IsNullOrEmpty(s)) return "";
      s = s.Trim();
      // collapse whitespace
      s = Regex.Replace(s, "\\s+", " ");
      // punctuation spacing
      s = Regex.Replace(s, "\\s*[,;]+\\s*", ", ");
      // unify separators: "x" between numbers into " by "
      s = Regex.Replace(s, "(\\d)\\s*[xX]\\s*(\\d)", "$1 by $2");
      // typos
      s = Regex.Replace(s, "\\blenght\\b", "length", RegexOptions.IgnoreCase);
      s = Regex.Replace(s, "\\bmetres?\\b", "m", RegexOptions.IgnoreCase);
      s = Regex.Replace(s, "\\bmeters?\\b", "m", RegexOptions.IgnoreCase);
      return s;
    }

    string ApplySynonyms(string s) {
      if (_synonyms == null || _synonyms.Count == 0) return s;
      foreach (var kv in _synonyms) {
        var pat = "\\b" + Regex.Escape(kv.Key) + "\\b";
        s = Regex.Replace(s, pat, kv.Value, RegexOptions.IgnoreCase);
      }
      return s;
    }

    string NormalizeUnits(string s) {
      // convert "40 m", "40m", "40 metres" -> "40m"
      s = Regex.Replace(s, "\\b(\\d+)\\s*m\\b", "$1m", RegexOptions.IgnoreCase);
      return s;
    }

    // ---- Pattern extraction
    void ApplyPatterns(string s, Dictionary<string, object> canon) {
      var any = false;
      foreach (var pat in _patterns) {
        var m = pat.re.Match(s);
        if (!m.Success) continue;
        any = true;
        foreach (var op in pat.ops) {
          if (op.op == "set") {
            var val = ResolveValue(op.value, m);
            SetAtPath(canon, op.path, val);
          }
        }
      }
      // very light built-in if no patterns matched: "(\d+)m by (\d+)m"
      if (!any) {
        var m = Regex.Match(s, "\\b(\\d+)\\s*m\\b\\s*by\\s*\\b(\\d+)\\s*m\\b", RegexOptions.IgnoreCase);
        if (m.Success) {
          SetAtPath(canon, "$.track.length", int.Parse(m.Groups[1].Value));
          SetAtPath(canon, "$.track.width", int.Parse(m.Groups[2].Value));
        }
      }
    }

    object ResolveValue(string v, Match m) {
      if (v.StartsWith("$")) {
        // $1:int or $2
        var idx = 0; var type = "str";
        var parts = v.Split(':');
        var cap = parts[0]; // "$1"
        if (cap.Length > 1 && int.TryParse(cap.Substring(1), out idx)) {
          var raw = m.Groups[idx].Value;
          if (parts.Length > 1) type = parts[1];
          if (type == "int") { if (int.TryParse(raw, out var i)) return i; }
          if (type == "float") { if (float.TryParse(raw, out var f)) return f; }
          return raw;
        }
      }
      return v;
    }

    // ---- Validators & defaults
    List<string> FindMissing(Dictionary<string, object> canon) {
      var missing = new List<string>();
      foreach (var path in _required) {
        if (!HasPath(canon, path))
          missing.Add(path);
      }
      return missing;
    }

    void TryLocalDefaults(Dictionary<string, object> canon, List<string> missing, List<string> issues) {
      // simple example default: width defaults to 3 if only length provided
      bool hasLength = HasPath(canon, "$.track.length");
      bool hasWidth  = HasPath(canon, "$.track.width");
      if (hasLength && !hasWidth) {
        SetAtPath(canon, "$.track.width", 3);
        issues.Add("Filled default width=3.");
      }
    }

    // ---- Spec readers
    Dictionary<string,string> ReadSynonyms() {
      var lex = Spec.Lexicon();
      if (lex.TryGetValue("synonyms", out var s) && s is Dictionary<string, object> d) {
        var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in d) map[kv.Key] = kv.Value?.ToString() ?? "";
        return map;
      }
      return new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    }

    Dictionary<string,string> ReadFieldmap() {
      var fm = Spec.Fieldmap();
      if (fm.TryGetValue("englishToPath", out var s) && s is Dictionary<string, object> d) {
        var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in d) map[kv.Key] = kv.Value?.ToString() ?? "";
        return map;
      }
      return new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    }

    List<string> ReadRequired() {
      var req = new List<string>();
      var val = Spec.Validators();
      if (val.TryGetValue("required", out var r) && r is List<object> arr) {
        foreach (var o in arr) { var s = o?.ToString(); if (!string.IsNullOrEmpty(s)) req.Add(s); }
      } else {
        // fallback
        req.Add("$.track.length");
        req.Add("$.track.width");
      }
      return req;
    }

    List<RegexPattern> ReadPatterns() {
      var list = new List<RegexPattern>();
      var pat = Spec.Patterns();
      if (pat.TryGetValue("patterns", out var pObj) && pObj is List<object> arr) {
        foreach (var o in arr) {
          var d = o as Dictionary<string, object>;
          if (d == null) continue;
          var name = d.ContainsKey("name") ? (d["name"]?.ToString() ?? "") : "";
          var regex = d.ContainsKey("regex") ? (d["regex"]?.ToString() ?? "") : "";
          var ops = new List<Op>();
          if (d.ContainsKey("ops") && d["ops"] is List<object> opsArr) {
            foreach (var oo in opsArr) {
              var od = oo as Dictionary<string, object>;
              if (od == null) continue;
              var op = new Op {
                op = od.ContainsKey("op") ? (od["op"]?.ToString() ?? "set") : "set",
                path = od.ContainsKey("path") ? (od["path"]?.ToString() ?? "") : "",
                value = od.ContainsKey("value") ? (od["value"]?.ToString() ?? "") : ""
              };
              ops.Add(op);
            }
          }
          if (!string.IsNullOrEmpty(regex)) {
            var rp = new RegexPattern { name = name, re = new Regex(regex, RegexOptions.IgnoreCase), ops = ops };
            list.Add(rp);
          }
        }
      }
      // fallback example if none provided
      if (list.Count == 0) {
        list.Add(new RegexPattern {
          name = "length by width (m)",
          re = new Regex("\\b(\\d+)\\s*m\\b\\s*by\\s*\\b(\\d+)\\s*m\\b", RegexOptions.IgnoreCase),
          ops = new List<Op> {
            new Op{ op="set", path="$.track.length", value="$1:int" },
            new Op{ op="set", path="$.track.width",  value="$2:int" }
          }
        });
      }
      return list;
    }

    // ---- Canon helpers
    Dictionary<string, object> NewTrack() {
      var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
      var t = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
      t["length"] = null; t["width"] = null;
      t["tileSpacing"] = 1.0; t["killzoneY"] = -5.0;
      t["obstacles"] = new List<object>();
      d["track"] = t;
      return d;
    }

    // very small JSONPath-ish setter supporting "$.a.b" and "$.a.b[0]"
    void SetAtPath(Dictionary<string, object> root, string path, object value) {
      if (string.IsNullOrEmpty(path)) return;
      if (!path.StartsWith("$."))
        path = "$." + path;
      var p = path.Substring(2);
      var parts = p.Split('.');
      Dictionary<string, object> cur = root;
      for (int i=0;i<parts.Length;i++) {
        var seg = parts[i];
        int bracket = seg.IndexOf('[');
        if (bracket >= 0) {
          var key = seg.Substring(0, bracket);
          int idx = 0;
          int.TryParse(seg.Substring(bracket+1).TrimEnd(']'), out idx);
          if (!cur.TryGetValue(key, out var arrObj) || !(arrObj is List<object> arr)) {
            arr = new List<object>();
            cur[key] = arr;
          }
          while (arr.Count <= idx) arr.Add(null);
          if (i == parts.Length - 1) {
            arr[idx] = value;
          } else {
            var next = arr[idx] as Dictionary<string, object>;
            if (next == null) { next = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); arr[idx] = next; }
            cur = next;
          }
        } else {
          if (i == parts.Length - 1) {
            cur[seg] = value;
          } else {
            if (!cur.TryGetValue(seg, out var o) || !(o is Dictionary<string, object> d)) {
              d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
              cur[seg] = d;
            }
            cur = d;
          }
        }
      }
    }

    bool HasPath(Dictionary<string, object> root, string path) {
      if (string.IsNullOrEmpty(path)) return false;
      if (!path.StartsWith("$."))
        path = "$." + path;
      var p = path.Substring(2);
      var parts = p.Split('.');
      object cur = root;
      foreach (var seg in parts) {
        int bracket = seg.IndexOf('[');
        if (bracket >= 0) {
          var key = seg.Substring(0, bracket);
          int idx = 0;
          int.TryParse(seg.Substring(bracket+1).TrimEnd(']'), out idx);
          if (!(cur is Dictionary<string, object> d) || !d.TryGetValue(key, out var arrObj) || !(arrObj is List<object> arr)) return false;
          if (idx < 0 || idx >= arr.Count) return false;
          cur = arr[idx];
        } else {
          if (!(cur is Dictionary<string, object> d) || !d.TryGetValue(seg, out var nxt)) return false;
          cur = nxt;
        }
      }
      return cur != null;
    }
  }
}
#endif
