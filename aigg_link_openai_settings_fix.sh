#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/OpenAISettingsLinkFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/WorkbenchWindow.cs.bak"
echo "Backup: $BK/WorkbenchWindow.cs.bak"

python3 - "$F" <<'PY'
import re, sys
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()

# 1) Replace RunScript(...) with env support
s = re.sub(
    r'static\s+int\s+RunScript\s*\(\s*string\s+scriptPath,\s*string\s+args,\s*out\s+string\s+output,\s*out\s+string\s+error\s*\)\s*\{[\s\S]*?\}',
    r'''static int RunScript(string scriptPath, string args, out string output, out string error, System.Collections.Generic.Dictionary<string,string> env = null)
{
  output = string.Empty;
  error  = string.Empty;
  if (!File.Exists(scriptPath)) { error = "Script not found: " + scriptPath; return -1; }
  try
  {
    using (var p = new Process())
    {
      var psi = new ProcessStartInfo("/bin/zsh", $"\"{scriptPath}\" {args}");
      psi.UseShellExecute = false;
      psi.RedirectStandardOutput = true;
      psi.RedirectStandardError  = true;
      psi.WorkingDirectory       = ProjectRoot;
      if (env != null)
      {
        foreach (var kv in env)
        {
          try { psi.EnvironmentVariables[kv.Key] = kv.Value ?? ""; } catch { /* mono safe */ }
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
}''',
    s, count=1, flags=re.DOTALL)

# 2) Replace SelfHealOpenAI to read AIGGSettings and pass env vars
s = re.sub(
    r'int\s+SelfHealOpenAI\s*\(\s*string\s+nl\s*\)\s*\{[\s\S]*?\}',
    r'''int SelfHealOpenAI(string nl)
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
    _diag = (o + (string.IsNullOrEmpty(e) ? "" : "\n" + e)).Trim() + "\n" + _diag;

    if (ec == 0)
    {
      AssetDatabase.Refresh();
      var aiDir = Path.Combine(SpecDir, "_AI");
      if (Directory.Exists(aiDir))
      {
        var patch = Directory.GetFiles(aiDir, "patch_*.json").OrderByDescending(f => f).FirstOrDefault();
        if (!string.IsNullOrEmpty(patch))
        {
          _json = File.ReadAllText(patch);
          OpenPasteAndMerge();
        }
      }
    }
    return ec;
  }
  finally { try { File.Delete(tmp); } catch { } }
}''',
    s, count=1, flags=re.DOTALL)

# 3) Replace the OpenAI button block to use settings (correctly pass the 'string' arg this time)
pat = re.compile(
    r'if\s*\(\s*GUILayout\.Button\("Self-heal\s*\(OpenAI\)".*?\)\s*\)\s*\{[\s\S]*?\}\s*GUI\.enabled\s*=\s*true\s*;',
    re.DOTALL)
repl = '''if (GUILayout.Button("Self-heal (OpenAI)", GUILayout.Height(22)))
{
  bool ok = false;
  try { var st = global::Aim2Pro.AIGG.AIGGSettings.LoadOrCreate(); ok = st && !string.IsNullOrEmpty(st.openAIKey); } catch { ok = false; }
  if (!ok) EditorUtility.DisplayDialog("OpenAI key missing", "Set the key in Window → Aim2Pro → Settings → API Settings.", "OK");
  var ec = ok ? SelfHealOpenAI(_nl) : -1;
  if (ec == 0) ShowNotification(new GUIContent("AI patch created → Paste & Merge opened"));
}
GUI.enabled = true;'''
s = pat.sub(repl, s, count=1)

open(p,'w',encoding='utf-8').write(s)
print("Fixed: OpenAI button uses AIGGSettings; RunScript env; corrected re.sub usage.")
PY

touch "$F"
echo "Done. Switch back to Unity to recompile."
