using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Aim2Pro.AIGG
{
    public static class NL_Correction
    {
        /// <summary>Fast pre-normalization: split joined number+unit (100m→100 m, 45degrees→45 degrees, 10rows→10 rows).</summary>
        public static string NormalizeBasic(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // 100m / 7m / 12cm / 3mm / 45degrees / 10rows
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\b(\d+)(m|cm|mm|meter|meters|degree|degrees|row|rows)\b", "$1 $2");
            // Normalize "45 degree" → "45 degrees"
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\b(\d+)\s*degree\b", "$1 degrees");
            return text;
        }

        private static readonly Dictionary<string,string> Map = new(StringComparer.InvariantCultureIgnoreCase)
        {
            {"trck","track"}, {"trak","track"},
            {"deg","degree"}, {"degs","degrees"},
            {"metre","meter"}, {"metres","meters"},
            {"lef","left"}, {"rigth","right"}
        };

        private static readonly string[] DomainWords = new[]
        {
            "build","track","left","right","curve",
            "degree","degrees","row","rows",
            "meter","meters","m","cm","mm",
            "by","over","across","with","without"
        };

        public sealed class Suggestion
        {
            public string Original;
            public string Replacement;
            public int StartIndex;
            public int Length;
            public string Reason;
        }

        public static List<Suggestion> Suggest(string text)
        {
            var list = new List<Suggestion>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            // --- NEW: split number+unit combos first ---
            foreach (Match m in Regex.Matches(text, @"\b(\d+)(m|cm|mm|meter|meters|degree|degrees|row|rows)\b"))
            {
                string joined = m.Value;
                string separated = $"{m.Groups[1].Value} {m.Groups[2].Value}";
                if (!text.Contains(separated))
                {
                    list.Add(new Suggestion {
                        Original = joined,
                        Replacement = separated,
                        StartIndex = m.Index,
                        Length = joined.Length,
                        Reason = "split"
                    });
                }
            }

            // --- typo + fuzzy logic ---
            int i = 0;
            while (i < text.Length)
            {
                if (!char.IsLetterOrDigit(text[i])) { i++; continue; }
                int j = i + 1;
                while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j]=='_')) j++;
                var token = text.Substring(i, j - i);

                // skip pure numbers and simple number+unit
                if (Regex.IsMatch(token, @"^\d+(\.\d+)?$") ||
                    Regex.IsMatch(token, @"^\d+(m|cm|mm)$")) { i = j; continue; }

                string rep = null, reason = null;
                if (Map.TryGetValue(token, out var mapped)) { rep = mapped; reason = "dict"; }

                if (rep == null)
                {
                    string lower = token.ToLowerInvariant();
                    if (lower == "degreee") { rep = "degree"; reason = "normalize"; }
                    else if (lower == "metre" || lower == "metres") { rep = "meter"; reason = "normalize"; }
                }

                if (rep == null)
                {
                    string best = null; int bestD = 3;
                    foreach (var cand in DomainWords)
                    {
                        int d = Lev(token.ToLowerInvariant(), cand);
                        if (d < bestD) { bestD = d; best = cand; if (d == 0) break; }
                    }
                    if (bestD > 0 && bestD <= 2 && best != null)
                    {
                        rep = best; reason = "fuzzy";
                    }
                }

                if (rep != null && !rep.Equals(token, StringComparison.InvariantCultureIgnoreCase))
                {
                    list.Add(new Suggestion { Original = token, Replacement = rep, StartIndex = i, Length = j - i, Reason = reason });
                }

                i = j;
            }

            return list;
        }

        public static string Apply(string text, List<Suggestion> suggestions)
        {
            if (string.IsNullOrEmpty(text) || suggestions == null || suggestions.Count == 0) return text;
            suggestions.Sort((a,b) => b.StartIndex.CompareTo(a.StartIndex));
            var s = text;
            foreach (var sg in suggestions)
                s = s[..sg.StartIndex] + sg.Replacement + s[(sg.StartIndex + sg.Length)..];
            return s;
        }

        private static int Lev(string a, string b)
        {
            int n=a.Length, m=b.Length;
            if (n==0) return m; if (m==0) return n;
            int[] prev=new int[m+1], cur=new int[m+1];
            for(int j=0;j<=m;j++) prev[j]=j;
            for(int i=1;i<=n;i++){
                cur[0]=i;
                for(int j=1;j<=m;j++){
                    int cost=(a[i-1]==b[j-1])?0:1;
                    int ins=cur[j-1]+1, del=prev[j]+1, sub=prev[j-1]+cost;
                    cur[j]=Math.Min(ins,Math.Min(del,sub));
                }
                (prev,cur)=(cur,prev);
            }
            return prev[m];
        }
    }
}
