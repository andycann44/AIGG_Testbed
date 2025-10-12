#!/usr/bin/env bash
set -euo pipefail

PW="Assets/AIGG/Editor/SpecPasteMergeWindow.cs"
[[ -f "$PW" ]] || { echo "Can't find $PW"; exit 1; }

# 1) Backup the current (broken) file
NOWBK="Assets/AIGG/_Backups/SpecPasteMergeWindow_BROKEN_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$NOWBK"
cp "$PW" "$NOWBK/SpecPasteMergeWindow.cs.bak"
echo "Saved broken copy to: $NOWBK/SpecPasteMergeWindow.cs.bak"

# 2) Find the most recent prior backup and restore it
mapfile -t CANDIDATES < <(ls -t Assets/AIGG/_Backups/*/SpecPasteMergeWindow.cs.bak 2>/dev/null || true)
if [[ ${#CANDIDATES[@]} -eq 0 ]]; then
  echo "No previous SpecPasteMergeWindow.cs.bak found under Assets/AIGG/_Backups."
  echo "Aborting to avoid making it worse."
  exit 1
fi
RESTORE="${CANDIDATES[0]}"
cp "$RESTORE" "$PW"
echo "Restored: $RESTORE -> $PW"

# 3) Add ONLY a safe outer scroll view (no MinHeight, no IsNullOrEmpty changes)
python3 - "$PW" <<'PY'
import sys, re
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()
orig=s

# Ensure a field exists for the scroll position
if "Vector2 _outerScroll" not in s:
    s=re.sub(r"(class\s+SpecPasteMergeWindow[^{]*\{)",
             r"\1\n    private UnityEngine.Vector2 _outerScroll = UnityEngine.Vector2.zero;\n",
             s, count=1)

# Wrap OnGUI() body with a single Begin/EndScrollView if not present
m=re.search(r"\bvoid\s+OnGUI\s*\(\s*\)\s*\{", s)
if m:
    start=m.end()
    depth=1; i=start
    while i<len(s) and depth:
        if s[i]=='{': depth+=1
        elif s[i]=='}': depth-=1
        i+=1
    end=i
    body=s[start:end-1]
    if "BeginScrollView(_outerScroll)" not in body:
        wrapped=('\n      _outerScroll = EditorGUILayout.BeginScrollView(_outerScroll);\n'
                 '      try {\n' + body +
                 '\n      } finally {\n        EditorGUILayout.EndScrollView();\n      }\n')
        s=s[:start]+wrapped+s[end-1:]

if s!=orig:
    open(p,'w',encoding='utf-8').write(s)
    print("Applied safe outer scroll to SpecPasteMergeWindow.")
else:
    print("SpecPasteMergeWindow already had scroll or could not patch OnGUI.")
PY

touch "$PW"
echo "Done. Let Unity recompile."
