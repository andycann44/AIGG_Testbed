// Editor-only: NL -> TrackSpec JSON
#if UNITY_EDITOR
using UnityEngine;
using Aim2Pro.AIGG.Track;

namespace Aim2Pro.AIGG.Generators
{
    public static class NLToJson
    {
        public static string GenerateTrackJson(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                Debug.LogWarning("[AIGG/NLToJson] Empty prompt"); 
                return null;
            }

            Debug.Log($"[AIGG/NLToJson] RAW NL:\n{prompt}");

            TrackSpec spec = null;
            try
            {
                spec = AIGG_NLInterpreter.TrackSpecFromNL(prompt);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[AIGG/NLToJson] Interpreter failed: " + ex.Message);
            }

            if (spec == null)
            {
                spec = new TrackSpec
                {
                    length_m = 120f, width_m = 4f, tiles_touch = true,
                    tile = new TileDef { length_m = 1f, width_m = 1f, height_m = 0.2f, stretch_last_tile = true }
                };
                Debug.LogWarning("[AIGG/NLToJson] Using fallback TrackSpec.");
            }

            var json = JsonUtility.ToJson(spec, true);
            Debug.Log("[AIGG/NLToJson] JSON len=" + json.Length);
            return json;
        }
    }
}
#endif
