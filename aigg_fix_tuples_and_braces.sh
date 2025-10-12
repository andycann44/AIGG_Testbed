#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
if [[ ! -f "$F" ]]; then
  echo "Can't find $F — run this from your Unity project root."; exit 1
fi

BK="Assets/AIGG/_Backups/TupleFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"
cp "$F" "$BK/WorkbenchWindow.cs.bak"

# Use python3 to patch safely
PY=$(command -v python3 || true)
if [[ -z "${PY}" ]]; then
  echo "python3 not found on PATH."; exit 1
fi

"$PY" - <<'PY' "$F"
import re, sys, io, os
path = sys.argv[1]
s = open(path, 'r', encoding='utf-8').read()

# 1) Ensure CurveSpec class exists (inside the namespace, before WorkbenchWindow)
if "internal class CurveSpec" not in s:
    ns = re.search(r'(namespace\s+Aim2Pro\.AIGG\.Workbench\s*\{\s*)', s)
    if ns:
        insert_at = ns.end()
        cls = (
            "\n  internal class CurveSpec {\n"
            "    public string side;\n"
            "    public float degrees;\n"
            "    public float? rows;\n"
            "    public CurveSpec(string side, float degrees, float? rows) {\n"
            "      this.side = side; this.degrees = degrees; this.rows = rows;\n"
            "    }\n"
            "  }\n\n"
        )
        s = s[:insert_at] + cls + s[insert_at:]

# 2) Replace any tuple-typed List<> with List<CurveSpec>
#    e.g. List<(string side, float degrees, float? rows)>  -> List<CurveSpec>
s = re.sub(r'List<\s*\([^)]+?\)\s*>', 'List<CurveSpec>', s)

# new List<(...)>()  -> new List<CurveSpec>()
s = re.sub(r'new\s+List<\s*\([^)]+?\)\s*>\s*\(\s*\)', 'new List<CurveSpec>()', s)

# Method parameters that might still carry tuple lists (belt & braces)
s = re.sub(r'(<\s*)\([^)]+?\)(\s*>)', r'\1CurveSpec\2', s)

# 3) curves.Add((side, deg, rows))  -> curves.Add(new CurveSpec(side, deg, rows))
s = re.sub(r'curves\.Add\(\(\s*([^)]+?)\s*\)\)', r'curves.Add(new CurveSpec(\1))', s)

# 4) Ensure BuildSimpleTrackJson signature uses List<CurveSpec>
# (if function present, this keeps the parameter typed correctly)
s = re.sub(
    r'(BuildSimpleTrackJson\s*\([\s\S]*?)(List<)\s*CurveSpec(\s*>\s*curves\s*=\s*null)',
    r'\1\2CurveSpec\3',
    s
)

# 5) Ensure file ends cleanly with balanced braces before #endif
#    - add missing closing '}' before the final #endif if needed
endif_idx = s.rfind("#endif")
tail = ""
if endif_idx != -1:
    head = s[:endif_idx]
    tail = s[endif_idx:]
else:
    head = s

# naive brace count (good enough here)
opens = head.count("{")
closes = head.count("}")
missing = max(0, opens - closes)
if missing:
    head = head.rstrip() + ("\n" + "}" * missing) + "\n"

# Keep only whitespace after #endif
if tail:
    # strip anything after #endif except newline
    tail = "#endif\n"

s = head + tail

open(path, 'w', encoding='utf-8').write(s)
print("Patched tuples → CurveSpec, fixed Add calls, and balanced braces.")
PY

# Touch to trigger Unity recompile
touch "$F"
echo "Done. Backup at: $BK/WorkbenchWindow.cs.bak"
