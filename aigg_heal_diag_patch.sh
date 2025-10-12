#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/HealDiag_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/WorkbenchWindow.cs.bak"
echo "Backup: $BK/WorkbenchWindow.cs.bak"

python3 - "$F" <<'PY'
import re, sys
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()

# Patch SelfHealOpenAI to:
# - capture exit code, stdout, stderr into Diagnostics
# - parse 'WROTE_PATCH:' from stdout
# - fallback scan aiDir and open Finder if none
# - clearer notifications
pattern=r'int\s+SelfHealOpenAI\s*\(\s*string\s+nl\s*\)\s*\{[\s\S]*?\}\s*\n'
m=re.search(pattern,s)
if not m:
  print("Could not find SelfHealOpenAI()"); sys.exit(1)

new_body=r'''int SelfHealOpenAI(string nl)
{
  string tmp = Path.Combine(Path.GetTempPath(), "aigg_nl_" + Guid.NewGuid().ToString("N") + ".txt");
  File.WriteAllText(tmp, nl ?? "");
  try
  {
    string key = null, model = "gpt-4o-mini";
    int timeoutSec = 30;
    bool useResponses = false;
    try {
      var st = global::Aim2Pro.AIGG.AIGGSettings.LoadOrCreate();
      if (st) {
        key = st.openAIKey;
        model = string.IsNullOrEmpty(st.model) ? (string.IsNullOrEmpty(st.openAIModel) ? model : st.openAIModel) : st.model;
#if UNITY_2020_1_OR_NEWER
        timeoutSec = Mathf.Max(1, st.timeoutSeconds);
#endif
        useResponses = st.useResponsesBeta;
      }
    } catch { }

    if (string.IsNullOrEmpty(key))
    {
      EditorUtility.DisplayDialog("OpenAI key missing", "Set the key in Window → Aim2Pro → Settings → API Settings.", "OK");
      return -1;
    }

    var env = new Dictionary<string,string> {
      { "OPENAI_API_KEY", key },
      { "OPENAI_MODEL", model },
      { "AIGG_TIMEOUT_SECONDS", timeoutSec.ToString() },
      { "AIGG_USE_RESPONSES", useResponses ? "1" : "0" }
    };

    var ec = RunScript(OpenAIHealScript, $"--file \"{tmp}\" --model \"{model}\"", out var o, out var e, env);

    // Always log a compact transcript to Diagnostics
    var aiDir = Path.Combine(SpecDir, "_AI");
    var log = new StringBuilder();
    log.AppendLine("[Self-heal OpenAI]");
    log.AppendLine(" script: " + OpenAIHealScript);
    log.AppendLine(" exit: " + ec.ToString());
    log.AppendLine(" aiDir: " + aiDir);
    if (!string.IsNullOrEmpty(o)) { log.AppendLine(" stdout:"); log.AppendLine(o.Trim()); }
    if (!string.IsNullOrEmpty(e)) { log.AppendLine(" stderr:"); log.AppendLine(e.Trim()); }
    _diag = log.ToString().TrimEnd() + "\n" + _diag;

    // Prefer explicit file path from stdout
    string wrote = null;
    if (!string.IsNullOrEmpty(o))
    {
      foreach (var line in o.Split('\n'))
        if (line.StartsWith("WROTE_PATCH:"))
          wrote = line.Substring("WROTE_PATCH:".Length).Trim();
    }

    if (!string.IsNullOrEmpty(wrote) && File.Exists(wrote))
    {
      _json = File.ReadAllText(wrote);
      OpenPasteAndMerge();
      ShowNotification(new GUIContent("AI patch created → Paste & Merge opened"));
      return 0;
    }

    // Fallback: refresh and scan the _AI directory
    AssetDatabase.Refresh();
    if (!Directory.Exists(aiDir)) Directory.CreateDirectory(aiDir);
    var patch = Directory.GetFiles(aiDir, "patch_*.json").OrderByDescending(f => f).FirstOrDefault();
    if (!string.IsNullOrEmpty(patch) && File.Exists(patch))
    {
      _json = File.ReadAllText(patch);
      OpenPasteAndMerge();
      ShowNotification(new GUIContent("AI patch found → Paste & Merge opened"));
      return 0;
    }

    // Nothing found; open folder for you and notify
    EditorUtility.RevealInFinder(aiDir);
    ShowNotification(new GUIContent("No patch file found. See Diagnostics for stdout/stderr."));
    return (ec == 0) ? 1 : ec;
  }
  finally { try { File.Delete(tmp); } catch { } }
}
'''
s = s[:m.start()] + new_body + s[m.end():]

open(p,'w',encoding='utf-8').write(s)
print("SelfHealOpenAI() upgraded with robust logging + patch discovery.")
PY

# Ensure script exists + is executable; if present, chmod +x
if [[ -f "aigg_self_heal_openai.zsh" ]]; then
  chmod +x aigg_self_heal_openai.zsh || true
fi

touch "$F"
echo "Patched WorkbenchWindow.cs. Recompile in Unity, then try Self-heal (OpenAI) again."
