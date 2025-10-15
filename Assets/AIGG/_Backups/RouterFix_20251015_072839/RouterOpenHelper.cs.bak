// ASCII only
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

namespace Aim2Pro.AIGG {
  public static class RouterOpenHelper {
    public static bool TryOpen() {
      var t = Type.GetType("Aim2Pro.AIGG.PreMergeRouterWindow");
      if (t == null) {
        t = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
          .FirstOrDefault(x => x.FullName == "Aim2Pro.AIGG.PreMergeRouterWindow");
      }
      if (t == null) return false;
      var m = t.GetMethod("Open", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
      if (m != null) { m.Invoke(null, null); return true; }
      // Fallback: open by type
      var w = EditorWindow.GetWindow(t); w.Show();
      return true;
    }
  }
  // Tiny global to be callable from patched dialogs
  public static class _AIGGRouterDialogPatch {
    public static void _AIGGRouterTryOpen() {
      if (!RouterOpenHelper.TryOpen()) {
        EditorUtility.DisplayDialog("Pre-Merge",
          "Router window type still not found after search.\nCheck Console for compile errors on PreMergeRouterWindow.",
          "OK");
      }
    }
  }
}
