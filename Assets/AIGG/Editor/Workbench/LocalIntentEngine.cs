// SPDX-License-Identifier: MIT
// Minimal local intent engine for Workbench.
// Reads only from Assets/AIGG/Spec and provides simple NL -> canonical JSON parsing.
// NOTE: This is a lightweight stub to restore compile; extend as needed.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Aim2Pro.AIGG.Workbench
{
    public static class LocalIntentEngine
    {
        private static readonly string[] SpecFiles = new[]
        {
            "intents.json","lexicon.json","macros.json","commands.json","fieldmap.json","registry.json","schema.json"
        };

        public static string[] GetRequiredSpecFiles() => (string[])SpecFiles.Clone();

        public static bool TryReadSpec(string specDir, out string[] present, out string[] missing)
        {
            present = new string[0];
            missing = new string[0];
            if (string.IsNullOrEmpty(specDir) || !Directory.Exists(specDir))
            {
                missing = SpecFiles;
                return false;
            }
            var have = SpecFiles.Where(f => File.Exists(Path.Combine(specDir, f))).ToArray();
            var miss = SpecFiles.Except(have).ToArray();
            present = have;
            missing = miss;
            return miss.Length == 0;
        }

        public static string Normalize(string nl)
        {
            if (string.IsNullOrWhiteSpace(nl)) return "";
            return Regex.Replace(nl.Trim().ToLowerInvariant(), @"\s+", " ");
        }

        public static string[] Tokenize(string nl)
        {
            nl = Normalize(nl);
            if (nl.Length == 0) return Array.Empty<string>();
            return nl.Split(' ');
        }

        public static bool LooksLikeScenePlan(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            var s = json.ToLowerInvariant();
            return s.Contains("\"sceneplan\"") || s.Contains("\"scenes\"") || s.Contains("\"nodes\"");
        }

        // Minimal local parse:
        // supports "<L>m by <W>m", optional "<p>% tiles missing", "<p>% gaps",
        // optional "left/right curve over <rows> rows".
        // Produces minimal canonical track JSON used by Workbench.
        public static bool TryConvertNLToCanonical(
            string nl,
            out string canonicalJson,
            out string[] unmatched,
            out string[] notes)
        {
            canonicalJson = "";
            unmatched = Array.Empty<string>();
            notes = Array.Empty<string>();

            var text = Normalize(nl);
            if (string.IsNullOrEmpty(text)) { unmatched = new[] { "empty_nl" }; return false; }

            int length = 0;
            int width = 0;
            double tileSpacing = 1.0;
            double missing = 0.0;
            double gaps = 0.0;
            string curveSide = null;
            int curveRows = 0;

            // length/width
            var mw = Regex.Match(text, @"\b(\d+)\s*m\s*(?:x|by)\s*(\d+)\s*m\b");
            if (mw.Success)
            {
                length = SafeInt(mw.Groups[1].Value);
                width  = SafeInt(mw.Groups[2].Value);
            }
            else
            {
                var ml = Regex.Match(text, @"\b(\d+)\s*m\b");
                if (ml.Success) length = SafeInt(ml.Groups[1].Value);
                var mwidth = Regex.Match(text, @"\bwidth\s*(\d+)\s*m\b");
                if (mwidth.Success) width = SafeInt(mwidth.Groups[1].Value);
            }

            // percents
            var mmiss = Regex.Match(text, @"\b(\d+)\s*%\s*(?:tiles?\s*missing|missing\s*tiles?)\b");
            if (mmiss.Success) missing = SafePercent(mmiss.Groups[1].Value);

            var mgap = Regex.Match(text, @"\b(\d+)\s*%\s*gaps?\b");
            if (mgap.Success) gaps = SafePercent(mgap.Groups[1].Value);

            // curve
            var mcurve = Regex.Match(text, @"\b(left|right)\s+curve\s+over\s+(\d+)\s+rows\b");
            if (mcurve.Success)
            {
                curveSide = mcurve.Groups[1].Value;
                curveRows = SafeInt(mcurve.Groups[2].Value);
            }
            else
            {
                var mcurve2 = Regex.Match(text, @"\bcurve\s+rows?\s+(\d+).*\b(left|right)\b");
                if (mcurve2.Success)
                {
                    curveRows = SafeInt(mcurve2.Groups[1].Value);
                    curveSide = mcurve2.Groups[2].Value;
                }
            }

            var notesList = new System.Collections.Generic.List<string>();
            var unmatchedList = new System.Collections.Generic.List<string>();

            if (length <= 0) unmatchedList.Add("length");
            if (width <= 0) { width = 3; notesList.Add("defaulted width=3"); }

            if (unmatchedList.Count > 0)
            {
                unmatched = unmatchedList.ToArray();
                notes = notesList.ToArray();
                return false;
            }

            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\"track\":{");
            sb.AppendFormat(ci, "\"length\":{0},\"width\":{1},\"tileSpacing\":{2}", length, width, tileSpacing);
            if (missing > 0) sb.AppendFormat(ci, ",\"missingTileChance\":{0}", Clamp01(missing));
            if (gaps > 0) sb.AppendFormat(ci, ",\"gapChance\":{0}", Clamp01(gaps));
            if (!string.IsNullOrEmpty(curveSide) && curveRows > 0)
            {
                sb.Append(",\"curve\":{");
                sb.AppendFormat(ci, "\"side\":\"{0}\",\"rows\":{1}", curveSide, curveRows);
                sb.Append("}");
            }
            sb.Append(",\"killzoneY\":-5");
            sb.Append("}}");

            canonicalJson = sb.ToString();
            unmatched = unmatchedList.ToArray();
            notes = notesList.ToArray();
            return true;
        }

        private static int SafeInt(string s)
        {
            if (int.TryParse(s, out var v)) return v;
            return 0;
        }

        private static double SafePercent(string s)
        {
            if (double.TryParse(s, out var v)) return v / 100.0;
            return 0.0;
        }

        private static double Clamp01(double v)
        {
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }
    }
}
