#!/bin/bash
: "${HISTTIMEFORMAT:=}"; set -euo pipefail
# Polyfill mapfile for old mac bash (3.2) if missing
if ! type mapfile >/dev/null 2>&1; then
  mapfile() { # mapfile -t VAR
    local _opt="$1" _var="$2"; shift 2 || true
    local _arr=()
    while IFS= read -r _line; do _arr+=("$_line"); done
    eval "$_var=(\"\${_arr[@]}\")"
  }
fi
# realpath fallback
if ! type realpath >/dev/null 2>&1; then
  realpath() { python3 - <<'PY' "$1"
import os,sys; print(os.path.abspath(sys.argv[1]))
PY
  }
fi
