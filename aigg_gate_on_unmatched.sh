#!/usr/bin/env bash
set -euo pipefail
F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/GateOnUnmatched_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/WorkbenchWindow.cs.bak"
echo "Backup: $BK/WorkbenchWindow.cs.bak"

python3 - "$F" <<'PY'
import re, sys
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()

# Insert a hard gate right after we capture _lastUnmatched in ParseLocal()
pat = r'(_lastUnmatched\s*=\s*unmatched\.ToList\(\);\s*)'
gate = r"""\1

        // HARD GATE: if any unmatched remain, clear output and stop here.
        if (unmatched.Length > 0)
        {
          // Optional: auto-patch and schedule a reparse, but still suppress output on THIS pass.
          if (_autoPatch && !_isReparsing)
          {
            int added = ApplyLexiconPatch(unmatched);
            if (added > 0)
            {
              AssetDatabase.SaveAssets();
              AssetDatabase.Refresh();
              _isReparsing = true;
              EditorApplication.delayCall += () => { try { ParseLocal(); } finally { _isReparsing = false; } };
            }
          }
          _json = "";
          ShowNotification(new GUIContent("Unmatched tokens → output EMPTY"));
          // Diagnostics builder below will list the unmatched; we bail before any JSON is produced.
          return;
        }
"""

s_new = re.sub(pat, gate, s, count=1)
if s_new == s:
  print("WARN: Could not find the _lastUnmatched capture line; no change made.")
else:
  open(p,'w',encoding='utf-8').write(s_new)
  print("Inserted hard gate on unmatched inside ParseLocal().")
PY

# prod a recompile
touch "$F"
echo "Done. Switch to Unity and click Parse NL again."
