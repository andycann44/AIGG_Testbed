#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.IO.Compression;

// avoid name clashes
using UDebug = UnityEngine.Debug;
using ZipArchive = System.IO.Compression.ZipArchive;
using ZipFile = System.IO.Compression.ZipFile;
using ZipArchiveMode = System.IO.Compression.ZipArchiveMode;
using ZipCompressionLevel = System.IO.Compression.CompressionLevel;

namespace Aim2Pro.Tools
{
    public static class ClearRestorePointsMenu
    {
        static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName.Replace("\\","/");
        static string BackupsDir  => Path.Combine(ProjectRoot, "_Backups").Replace("\\","/");
        static string SpecBakDir  => Path.Combine(ProjectRoot, "StickerDash_Status/Backups/Spec").Replace("\\","/");

        [MenuItem("Window/Aim2Pro/Tools/Clear All Restore Points…", priority = 2050)]
        public static void ClearAllRestorePoints()
        {
            Directory.CreateDirectory(BackupsDir);
            Directory.CreateDirectory(SpecBakDir);

            int choice = EditorUtility.DisplayDialogComplex(
                "Clear Restore Points",
                "This will permanently delete all project restore zips in _Backups/.\n" +
                "Optionally also delete Spec backups (StickerDash_Status/Backups/Spec).\n\n" +
                "Choose what to do:",
                "Clear Only",
                "Cancel",
                "Clear + New Baseline"
            );
            if (choice == 1) return; // Cancel
            bool makeBaseline = (choice == 2);

            bool includeSpec = EditorUtility.DisplayDialog(
                "Include Spec Backups?",
                "Also delete Spec backups (lexicon.json.bak_*, schema.json.bak_*, etc.)?",
                "Yes, include Spec", "No, only project zips"
            );

            try
            {
                int removedZips = ClearProjectZips();
                int removedSpec = includeSpec ? ClearSpecBackups() : 0;

                string baselinePath = null;
                if (makeBaseline)
                {
                    baselinePath = CreateBaselineZip();
                }

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Done",
                    $"Removed project zips: {removedZips}\n" +
                    (includeSpec ? $"Removed spec backups: {removedSpec}\n" : "") +
                    (makeBaseline ? $"New baseline: {baselinePath}\n" : "") +
                    "✓",
                    "OK");
            }
            catch (Exception ex)
            {
                UDebug.LogError("[Aim2Pro] Clear restore points failed: " + ex);
                EditorUtility.DisplayDialog("Failed", ex.Message, "OK");
            }
        }

        static int ClearProjectZips()
        {
            if (!Directory.Exists(BackupsDir)) return 0;
            var zips = Directory.GetFiles(BackupsDir, "*.zip", SearchOption.TopDirectoryOnly);
            int count = 0;
            foreach (var z in zips)
            {
                try { File.Delete(z); count++; } catch { /* ignore */ }
            }
            return count;
        }

        static int ClearSpecBackups()
        {
            if (!Directory.Exists(SpecBakDir)) return 0;
            var files = Directory.GetFiles(SpecBakDir, "*", SearchOption.TopDirectoryOnly)
                                 .Where(p => p.Contains(".bak_") || p.Contains(".prewrite_"))
                                 .ToArray();
            int count = 0;
            foreach (var f in files)
            {
                try { File.Delete(f); count++; } catch { /* ignore */ }
            }
            return count;
        }

        static string CreateBaselineZip()
        {
            Directory.CreateDirectory(BackupsDir);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipOut = Path.Combine(BackupsDir, $"SDv1-{stamp}.zip");

            using (var zip = ZipFile.Open(zipOut, ZipArchiveMode.Create))
            {
                AddDir(zip, Path.Combine(ProjectRoot, "Assets"), "Assets");
                AddDir(zip, Path.Combine(ProjectRoot, "ProjectSettings"), "ProjectSettings");

                var manifest = Path.Combine(ProjectRoot, "Packages", "manifest.json");
                if (File.Exists(manifest))
                    zip.CreateEntryFromFile(manifest, "Packages/manifest.json", ZipCompressionLevel.Optimal);

                var note = zip.CreateEntry("_note.txt");
                using var w = new StreamWriter(note.Open());
                w.WriteLine("Baseline created after clearing all restore points");
                w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                w.WriteLine($"Unity: {Application.unityVersion}");
            }

            UDebug.Log("[Aim2Pro] New baseline → " + zipOut);
            return zipOut.Replace("\\","/");
        }

        static void AddDir(ZipArchive zip, string src, string relRoot)
        {
            if (!Directory.Exists(src)) return;
            var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                var rel = relRoot + f.Substring(src.Length).Replace('\\','/');
                zip.CreateEntryFromFile(f, rel, ZipCompressionLevel.Optimal);
            }
        }
    }
}
#endif
