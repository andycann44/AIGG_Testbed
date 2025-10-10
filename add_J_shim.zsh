#!/bin/zsh
set -e

mkdir -p "Assets/AIGG/Editor/Utils"

cat > "Assets/AIGG/Editor/Utils/JShim.cs" <<'CS'
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Aim2Pro.AIGG
{
    /// <summary>
    /// Minimal shim to satisfy legacy calls like J.Join(...), J.Parse(...), etc.
    /// Extend if your code needs more helpers.
    /// </summary>
    internal static class J
    {
        // String helpers
        public static string Join(string separator, params string[] parts) =>
            string.Join(separator, parts);
        public static string Join(string separator, IEnumerable<string> parts) =>
            string.Join(separator, parts);

        // JSON helpers (Newtonsoft)
        public static JToken Parse(string json) => JToken.Parse(json);
        public static bool TryParse(string json, out JToken token)
        {
            try { token = JToken.Parse(json); return true; }
            catch { token = null; return false; }
        }
        public static JObject Obj() => new JObject();
        public static JArray Arr() => new JArray();
        public static JValue Val(object value) => new JValue(value);

        // Convenience object builders
        public static JObject Obj(params (string key, object value)[] kvps)
        {
            var o = new JObject();
            foreach (var (k, v) in kvps)
                o[k] = v is JToken jt ? jt : (v == null ? JValue.CreateNull() : JToken.FromObject(v));
            return o;
        }

        public static JArray Arr(params object[] items)
        {
            var a = new JArray();
            foreach (var v in items)
                a.Add(v is JToken jt ? jt : (v == null ? JValue.CreateNull() : JToken.FromObject(v)));
            return a;
        }
    }
}
#endif
CS

echo "J shim added at Assets/AIGG/Editor/Utils/JShim.cs"
