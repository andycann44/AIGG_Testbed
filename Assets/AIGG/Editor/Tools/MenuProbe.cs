// ASCII only
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;

namespace Aim2Pro.AIGG {
  public static class MenuProbe {
    [MenuItem(MenuSpec.Tools + "/Run Spec Guard Now")]
    public static void Run() {
      int ok=0, bad=0;
      var asm = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var t in asm.SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })) {
        foreach (var m in t.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)) {
          foreach (var a in m.GetCustomAttributes(typeof(MenuItem), false).OfType<MenuItem>()) {
            var path = a.menuItem ?? "";
            bool good = path.StartsWith(MenuSpec.Root);
            if (good) ok++; else { bad++; Debug.LogWarning("[MenuSpec] Off-spec menu: " + path + " (" + t.FullName + "." + m.Name + ")"); }
          }
        }
      }
      EditorUtility.DisplayDialog("Menu Spec Guard", "OK: "+ok+"\nOff-spec: "+bad+"\nSee Console for details.", "OK");
    }
  }
}
