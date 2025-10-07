#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.Editor.Tools
{
    // Moves superseded .cs files OUT of Assets/ into StickerDash_Status/_Superseded/<stamp>/...
    // Activate by adding at the top of a new .cs file:
    //   // A2P_SUPERSEDES: Assets/StickerDash/AIGG/Editor/Track/OldTrackGen.cs, Assets/.../LegacyThing.cs
    //   // A2P_REPLACES_GLOB: Assets/StickerDash/**/Old*Track*.cs
    //
    // Toggle auto via menu: Window/Aim2Pro/Tools/Tidy/Toggle Auto Supersede
    // Manual run: Window/Aim2Pro/Tools/Tidy/Scan & Move Now
    public class A2P_TidySupersede : AssetPostprocessor
    {
        const string PREF_AUTO = "A2P_TIDY_SUPERSEDE_AUTO";
        const string MARK_SUP  = "A2P_SUPERSEDES:";
        const string MARK_GLOB = "A2P_REPLACES_GLOB:";
        const int    HEADER_SCAN_LINES = 120; // only scan small header

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName.Replace('\\','/');
        static string QuarantineRoot => Path.Combine(ProjectRoot, "StickerDash_Status/_Superseded").Replace('\\','/');

        // ===== Auto: when new assets are imported =====
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!EditorPrefs.GetBool(PREF_AUTO, false)) return;

            var toMove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in imported)
            {
                if (!a.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var tgt in ReadDirectives(a))
                    CollectTargets(a, tgt, toMove);
            }

            if (toMove.Count > 0) ConfirmAndQuarantine("Supersede (auto)", toMove);
        }

        // ===== Menu: toggle auto =====
        [MenuItem("Window/Aim2Pro/Tools/Tidy/Toggle Auto Supersede")]
        static void ToggleAuto()
        {
            bool newVal = !EditorPrefs.GetBool(PREF_AUTO, false);
            EditorPrefs.SetBool(PREF_AUTO, newVal);
            EditorUtility.DisplayDialog("Aim2Pro Tidy", "Auto Supersede is now " + (newVal ? "ON" : "OFF"), "OK");
        }

        // ===== Menu: scan all .cs for directives and move =====
        [MenuItem("Window/Aim2Pro/Tools/Tidy/Scan & Move Now")]
        static void ScanNow()
        {
            var allCs = AssetDatabase.FindAssets("t:Script")
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
            var toMove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in allCs)
                foreach (var tgt in ReadDirectives(path))
                    CollectTargets(path, tgt, toMove);

            if (toMove.Count == 0)
                EditorUtility.DisplayDialog("Aim2Pro Tidy", "No supersede directives found.", "OK");
            else
                ConfirmAndQuarantine("Supersede (manual)", toMove);
        }

        // ===== Read directives from a .cs file header =====
        struct Directive { public string literalOrGlob; public bool isGlob; }
        static IEnumerable<Directive> ReadDirectives(string assetPath)
        {
            var list = new List<Directive>();
            var full = Path.Combine(ProjectRoot, assetPath).Replace('\\','/');
            if (!File.Exists(full)) yield break;

            try
            {
                using var sr = new StreamReader(full);
                for (int i = 0; i < HEADER_SCAN_LINES && !sr.EndOfStream; i++)
                {
                    var line = sr.ReadLine();
                    if (line == null) break;
                    var t = line.Trim();

                    int iSup = t.IndexOf(MARK_SUP, StringComparison.Ordinal);
                    if (iSup >= 0)
                    {
                        var rest = t.Substring(iSup + MARK_SUP.Length).Trim();
                        foreach (var part in rest.Split(new[] {',',';'}, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var rel = part.Trim().Replace('\\','/');
                            if (!string.IsNullOrEmpty(rel))
                                list.Add(new Directive { literalOrGlob = rel, isGlob = false });
                        }
                    }

                    int iGlob = t.IndexOf(MARK_GLOB, StringComparison.Ordinal);
                    if (iGlob >= 0)
                    {
                        var pat = t.Substring(iGlob + MARK_GLOB.Length).Trim().Replace('\\','/');
                        if (!string.IsNullOrEmpty(pat))
                            list.Add(new Directive { literalOrGlob = pat, isGlob = true });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[A2P Tidy] ReadDirectives failed for {assetPath}: {ex.Message}");
            }

            foreach (var d in list) yield return d;
        }

        // ===== Expand directives to concrete asset paths =====
        static void CollectTargets(string sourceAsset, Directive dir, HashSet<string> toMove)
        {
            if (dir.isGlob)
            {
                foreach (var path in GlobAssets(dir.literalOrGlob))
                    if (!SamePath(path, sourceAsset))
                        toMove.Add(path);
            }
            else
            {
                var path = dir.literalOrGlob.Replace('\\','/');
                if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                    AssetExists(path) && !SamePath(path, sourceAsset))
                    toMove.Add(path);
            }
        }

        static bool AssetExists(string assetsPath)
        {
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetsPath);
            return obj != null || File.Exists(Path.Combine(ProjectRoot, assetsPath));
        }

        // Simple glob to regex: supports * and ** and ?
        static IEnumerable<string> GlobAssets(string pattern)
        {
            pattern = pattern.Replace('\\','/').Trim();
            if (!pattern.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) yield break;

            string rx = "^" + Regex.Escape(pattern)
                                .Replace(@"\*\*", @"(.+?)")
                                .Replace(@"\*", @"[^/]*")
                                .Replace(@"\?", @".") + "$";

            var re = new Regex(rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            foreach (var guid in AssetDatabase.FindAssets("t:Script"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid).Replace('\\','/');
                if (p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && re.IsMatch(p))
                    yield return p;
            }
        }

        static bool SamePath(string a, string b) =>
            string.Equals(a.Replace('\\','/'), b.Replace('\\','/'), StringComparison.OrdinalIgnoreCase);

        // ===== Safe move (overwrite if exists) =====
        static void MoveOverwrite(string src, string dst)
        {
            try
            {
                var dir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(src, dst);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[A2P Tidy] MoveOverwrite failed {src} -> {dst}: {ex.Message}");
            }
        }

        // ===== Move out of Assets (with structure), plus .meta =====
        static void ConfirmAndQuarantine(string title, IEnumerable<string> paths)
        {
            var unique = paths.Distinct(StringComparer.OrdinalIgnoreCase).Where(p => p.StartsWith("Assets/")).ToList();
            if (unique.Count == 0) return;

            var msg = $"{title} will move these files out of Assets:\n\n" + string.Join("\n", unique);
            if (!EditorUtility.DisplayDialog("Aim2Pro Tidy — Confirm", msg, "Move to _Superseded", "Cancel"))
                return;

            var stampRoot = Path.Combine(QuarantineRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss")).Replace('\\','/');
            foreach (var rel in unique)
            {
                try
                {
                    var src = Path.Combine(ProjectRoot, rel).Replace('\\','/');
                    var dst = Path.Combine(stampRoot, rel).Replace('\\','/');

                    if (File.Exists(src))
                    {
                        MoveOverwrite(src, dst);
                        var meta = src + ".meta";
                        if (File.Exists(meta))
                            MoveOverwrite(meta, dst + ".meta");
                        Debug.Log($"[A2P Tidy] Moved → {dst}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[A2P Tidy] Failed to move {rel}: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(stampRoot);
        }
    }
}
#endif
