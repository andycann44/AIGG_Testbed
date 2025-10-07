#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG.Track
{
    public class NLTrackBuilderPro : EditorWindow
    {
        [MenuItem("Window/Aim2Pro/Track Creator/Track Builder Pro")]
        public static void Open() => GetWindow<NLTrackBuilderPro>("Track Builder Pro");

        [TextArea(3, 8)]
        string nl = "120m by 3m, straight 100m, left curve 30°, gaps 8%, tile gaps 10%, chicane, slope 4%, seed 777";

        bool appendFromLast = false;
        bool autoStraightFromSize = true;
        float safeStartMeters = 5f;
        float safeFinishMeters = 5f;
        bool groupRowsInHierarchy = true;
        bool verboseGaps = false;

        void OnEnable()
        {
            minSize = new Vector2(280, 320);
            EditorGUIUtility.labelWidth = 80f;
        }

        [Serializable]
        private class TrackBuildState : MonoBehaviour
        {
            public Vector3 pos = Vector3.zero;
            public Vector3 fwd = Vector3.forward;
            public float builtMeters = 0f;
            public float trackWidth = 3f;
            public int seed = 0;
        }

        void OnGUI()
        {
            // NL (single line)
            GUILayout.Label("Natural Language", EditorStyles.boldLabel);
            var nlRect = GUILayoutUtility.GetRect(10, 20, GUILayout.ExpandWidth(true));
            nl = EditorGUI.TextField(nlRect, nl);

            // Row A: Build / Append / Clear
            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Build",  EditorStyles.toolbarButton)) BuildFromNL(false);
                if (GUILayout.Button("Append", EditorStyles.toolbarButton)) { appendFromLast = true; BuildFromNL(true); }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear",  EditorStyles.toolbarButton)) ClearTrack();
            }

            // Options (stacked to fit narrow widths)
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Two columns
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        appendFromLast       = GUILayout.Toggle(appendFromLast,       "Append");
                        autoStraightFromSize = GUILayout.Toggle(autoStraightFromSize, "Auto-straight");
                        groupRowsInHierarchy = GUILayout.Toggle(groupRowsInHierarchy, "Group rows");
                        verboseGaps          = GUILayout.Toggle(verboseGaps,          "Gap debug");
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        safeStartMeters  = EditorGUILayout.FloatField("Safe start (m)",  safeStartMeters);
                        safeFinishMeters = EditorGUILayout.FloatField("Safe finish (m)", safeFinishMeters);
                        GUILayout.FlexibleSpace();
                    }
                }
            }

            // Collapsible help
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var fold = SessionState.GetBool("A2P_TBPro_HelpFold", false);
                var nf = EditorGUILayout.Foldout(fold, "Short help", true);
                if (nf != fold) SessionState.SetBool("A2P_TBPro_HelpFold", nf);
                if (nf)
                {
                    EditorGUILayout.LabelField(
                        "Understands: \"Xm by Ym\", \"straight Xm\", \"left/right curve Ndg\", " +
                        "\"Ndg left/right over N rows\", \"gaps N%\", \"tile gaps N%\", " +
                        "\"chicane\", \"slope N%\", \"seed N\", \"width Xm\".\n" +
                        "Priorities with P##| (lower runs earlier). Defaults: straight P10, curves P20, slope P40, gaps P50.",
                        EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        // compact numeric field inside toolbar rows
        float FloatFieldToolbar(string label, float value, float labelWidth)
        {
            var prev = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;
            value = EditorGUILayout.FloatField(label, value, GUILayout.Width(labelWidth + 55));
            EditorGUIUtility.labelWidth = prev;
            return value;
        }

        // ==== Entry ====
        void BuildFromNL(bool forceAppend)
        {
            var spec = ParseNL(nl, autoStraightFromSize);
            Execute(spec, forceAppend || appendFromLast);
        }

        void ClearTrack()
        {
            var parent = GameObject.Find("/Track_Built");
            if (parent != null) Undo.DestroyObjectImmediate(parent);
            Debug.Log("[NLTrack] Cleared Track.");
        }

        // ==== NL parsing (regex, self-contained) ====
        private class Spec { public Track track = new Track(); public Rules rules = new Rules(); public List<string> cmds = new List<string>(); }
        [Serializable] private class Track { public int length; public int width = 3; public int seed; }
        [Serializable] private class Rules { public bool safeZones = true; }

        Spec ParseNL(string text, bool autoStraight)
        {
            var spec = new Spec();
            var t = text ?? "";

            int GetInt(Match m, int group, int def = 0)
                => (m.Success && int.TryParse(m.Groups[group].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) ? v : def;

            // size
            var mSize = Regex.Match(t, @"\b(\d+)\s*m\s*by\s*(\d+)\s*m\b", RegexOptions.IgnoreCase);
            if (mSize.Success)
            {
                spec.track.length = GetInt(mSize, 1, 60);
                spec.track.width  = GetInt(mSize, 2, 3);
                if (autoStraight) spec.cmds.Add($"P10|AppendStraight:{spec.track.length}");
            }

            // explicit width
            var mWidth = Regex.Match(t, @"\bwidth\s*(\d+)\s*m\b", RegexOptions.IgnoreCase);
            if (mWidth.Success) spec.cmds.Add($"P08|SetWidth:{GetInt(mWidth, 1, spec.track.width)}");

            // straight
            foreach (Match m in Regex.Matches(t, @"\bstraight\s*(\d+)\s*m\b", RegexOptions.IgnoreCase))
                spec.cmds.Add($"P10|AppendStraight:{GetInt(m, 1, 10)}");

            // curves: “left/right curve 30°|deg”, also “30 degree left over 10 rows”
            foreach (Match m in Regex.Matches(t, @"\b(left|right)\s*curve\s*(\d+)\s*(?:°|deg|degree|degrees)?\b(?:\s*over\s*(\d+)\s*rows?)?", RegexOptions.IgnoreCase))
            {
                var side = m.Groups[1].Value.ToLowerInvariant();
                int deg = GetInt(m, 2, 15);
                int steps = GetInt(m, 3, -1);
                spec.cmds.Add(steps > 0 ? $"P20|AppendArc:{side}:{deg}:{steps}" : $"P20|AppendArc:{side}:{deg}");
            }
            foreach (Match m in Regex.Matches(t, @"\b(\d+)\s*(?:°|deg|degree|degrees)\s*(left|right)\b(?:\s*over\s*(\d+)\s*rows?)?", RegexOptions.IgnoreCase))
            {
                var side = m.Groups[2].Value.ToLowerInvariant();
                int deg = GetInt(m, 1, 15);
                int steps = GetInt(m, 3, -1);
                spec.cmds.Add(steps > 0 ? $"P20|AppendArc:{side}:{deg}:{steps}" : $"P20|AppendArc:{side}:{deg}");
            }

            // chicane
            if (Regex.IsMatch(t, @"\bchicanes?\b", RegexOptions.IgnoreCase))
            {
                spec.cmds.Add("P20|AppendArc:left:25:8");
                spec.cmds.Add("P20|AppendArc:right:25:8");
            }

            // slope / incline
            foreach (Match m in Regex.Matches(t, @"\b(?:slope|incline)\s*(\d+)%\b", RegexOptions.IgnoreCase))
                spec.cmds.Add($"P40|SlopePercent:{GetInt(m, 1, 3)}");

            // row gaps / tile gaps (robust to trailing punctuation)
            foreach (Match m in Regex.Matches(t, @"\bgaps?(?:\s*of)?\s*(\d+)\s*%(?=\s|,|\.|;|$)", RegexOptions.IgnoreCase))
                spec.cmds.Add($"P50|AddGaps:{GetInt(m, 1, 0)}");
            foreach (Match m in Regex.Matches(t, @"\btile\s*gaps?(?:\s*of)?\s*(\d+)\s*%(?=\s|,|\.|;|$)", RegexOptions.IgnoreCase))
                spec.cmds.Add($"P50|AddGapsTile:{GetInt(m, 1, 0)}");

            // seed
            var mSeed = Regex.Match(t, @"\bseed\s*(\d+)\b", RegexOptions.IgnoreCase);
            if (mSeed.Success) spec.track.seed = GetInt(mSeed, 1, 0);

            // safe zones on
            spec.rules.safeZones = true;

            // pass through manual P##|cmds
            foreach (Match m in Regex.Matches(t, @"\bP(\d{1,3})\|([^\s,;]+)\b", RegexOptions.IgnoreCase))
                spec.cmds.Add(m.Value.Trim());

            return spec;
        }

        // ==== Execute ====
        void Execute(Spec spec, bool append)
        {
            GameObject parent = GameObject.Find("/Track_Built");
            TrackBuildState state = null;

            if (!append || parent == null)
            {
                parent = new GameObject("Track_Built");
                Undo.RegisterCreatedObjectUndo(parent, "Create Track_Built");
                state = parent.AddComponent<TrackBuildState>();
                state.pos = Vector3.zero;
                state.fwd = Vector3.forward;
                state.builtMeters = 0f;
                state.trackWidth = Mathf.Max(0.5f, spec.track.width);
                state.seed = spec.track.seed;
            }
            else
            {
                state = parent.GetComponent<TrackBuildState>() ?? parent.AddComponent<TrackBuildState>();
                if (spec.track.width > 0) state.trackWidth = spec.track.width;
                if (spec.track.seed != 0) state.seed = spec.track.seed;
            }

            var rand = new System.Random(state.seed);
            float tileStep = 1f;
            float gapRowPercent = 0f;
            float gapTilePercent = 0f;
            bool safeZones = spec.rules.safeZones;

            foreach (var raw in spec.cmds)
            {
                var cmd0 = StripPriority(raw);
                if (cmd0.StartsWith("AddGapsTile:", StringComparison.OrdinalIgnoreCase))
                    gapTilePercent = ParseFloatArg(cmd0, 1, gapTilePercent);
                else if (cmd0.StartsWith("AddGaps:", StringComparison.OrdinalIgnoreCase))
                    gapRowPercent = ParseFloatArg(cmd0, 1, gapRowPercent);
            }

            var prioritized = BuildPriorityList(spec.cmds);
            prioritized = MergeConsecutiveStraights(prioritized);

            float estTotal = EstimateTotalLength(prioritized, tileStep);
            float startSafe = Mathf.Max(0f, safeStartMeters);
            float endSafe = Mathf.Max(0f, safeFinishMeters);

            foreach (var item in prioritized)
            {
                var cmd = item.cmd;
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                if (cmd.StartsWith("SetWidth:", StringComparison.OrdinalIgnoreCase))
                {
                    state.trackWidth = Mathf.Max(0.5f, ParseFloatArg(cmd, 1, state.trackWidth));
                }
                else if (cmd.StartsWith("AppendStraight", StringComparison.OrdinalIgnoreCase))
                {
                    float dist = ParseFloatArg(cmd, 1, 10f);
                    float step = ParseFloatArg(cmd, 2, tileStep);
                    int steps = Mathf.Max(1, Mathf.RoundToInt(dist / step));
                    for (int i = 0; i < steps; i++)
                    {
                        state.pos += state.fwd * step;
                        PlaceRow(parent.transform, ref state, tileStep, startSafe, endSafe, estTotal,
                                 gapRowPercent, gapTilePercent, safeZones, rand);
                    }
                }
                else if (cmd.StartsWith("AppendArc", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = cmd.Split(':');
                    if (parts.Length >= 3 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var deg))
                    {
                        bool left = parts[1].Equals("left", StringComparison.OrdinalIgnoreCase);
                        int steps = (parts.Length >= 4 && int.TryParse(parts[3], out var st)) ? Mathf.Max(st, 1)
                                  : Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(deg) / 5f));
                        float stepDeg = deg / steps;
                        float stepLen = tileStep;

                        for (int i = 0; i < steps; i++)
                        {
                            // Unity +Yaw is right; negative yaw is left
                            float yaw = left ? -stepDeg : stepDeg;
                            state.fwd = Quaternion.Euler(0f, yaw, 0f) * state.fwd;
                            state.pos += state.fwd * stepLen;
                            PlaceRow(parent.transform, ref state, tileStep, startSafe, endSafe, estTotal,
                                     gapRowPercent, gapTilePercent, safeZones, rand);
                        }
                    }
                }
                else if (cmd.StartsWith("SlopePercent:", StringComparison.OrdinalIgnoreCase))
                {
                    float p = ParseFloatArg(cmd, 1, 0f);
                    int steps = 20;
                    float risePerStep = (p / 100f) * tileStep;
                    for (int i = 0; i < steps; i++)
                    {
                        state.pos += state.fwd * tileStep + Vector3.up * risePerStep;
                        PlaceRow(parent.transform, ref state, tileStep, startSafe, endSafe, estTotal,
                                 gapRowPercent, gapTilePercent, safeZones, rand);
                    }
                }
            }

            Selection.activeGameObject = parent;
            Debug.Log($"[NLTrack] Built ~{Mathf.RoundToInt(state.builtMeters)} m | width {state.trackWidth} m | tiles {parent.transform.childCount} | gaps row {gapRowPercent}% tile {gapTilePercent}% | seed {state.seed}");
        }

        void PlaceRow(Transform parent, ref TrackBuildState st, float tileStep,
                      float safeStart, float safeFinish, float estTotal,
                      float gapRowPercent, float gapTilePercent, bool safeZones, System.Random rand)
        {
            bool inSafeStart = safeZones && (st.builtMeters < safeStart);
            bool inSafeEnd   = safeZones && ((estTotal - st.builtMeters) <= safeFinish);

            // Row-level gap? skip entire row (and don't create a Row_XXXm folder)
            if (!inSafeStart && !inSafeEnd && gapRowPercent > 0f && rand.NextDouble() < (gapRowPercent / 100.0))
            {
                if (verboseGaps) Debug.Log($"[NLTrack:gaps] SKIP ROW at ~{Mathf.RoundToInt(st.builtMeters)}m (row {gapRowPercent}%)");
                st.builtMeters += tileStep;
                return;
            }

            // Optional row folder
            Transform rowParent = parent;
            GameObject rowGO = null;
            if (groupRowsInHierarchy)
            {
                rowGO = new GameObject($"Row_{Mathf.RoundToInt(st.builtMeters)}m");
                rowGO.transform.SetParent(parent, true);
                rowGO.transform.position = st.pos;
                rowParent = rowGO.transform;
            }

            float tileUnit = 1f;
            int cols = Mathf.Max(1, Mathf.RoundToInt(st.trackWidth / tileUnit));
            var right = Vector3.Cross(Vector3.up, st.fwd).normalized;
            float half = (cols - 1) * 0.5f;
            bool placedAny = false;

            for (int j = 0; j < cols; j++)
            {
                // Tile-level gap?
                if (!inSafeStart && !inSafeEnd && gapTilePercent > 0f && rand.NextDouble() < (gapTilePercent / 100.0))
                {
                    if (verboseGaps) Debug.Log($"[NLTrack:gaps] skip tile r~{Mathf.RoundToInt(st.builtMeters)}m c{j} (tile {gapTilePercent}%)");
                    continue;
                }

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Tile_{Mathf.RoundToInt(st.builtMeters)}m_c{j}";
                go.transform.SetParent(rowParent, true);
                go.transform.position = st.pos + right * ((j - half) * tileUnit);
                go.transform.rotation = Quaternion.LookRotation(st.fwd, Vector3.up);
                go.transform.localScale = new Vector3(tileUnit * 0.98f, 0.1f, tileStep);
                placedAny = true;
            }

            // If no tiles were placed, remove the empty row folder
            if (groupRowsInHierarchy && rowGO != null && !placedAny)
            {
                UnityEngine.Object.DestroyImmediate(rowGO);
                if (verboseGaps) Debug.Log($"[NLTrack:gaps] delete empty row folder at ~{Mathf.RoundToInt(st.builtMeters)}m");
            }

            st.builtMeters += tileStep;
        }

        private struct CmdItem { public int pri; public int idx; public string cmd; }

        List<CmdItem> BuildPriorityList(List<string> raw)
        {
            var list = new List<CmdItem>();
            for (int i = 0; i < raw.Count; i++)
            {
                int pri; string clean;
                GetPriorityAndClean(raw[i], out pri, out clean);
                list.Add(new CmdItem { pri = pri, idx = i, cmd = clean });
            }
            list.Sort((a, b) => a.pri != b.pri ? a.pri.CompareTo(b.pri) : a.idx.CompareTo(b.idx));
            return list;
        }

        List<CmdItem> MergeConsecutiveStraights(List<CmdItem> src)
        {
            var merged = new List<CmdItem>();
            for (int i = 0; i < src.Count; i++)
            {
                var cur = src[i];
                if (cur.cmd.StartsWith("AppendStraight:", StringComparison.OrdinalIgnoreCase) && merged.Count > 0)
                {
                    var last = merged[merged.Count - 1];
                    if (last.cmd.StartsWith("AppendStraight:", StringComparison.OrdinalIgnoreCase))
                    {
                        float d1 = ParseFloatArg(last.cmd, 1, 0f);
                        float d2 = ParseFloatArg(cur.cmd, 1, 0f);
                        last.cmd = "AppendStraight:" + (d1 + d2).ToString(CultureInfo.InvariantCulture);
                        merged[merged.Count - 1] = last;
                        continue;
                    }
                }
                merged.Add(cur);
            }
            return merged;
        }

        float EstimateTotalLength(List<CmdItem> cmds, float tileStep)
        {
            float total = 0f;
            foreach (var it in cmds)
            {
                var cmd = it.cmd;
                if (cmd.StartsWith("AppendStraight:", StringComparison.OrdinalIgnoreCase))
                    total += ParseFloatArg(cmd, 1, 0f);
                else if (cmd.StartsWith("AppendArc:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = cmd.Split(':');
                    if (parts.Length >= 3 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var deg))
                    {
                        int steps = (parts.Length >= 4 && int.TryParse(parts[3], out var st)) ? Mathf.Max(st, 1) : Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(deg) / 5f));
                        total += steps * tileStep;
                    }
                }
            }
            return total;
        }

        static void GetPriorityAndClean(string raw, out int pri, out string clean)
        {
            pri = 100; clean = raw ?? "";
            if (string.IsNullOrWhiteSpace(raw)) return;
            raw = raw.Trim();
            if (raw.Length > 3 && (raw[0] == 'P' || raw[0] == 'p'))
            {
                int bar = raw.IndexOf('|');
                if (bar > 1 && bar <= 5)
                {
                    string num = raw.Substring(1, bar - 1);
                    if (int.TryParse(num, out var p))
                    { pri = Mathf.Clamp(p, 0, 999); clean = raw.Substring(bar + 1); return; }
                }
            }
        }

        static float ParseFloatArg(string cmd, int argIndex, float def)
        {
            var parts = cmd.Split(':');
            if (parts.Length >= argIndex + 1 &&
                float.TryParse(parts[argIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
            return def;
        }

        static string StripPriority(string raw)
        {
            GetPriorityAndClean(raw, out var _, out var clean);
            return clean;
        }
    }
}
#endif
