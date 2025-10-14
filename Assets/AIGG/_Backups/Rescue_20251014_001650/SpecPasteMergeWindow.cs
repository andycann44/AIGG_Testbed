using UnityEditor; using UnityEngine;
namespace Aim2Pro.AIGG {
  public class SpecPasteMergeWindow : EditorWindow {
    const string MenuPath="Window/Aim2Pro/Aigg/Paste & Merge";
    static string _incoming=""; Vector2 _scroll; string _status="Idle (stub)";
    [MenuItem(MenuPath, priority=120)] public static void Open(){ var w=GetWindow<SpecPasteMergeWindow>("Paste & Merge"); w.minSize=new Vector2(640,420); w.Show(); Debug.Log("[AIGG][Paste] Opened"); }
    public static void OpenWithJson(string json){ _incoming=json??""; Open(); Debug.Log("[AIGG][Paste] OpenWithJson payload="+(_incoming?.Length??0)); }
    void OnGUI(){
      GUILayout.Label("Incoming JSON (read-only)", EditorStyles.boldLabel);
      _scroll=EditorGUILayout.BeginScrollView(_scroll); EditorGUILayout.TextArea(_incoming??"", GUILayout.ExpandHeight(true)); EditorGUILayout.EndScrollView();
      EditorGUILayout.Space();
      using(new EditorGUILayout.HorizontalScope()){
        if(GUILayout.Button("Dry-Run", GUILayout.Height(26))){ _status=string.IsNullOrWhiteSpace(_incoming)?"No JSON.":"Would route to Spec file per manifest (stub)."; Debug.Log("[AIGG][Paste] Dry-Run -> "+_status); }
        if(GUILayout.Button("Apply (Replace)", GUILayout.Height(26))){
          if(string.IsNullOrWhiteSpace(_incoming)) _status="No JSON."; else { EditorGUIUtility.systemCopyBuffer=_incoming; _status="Stub: copied JSON to clipboard (Replace default)."; }
          Debug.Log("[AIGG][Paste] Apply -> "+_status);
        }
      }
      EditorGUILayout.HelpBox(_status, MessageType.Info);
    }
  }
}
