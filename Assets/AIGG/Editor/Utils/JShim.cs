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
