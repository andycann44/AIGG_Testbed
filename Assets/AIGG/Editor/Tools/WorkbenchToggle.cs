using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;

namespace Aim2Pro.AIGG {
  public static class WorkbenchToggle {
    const string SYMBOL = "AIGG_ENABLE_WORKBENCH";

    [MenuItem("Window/Aim2Pro/Aigg/Workbench/Enable")]
    public static void Enable() => Toggle(true);

    [MenuItem("Window/Aim2Pro/Aigg/Workbench/Disable")]
    public static void Disable() => Toggle(false);

    static void Toggle(bool on) {
      foreach (BuildTargetGroup g in Enum.GetValues(typeof(BuildTargetGroup)).Cast<BuildTargetGroup>()) {
        if (g == BuildTargetGroup.Unknown) continue;
        try {
          var raw = PlayerSettings.GetScriptingDefineSymbolsForGroup(g) ?? "";
          var set = new HashSet<string>(
            raw.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase
          );
          if (on) set.Add(SYMBOL); else set.Remove(SYMBOL);
          var next = string.Join(";", set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
          PlayerSettings.SetScriptingDefineSymbolsForGroup(g, next);
        } catch { /* ignore */ }
      }
      AssetDatabase.Refresh();
      EditorUtility.DisplayDialog("Workbench", on ? "Enabled (recompile to include files)." : "Disabled.", "OK");
    }
  }
}
