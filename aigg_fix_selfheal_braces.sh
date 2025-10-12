#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/SelfHealBraceFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/WorkbenchWindow.cs.bak"
echo "Backup: $BK/WorkbenchWindow.cs.bak"

python3 - "$F" <<'PY'
import sys, re, io, os
path=sys.argv[1]
s=open(path,'r',encoding='utf-8').read()

def replace_method(source, signature, body):
    i = source.find(signature)
    if i == -1:
        return None
    # find opening brace of method
    j = source.find("{", i)
    if j == -1:
        return None
    # balance braces to find end of method
    depth = 0
    end = None
    for k in range(j, len(source)):
        c = source[k]
        if c == "{": depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                end = k + 1
                break
    if end is None:
        return None
    return source[:i] + body + source[end:]

# Ensure RunScript has env support (optional upgrade)
m = re.search(r'static\s+int\s+RunScript\s*\(\s*string\s+scriptPath,\s*string\s+args,\s*out\s+string\s+output,\s*out\s+string\s+error(?:\s*,\s*System\.Collections\.Generic\.Dictionary<string,string>\s+env\s*=\s*null)?\)\s*\{', s)
if not m or "EnvironmentVariables" not in s[m.start():m.end()+800]:
    # replace entire RunScript block (best effort)
    rs = re.search(r'static\s+int\s+RunScript\s*\([\s\S]*?\)\s*\{[\s\S]*?\n\}', s)
    if rs:
        runscript = r'''static int RunScript(string scriptPath, string args, out string output, out string error, System.Collections.Generic.Dictionary<string,string> env = null)
{
  output = string.Empty;
  error  = string.Empty;
  if (!File.Exists(scriptPath)) { error = "Script not found: " + scriptPath; return -1; }
  try
  {
    using (var p = new System.Diagnostics.Process())
    {
      var psi = new System.Diagnostics.ProcessStartInfo("/bin/zsh", $"\"{scriptPath}\" {args}");
      psi.UseShellExecute = false;
      psi.RedirectStandardOutput = true;
      psi.RedirectStandardError  = true;
      psi.WorkingDirectory       = ProjectRoot;
      if (env != null)
      {
        foreach (var kv in env)
        {
          try { psi.EnvironmentVariables[kv.Key] = kv.Value ?? ""; } catch { }
        }
      }
      p.StartInfo = psi;
      p.Start();
      string o = p.StandardOutput.ReadToEnd();
      string e = p.StandardError.ReadToEnd();
      p.WaitForExit();
      int code = p.ExitCode;
      output = o; error = e;
      return code;
    }
  }
  catch (Exception ex)
  {
    error = ex.GetType().Name + ": " + ex.Message;
    output = string.Empty;
    return -1;
  }
}'''
        s = s[:rs.start()] + runscript + s[rs.end():]

# Replace SelfHealOpenAI (full method)
good = r'''int SelfHealOpenAI(string nl)
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

    // Log transcript to Diagnostics
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
  finally
  {
    try { File.Delete(tmp); } catch { }
  }
}
'''
replaced = replace_method(s, "int SelfHealOpenAI(string nl)", good)
if replaced is None:
    print("SelfHealOpenAI() signature not found; no changes made.")
    sys.exit(1)
s = replaced

# Balance braces before #endif (if needed)
endif_idx = s.rfind("#endif")
if endif_idx != -1:
    head = s[:endif_idx]
    tail = s[endif_idx:]
else:
    head = s
    tail = ""

opens = head.count("{")
closes = head.count("}")
missing = max(0, opens - closes)
if missing:
    head = head.rstrip() + ("\n" + "}" * missing) + "\n"

if tail and not tail.endswith("\n"):
    tail += "\n"

open(path,'w',encoding='utf-8').write(head + tail)
print("SelfHealOpenAI replaced and braces balanced.")
PY

touch "$F"
echo "Done. Recompile in Unity and try Self-heal (OpenAI) again."
