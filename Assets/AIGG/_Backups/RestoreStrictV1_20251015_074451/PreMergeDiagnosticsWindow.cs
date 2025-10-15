// ASCII only
using UnityEditor; using UnityEngine; using System.Collections.Generic;
namespace Aim2Pro.AIGG {
  public class PreMergeDiagnosticsWindow : EditorWindow {
    private string reason="", nl="", canonical="", diagnostics=""; private List<string> unmatched=new List<string>();
    public static void Show(string reason,string nl,string canonical,string diagnostics,object diagObj){
      var w=GetWindow<PreMergeDiagnosticsWindow>(); w.titleContent=new GUIContent("Pre-Merge (Blocked)");
      w.reason=reason??""; w.nl=nl??""; w.canonical=canonical??""; w.diagnostics=diagnostics??"";
      w.unmatched=new List<string>(); if(diagObj!=null){ var t=diagObj.GetType(); var f=t.GetField("unmatched");
        if(f!=null){ var v=f.GetValue(diagObj) as System.Collections.IEnumerable; if(v!=null) foreach(var x in v) w.unmatched.Add(""+x);} }
      w.ShowUtility();
    }
    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge Diagnostics")] public static void OpenEmpty(){ Show("Manual open","","","",null); }
    void OnGUI(){
      GUILayout.Label("Pre-Merge STRICT BLOCK",EditorStyles.boldLabel);
      EditorGUILayout.HelpBox(reason,MessageType.Warning);
      if(unmatched.Count>0){ GUILayout.Label("Unmatched NL tokens:"); foreach(var u in unmatched) GUILayout.Label("• "+u); }
      GUILayout.Space(6); GUILayout.Label("NL (normalized):"); EditorGUILayout.TextArea(nl,GUILayout.MinHeight(40));
      GUILayout.Label("Diagnostics (raw):"); EditorGUILayout.TextArea(diagnostics,GUILayout.MinHeight(60));
      GUILayout.Label("Canonical (blocked):"); EditorGUILayout.TextArea(canonical,GUILayout.MinHeight(100));
    }
  }
}
