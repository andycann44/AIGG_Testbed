// AIGG_LocalIntentEngine.Helpers.cs
// Provides ParseInt and Clamp01 used inside AIGG_LocalIntentEngine.
using System;
using System.Globalization;

namespace Aim2Pro.AIGG
{
    internal static class AIGG_LocalIntentEngineHelpers
    {
        // Safe int parse: trims, handles null/empty, decimal-like strings, and defaults.
        public static int ParseInt(string s, int @default = 0)
        {
            if (string.IsNullOrWhiteSpace(s)) return @default;
            s = s.Trim();
            // Handle values like "15%" or "10.0"
            if (s.EndsWith("%")) s = s.Substring(0, s.Length - 1);
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                return iv;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv))
                return (int)Math.Round(fv);
            return @default;
        }

        // Clamp to [0,1] for probability-like inputs (e.g., 0..1 or 0..100%)
        public static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 0f;
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
