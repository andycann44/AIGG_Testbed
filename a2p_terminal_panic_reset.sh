#!/bin/bash
: "${HISTTIMEFORMAT:=}"
set -euo pipefail
[ -f ./a2p_bash_compat.sh ] && source ./a2p_bash_compat.sh || true
[ -f /tmp/a2p_env.sh ] && source /tmp/a2p_env.sh || true

ts="$(date +%Y%m%d_%H%M%S)"

# 0) Write/refresh tiny compat shim (polyfills; safe to re-run)
cat > /tmp/a2p_bash_compat.sh <<'SH2'
#!/bin/bash
: "${HISTTIMEFORMAT:=}"; set -euo pipefail
# Polyfill mapfile for ancient macOS bash (3.2)
if ! type mapfile >/dev/null 2>&1; then
  mapfile() { # usage: mapfile -t VAR
    local _opt="$1" _var="$2"; shift 2 || true
    local _arr=(); while IFS= read -r _line; do _arr+=("$_line"); done
    eval "$_var=(\"\${_arr[@]}\")"
  }
fi
# realpath fallback
if ! type realpath >/dev/null 2>&1; then
  realpath() { python3 - "$1" <<'PY'
import os,sys; print(os.path.abspath(sys.argv[1]))
PY
  }
fi
# quote normalizer (ASCII only)
a2p_ascii() { python3 - <<'PY' "$@"
import sys; 
for s in sys.argv[1:]:
    sys.stdout.write(s.encode("ascii","ignore").decode()+"\n")
PY
}
SH2
chmod +x /tmp/a2p_bash_compat.sh

# 1) Backup risky startup files then write minimal, safe ones
for f in "$HOME/.bashrc" "$HOME/.bash_profile" "$HOME/.profile"; do
  [ -f "$f" ] && cp -f "$f" "$f.$ts.bak" || true
done

cat > "$HOME/.bashrc" <<'RC'
# AIGG_SAFE_RC (minimal)
export SHELL=/bin/bash
export BASH_SILENCE_DEPRECATION_WARNING=1
[ -d /opt/homebrew/bin ] && PATH="/opt/homebrew/bin:$PATH"
[ -d /usr/local/bin ] && PATH="/usr/local/bin:$PATH"
unalias -a 2>/dev/null || true
source /tmp/a2p_bash_compat.sh
[ -f /tmp/a2p_env.sh ] && source /tmp/a2p_env.sh
PS1="\u@\h \W \$ "
RC

cat > "$HOME/.bash_profile" <<'RC'
# AIGG_SAFE_PROFILE
[ -f "$HOME/.bashrc" ] && source "$HOME/.bashrc"
RC

[ -f /tmp/a2p_env.sh ] || printf '%s\n' '#!/bin/bash' > /tmp/a2p_env.sh

# 2) Panic-safe launcher that ignores all profiles, then opts-in
cat > /tmp/a2p_safe_bash.sh <<'SH3'
#!/bin/bash
exec /bin/bash --noprofile --norc -ic 'source /tmp/a2p_bash_compat.sh; [ -f ~/.bashrc ] && source ~/.bashrc; echo "SAFE BASH READY"; echo "$0"; echo "${BASH_VERSION:-unknown}"'
SH3
chmod +x /tmp/a2p_safe_bash.sh

# 3) Force VS Code terminals to bash (Stable + Insiders)
merge_settings_py='
import json, os, sys
p=sys.argv[1]; os.makedirs(os.path.dirname(p), exist_ok=True)
data={}
if os.path.isfile(p):
  try: data=json.load(open(p,"r",encoding="utf-8"))
  except: data={}
data.setdefault("terminal.integrated.profiles.osx",{})
data["terminal.integrated.profiles.osx"]["bash"]={"path":"/bin/bash"}
data["terminal.integrated.defaultProfile.osx"]="bash"
data["terminal.integrated.enableMultiLinePasteWarning"]=False
json.dump(data, open(p,"w",encoding="utf-8"), indent=2); print("OK:",p)
'
fix_settings() {
  local base="$1"
  local settings="$base/User/settings.json"
  python3 - <<PY "$settings"
$merge_settings_py
PY
}
fix_settings "$HOME/Library/Application Support/Code"
fix_settings "$HOME/Library/Application Support/Code - Insiders"

echo
echo "=== NEXT STEPS ==="
echo "1) Close ALL terminals (trash icon in VS Code)."
echo "2) Launch a panic-safe shell:"
echo "   /tmp/a2p_safe_bash.sh"
echo "   (Expect: SAFE BASH READY + bash version.)"
echo "3) Health check:"
echo '   echo "$0"; echo "$BASH_VERSION"; type mapfile || echo "mapfile polyfilled"'
echo
echo "Backups created (if existed):"
for f in "$HOME/.bashrc" "$HOME/.bash_profile" "$HOME/.profile"; do
  [ -f "$f.$ts.bak" ] && echo " - $f.$ts.bak" || true
done
echo "If it still crashes: run ->  /bin/bash --noprofile --norc"
echo "✅ Panic reset staged."
