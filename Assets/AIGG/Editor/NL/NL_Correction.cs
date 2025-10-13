using System;
using System.Collections.Generic;

namespace Aim2Pro.AIGG
{
    // Small, fast, Editor-safe corrector for common NL mistakes in our domain.
    // - Fixes common typos via dictionary
    // - Fuzzy fixes via Levenshtein (distance 1–2) against domain lexicon
    // - Normalizes units and plurals (degree(s), m/meters, cm, etc.)
    public static class NL_Correction
    {
        private static readonly Dictionary<string,string> Map = new Dictionary<string,string>(StringComparer.InvariantCultureIgnoreCase)
        {
            {"trck","track"},
            {"trk","track"},
            {"deg","degree"},
            {"degs","degrees"},
            {"metre","meter"},
            {"metres","meters"},
            {"cm.","cm"},
            {"mm.","mm"},
            {"curv","curve"},
            {"lef","left"},
            {"rigth","right"},
            {"over","over"},
        };

        private static readonly string[] DomainWords = new string[]
        {
            // Extend as needed (pull from lexicon later if desired)
            "build","track","left","right","curve","degree","degrees","row","rows",
            "meter","meters","m","cm","mm","by","over","across","with","without"
        };

        public sealed class Suggestion
        {
            public string Original;
            public string Replacement;
            public int StartIndex;
            public int Length;
            public string Reason; // "dict" | "fuzzy" | "normalize"
        }

        public static List<Suggestion> Suggest(string text)
        {
            var list = new List<Suggestion>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            int i = 0;
            while (i < text.Length)
            {
                // simple token scan: letters/digits
                if (char.IsLetterOrDigit(text[i]))
                {
                    int j = i + 1;
                    while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j]=='_')) j++;
                    var token = text.Substring(i, j - i);

                    string rep = null;
                    string reason = null;

                    // 1) dictionary
                    if (Map.TryGetValue(token, out var mapped))
                    {
                        rep = mapped;
                        reason = "dict";
                    }

                    // 2) normalize units/plurals
                    if (rep == null)
                    {
                        var lower = token.ToLowerInvariant();
                        if (lower == "degree") { rep = "degree"; reason = "normalize"; }
                        else if (lower == "degrees" || lower == "degreee") { rep = "degrees"; reason = "normalize"; }
                        else if (lower == "mtr" || lower == "metre") { rep = "meter"; reason = "normalize"; }
                        else if (lower == "metres") { rep = "meters"; reason = "normalize"; }
                        else if (lower == "cm" || lower == "mm" || lower == "m") { rep = lower; reason = "normalize"; }
                    }

                    // 3) fuzzy to domain words (distance <= 2)
                    if (rep == null)
                    {
                        string best = null; int bestD = 3;
                        for (int k = 0; k < DomainWords.Length; k++)
                        {
                            var cand = DomainWords[k];
                            int d = Lev(token.ToLowerInvariant(), cand);
                            if (d < bestD) { bestD = d; best = cand; if (d == 0) break; }
                        }
                        if (bestD > 0 && bestD <= 2 && best != null)
                        {
                            rep = best; reason = "fuzzy";
                        }
                    }

                    if (rep != null && !string.Equals(rep, token, StringComparison.InvariantCultureIgnoreCase))
                    {
                        list.Add(new Suggestion {
                            Original = token,
                            Replacement = rep,
                            StartIndex = i,
                            Length = j - i,
                            Reason = reason
                        });
                    }

                    i = j;
                }
                else
                {
                    i++;
                }
            }

            // Unit patterns like "45 degree" -> "45 degrees"
            // scan naive pattern: number + "degree"
            for (int pos = 0; pos < text.Length; pos++)
            {
                if (!char.IsDigit(text[pos])) continue;
                int s = pos;
                while (pos < text.Length && char.IsDigit(text[pos])) pos++;
                // skip space
                int k2 = pos;
                while (k2 < text.Length && char.IsWhiteSpace(text[k2])) k2++;
                string tail = TakeWord(text, k2);
                if (tail == "degree")
                {
                    list.Add(new Suggestion {
                        Original = "degree",
                        Replacement = "degrees",
                        StartIndex = k2,
                        Length = "degree".Length,
                        Reason = "normalize"
                    });
                }
            }

            return list;
        }

        public static string Apply(string text, List<Suggestion> suggestions)
        {
            if (string.IsNullOrEmpty(text) || suggestions == null || suggestions.Count == 0) return text;
            // apply from end to start to keep indexes valid
            suggestions.Sort((a,b) => (b.StartIndex.CompareTo(a.StartIndex)));
            var s = text;
            foreach (var sg in suggestions)
            {
                s = s.Substring(0, sg.StartIndex) + sg.Replacement + s.Substring(sg.StartIndex + sg.Length);
            }
            return s;
        }

        private static string TakeWord(string text, int start)
        {
            int i = start;
            while (i < text.Length && char.IsLetter(text[i])) i++;
            return i > start ? text.Substring(start, i - start).ToLowerInvariant() : "";
        }

        // Very small Levenshtein for short tokens
        private static int Lev(string a, string b)
        {
            int n=a.Length, m=b.Length;
            if (n==0) return m; if (m==0) return n;
            int[] prev = new int[m+1], cur = new int[m+1];
            for (int j=0;j<=m;j++) prev[j]=j;
            for (int i=1;i<=n;i++)
            {
                cur[0]=i;
                for (int j=1;j<=m;j++)
                {
                    int cost = (a[i-1]==b[j-1]) ? 0 : 1;
                    int ins = cur[j-1]+1;
                    int del = prev[j]+1;
                    int sub = prev[j-1]+cost;
                    int v = ins<del?ins:del;
                    cur[j] = v<sub?v:sub;
                }
                var tmp=prev; prev=cur; cur=tmp;
            }
            return prev[m];
        }
    }
}
