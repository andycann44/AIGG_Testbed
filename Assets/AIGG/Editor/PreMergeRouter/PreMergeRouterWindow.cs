// SPDX-License-Identifier: MIT
using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public class PreMergeRouterWindow : EditorWindow
    {
        private enum Tab { Intents, Lexicon, Macros, Commands, FieldMap, Registry, Schema, Other, Raw }

        private static string _incomingRaw = "";
        private int _tabIndex = 0;

        // Extracted slices
        private string _intents = "";
        private string _lexiconAdd = "";
        private string _macrosAdd = "";
        private string _commands = "";
        private string _fieldmap = "";
        private string _registry = "";
        private string _schema = "";
        private string _other = "";

        private Vector2 _scroll;

        public static void Open()
        {
            var w = GetWindow<PreMergeRouterWindow>("Pre-Merge Router");
            w.minSize = new Vector2(640, 420);
            w.Show(); w.Focus();
        }

        public static void SetInput(string json)
        {
            _incomingRaw = json ?? "";
            var w = GetWindow<PreMergeRouterWindow>("Pre-Merge Router");
            w.ParsePayload();
            w.Repaint();
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_incomingRaw)) ParsePayload();
        }

        private void OnGUI()
        {
            GUILayout.Label("Self-Heal (OpenAI) JSON -> Spec Tabs -> Paste & Merge", EditorStyles.boldLabel);

            // Raw input box
            GUILayout.Label("Input (editable):", EditorStyles.miniBoldLabel);
            var newRaw = EditorGUILayout.TextArea(_incomingRaw, GUILayout.MinHeight(80));
            if (!ReferenceEquals(newRaw, _incomingRaw))
            {
                _incomingRaw = newRaw;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Parse Now")) ParsePayload();
                if (GUILayout.Button("Copy Raw")) EditorGUIUtility.systemCopyBuffer = _incomingRaw ?? "";
                if (GUILayout.Button("Open Paste & Merge (All)")) RouteCombinedToPaste();
            }

            EditorGUILayout.Space();

            string[] tabs = new[] { "Intents", "Lexicon", "Macros", "Commands", "FieldMap", "Registry", "Schema", "Other", "Raw" };
            _tabIndex = GUILayout.Toolbar(_tabIndex, tabs);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch ((Tab)_tabIndex)
            {
                case Tab.Intents: DrawSlice("intents", _intents, WrapAsObject("intents", _intents)); break;
                case Tab.Lexicon: DrawSlice("lexicon_add", _lexiconAdd, WrapAsObject("lexicon_add", _lexiconAdd)); break;
                case Tab.Macros: DrawSlice("macros_add", _macrosAdd, WrapAsObject("macros_add", _macrosAdd)); break;
                case Tab.Commands: DrawSlice("commands", _commands, WrapAsObject("commands", _commands)); break;
                case Tab.FieldMap: DrawSlice("fieldmap", _fieldmap, WrapAsObject("fieldmap", _fieldmap)); break;
                case Tab.Registry: DrawSlice("registry", _registry, WrapAsObject("registry", _registry)); break;
                case Tab.Schema: DrawSlice("schema", _schema, WrapAsObject("schema", _schema)); break;
                case Tab.Other: DrawSlice("other", _other, _other); break;
                case Tab.Raw: DrawSlice("raw", _incomingRaw, _incomingRaw); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSlice(string key, string valueOnly, string wrappedForMerge)
        {
            GUILayout.Label(key + " slice:", EditorStyles.miniBoldLabel);
            var editable = EditorGUILayout.TextArea(valueOnly ?? "", GUILayout.ExpandHeight(true));
            if (!ReferenceEquals(editable, valueOnly))
            {
                // Update backing field by key
                AssignSlice(key, editable);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Value"))
                    EditorGUIUtility.systemCopyBuffer = editable ?? "";
                if (GUILayout.Button("Open Paste & Merge (Tab)"))
                    RouteToPaste(wrappedForMerge);
            }
        }

        private void AssignSlice(string key, string text)
        {
            switch (key)
            {
                case "intents": _intents = text; break;
                case "lexicon_add": _lexiconAdd = text; break;
                case "macros_add": _macrosAdd = text; break;
                case "commands": _commands = text; break;
                case "fieldmap": _fieldmap = text; break;
                case "registry": _registry = text; break;
                case "schema": _schema = text; break;
                case "other": _other = text; break;
                case "raw": _incomingRaw = text; break;
            }
        }

        private void RouteToPaste(string wrappedJson)
        {
            if (string.IsNullOrWhiteSpace(wrappedJson))
            {
                EditorUtility.DisplayDialog("AIGG", "Nothing to route for this tab.", "OK");
                return;
            }
            if (!PreMergeRouterAPI.TrySendToPasteMerge(wrappedJson))
            {
                EditorGUIUtility.systemCopyBuffer = wrappedJson;
                EditorUtility.DisplayDialog("AIGG", "Paste & Merge not found. JSON copied to clipboard.", "OK");
            }
        }

        private void RouteCombinedToPaste()
        {
            var combined = BuildCombinedJson();
            RouteToPaste(combined);
        }

        private string BuildCombinedJson()
        {
            // Combine only non-empty slices
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;

            void addPair(string k, string v)
            {
                if (string.IsNullOrWhiteSpace(v)) return;
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\"").Append(k).Append("\":").Append(v);
            }

            addPair("intents", _intents);
            addPair("lexicon_add", _lexiconAdd);
            addPair("macros_add", _macrosAdd);
            addPair("commands", _commands);
            addPair("fieldmap", _fieldmap);
            addPair("registry", _registry);
            addPair("schema", _schema);

            // If 'other' is a JSON object, merge its top-level pairs as-is; else ignore.
            // Minimal safe behavior: emit "other" field as raw text for visibility if nothing else exists.
            if (first && !string.IsNullOrWhiteSpace(_other))
            {
                addPair("other", _other);
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string WrapAsObject(string key, string valueJson)
        {
            if (string.IsNullOrWhiteSpace(valueJson)) return "";
            return "{\"" + key + "\":" + valueJson + "}";
        }

        private void ParsePayload()
        {
            // Reset
            _intents = _lexiconAdd = _macrosAdd = _commands = _fieldmap = _registry = _schema = _other = "";

            string j = _incomingRaw ?? "";

            // Known keys (arrays or objects). We do simple bracket-matching extraction without extra dependencies.
            _intents    = TryExtractValue("intents", j);
            if (string.IsNullOrEmpty(_intents))
            {
                // Also support { "patches": { "intents": [...] } }
                var patches = TryExtractValue("patches", j);
                if (!string.IsNullOrEmpty(patches))
                {
                    _intents = TryExtractValue("intents", patches);
                }
            }
            _lexiconAdd = TryExtractValue("lexicon_add", j);
            _macrosAdd  = TryExtractValue("macros_add", j);
            _commands   = TryExtractValue("commands", j);
            _fieldmap   = TryExtractValue("fieldmap", j);
            _registry   = TryExtractValue("registry", j);
            _schema     = TryExtractValue("schema", j);

            // Whatever remains that we didn't classify goes into "Other" view for manual copy.
            // For simplicity, show raw.
            _other = "";
        }

        private static string TryExtractValue(string key, string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var needle = "\"" + key + "\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return "";
            i = json.IndexOf(':', i + needle.Length);
            if (i < 0) return "";
            // Skip whitespace
            while (i + 1 < json.Length && char.IsWhiteSpace(json[i + 1])) i++;
            if (i + 1 >= json.Length) return "";
            int start = i + 1;
            char c = json[start];
            if (c == '[')
                return ExtractBracket(json, start, '[', ']');
            if (c == '{')
                return ExtractBracket(json, start, '{', '}');
            // Primitive (string/number/bool) until comma or end/bracket
            int end = start;
            while (end < json.Length && ",}]".IndexOf(json[end]) == -1) end++;
            return json.Substring(start, end - start).Trim();
        }

        private static string ExtractBracket(string s, int start, char open, char close)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = start; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '"' && (i == 0 || s[i - 1] != '\\')) inStr = !inStr;
                if (inStr) continue;
                if (ch == open) depth++;
                else if (ch == close)
                {
                    depth--;
                    if (depth == 0) return s.Substring(start, i - start + 1);
                }
            }
            return "";
        }
    }
}
