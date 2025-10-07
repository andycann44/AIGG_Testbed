#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// Workbench with proper DTO-based canonical JSON writer
namespace Aim2Pro.AIGG {
  public class WorkbenchWindow : EditorWindow {
    // Injected: NL input state
    string nlText = "";

    [Serializable] class Resp { public string mode; public string test; public string input; public string timestamp; }

    // Canonical DTOs (Unity-serializable)
    [Serializable] class CanonicalSpec {
      public string type="scenePlan";
      public string name="Quick Plan";
      public Grid grid=new Grid();
      public TrackTemplate trackTemplate=new TrackTemplate();
      public Difficulty difficulty=new Difficulty();
      public Progression progression=new Progression();
      public Layers layers=new Layers();
      public CameraCfg camera=new CameraCfg();
      public Meta meta=new Meta();
    }
    [Serializable] class Grid { public int cols=1, rows=1; public float dx=30, dy=18; public Vec2 origin=new Vec2(); }
    [Serializable] class Vec2 { public float x=0, y=0; }
    [Serializable] class TrackTemplate {
      public int lanes=1;
      public string[] segments=new string[]{"straight"};
      public float lengthUnits=14;
      public float tileWidth=1;
      public Zones zones=new Zones();
      public KillZone killZone=new KillZone();
    }
    [Serializable] class Zones { public Zone start=new Zone(); public Zone end=new Zone(); }
    [Serializable] class Zone { public Vec2 size=new Vec2{ x=2, y=3 }; }
    [Serializable] class KillZone { public float y=-5, height=2; }
    [Serializable] class Difficulty {
      public int tracks=1;
      public PlayerSpeed playerSpeed=new PlayerSpeed();
      public float jumpForce=9;
      public GapProbability gapProbability=new GapProbability();
      public GapRules gapRules=new GapRules();
    }
    [Serializable] class PlayerSpeed { public float start=6, deltaPerTrack=0.35f; }
    [Serializable] class GapProbability { public float start=0.02f, deltaPerTrack=0.02f, max=0.35f; }
    [Serializable] class GapRules { public bool noAtSpawn=true, noAdjacentGaps=true; public int maxGapWidth=1; }
    [Serializable] class Progression { public string ordering="rowMajor"; public Carrier carrier=new Carrier(); }
    [Serializable] class Carrier { public string type="dartboardTaxi"; public bool attachKinematic=true; public float moveSpeed=7; }
    [Serializable] class Layers { public string track="Track", player="Player", killZone="KillZone"; }
    [Serializable] class CameraCfg { public float offsetX=2, offsetY=1, smooth=0.15f; }
    [Serializable] class Meta { public string slope="flat"; public float slopeDegrees=0, amplitude=1.5f, wavelength=6f; }

    string nlInput = "";
    string jsonOut = "{}";

    [MenuItem("Window/Aim2Pro/Workbench/Workbench")]
    public static void Open(){ var w=GetWindow<WorkbenchWindow>("Workbench"); w.minSize=new Vector2(640,420); }

    void OnGUI(){
        // Injected: NL prompt UI
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Natural language prompt", EditorStyles.boldLabel);
        nlText = EditorGUILayout.TextArea(nlText, GUILayout.MinHeight(80));

        if (GUILayout.Button("Parse NL (Intents)")) {
            try {
                var json = Aim2Pro.AIGG.Local.IntentRunner.RunFromNL(nlText);
                
  { var fld = this.GetType().GetField("jsonSpecInline",
      (System.Reflection.BindingFlags)(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public));
    if (fld != null) { fld.SetValue(this, json); Repaint(); }
    else { UnityEditor.EditorGUIUtility.systemCopyBuffer = json; Debug.Log("AIGG: JSON copied to clipboard — paste into jsonSpecInline"); } }
                Debug.Log("AIGG: NL -> intents -> scenePlan set in jsonSpecInline");
            } catch (Exception e) { Debug.LogError("AIGG intents parse error: "+e.Message); }
        }

      var s=AIGGSettings.LoadOrCreate(); if(!s){ EditorGUILayout.HelpBox("Missing AIGG Settings.", MessageType.Error); return; }
      GUILayout.Label("AIGG Workbench", EditorStyles.boldLabel);

      using(new EditorGUILayout.HorizontalScope()){
        EditorGUILayout.LabelField("Mode", GUILayout.Width(50));
        s.mode=(AIGGMode)EditorGUILayout.EnumPopup(s.mode, GUILayout.Width(150));
        GUILayout.Space(10);
        if(GUILayout.Button("AI Test", GUILayout.Width(110))){
          var r=new Resp{ mode=s.mode.ToString(), test=(s.mode==AIGGMode.LOCAL?"hello Andy":"hello Andy from AIGG"), input=(nlInput??"").Trim(), timestamp=DateTime.UtcNow.ToString("o") };
          jsonOut=JsonUtility.ToJson(r,true);
          Debug.Log($"AIGG/WB: AI Test → {r.test} (mode={r.mode})");
        }
        if(GUILayout.Button("API Settings…", GUILayout.Width(120))) ApiSettingsWindow.Open();
        GUILayout.FlexibleSpace();
      }

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Natural Language Input");
      nlInput = EditorGUILayout.TextArea(nlInput, GUILayout.MinHeight(120));

      EditorGUILayout.Space();
      using(new EditorGUILayout.HorizontalScope()){
        if(GUILayout.Button("Make Canonical", GUILayout.Height(26), GUILayout.Width(160))){
          var canonical = BuildCanonicalDTO(nlInput);
          var json = JsonUtility.ToJson(canonical, true);
          jsonOut = json;
          WriteCanonical(json);
        }
        if(GUILayout.Button("Reveal Canonical", GUILayout.Height(26), GUILayout.Width(160))){
          var p = CanonicalPath(); if(File.Exists(p)) EditorUtility.RevealInFinder(p); else Debug.LogWarning("AIGG/WB: canonical not found: "+p);
        }
        GUILayout.FlexibleSpace();
      }

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("JSON Output");
      using(new EditorGUI.DisabledScope(true)){
        jsonOut = EditorGUILayout.TextArea(string.IsNullOrEmpty(jsonOut)?"{}":jsonOut, GUILayout.MinHeight(160));
      }
      EditorGUILayout.HelpBox("Make Canonical writes StickerDash_Status/LastCanonical.json.\nThen use Scene Creator → Build From Canonical.", MessageType.Info);
    }

    static string CanonicalPath(){ var root=Directory.GetParent(Application.dataPath).FullName; return Path.Combine(root,"StickerDash_Status/LastCanonical.json"); }
    static void WriteCanonical(string json){ var p=CanonicalPath(); Directory.CreateDirectory(Path.GetDirectoryName(p)); File.WriteAllText(p,json,Encoding.UTF8); Debug.Log("AIGG/WB: wrote canonical → "+p); }

    static CanonicalSpec BuildCanonicalDTO(string nl){
      nl=(nl??"").ToLowerInvariant();
      var spec=new CanonicalSpec();

      // length by seconds or meters
      float lengthUnits=14f;
      var mSec=Regex.Match(nl,@"(\d+(?:\.\d+)?)\s*(?:s|sec|secs|seconds)\b");
      var mSpd=Regex.Match(nl,@"(?:speed|at)\s*(\d+(?:\.\d+)?)\s*(?:u/s|m/s|units/s|units/sec)");
      var mMet=Regex.Match(nl,@"(\d+(?:\.\d+)?)\s*(?:m|meter|metre|meters|metres|units)\b");
      if(mSec.Success){ var secs=float.Parse(mSec.Groups[1].Value); var spd=mSpd.Success?float.Parse(mSpd.Groups[1].Value):6f; lengthUnits=Mathf.Max(1f,secs*spd); }
      else if(mMet.Success){ lengthUnits=Mathf.Max(1f,float.Parse(mMet.Groups[1].Value)); }
      spec.trackTemplate.lengthUnits = Mathf.Round(lengthUnits);

      // slope / hills
      string slope="flat"; float slopeDeg=0f, amp=1.5f, wave=6f;
      if(Regex.IsMatch(nl,@"\b(ramp up|uphill|incline|slope up)\b")){ slope="rampUp"; var d=Regex.Match(nl,@"(\d+(?:\.\d+)?)\s*deg"); slopeDeg=d.Success?float.Parse(d.Groups[1].Value):8f; }
      else if(Regex.IsMatch(nl,@"\b(ramp down|downhill|decline|slope down)\b")){ slope="rampDown"; var d=Regex.Match(nl,@"(\d+(?:\.\d+)?)\s*deg"); slopeDeg=d.Success?float.Parse(d.Groups[1].Value):8f; }
      else if(Regex.IsMatch(nl,@"\b(hills|rolling|wavy|waves|undulating)\b")){ slope="hills"; var a=Regex.Match(nl,@"amp(?:litude)?\s*(\d+(?:\.\d+)?)"); var w=Regex.Match(nl,@"wave(?:length)?\s*(\d+(?:\.\d+)?)"); if(a.Success) amp=float.Parse(a.Groups[1].Value); if(w.Success) wave=float.Parse(w.Groups[1].Value); }

      spec.trackTemplate.segments = slope=="rampUp" ? new[]{"rampUp"} : slope=="rampDown" ? new[]{"rampDown"} : slope=="hills" ? new[]{"hills"} : new[]{"straight"};
      spec.meta.slope = slope; spec.meta.slopeDegrees = slopeDeg; spec.meta.amplitude = amp; spec.meta.wavelength = wave;

      return spec;
    }
  }
}
#endif
