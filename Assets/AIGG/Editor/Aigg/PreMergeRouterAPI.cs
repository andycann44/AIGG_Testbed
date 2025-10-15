// ASCII only
using System; using System.Text.RegularExpressions; using UnityEditor; using UnityEngine;
namespace Aim2Pro.AIGG {
  public static class PreMergeRouterAPI {
    public static void Route(string nl, string canonicalJson, string diagnosticsJson){ StrictRoute(nl??"",canonicalJson??"",diagnosticsJson??""); }
    public static void RoutePayload(string payloadJson){
      if (string.IsNullOrWhiteSpace(payloadJson)) { Fail("No payload",null,null,null); return; }
      string nl = RX("\"nl\"\\s*:\\s*\"([^\"]*)\"", payloadJson);
      string dj = Obj("\"diagnostics\"\\s*:\\s*\\{", payloadJson);
      string cj = Obj("\"canonical\"\\s*:\\s*\\{", payloadJson);
      if (string.IsNullOrEmpty(cj)) { Fail("No canonical", nl,null,dj); return; }
      if (string.IsNullOrEmpty(dj)) { Fail("No diagnostics", nl,"{"+cj+"}",null); return; }
      StrictRoute(nl,"{"+cj+"}","{"+dj+"}");
    }
    private static void StrictRoute(string nl, string cj, string dj){
      var d = Diagnostics.Parse(dj); if (!d.HasValue){ Fail("Diagnostics parse failed",nl,cj,dj); return; }
      if (d.unmatched.Count>0 || d.ok==false){ Fail("Unmatched tokens present",nl,cj,dj,d); return; }
      var missing = SpecAudit.FindMissingCommands(cj); if (missing.Count>0){ Fail("Missing commands: "+string.Join(", ",missing),nl,cj,dj,d); return; }
      Forward(cj);
    }
    private static void Forward(string json){
      var asm = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var a in asm){ var t=a.GetType("Aim2Pro.AIGG.SpecPasteMergeWindow"); if (t==null) continue;
        var m=t.GetMethod("OpenWithJson",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
        if (m!=null){ m.Invoke(null,new object[]{json}); return; } }
      EditorGUIUtility.systemCopyBuffer=json;
      EditorUtility.DisplayDialog("Pre-Merge (Fallback)","Paste & Merge not found. JSON copied to clipboard.","OK");
      Debug.Log("[PreMergeRouterAPI] Paste window not found. JSON copied to clipboard.");
    }
    private static void Fail(string reason,string nl,string cj,string dj, Diagnostics? d=null){
      PreMergeDiagnosticsWindow.Show(reason??"Blocked", nl??"", cj??"", dj??"", d);
      Debug.LogWarning("[PreMerge STRICT BLOCK] "+reason);
    }
    private static string RX(string pat,string src){ var m=Regex.Match(src,pat); return m.Success?m.Groups[1].Value:""; }
    private static string Obj(string anchor,string src){
      var a=Regex.Match(src,anchor); if(!a.Success) return ""; int start=a.Index+a.Length-1, depth=0;
      for(int i=start;i<src.Length;i++){ char c=src[i]; if(c=='{') depth++; else if(c=='}'){ depth--; if(depth==0) return src.Substring(start+1,i-(start+1)); } }
      return "";
    }
    public struct Diagnostics{
      public System.Collections.Generic.List<string> unmatched; public bool ok;
      public static Diagnostics? Parse(string json){ if(string.IsNullOrWhiteSpace(json)) return null;
        var d=new Diagnostics{ unmatched=new System.Collections.Generic.List<string>(), ok=false };
        var um=Regex.Match(json,"\"unmatched\"\\s*:\\s*\\[(.*?)\\]"); if(um.Success)
          foreach(Match m in Regex.Matches(um.Groups[1].Value,"\"([^\"]+)\"")) d.unmatched.Add(m.Groups[1].Value);
        var okm=Regex.Match(json,"\"ok\"\\s*:\\s*(true|false)"); d.ok=okm.Success && okm.Groups[1].Value=="true"; return d; }
    }
  }
}
