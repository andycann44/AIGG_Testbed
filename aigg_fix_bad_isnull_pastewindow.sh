#!/usr/bin/env bash
set -euo pipefail
F="Assets/AIGG/Editor/SpecPasteMergeWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/BadIsNullFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"
cp "$F" "$BK/SpecPasteMergeWindow.cs.bak"
echo "Backup: $BK/SpecPasteMergeWindow.cs.bak"

python3 - "$F" <<'PY'
import re, sys
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()
orig=s

# 1) string.IsNullOrEmpty(GUILayout..., X)  -> string.IsNullOrEmpty(X)
s=re.sub(r'(?i)\bstring\.IsNullOrEmpty\s*\(\s*GUILayout\.[^,]*,\s*([^)]+)\)',
         r'string.IsNullOrEmpty(\1)', s)

# 2) string.IsNullOrEmpty(X, GUILayout...)  -> string.IsNullOrEmpty(X)
s=re.sub(r'(?i)\bstring\.IsNullOrEmpty\s*\(\s*([^,]+)\s*,\s*GUILayout\.[^)]*\)',
         r'string.IsNullOrEmpty(\1)', s)

# 3) System.String variants
s=re.sub(r'(?i)\bSystem\.String\.IsNullOrEmpty\s*\(\s*GUILayout\.[^,]*,\s*([^)]+)\)',
         r'System.String.IsNullOrEmpty(\1)', s)
s=re.sub(r'(?i)\bSystem\.String\.IsNullOrEmpty\s*\(\s*([^,]+)\s*,\s*GUILayout\.[^)]*\)',
         r'System.String.IsNullOrEmpty(\1)', s)

# 4) Any leftover IsNullOrEmpty(GUILayout...) -> false (never a string)
s=re.sub(r'(?i)\bstring\.IsNullOrEmpty\s*\(\s*GUILayout\.[^)]*\)', 'false', s)
s=re.sub(r'(?i)\bSystem\.String\.IsNullOrEmpty\s*\(\s*GUILayout\.[^)]*\)', 'false', s)

if s!=orig:
    open(p,'w',encoding='utf-8').write(s)
    print("Fixed bad IsNullOrEmpty() usages that referenced GUILayout options.")
else:
    print("No bad IsNullOrEmpty() usages found.")
PY

touch "$F"
echo "Done. Switch to Unity and let it recompile."
