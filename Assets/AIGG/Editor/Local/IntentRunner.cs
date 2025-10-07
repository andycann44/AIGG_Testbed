#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;

namespace Aim2Pro.AIGG.Local
{
    public static class IntentRunner
    {
        [Serializable] class OpDTO { public string op="set"; public string path; public string value; }
        [Serializable] class IntentDTO { public string name; public string regex; public List<OpDTO> ops; }
        [Serializable] class IntentsFile { public List<IntentDTO> intents; }

        static string FindJson(string fileName)
        {
            foreach (var p in Directory.GetFiles(Application.dataPath, "*.json", SearchOption.AllDirectories))
                if (Path.GetFileName(p).Equals(fileName, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        static Dictionary<string,object> NewScenePlan()
        {
            return new Dictionary<string, object> {
                {"type","scenePlan"},
                {"name","Quick Plan"},
                {"grid", new Dictionary<string,object>{{"cols",1},{"rows",1},{"dx",30.0f},{"dy",18.0f},{"origin", new Dictionary<string,object>{{"x",0f},{"y",0f}}}}},
                {"trackTemplate", new Dictionary<string,object>{
                    {"lanes",1},{"segments", new List<object>{"straight"}},
                    {"lengthUnits",100},{"tileWidth",1.0f},
                    {"zones", new Dictionary<string,object>{
                        {"start", new Dictionary<string,object>{{"size", new Dictionary<string,object>{{"x",2.0f},{"y",3.0f}}}}},
                        {"end",   new Dictionary<string,object>{{"size", new Dictionary<string,object>{{"x",2.0f},{"y",3.0f}}}}}
                    }},
                    {"killZone", new Dictionary<string,object>{{"y",-5.0f},{"height",2.0f}}}
                }},
                {"difficulty", new Dictionary<string,object>{
                    {"tracks",1},
                    {"playerSpeed", new Dictionary<string,object>{{"start",6.0f},{"deltaPerTrack",0.35f}}},
                    {"jumpForce",9.0f},
                    {"gapProbability", new Dictionary<string,object>{{"start",0.02f},{"deltaPerTrack",0.02f},{"max",0.35f}}},
                    {"gapRules", new Dictionary<string,object>{{"noAtSpawn",true},{"noAdjacentGaps",true},{"maxGapWidth",1}}}
                }},
                {"progression", new Dictionary<string,object>{
                    {"ordering","rowMajor"},
                    {"carrier", new Dictionary<string,object>{{"type","dartboardTaxi"},{"attachKinematic",true},{"moveSpeed",7.0f}}}
                }},
                {"layers", new Dictionary<string,object>{{"track","Track"},{"player","Player"},{"killZone","KillZone"}}},
                {"camera", new Dictionary<string,object>{{"offsetX",2.0f},{"offsetY",1.0f},{"smooth",0.15f}}},
                {"meta", new Dictionary<string,object>{{"slope","flat"},{"slopeDegrees",0.0f},{"amplitude",1.5f},{"wavelength",6.0f}}}
            };
        }

        public static string RunFromNL(string nl)
        {
            if (nl == null) nl = "";

            // Load intents.json via typed DTOs (JsonUtility works for this)
            var intents = new List<IntentDTO>();
            try {
                var ip = FindJson("intents.json");
                if (!string.IsNullOrEmpty(ip))
                {
                    var text = File.ReadAllText(ip);
                    var root = JsonUtility.FromJson<IntentsFile>(text);
                    if (root != null && root.intents != null) intents = root.intents;
                }
            } catch (Exception e) {
                Debug.LogWarning("AIGG intents load failed: "+e.Message);
            }

            var doc = NewScenePlan();
            foreach (var it in intents)
            {
                if (it == null || string.IsNullOrEmpty(it.regex)) continue;
                Match m;
                try { m = Regex.Match(nl, it.regex, RegexOptions.IgnoreCase); }
                catch (Exception re) { Debug.LogWarning($"Intent regex invalid '{it.name}': {re.Message}"); continue; }
                if (!m.Success) continue;

                if (it.ops == null) continue;
                foreach (var op in it.ops)
                {
                    if (op == null) continue;
                    if (!string.Equals(op.op ?? "set","set",StringComparison.OrdinalIgnoreCase)) continue;
                    var val = ExpandValue(op.value ?? "", m);
                    SetByPath(doc, op.path ?? "$", val);
                }
            }

            return Serialize(doc, pretty:true);
        }

        static object ExpandValue(string spec, Match m)
        {
            if (string.IsNullOrEmpty(spec)) return "";
            // Substitute $1, $2 …
            var s = Regex.Replace(spec, @"\$(\d+)", mm => {
                int gi = int.Parse(mm.Groups[1].Value);
                return gi < m.Groups.Count ? m.Groups[gi].Value : "";
            });

            // Type suffix like ":int", ":float", ":bool"
            string type = null;
            int ix = s.LastIndexOf(':');
            if (ix >= 0) { type = s.Substring(ix+1); s = s.Substring(0, ix); }

            if (string.Equals(type,"int",StringComparison.OrdinalIgnoreCase) && int.TryParse(s, out var iv)) return iv;
            if (string.Equals(type,"float",StringComparison.OrdinalIgnoreCase) && float.TryParse(s, out var fv)) return fv;
            if (string.Equals(type,"bool",StringComparison.OrdinalIgnoreCase) && bool.TryParse(s, out var bv)) return bv;

            if (string.Equals(s,"true",StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(s,"false",StringComparison.OrdinalIgnoreCase)) return false;
            return s;
        }

        static void SetByPath(Dictionary<string,object> root, string path, object value)
        {
            if (string.IsNullOrEmpty(path)) return;
            var p = path.Trim();
            if (p.StartsWith("$.")) p = p.Substring(2);
            var keys = p.Split('.');
            var cur = root;
            for (int i=0;i<keys.Length;i++)
            {
                var k = keys[i];
                if (i == keys.Length-1) { cur[k] = value; break; }
                if (!cur.TryGetValue(k, out var next) || !(next is Dictionary<string,object>))
                {
                    next = new Dictionary<string,object>();
                    cur[k] = next;
                }
                cur = (Dictionary<string,object>)next;
            }
        }

        // Minimal JSON serializer for Dictionary/List/String/Number/Bool
        static string Serialize(object obj, bool pretty=false, int depth=0)
        {
            if (obj == null) return "null";
            switch (obj)
            {
                case string s: return "\"" + s.Replace("\\","\\\\").Replace("\"","\\\"") + "\"";
                case bool b: return b ? "true" : "false";
                case int i: return i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case long l: return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case float f: return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case double d: return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (obj is Dictionary<string,object> map)
            {
                var indent = pretty ? new string(' ', depth*2) : "";
                var indent2 = pretty ? new string(' ', (depth+1)*2) : "";
                var nl = pretty ? "\n" : "";
                var sep = pretty ? ",\n" : ",";
                var parts = new List<string>();
                foreach (var kv in map)
                    parts.Add($"{(pretty?indent2:"")}\"{kv.Key.Replace("\\","\\\\").Replace("\"","\\\"")}\":{(pretty?" ":"")}{Serialize(kv.Value, pretty, depth+1)}");
                return "{"+nl+string.Join(sep, parts)+(pretty?nl+indent:"")+"}";
            }

            if (obj is System.Collections.IEnumerable list && !(obj is string))
            {
                var indent = pretty ? new string(' ', depth*2) : "";
                var indent2 = pretty ? new string(' ', (depth+1)*2) : "";
                var nl = pretty ? "\n" : "";
                var sep = pretty ? ",\n" : ",";
                var parts = new List<string>();
                foreach (var v in list) parts.Add($"{(pretty?indent2:"")}{Serialize(v, pretty, depth+1)}");
                return "["+nl+string.Join(sep, parts)+(pretty?nl+indent:"")+"]";
            }

            return "\"" + obj.ToString().Replace("\\","\\\\").Replace("\"","\\\"") + "\"";
        }
    }
}
#endif
