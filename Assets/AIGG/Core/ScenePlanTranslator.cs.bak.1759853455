using UnityEngine;

namespace Aim2Pro.AIGG.Track
{
    public static class ScenePlanTranslator
    {
        /// <summary>
        /// Converts ScenePlan to TrackSpec v1.0 (straight-only for v0.2).
        /// Width logic:
        ///   - if trackTemplate.widthUnits > 0, use it
        ///   - else width = max(dict.defaultWidthM, lanes * tileWidth)
        /// Length = trackTemplate.lengthUnits (meters)
        /// Tile sizes from dict; tile width may use trackTemplate.tileWidth when > 0.
        /// </summary>
        public static TrackSpec ToTrackSpec(ScenePlan sp, AiggDictionary dict)
        {
            if (sp == null || sp.trackTemplate == null) return null;
            var tt = sp.trackTemplate;

            float lengthM = Mathf.Max(0f, tt.lengthUnits);
            float widthFromLanes = Mathf.Max(0f, tt.lanes * Mathf.Max(0.01f, tt.tileWidth));
            float widthM = tt.widthUnits > 0f ? tt.widthUnits : Mathf.Max(dict.defaultWidthM, widthFromLanes);

            var spec = new TrackSpec
            {
                version = "1.0",
                units = "m",
                length_m = lengthM,
                width_m = widthM,
                tiles_touch = true,
                tile = new TileDef
                {
                    length_m = dict.defaultTileLenM,
                    width_m = tt.tileWidth > 0f ? tt.tileWidth : dict.defaultTileWidM,
                    height_m = dict.defaultTileHeightM,
                    stretch_last_tile = dict.stretchLastTile
                },
                segments = new Segment[]
                {
                    new Segment { type = "straight", length_m = lengthM }
                }
            };
            return spec;
        }
    }
}
