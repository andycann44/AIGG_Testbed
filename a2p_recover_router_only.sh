#!/usr/bin/env bash
set -euo pipefail

ts="$(date +%Y%m%d_%H%M%S)"
bk="Assets/AIGG/_Backups/Recover_${ts}"
mkdir -p "$bk" Assets/AIGG/Editor/AI Assets/AIGG/Editor

# ---- 0) De-dup any old "OpenAI → Router (no Workbench)" menu items (unicode arrow) ----
# Rename to an ASCII path to avoid collisions.
grep -RIl 'MenuItem("Window/Aim2Pro/Aigg/OpenAI → Router (no Workbench)")' Assets 2>/dev/null | while read -r f; do
  cp -p "$f" "$bk/$(basename "$f").bak"
  /usr/bin/perl -0777 -i '' -pe 's#MenuItem\("Window/Aim2Pro/Aigg/OpenAI → Router \(no Workbench\)"\)#MenuItem("Window/Aim2Pro/Aigg/OpenAI_Router_Only_Legacy")#g' "$f"
done

# ---- 1) Write a clean Router window (ASCII only) ----
router="Assets/AIGG/Editor/PreMergeRouterWindow.cs"
[[ -f "$router" ]] && cp -p "$router" "$bk/PreMergeRouterWindow.cs.bak"
cat > "$router" <<'CS'
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aim2Pro.AIGG.Aigg
{
    public class PreMergeRouterWindow : EditorWindow
    {
        string _raw = "";
        GUIStyle _mono;
        int _tabIndex = 0;
        List<string> _tabKeys = new List<string>();
        Dictionary<string,string> _payloads = new Dictionary<string,string>();
        Vector2 _svRaw, _svTab;

        [MenuItem("Window/Aim2Pro/Aigg/Pre_Merge_Router")]
        public static void Open()
        {
            var w = GetWindow<PreMergeRouterWindow>("Pre-Merge Router");
            w.minSize = new Vector2(800, 520);
            w.Show();
        }

        // Entry point for the runner (reflection)
        public static void ReceiveFromOpenAI(string json, bool focus = true)
        {
            var w = GetWindow<PreMergeRouterWindow>("Pre-Merge Router");
            w.minSize = new Vector2(800, 520);
            w._raw = json ?? "";
            w.TrySplit();
            if (focus) { w.Show(); w.Focus(); }
        }

        void OnEnable() { _mono = null; if (_payloads == null) _payloads = new Dictionary<string,string>(); if (_tabKeys == null) _tabKeys = new List<string>(); }

        void EnsureMono()
        {
            if (_mono != null) return;
            try { _mono = new GUIStyle(EditorStyles.textArea ?? (GUI.skin != null ? GUI.skin.textArea : GUIStyle.none)); }
            catch { _mono = new GUIStyle(GUI.skin != null ? GUI.skin.textArea : GUIStyle.none); }
            _mono.fontSize = 12; _mono.richText = false; _mono.wordWrap = false;
        }

        void OnGUI()
        {
            EnsureMono();
            EditorGUILayout.LabelField("OpenAI Bundle (Raw JSON)", EditorStyles.boldLabel);
            _svRaw = EditorGUILayout.BeginScrollView(_svRaw, GUILayout.MinHeight(140));
            _raw = EditorGUILayout.TextArea(_raw, _mono, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Split Bundle -> Tabs", GUILayout.Height(24))) TrySplit();
                if (GUILayout.Button("Clear", GUILayout.Width(80), GUILayout.Height(24))) { _raw=""; _payloads.Clear(); _tabKeys.Clear(); _tabIndex=0; }
                GUILayout.FlexibleSpace();
                GUI.enabled = _tabKeys.Count > 0;
                if (GUILayout.Button("Send ALL to Paste window", GUILayout.Height(24))) SendAllToPaste();
                GUI.enabled = true;
            }

            GUILayout.Space(6);
            DrawTabs();
        }

        void DrawTabs()
        {
            if (_tabKeys.Count == 0)
            {
                EditorGUILayout.HelpBox("Paste an OpenAI bundle above and click Split. Keys: intents, intents_add, lexicon, lexicon_add.", MessageType.Info);
                return;
            }

            _tabIndex = GUILayout.Toolbar(_tabIndex, _tabKeys.ToArray());
            if (_tabIndex < 0 || _tabIndex >= _tabKeys.Count) _tabIndex = 0;

            var key = _tabKeys[_tabIndex];
            EditorGUILayout.LabelField(key, EditorStyles.miniBoldLabel);
            _svTab = EditorGUILayout.BeginScrollView(_svTab, GUILayout.MinHeight(220));
            string payload = _payloads.TryGetValue(key, out var v) ? v : "";
            payload = EditorGUILayout.TextArea(payload, _mono, GUILayout.ExpandHeight(true));
            _payloads[key] = payload;
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy", GUILayout.Width(80))) { EditorGUIUtility.systemCopyBuffer = payload ?? ""; ShowNotification(new GUIContent("Copied")); }
                if (GUILayout.Button("Open Paste window")) { EditorGUIUtility.systemCopyBuffer = payload ?? ""; OpenPasteWindow(); }
                GUILayout.FlexibleSpace();
            }
        }

        void TrySplit()
        {
            _payloads.Clear(); _tabKeys.Clear();
            var json = _raw ?? ""; if (string.IsNullOrWhiteSpace(json)) return;

            if (TryExtractTopLevelValue(json, "intents_add", out var ia)) AddTab("Intents (Add)", WrapAs("intents", ia));
            if (TryExtractTopLevelValue(json, "intents", out var ii))     AddTab("Intents",      WrapAs("intents", ii));
            if (TryExtractTopLevelValue(json, "lexicon_add", out var la)) AddTab("Lexicon (Add)", BuildLexiconPayload(la));
            if (TryExtractTopLevelValue(json, "lexicon", out var lx))     AddTab("Lexicon",       BuildLexiconPayload(lx));

            if (_tabKeys.Count == 0) AddTab("Bundle (Raw)", json);
        }

        void AddTab(string title, string payload) { _tabKeys.Add(title); _payloads[title] = payload ?? ""; }

        static string WrapAs(string name, string valueJson)
        {
            var v = (valueJson ?? "").Trim();
            if (v.StartsWith("{") && v.Contains("\""+name+"\"")) return v;
            return "{\""+name+"\":"+v+"}";
        }

        static string BuildLexiconPayload(string valueJson)
        {
            var v = (valueJson ?? "").Trim();
            if (v.StartsWith("{") && v.Contains("\"synonyms\"")) return v;
            if (v.StartsWith("{") && v.EndsWith("}")) return "{\"synonyms\":"+v+"}";
            if (v.StartsWith("["))
            {
                var list = MiniParseJsonArrayOfStrings(v);
                var sb = new StringBuilder(); sb.Append("{\"synonyms\":{");
                int wrote = 0;
                foreach (var raw in list)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var s = raw; var idx = s.IndexOf("=>", StringComparison.Ordinal);
                    if (idx <= 0) continue;
                    var lhs = s.Substring(0, idx).Trim().Trim('\"');
                    var rhs = s.Substring(idx + 2).Trim().Trim('\"');
                    if (wrote++ > 0) sb.Append(',');
                    sb.Append('\"').Append(Escape(lhs)).Append("\":\"").Append(Escape(rhs)).Append('\"');
                }
                sb.Append("}}"); return sb.ToString();
            }
            return "{\"synonyms\":{}}";
        }

        static List<string> MiniParseJsonArrayOfStrings(string json)
        {
            var list = new List<string>(); if (string.IsNullOrEmpty(json)) return list;
            bool inStr=false, esc=false; var sb = new StringBuilder();
            for (int i=0;i<json.Length;i++)
            {
                char ch=json[i];
                if (!inStr) { if (ch=='\"') { inStr=true; sb.Length=0; } }
                else {
                    if (esc) { sb.Append(ch); esc=false; }
                    else if (ch=='\\') esc=true;
                    else if (ch=='\"') { inStr=false; list.Add(sb.ToString()); }
                    else sb.Append(ch);
                }
            }
            return list;
        }

        static string Escape(string s) { return (s??"").Replace("\\","\\\\").Replace("\"","\\\""); }

        static bool TryExtractTopLevelValue(string json, string key, out string valueJson)
        {
            valueJson = null; if (string.IsNullOrEmpty(json)) return false;
            string token = "\""+key+"\""; int pos = json.IndexOf(token, StringComparison.Ordinal); if (pos < 0) return false;
            int i = pos + token.Length; while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            i = (i < json.Length && json[i] == ':') ? i + 1 : json.IndexOf(':', i) + 1; if (i <= 0 || i >= json.Length) return false;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++; if (i >= json.Length) return false;
            char start = json[i];
            if (start=='{' || start=='[')
            {
                int depth=0; int j=i;
                while (j < json.Length)
                {
                    char c=json[j];
                    if (c=='\"')
                    {
                        j++; bool esc=false;
                        while (j < json.Length)
                        {
                            char cc=json[j];
                            if (esc) { esc=false; j++; continue; }
                            if (cc=='\\') { esc=true; j++; continue; }
                            if (cc=='\"') { j++; break; }
                            j++;
                        }
                        continue;
                    }
                    if (c=='{' || c=='[') depth++;
                    else if (c=='}' || c==']')
                    {
                        depth--;
                        if (depth==0) { valueJson = json.Substring(i, j - i + 1); return true; }
                    }
                    j++;
                }
                return false;
            }
            else
            {
                int j=i; bool inStr=(start=='\"'); bool esc=false;
                if (inStr)
                {
                    j++;
                    while (j < json.Length)
                    {
                        char cc=json[j];
                        if (esc) { esc=false; j++; continue; }
                        if (cc=='\\') { esc=true; j++; continue; }
                        if (cc=='\"') { j++; break; }
                        j++;
                    }
                }
                else { while (j < json.Length && json[j]!=',' && json[j]!='}' && json[j]!=']') j++; }
                valueJson = json.Substring(i, j - i).Trim(); return valueJson.Length > 0;
            }
        }

        void SendAllToPaste()
        {
            for (int t=0; t<_tabKeys.Count; t++)
            {
                var key=_tabKeys[t];
                if (!_payloads.TryGetValue(key, out var payload)) continue;
                try { EditorGUIUtility.systemCopyBuffer = payload ?? ""; } catch {}
                OpenPasteWindow();
            }
            ShowNotification(new GUIContent("Sent all sections to Paste window (clipboard)."));
        }

        static bool OpenPasteWindow()
        {
            try
            {
                var t = FindType("Aim2Pro.AIGG.Editor.SpecPasteMergeWindow");
                if (t != null)
                {
                    var win = (EditorWindow)EditorWindow.GetWindow(t);
                    win.minSize = new Vector2(700, 450);
                    win.Show(); win.Focus();
                    return true;
                }
            } catch {}
            try
            {
                if (EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Aigg/Paste & Merge")) return true;
                if (EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Aigg/Paste & Replace")) return true;
            } catch {}
            return false;
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
#endif
CS

# ---- 2) Write a runner that ONLY sends to the Router (ASCII menu path) ----
runner="Assets/AIGG/Editor/AI/AIGG_OpenAI_RouterOnly.cs"
[[ -f "$runner" ]] && cp -p "$runner" "$bk/AIGG_OpenAI_RouterOnly.cs.bak"
cat > "$runner" <<'CS'
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Aim2Pro.AIGG.Editor
{
    public class AIGG_OpenAI_RouterOnly : EditorWindow
    {
        string nl = "";
        Vector2 sv;

        [MenuItem("Window/Aim2Pro/Aigg/OpenAI_Router_Only")]
        public static void Open()
        {
            var w = GetWindow<AIGG_OpenAI_RouterOnly>("OpenAI -> Router");
            w.minSize = new Vector2(720, 320);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Natural language prompt", EditorStyles.boldLabel);
            sv = EditorGUILayout.BeginScrollView(sv, GUILayout.MinHeight(120));
            nl = EditorGUILayout.TextArea(nl, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Run OpenAI -> Pre-Merge Router", GUILayout.Height(26)))
                _ = Run(nl);

            EditorGUILayout.HelpBox("Sends FINAL canonical JSON directly to the Pre-Merge Router. Never writes to Workbench.", MessageType.Info);
        }

        async Task Run(string input)
        {
            string json = await RequestCanonicalAsync(input);
            if (!IsJsonLikely(json)) return;
            if (SendToRouter(json)) return;
            try { EditorGUIUtility.systemCopyBuffer = json ?? ""; } catch {}
        }

        static bool IsJsonLikely(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            return s.StartsWith("{") && s.EndsWith("}") && s.Contains(":");
        }

        static bool SendToRouter(string json)
        {
            try
            {
                Type router = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    router = asm.GetType("Aim2Pro.AIGG.Aigg.PreMergeRouterWindow")
                          ?? asm.GetType("Aim2Pro.AIGG.PreMergeRouterWindow");
                    if (router != null) break;
                }
                if (router == null) return false;
                var m = router.GetMethod("ReceiveFromOpenAI", BindingFlags.Public | BindingFlags.Static);
                if (m == null) return false;
                var ps = m.GetParameters();
                if (ps.Length >= 2) m.Invoke(null, new object[] { json ?? string.Empty, true });
                else               m.Invoke(null, new object[] { json ?? string.Empty });
                return true;
            }
            catch { return false; }
        }

        static async Task<string> RequestCanonicalAsync(string input)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Aim2Pro.AIGG.OpenAI.Client");
                    if (t != null)
                    {
                        var m = t.GetMethod("RequestCanonicalAsync", BindingFlags.Public | BindingFlags.Static);
                        if (m != null) return await (Task<string>)m.Invoke(null, new object[] { input });
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Aim2Pro] OpenAI reflection fallback: " + ex.Message); }
            await Task.Delay(150);
            return ""; // no echo
        }
    }
}
#endif
CS

# ---- 3) Put Workbench into router-only mode (disable OpenAI button + hide JSON Output) ----
wb="$(grep -RIl 'class .*WorkbenchWindow' Assets 2>/dev/null | head -n1 || true)"
if [[ -n "$wb" ]]; then
  cp -p "$wb" "$bk/WorkbenchWindow.cs.bak"
  /usr/bin/perl -0777 -i '' -pe 's/if\s*\(\s*GUILayout\.Button\(\s*"Self-?heal\s*\(OpenAI\)"/if (false && GUILayout.Button("Self-heal (OpenAI)"/ig' "$wb"
  python3 - "$wb" <<'PY'
import sys,re
p=sys.argv[1]; s=open(p,'r',encoding='utf-8',errors='ignore').read(); orig=s
lab=re.search(r'"JSON Output"',s)
def repl_after(idx, s):
    m=re.search(r'EditorGUILayout\.TextArea\s*\([^;]*\);', s[idx:])
    if not m: return s, False
    a=idx+m.start(); b=idx+m.end()
    rep='EditorGUILayout.HelpBox("Router-only mode: output hidden.", UnityEditor.MessageType.None);'
    return s[:a]+rep+s[b:], True
changed=False
if lab:
    s,ok=repl_after(lab.end(), s); changed|=ok
if not changed:
    m=re.search(r'EditorGUILayout\.TextArea\s*\([^;]*\);', s)
    if m: s=s[:m.start()]+'EditorGUILayout.HelpBox("Router-only mode", UnityEditor.MessageType.None);'+s[m.end():]; changed=True
if changed: open(p,'w',encoding='utf-8').write(s)
PY
else
  echo "Note: WorkbenchWindow.cs not found; skipping WB patch."
fi

echo "✅ Router-only recovered."
echo "   Backups: $bk"
echo "→ In Unity: Window > Aim2Pro > Aigg > OpenAI_Router_Only  (ASCII path)"
