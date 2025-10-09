using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ---------------- Core: tiny, deterministic ----------------
static class A2PNLCore {
    public static string SpecDir => "Assets/AIGG/Spec";
    static Dictionary<string,string> Synonyms = new(StringComparer.OrdinalIgnoreCase);
    static List<Dictionary<string,object>> Intents  = new();
    static List<Dictionary<string,object>> Commands = new();
    static List<Dictionary<string,object>> Macros   = new();
    static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase){ "m","deg","by","x","rows","row","to","len","steps" };
    static bool loaded=false;

    // anchors derived from command regexes
    static Dictionary<string,string> CmdAnchorByName = new(StringComparer.OrdinalIgnoreCase);
    static HashSet<string> AllCommandAnchors = new(StringComparer.OrdinalIgnoreCase);

    public static (int intents, int commands, int macros) Counts => (Intents.Count, Commands.Count, Macros.Count);

    public static void EnsureLoaded(bool force=false){
        EnsureSpecSkeleton();
        if(loaded && !force) return;
        loaded=false;
        string L(string f)=>Path.Combine(SpecDir,f);
        Synonyms = LoadSynonyms(L("lexicon.json"));
        Intents  = LoadArray(L("intents.json"),  "intents");
        Commands = LoadArray(L("commands.json"), "commands");
        Macros   = LoadArray(L("macros.json"),   "commands");
        BuildCommandAnchors();
        loaded=true;
    }

    public static Dictionary<string,object> Run(string prompt, out List<string> matched, out List<string> unmatched){
        EnsureLoaded();
        var norm = Normalize(prompt);

        var matchedLocal = new List<string>();
        var unmatchedLocal = new List<string>();
        var exec = new List<object>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Scan(List<Dictionary<string,object>> arr, string kind){
            foreach(var it in arr){
                var name = it.TryGet("name") ?? kind;
                if(kind=="commands" && name.StartsWith("auto-", StringComparison.OrdinalIgnoreCase)) continue;
                var rx   = it.TryGet("regex") ?? "";
                if(string.IsNullOrEmpty(rx)) continue;
                var m = Regex.Match(norm, rx, RegexOptions.IgnoreCase);
                if(!m.Success) continue;
                seen.Add(name);
                matchedLocal.Add("["+kind+"] "+name);

                if(kind=="commands" || kind=="macros"){
                    if(it.TryGetValue("kernel", out var kern) && kern is List<object> kl){
                        foreach(var ko in kl){
                            var d = ko as Dictionary<string,object>;
                            var opname = d["op"].ToString();
                            var argSpec = d.ContainsKey("args")? d["args"] as Dictionary<string,object> : new Dictionary<string,object>();
                            var args = new Dictionary<string,object>();
                            foreach(var kv in argSpec) args[kv.Key]=Cast(kv.Value?.ToString()??"", m.Groups);
                            exec.Add(new Dictionary<string,object>{{"op",opname},{"args",args}});
                        }
                    }
                } else if(kind=="intents"){
                    if(it.TryGetValue("ops", out var ops) && ops is List<object> ol){
                        foreach(var oo in ol){
                            var d = oo as Dictionary<string,object>;
                            var path = d["path"].ToString();
                            var val  = Cast(d["value"]?.ToString()??"", m.Groups);
                            exec.Add(new Dictionary<string,object>{{"op","set"},{"path",path},{"value",val}});
                        }
                    }
                }
            }
        }

        Scan(Intents,"intents");
        Scan(Macros,"macros");
        Scan(Commands,"commands");

        var toks = Regex.Split(norm, "[^a-z0-9\\.]+");
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var k in Synonyms.Keys) known.Add(k); foreach(var v in Synonyms.Values) known.Add(v);
        foreach(var t in toks){
            if(string.IsNullOrEmpty(t)) continue;
            if(Regex.IsMatch(t,"^[0-9]+(\\.[0-9]+)?$")) continue;
            if(Stop.Contains(t)) continue;
            if(!known.Contains(t) && !seen.Contains(t)) unmatchedLocal.Add(t);
        }
        if(matchedLocal.Count==0 && unmatchedLocal.Count==0){
            foreach(var t in toks){ if(Regex.IsMatch(t,"^[0-9]+(\\.[0-9]+)?$")) continue; if(Stop.Contains(t)) continue; if(!unmatchedLocal.Contains(t)) unmatchedLocal.Add(t); }
        }

        matched = matchedLocal; unmatched = unmatchedLocal;
        return new Dictionary<string,object>{{"normalized", norm},{"exec", exec},{"matched", matchedLocal},{"unmatched", unmatchedLocal}};
    }

    // ---- completeness helpers (all implied commands must match) ----
    static void BuildCommandAnchors(){
        CmdAnchorByName.Clear(); AllCommandAnchors.Clear();
        foreach(var c in Commands){
            var name = c.TryGet("name") ?? "";
            var rx   = c.TryGet("regex") ?? "";
            if(string.IsNullOrEmpty(name) || string.IsNullOrEmpty(rx)) continue;
            string anchor=null;
            foreach(Match m in Regex.Matches(rx, @"\\b([a-z]{3,})\\b", RegexOptions.IgnoreCase)){
                var w = m.Groups[1].Value.ToLowerInvariant();
                if(w=="rows"||w=="left"||w=="right"||w=="deg"||w=="m"||w=="make"||w=="build"||w=="create") continue;
                if(Stop.Contains(w)) continue;
                anchor = w; break;
            }
            if(!string.IsNullOrEmpty(anchor)){ CmdAnchorByName[name]=anchor; AllCommandAnchors.Add(anchor); }
        }
    }
    public static HashSet<string> AnchorsInPrompt(string norm){
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var a in AllCommandAnchors)
            if(Regex.IsMatch(norm, "\\b"+Regex.Escape(a)+"\\b", RegexOptions.IgnoreCase))
                set.Add(a);
        return set;
    }
    public static string AnchorForCommandName(string name){
        return CmdAnchorByName.TryGetValue(name, out var a) ? a : null;
    }

    // ---- normalization + casts (minimal) ----
    public static string Normalize(string s){
        s = s.Trim().ToLowerInvariant();
        foreach(var kv in Synonyms){ var pat="\\b"+Regex.Escape(kv.Key.ToLowerInvariant())+"\\b"; s = Regex.Replace(s, pat, kv.Value.ToLowerInvariant()); }
        s = Regex.Replace(s, "\\s+", " ").Trim(); return s;
    }
    static object Cast(string spec, GroupCollection g){
        if(string.IsNullOrEmpty(spec)) return null;
        if(!spec.StartsWith("$")) return spec;
        var core = spec.Substring(1); var parts = core.Split(':'); int idx = int.Parse(parts[0]);
        var typ = parts.Length>1?parts[1]:"string"; var txt = (idx>=0 && idx<g.Count)? g[idx].Value : "";
        switch(typ){ case "int": return int.TryParse(txt, out var iv)?iv:0;
            case "float": return double.TryParse(txt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dv)?dv:0.0;
            case "bool": return txt.Equals("true", StringComparison.OrdinalIgnoreCase);
            default: return txt; }
    }

    // ---- safe regex relax (units/by/deg) ----
    public static bool PatchSpec_UnitsAndBy(){
        bool changed=false, changedCmd=false;
        EnsureLoaded();

        string intentsPath = Path.Combine(SpecDir, "intents.json");
        for(int i=0;i<Intents.Count;i++){
            var it = Intents[i];
            if(string.Equals(it.TryGet("name"), "set-size-length-by-width", StringComparison.OrdinalIgnoreCase)){
                string rx = it.TryGet("regex") ?? "";
                string target = @"\b(?:build|make|create)\s+(\d+)\s*m\s*(?:by|x|×)\s*(\d+)\s*m\b";
                if(rx != target){ it["regex"] = target; changed = true; }
            }
        }
        if(changed) SaveArrayFile(intentsPath, "intents", Intents);

        string commandsPath = Path.Combine(SpecDir, "commands.json");
        for(int i=0;i<Commands.Count;i++){
            var it = Commands[i];
            if(string.Equals(it.TryGet("name"), "curve-range", StringComparison.OrdinalIgnoreCase)){
                string rx = it.TryGet("regex") ?? "";
                string target = @"\bcurve\s+rows\s+(\d+)\s*-\s*(\d+)\s+(left|right)\s+(\d+)\s*deg\b";
                if(rx != target){ it["regex"] = target; changedCmd = true; }
            }
        }
        if(changedCmd) SaveArrayFile(commandsPath, "commands", Commands);

        if(changed || changedCmd){ loaded=false; BuildCommandAnchors(); return true; }
        return false;
    }

    // ---- IO utilities (small) ----
    static void EnsureSpecSkeleton(){
        Directory.CreateDirectory(SpecDir);
        EnsureFile(Path.Combine(SpecDir,"lexicon.json"),  "{\"synonyms\":{}}");
        EnsureFile(Path.Combine(SpecDir,"intents.json"),  "{\"intents\":[]}");
        EnsureFile(Path.Combine(SpecDir,"commands.json"), "{\"commands\":[]}");
        EnsureFile(Path.Combine(SpecDir,"macros.json"),   "{\"commands\":[]}");
    }
    static void EnsureFile(string path, string content){ if(!File.Exists(path)) File.WriteAllText(path, content); }
    static Dictionary<string,string> LoadSynonyms(string p){
        var text = File.Exists(p) ? File.ReadAllText(p) : "{}";
        var syn = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var m = Regex.Match(text, "\"synonyms\"\\s*:\\s*\\{(.*?)\\}", RegexOptions.Singleline);
        if(m.Success){ foreach(Match kv in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"")) syn[kv.Groups[1].Value] = JsonUnescape(kv.Groups[2].Value); }
        return syn;
    }
    static List<Dictionary<string,object>> LoadArray(string p, string key){
        var list = new List<Dictionary<string,object>>();
        var text = File.Exists(p) ? File.ReadAllText(p) : "{}";
        var m = Regex.Match(text, "\""+Regex.Escape(key)+"\"\\s*:\\s*\\[(.*)\\]", RegexOptions.Singleline);
        if(!m.Success) return list;
        var body = m.Groups[1].Value;
        int i=0; while(i<body.Length){
            if(body[i]=='{'){ int depth=0, start=i;
                for(; i<body.Length; i++){
                    if(body[i]=='{') depth++; else if(body[i]=='}'){ depth--; if(depth==0){ list.Add(ParseFlatObject(body.Substring(start, i-start+1))); i++; break; } }
                }
            } else i++;
        }
        return list;
    }
    static Dictionary<string,object> ParseFlatObject(string obj){
        var d = new Dictionary<string,object>();
        string Str(string k){ var m = Regex.Match(obj, "\""+Regex.Escape(k)+"\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\""); return m.Success? JsonUnescape(m.Groups[1].Value) : null; }
        d["name"]=Str("name") ?? ""; d["regex"]=Str("regex") ?? "";
        var mk = Regex.Match(obj, "\"kernel\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if(mk.Success){ var arr = new List<object>(); foreach(Match item in Regex.Matches(mk.Groups[1].Value, "\\{(.*?)\\}", RegexOptions.Singleline)){
            var chunk=item.Value; var op = JsonUnescape(Regex.Match(chunk, "\"op\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"").Groups[1].Value);
            var args = new Dictionary<string,object>(); var ma = Regex.Match(chunk, "\"args\"\\s*:\\s*\\{(.*?)\\}", RegexOptions.Singleline);
            if(ma.Success){ foreach(Match kv in Regex.Matches(ma.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"")) args[kv.Groups[1].Value] = JsonUnescape(kv.Groups[2].Value); }
            arr.Add(new Dictionary<string,object>{{"op",op},{"args",args}});} d["kernel"]=arr; }
        var mo = Regex.Match(obj, "\"ops\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if(mo.Success){ var arr = new List<object>(); foreach(Match item in Regex.Matches(mo.Groups[1].Value, "\\{(.*?)\\}", RegexOptions.Singleline)){
            var chunk=item.Value; var op = JsonUnescape(Regex.Match(chunk, "\"op\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"").Groups[1].Value);
            var path= JsonUnescape(Regex.Match(chunk, "\"path\"\\s*:\\*?\"?((?:\\\\.|[^\"])*)\"").Groups[1].Value);
            var qv  = Regex.Match(chunk, "\"value\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"").Groups[1].Value;
            string val = !string.IsNullOrEmpty(qv) ? JsonUnescape(qv) : Regex.Match(chunk, "\"value\"\\s*:\\s*([^,}\\]]+)").Groups[1].Value.Trim();
            arr.Add(new Dictionary<string,object>{{"op",op},{"path",path},{"value",val}});} d["ops"]=arr; }
        return d;
    }
    static void SaveArrayFile(string path, string key, List<Dictionary<string,object>> arr){
        var root = new Dictionary<string,object>{{key, arr}};
        File.WriteAllText(path, ObjToJson(root));
    }
    // JSON write helpers
    static string ObjToJson(Dictionary<string,object> d){
        var sb=new StringBuilder(); sb.Append('{'); bool f=true;
        foreach(var kv in d){ if(!f) sb.Append(','); sb.Append(JsonEscape(kv.Key)); sb.Append(':'); sb.Append(Val(kv.Value)); f=false; } sb.Append('}'); return sb.ToString();
    }
    static string Val(object v){
        if(v==null) return "null";
        if(v is string s) return JsonEscape(s);
        if(v is bool b) return b ? "true" : "false";
        if(v is int || v is long || v is float || v is double || v is decimal) return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        if(v is Dictionary<string,object> d) return ObjToJson(d);
        if(v is System.Collections.IList il){ var sb=new StringBuilder(); sb.Append('['); bool f=true; foreach(var x in il){ if(!f) sb.Append(','); sb.Append(Val(x)); f=false; } sb.Append(']'); return sb.ToString(); }
        return JsonEscape(v.ToString());
    }
    static string JsonEscape(string s){
        var sb=new StringBuilder(); sb.Append('"');
        foreach(var c in s){ switch(c){ case '"': sb.Append("\\\""); break; case '\\': sb.Append("\\\\"); break; case '\b': sb.Append("\\b"); break; case '\f': sb.Append("\\f"); break; case '\n': sb.Append("\\n"); break; case '\r': sb.Append("\\r"); break; case '\t': sb.Append("\\t"); break; default: if(c<32||c>126) sb.Append("\\u"+((int)c).ToString("x4")); else sb.Append(c); break; } }
        sb.Append('"'); return sb.ToString();
    }
    static string JsonUnescape(string s){
        if (s==null) return null; var sb=new StringBuilder();
        for(int i=0;i<s.Length;i++){ char c=s[i];
            if(c=='\\' && i+1<s.Length){ char n=s[++i];
                switch(n){ case '"': sb.Append('\"'); break; case '\\': sb.Append('\\'); break; case '/': sb.Append('/'); break; case 'b': sb.Append('\b'); break; case 'f': sb.Append('\f'); break; case 'n': sb.Append('\n'); break; case 'r': sb.Append('\r'); break; case 't': sb.Append('\t'); break;
                    case 'u': if(i+4<s.Length && ushort.TryParse(s.Substring(i+1,4), System.Globalization.NumberStyles.HexNumber, null, out var code)){ sb.Append((char)code); i+=4; } else sb.Append('u'); break;
                    default: sb.Append(n); break; } }
            else sb.Append(c); }
        return sb.ToString();
    }
    static string TryGet(this Dictionary<string,object> d, string k){ return (d!=null && d.TryGetValue(k, out var v) && v!=null) ? v.ToString() : null; }
}

// ---------------- UI Window (lean) ----------------
public class NLGeneratorWindow : EditorWindow {
    string nl = "build 105m by 6m, curve rows 10-20 left 15deg";
    string usedPrompt = "";
    string jsonOut = "{}";
    string status = "Ready";
    bool autoFixed = false;
    Vector2 s1, s2;
    List<string> lastMatched = new();
    List<string> lastUnmatched = new();
    Dictionary<string,object> lastResult = null;

    [MenuItem("Window/Aim2Pro/Track Creator/NL Generator")]
    public static void Open(){ GetWindow<NLGeneratorWindow>("NL Generator"); }

    void OnGUI(){
        using(new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)){
            GUILayout.Label($"Spec: {A2PNLCore.SpecDir}", EditorStyles.miniLabel);
            A2PNLCore.EnsureLoaded(); var (i,c,m)=A2PNLCore.Counts;
            GUILayout.FlexibleSpace(); GUILayout.Label($"intents: {i} • commands: {c} • macros: {m}", EditorStyles.miniBoldLabel);
            if(GUILayout.Button("Reload", GUILayout.Height(18), GUILayout.Width(80))) A2PNLCore.EnsureLoaded(true);
        }

        GUILayout.Label("Natural Language Prompt", EditorStyles.boldLabel);
        nl = EditorGUILayout.TextArea(nl, GUILayout.MinHeight(60));

        using(new EditorGUILayout.HorizontalScope()){
            if(GUILayout.Button("NL > Json", GUILayout.Height(28))) RunOnce();
            if(GUILayout.Button("Copy JSON", GUILayout.Height(28), GUILayout.Width(110))){
                EditorGUIUtility.systemCopyBuffer = Pretty(jsonOut);
                status = "Copied JSON to clipboard.";
            }
        }

        if(autoFixed) EditorGUILayout.HelpBox($"Auto-fixed input → \"{usedPrompt}\"", MessageType.Info);
        EditorGUILayout.HelpBox(status, status.StartsWith("Matched")? MessageType.Info : MessageType.Warning);

        GUILayout.Space(6);
        GUILayout.Label("Matched / Unmatched", EditorStyles.boldLabel);
        s1 = EditorGUILayout.BeginScrollView(s1, GUILayout.MinHeight(90), GUILayout.MaxHeight(160));
        if(lastMatched.Count==0 && lastUnmatched.Count==0) GUILayout.Label("— nothing yet —", EditorStyles.miniLabel);
        else {
            foreach(var x in lastMatched) GUILayout.Label("• "+x, EditorStyles.miniBoldLabel);
            if(lastUnmatched.Count>0){ GUILayout.Space(4); GUILayout.Label("Unmatched tokens:", EditorStyles.miniBoldLabel);
                GUILayout.Label(string.Join(", ", lastUnmatched), EditorStyles.wordWrappedMiniLabel); }
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(6);
        GUILayout.Label("Generated JSON (shown only when complete)", EditorStyles.boldLabel);
        s2 = EditorGUILayout.BeginScrollView(s2, GUILayout.MinHeight(140));
        if(IsComplete()) EditorGUILayout.TextArea(Pretty(jsonOut), GUILayout.ExpandHeight(true));
        else GUILayout.Label("— incomplete: every implied command must match, and no unmatched tokens —", EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
    }

    void RunOnce(){
        autoFixed=false; usedPrompt = nl;

        Execute(usedPrompt);
        if(IsComplete()) return;

        // tiny auto-fix (spacing + by/x/×)
        var fixedPrompt = AutoFix(usedPrompt);
        if(fixedPrompt != usedPrompt){
            usedPrompt = fixedPrompt; autoFixed = true;
            Execute(usedPrompt);
            if(IsComplete()) return;
        }

        // relax common regex strictness for units/by/deg
        if(A2PNLCore.PatchSpec_UnitsAndBy()){
            AssetDatabase.Refresh();
            Execute(usedPrompt);
        }
    }

    void Execute(string prompt){
        try{
            lastResult = A2PNLCore.Run(prompt, out lastMatched, out lastUnmatched);
            jsonOut = Obj(lastResult);
            int cmd = CountKind(lastMatched, "[commands]");
            int intent = CountKind(lastMatched, "[intents]");
            status = (lastMatched.Count==0) ? "No rules matched this prompt."
                : $"Matched {lastMatched.Count} rule(s) • cmds:{cmd} intents:{intent}";
        }catch(Exception ex){
            lastResult=null; lastMatched.Clear(); lastUnmatched.Clear();
            status = "Error: " + ex.Message;
        }
        Repaint();
    }

    bool IsComplete(){
        if (lastResult == null) return false;
        // must have executable ops
        if (!(lastResult.TryGetValue("exec", out var e) && e is System.Collections.IList il && il.Count > 0))
            return false;
        // must have zero unmatched tokens
        if (lastUnmatched != null && lastUnmatched.Count > 0)
            return false;
        // if the prompt implies command verbs, every one must be matched
        var norm = A2PNLCore.Normalize(usedPrompt);
        var implied = A2PNLCore.AnchorsInPrompt(norm);   // e.g., { "curve" }
        if (implied.Count == 0) return true;             // intent-only is fine
        var matchedAnchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in lastMatched){
            if (tag.StartsWith("[commands] ")){
                var name = tag.Substring(12);
                var a = A2PNLCore.AnchorForCommandName(name);
                if (!string.IsNullOrEmpty(a)) matchedAnchors.Add(a);
            }
        }
        foreach (var a in implied) if (!matchedAnchors.Contains(a)) return false;
        return true;
    }

    static string AutoFix(string s){
        var t = s;
        t = Regex.Replace(t, @"(\d)\s*[xX×]\s*(\d)", "$1 by $2");
        t = Regex.Replace(t, @"(?<=\d)\s*(m\b)", " $1", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(?<=\d)\s*(deg\b)", " $1", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\s+", " ").Trim();
        return t;
    }

    // tiny JSON helpers for UI only (no allocations elsewhere)
    static string Pretty(string raw){
        var sb=new StringBuilder(); int ind=0; bool q=false;
        foreach(char c in raw){
            if(c=='"') q=!q;
            if(!q && c==','){ sb.Append(c); sb.Append('\n'); sb.Append(new string(' ',ind*2)); continue; }
            if(!q && (c=='{'||c=='[')){ sb.Append(c); sb.Append('\n'); ind++; sb.Append(new string(' ',ind*2)); continue; }
            if(!q && (c=='}'||c==']')){ sb.Append('\n'); ind=Math.Max(0,ind-1); sb.Append(new string(' ',ind*2)); sb.Append(c); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }
    static string Obj(Dictionary<string,object> d){
        System.Func<object,string> V=null; V=(v)=>{ if(v==null) return "null";
            if(v is string s) return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
            if(v is bool b) return b?"true":"false";
            if(v is int||v is long||v is float||v is double||v is decimal) return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
            if(v is Dictionary<string,object> dd){ var sb=new StringBuilder(); sb.Append('{'); bool f=true; foreach(var kv in dd){ if(!f) sb.Append(','); sb.Append(V(kv.Key)); sb.Append(':'); sb.Append(V(kv.Value)); f=false; } sb.Append('}'); return sb.ToString(); }
            if(v is System.Collections.IList il){ var sb=new StringBuilder(); sb.Append('['); bool f=true; foreach(var x in il){ if(!f) sb.Append(','); sb.Append(V(x)); f=false; } sb.Append(']'); return sb.ToString(); }
            return "\""+v.ToString()+"\""; };
        return V(d);
    }
    static int CountKind(List<string> list, string tag){ int n=0; foreach(var s in list) if(s.StartsWith(tag, StringComparison.OrdinalIgnoreCase)) n++; return n; }
}
