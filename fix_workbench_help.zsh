#!/bin/zsh
set -e

f="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$f" ]] || { echo "Can't find $f — run from your Unity project root."; exit 1; }

bk="Assets/AIGG/_Backups/AutoFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$bk"
cp "$f" "$bk/WorkbenchWindow.cs.bak"

# Use Ruby (ships on macOS) to:
# 1) Insert a safe multi-line help string field AIGG_HELP once per class.
# 2) In the line range around the reported error (470..520),
#    replace any GUILayout.Label("...<newline>...") with GUILayout.Label(AIGG_HELP);
#    (i.e., only fixes multi-line string literals in that region).
/usr/bin/ruby - "$f" <<'RUBY'
path = ARGV[0]
text = File.read(path, encoding: "UTF-8")

# 1) Inject a class-level help constant if not already present
unless text.include?("static readonly string AIGG_HELP")
  # Find first class opening brace
  m = text.match(/class\s+\w+[^{]*\{/)
  if m
    insert_at = m.end(0)
    help = %Q(
    private static readonly string AIGG_HELP = @"AIGG Workbench
— Parse NL (local): NLToJson.GenerateFromPrompt() then AIGG_NLInterpreter.RunToJson() fallback.
— Open Paste & Merge: routes JSON into SpecPasteMergeWindow.
— Copy skeleton intent: scaffolds an intent for unmatched phrases.
Tip: click Unmatched chips to copy patterns.";
)
    text = text.dup.insert(insert_at, help)
    puts "Inserted AIGG_HELP in class."
  else
    puts "WARN: Could not find class brace to insert AIGG_HELP."
  end
end

# 2) Replace multi-line string literal passed to GUILayout.Label(...) near the error range
lines = text.lines
# Build byte offsets for precise slicing
offsets = [0]
acc = 0
lines.each { |ln| acc += ln.bytesize; offsets << acc }

get_pos = ->(line_no) { # 1-based line numbers
  line_no = 1 if line_no < 1
  line_no = lines.size if line_no > lines.size
  offsets[line_no - 1]
}

start_pos = get_pos.(470)
end_pos   = get_pos.(520)

head = text.byteslice(0, start_pos)
region = text.byteslice(start_pos, end_pos - start_pos) || ""
tail = text.byteslice(end_pos, text.bytesize - end_pos) || ""

# Regex: GUILayout.Label("...<newline>...") -> GUILayout.Label(AIGG_HELP)
# Only hits if the string literal actually spans a newline.
fixed = region.gsub(/GUILayout\.Label\(\s*"(?:\\.|[^"\\])*?\n(?:\\.|[^"\\])*?"\s*\)/m, 'GUILayout.Label(AIGG_HELP)')

if fixed != region
  puts "Replaced broken multi-line GUILayout.Label(...) with AIGG_HELP in error region."
  text = head + fixed + tail
else
  puts "No multi-line GUILayout.Label(...) found in error region (470..520)."
end

File.write(path, text, mode: "w", encoding: "UTF-8")
RUBY

echo "Patched. Bringing Unity to front will trigger a recompile."
