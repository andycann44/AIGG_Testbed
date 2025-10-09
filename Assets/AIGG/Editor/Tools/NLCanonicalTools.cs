#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public static class NLCanonicalTools {
    const string CanonPath = "Assets/AIGG/Test/AIGG_TrackCreator_NL_canonical.json";
    const string OutPath   = "Assets/AIGG/Test/AIGG_TrackCreator_NL_output.json";

    [MenuItem("Aim2Pro/Track Creator/Validate Canonical NL")]
    public static void ValidateCanonical() {
        EnsureExists(CanonPath);
        var raw = File.ReadAllText(CanonPath);

        // Prefer "normalized" from canonical; otherwise auto-fix "nl"
        string nl = ExtractField(raw, "normalized");
        if (string.IsNullOrEmpty(nl)) {
            var nlRaw = ExtractField(raw, "nl");
            if (string.IsNullOrEmpty(nlRaw)) { Debug.LogError("[NL] canonical missing both \"normalized\" and \"nl\""); return; }
            nl = AutoFix(nlRaw);
        }

        A2PNLCore.EnsureLoaded(true);
        A2PNLCore.PatchSpec_UnitsAndBy(); // ensure tolerant regex
        var result = A2PNLCore.Run(nl, out var matched, out var unmatched);

        // Save actual output for easy diff
        File.WriteAllText(OutPath, JsonW.Pretty(JsonW.Obj(result)));
        AssetDatabase.Refresh();

        bool hasExec = result.TryGetValue("exec", out var e) && e is System.Collections.IList il && il.Count > 0;
        if (!hasExec) { Debug.LogError("[NL] Parser produced no exec ops. See " + OutPath); return; }
        if (unmatched != null && unmatched.Count > 0) {
            Debug.LogWarning("[NL] Unmatched tokens: " + string.Join(", ", unmatched) + "  (See " + OutPath + ")");
        } else {
            Debug.Log("[NL] ✅ Canonical NL parsed with exec ops and no unmatched. Output saved: " + OutPath);
        }
    }

    [MenuItem("Aim2Pro/Track Creator/Send Canonical NL > Bus")]
    public static void SendCanonicalToBus() {
        EnsureExists(CanonPath);
        var raw = File.ReadAllText(CanonPath);

        string nl = ExtractField(raw, "normalized");
        if (string.IsNullOrEmpty(nl)) {
            var nlRaw = ExtractField(raw, "nl");
            if (string.IsNullOrEmpty(nlRaw)) { Debug.LogError("[NL] canonical missing both \"normalized\" and \"nl\""); return; }
            nl = AutoFix(nlRaw);
        }

        A2PNLCore.EnsureLoaded(true);
        A2PNLCore.PatchSpec_UnitsAndBy();
        var result = A2PNLCore.Run(nl, out var matched, out var unmatched);

        try {
            A2PNLBus.Send?.Invoke(result);
            Debug.Log("[NL] ▶ Sent canonical payload via A2PNLBus.Send");
        } catch (System.SystemException ex) {
            Debug.LogError("[NL] Bus send failed: " + ex.Message);
        }
    }

    // ---- helpers ----
    static void EnsureExists(string p) {
        if (!File.Exists(p)) throw new System.IO.FileNotFoundException("Missing file", p);
    }

    static string ExtractField(string json, string key) {
        // supports escaped quotes inside strings
        var m = Regex.Match(json, "\""+Regex.Escape(key)+"\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"", RegexOptions.Singleline);
        if (!m.Success) return null;
        return JsonUnescape(m.Groups[1].Value);
    }

    static string JsonUnescape(string s){
        if (s==null) return null;
        var sb=new System.Text.StringBuilder();
        for(int i=0;i<s.Length;i++){
            char c=s[i];
            if(c=='\\' && i+1<s.Length){
                char n=s[++i];
                switch(n){
                    case '"': sb.Append('\"'); break; case '\\': sb.Append('\\'); break; case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break; case 'f': sb.Append('\f'); break; case 'n': sb.Append('\n'); break; case 'r': sb.Append('\r'); break; case 't': sb.Append('\t'); break;
                    case 'u': if(i+4<s.Length && ushort.TryParse(s.Substring(i+1,4), System.Globalization.NumberStyles.HexNumber, null, out var code)){ sb.Append((char)code); i+=4; } else sb.Append('u'); break;
                    default: sb.Append(n); break;
                }
            } else sb.Append(c);
        }
        return sb.ToString();
    }

    // same tiny auto-fix used in the window
    static string AutoFix(string s){
        var t = s;
        t = Regex.Replace(t, @"(\d)\s*[xX×]\s*(\d)", "$1 by $2");
        t = Regex.Replace(t, @"(?<=\d)\s*(m\b)", " $1", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=\d)\s*(deg\b)", " $1", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\s+", " ").Trim();
        return t;
    }
}
#endif
