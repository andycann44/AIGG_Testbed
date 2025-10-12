#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/PasteMergeForceInject_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BK"; cp "$F" "$BK/WorkbenchWindow.cs.bak"
echo "Backup: $BK/WorkbenchWindow.cs.bak"

python3 - "$F" <<'PY'
import sys, re
p=sys.argv[1]
s=open(p,'r',encoding='utf-8').read()

sig = "void OpenPasteAndMerge()"
i = s.find(sig)
if i == -1:
    print("OpenPasteAndMerge() not found"); sys.exit(1)

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
    // keep it in clipboard anyway
    if (!string.IsNullOrEmpty(_json)) EditorGUIUtility.systemCopyBuffer = _json;

    // Prefer SpecPasteMergeWindow if present; otherwise any window with paste+merge in the name
    var t = AppDomain.CurrentDomain.GetAssemblies()
      .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
      .FirstOrDefault(tt => (tt.FullName ?? tt.Name ?? "").EndsWith("SpecPasteMergeWindow"));

    if (t == null)
    {
      t = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
        .FirstOrDefault(tt => {
          var n = (tt.FullName ?? tt.Name ?? "").ToLowerInvariant();
          return n.Contains("paste") && n.Contains("merge") && typeof(EditorWindow).IsAssignableFrom(tt);
        });
    }

    if (t != null)
    {
      EditorWindow win = null;
      try { win = EditorWindow.GetWindow(t); } catch {}
      if (win != null)
      {
        var touched = new List<string>();

        // 1) Any one-arg(string) method (OpenWithJson/SetJson/etc.)
        foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|
                                       System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static))
        {
          var ps = m.GetParameters();
          if (ps.Length == 1 && ps[0].ParameterType == typeof(string))
          {
            try { object inst = m.IsStatic ? null : (object)win; m.Invoke(inst, new object[]{ _json ?? "" }); touched.Add("Method:"+m.Name); } catch {}
          }
        }

        // 2) Any writable string property
        foreach (var pr in t.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|
                                           System.Reflection.BindingFlags.Instance))
        {
          if (pr.CanWrite && pr.PropertyType == typeof(string))
          {
            try { pr.SetValue(win, _json ?? ""); touched.Add("Property:"+pr.Name); } catch {}
          }
        }

        // 3) Any string field (private/public)
        foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|
                                      System.Reflection.BindingFlags.Instance))
        {
          if (f.FieldType == typeof(string))
          {
            try { f.SetValue(win, _json ?? ""); touched.Add("Field:"+f.Name); } catch {}
          }
        }

        // Focus & repaint
        try { win.Focus(); win.Repaint(); } catch {}

        // Diagnostics
        if (touched.Count > 0)
        {
          _diag = ("OpenPasteAndMerge injected into " + t.FullName + " → " + string.Join(", ", touched) + "\n") + _diag;
          ShowNotification(new GUIContent("Pasted into Paste & Merge"));
          return;
        }
        else
        {
          _diag = ("OpenPasteAndMerge opened " + t.FullName + " (no injectable members found). JSON copied to clipboard.\n") + _diag;
          ShowNotification(new GUIContent("Opened Paste & Merge (JSON copied)"));
          return;
        }
      }
    }

    // Menus as last resort
    string[] menus = {
      "Window/Aim2Pro/Paste & Merge",
      "Window/Paste & Merge",
      "Tools/Paste & Merge",
      "Window/AIGG/Paste & Merge",
      "Window/Spec/Paste & Merge",
      "Tools/AIGG/Paste & Merge"
    };
    foreach (var m in menus)
    {
      try {
        if (EditorApplication.ExecuteMenuItem(m))
        {
          _diag = ("OpenPasteAndMerge via Menu:" + m + " (JSON copied)\n") + _diag;
          ShowNotification(new GUIContent("Opened via menu (JSON copied)"));
          return;
        }
      } catch {}
    }

    // Fall back: copy & notify
    _diag = "OpenPasteAndMerge: window not found; JSON copied to clipboard.\n" + _diag;
    EditorUtility.DisplayDialog("Paste & Merge not found",
      "Could not locate the Paste & Merge window.\nJSON has been copied to your clipboard.",
      "OK");
  }
  catch (Exception ex)
  {
    _diag = "OpenPasteAndMerge error: " + ex.Message + "\n" + _diag;
    ShowNotification(new GUIContent("Error; JSON copied"));
  }
}'''
s = s[:i] + new_body + s[end:]
open(p,'w',encoding='utf-8').write(s)
print("OpenPasteAndMerge() now injects all string members.")
PY

touch "$F"
echo "Patched. Let Unity recompile."
