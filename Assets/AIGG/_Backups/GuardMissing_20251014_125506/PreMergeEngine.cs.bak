#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Aim2Pro.AIGG; // MiniJSON

namespace Aim2Pro.AIGG.PreMerge
{
  public class PreMergeResult {
    public string original = "";
    public string normalized = "";
    public List<string> issues = new List<string>();
    public Dictionary<string, object> canonical = new Dictionary<string, object>();
    public List<string> missing = new List<string>();
    public bool locallyFixed = false;
    public string canonicalJson = "{}";
  }

  static class SpecFS {
    public static string Root = "Assets/AIGG/Spec";
    static Dictionary<string, object> Load(string name) {
      var p = Path.Combine(Root, name);
      if (!File.Exists(p)) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
      try {
        object o;
        if (MiniJSON.TryDeserialize(File.ReadAllText(p, Encoding.UTF8), out o))
          return o as Dictionary<string, object> ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
      } catch {}
      return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
    public static Dictionary<string, object> Lexicon()    => Load("lexicon.json");
    public static Dictionary<string, object> Fieldmap()   => Load("fieldmap.json");
    public static Dictionary<string, object> Validators() => Load("validators.json");
    public static Dictionary<string, object> Patterns()   => Load("patterns.json");
  }

  public class PreMergeEngine {
    readonly Dictionary<string,string> _synonyms;
    readonly List<RegexPattern> _patterns;
    readonly List<string> _required;

    struct RegexPattern { public string name; public Regex re; public List<Op> ops; }
    struct Op { public string op; public string path; public string value; }

    public PreMergeEngine() {
      _synonyms = ReadSynonyms();
      _patterns = ReadPatterns();
      _required = ReadRequired();
    }

    public PreMergeResult Process(string nl) {
      var r = new PreMergeResult { original = nl ?? "" };
      var s = r.original;

      // 0) Basic normalize (spaces, commas, x→by)
      s = BasicNormalize(s);
      // 1) Spelling/typo corrections (expand as needed)
      s = FixTypos(s);
      // 2) Synonyms
      s = ApplySynonyms(s);
      // 3) Units
      s = NormalizeUnits(s);
      r.normalized = s;

      // 4) Extract → canonical
      var canonical = NewTrack();
      ApplyPatterns(s, canonical);

      // 5) Validate + simple fills
      r.canonical = canonical;
      var miss = FindMissing(canonical);
      if (miss.Count > 0) { TryLocalDefaults(canonical, miss, r.issues); miss = FindMissing(canonical); }
      r.locallyFixed = miss.Count == 0;
      r.missing = miss;

      r.canonicalJson = MiniJSON.Serialize(canonical, pretty:true);
      return r;
    }

    // ---------- Normalization ----------
    string BasicNormalize(string s) {
      if (string.IsNullOrEmpty(s)) return "";
      s = s.Trim();
      s = Regex.Replace(s, "\\s+", " ");
      s = Regex.Replace(s, "\\s*[,;]+\\s*", ", ");
      s = Regex.Replace(s, "(\\d)\\s*[xX]\\s*(\\d)", "$1 by $2");
      return s;
    }

    static readonly Dictionary<string,string> CommonTypos = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
      { "lenght", "length" }, { "widht", "width" },
      { "metre", "m" }, { "metres", "m" }, { "meter", "m" }, { "meters", "m" },
      { "bye", "by" }
    };
    string FixTypos(string s) {
      return Regex.Replace(s, "[A-Za-z]+", m => CommonTypos.TryGetValue(m.Value, out var rep) ? rep : m.Value);
    }

    string ApplySynonyms(string s) {
      foreach (var kv in _synonyms) {
        var pat = "\\b" + Regex.Escape(kv.Key) + "\\b";
        s = Regex.Replace(s, pat, kv.Value, RegexOptions.IgnoreCase);
      }
      return s;
    }
    string NormalizeUnits(string s) { return Regex.Replace(s, "\\b(\\d+)\\s*m\\b", "$1m", RegexOptions.IgnoreCase); }

    // ---------- Patterns / extraction ----------
    void ApplyPatterns(string s, Dictionary<string, object> canon) {
      var hit = false;
      foreach (var pat in _patterns) {
        var m = pat.re.Match(s);
        if (!m.Success) continue;
        hit = true;
        foreach (var op in pat.ops) {
          if (op.op == "set") {
            var val = ResolveValue(op.value, m);
            SetAtPath(canon, op.path, val);
          }
        }
      }
      if (!hit) {
        var m = Regex.Match(s, "\\b(\\d+)\\s*m\\b\\s*by\\s*\\b(\\d+)\\s*m\\b", RegexOptions.IgnoreCase);
        if (m.Success) {
          SetAtPath(canon, "$.track.length", int.Parse(m.Groups[1].Value));
          SetAtPath(canon, "$.track.width",  int.Parse(m.Groups[2].Value));
        }
      }
    }

    object ResolveValue(string v, Match m) {
      if (v.StartsWith("$")) {
        var parts = v.Split(':'); var cap = parts[0]; var type = parts.Length>1 ? parts[1] : "str";
        if (cap.Length>1 && int.TryParse(cap.Substring(1), out var idx)) {
          var raw = m.Groups[idx].Value;
          if (type=="int" && int.TryParse(raw, out var i)) return i;
          if (type=="float" && float.TryParse(raw, out var f)) return f;
          return raw;
        }
      }
      return v;
    }

    // ---------- Validators / defaults ----------
    List<string> FindMissing(Dictionary<string, object> canon) {
      var outp = new List<string>();
      foreach (var p in _required) if (!HasPath(canon, p)) outp.Add(p);
      return outp;
    }
    void TryLocalDefaults(Dictionary<string, object> canon, List<string> missing, List<string> issues) {
      bool hasL = HasPath(canon, "$.track.length");
      bool hasW = HasPath(canon, "$.track.width");
      if (hasL && !hasW) { SetAtPath(canon, "$.track.width", 3); issues.Add("Filled default width=3."); }
    }

    // ---------- Spec readers ----------
    Dictionary<string,string> ReadSynonyms() {
      var lex = SpecFS.Lexicon();
      if (lex.TryGetValue("synonyms", out var o) && o is Dictionary<string,object> d) {
        var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in d) map[kv.Key] = kv.Value?.ToString() ?? "";
        return map;
      }
      return new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    }
    List<string> ReadRequired() {
      var req = new List<string>();
      var v = SpecFS.Validators();
      if (v.TryGetValue("required", out var o) && o is List<object> arr)
        foreach (var a in arr) { var s = a?.ToString(); if (!string.IsNullOrEmpty(s)) req.Add(s); }
      if (req.Count==0) { req.Add("$.track.length"); req.Add("$.track.width"); }
      return req;
    }
    List<RegexPattern> ReadPatterns() {
      var list = new List<RegexPattern>();
      var p = SpecFS.Patterns();
      if (p.TryGetValue("patterns", out var o) && o is List<object> arr) {
        foreach (var it in arr) {
          var d = it as Dictionary<string,object>; if (d==null) continue;
          var name = d.ContainsKey("name") ? (d["name"]?.ToString() ?? "") : "";
          var regex = d.ContainsKey("regex") ? (d["regex"]?.ToString() ?? "") : "";
          var ops = new List<Op>();
          if (d.ContainsKey("ops") && d["ops"] is List<object> opsArr) {
            foreach (var oo in opsArr) {
              var od = oo as Dictionary<string,object>; if (od==null) continue;
              ops.Add(new Op{
                op = od.ContainsKey("op") ? (od["op"]?.ToString() ?? "set") : "set",
                path = od.ContainsKey("path") ? (od["path"]?.ToString() ?? "") : "",
                value = od.ContainsKey("value") ? (od["value"]?.ToString() ?? "") : ""
              });
            }
          }
          if (!string.IsNullOrEmpty(regex))
            list.Add(new RegexPattern{ name=name, re=new Regex(regex, RegexOptions.IgnoreCase), ops=ops });
        }
      }
      if (list.Count==0) {
        list.Add(new RegexPattern{
          name="length by width (m)",
          re=new Regex("\\b(\\d+)\\s*m\\b\\s*by\\s*\\b(\\d+)\\s*m\\b", RegexOptions.IgnoreCase),
          ops=new List<Op>{
            new Op{ op="set", path="$.track.length", value="$1:int" },
            new Op{ op="set", path="$.track.width",  value="$2:int" }
          }
        });
      }
      return list;
    }

    // ---------- Canon helpers ----------
    Dictionary<string, object> NewTrack() {
      var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
      var t = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
      t["length"]=null; t["width"]=null; t["tileSpacing"]=1.0; t["killzoneY"]=-5.0; t["obstacles"]=new List<object>();
      d["track"]=t; return d;
    }
    void SetAtPath(Dictionary<string, object> root, string path, object value) {
      if (string.IsNullOrEmpty(path)) return;
      if (!path.StartsWith("$."))
        path = "$." + path;
      var p = path.Substring(2);
      var parts = p.Split('.');
      Dictionary<string, object> cur = root;
      for (int i=0;i<parts.Length;i++) {
        var seg = parts[i];
        int br = seg.IndexOf('[');
        if (br >= 0) {
          var key = seg.Substring(0, br);
          int idx=0; int.TryParse(seg.Substring(br+1).TrimEnd(']'), out idx);
          if (!cur.TryGetValue(key, out var arrObj) || !(arrObj is List<object> arr)) { arr = new List<object>(); cur[key]=arr; }
          while (arr.Count <= idx) arr.Add(null);
          if (i == parts.Length-1) arr[idx]=value;
          else { var next = arr[idx] as Dictionary<string,object>; if (next==null){ next=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase); arr[idx]=next; } cur=next; }
        } else {
          if (i == parts.Length-1) cur[seg]=value;
          else {
            if (!cur.TryGetValue(seg, out var o) || !(o is Dictionary<string,object> d)) { d=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase); cur[seg]=d; }
            cur = (Dictionary<string,object>)cur[seg];
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
        int br = seg.IndexOf('[');
        if (br >= 0) {
          var key = seg.Substring(0, br);
          int idx=0; int.TryParse(seg.Substring(br+1).TrimEnd(']'), out idx);
          if (!(cur is Dictionary<string,object> d) || !d.TryGetValue(key, out var arrObj) || !(arrObj is List<object> arr)) return false;
          if (idx<0 || idx>=arr.Count) return false;
          cur = arr[idx];
        } else {
          if (!(cur is Dictionary<string,object> d) || !d.TryGetValue(seg, out var nxt)) return false;
          cur = nxt;
        }
      }
      return cur != null;
    }
  }
}
#endif
