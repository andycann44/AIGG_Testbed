using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Aim2Pro.AIGG.Track
{
    [Serializable] class IntentOp { public string op; public string path; public string value; }
    [Serializable] class IntentDef { public string name; public string regex; public List<IntentOp> ops; }
    [Serializable] class IntentRoot { public List<IntentDef> intents; }

    public static class AIGG_NLInterpreter
    {
        public static ScenePlan ScenePlanFromNL(string nl)
        {
            var sp = DefaultScenePlan();

            var intentsTa = LoadTa("intents"); // looks in Spec/ then Spec/NL/
            if (intentsTa == null)
            {
                Debug.LogWarning("[AIGG/NL] intents.json NOT found in Resources/Spec/ or Resources/Spec/NL/");
                return sp;
            }
            Debug.Log($"[AIGG/NL] intents.json loaded ({intentsTa.text.Length} chars)");

            if (!string.IsNullOrWhiteSpace(nl))
            {
                try
                {
                    var root = JsonUtility.FromJson<IntentRoot>(intentsTa.text);
                    if (root?.intents != null)
                    {
                        foreach (var intent in root.intents)
                        {
                            if (string.IsNullOrEmpty(intent.regex) || intent.ops == null || intent.ops.Count == 0) continue;
                            var m = Regex.Match(nl, intent.regex, RegexOptions.IgnoreCase);
                            if (!m.Success) continue;

                            foreach (var op in intent.ops)
                            {
                                if (op == null || op.op != "set" || string.IsNullOrEmpty(op.path)) continue;
                                var (path, val) = Expand(op.path, op.value, m);
                                ApplySet(sp, path, val);
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.LogWarning($"[AIGG/NL] Failed to apply intents: {ex.Message}"); }
            }
            return sp;
        }

        public static TrackSpec TrackSpecFromNL(string nl)
        {
            var dict = AiggDictionaryLoader.LoadOrDefaults();
            var sp   = ScenePlanFromNL(nl);
            var spec = ScenePlanTranslator.ToTrackSpec(sp, dict);
            if (spec == null) spec = new TrackSpec();
            return spec;
        }

        static (string path, object val) Expand(string path, string raw, Match m)
        {
            string v = raw ?? "";
            if (m != null) for (int i = 1; i < m.Groups.Count; i++) v = v.Replace($"${i}", m.Groups[i].Value);

            object boxed = v;
            if      (v.EndsWith(":int",   StringComparison.OrdinalIgnoreCase)) boxed = int.TryParse(v[..^4], out var iv) ? iv : 0;
            else if (v.EndsWith(":float", StringComparison.OrdinalIgnoreCase)) boxed = float.TryParse(v[..^6], out var fv) ? fv : 0f;
            else if (v.EndsWith(":bool",  StringComparison.OrdinalIgnoreCase)) boxed = v[..^5].Trim().ToLower() == "true";

            string clean = (path ?? "").Trim();
            if (clean.StartsWith("$."))
                clean = clean.Substring(2);

            return (clean, boxed);
        }

        static void ApplySet(ScenePlan sp, string path, object value)
        {
            if (sp == null || string.IsNullOrEmpty(path)) return;
            var p = path.Split('.');
            try
            {
                switch (p[0])
                {
                    case "name": sp.name = value?.ToString(); break;

                    case "grid":
                        if (p.Length >= 2)
                        {
                            if (p[1] == "cols") sp.grid.cols = ToInt(value);
                            else if (p[1] == "rows") sp.grid.rows = ToInt(value);
                            else if (p[1] == "dx")   sp.grid.dx   = ToFloat(value);
                            else if (p[1] == "dy")   sp.grid.dy   = ToFloat(value);
                            else if (p[1] == "origin" && p.Length >= 3)
                            {
                                if (p[2] == "x") sp.grid.origin.x = ToFloat(value);
                                if (p[2] == "y") sp.grid.origin.y = ToFloat(value);
                            }
                        }
                        break;

                    case "trackTemplate":
                        if (p.Length >= 2)
                        {
                            if (p[1] == "lanes")       sp.trackTemplate.lanes = ToInt(value);
                            else if (p[1] == "lengthUnits") sp.trackTemplate.lengthUnits = ToFloat(value);
                            else if (p[1] == "tileWidth")   sp.trackTemplate.tileWidth   = ToFloat(value);
                            else if (p[1] == "widthUnits")  sp.trackTemplate.widthUnits  = ToFloat(value);
                            else if (p[1] == "zones" && p.Length >= 4)
                            {
                                if (p[2] == "start" && p[3] == "size" && p.Length >= 5)
                                {
                                    if (p[4] == "x") sp.trackTemplate.zones.start.size.x = ToFloat(value);
                                    if (p[4] == "y") sp.trackTemplate.zones.start.size.y = ToFloat(value);
                                }
                                else if (p[2] == "end" && p[3] == "size" && p.Length >= 5)
                                {
                                    if (p[4] == "x") sp.trackTemplate.zones.end.size.x = ToFloat(value);
                                    if (p[4] == "y") sp.trackTemplate.zones.end.size.y = ToFloat(value);
                                }
                            }
                            else if (p[1] == "killZone" && p.Length >= 3)
                            {
                                if (p[2] == "y")      sp.trackTemplate.killZone.y = ToFloat(value);
                                if (p[2] == "height") sp.trackTemplate.killZone.height = ToFloat(value);
                            }
                        }
                        break;

                    case "difficulty":
                        if (p.Length >= 2)
                        {
                            if (p[1] == "tracks") sp.difficulty.tracks = ToInt(value);
                            else if (p[1] == "playerSpeed" && p.Length >= 3)
                            {
                                if (p[2] == "start")         sp.difficulty.playerSpeed.start = ToFloat(value);
                                if (p[2] == "deltaPerTrack") sp.difficulty.playerSpeed.deltaPerTrack = ToFloat(value);
                            }
                            else if (p[1] == "jumpForce") sp.difficulty.jumpForce = ToFloat(value);
                            else if (p[1] == "gapProbability" && p.Length >= 3)
                            {
                                if (p[2] == "start")         sp.difficulty.gapProbability.start = ToFloat(value);
                                if (p[2] == "deltaPerTrack") sp.difficulty.gapProbability.deltaPerTrack = ToFloat(value);
                                if (p[2] == "max")           sp.difficulty.gapProbability.max = ToFloat(value);
                            }
                            else if (p[1] == "gapRules" && p.Length >= 3)
                            {
                                if (p[2] == "noAtSpawn")       sp.difficulty.gapRules.noAtSpawn = ToBool(value);
                                if (p[2] == "noAdjacentGaps")  sp.difficulty.gapRules.noAdjacentGaps = ToBool(value);
                                if (p[2] == "maxGapWidth")     sp.difficulty.gapRules.maxGapWidth = ToInt(value);
                            }
                        }
                        break;

                    case "progression":
                        if (p.Length >= 2)
                        {
                            if (p[1] == "ordering") sp.progression.ordering = value?.ToString();
                            else if (p[1] == "carrier" && p.Length >= 3)
                            {
                                if (p[2] == "type")            sp.progression.carrier.type = value?.ToString();
                                if (p[2] == "attachKinematic") sp.progression.carrier.attachKinematic = ToBool(value);
                                if (p[2] == "moveSpeed")       sp.progression.carrier.moveSpeed = ToFloat(value);
                            }
                        }
                        break;

                    case "camera":
                        if (p.Length >= 2)
                        {
                            if (p[1] == "offsetX") sp.camera.offsetX = ToFloat(value);
                            else if (p[1] == "offsetY") sp.camera.offsetY = ToFloat(value);
                            else if (p[1] == "smooth")  sp.camera.smooth  = ToFloat(value);
                        }
                        break;
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[AIGG/NL] set '{path}' failed: {ex.Message}"); }
        }

        static int ToInt(object v)=>Convert.ToInt32(v);
        static float ToFloat(object v)=>Convert.ToSingle(v);
        static bool ToBool(object v)=> (v is bool b)? b : (v?.ToString().ToLowerInvariant()=="true");

        static TextAsset LoadTa(string name)
        {
            var ta = Resources.Load<TextAsset>($"Spec/{name}");
            if (ta == null) ta = Resources.Load<TextAsset>($"Spec/NL/{name}");
            return ta;
        }

        static ScenePlan DefaultScenePlan()
        {
            return new ScenePlan {
                type="scenePlan",
                name="AutoScene",
                grid=new GridDef{ cols=3, rows=0, dx=30, dy=18, origin=new Vec2{ x=0, y=0 } },
                trackTemplate=new TrackTemplate{
                    lanes=1, segments=new[]{"straight","straight","straight"}, lengthUnits=40f, tileWidth=1f,
                    zones=new Zones{ start=new Zone{ size=new Size{ x=2, y=3 }}, end=new Zone{ size=new Size{ x=2, y=3 }}},
                    killZone=new KillZone{ y=-5f, height=2f }
                },
                difficulty=new Difficulty{
                    tracks=3, playerSpeed=new PlayerSpeed{ start=5f, deltaPerTrack=0.35f },
                    jumpForce=9f, gapProbability=new GapProb{ start=0.02f, deltaPerTrack=0.02f, max=0.35f},
                    gapRules=new GapRules{ noAtSpawn=true, noAdjacentGaps=true, maxGapWidth=1}
                },
                progression=new Progression{ ordering="snakeRows", carrier=new Carrier{ type="dartboardTaxi", attachKinematic=true, moveSpeed=5f }},
                layers=new Layers{ track="Track", player="Player", killZone="KillZone" },
                camera=new CameraCfg{ offsetX=2f, offsetY=1f, smooth=0.15f },
                meta=new Meta()
            };
        }
    }
}
