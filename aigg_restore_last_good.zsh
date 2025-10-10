#!/bin/zsh
set -e

target1="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
target2="Assets/StickerDash/AIGG/Editor/Aigg/TrackBuilderProV1.cs"

[[ -d Assets && -d ProjectSettings ]] || { echo "Run from your Unity project root."; exit 1; }

mkdir -p Assets/AIGG/_Backups/_Restored

git_restored1=0
git_restored2=0

# 1) Try Git restore from HEAD (if repo + file tracked)
if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  if git ls-files --error-unmatch "$target1" >/dev/null 2>&1 && git show HEAD:"$target1" >/dev/null 2>&1; then
    echo "Git restoring $target1…"
    cp "$target1" "Assets/AIGG/_Backups/_Restored/WorkbenchWindow.cs.current.bak" 2>/dev/null || true
    git restore --source=HEAD -- "$target1" || git checkout -- "$target1"
    git_restored1=1
  fi
  if git ls-files --error-unmatch "$target2" >/dev/null 2>&1 && git show HEAD:"$target2" >/dev/null 2>&1; then
    echo "Git restoring $target2…"
    cp "$target2" "Assets/AIGG/_Backups/_Restored/TrackBuilderProV1.cs.current.bak" 2>/dev/null || true
    git restore --source=HEAD -- "$target2" || git checkout -- "$target2"
    git_restored2=1
  fi
fi

# 2) If Git wasn’t possible, restore from our .bak backups
restore_from_bak () {
  local target="$1"
  local base="$(basename "$target")"
  local bak
  bak=$(find Assets/AIGG/_Backups -type f -name "${base}.bak" -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -n1)
  if [[ -n "$bak" && -f "$bak" ]]; then
    echo "Restoring $target from backup: $bak"
    cp "$target" "Assets/AIGG/_Backups/_Restored/${base}.current.bak" 2>/dev/null || true
    cp "$bak" "$target"
  else
    echo "No backup found for $target — skipped."
  fi
}

[[ "$git_restored1" == "1" ]] || restore_from_bak "$target1"
[[ "$git_restored2" == "1" ]] || restore_from_bak "$target2"

echo
echo "Restore complete. Bring Unity to the front to recompile."
