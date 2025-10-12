#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/PasteMergeOpenFix_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/WorkbenchWindow.cs.bak"
echo "Backup: $BK/WorkbenchWindow.cs.bak"

python3 - "$F" <<'PY'
import sys, re
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()

sig = "void OpenPasteAndMerge()"
i = s.find(sig)
if i == -1:
    print("Could not find OpenPasteAndMerge()"); sys.exit(1)

j = s.find("{", i)
if j == -1:
    print("OpenPasteAndMerge: no {"); sys.exit(1)

# balance braces
depth=0; end=None
for k in range(j, len(s)):
    c=s[k]
    if c=="{": depth+=1
    elif c=="}":
        depth-=1
        if depth==0:
            end=k+1; break
if end is None:
    print("OpenPasteAndMerge: no }"); sys.exit(1)

new_body = r'''void OpenPasteAndMerge()
{
  try
  {
    string used = null;
    // 1) Try finding a window type with name containing both "Paste" and "Merge"
    var types = AppDomain.CurrentDomain.GetAssemblies()
      .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
      .Where(t => {
        var n = (t.FullName ?? t.Name ?? "");
        n = n.ToLowerInvariant();
        return n.Contains("paste") && n.Contains("merge");
      })
      .ToArray();

    foreach (var t in types)
    {
      var methods = t.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|
                                 System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance)
                     .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string))
                     .ToArray();

      // Prefer obvious names
      string[] prefs = { "openwithjson", "loadjson", "setjson", "pastejson", "open", "show" };

      foreach (var m in methods.OrderBy(m => {
        var n = m.Name.ToLowerInvariant();
        for (int pi=0; pi<prefs.Length; ++pi) if (n.Contains(prefs[pi])) return pi;
        return 999;
      }))
      {
        try
        {
          object inst = m.IsStatic ? null : EditorWindow.GetWindow(t);
          m.Invoke(inst, new object[]{ _json ?? "" });
          used = $"Type:{t.FullName} Method:{m.Name}";
          _diag = $"OpenPasteAndMerge via {used}\n" + _diag;
          ShowNotification(new GUIContent("Sent to Paste & Merge"));
          return;
        }
        catch {}
      }

      // If no suitable method, at least open the window and copy JSON
      try
      {
        var win = EditorWindow.GetWindow(t);
        if (win != null)
        {
          EditorGUIUtility.systemCopyBuffer = _json ?? "";
          used = $"Type:{t.FullName} (opened, JSON copied)";
          _diag = $"OpenPasteAndMerge via {used}\n" + _diag;
          ShowNotification(new GUIContent("Opened Paste & Merge window (JSON copied)"));
          return;
        }
      } catch {}
    }

    // 2) Try menu paths (broad sweep)
    string[] menus = new string[] {
      "Window/Aim2Pro/Paste & Merge",
      "Window/Paste & Merge",
      "Tools/Paste & Merge",
      "Window/AIGG/Paste & Merge",
      "Window/Spec/Paste & Merge",
      "Tools/AIGG/Paste & Merge"
    };
    foreach (var m in menus)
    {
      try
      {
        if (EditorApplication.ExecuteMenuItem(m))
        {
          if (!string.IsNullOrEmpty(_json)) EditorGUIUtility.systemCopyBuffer = _json;
          used = "Menu:" + m;
          _diag = $"OpenPasteAndMerge via {used}\n" + _diag;
          ShowNotification(new GUIContent("Opened via menu (JSON copied)"));
          return;
        }
      } catch {}
    }

    // 3) Last resort: copy & notify
    if (!string.IsNullOrEmpty(_json)) EditorGUIUtility.systemCopyBuffer = _json;
    _diag = "OpenPasteAndMerge: window not found; JSON copied to clipboard.\n" + _diag;
    EditorUtility.DisplayDialog("Paste & Merge not found",
      "Could not locate the Paste & Merge window.\nJSON has been copied to your clipboard.",
      "OK");
  }
  catch (Exception ex)
  {
    if (!string.IsNullOrEmpty(_json)) EditorGUIUtility.systemCopyBuffer = _json;
    _diag = "OpenPasteAndMerge error: " + ex.Message + "\n" + _diag;
    ShowNotification(new GUIContent("Error; JSON copied"));
  }
}'''

s = s[:i] + new_body + s[end:]
open(p,'w',encoding='utf-8').write(s)
print("OpenPasteAndMerge() replaced with robust variant.")
PY

touch "$F"
echo "Patched. Let Unity recompile, then press Self-heal (OpenAI) again."
