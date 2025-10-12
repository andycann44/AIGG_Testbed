#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/CriticalResidual_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/WorkbenchWindow.cs.bak"
echo "Backup: $BK/WorkbenchWindow.cs.bak"

python3 - "$F" <<'PY'
import re, sys
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()

# Insert a force-residual block just before: return residual.Distinct().ToArray();
pattern = r'(static\s+string\[\]\s+GetResidualMeaningfulTokens\s*\(\s*string\s+norm\s*\)\s*\{[\s\S]*?)\breturn\s+residual\.Distinct\(\)\.ToArray\(\)\s*;'
m = re.search(pattern, s)
if not m:
    print("Could not find GetResidualMeaningfulTokens() return; no changes made.")
    sys.exit(1)

insertion = r'''
      // Force certain semantics to remain "residual" until we explicitly implement them.
      // This ensures JSON stays EMPTY even if the words are present in the lexicon.
      var toksAll = Tokenize(norm).ToArray();
      var tokset  = new HashSet<string>(toksAll);
      string[] critical = { "under","below","beneath","bottom","sloping","slope","clearance","clearence","underpass","tunnel","overpass" };
      foreach (var ct in critical)
      {
        if (tokset.Contains(ct))
        {
          if (!residual.Contains(ct)) residual.Add(ct);
        }
      }
'''
start = m.start(0)
end   = m.end(0)
body  = m.group(0)
body2 = re.sub(r'\breturn\s+residual\.Distinct\(\)\.ToArray\(\)\s*;', insertion + r'\n      return residual.Distinct().ToArray();', body, count=1)
s = s[:start] + body2 + s[end:]

open(p,'w',encoding='utf-8').write(s)
print("Injected critical semantics → residual gate.")
PY

touch "$F"
echo "Patched. Go back to Unity and let it recompile."
