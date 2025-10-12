#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Aim2Pro.AIGG.Workbench
{
    internal static class LocalIntentEngine
    {
        [Serializable] private class SpecOp { public string op; public string path; public string value; }
        [Serializable] private class SpecIntent { public string name; public string regex; public List<SpecOp> ops; }
        [Serializable] private class SpecRoot { public List<SpecIntent> intents; }

        public static bool TryParse(string nl, string specDir, out string json)
        {
            json = null;
            var intents = LoadIntents(specDir);
            if (intents == null || intents.Count == 0) return false;

            var text = nl ?? "";
            var rxOpts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;

            foreach (var it in intents)
            {
                if (it == null || string.IsNullOrEmpty(it.regex) || it.ops == null || it.ops.Count == 0) continue;
                Match m;
                try { m = Regex.Match(text, it.regex, rxOpts); } catch { continue; }
                if (!m.Success) continue;

                var root = new Dictionary<string, object>();
                foreach (var op in it.ops)
                {
                    if (op == null) continue;
                    if (!string.Equals(op.op ?? "set", "set", StringComparison.OrdinalIgnoreCase)) continue;
                    var expanded = ExpandTemplate(op.value ?? "", m);
                    var val = CoerceScalar(expanded);
                    SetAtPath(root, op.path ?? "$.value", val);
                }
                json = Serialize(root);
                return true;
            }
            return false;
        }

        // ----- Intents loading -----
        private static List<SpecIntent> LoadIntents(string specDir)
        {
            try
            {
                var p = Path.Combine(specDir ?? "Assets/AIGG/Spec", "intents.json");
                if (!File.Exists(p)) return null;
                var txt = File.ReadAllText(p);
                // Expect {"intents":[...]} (our merge script writes this)
                var root = JsonUtility.FromJson<SpecRoot>(txt);
                return root != null ? (root.intents ?? new List<SpecIntent>()) : null;
            }
            catch { return null; }
        }

        // ----- Ops helpers -----
        private static string ExpandTemplate(string tpl, Match mm)
        {
            if (tpl == null) return "";
            return Regex.Replace(tpl, "\\$(\\d+)(?::(int|float))?", m => {
                var idxStr = m.Groups[1].Value;
                var type = m.Groups[2].Success ? m.Groups[2].Value.ToLowerInvariant() : null;
                int idx = 0; int.TryParse(idxStr, out idx);
                var src = (idx >= 0 && idx < mm.Groups.Count) ? mm.Groups[idx].Value : "";
                if (string.IsNullOrEmpty(type)) return src;
                if (type == "int")
                {
                    if (double.TryParse(src, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                        return ((long)Math.Round(dv)).ToString(CultureInfo.InvariantCulture);
                    return "0";
                }
                if (type == "float")
                {
                    if (double.TryParse(src, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                        return dv.ToString(CultureInfo.InvariantCulture);
                    return "0";
                }
                return src;
            });
        }

        private static object CoerceScalar(string raw)
        {
            if (raw == null) return "";
            var s = raw.Trim();
            if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                if (s.IndexOf('.') >= 0 || s.IndexOf('e') >= 0 || s.IndexOf('E') >= 0) return d;
                if (long.TryParse(s, out var l)) return l;
                return d;
            }
            return s;
        }

        private static void SetAtPath(Dictionary<string, object> root, string path, object value)
        {
            if (string.IsNullOrEmpty(path)) return;
            var p = path.Trim();
            if (p.StartsWith("$.")) p = p.Substring(2);
            else if (p.StartsWith("$")) p = p.Substring(1);
            var parts = p.Split('.');
            var cur = root;
            for (int i = 0; i < parts.Length; i++)
            {
                var k = parts[i];
                if (i == parts.Length - 1) { cur[k] = value; return; }
                if (!cur.TryGetValue(k, out var next) || !(next is Dictionary<string, object>))
                {
                    var nd = new Dictionary<string, object>();
                    cur[k] = nd; cur = nd;
                }
                else cur = (Dictionary<string, object>)next;
            }
        }

        // ----- Minimal JSON serializer (dict/list/primitive) -----
        private static string Serialize(object o)
        {
            if (o == null) return "null";
            if (o is string s) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            if (o is bool b) return b ? "true" : "false";
            if (o is double d) return d.ToString(CultureInfo.InvariantCulture);
            if (o is float f) return f.ToString(CultureInfo.InvariantCulture);
            if (o is long l) return l.ToString(CultureInfo.InvariantCulture);
            if (o is int i) return i.ToString(CultureInfo.InvariantCulture);

            if (o is Dictionary<string, object> dict)
            {
                var first = true;
                var sb = new System.Text.StringBuilder();
                sb.Append('{');
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(kv.Key.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                    sb.Append(':').Append(Serialize(kv.Value));
                }
                sb.Append('}');
                return sb.ToString();
            }

            if (o is System.Collections.IEnumerable en)
            {
                var first = true;
                var sb = new System.Text.StringBuilder();
                sb.Append('[');
                foreach (var it in en)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(Serialize(it));
                }
                sb.Append(']');
                return sb.ToString();
            }

            return "\"" + o.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
#endif
