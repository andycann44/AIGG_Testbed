#if UNITY_EDITOR
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public static class JsonW {
    // Return a compact JSON string for dictionaries/lists/primitives.
    public static string Obj(object v) { return Val(v); }

    // Pretty-print a JSON string (basic indentation, string-aware).
    public static string Pretty(string raw){
        if (string.IsNullOrEmpty(raw)) return raw ?? "";
        var sb = new StringBuilder();
        int ind = 0; bool inString = false; bool esc = false;
        for (int i = 0; i < raw.Length; i++){
            char c = raw[i];
            if (inString){
                sb.Append(c);
                esc = (!esc && c == '\\');
                if (!esc && c == '"') inString = false;
                continue;
            }
            if (c == '"'){ sb.Append(c); inString = true; esc = false; continue; }
            if (c == '{' || c == '['){ sb.Append(c); sb.Append('\n'); ind++; sb.Append(new string(' ', ind*2)); }
            else if (c == '}' || c == ']'){ sb.Append('\n'); ind = Math.Max(0, ind-1); sb.Append(new string(' ', ind*2)); sb.Append(c); }
            else if (c == ','){ sb.Append(c); sb.Append('\n'); sb.Append(new string(' ', ind*2)); }
            else { sb.Append(c); }
        }
        return sb.ToString();
    }

    static string Val(object v){
        if (v == null) return "null";
        if (v is string) return Quote((string)v);
        if (v is bool)   return (bool)v ? "true" : "false";
        if (v is int || v is long || v is float || v is double || v is decimal)
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);

        if (v is IDictionary dict){
            var sb = new StringBuilder(); sb.Append('{'); bool first = true;
            foreach (DictionaryEntry de in dict){
                if (!first) sb.Append(',');
                sb.Append(Quote(Convert.ToString(de.Key)));
                sb.Append(':');
                sb.Append(Val(de.Value));
                first = false;
            }
            sb.Append('}'); return sb.ToString();
        }

        if (v is IEnumerable en && !(v is string)){
            var sb = new StringBuilder(); sb.Append('['); bool first = true;
            foreach (var it in en){
                if (!first) sb.Append(',');
                sb.Append(Val(it));
                first = false;
            }
            sb.Append(']'); return sb.ToString();
        }

        return Quote(Convert.ToString(v));
    }

    static string Quote(string s){
        if (s == null) return "null";
        var sb = new StringBuilder(); sb.Append('"');
        foreach (char c in s){
            switch (c){
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32 || c > 126) sb.Append("\\u" + ((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"'); return sb.ToString();
    }
}
#endif
