// ASCII only
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

namespace Aim2Pro.AIGG {
  internal static class _PreMergeAliasUtil {
    public static bool TryOpenRouter() {
      var t = Type.GetType("Aim2Pro.AIGG.PreMergeRouterWindow");
      if (t == null) {
        // search all assemblies just in case
        t = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(x => x.FullName == "Aim2Pro.AIGG.PreMergeRouterWindow");
      }
      if (t == null) {
        EditorUtility.DisplayDialog("Pre-Merge",
          "Router window type not found.\n\nOpen it via Window > Aim2Pro > Aigg > Pre-Merge if present.\n" +
          "Aliases are installed; once the type compiles, these will open it.",
          "OK");
        return false;
      }
      var m = t.GetMethod("Open", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
      if (m == null) {
        EditorUtility.DisplayDialog("Pre-Merge",
          "Found PreMergeRouterWindow type but no static Open() method.\n" +
          "Add: public static void Open(){ GetWindow<PreMergeRouterWindow>().Show(); }",
          "OK");
        return false;
      }
      m.Invoke(null, null);
      return true;
    }
  }

  public static class PreMergeWindow {
    public static void Open(){ Aim2Pro.AIGG.RouterOpenHelper.TryOpen(); }
public static void Route(string nl, string canonical, string diagnostics){ PreMergeRouterAPI.Route(nl,canonical,diagnostics); }
    public static void Route(string canonical){ PreMergeRouterAPI.Route(canonical); }
    public static void Route(string nl, string canonical){ PreMergeRouterAPI.Route(nl,canonical); }
  }

  public static class PreMergeCenterWindow {
    public static void Open(){ Aim2Pro.AIGG.RouterOpenHelper.TryOpen(); }
public static void Route(string nl, string canonical, string diagnostics){ PreMergeRouterAPI.Route(nl,canonical,diagnostics); }
    public static void Route(string canonical){ PreMergeRouterAPI.Route(canonical); }
    public static void Route(string nl, string canonical){ PreMergeRouterAPI.Route(nl,canonical); }
  }

  public static class PreMergeRouter {
    public static void Open(){ Aim2Pro.AIGG.RouterOpenHelper.TryOpen(); }
}
}
