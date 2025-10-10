#!/bin/zsh
set -e

# 1) Minimal, package-free shim
mkdir -p "Assets/AIGG/Editor/Utils"
cat > "Assets/AIGG/Editor/Utils/JShim.cs" <<'CS'
#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Aim2Pro.AIGG
{
    /// <summary>
    /// Package-free JSON shim. Compiles without Newtonsoft.
    /// If you later install com.unity.nuget.newtonsoft-json, you can
    /// swap these to real JToken/JObject/JArray easily.
    /// </summary>
    internal static class J
    {
        // ---- String helpers ----
        public static string Join(string separator, params string[] parts) =>
            string.Join(separator, parts);
        public static string Join(string separator, IEnumerable<string> parts) =>
            string.Join(separator, parts);

        // ---- Lightweight stand-ins for JSON types ----
        public class JObject : Dictionary<string, object> { }
        public class JArray  : List<object> { }
        public class JValue
        {
            public object Value;
            public JValue(object v) { Value = v; }
            public static JValue CreateNull() => new JValue(null);
            public override string ToString() => Value?.ToString() ?? "null";
        }

        // ---- API surface (safe no-ops until Newtonsoft is added) ----
        public static object Parse(string json)
            => throw new NotSupportedException("J.Parse requires Newtonsoft.Json. Install com.unity.nuget.newtonsoft-json to enable parsing.");
        public static bool TryParse(string json, out object token)
        { token = null; return false; }

        public static JObject Obj() => new JObject();
        public static JArray  Arr() => new JArray();
        public static JValue  Val(object value) => new JValue(value);

        public static JObject Obj(params (string key, object value)[] kvps)
        {
            var o = new JObject();
            foreach (var (k, v) in kvps) o[k] = v;
            return o;
        }

        public static JArray Arr(params object[] items)
        {
            var a = new JArray();
            foreach (var v in items) a.Add(v);
            return a;
        }
    }
}
#endif
CS

echo "✓ Wrote package-free J shim: Assets/AIGG/Editor/Utils/JShim.cs"

# 2) (Optional) Ensure Newtonsoft package is listed (won't break if already there)
if [[ -f "Packages/manifest.json" ]]; then
  /usr/bin/ruby - <<'RUBY'
require "json"
path = "Packages/manifest.json"
m = JSON.parse(File.read(path))
m["dependencies"] ||= {}
ver = m["dependencies"]["com.unity.nuget.newtonsoft-json"]
if ver.nil?
  # Use a safe, recent version; Unity will resolve to something compatible
  m["dependencies"]["com.unity.nuget.newtonsoft-json"] = "3.2.1"
  File.write(path, JSON.pretty_generate(m))
  puts "✓ Added com.unity.nuget.newtonsoft-json@3.2.1 to Packages/manifest.json"
else
  puts "• Newtonsoft already present (#{ver})"
end
RUBY
else
  echo "• Packages/manifest.json not found (skipped adding Newtonsoft)."
fi

echo "Done. Bring Unity to front to recompile."
