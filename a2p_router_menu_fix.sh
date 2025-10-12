#!/usr/bin/env bash
set -euo pipefail

FILE="$(find Assets -name PreMergeRouterWindow.cs -print -quit || true)"
if [[ -z "$FILE" ]]; then
  echo "❌ PreMergeRouterWindow.cs not found under Assets/"; exit 1
fi

TS="$(date +%Y%m%d_%H%M%S)"
BKP="Assets/AIGG/_Backups/RouterMenuFix_${TS}"
mkdir -p "$BKP"
cp -p "$FILE" "$BKP/PreMergeRouterWindow.cs.bak"

python3 - "$FILE" <<'PY'
import sys, re

path = sys.argv[1]
src  = open(path, 'r', encoding='utf-8', errors='ignore').read()

# 1) Insert helper methods OpenPasteWindow + FindType inside the class
def insert_helpers(code):
    # find class start and end
    m = re.search(r'\bclass\s+PreMergeRouterWindow\s*:\s*EditorWindow\s*\{', code)
    if not m:
        return code, False
    cls_open = m.end() - 1
    depth = 0; i = cls_open
    while i < len(code):
        if code[i] == '{': depth += 1
        elif code[i] == '}':
            depth -= 1
            if depth == 0: cls_close = i; break
        i += 1
    helpers = r'''
        private static bool OpenPasteWindow()
        {
            try
            {
                // Prefer opening by type (no menu dependency)
                var t = FindType("Aim2Pro.AIGG.Editor.SpecPasteMergeWindow");
                if (t != null)
                {
                    var win = (UnityEditor.EditorWindow)UnityEditor.EditorWindow.GetWindow(t);
                    win.minSize = new UnityEngine.Vector2(700, 450);
                    win.Show(); win.Focus();
                    return true;
                }
            }
            catch { }

            try
            {
                // Fallback: try known menus (if present)
                if (UnityEditor.EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Aigg/Paste & Merge")) return true;
                if (UnityEditor.EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Aigg/Paste & Replace")) return true;
            }
            catch { }
            return false;
        }

        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    '''
    # if they already exist, skip
    if 'OpenPasteWindow()' in code and 'FindType(string fullName)' in code:
        return code, False
    return code[:cls_close] + helpers + code[cls_close:], True

src, added_helpers = insert_helpers(src)

# 2) Replace button handler in DrawTabs(): remove ExecuteMenuItem calls -> use OpenPasteWindow()
src = re.sub(
    r'EditorGUIUtility\.systemCopyBuffer\s*=\s*payload\s*\?\?\s*""\s*;\s*[\r\n\s]*'
    r'if\s*\(!?EditorApplication\.ExecuteMenuItem\([^\)]*\)\)\s*'
    r'EditorApplication\.ExecuteMenuItem\([^\)]*\)\s*;',
    'EditorGUIUtility.systemCopyBuffer = payload ?? "";\n                    OpenPasteWindow();',
    src
)

# 3) Replace SendAllToPaste() body to use OpenPasteWindow() once per tab, no menu warnings
def patch_sendall(code):
    m = re.search(r'\bvoid\s+SendAllToPaste\s*\(\s*\)\s*\{', code)
    if not m: return code, False
    start = m.end()
    # find end of method
    depth = 1; i = start
    while i < len(code):
        if code[i] == '{': depth += 1
        elif code[i] == '}':
            depth -= 1
            if depth == 0:
                end = i+1; break
        i += 1
    new_body = r'''
        {
            for (int t = 0; t < _tabKeys.Count; t++)
            {
                var key = _tabKeys[t];
                if (!_payloads.TryGetValue(key, out var payload)) continue;
                try { EditorGUIUtility.systemCopyBuffer = payload ?? ""; } catch {}
                OpenPasteWindow();
            }
            ShowNotification(new GUIContent("Sent all sections to Paste window (clipboard)."));
        }
    '''
    return code[:m.start()] + 'private void SendAllToPaste()' + new_body + code[end:], True

src, patched_sendall = patch_sendall(src)

# 4) Guard DrawTabs() Copy/Open block to ensure layout stays balanced (no early returns)
#    (Already balanced in most cases; we just make sure we didn't leave stray braces)
# Nothing extra needed if regex in (2) succeeded.

open(path, 'w', encoding='utf-8').write(src)
print("helpers_added=", added_helpers, "sendall_patched=", patched_sendall)
PY

echo "✅ Patched router menu handling. Backup: $BKP"
echo "→ Refocus Unity to recompile and retry the buttons."
