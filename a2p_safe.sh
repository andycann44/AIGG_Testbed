#!/usr/bin/env bash
# If invoked from zsh by mistake, re-exec in bash:
if [ -n "${ZSH_VERSION:-}" ]; then exec /bin/bash "$0" "$@"; fi
export LC_ALL=C LANG=C
set -Eeuo pipefail
trap 'echo "❌ $(basename "$0"):$LINENO: $BASH_COMMAND (exit $?)"' ERR

if [ $# -lt 1 ]; then
  echo "Usage: ./a2p_safe.sh <script.sh> [args...]"
  exit 2
fi
script="$1"; shift
if [ ! -f "$script" ]; then echo "❌ Not found: $script"; exit 1; fi
echo "→ running with bash: $script $*"
bash "$script" "$@"
