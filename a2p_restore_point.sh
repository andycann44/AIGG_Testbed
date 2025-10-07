#!/usr/bin/env bash
# Aim2Pro Restore Point
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$here"
BACKUPS_DIR="${BACKUPS_DIR:-_Backups}"
KEEP="${KEEP:-20}"
STAMP="$(date +"%Y%m%d_%H%M%S")"
ZIP="${BACKUPS_DIR}/SDv1-${STAMP}.zip"
mkdir -p "$BACKUPS_DIR"
if [[ ! -d "Assets" || ! -d "ProjectSettings" ]]; then
  echo "Run from Unity project root (must contain Assets/ and ProjectSettings/)" >&2
  exit 1
fi
zip -rq "$ZIP" Assets ProjectSettings Packages/manifest.json
if [[ "${UNPACKED:-0}" == "1" || "${1:-}" == "--unpacked" ]]; then
  FOLDER="${BACKUPS_DIR}/SDv1-${STAMP}"
  mkdir -p "$FOLDER"
  rsync -a --exclude='Library/' --exclude='Temp/' --exclude='Logs/' Assets ProjectSettings "$FOLDER/"
  mkdir -p "$FOLDER/Packages"
  cp -f Packages/manifest.json "$FOLDER/Packages/manifest.json" || true
fi
ls -1t "${BACKUPS_DIR}"/SDv1-*.zip 2>/dev/null | awk "NR>${KEEP}" | while read -r f; do rm -f "$f"; done
echo "Restore point created -> $ZIP"