#!/usr/bin/env bash
set -euo pipefail

F="Assets/AIGG/Editor/Workbench/WorkbenchWindow.cs"
[[ -f "$F" ]] || { echo "Can't find $F"; exit 1; }

BK="Assets/AIGG/_Backups/PasteMergeInject_$(date +%Y%m%d_%H%M%S)"
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

depth=0; end=None
for k in range(j, len(s)):
    c=s[k]
    if c=="{": depth+=1
    elif c=="}":
        depth-=1
        if depth==0: end=k+1; break
if end is None: 
    print("OpenPasteAndMerge: no }"); sys.exit(1)

new_body = r'''void OpenPasteAndMerge()
{
  try
  {
    string used = null;

    // --- 1) Prefer the explicit SpecPasteMergeWindow type if present
    var targetType = AppDomain.CurrentDomain.GetAssemblies()
      .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
      .FirstOrDefault(t => (t.FullName ?? t.Name ?? "").EndsWith("SpecPasteMergeWindow"));

    // Otherwise, search for any window that looks like a paste+merge tool
    var types = (targetType != null ? new[]{ targetType } :
      AppDomain.CurrentDomain.GetAssemblies()
      .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
      .Where(t => {
        var n = (t.FullName ?? t.Name ?? "").ToLowerInvariant();
        return n.Contains("paste") && n.Contains("merge") && typeof(EditorWindow).IsAssignableFrom(t);
      }).ToArray());

    foreach (var t in types)
    {
      EditorWindow win = null;
      try { win = EditorWindow.GetWindow(t); } catch { }
      if (win == null) continue;

      // Try common single-string methods first
      string[] prefMethods = { "OpenWithJson","LoadJson","SetPastedContent","SetPastedText","SetJson","PasteJson","LoadFromString","Open","Show" };
      var methods = t.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|
                                 System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static)
                     .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string))
                     .OrderBy(m => {
                       var ln = m.Name.ToLowerInvariant();
                       for (int idx=0; idx<prefMethods.Length; ++idx) if (ln.Contains(prefMethods[idx].ToLowerInvariant())) return idx;
                       return 999;
                     }).ToArray();

      foreach (var m in methods)
      {
        try {
          object inst = m.IsStatic ? null : (object)win;
          m.Invoke(inst, new object[]{ _json ?? "" });
          win.Focus(); win.Repaint();
          used = $"Type:{t.FullName} Method:{m.Name}";
          _diag = $"OpenPasteAndMerge via {used}\n" + _diag;
          ShowNotification(new GUIContent("Sent to Paste & Merge"));
          return;
        } catch {}
      }

      // Try a writable string property with "pasted/content/json/input/text" in the name
      string[] keys = { "pasted", "paste", "content", "json", "input", "text", "buffer" };
      var props = t.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                   .Where(pr => pr.CanWrite && pr.PropertyType == typeof(string) &&
                                keys.Any(k => (pr.Name ?? "").ToLowerInvariant().Contains(k)))
                   .ToArray();
      foreach (var pr in props)
      {
        try {
          pr.SetValue(win, _json ?? "");
          win.Focus(); win.Repaint();
          used = $"Type:{t.FullName} Property:{pr.Name}";
          _diag = $"OpenPasteAndMerge via {used}\n" + _diag;
          ShowNotification(new GUIContent("Pasted into Paste & Merge (property)"));
          return;
        } catch {}
      }

      // Try a string field with similar name
      var fields = t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                    .Where(f => f.FieldType == typeof(string) &&
                                keys.Any(k => (f.Name ?? "").ToLowerInvariant().Contains(k)))
                    .ToArray();
      foreach (var f in fields)
      {
        try {
          f.SetValue(win, _json ?? "");
          win.Focus(); win.Repaint();
          used = $"Type:{t.FullName} Field:{f.Name}";
          _diag = $"OpenPasteAndMerge via {used}\n" + _diag;
          ShowNotification(new GUIContent("Pasted into Paste & Merge (field)"));
          return;
        } catch {}
      }

      // If we got the window but couldn't inject, at least copy
      try {
        EditorGUIUtility.systemCopyBuffer = _json ?? "";
        win.Focus(); win.Repaint();
        used = $"Type:{t.FullName} (opened, JSON copied)";
        _diag = $"OpenPasteAndMerge via {used}\n" + _diag;
        ShowNotification(new GUIContent("Opened Paste & Merge (JSON copied)"));
        return;
      } catch {}
    }

    // --- 2) Try menu paths if reflection failed completely
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
      try {
        if (EditorApplication.ExecuteMenuItem(m))
        {
          if (!string.IsNullOrEmpty(_json)) EditorGUIUtility.systemCopyBuffer = _json;
          _diag = $"OpenPasteAndMerge via Menu:{m}\n" + _diag;
          ShowNotification(new GUIContent("Opened via menu (JSON copied)"));
          return;
        }
      } catch {}
    }

    // --- 3) Last resort: copy and notify
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
print("OpenPasteAndMerge() updated with method/property/field injection.")
PY

touch "$F"
echo "Patched. Let Unity recompile, then click Self-heal (OpenAI) once more."
