#!/bin/zsh
set -e

f="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$f" ]] || { echo "Can't find $f — run from your Unity project root."; exit 1; }

bk="Assets/AIGG/_Backups/AutoFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$bk"
cp "$f" "$bk/WorkbenchWindow.cs.bak"

# Ruby script: convert any string literal that spans lines into a verbatim string.
# Handles both "..."  -> @"..."
# and    $"..." -> $@"..."
/usr/bin/ruby - "$f" <<'RUBY'
path = ARGV[0]
src  = File.read(path, encoding: "UTF-8")
out  = +""
pos  = 0
changed = 0

rx = /(?<!@)"(?:[^"\\]|\\.|[\r\n])*"/m

while (m = rx.match(src, pos))
  start = m.begin(0)
  stop  = m.end(0)
  lit   = m[0]

  out << src[pos...start]

  if lit.include?("\n") || lit.include?("\r")
    # Multi-line string -> make verbatim
    prev_char = start > 0 ? src[start-1] : nil
    if prev_char == '$'
      # Replace $"..." with $@"..."
      out.chop!   # remove the '$' we just copied in previous chunk
      out << '$@' << lit
    else
      # Replace "..." with @"..."
      out << '@' << lit
    end
    changed += 1
  else
    out << lit
  end

  pos = stop
end

out << src[pos..-1]

if changed > 0
  File.write(path, out, mode: "w", encoding: "UTF-8")
  puts "Converted #{changed} multi-line string literal(s) to verbatim in #{path}"
else
  puts "No multi-line string literals found in #{path}"
end
RUBY

echo "Done. Backup at: $bk/WorkbenchWindow.cs.bak"
