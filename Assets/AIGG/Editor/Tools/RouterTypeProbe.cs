// ASCII only
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

namespace Aim2Pro.AIGG {
  public static class RouterTypeProbe {
    [MenuItem("Window/Aim2Pro/Tools/Router Diagnostics/Type Probe")]
    public static void Probe() {
      var names = new[]{
        "Aim2Pro.AIGG.PreMergeRouterWindow",
        "Aim2Pro.AIGG.PreMergeRouterAPI",
        "Aim2Pro.AIGG.PreMergeDiagnosticsWindow",
        "Aim2Pro.AIGG.SpecAudit",
        "Aim2Pro.AIGG.SpecPasteMergeWindow"
      };
      foreach (var n in names) {
        var t = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
          .FirstOrDefault(x => x.FullName == n);
        Debug.Log("[RouterTypeProbe] " + n + " => " + (t != null ? "FOUND" : "MISSING"));
      }
      EditorUtility.DisplayDialog("Router Type Probe", "Check Console for FOUND/MISSING.", "OK");
    }
  }
}
