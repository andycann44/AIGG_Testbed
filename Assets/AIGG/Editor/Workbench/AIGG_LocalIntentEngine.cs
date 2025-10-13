#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Aim2Pro.AIGG.Workbench
{
    internal static class LocalIntentEngine
    {
        private static readonly string[] SpecFiles = {
            "intents.json","lexicon.json","macros.json","commands.json","fieldmap.json","registry.json","schema.json"
        };

        public static string[] GetRequiredSpecFiles() => (string[])SpecFiles.Clone();

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

        public static bool TryReadSpec(string specDir, out string[] present, out string[] missing)
        {
            present = Array.Empty<string>(); missing = Array.Empty<string>();
            if (string.IsNullOrEmpty(specDir) || !Directory.Exists(specDir)) { missing = SpecFiles; return false; }
            var existing = SpecFiles.Where(f => File.Exists(Path.Combine(specDir, f))).ToArray();
            var missingFiles = SpecFiles.Except(existing).ToArray();
            present = existing; missing = missingFiles;
            return missingFiles.Length == 0;
        }

        public static string Normalize(string nl)
        {
            if (string.IsNullOrWhiteSpace(nl)) return string.Empty;
            return Regex.Replace(nl.Trim().ToLowerInvariant(), @"\s+", " ");
        }

        public static bool LooksLikeScenePlan(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            var lower = json.ToLowerInvariant();
            return lower.Contains("\"sceneplan\"") || lower.Contains("\"scenes\"") || lower.Contains("\"nodes\"");
        }

        /// <summary>
        /// Minimal NL heuristic to produce canonical track JSON without relying on intents.
        /// </summary>
        public static bool TryConvertNLToCanonical(string nl, out string canonicalJson, out string[] unmatched, out string[] notes)
        {
            canonicalJson = string.Empty;
            unmatched = Array.Empty<string>();
            notes = Array.Empty<string>();

            var normalized = Normalize(nl);
            if (string.IsNullOrEmpty(normalized)) { unmatched = new[] { "empty_nl" }; return false; }

            int length = 0, width = 0, curveRows = 0;
            double missing = 0.0, gaps = 0.0;
            const double tileSpacing = 1.0;
            string curveSide = null;

            var matchLengthWidth = Regex.Match(normalized, @"\b(\d+)\s*m\s*(?:x|by)\s*(\d+)\s*m\b");
            if (matchLengthWidth.Success) {
                length = ParseInt(matchLengthWidth.Groups[1].Value);
                width = ParseInt(matchLengthWidth.Groups[2].Value);
            } else {
                var lengthMatch = Regex.Match(normalized, @"\b(\d+)\s*m\b");
                if (lengthMatch.Success) length = ParseInt(lengthMatch.Groups[1].Value);
                var widthMatch = Regex.Match(normalized, @"\bwidth\s*(\d+)\s*m\b");
                if (widthMatch.Success) width = ParseInt(widthMatch.Groups[1].Value);
            }

            var missingMatch = Regex.Match(normalized, @"\b(\d+)\s*%\s*(?:tiles?\s*missing|missing\s*tiles?)\b");
            if (missingMatch.Success) missing = ParseInt(missingMatch.Groups[1].Value) / 100.0;

            var gapMatch = Regex.Match(normalized, @"\b(\d+)\s*%\s*gaps?\b");
            if (gapMatch.Success) gaps = ParseInt(gapMatch.Groups[1].Value) / 100.0;

            var curveMatch = Regex.Match(normalized, @"\b(left|right)\s+curve\s+over\s+(\d+)\s+rows\b");
            if (curveMatch.Success) {
                curveSide = curveMatch.Groups[1].Value;
                curveRows = ParseInt(curveMatch.Groups[2].Value);
            }

            var missingFields = new List<string>();
            var noteList = new List<string>();
            if (length <= 0) missingFields.Add("length");
            if (width <= 0) { width = 3; noteList.Add("defaulted width=3"); }

            if (missingFields.Count > 0) {
                unmatched = missingFields.ToArray();
                notes = noteList.ToArray();
                return false;
            }

            var ci = CultureInfo.InvariantCulture;
            var builder = new StringBuilder();
            builder.Append("{\"track\":{");
            builder.AppendFormat(ci, "\"length\":{0},\"width\":{1},\"tileSpacing\":{2}", length, width, tileSpacing);
            if (missing > 0) builder.AppendFormat(ci, ",\"missingTileChance\":{0}", Clamp01(missing));
            if (gaps > 0) builder.AppendFormat(ci, ",\"gapChance\":{0}", Clamp01(gaps));
            if (!string.IsNullOrEmpty(curveSide) && curveRows > 0)
                builder.AppendFormat(ci, ",\"curve\":{{\"side\":\"{0}\",\"rows\":{1}}}", curveSide, curveRows);
            builder.Append(",\"killzoneY\":-5}");
            builder.Append('}');

            canonicalJson = builder.ToString();
            notes = noteList.ToArray();
            return true;
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
