#!/bin/zsh
set -e

f="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$f" ]] || { echo "Can't find $f — run from your Unity project root."; exit 1; }

bk="Assets/AIGG/_Backups/Replace_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$bk"
cp "$f" "$bk/WorkbenchWindow.cs.bak"

cat > "$f" <<'CS'
// Auto-replaced safe stub — keeps the project compiling.
// Feel free to expand this later; it's intentionally minimal.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public class WorkbenchWindow : EditorWindow
    {
        private static readonly string HelpText = 
@"AIGG Workbench
- Parse NL (local): uses local parsers (no network)
- Open Paste & Merge: routes JSON into your merge window
- Copy skeleton intent: scaffolds patterns for unmatched phrases";

        [MenuItem("Window/Aim2Pro/Workbench")]
        public static void Open()
        {
            GetWindow<WorkbenchWindow>("Workbench");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(HelpText, MessageType.Info);
            GUILayout.Space(8);
            if (GUILayout.Button("Parse NL (local)")) {
                Debug.Log("[Workbench] Parse NL clicked (stub).");
            }
            if (GUILayout.Button("Open Paste & Merge")) {
                Debug.Log("[Workbench] Open Paste & Merge (stub).");
            }
            if (GUILayout.Button("Copy skeleton intent")) {
                EditorGUIUtility.systemCopyBuffer = "{ /* intent skeleton */ }";
                Debug.Log("[Workbench] Intent skeleton copied to clipboard.");
            }
        }
    }
}
#endif
CS

echo "Replaced $f with a safe compiling stub."
echo "Backup saved to: $bk/WorkbenchWindow.cs.bak"
