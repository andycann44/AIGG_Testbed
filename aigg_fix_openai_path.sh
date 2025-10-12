#!/usr/bin/env bash
set -euo pipefail
F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }
BK="Assets/AIGG/_Backups/OpenAIPathFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/WorkbenchWindow.cs.bak"; echo "Backup: $BK/WorkbenchWindow.cs.bak"

python3 - "$F" <<'PY'
import re, sys
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()

# 1) Robustly load the exact file printed by WROTE_PATCH (handles relative + AssetDatabase)
s=re.sub(
r'''if\s*\(\s*!string\.IsNullOrEmpty\(wrote\)\s*&&\s*File\.Exists\(wrote\)\s*\)\s*\{\s*
\s*_json\s*=\s*File\.ReadAllText\(wrote\);\s*
\s*OpenPasteAndMerge\(\);\s*
\s*ShowNotification\(new\s+GUIContent\("AI patch created → Paste & Merge opened"\)\);\s*
\s*return\s*0;\s*
\s*\}''',
r'''if (!string.IsNullOrEmpty(wrote))
{
  // Try as absolute or relative-to-project path
  var abs = System.IO.Path.IsPathRooted(wrote) ? wrote : System.IO.Path.Combine(ProjectRoot, wrote);
  if (System.IO.File.Exists(abs))
  {
    _json = System.IO.File.ReadAllText(abs);
    OpenPasteAndMerge();
    ShowNotification(new GUIContent("AI patch created → Paste & Merge opened"));
    return 0;
  }
  // Try as a Unity asset path
  var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(wrote);
  if (ta != null && !string.IsNullOrEmpty(ta.text))
  {
    _json = ta.text;
    OpenPasteAndMerge();
    ShowNotification(new GUIContent("AI patch (asset) → Paste & Merge"));
    return 0;
  }
}''',
s, count=1, flags=re.DOTALL)

# 2) Fallback scan: use absolute dir to find recent patch reliably
s=re.sub(
r'''var\s+aiDir\s*=\s*Path\.Combine\(SpecDir,\s*"_AI"\);\s*
\s*var\s+patch\s*=\s*Directory\.GetFiles\(aiDir,\s*"patch_.*?json"\).*?FirstOrDefault\(\);\s*
\s*if\s*\(!string\.IsNullOrEmpty\(patch\)\s*&&\s*File\.Exists\(patch\)\)\s*\{\s*
\s*_json\s*=\s*File\.ReadAllText\(patch\);\s*
\s*OpenPasteAndMerge\(\);\s*
\s*ShowNotification\(new\s+GUIContent\("AI patch found → Paste & Merge opened"\)\);\s*
\s*return\s*0;\s*
\s*\}''',
r'''var aiDir = Path.Combine(SpecDir, "_AI");
var aiDirAbs = System.IO.Path.IsPathRooted(aiDir) ? aiDir : System.IO.Path.Combine(ProjectRoot, aiDir);
var patch = Directory.GetFiles(aiDirAbs, "patch_*.json").OrderByDescending(f => f).FirstOrDefault();
if (!string.IsNullOrEmpty(patch) && File.Exists(patch))
{
  _json = File.ReadAllText(patch);
  OpenPasteAndMerge();
  ShowNotification(new GUIContent("AI patch found → Paste & Merge opened"));
  return 0;
}''',
s, count=1, flags=re.DOTALL)

open(p,'w',encoding='utf-8').write(s)
print("Patched path handling in SelfHealOpenAI.")
PY

touch "$F"
echo "Done. Switch to Unity and let it recompile."
