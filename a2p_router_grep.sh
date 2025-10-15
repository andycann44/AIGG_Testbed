#!/bin/bash
: "${HISTTIMEFORMAT:=}"; set -euo pipefail
echo "[grep] Searching for legacy Pre-Merge callers and dialogs..."
grep -RIn --binary-files=without-match \
  -e "Router window not found" \
  -e "PreMergeWindow" \
  -e "PreMergeCenterWindow" \
  -e "PreMergeRouterWindow" \
  -e "SpecPasteMergeWindow" \
  Assets || true
echo "[grep] Done."
