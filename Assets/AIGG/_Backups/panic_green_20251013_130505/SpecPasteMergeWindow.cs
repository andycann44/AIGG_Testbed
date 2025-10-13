#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/*
 * Window: Window/Aim2Pro/Aigg/Paste & Merge
 * Paste JSON or "alias => canonical", choose target, find duplicates, then Apply (Replace/Skip).
 * Backs up before writing.
 */

namespace Aim2Pro.AIGG
{
    public class SpecPasteMergeWindow : EditorWindow
    {
        // Added Commands, Macros
        enum TargetType { Lexicon, Fieldmap, Intents, Schema, Registry, Commands, Macros }
        enum DupPolicy  { Skip, Replace }

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge")]
        public static void Open()
        {
            var w = GetWindow<SpecPasteMergeWindow>("Paste & Merge");
            w.minSize = new Vector2(720, 520);
        }

        // UI state
        string pasted = "";
        TargetType target = TargetType.Lexicon;
        DupPolicy policy  = DupPolicy.Skip;

        // analysis
        string status = "";
        string detail = "";
        string preview = "";
        int addCount = 0, dupCount = 0, sameCount = 0, errCount = 0;

        Vector2 svPaste, svDetail, svPreview;

        static string SpecRoot => "Assets/StickerDash/AIGG/Resources/Spec";

        // --- Paths (now includes Commands/Macros) ---
        static string PathFor(TargetType t)
        {
            switch (t)
            {
                case TargetType.Lexicon:  return Path.Combine(SpecRoot, "lexicon.json");
                case TargetType.Fieldmap: return Path.Combine(SpecRoot, "fieldmap.json");
                case TargetType.Intents:  return Path.Combine(SpecRoot, "intents.json");
                case TargetType.Schema:   return Path.Combine(SpecRoot, "schema.json");
                case TargetType.Registry: return Path.Combine(SpecRoot, "registry.json");
                case TargetType.Commands: return Path.Combine(SpecRoot, "commands.json");
                case TargetType.Macros:   return Path.Combine(SpecRoot, "macros.json");
                default:                  return Path.Combine(SpecRoot, "lexicon.json");
            }
        }

        void OnEnable()
        {
            Directory.CreateDirectory(SpecRoot);
        }

        // --- Backups navigation + file load ---

        static string ProjectRoot => System.IO.Directory.GetParent(Application.dataPath).FullName;
        static string BackupsRoot => System.IO.Path.Combine(ProjectRoot, "StickerDash_Status", "_Backups");

        string LatestBackupDir()
        {
            if (!System.IO.Directory.Exists(BackupsRoot)) System.IO.Directory.CreateDirectory(BackupsRoot);
            var dirs = System.IO.Directory.GetDirectories(BackupsRoot);
            if (dirs == null || dirs.Length == 0) return BackupsRoot;
            System.Array.Sort(dirs, System.StringComparer.Ordinal);
            return dirs[dirs.Length - 1];
        }

        void OpenBackups() => UnityEditor.EditorUtility.RevealInFinder(LatestBackupDir());

        void LoadFromFile()
        {
            var start = LatestBackupDir();
            var path = UnityEditor.EditorUtility.OpenFilePanel("Load JSON (backup or any json)", start, "json");
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                try
                {
                    pasted = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
                    status = "Loaded " + System.IO.Path.GetFileName(path);
                    detail = ""; preview = ""; addCount = dupCount = sameCount = errCount = 0;

                    var fn = System.IO.Path.GetFileName(path).ToLowerInvariant();
                    if      (fn.Contains("lexicon"))   target = TargetType.Lexicon;
                    else if (fn.Contains("fieldmap"))  target = TargetType.Fieldmap;
                    else if (fn.Contains("intents"))   target = TargetType.Intents;
                    else if (fn.Contains("schema"))    target = TargetType.Schema;
                    else if (fn.Contains("registry"))  target = TargetType.Registry;
                    else if (fn.Contains("commands"))  target = TargetType.Commands;
                    else if (fn.Contains("macros"))    target = TargetType.Macros;

                    Repaint();
                }
                catch (System.Exception ex) { UnityEditor.EditorUtility.DisplayDialog("Load Failed", ex.Message, "OK"); }
            }
        }

        void RestoreFromBackup()
        {
            OpenBackups();
            LoadFromFile();
        }

        string FileNameForTarget(TargetType t)
        {
            switch (t)
            {
                case TargetType.Lexicon:  return "lexicon.json";
                case TargetType.Fieldmap: return "fieldmap.json";
                case TargetType.Intents:  return "intents.json";
                case TargetType.Schema:   return "schema.json";
                case TargetType.Registry: return "registry.json";
                case TargetType.Commands: return "commands.json";
                case TargetType.Macros:   return "macros.json";
            }
            return "lexicon.json";
        }

        bool LoadLatestBackupForTarget()
        {
            try
            {
                var dir  = LatestBackupDir();
                string want = FileNameForTarget(target);
                var path = System.IO.Path.Combine(dir, want);
                if (!System.IO.File.Exists(path))
                {
                    var files = System.IO.Directory.GetFiles(dir, "*.json", System.IO.SearchOption.TopDirectoryOnly);
                    System.Array.Sort(files, System.StringComparer.Ordinal);
                    foreach (var f in files)
                    {
                        var fn = System.IO.Path.GetFileName(f).ToLowerInvariant();
                        if ((target == TargetType.Lexicon  && fn.Contains("lexicon"))  ||
                            (target == TargetType.Fieldmap && fn.Contains("fieldmap")) ||
                            (target == TargetType.Intents  && fn.Contains("intents"))  ||
                            (target == TargetType.Schema   && fn.Contains("schema"))   ||
                            (target == TargetType.Registry && fn.Contains("registry")) ||
                            (target == TargetType.Commands && fn.Contains("commands")) ||
                            (target == TargetType.Macros   && fn.Contains("macros")))
                        { path = f; break; }
                    }
                }
                if (!System.IO.File.Exists(path)) return false;

                pasted = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
                status = "Loaded latest backup: " + System.IO.Path.GetFileName(path);
                detail = ""; preview = ""; addCount = dupCount = sameCount = errCount = 0;
                Repaint();
                return true;
            }
            catch (System.Exception ex) { status = "Load failed: " + ex.Message; return false; }
        }

        // === External Entry (called from other windows) ===
        public static void OpenWithPaste(string content, string targetName = "Lexicon", string onDuplicates = "Skip", bool analyze = true, bool autoApply = false)
        {
            var w = GetWindow<SpecPasteMergeWindow>("Paste & Merge");
            w.minSize = new Vector2(720, 520);
            if (!Enum.TryParse<TargetType>(targetName, true, out var tType)) tType = TargetType.Lexicon;
            if (!Enum.TryParse<DupPolicy>(onDuplicates, true, out var p))     p     = DupPolicy.Skip;

            w.pasted = content ?? "";
            w.target = tType;
            w.policy = p;

            if (analyze) w.Analyze();
            if (autoApply)
            {
                if (w.Apply()) w.ShowNotification(new GUIContent("Merged & Saved"));
                else w.ShowNotification(new GUIContent("Nothing to write"));
            }
            w.Repaint();
        }

        void OnGUI()
        {
            GUILayout.Label("Paste & Merge into AIGG Spec", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                target = (TargetType)EditorGUILayout.EnumPopup("Target", target);
                policy = (DupPolicy)EditorGUILayout.EnumPopup(new GUIContent("On Duplicates", "Skip: keep existing values.\nReplace: overwrite existing with pasted."), policy);
                if (GUILayout.Button("Open Target", GUILayout.Width(120)))
                    EditorUtility.RevealInFinder(PathFor(target));
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Load Latest Backup", GUILayout.Width(170))) { if (!LoadLatestBackupForTarget()) ShowNotification(new GUIContent("No backup found")); }
                GUILayout.FlexibleSpace();
            }

            // Paste area
            EditorGUILayout.LabelField("Pasted Content", EditorStyles.miniBoldLabel);
            svPaste = EditorGUILayout.BeginScrollView(svPaste, GUILayout.MinHeight(180));
            pasted = EditorGUILayout.TextArea(pasted ?? "", GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load Example")) { pasted = ExampleFor(target); }
                if (GUILayout.Button("Open Backups", GUILayout.Width(120))) OpenBackups();
                if (GUILayout.Button("Load From File…", GUILayout.Width(140))) LoadFromFile();
                if (GUILayout.Button("Restore From Backup…", GUILayout.Width(180))) RestoreFromBackup();
                if (GUILayout.Button("Validate & Find Duplicates")) { Analyze(); }
                if (GUILayout.Button("Clear"))
                {
                    pasted = ""; status = ""; detail = ""; preview = ""; addCount = dupCount = sameCount = errCount = 0;
                }
            }

            // Status
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                string.IsNullOrEmpty(status) ? "—"
                : $"{status}\nAdd: {addCount} | Duplicates: {dupCount} | Same: {sameCount} | Errors: {errCount}",
                MessageType.Info);

            // Detail
            EditorGUILayout.LabelField("Duplicate / Error Detail");
            svDetail = EditorGUILayout.BeginScrollView(svDetail, GUILayout.MinHeight(120));
            EditorGUILayout.TextArea(string.IsNullOrEmpty(detail) ? "—" : detail, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            // Preview
            EditorGUILayout.LabelField("Preview (Post-Merge)");
            svPreview = EditorGUILayout.BeginScrollView(svPreview, GUILayout.MinHeight(160));
            EditorGUILayout.TextArea(string.IsNullOrEmpty(preview) ? "—" : preview, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            // Apply
            using (new EditorGUI.DisabledScope(addCount==0 && dupCount==0 && target!=TargetType.Schema && target!=TargetType.Registry))
            {
                if (GUILayout.Button("Apply Merge (Backup then Write)"))
                {
                    if (Apply()) ShowNotification(new GUIContent("Merged & Saved"));
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Tip: You can paste either JSON or simple \"alias => canonical\" lines for Lexicon/Fieldmap.", MessageType.None);
        }

        // --------- Analyze ----------
        void Analyze()
        {
            status = ""; detail = ""; preview = ""; addCount = dupCount = sameCount = errCount = 0;

            var path = PathFor(target);
            EnsureFile(path);

            string current = File.ReadAllText(path, Encoding.UTF8);
            object existingObj, incomingObj;

            // Parse existing JSON (tolerant)
            if (!MiniJSON.TryDeserialize(current, out existingObj))
            {
                status = "Error: Target JSON failed to parse. Aborting analysis.";
                errCount++;
                return;
            }

            // Parse incoming (try JSON, else try alias=>canonical lines when appropriate)
            if (!TryParseIncoming(out incomingObj))
            {
                status = "Error: Could not parse pasted content.";
                errCount++;
                return;
            }

            // Dispatch per type
            try
            {
                switch (target)
                {
                    case TargetType.Lexicon:
                        MergeDict(existingObj, incomingObj, "synonyms", out addCount, out dupCount, out sameCount, out preview, out detail);
                        status = "Lexicon analyzed.";
                        break;

                    case TargetType.Fieldmap:
                        MergeDict(existingObj, incomingObj, "englishToPath", out addCount, out dupCount, out sameCount, out preview, out detail);
                        status = "Fieldmap analyzed.";
                        break;

                    case TargetType.Intents:
                        MergeIntents(existingObj, incomingObj, out addCount, out dupCount, out sameCount, out preview, out detail);
                        status = "Intents analyzed.";
                        break;

                    case TargetType.Commands:
                        MergeNamedList(existingObj, incomingObj, "commands", out addCount, out dupCount, out sameCount, out preview, out detail);
                        status = "Commands analyzed.";
                        break;

                    case TargetType.Macros:
                        MergeNamedList(existingObj, incomingObj, "macros", out addCount, out dupCount, out sameCount, out preview, out detail);
                        status = "Macros analyzed.";
                        break;

                    case TargetType.Schema:
                    case TargetType.Registry:
                        // For now: preview will be full replacement if JSON valid
                        preview = MiniJSON.Serialize(incomingObj, pretty:true);
                        status = $"{target} ready to replace (no granular merge yet).";
                        break;
                }
            }
            catch (Exception ex)
            {
                status = $"Error during analysis: {ex.Message}";
                errCount++;
            }
        }

        // --------- Apply ----------
        bool Apply()
        {
            var path = PathFor(target);
            EnsureFile(path);

            string current = File.ReadAllText(path, Encoding.UTF8);
            object existingObj, incomingObj;

            if (!MiniJSON.TryDeserialize(current, out existingObj)) { EditorUtility.DisplayDialog("Error","Target JSON unreadable.","OK"); return false; }
            if (!TryParseIncoming(out incomingObj))               { EditorUtility.DisplayDialog("Error","Pasted content unreadable.","OK"); return false; }

            object result = null;

            switch (target)
            {
                case TargetType.Lexicon:   result = ApplyDict(existingObj, incomingObj, "synonyms");       break;
                case TargetType.Fieldmap:  result = ApplyDict(existingObj, incomingObj, "englishToPath");  break;
                case TargetType.Intents:   result = ApplyIntents(existingObj, incomingObj);                 break;
                case TargetType.Commands:  result = ApplyNamedList(existingObj, incomingObj, "commands");   break;
                case TargetType.Macros:    result = ApplyNamedList(existingObj, incomingObj, "macros");     break;
                case TargetType.Schema:
                case TargetType.Registry:  result = incomingObj; // replace entirely
                    break;
            }

            if (result == null) { EditorUtility.DisplayDialog("Error","Nothing to write.","OK"); return false; }

            Backup(path);
            File.WriteAllText(path, MiniJSON.Serialize(result, pretty:true), Encoding.UTF8);
            AssetDatabase.Refresh();

            status = $"Saved {Path.GetFileName(path)}";
            return true;
        }

        // --------- Helpers: Parsing ---------
        bool TryParseIncoming(out object obj)
        {
            obj = null;
            if (string.IsNullOrWhiteSpace(pasted)) return false;

            // Try JSON first
            if (MiniJSON.TryDeserialize(pasted, out obj)) return true;

            // If lexicon/fieldmap: allow "alias => canonical" lines
            if (target==TargetType.Lexicon || target==TargetType.Fieldmap)
            {
                var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var lines = pasted.Split(new[] {'\n','\r'}, StringSplitOptions.RemoveEmptyEntries);
                foreach (var raw in lines)
                {
                    var line = raw.Trim();
                    if (line.Length==0 || line.StartsWith("#")) continue;
                    var idx = line.IndexOf("=>", StringComparison.Ordinal);
                    if (idx<0) continue;
                    var key = line.Substring(0, idx).Trim();
                    var val = line.Substring(idx+2).Trim();
                    if (key.Length==0 || val.Length==0) continue;
                    map[key] = val;
                }
                if (map.Count>0)
                {
                    // wrap into expected shape
                    var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    if (target==TargetType.Lexicon)       root["synonyms"]      = map;
                    else if (target==TargetType.Fieldmap) root["englishToPath"] = map;
                    obj = root;
                    return true;
                }
            }
            return false;
        }

        void EnsureFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path))
            {
                var seed = new Dictionary<string, object>();
                if (target==TargetType.Lexicon)   seed["synonyms"]      = new Dictionary<string,object>();
                if (target==TargetType.Fieldmap)  seed["englishToPath"] = new Dictionary<string,object>();
                if (target==TargetType.Intents)   seed["intents"]       = new List<object>();
                if (target==TargetType.Schema)    seed["version"]       = "v1";
                if (target==TargetType.Registry)  seed["components"]    = new List<object>();
                if (target==TargetType.Commands)  seed["commands"]      = new List<object>();
                if (target==TargetType.Macros)    seed["macros"]        = new List<object>();
                File.WriteAllText(path, MiniJSON.Serialize(seed, pretty:true), Encoding.UTF8);
            }
        }

        void Backup(string path)
        {
            var dir = Path.Combine("StickerDash_Status","_Backups","Spec_"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"));
            Directory.CreateDirectory(dir);
            var dst = Path.Combine(dir, Path.GetFileName(path));
            File.Copy(path, dst, true);
            var meta = path + ".meta";
            if (File.Exists(meta)) File.Copy(meta, Path.Combine(dir, Path.GetFileName(meta)), true);
            Debug.Log($"[AIGG] Backup: {dst}");
        }

        string ExampleFor(TargetType t)
        {
            switch (t)
            {
                case TargetType.Lexicon:
                    return "{\n  \"synonyms\": {\n    \"snake rows\": \"snakeRows\",\n    \"row major\": \"rowMajor\"\n  }\n}\n# or lines:\nsnake rows => snakeRows\nrow major => rowMajor\n";
                case TargetType.Fieldmap:
                    return "{\n  \"englishToPath\": {\n    \"tracks\": \"$.difficulty.tracks\",\n    \"grid columns\": \"$.grid.cols\"\n  }\n}\n# or lines:\ntracks => $.difficulty.tracks\ngrid columns => $.grid.cols\n";
                case TargetType.Intents:
                    return "{\n  \"intents\": [\n    {\"name\":\"set-tracks\",\"regex\":\"\\\\b(\\\\d+)\\\\s+tracks?\\\\b\",\"ops\":[{\"op\":\"set\",\"path\":\"$.difficulty.tracks\",\"value\":\"$1:int\"}]}\n  ]\n}\n";
                case TargetType.Commands:
                    return "{\n  \"commands\": [\n    {\"name\":\"curve-rows-range-left-deg (visual)\",\"regex\":\"^\\\\s*curve\\\\s+rows\\\\s+(\\\\d+)\\\\s*(?:-|to)\\\\s*(\\\\d+)\\\\s+left\\\\s+(\\\\d+)\\\\s*deg\\\\s*$\",\"ops\":[{\"op\":\"set\",\"path\":\"$.args.start\",\"value\":\"$1:int\"},{\"op\":\"set\",\"path\":\"$.args.end\",\"value\":\"$2:int\"},{\"op\":\"set\",\"path\":\"$.args.deg\",\"value\":\"$3:int\"},{\"op\":\"set\",\"path\":\"$.args.side\",\"value\":\"right\"},{\"op\":\"custom\",\"path\":\"$.args\",\"value\":\"clamp_rows_to_plan\"},{\"op\":\"custom\",\"path\":\"$.exec\",\"value\":\"CurveRows($.args.start,$.args.end,$.args.side,$.args.deg)\"}]}\n  ]\n}\n";
                case TargetType.Macros:
                    return "{\n  \"macros\": [\n    {\"name\":\"on_zero_hit_call_api_fix\",\"trigger\":\"zero_hit\",\"ops\":[{\"op\":\"custom\",\"path\":\"$.nl.normalized\",\"value\":\"Aim2Pro.AIGG.ApiFixOp.ApiFix\"},{\"op\":\"custom\",\"path\":\"$.reprocess\",\"value\":\"rerun_match_once\"}]}\n  ]\n}\n";
                case TargetType.Schema:
                    return "{\n  \"version\":\"v1\",\n  \"description\":\"Your schema (replace file)\"\n}\n";
                case TargetType.Registry:
                    return "{\n  \"components\": [],\n  \"windows\": []\n}\n";
            }
            return "";
        }

        // --------- Merge: lexicon/fieldmap ----------
        void MergeDict(object existingObj, object incomingObj, string dictKey,
            out int add, out int dup, out int same, out string mergedPreview, out string log)
        {
            add=dup=same=0; log="";
            var rootExisting = AsDict(existingObj);
            var rootIncoming = AsDict(incomingObj);
            var existMap = GetOrEmptyDict(rootExisting, dictKey);
            var incMap   = GetOrEmptyDict(rootIncoming, dictKey);
            var report = new StringBuilder();

            foreach (var kv in incMap)
            {
                var key = kv.Key; var val = kv.Value?.ToString() ?? "";
                if (existMap.TryGetValue(key, out var cur))
                {
                    var curStr = cur?.ToString() ?? "";
                    if (curStr == val) { same++; continue; }
                    dup++;
                    if (policy == DupPolicy.Replace)
                    {
                        existMap[key] = val;
                        report.AppendLine($"REPLACE: {key} : '{curStr}' → '{val}'");
                    }
                    else
                    {
                        report.AppendLine($"SKIP   : {key} : keep '{curStr}' (new '{val}')");
                    }
                }
                else
                {
                    add++;
                    existMap[key] = val;
                    report.AppendLine($"ADD    : {key} : '{val}'");
                }
            }

            rootExisting[dictKey] = existMap;
            mergedPreview = MiniJSON.Serialize(rootExisting, pretty:true);
            log = report.ToString();
        }

        object ApplyDict(object existingObj, object incomingObj, string dictKey)
        {
            int _, __, ___; string __p, __d;
            MergeDict(existingObj, incomingObj, dictKey, out _, out __, out ___, out __p, out __d);
            return existingObj;
        }

        // --------- Merge: intents ----------
        void MergeIntents(object existingObj, object incomingObj,
            out int add, out int dup, out int same, out string mergedPreview, out string log)
        {
            add=dup=same=0; log="";
            var rootExisting = AsDict(existingObj);
            var rootIncoming = AsDict(incomingObj);

            var existArr = GetOrEmptyList(rootExisting, "intents");
            var incArr   = GetOrEmptyList(rootIncoming, "intents");

            var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i=0;i<existArr.Count;i++)
            {
                var d = AsDict(existArr[i]);
                if (d.TryGetValue("name", out var nObj))
                {
                    var name = nObj?.ToString() ?? "";
                    if (!idx.ContainsKey(name)) idx[name]=i;
                }
            }

            var report = new StringBuilder();

            foreach (var item in incArr)
            {
                var d = AsDict(item);
                var name = d.TryGetValue("name", out var nObj) ? (nObj?.ToString() ?? "") : "";
                if (string.IsNullOrEmpty(name)) { errCount++; report.AppendLine("ERROR : intent missing 'name'"); continue; }

                if (idx.TryGetValue(name, out var at))
                {
                    dup++;
                    var oldSer = MiniJSON.Serialize(existArr[at], pretty:false);
                    var newSer = MiniJSON.Serialize(d, pretty:false);
                    if (oldSer == newSer) { same++; continue; }

                    if (policy == DupPolicy.Replace)
                    {
                        existArr[at] = d;
                        report.AppendLine($"REPLACE: intent '{name}'");
                    }
                    else
                    {
                        report.AppendLine($"SKIP   : intent '{name}' (kept existing)");
                    }
                }
                else
                {
                    add++;
                    existArr.Add(d);
                    report.AppendLine($"ADD    : intent '{name}'");
                }
            }

            rootExisting["intents"] = existArr;
            mergedPreview = MiniJSON.Serialize(rootExisting, pretty:true);
            log = report.ToString();
        }

        object ApplyIntents(object existingObj, object incomingObj)
        {
            int _, __, ___; string __p, __d;
            MergeIntents(existingObj, incomingObj, out _, out __, out ___, out __p, out __d);
            return existingObj;
        }

        // --------- Merge: generic named lists (Commands/Macros) ----------
        void MergeNamedList(object existingObj, object incomingObj, string topKey,
            out int add, out int dup, out int same, out string mergedPreview, out string log)
        {
            add=dup=same=0; log="";
            var rootExisting = AsDict(existingObj);
            var rootIncoming = AsDict(incomingObj);

            var existArr = GetOrEmptyList(rootExisting, topKey);
            var incArr   = GetOrEmptyList(rootIncoming, topKey);

            var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i=0;i<existArr.Count;i++)
            {
                var d = AsDict(existArr[i]);
                if (d.TryGetValue("name", out var nObj))
                {
                    var name = nObj?.ToString() ?? "";
                    if (!idx.ContainsKey(name)) idx[name]=i;
                }
            }

            var report = new StringBuilder();

            foreach (var item in incArr)
            {
                var d = AsDict(item);
                var name = d.TryGetValue("name", out var nObj) ? (nObj?.ToString() ?? "") : "";
                if (string.IsNullOrEmpty(name)) { errCount++; report.AppendLine($"ERROR : {topKey} item missing 'name'"); continue; }

                if (idx.TryGetValue(name, out var at))
                {
                    dup++;
                    var oldSer = MiniJSON.Serialize(existArr[at], pretty:false);
                    var newSer = MiniJSON.Serialize(d, pretty:false);
                    if (oldSer == newSer) { same++; continue; }

                    if (policy == DupPolicy.Replace)
                    {
                        existArr[at] = d;
                        report.AppendLine($"REPLACE: {topKey} '{name}'");
                    }
                    else
                    {
                        report.AppendLine($"SKIP   : {topKey} '{name}' (kept existing)");
                    }
                }
                else
                {
                    add++;
                    existArr.Add(d);
                    report.AppendLine($"ADD    : {topKey} '{name}'");
                }
            }

            rootExisting[topKey] = existArr;
            mergedPreview = MiniJSON.Serialize(rootExisting, pretty:true);
            log = report.ToString();
        }

        object ApplyNamedList(object existingObj, object incomingObj, string topKey)
        {
            int _, __, ___; string __p, __d;
            MergeNamedList(existingObj, incomingObj, topKey, out _, out __, out ___, out __p, out __d);
            return existingObj;
        }

        // --------- Mini helpers ----------
        static Dictionary<string, object> AsDict(object o) =>
            (o as Dictionary<string, object>) ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        static Dictionary<string, object> GetOrEmptyDict(Dictionary<string, object> root, string key)
        {
            if (root.TryGetValue(key, out var o) && o is Dictionary<string, object> d) return d;
            var nd = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            root[key] = nd;
            return nd;
        }

        static List<object> GetOrEmptyList(Dictionary<string, object> root, string key)
        {
            if (root.TryGetValue(key, out var o) && o is List<object> l) return l;
            var nl = new List<object>();
            root[key] = nl;
            return nl;
        }
    }

    // --------- Minimal JSON (parse/serialize) ----------
    static class MiniJSON
    {
        public static bool TryDeserialize(string json, out object obj)
        {
            obj = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try { obj = Deserialize(json); return true; } catch { return false; }
        }

        public static object Deserialize(string json) => Parser.Parse(json);
        public static string Serialize(object obj, bool pretty=false) => Serializer.Serialize(obj, pretty);

        sealed class Parser : IDisposable
        {
            StringReader json;

            Parser(string jsonString) { json = new StringReader(jsonString); }
            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                {
                    return instance.ParseValue();
                }
            }

            public void Dispose() { json.Dispose(); }

            object ParseValue()
            {
                EatWhitespace();
                if (json.Peek() == -1) return null;
                var c = PeekChar;
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': NextWord("true"); return true;
                    case 'f': NextWord("false"); return false;
                    case 'n': NextWord("null"); return null;
                    default: return ParseNumber();
                }
            }

            Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                NextChar(); // '{'
                for (;;)
                {
                    EatWhitespace();
                    if (PeekChar == '}') { NextChar(); break; }
                    var key = ParseString();
                    EatWhitespace();
                    if (NextChar() != ':') throw new Exception("Expected ':'");
                    var value = ParseValue();
                    table[key] = value;
                    EatWhitespace();
                    var ch = PeekChar;
                    if (ch == ',') { NextChar(); continue; }
                    if (ch == '}') { NextChar(); break; }
                    throw new Exception("Bad object");
                }
                return table;
            }

            List<object> ParseArray()
            {
                var array = new List<object>();
                NextChar(); // '['
                for (;;)
                {
                    EatWhitespace();
                    if (PeekChar == ']') { NextChar(); break; }
                    var val = ParseValue();
                    array.Add(val);
                    EatWhitespace();
                    var ch = PeekChar;
                    if (ch == ',') { NextChar(); continue; }
                    if (ch == ']') { NextChar(); break; }
                    throw new Exception("Bad array");
                }
                return array;
            }

            string ParseString()
            {
                var s = new StringBuilder();
                if (NextChar() != '"') throw new Exception("Expected '\"'");
                for (;;)
                {
                    if (json.Peek() == -1) throw new Exception("Unterminated string");
                    var ch = NextChar();
                    if (ch == '"') break;
                    if (ch == '\\')
                    {
                        if (json.Peek() == -1) break;
                        ch = NextChar();
                        switch (ch)
                        {
                            case '"': s.Append('"'); break;
                            case '\\': s.Append('\\'); break;
                            case '/': s.Append('/'); break;
                            case 'b': s.Append('\b'); break;
                            case 'f': s.Append('\f'); break;
                            case 'n': s.Append('\n'); break;
                            case 'r': s.Append('\r'); break;
                            case 't': s.Append('\t'); break;
                            case 'u':
                                var hex = new char[4];
                                for (int i=0;i<4;i++) hex[i] = NextChar();
                                s.Append((char)Convert.ToInt32(new string(hex), 16));
                                break;
                            default: s.Append(ch); break;
                        }
                    }
                    else s.Append(ch);
                }
                return s.ToString();
            }

            object ParseNumber()
            {
                var num = NextWord();
                if (num.IndexOf('.') != -1 || num.IndexOf('e')!=-1 || num.IndexOf('E')!=-1)
                {
                    if (double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
                }
                else
                {
                    if (long.TryParse(num, out var l)) return l;
                }
                throw new Exception("Bad number");
            }

            void EatWhitespace()
            {
                while (json.Peek() != -1 && char.IsWhiteSpace(PeekChar)) { NextChar(); }
                // also skip comment lines starting with #
                if (PeekChar == '#')
                {
                    while (json.Peek()!=-1 && NextChar()!='\n') {}
                    EatWhitespace();
                }
            }

            char PeekChar => Convert.ToChar(json.Peek());
            char NextChar() => Convert.ToChar(json.Read());

            string NextWord()
            {
                var sb = new StringBuilder();
                while (json.Peek()!=-1 && !" \t\r\n{}[],:\"".Contains(PeekChar)) sb.Append(NextChar());
                return sb.ToString();
            }
            void NextWord(string expect)
            {
                var got = new char[expect.Length];
                for (int i=0;i<expect.Length;i++) got[i] = NextChar();
                if (new string(got) != expect) throw new Exception("Expected "+expect);
            }
        }

        sealed class Serializer
        {
            StringBuilder builder;
            bool pretty;
            int indent;

            Serializer(bool pretty)
            {
                this.pretty = pretty;
                builder = new StringBuilder();
            }

            public static string Serialize(object obj, bool pretty=false)
            {
                var s = new Serializer(pretty);
                s.SerializeValue(obj);
                return s.builder.ToString();
            }

            void IndentInc() { indent += 2; }
            void IndentDec() { indent = Math.Max(0, indent - 2); }
            void IndentWrite() { if (pretty) builder.Append('\n').Append(' ', indent); }

            void SerializeValue(object value)
            {
                if (value == null) { builder.Append("null"); return; }
                if (value is string) { SerializeString((string)value); return; }
                if (value is bool) { builder.Append(((bool)value) ? "true" : "false"); return; }
                if (value is IDictionary) { SerializeObject((IDictionary)value); return; }
                if (value is Dictionary<string,object> d) { SerializeObject(d); return; }
                if (value is IList) { SerializeArray((IList)value); return; }
                if (value is double || value is float || value is long || value is int)
                {
                    builder.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                    return;
                }
                SerializeString(value.ToString());
            }

            void SerializeObject(IDictionary obj)
            {
                builder.Append('{'); if (pretty) IndentInc();
                bool first = true;
                foreach (DictionaryEntry e in obj)
                {
                    if (!first) builder.Append(','); if (pretty) IndentWrite();
                    SerializeString(e.Key.ToString());
                    builder.Append(pretty ? ": " : ":");
                    SerializeValue(e.Value);
                    first = false;
                }
                if (pretty) { IndentDec(); IndentWrite(); }
                builder.Append('}');
            }

            void SerializeObject(Dictionary<string,object> obj)
            {
                builder.Append('{'); if (pretty) IndentInc();
                bool first = true;
                foreach (var kv in obj)
                {
                    if (!first) builder.Append(','); if (pretty) IndentWrite();
                    SerializeString(kv.Key);
                    builder.Append(pretty ? ": " : ":");
                    SerializeValue(kv.Value);
                    first = false;
                }
                if (pretty) { IndentDec(); IndentWrite(); }
                builder.Append('}');
            }

            void SerializeArray(IList anArray)
            {
                builder.Append('['); if (pretty) IndentInc();
                bool first = true;
                for (int i=0;i<anArray.Count;i++)
                {
                    if (!first) builder.Append(','); if (pretty) IndentWrite();
                    SerializeValue(anArray[i]);
                    first = false;
                }
                if (pretty) { IndentDec(); IndentWrite(); }
                builder.Append(']');
            }

            void SerializeString(string str)
            {
                builder.Append('\"');
                foreach (var c in str)
                {
                    switch (c)
                    {
                        case '\"': builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\b': builder.Append("\\b"); break;
                        case '\f': builder.Append("\\f"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (c < ' ') builder.Append("\\u").Append(((int)c).ToString("x4"));
                            else builder.Append(c);
                            break;
                    }
                }
                builder.Append('\"');
            }
        }
    }
}
#endif
