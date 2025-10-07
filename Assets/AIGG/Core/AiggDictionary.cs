using System;
using UnityEngine;

namespace Aim2Pro.AIGG.Track
{
    [Serializable]
    public class AiggDictionary
    {
        public float defaultWidthM = 3f;
        public float defaultTileLenM = 1f;
        public float defaultTileWidM = 1f;
        public float defaultTileHeightM = 0.2f;
        public bool stretchLastTile = true;
    }

    public static class AiggDictionaryLoader
    {
        public static AiggDictionary LoadOrDefaults()
        {
            var ta = Resources.Load<TextAsset>("Spec/Dictionary/AIGG_Dictionary");
            if (ta == null) return new AiggDictionary();
            try
            {
                return JsonUtility.FromJson<AiggDictionary>(ta.text) ?? new AiggDictionary();
            }
            catch
            {
                Debug.LogWarning("AIGG_Dictionary.json parse failed, using defaults.");
                return new AiggDictionary();
            }
        }
    }
}
