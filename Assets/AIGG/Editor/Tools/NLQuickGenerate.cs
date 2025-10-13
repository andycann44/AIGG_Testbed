#if UNITY_EDITOR
using UnityEditor; using UnityEngine; using Aim2Pro.AIGG.Generators;
namespace Aim2Pro.AIGG.Tools {
  public class NLQuickGenerate : EditorWindow {
    string _nl="120 m by 3 m; curve rows 10-20 left 15 deg; spikes every 10 m", _json=""; Vector2 _s1,_s2;
    [MenuItem("Window/Aim2Pro/NL Quick Generate (Fallback)")]
    static void Open(){ var w=GetWindow<NLQuickGenerate>("NL Quick Generate"); w.minSize=new Vector2(640,420); w.Show(); }
    void OnGUI(){
      GUILayout.Label("Natural language", EditorStyles.boldLabel);
      _s1=EditorGUILayout.BeginScrollView(_s1,GUILayout.Height(120)); _nl=EditorGUILayout.TextArea(_nl,GUILayout.ExpandHeight(true)); EditorGUILayout.EndScrollView();
      using(new EditorGUILayout.HorizontalScope()){
        if(GUILayout.Button("Generate JSON")){
          _json = NLToJson.GenerateFromPrompt(_nl) ?? "";
          if(!string.IsNullOrEmpty(_json)){ System.IO.Directory.CreateDirectory("Assets/AIGG/Test"); System.IO.File.WriteAllText("Assets/AIGG/Test/AIGG_TrackCreator_NL_canonical.json",_json); AssetDatabase.Refresh(); ShowNotification(new GUIContent("Wrote AIGG/Test/AIGG_TrackCreator_NL_canonical.json")); }
          else ShowNotification(new GUIContent("No match — check NL format"));
        }
        if(GUILayout.Button("Open Output Folder")){ System.IO.Directory.CreateDirectory("Assets/AIGG/Test"); EditorUtility.RevealInFinder("Assets/AIGG/Test"); }
      }
      GUILayout.Label("Canonical JSON", EditorStyles.boldLabel);
      _s2=EditorGUILayout.BeginScrollView(_s2); EditorGUILayout.TextArea(_json,GUILayout.ExpandHeight(true)); EditorGUILayout.EndScrollView();
    }
  }
}
#endif
