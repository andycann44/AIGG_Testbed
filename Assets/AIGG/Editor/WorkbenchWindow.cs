#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace Aim2Pro.AIGG
{
    /// <summary>
    /// Workbench (local-only): NL → (NLToJson | AIGG_NLInterpreter) → JSON
    /// Diagnostics show matched/unmatched tokens by SPEC source (intents/macros/lexicon/commands).
    /// No OpenAI calls here per plan.
    /// </summary>
    public class WorkbenchWindow : EditorWindow
    {
        [SerializeField] private string _prompt = "";
        [SerializeField] private string _diagnostics = "";
        [SerializeField] private string _json = "";
        [SerializeField] private string _selectedUnmatched = "";
        [SerializeField] private Vector2 _scrollPrompt, _scrollDiag, _scrollJson;

        // Cached reflection
        static readonly Dictionary<(string,string,bool), MethodInfo> _miCache = new();

        [MenuItem("Window/Aim2Pro/Workbench")]
        public static void Open()
        {
            var win = GetWindow<WorkbenchWindow>("Workbench");
            win.minSize = new Vector2(760, 480);
            win.Show();
        }

        void OnGUI()
        {
            GUILayout.Label("Natural language prompt", EditorStyles.boldLabel);
            _scrollPrompt = EditorGUILayout.BeginScrollView(_scrollPrompt, GUILayout.MinHeight(90));
            _prompt = EditorGUILayout.TextArea(_prompt ?? string.Empty, GUILayout.MinHeight(90));
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Parse NL (local intents)", GUILayout.Height(26)))
                    ParseLocal();
                if (GUILayout.Button("Open Paste & Merge", GUILayout.Height(26)))
                    OpenMergeWithJson(_json);
                if (GUILayout.Button("Copy skeleton intent", GUILayout.Height(26)))
                    CopySkeletonIntent(_selectedUnmatched);
            }

            EditorGUILayout.Space(8);
            GUILayout.Label("Diagnostics (normalized, matched by source, unmatched chips)", EditorStyles.boldLabel);
            _scrollDiag = EditorGUILayout.BeginScrollView(_scrollDiag, GUILayout.MinHeight(160));
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(_diagnostics ?? "", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(4);

                // Unmatched chips row
                var unmatched = _lastUnmatched ?? Array.Empty<string>();
                if (unmatched.Length > 0)
                {
                    GUILayout.Label("Unmatched:", EditorStyles.miniBoldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        foreach (var u in unmatched.Distinct())
                        {
                            if (GUILayout.Button(u, GUILayout.Height(22)))
                                _selectedUnmatched = u;
                        }
                    }
                }

                // Selected box
                _selectedUnmatched = EditorGUILayout.TextField("Unmatched phrase", _selectedUnmatched ?? "");
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            GUILayout.Label("JSON Output", EditorStyles.boldLabel);
            _scrollJson = EditorGUILayout.BeginScrollView(_scrollJson, GUILayout.MinHeight(180));
            _json = EditorGUILayout.TextArea(_json ?? "", GUILayout.MinHeight(180));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "Flow: normalize → NLToJson → AIGG_NLInterpreter → JSON.\n" +
                "Diagnostics show which SPEC files (intents/macros/lexicon/commands) contain your tokens.\n" +
                "If JSON is empty: click a chip, 'Copy skeleton intent', then paste it in Paste & Merge.",
                MessageType.Info);
        }

        // -------------------- main flow --------------------

        static string Normalize(string s)
        {
            s = (s ?? "").Trim();
            // Optional shim: if you kept Local.NL.Normalize
            var shim = CallOptional<string>(null, "Aim2Pro.AIGG.Local.NL", "Normalize", new object[]{ s });
            return string.IsNullOrEmpty(shim) ? s : shim;
        }

        void ParseLocal()
        {
            _diagnostics = "";
            _json = "";

            var input = ( _prompt ?? "" ).Trim();
            if (string.IsNullOrEmpty(input))
            {
                _diagnostics = "Input is empty.";
                _lastUnmatched = Array.Empty<string>();
                return;
            }

            var normalized = Normalize(input);

            // 1) Try NLToJson
            string json = InvokeStatic<string>("NLToJson", "GenerateFromPrompt", normalized);

            // 2) Fallback: AIGG_NLInterpreter.RunToJson / Run
            if (string.IsNullOrEmpty(json))
                json = InvokeInstance<string>("AIGG_NLInterpreter", new[] { "RunToJson", "Run" }, normalized);

            // 3) Diagnostics by source file
            var tokens = Tokenize(normalized);
            var dicts = LoadSpecSetsBySource();
            var matchedBySource = new Dictionary<string, List<string>>
            {
                { "intents", new List<string>() },
                { "macros", new List<string>() },
                { "lexicon", new List<string>() },
                { "commands", new List<string>() }
            };
            var unmatched = new List<string>();

            foreach (var t in tokens)
            {
                if (IsNumeric(t)) { matchedBySource["lexicon"].Add(t); continue; }

                bool any = false;
                foreach (var kv in dicts)
                {
                    if (kv.Value.Contains(t)) { matchedBySource[kv.Key].Add(t); any = true; }
                }
                if (!any) unmatched.Add(t);
            }

            _lastUnmatched = unmatched.Distinct().ToArray();

            _diagnostics =
                $"Normalized:\n  {normalized}\n\n" +
                $"Matched (by source):\n" +
                $"  intents:  {string.Join(\", \", matchedBySource[\"intents\"].Distinct())}\n" +
                $"  macros:   {string.Join(\", \", matchedBySource[\"macros\"].Distinct())}\n" +
                $"  lexicon:  {string.Join(\", \", matchedBySource[\"lexicon\"].Distinct())}\n" +
                $"  commands: {string.Join(\", \", matchedBySource[\"commands\"].Distinct())}\n\n" +
                $"Unmatched ({_lastUnmatched.Length}):\n  {string.Join(\", \", _lastUnmatched)}\n\n" +
                (string.IsNullOrEmpty(json)
                    ? "No JSON produced by local parsers.\n→ Create intents/macros for unmatched phrase(s), then try again."
                    : "Local parsers produced JSON.\n→ You can Open Paste & Merge.");

            _json = json ?? "";
        }

        // -------------------- diagnostics helpers --------------------

        static List<string> Tokenize(string s)
        {
            var raw = s.ToLowerInvariant();
            var seps = new char[] {' ', ',', ';', '.', ':', '(', ')', '[', ']', '{', '}', '/', '\\', '|', '-', '+'};
            return raw.Split(seps, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        static bool IsNumeric(string s) => s.All(c => char.IsDigit(c));

        // Loads token sets per SPEC source (Editor-safe via AssetDatabase; fallback to Resources)
        static Dictionary<string, HashSet<string>> LoadSpecSetsBySource()
        {
            var map = new Dictionary<string, HashSet<string>>
            {
                { "intents",  new HashSet<string>() },
                { "macros",   new HashSet<string>() },
                { "lexicon",  new HashSet<string>() },
                { "commands", new HashSet<string>() }
            };

            // Preferred (Editor): Assets/AIGG/Spec/*.json
            AddWordsFromJson("Assets/AIGG/Spec/intents.json", map["intents"]);
            AddWordsFromJson("Assets/AIGG/Spec/macros.json", map["macros"]);
            AddWordsFromJson("Assets/AIGG/Spec/lexicon.json", map["lexicon"]);
            AddWordsFromJson("Assets/AIGG/Spec/commands.json", map["commands"]);

            // Fallback (Runtime-like): Resources/Spec/*
            if (map.All(kv => kv.Value.Count == 0))
            {
                AddWordsFromResources("Spec/intents",   map["intents"]);
                AddWordsFromResources("Spec/macros",    map["macros"]);
                AddWordsFromResources("Spec/lexicon",   map["lexicon"]);
                AddWordsFromResources("Spec/commands",  map["commands"]);
            }
            return map;
        }

        static void AddWordsFromJson(string assetPath, HashSet<string> set)
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (ta == null) return;
            var l = ta.text.ToLowerInvariant();
            foreach (var w in l.Split(new[]{'\"',' ', '\n', '\r', '\t', ',', ':', '{', '}', '[', ']'},
                StringSplitOptions.RemoveEmptyEntries))
                set.Add(w);
        }

        static void AddWordsFromResources(string resPath, HashSet<string> set)
        {
            var ta = Resources.Load<TextAsset>(resPath);
            if (ta == null) return;
            var l = ta.text.ToLowerInvariant();
            foreach (var w in l.Split(new[]{'\"',' ', '\n', '\r', '\t', ',', ':', '{', '}', '[', ']'},
                StringSplitOptions.RemoveEmptyEntries))
                set.Add(w);
        }

        // -------------------- reflection helpers (cached) --------------------

        static T InvokeStatic<T>(string typeName, string method, params object[] args)
        {
            var t = ResolveType(typeName);
            if (t == null) return default;
            var key = (typeName, method, true);
            if (!_miCache.TryGetValue(key, out var m) || m == null)
            {
                m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
                _miCache[key] = m;
            }
            if (m == null) return default;
            try { return (T)m.Invoke(null, args); } catch { return default; }
        }

        static T InvokeInstance<T>(string typeName, string[] methods, params object[] args)
        {
            var t = ResolveType(typeName);
            if (t == null) return default;
            var inst = Activator.CreateInstance(t);
            foreach (var name in methods)
            {
                var key = (typeName, name, false);
                if (!_miCache.TryGetValue(key, out var m) || m == null)
                {
                    m = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
                    _miCache[key] = m;
                }
                if (m == null) continue;
                try { return (T)m.Invoke(inst, args); } catch { }
            }
            return default;
        }

        static T CallOptional<T>(object instance, string typeName, string methodName, object[] args)
        {
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName)) return default;
            var t = ResolveType(typeName);
            if (t == null) return default;

            bool isStatic = (instance == null);
            var key = (typeName, methodName, isStatic);
            if (!_miCache.TryGetValue(key, out var m) || m == null)
            {
                m = t.GetMethod(methodName,
                    (isStatic ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.Public);
                _miCache[key] = m;
            }
            if (m == null) return default;

            try { return (T)m.Invoke(instance, args); } catch { return default; }
        }

        static Type ResolveType(string typeName)
        {
            var t = Type.GetType(typeName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var n = asm.GetName().Name ?? "";
                if (!(n.StartsWith("Assembly-CSharp") || n.StartsWith("AIGG"))) continue;
                t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        // -------------------- actions --------------------

        static void OpenMergeWithJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[AIGG] No JSON to send to Paste & Merge.");
                return;
            }

            var t = ResolveType("SpecPasteMergeWindow");
            var openWith = t?.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.Static);
            if (openWith != null) { openWith.Invoke(null, new object[] { json }); return; }

            EditorGUIUtility.systemCopyBuffer = json;
            if (t != null) EditorWindow.GetWindow(t, false, "Spec Paste & Merge");
            Debug.Log("[AIGG] Spec JSON copied to clipboard → open Paste & Merge and paste.");
        }

        static void CopySkeletonIntent(string phrase)
        {
            phrase = (phrase ?? "").Trim();
            if (phrase.Length == 0) { Debug.LogWarning("[AIGG] Click a chip or enter an unmatched phrase first."); return; }

            var skeleton =
$@"{{
  ""intent"": ""{phrase}"",
  ""slots"": [],
  ""examples"": [ ""{phrase}"" ],
  ""commands"": [
    {{ ""op"": ""insertStraight"", ""length_m"": 50 }}
  ]
}}";
            EditorGUIUtility.systemCopyBuffer = skeleton;
            Debug.Log("[AIGG] Skeleton intent copied to clipboard. Paste into Paste & Merge.");
        }

        // state
        static string[] _lastUnmatched = Array.Empty<string>();
    }
}
#endif
