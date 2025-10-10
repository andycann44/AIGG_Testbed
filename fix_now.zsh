#!/bin/zsh
set -e

# 1) Ensure we're in a Unity project
[[ -d Assets && -d ProjectSettings ]] || { echo "Run from your Unity project root."; exit 1; }

# 2) Remove archived duplicates that cause CS0111
if [[ -d "Assets/AIGG/_Archive" ]]; then
  echo "Removing Assets/AIGG/_Archive…"
  rm -rf Assets/AIGG/_Archive
fi

# 3) Fix wrong casing: toLowerInvariant -> ToLowerInvariant across Assets
files=($(grep -RIl 'toLowerInvariant' Assets 2>/dev/null || true))
if (( ${#files[@]} > 0 )); then
  for f in $files; do
    sed -i '' 's/toLowerInvariant/ToLowerInvariant/g' "$f"
  done
  echo "Casing fixed in ${#files[@]} file(s)."
else
  echo "No toLowerInvariant occurrences found."
fi

echo "Done. Now open WorkbenchWindow.cs and paste the 'BuildWorkbenchHelp' block from my message."
