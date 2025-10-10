#!/bin/zsh
set -e

[[ -d Assets && -d ProjectSettings ]] || { echo "Run from your Unity project root."; exit 1; }

bk="Assets/AIGG/_Backups/AutoFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$bk"

# --- 1) WorkbenchWindow.cs: fix broken multi-line help string ---
f="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
if [[ -f "$f" ]]; then
  cp "$f" "$bk/WorkbenchWindow.cs.bak"
  /usr/bin/ruby - "$f" <<'RUBY'
path=ARGV[0]
s=File.read(path)
before=s.dup
# Replace any 'var help = ...;' (even if it was broken across lines) with a safe verbatim string.
s=s.gsub(/var\s+help\s*=\s*.*?;/m, 'var help = @"AIGG Workbench
— Parse NL (local): runs NLToJson.GenerateFromPrompt(), then AIGG_NLInterpreter.RunToJson() fallback.
— Open Paste & Merge: routes the JSON into your existing SpecPasteMergeWindow.
— Copy skeleton intent: scaffolds an intent for unmatched phrases.
Tip: click Unmatched chips to copy patterns.";')
if s!=before
  File.write(path, s)
  puts "Patched help string in #{path}"
else
  puts "No 'var help = ...;' pattern found in #{path} (skipped)"
end
RUBY
fi

# --- 2) TrackBuilderProV1.cs: switch to Dictionary + fix Add() usages ---
g="Assets/StickerDash/AIGG/Editor/Aigg/TrackBuilderProV1.cs"
if [[ -f "$g" ]]; then
  cp "$g" "$bk/TrackBuilderProV1.cs.bak"
  /usr/bin/ruby - "$g" <<'RUBY'
path=ARGV[0]
s=File.read(path)
changed=false

# If using List<KeyValuePair<...>>, convert constructor to Dictionary<string, object>()
s=s.gsub(/new\s+List<KeyValuePair<string,\s*object>>\s*\(\s*\)/) do
  changed=true
  'new Dictionary<string, object>()'
end

# Convert args.Add("key", value) -> args["key"] = value;
s=s.gsub(/(\b\w+)\.Add\(\s*"([^"]+)"\s*,\s*([^)]+)\)/) do
  changed=true
  "#{$1}[\"#{$2}\"] = #{$3}"
end

if changed
  File.write(path, s)
  puts "Converted to Dictionary and updated Add() calls in #{path}"
else
  puts "No List<KeyValuePair> or string Add() patterns found in #{path} (skipped)"
end
RUBY
fi

echo "Auto-fix complete. Focus Unity to recompile."
