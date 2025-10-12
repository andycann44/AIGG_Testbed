#!/bin/bash
set -euo pipefail

DETAIL_H="${DETAIL_H:-80}"   # second box height (Duplicate / Error Detail)
PASTE_H="${PASTE_H:-}"       # optional override for paste box (default ~180)
PREVIEW_H="${PREVIEW_H:-}"   # optional override for preview box (default ~160)

# 1) locate file
FILE="$(find Assets -name SpecPasteMergeWindow.cs -print -quit)"
if [[ -z "${FILE}" ]]; then
  echo "❌ SpecPasteMergeWindow.cs not found under Assets/"; exit 1
fi

# 2) backup
ts="$(date +%Y%m%d_%H%M%S)"
bakdir="Assets/AIGG/_Backups/SpecPasteMergeWindow_HEIGHT_${ts}"
mkdir -p "$bakdir"
cp -p "$FILE" "$bakdir/SpecPasteMergeWindow.cs.bak"

# 3) shrink the Detail box (keep scrolling intact)
#    Replace only the 120px MinHeight used for the detail pane.
#    (macOS Perl needs -i '' for in-place edit without backup)
 /usr/bin/perl -0777 -i '' -pe "s/MinHeight\\(\\s*120\\s*\\)/MinHeight(${DETAIL_H})/g" "$FILE"

# 4) optional: adjust Paste/Preview if provided
if [[ -n "${PASTE_H}" ]]; then
  /usr/bin/perl -0777 -i '' -pe "s/MinHeight\\(\\s*180\\s*\\)/MinHeight(${PASTE_H})/g" "$FILE"
fi
if [[ -n "${PREVIEW_H}" ]]; then
  /usr/bin/perl -0777 -i '' -pe "s/MinHeight\\(\\s*160\\s*\\)/MinHeight(${PREVIEW_H})/g" "$FILE"
fi

echo "✅ Patched: $FILE"
echo "   Backup:  $bakdir/SpecPasteMergeWindow.cs.bak"
echo "→ Focus Unity to recompile; the second box is smaller and scrollable."
