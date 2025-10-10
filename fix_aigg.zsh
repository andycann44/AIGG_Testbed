#!/bin/zsh
set -e

# Ensure we're in a Unity project
if [[ ! -d Assets || ! -d ProjectSettings ]]; then
  echo "Run this from your Unity project root (must contain Assets/ and ProjectSettings/)." >&2
  exit 1
fi

# 1) Remove archived duplicates that cause CS0111
if [[ -d "Assets/AIGG/_Archive" ]]; then
  echo "Removing Assets/AIGG/_Archive…"
  git rm -r --cached -f Assets/AIGG/_Archive >/dev/null 2>&1 || true
  rm -rf Assets/AIGG/_Archive
fi

# 2) Fix wrong casing: toLowerInvariant -> ToLowerInvariant
# (BSD sed on macOS: -i '')
files=($(grep -RIl 'toLowerInvariant' Assets 2>/dev/null || true))
if (( ${#files[@]} > 0 )); then
  for f in $files; do
    sed -i '' 's/toLowerInvariant/ToLowerInvariant/g' "$f"
  done
  echo "Casing fixed in ${#files[@]} file(s)."
else
  echo "No toLowerInvariant occurrences found."
fi

# 3) Nudge on the unterminated string (CS1010/1003/1026)
echo
echo "Now fix the broken multi-line string around:"
echo "  Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs:490"
echo "Tip: use a verbatim interpolated string:"
echo '  var help = @$"Line one\nLine two with {value}";'
echo
