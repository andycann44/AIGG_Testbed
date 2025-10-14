using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;

namespace Aim2Pro.AIGG.Tools {
  public static class MenuSanity {
    // Required menu paths (allow either Router or Center)
    static readonly string[] RequiredMenuItems = {
      "Window/Aim2Pro/Aigg/Workbench",
      "Window/Aim2Pro/Aigg/Paste & Merge"
    };
    static readonly string[] RouterMenusAny = {
      "Window/Aim2Pro/Aigg/Pre-Merge Router",
      "Window/Aim2Pro/Aigg/Pre-Merge Center"
    };

    [MenuItem("Window/Aim2Pro/Test/Run Sanity Checks", priority = 10)]
    public static void Run() {
      Debug.Log("===== [AIGG] SANITY CHECKS START =====");
      CheckMenus();
      OpenIfPresent("Window/Aim2Pro/Aigg/Workbench");
      OpenIfPresent("Window/Aim2Pro/Aigg/Pre-Merge Router", optional:true);
      OpenIfPresent("Window/Aim2Pro/Aigg/Pre-Merge Center", optional:true);
      OpenIfPresent("Window/Aim2Pro/Aigg/Paste & Merge");
      Debug.Log("===== [AIGG] SANITY CHECKS END =====");
    }

    static void CheckMenus() {
      var found = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => {
          try { return a.GetTypes(); } catch { return new Type[0]; }
        })
        .SelectMany(t => t.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
        .Select(m => (method:m, attr:m.GetCustomAttributes(typeof(MenuItem), false).FirstOrDefault() as MenuItem))
        .Where(x => x.attr != null)
        .Select(x => x.attr.menuItem)
        .ToArray();

      foreach (var req in RequiredMenuItems) {
        if (found.Contains(req)) Debug.Log($"[AIGG][OK] Menu found: {req}");
        else Debug.LogError($"[AIGG][MISS] Menu missing: {req}");
      }

      if (RouterMenusAny.Any(r => found.Contains(r))) {
        Debug.Log("[AIGG][OK] Router menu present (Router or Center).");
      } else {
        Debug.LogError("[AIGG][MISS] Router menu missing (need one of: Pre-Merge Router OR Pre-Merge Center).");
      }
    }

    static void OpenIfPresent(string menu, bool optional=false) {
      if (EditorApplication.ExecuteMenuItem(menu)) {
        Debug.Log("[AIGG][OPEN] " + menu);
      } else {
        var msg = "[AIGG][MISS] Cannot open menu: " + menu;
        if (optional) Debug.Log(msg); else Debug.LogError(msg);
      }
    }
  }
}
