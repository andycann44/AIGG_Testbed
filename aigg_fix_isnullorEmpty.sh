#!/usr/bin/env bash
set -euo pipefail
F="Assets/AIGG/Editor/SpecPasteMergeWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/IsNullOrEmptyFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/SpecPasteMergeWindow.cs.bak"
echo "Backup: $BK/SpecPasteMergeWindow.cs.bak"

python3 - "$F" <<'PY'
import re, sys
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()

# Replace string.IsNullOrEmpty(x, y) or String.IsNullOrEmpty(x, y) with
# (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y))
pat=re.compile(r'(?i)\bstring\.IsNullOrEmpty\s*\(\s*([^,()]+?)\s*,\s*([^,()]+?)\s*\)')
s=pat.sub(r'(string.IsNullOrEmpty(\1) || string.IsNullOrEmpty(\2))', s)

open(p,'w',encoding='utf-8').write(s)
print("Patched two-arg IsNullOrEmpty usages.")
PY

touch "$F"
echo "Done. Switch back to Unity and let it recompile."
