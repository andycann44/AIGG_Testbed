#!/usr/bin/env bash
set -euo pipefail

# Locate the SpecPasteMergeWindow C# file
FOUND="$(grep -RIl --include="*.cs" "class SpecPasteMergeWindow" Assets || true)"
if [[ -z "$FOUND" ]]; then
  echo "Couldn't find a file that defines: class SpecPasteMergeWindow"
  exit 1
fi

F="$FOUND"
BK="Assets/AIGG/_Backups/SpecPasteMergeWindow_ScrollFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"
cp "$F" "$BK/SpecPasteMergeWindow.cs.bak"
echo "Backup: $BK/SpecPasteMergeWindow.cs.bak"
echo "Patching: $F"

python3 - "$F" <<'PY'
import sys, re
p = sys.argv[1]
s = open(p,'r',encoding='utf-8').read()

# 1) Ensure a Vector2 scroll field exists
if "Vector2 _outerScroll" not in s:
    s = re.sub(r"(class\s+SpecPasteMergeWindow[^{]*\{)",
               r"\1\n    private UnityEngine.Vector2 _outerScroll = UnityEngine.Vector2.zero;\n",
               s, count=1)

# 2) Wrap the body of void OnGUI() with a Begin/EndScrollView
m = re.search(r"\bvoid\s+OnGUI\s*\(\s*\)\s*\{", s)
if not m:
    print("Could not find void OnGUI()"); sys.exit(1)

start = m.end()
# find matching closing brace for OnGUI
depth = 1; i = start
while i < len(s) and depth > 0:
    if s[i] == '{': depth += 1
    elif s[i] == '}': depth -= 1
    i += 1
end = i

body = s[start:end-1]  # contents inside OnGUI braces
if "BeginScrollView(_outerScroll)" not in body:
    wrapped = (
        '\n      _outerScroll = EditorGUILayout.BeginScrollView(_outerScroll);\n'
        '      try {\n' + body +
        '\n      } finally {\n        EditorGUILayout.EndScrollView();\n      }\n'
    )
    s = s[:start] + wrapped + s[end-1:]

# 3) (Optional) make the status area smaller by default:
#    try to add a min-height hint to any TextArea that has the "Status" label above it.
s = re.sub(
    r'(EditorGUILayout\.LabelField\(\s*"Status"\s*[,)].*?\n\s*)(var\s+\w+\s*=\s*)?EditorGUILayout\.TextArea\(',
    r'\1EditorGUILayout.TextArea(',
    s, flags=re.DOTALL
)
# add a MinHeight(140) to any TextArea that has zero options
def add_minheight(match):
    inner = match.group(1)
    # if options already present (comma after the last arg and before ");"), keep as-is
    if "GUILayout." in inner:
        return match.group(0)  # already has options
    return f'EditorGUILayout.TextArea({inner}, GUILayout.MinHeight(140))'
s = re.sub(r'EditorGUILayout\.TextArea\(([^)\n]+)\)', add_minheight, s)

open(p,'w',encoding='utf-8').write(s)
print("Patched: outer scroll + default min-height for text areas.")
PY

touch "$F"
echo "Done. Switch back to Unity and let it recompile."
