#!/bin/zsh
set -e

# Ensure Unity project root
[[ -d Assets && -d ProjectSettings ]] || { echo "Run this from your Unity project root."; exit 1; }

# 0) Safety snapshot (excludes heavy/ephemeral dirs)
SNAPDIR="AIGG_Snapshots"
mkdir -p "$SNAPDIR"
SNAP="$SNAPDIR/$(date +%Y%m%d_%H%M%S)_pre_rollback.zip"
echo "Creating safety snapshot: $SNAP"
zip -r -q "$SNAP" . -x "Library/*" "Temp/*" "Logs/*" "obj/*" "Packages/*/node_modules/*"

if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Git repo detected — reverting working tree to HEAD…"
  git restore --staged . >/dev/null 2>&1 || true
  git checkout -- . || true
  echo "✔ Working tree restored to HEAD."

  # Optional: roll back one commit (use: ./rollback_now.zsh --previous)
  if [[ "$1" == "--previous" ]]; then
    echo "Rolling back one commit (HEAD@{1})…"
    git reset --hard HEAD@{1}
    echo "✔ Now at previous commit."
  fi

  # NOTE: If you want a deep clean of untracked build junk, uncomment:
  # git clean -fdX   # removes ignored files (safe-ish)
  # git clean -fd    # removes ALL untracked files (dangerous — be sure!)

else
  echo "No git repo — restoring from local backups…"
  # Restore WorkbenchWindow.cs from newest .bak we created
  t1="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
  bak1=$(find Assets/AIGG/_Backups -type f -name "WorkbenchWindow.cs.bak" -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -n1)
  if [[ -n "$bak1" && -f "$t1" ]]; then
    cp "$t1" "$t1.current.bak" 2>/dev/null || true
    cp "$bak1" "$t1"
    echo "✔ Restored $t1 from $bak1"
  else
    echo "• No backup found for $t1 (skipped)."
  fi

  # Restore TrackBuilderProV1.cs if present
  t2="Assets/StickerDash/AIGG/Editor/Aigg/TrackBuilderProV1.cs"
  if [[ -f "$t2" ]]; then
    bak2=$(find Assets/AIGG/_Backups -type f -name "TrackBuilderProV1.cs.bak" -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -n1)
    if [[ -n "$bak2" ]]; then
      cp "$t2" "$t2.current.bak" 2>/dev/null || true
      cp "$bak2" "$t2"
      echo "✔ Restored $t2 from $bak2"
    else
      echo "• No backup found for $t2 (skipped)."
    fi
  fi

  # Remove J shim we added
  rm -f "Assets/AIGG/Editor/Utils/JShim.cs" && echo "✔ Removed JShim.cs" || true
fi

echo "All done. Bring Unity to the front to recompile."
