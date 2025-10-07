using System;
using UnityEngine;

namespace Aim2Pro.AIGG.Track
{
    [Serializable]
    public class ScenePlan
    {
        public string type;       // "scenePlan"
        public string name;
        public GridDef grid;
        public TrackTemplate trackTemplate;
        public Difficulty difficulty;
        public Progression progression;
        public Layers layers;
        public CameraCfg camera;
        public Meta meta;

        public static ScenePlan FromJson(string json)
        {
            // allow accidental leading marker (e.g., '§') and trailing period
            if (!string.IsNullOrEmpty(json))
            {
                if (json.Length > 0 && json[0] == '§') json = json.Substring(1);
                int lastBrace = json.LastIndexOf('}');
                if (lastBrace >= 0) json = json.Substring(0, lastBrace + 1);
            }
            return JsonUtility.FromJson<ScenePlan>(json);
        }
    }

    [Serializable] public class GridDef { public int cols; public int rows; public float dx; public float dy; public Vec2 origin; }
    [Serializable] public class Vec2 { public float x; public float y; }

    [Serializable]
    public class TrackTemplate
    {
        public int lanes = 1;
        public string[] segments;        // e.g., ["straight"]
        public float lengthUnits = 100f; // meters in our translator
        public float tileWidth = 1f;     // lane tile width in meters
        public Zones zones;
        public KillZone killZone;
        public float widthUnits = 0f;    // optional (if provided we’ll use it)
    }

    [Serializable] public class Zones { public Zone start; public Zone end; }
    [Serializable] public class Zone { public Size size; }
    [Serializable] public class Size { public float x; public float y; }
    [Serializable] public class KillZone { public float y; public float height; }
    [Serializable] public class Difficulty { public int tracks; public PlayerSpeed playerSpeed; public float jumpForce; public GapProb gapProbability; public GapRules gapRules; }
    [Serializable] public class PlayerSpeed { public float start; public float deltaPerTrack; }
    [Serializable] public class GapProb { public float start; public float deltaPerTrack; public float max; }
    [Serializable] public class GapRules { public bool noAtSpawn; public bool noAdjacentGaps; public int maxGapWidth; }
    [Serializable] public class Progression { public string ordering; public Carrier carrier; }
    [Serializable] public class Carrier { public string type; public bool attachKinematic; public float moveSpeed; }
    [Serializable] public class Layers { public string track; public string player; public string killZone; }
    [Serializable] public class CameraCfg { public float offsetX; public float offsetY; public float smooth; }
    [Serializable] public class Meta { public string slope; public float slopeDegrees; public float amplitude; public float wavelength; }
}
