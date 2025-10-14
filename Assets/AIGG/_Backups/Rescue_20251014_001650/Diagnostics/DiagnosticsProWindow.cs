using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public class DiagnosticsProWindow : EditorWindow
    {
        private string _nl = "";
        private readonly List<string> _matched = new List<string>();
        private readonly List<string> _unmatched = new List<string>();
        private int _selectedUnmatched = -1;

        private static readonly string SpecDir = "Assets/AIGG/Spec";

        [MenuItem("Window/Aim2Pro/Aigg/Diagnostics Pro", priority = 1850)]
        public static void ShowWindow()
        {
            var w = GetWindow<DiagnosticsProWindow>("Diagnostics");
            w.minSize = new Vector2(720, 520);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Natural Language", EditorStyles.boldLabel);
            _nl = EditorGUILayout.TextArea(_nl, GUILayout.Height(100));

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan", GUILayout.Height(24))) { ScanNow(); }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Matched: " + _matched.Count + "    Unmatched: " + _unmatched.Count, GUILayout.Width(260));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical("box", GUILayout.Width(position.width * 0.48f));
            EditorGUILayout.LabelField("Matched", EditorStyles.boldLabel);
            for (int i = 0; i < _matched.Count; i++) { EditorGUILayout.LabelField("• " + _matched[i]); }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Unmatched (click to inspect)", EditorStyles.boldLabel);
            for (int i = 0; i < _unmatched.Count; i++)
            {
                if (GUILayout.Button("• " + _unmatched[i], GUILayout.Height(20))) { _selectedUnmatched = i; }
            }
            if (_selectedUnmatched >= 0 && _selectedUnmatched < _unmatched.Count)
            {
                string u = _unmatched[_selectedUnmatched];
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Token: " + u, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Suggested regex:", EditorStyles.miniBoldLabel);
                EditorGUILayout.TextField("\\b" + Regex.Escape(u) + "\\b");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            bool disable = _unmatched.Count == 0;
            using (new EditorGUI.DisabledScope(disable))
            {
                if (GUILayout.Button("Self-Fix (Local Patch)", GUILayout.Height(26)))
                    PreMergeRouterAPI.Route(BuildLocalPatchJSON(_unmatched));
                if (GUILayout.Button("Self-Fix (AI -> Router)", GUILayout.Height(26)))
                    PreMergeRouterAPI.Route(BuildAIEnvelope(_nl, _unmatched));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ScanNow()
        {
            _matched.Clear(); _unmatched.Clear();
            string text = (_nl ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(text)) return;

            HashSet<string> lex = LoadLexicon();
            List<string> rx = LoadIntentRegexes();

            foreach (Match m in Regex.Matches(text, "[a-z0-9]+"))
            {
                string w = m.Value;
                if (IsStop(w)) { _matched.Add(w); continue; }

                bool ok = lex.Contains(w);
                if (!ok)
                {
                    for (int i = 0; i < rx.Count; i++)
                    {
                        string pat = rx[i];
                        try
                        {
                            string one = pat.Replace("\\\\", "\\");
                            if (Regex.IsMatch(text, one, RegexOptions.IgnoreCase)) { ok = true; break; }
                        } catch {}
                    }
                }
                if (ok) _matched.Add(w); else _unmatched.Add(w);
            }
        }

        private static bool IsStop(string w)
        {
            switch (w)
            {
                case "a": case "an": case "the": case "and": case "or": case "to": case "of":
                case "by": case "over": case "across": case "with": case "without": case "for":
                case "in": case "on": case "at": case "is": case "are": case "left": case "right":
                case "deg": case "degree": case "degrees": case "m": case "meter": case "meters":
                case "metre": case "metres": case "mm": case "cm": case "percent": case "percentage":
                    return true;
            }
            return false;
        }

        private static HashSet<string> LoadLexicon()
        {
            var set = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            try
            {
                string p = Path.Combine(SpecDir, "lexicon.json");
                if (File.Exists(p))
                {
                    string s = File.ReadAllText(p);
                    foreach (Match m in Regex.Matches(s, "\"([^\"]+)\""))
                    {
                        string v = m.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(v)) set.Add(v);
                    }
                }
            } catch {}
            return set;
        }

        private static List<string> LoadIntentRegexes()
        {
            var list = new List<string>();
            try
            {
                string p = Path.Combine(SpecDir, "intents.json");
                if (File.Exists(p))
                {
                    string s = File.ReadAllText(p);
                    foreach (Match m in Regex.Matches(s, "\"regex\"\\s*:\\s*\"([^\"]+)\""))
                    {
                        string v = m.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(v)) list.Add(v);
                    }
                }
            } catch {}
            return list;
        }

        private static string BuildLocalPatchJSON(List<string> unmatched)
        {
            var items = new List<string>();
            for (int i = 0; i < unmatched.Count; i++)
            {
                string u = unmatched[i];
                if (string.IsNullOrWhiteSpace(u)) continue;
                string rx = "\\\\b" + Regex.Escape(u) + "\\\\b";
                items.Add("{\"name\":\"auto-" + u + "\",\"regex\":\"" + rx + "\",\"ops\":[{\"op\":\"custom\",\"path\":\"$.\",\"value\":\"define-" + u + "\"}]}");
            }
            return "{ \"intents\": [" + string.Join(",", items.ToArray()) + "] }";
        }

        private static string BuildAIEnvelope(string nl, List<string> unmatched)
        {
            string escapedNl = Esc(nl);
            var q = new List<string>();
            for (int i = 0; i < unmatched.Count; i++) q.Add("\"" + Esc(unmatched[i]) + "\"");
            return "{\"nl\":\"" + escapedNl + "\",\"issues\":{\"unmatched\":[" + string.Join(",", q.ToArray()) + "]},\"request\":\"propose intents\",\"schema\":\"intents\"}";
        }

        private static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
