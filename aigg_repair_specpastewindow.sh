#!/usr/bin/env bash
set -euo pipefail
F="Assets/AIGG/Editor/SpecPasteMergeWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/SpecPasteMergeWindow_Repair_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"
cp "$F" "$BK/SpecPasteMergeWindow.cs.bak"
echo "Backup: $BK/SpecPasteMergeWindow.cs.bak"

python3 - "$F" <<'PY'
import re, sys

p = sys.argv[1]
s = open(p,'r',encoding='utf-8').read()
orig = s

# 1) Remove any MinHeight we may have injected earlier (keep code simple & compile-safe)
s = s.replace(", GUILayout.MinHeight(140)", "")
s = s.replace(",GUILayout.MinHeight(140)", "")

# 2) Normalize any accidental double commas left behind (just in case)
s = re.sub(r',\s*,', ',', s)

# 3) Fix any IsNullOrEmpty with 2 args → split into OR of two single checks
def fix_two_arg_isnull(text):
    out = []
    i = 0
    pat = re.compile(r'(?i)(?:\bstring|\bSystem\.String)\.IsNullOrEmpty\s*\(')
    while True:
        m = pat.search(text, i)
        if not m:
            out.append(text[i:])
            break
        out.append(text[i:m.start()])
        # find matching ')'
        j = text.find('(', m.start())
        depth, k = 1, j+1
        while k < len(text) and depth:
            c = text[k]
            if c == '(':
                depth += 1
            elif c == ')':
                depth -= 1
            k += 1
        args = text[j+1:k-1]

        # split args at top-level commas
        parts, buf, d = [], "", 0
        for ch in args:
            if ch == '(':
                d += 1
            elif ch == ')':
                d -= 1
            if ch == ',' and d == 0:
                parts.append(buf.strip()); buf = ""
            else:
                buf += ch
        if buf.strip():
            parts.append(buf.strip())

        if len(parts) == 2:
            a, b = parts
            out.append(f'(string.IsNullOrEmpty({a}) || string.IsNullOrEmpty({b}))')
        else:
            # leave as-is (1 arg is fine)
            out.append(text[m.start():k])
        i = k
    return ''.join(out)

s = fix_two_arg_isnull(s)

# 4) If any IsNullOrEmpty accidentally references GUILayout.* as its SOLE arg, replace with false
s = re.sub(r'(?i)\b(?:string|System\.String)\.IsNullOrEmpty\s*\(\s*GUILayout\.[^)]*\)', 'false', s)

# 5) Quick sanity: remove stray ", )" or "( ,"
s = s.replace(", )", ")").replace("( ,", "(")

if s != orig:
    open(p,'w',encoding='utf-8').write(s)
    print("SpecPasteMergeWindow repaired (MinHeight removed, IsNullOrEmpty sanitized).")
else:
    print("No changes were necessary.")
PY

touch "$F"
echo "Done. Let Unity recompile."
