#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/DynamicKillzone_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/WorkbenchWindow.cs.bak"
echo "Backup: $BK/WorkbenchWindow.cs.bak"

python3 - "$F" <<'PY'
import re, sys
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()

# 1) Add a small safety margin constant inside the class (once)
s = re.sub(
    r'(const string PrefAutoPatch[^\n]*\n)',
    r'\1    static readonly float KillzoneSafetyMargin = 1.0f;\n',
    s, count=1)

# 2) Extend BuildSimpleTrackJson signature to accept killzone override
s = re.sub(
    r'(List<CurveSpec>\s*curves\s*=\s*null\))',
    r'\1',
    s, count=1)  # ensure we match the spot
s = re.sub(
    r'List<CurveSpec>\s*curves\s*=\s*null\)',
    r'List<CurveSpec> curves = null, float? killzoneY = null)',
    s, count=1)

# 3) Replace the fixed killzone with conditional output
s = s.replace(
    'sb.Append("    \\"killzoneY\\": -5\\n");',
    'if (killzoneY.HasValue)\n'
    '        sb.Append($"    \\"killzoneY\\": {killzoneY.Value.ToString(CultureInfo.InvariantCulture)}\\n");\n'
    '      else\n'
    '        sb.Append("    \\"killzoneY\\": -5\\n");'
)

# 4) In TryConvertNLToCanonical: compute killOverride from phrases and pass it through
# Insert killOverride computation before curves list creation
s = re.sub(
    r'(var\s+curves\s*=\s*new\s+List<CurveSpec>\(\)\s*;)',
    (
      'float? killOverride = null;\n'
      '      try {\n'
      '        float maxDepth = 0f;\n'
      '        var m1 = Regex.Match(s, @"\\b(?:under(?:\\s+the)?\\s+track|going\\s+under(?:\\s+the)?\\s+track).*?(?:clearance|clearence)\\s*(?:of\\s*)?(\\d+(?:\\.\\d+)?)\\s*m");\n'
      '        if (m1.Success) maxDepth = Math.Max(maxDepth, float.Parse(m1.Groups[1].Value, CultureInfo.InvariantCulture));\n'
      '        var m2 = Regex.Match(s, @"\\b(?:below|under)\\s+(?:the\\s+bottom\\s+of\\s+the\\s+track|the\\s+track|track|surface)[^0-9]{0,20}?(\\d+(?:\\.\\d+)?)\\s*m");\n'
      '        if (m2.Success) maxDepth = Math.Max(maxDepth, float.Parse(m2.Groups[1].Value, CultureInfo.InvariantCulture));\n'
      '        var m3 = Regex.Match(s, @"\\bslop(?:e|ing)\\s+(?:down|downwards?)\\s*(\\d+(?:\\.\\d+)?)\\s*m");\n'
      '        if (m3.Success) maxDepth = Math.Max(maxDepth, float.Parse(m3.Groups[1].Value, CultureInfo.InvariantCulture));\n'
      '        if (maxDepth > 0f) killOverride = -(maxDepth + KillzoneSafetyMargin);\n'
      '      } catch {}\n'
      r'\1'
    ),
    s, count=1, flags=re.DOTALL)

# Pass killOverride when returning JSON
s = s.replace(
    'return BuildSimpleTrackJson(length, width, missingChance, gapChance, curves);',
    'return BuildSimpleTrackJson(length, width, missingChance, gapChance, curves, killOverride);'
)

# 5) Coverage: treat those phrases as "covered", so residual gate won’t block output
coverage_block = (
    '      // under/clearance/below/slope phrases — mark as covered when present\n'
    '      if (Regex.IsMatch(norm, @"\\b(?:under(?:\\s+the)?\\s+track|going\\s+under(?:\\s+the)?\\s+track).*?(?:clearance|clearence)\\s*(?:of\\s*)?\\d+(?:\\.\\d+)?\\s*m"))\n'
    '        covered.UnionWith(new[]{"under","clearance","clearence","track"});\n'
    '      if (Regex.IsMatch(norm, @"\\b(?:below|under)\\s+(?:the\\s+bottom\\s+of\\s+the\\s+track|the\\s+track|track|surface)[^0-9]{0,20}?\\d+(?:\\.\\d+)?\\s*m"))\n'
    '        covered.UnionWith(new[]{"below","under","bottom","track","surface"});\n'
    '      if (Regex.IsMatch(norm, @"\\bslop(?:e|ing)\\s+(?:down|downwards?)\\s*\\d+(?:\\.\\d+)?\\s*m"))\n'
    '        covered.UnionWith(new[]{"slope","sloping","down","downwards"});\n'
)

s = re.sub(
    r'(// tiles missing / gaps %[\s\S]*?if\s*\(Regex\.IsMatch\(norm,.*?probability.*?\)\)\s*covered\.UnionWith.*?;\s*)',
    r'\1' + coverage_block,
    s, count=1, flags=re.DOTALL)

open(p,'w',encoding='utf-8').write(s)
print("Dynamic killzone + coverage injected.")
PY

touch "$F"
echo "Patched. Switch to Unity and let it recompile."
