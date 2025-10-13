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
        private List<string> _matched = new List<string>();
        private List<string> _unmatched = new List<string>();
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

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan", GUILayout.Height(24))) ScanNow();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Matched: " + _matched.Count + "    Unmatched: " + _unmatched.Count, GUILayout.Width(220));
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(position.width/2 - 8)))
                {
                    EditorGUILayout.LabelField("Matched", EditorStyles.boldLabel);
                    foreach (var m in _matched) EditorGUILayout. " + m);LabelField("
                }
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Unmatched (click to inspect)", EditorStyles.boldLabel);

                    // clickable list
                    for (int i=0;i<_unmatched.Count;i++)
                    {
                        var isSel = i == _selectedUnmatched;
                        var style = isSel ? EditorStyles.whiteLabel : EditorStyles.label;
                        if (GUILayout. " + _unmatched[i], style))Button("
                            _selectedUnmatched = i;
                    }

                    if (_selectedUnmatched >= 0 && _selectedUnmatched < _unmatched.Count)
                    {
                        var u = _unmatched[_selectedUnmatched];
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Token: " + u, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Suggested regex:", EditorStyles.miniBoldLabel);
                        EditorGUILayout.TextField("\\b" + Regex.Escape(u) + "\\b");
                    }
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_unmatched.Count == 0))
                {
                    if (GUILayout.Button("Self-Fix (Local Patch)", GUILayout.Height(26)))
                        PreMergeRouterAPI.Route(BuildLocalPatchJSON(_unmatched));
 Router)", GUILayout.Height(26)))
                        PreMergeRouterAPI.Route(BuildAIEnvelope(_nl, _unmatched));
                }
            }
        }

        // ===== scan helpers =====
        private void ScanNow()
        {
            _matched.Clear(); _unmatched.Clear();
            var text = (_nl ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(text)) return;

            var lex = LoadLexicon();
            var rx = LoadIntentRegexes();
            foreach (Match m in Regex.Matches(text, "[a-z0-9]+"))
            {
                var w = m.Value;
                if (IsStop(w)) { _matched.Add(w); continue; }
                bool ok = lex.Contains(w);
                if (!ok)
                {
                    foreach (var pat in rx)
                    {
                        try
                        {
                            var one = pat.Replace("\\\\","\\");
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
                var p = Path.Combine(SpecDir, "lexicon.json");
                if (File.Exists(p))
                    foreach (Match m in Regex.Matches(File.ReadAllText(p), "\"([^\"]+)\""))
                        set.Add(m.Groups[1].Value.Trim());
            } catch {}
            return set;
        }

        private static List<string> LoadIntentRegexes()
        {
            var list = new List<string>();
            try
            {
                var p = Path.Combine(SpecDir, "intents.json");
                if (File.Exists(p))
                    foreach (Match m in Regex.Matches(File.ReadAllText(p), "\"regex\"\\s*:\\s*\"([^\"]+)\""))
                        list.Add(m.Groups[1].Value.Trim());
            } catch {}
            return list;
        }

        private static string BuildLocalPatchJSON(List<string> unmatched)
        {
            var items = new List<string>();
            foreach (var u in unmatched)
            {
                if (string.IsNullOrWhiteSpace(u)) continue;
                var rx="\\\\b"+Regex.Escape(u)+"\\\\b";
                items.Add("{\"name\":\"auto-"+u+"\",\"regex\":\""+rx+"\",\"ops\":[{\"op\":\"custom\",\"path\":\"$.\",\"value\":\"define-"+u+"\"}]}");
            }
            return "{ \"intents\": ["+string.Join(",", items)+"] }";
        }

        private static string BuildAIEnvelope(string nl, List<string> unmatched)
        {
            string esc(string s)=> (s??"").Replace("\\","\\\\").Replace("\"","\\\"");
            var inner = string.Join(",", (unmatched??new List<string>()).ConvertAll(q=>"\""+esc(q)+"\""));
            return "{\"nl\":\""+esc(nl)+"\",\"issues\":{\"unmatched\":["+inner+"]},\"request\":\"propose intents\",\"schema\":\"intents\"}";
        }
    }
}
