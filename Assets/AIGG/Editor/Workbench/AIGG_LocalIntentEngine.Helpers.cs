using System;
using System.Globalization;
namespace Aim2Pro.AIGG
{
    internal static class AIGG_LocalIntentEngineHelpers
    {
        public static int ParseInt(string s, int def = 0)
        {
            if (string.IsNullOrWhiteSpace(s)) return def;
            s = s.Trim();
            if (s.EndsWith("%")) s = s.Substring(0, s.Length-1);
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv)) return iv;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv)) return (int)Math.Round(fv);
            return def;
        }
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : (float)(double.IsNaN(v) ? 0f : v);
    }
}
