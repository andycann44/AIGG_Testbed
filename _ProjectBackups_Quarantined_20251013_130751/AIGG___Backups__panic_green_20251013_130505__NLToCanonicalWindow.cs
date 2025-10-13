#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public class NLToCanonicalWindow : EditorWindow
    {
        const string CanonRel = "StickerDash_Status/LastCanonical.json";
        string nl = "";

        [MenuItem("Window/Aim2Pro/Aigg/NL \u2192 Canonical")]
        public static void Open() => GetWindow<NLToCanonicalWindow>("NL → Canonical");

        void OnGUI()
        {
            GUILayout.Label("Natural Language", EditorStyles.boldLabel);
            nl = EditorGUILayout.TextArea(nl, GUILayout.MinHeight(120));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Parse & Save Canonical")) ParseAndSave();
                if (GUILayout.Button("Reveal Canonical")) Reveal();
                if (GUILayout.Button("Clear Log")) Debug.ClearDeveloperConsole();
            }

            EditorGUILayout.HelpBox($"Writes: ./{CanonRel}\nTries AIGG_NLInterpreter first; falls back to  '120 m by 3 m' regex.", MessageType.Info);
        }

        void ParseAndSave()
        {
            string json = TryInterpreter(nl);
            if (string.IsNullOrEmpty(json))
            {
                // Fallback: extract "Lm by Wm" and inject into a minimal scenePlan
                var inv = CultureInfo.InvariantCulture;
                var m = Regex.Match(nl, "\\b(\\d+(?:\\.\\d+)?)\\s*m\\s*(?:x|by|×)\\s*(\\d+(?:\\.\\d+)?)\\s*m\\b", RegexOptions.IgnoreCase);
                if (!m.Success) { Debug.LogError("[AIGG] Could not parse size from NL. Try '120 m by 3 m'."); return; }
                var L = double.Parse(m.Groups[1].Value, inv);
                var W = double.Parse(m.Groups[2].Value, inv);
                bool rebuild = Regex.IsMatch(nl, "\\brebuild\\b", RegexOptions.IgnoreCase);

                json = "{\n" +
                       "  \"type\":\"scenePlan\",\n" +
                       "  \"name\":\"Quick Plan\",\n" +
                       "  \"grid\":{\"cols\":1,\"rows\":1,\"dx\":30.0,\"dy\":18.0,\"origin\":{\"x\":0.0,\"y\":0.0}},\n" +
                       "  \"trackTemplate\":{\n" +
                       "    \"lanes\":1,\n" +
                       "    \"segments\":[\"straight\"],\n" +
                       "    \"lengthUnits\":" + L.ToString(inv) + ",\n" +
                       "    \"tileWidth\":" + W.ToString(inv) + ",\n" +
                       "    \"zones\":{\"start\":{\"size\":{\"x\":2.0,\"y\":3.0}},\"end\":{\"size\":{\"x\":2.0,\"y\":3.0}}},\n" +
                       "    \"killZone\":{\"y\":-5.0,\"height\":2.0}\n" +
                       "  },\n" +
                       "  \"difficulty\":{\"tracks\":1,\"playerSpeed\":6.0},\n" +
                       "  \"camera\":{\"offsetX\":2.0,\"offsetY\":1.0,\"smooth\":0.15},\n" +
                       "  \"meta\":{\"rebuild\":" + (rebuild ? "true" : "false") + "}\n" +
                       "}\n";
                Debug.Log("[AIGG] Used regex fallback to create canonical.");
            }

            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CanonRel));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log($"[AIGG] Wrote canonical: {path}");
        }

        static string TryInterpreter(string nl)
        {
            try
            {
                // Try fully-qualified type first, then simple
                var t = Type.GetType("Aim2Pro.AIGG.Track.AIGG_NLInterpreter, Assembly-CSharp") ??
                        Type.GetType("AIGG_NLInterpreter, Assembly-CSharp");
                if (t == null) { Debug.LogWarning("[AIGG] NL interpreter not found; using regex fallback."); return null; }

                var method = t.GetMethod("ScenePlanFromNL", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method == null) { Debug.LogWarning("[AIGG] Interpreter missing ScenePlanFromNL; using regex fallback."); return null; }

                var scenePlanObj = method.Invoke(null, new object[] { nl });
                if (scenePlanObj == null) { Debug.LogWarning("[AIGG] Interpreter returned null ScenePlan; using regex fallback."); return null; }

                // Serialize via JsonUtility (requires a known type); try ToString first, else fallback to JsonUtility
                // If the ScenePlan class has a ToJson or similar, reflection could call it. For now:
                var json = JsonUtility.ToJson(scenePlanObj, true);
                if (string.IsNullOrEmpty(json) || json == "{}")
                {
                    Debug.LogWarning("[AIGG] JsonUtility returned empty; using regex fallback.");
                    return null;
                }
                Debug.Log("[AIGG] Parsed via interpreter.");
                return json;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AIGG] Interpreter error; using regex fallback. " + ex.Message);
                return null;
            }
        }

        void Reveal()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CanonRel));
            if (File.Exists(path)) EditorUtility.RevealInFinder(path);
            else Debug.LogWarning($"[AIGG] Canonical not found: {path}");
        }
    }
}
#endif
