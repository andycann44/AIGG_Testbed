using System;
using UnityEngine;

namespace Aim2Pro.AIGG.Track
{
    [Serializable]
    public class TrackSpec
    {
        public string version = "1.0";
        public string units = "m";
        public float length_m = 100f; // along +Z
        public float width_m  = 3f;   // along +X
        public bool tiles_touch = true;

        public TileDef tile = new TileDef();
        public Segment[] segments = null; // v0.1: optional; straight if null

        public static TrackSpec FromJson(string json) => JsonUtility.FromJson<TrackSpec>(json);
    }

    [Serializable]
    public class TileDef
    {
        public float length_m = 1f;   // Z size of a base tile
        public float width_m  = 1f;   // X size of a base tile
        public float height_m = 0.2f; // Y size
        public string prefab = null;  // (unused in v0.1 – use window field)
        public bool stretch_last_tile = true; // ensures exact fit with touching tiles
    }

    [Serializable]
    public class Segment
    {
        public string type = "straight"; // future: "left","right","up","down","gap"
        public float length_m = 0f;      // straight length
        public float curve_radius_m = 0f;
        public float slope_deg = 0f;
        public float gap_m = 0f;
    }
}
