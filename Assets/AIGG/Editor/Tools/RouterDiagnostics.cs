// ASCII only
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

namespace Aim2Pro.AIGG {
  public static class RouterDiagnostics {
    [MenuItem("Window/Aim2Pro/Tools/Router Diagnostics/Run Self-Test")]
    public static void RunSelfTest() {
      Debug.Log("[RouterDiag] ==== START ====");
      var asm = AppDomain.CurrentDomain.GetAssemblies();

      // 1) List all Aim2Pro.AIGG types containing 'PreMerge' or 'Router'
      var types = asm.SelectMany(a => {
        try { return a.GetTypes(); } catch { return new Type[0]; }
      }).Where(t => t.FullName != null && t.FullName.Contains("Aim2Pro.AIGG"))
        .Where(t => t.FullName.IndexOf("PreMerge", StringComparison.OrdinalIgnoreCase) >= 0
                 || t.FullName.IndexOf("Router", StringComparison.OrdinalIgnoreCase) >= 0)
        .OrderBy(t => t.FullName)
        .ToList();

      foreach (var t in types) Debug.Log("[RouterDiag] Type: " + t.FullName);

      // 2) Try to open the canonical window directly
      var ok = TryOpen("Aim2Pro.AIGG.PreMergeRouterWindow");
      Debug.Log("[RouterDiag] TryOpen PreMergeRouterWindow: " + (ok ? "OK" : "NOT FOUND"));

      // 3) Try legacy names
      ok |= TryOpen("Aim2Pro.AIGG.PreMergeWindow");
      ok |= TryOpen("Aim2Pro.AIGG.PreMergeCenterWindow");
      ok |= TryOpen("Aim2Pro.AIGG.PreMergeRouter");
      ok |= TryOpen("Aim2Pro.AIGG.PreMerge");
      ok |= TryOpen("Aim2Pro.AIGG.RouterWindow");

      // 4) Check for Paste&Merge to ensure router can forward when strict OK
      bool hasPaste = HasType("Aim2Pro.AIGG.SpecPasteMergeWindow");
      Debug.Log("[RouterDiag] SpecPasteMergeWindow present: " + hasPaste);

      // 5) Final message
      EditorUtility.DisplayDialog("Router Diagnostics",
        "Found " + types.Count + " matching types.\n" +
        "Router window open attempt: " + (ok ? "Succeeded" : "Failed") + "\n" +
        "Paste & Merge present: " + (hasPaste ? "Yes" : "No") + "\n\n" +
        "See Console for full type list.\n" +
        "If still seeing 'Router window not found', the caller is using a different type name.\n" +
        "Aliases installed: PreMergeWindow, PreMergeCenterWindow, PreMergeRouter, PreMerge, RouterWindow.",
        "OK");
      Debug.Log("[RouterDiag] ==== END ====");
    }

    private static bool TryOpen(string fullName) {
      var t = Type.GetType(fullName);
      if (t == null) return false;
      var m = t.GetMethod("Open", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
      if (m == null) return false;
      m.Invoke(null, null);
      return true;
    }

    private static bool HasType(string fullName) => Type.GetType(fullName) != null;
  }
}
