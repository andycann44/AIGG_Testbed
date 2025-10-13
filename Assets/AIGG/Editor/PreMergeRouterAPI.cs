using System; using System.Reflection; using UnityEditor; using UnityEngine;
namespace Aim2Pro.AIGG {
  public static class PreMergeRouterAPI {
    public static void Route(string json) {
      if (string.IsNullOrEmpty(json)) { Debug.LogWarning("[PreMergeRouterAPI] Empty JSON"); return; }
      if (Call("Aim2Pro.AIGG.PreMergeCenterWindow","OpenWithJson",json)) return;
      if (Call("Aim2Pro.AIGG.PreMergeRouterWindow","OpenWithJson",json)) return;
      EditorGUIUtility.systemCopyBuffer = json;
      Debug.Log("[PreMergeRouterAPI] No Pre-Merge window found; JSON copied to clipboard.");
    }
    public static void Route(string json, bool _) { Route(json); }
    static bool Call(string tname, string mname, string arg){
      foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()){
        try { var t=asm.GetType(tname); if (t==null) continue;
          var m=t.GetMethod(mname, BindingFlags.Public|BindingFlags.Static); if (m==null) continue;
          m.Invoke(null, new object[]{arg}); return true; } catch {}
      } return false;
    }
  }
}
