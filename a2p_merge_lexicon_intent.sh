#!/usr/bin/env bash
set -euo pipefail

# Target files (adjust if yours live elsewhere)
LEX_FILE="${LEX_FILE:-Assets/AIGG/Spec/lexicon.json}"
INT_FILE="${INT_FILE:-Assets/AIGG/Spec/intents.json}"

mkdir -p "$(dirname "$LEX_FILE")" "$(dirname "$INT_FILE")"

TS="$(date +%Y%m%d_%H%M%S)"
BKP="Assets/AIGG/_Backups/SpecMerge_$TS"
mkdir -p "$BKP"
[[ -f "$LEX_FILE" ]] && cp -p "$LEX_FILE" "$BKP/lexicon.json.bak"
[[ -f "$INT_FILE" ]] && cp -p "$INT_FILE" "$BKP/intents.json.bak"

# ---- your payloads ----
LEX_JSON="$(cat <<'JSON'
{
  "synonyms": {
    "100m": "100",
    "20m": "20",
    "10m": "10",
    "left": "left",
    "45 degree curve": "curve_45_left",
    "under the track": "under_track",
    "clearance": "clearance"
  }
}
JSON
)"

INTENTS_JSON="$(cat <<'JSON'
{
  "intents": [
    {
      "name": "generate_track",
      "regex": "^(\\d+)m by (\\d+)m with (left|right) (\\d+) degree curve and (\\d+)m under the track with clearance$",
      "ops": [
        { "op": "set", "path": "$.track.length",    "value": "$1:float" },
        { "op": "set", "path": "$.track.width",     "value": "$2:float" },
        { "op": "set", "path": "$.track.curve",     "value": "curve_$4_$3" },
        { "op": "set", "path": "$.track.clearance", "value": "$5:float" }
      ]
    }
  ]
}
JSON
)"

# ---- merge using python (keeps existing content; replaces by key/name) ----
python3 - <<'PY' "$LEX_FILE" "$INT_FILE" "$LEX_JSON" "$INTENTS_JSON"
import json, sys, os
lex_path, int_path, lex_in, intents_in = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]

def load(path, default):
    try:
        with open(path, 'r', encoding='utf-8') as f: return json.load(f)
    except Exception:
        return default

lex = load(lex_path, {"synonyms": {}})
lex.setdefault("synonyms", {})

intents = load(int_path, {"intents": []})
intents.setdefault("intents", [])

new_lex = json.loads(lex_in)
new_ints = json.loads(intents_in)

# Merge lexicon (Replace policy)
lex["synonyms"].update(new_lex.get("synonyms", {}))

# Merge intents by name (Replace if same name)
by_name = {it.get("name"): i for i,it in enumerate(intents["intents"]) if isinstance(it, dict) and "name" in it}
for it in new_ints.get("intents", []):
    nm = it.get("name")
    if nm in by_name:
        intents["intents"][by_name[nm]] = it
    else:
        intents["intents"].append(it)

os.makedirs(os.path.dirname(lex_path), exist_ok=True)
with open(lex_path, 'w', encoding='utf-8') as f: json.dump(lex, f, indent=2, ensure_ascii=False)

os.makedirs(os.path.dirname(int_path), exist_ok=True)
with open(int_path, 'w', encoding='utf-8') as f: json.dump(intents, f, indent=2, ensure_ascii=False)

print("WROTE", lex_path)
print("WROTE", int_path)
PY

echo "✅ Done. Backups in: $BKP"
echo "   Lexicon → $LEX_FILE"
echo "   Intents → $INT_FILE"
