#!/usr/bin/env bash
set -euo pipefail

log(){ printf '%s\n' "$*"; }

# ---- 1) Patch WorkbenchWindow.cs (disable Self-heal OpenAI + hide JSON Output) ----
WB="$(grep -RIl 'class .*WorkbenchWindow' Assets 2>/dev/null | head -n1 || true)"
if [[ -z "$WB" ]]; then
  log "WB: WorkbenchWindow.cs not found (skipping WB patch)."
else
  TS="$(date +%Y%m%d_%H%M%S)"
  BKP="Assets/AIGG/_Backups/WB_RouterOnly_${TS}"
  mkdir -p "$BKP"
  cp -p "$WB" "$BKP/WorkbenchWindow.cs.bak"
  python3 - "$WB" <<'PY'
import sys,re
p=sys.argv[1]
s=open(p,'r',encoding='utf-8',errors='ignore').read()
orig=s; changed=False

# Disable the "Self-heal (OpenAI)" button – support minor label variants.
s2, n = re.subn(r'if\s*\(\s*GUILayout\.Button\s*\(\s*"(?:Self[- ]?heal|Self[ -]?heal)\s*\(OpenAI\)\s*"', 
                'if (false && GUILayout.Button("Self-heal (OpenAI)"', s, flags=re.IGNORECASE)
if n: changed=True; s=s2

# Hide the "JSON Output" area: replace first TextArea after the "JSON Output" label with a HelpBox.
lab=re.search(r'"JSON Output"',s)
def replace_first_textarea_from(pos, s):
    m=re.search(r'EditorGUILayout\.TextArea\s*\(', s[pos:])
    if not m: return s, False
    start=pos+m.start()
    # find end of this call (match parentheses) and trailing ';'
    i=start; depth=0; end=None
    while i<len(s):
        c=s[i]
        if c=='(':
            depth+=1
        elif c==')':
            depth-=1
            if depth==0:
                j=i
                while j<len(s) and s[j]!=';': j+=1
                end=j+1; break
        i+=1
    if end is None: return s, False
    repl='EditorGUILayout.HelpBox("Router-only mode: Workbench output hidden.", UnityEditor.MessageType.None);'
    return s[:start]+repl+s[end:], True

did=False
if lab:
    s, did = replace_first_textarea_from(lab.end(), s)
    changed |= did

# Fallback: if not found, blank any TextArea fed by likely vars (canonical/json/output)
if not did:
    for var in ['canonical','json','output','result']:
        pat = r'EditorGUILayout\.TextArea\s*\(\s*' + var + r'\b'
        if re.search(pat, s):
            s = re.sub(pat, 'EditorGUILayout.TextArea("",', s, count=1)
            changed=True
            break

if changed:
    open(p,'w',encoding='utf-8').write(s)
    print("WB patched:", p)
else:
    print("WB unchanged:", p)
PY
fi

# ---- 2) Ensure an OpenAI → Router runner that never touches Workbench ----
AI_DIR="Assets/AIGG/Editor/AI"
RUNNER="$AI_DIR/AIGG_OpenAI_RouterOnly.cs"
mkdir -p "$AI_DIR"

cat > "$RUNNER" <<'CS'
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

        [MenuItem("Window/Aim2Pro/Aigg/OpenAI → Router (no Workbench)")]
        public static void Open()
        {
            var w = GetWindow<AIGG_OpenAI_RouterOnly>("OpenAI → Router");
            w.minSize = new Vector2(720, 320);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Natural language prompt", EditorStyles.boldLabel);
            sv = EditorGUILayout.BeginScrollView(sv, GUILayout.MinHeight(120));
            nl = EditorGUILayout.TextArea(nl, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Run OpenAI → Pre-Merge Router", GUILayout.Height(26)))
                _ = Run(nl);

            EditorGUILayout.HelpBox("Sends FINAL canonical JSON directly to the Pre-Merge Router.\nThis window never writes to Workbench.", MessageType.Info);
        }

        async Task Run(string input)
        {
            string json = await RequestCanonicalAsync(input);
            if (!IsJsonLikely(json)) return;
            if (SendToRouter(json)) return;
            // Fallback: copy to clipboard only
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

        // Hook into your client if present; otherwise return "" (no echo).
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
            catch (Exception ex)
            {
                Debug.LogWarning("[Aim2Pro] OpenAI reflection fallback: " + ex.Message);
            }
            await Task.Delay(150);
            return "";
        }
    }
}
#endif
CS

echo "Runner ready at: $RUNNER"
echo "✅ Router-only mode applied. Refocus Unity to recompile."
