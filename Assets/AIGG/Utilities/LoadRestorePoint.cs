#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using ZipArchive = System.IO.Compression.ZipArchive;
using ZipFile = System.IO.Compression.ZipFile;
using ZipArchiveMode = System.IO.Compression.ZipArchiveMode;
using UDebug = UnityEngine.Debug;

namespace Aim2Pro.Tools
{
    public static class LoadRestorePointMenu
    {
        static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName.Replace("\\","/");
        static string BackupsDir  => Path.Combine(ProjectRoot, "_Backups").Replace("\\","/");

        [MenuItem("Window/Aim2Pro/Tools/Load Restore Point", priority = 2001)]
        public static void LoadRestorePoint()
        {
            Directory.CreateDirectory(BackupsDir);
            var zip = EditorUtility.OpenFilePanel("Choose backup zip", BackupsDir, "zip");
            if (string.IsNullOrEmpty(zip)) return;

            int choice = EditorUtility.DisplayDialogComplex(
                "Restore Mode",
                $"Restore from:\n{Path.GetFileName(zip)}\n\nChoose mode:",
                "Soft (overwrite only)", "Cancel", "Hard (delete & replace)"
            );
            if (choice == 1) return; // Cancel
            bool hard = (choice == 2);

            try
            {
                try { CreateSafetyBackup(); } catch (Exception e) { UDebug.LogWarning("[Aim2Pro] Safety backup failed: " + e.Message); }

                if (hard)
                {
                    var assets = Path.Combine(ProjectRoot, "Assets");
                    var proj   = Path.Combine(ProjectRoot, "ProjectSettings");
                    if (Directory.Exists(assets)) Directory.Delete(assets, true);
                    if (Directory.Exists(proj))   Directory.Delete(proj,   true);
                    Directory.CreateDirectory(assets);
                    Directory.CreateDirectory(proj);
                }

                using (ZipArchive za = ZipFile.OpenRead(zip))
                {
                    foreach (var entry in za.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
                        var outPath = Path.Combine(ProjectRoot, entry.FullName).Replace("\\","/");
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                        entry.ExtractToFile(outPath, overwrite: true);
                    }
                }

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Restore Complete",
                    $"Restored from {Path.GetFileName(zip)}\nMode: {(hard ? "HARD" : "SOFT")}",
                    "OK");
            }
            catch (Exception ex)
            {
                UDebug.LogError("[Aim2Pro] Restore failed: " + ex);
                EditorUtility.DisplayDialog("Restore Failed", ex.Message, "OK");
            }
        }

        static void CreateSafetyBackup()
        {
            Directory.CreateDirectory(BackupsDir);
            var stamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipOut = Path.Combine(BackupsDir, $"SDv1-{stamp}-before-restore.zip");

            using (var zip = ZipFile.Open(zipOut, ZipArchiveMode.Create))
            {
                AddDir(zip, Path.Combine(ProjectRoot,"Assets"), "Assets");
                AddDir(zip, Path.Combine(ProjectRoot,"ProjectSettings"), "ProjectSettings");
                var manifest = Path.Combine(ProjectRoot,"Packages","manifest.json");
                if (File.Exists(manifest))
                    zip.CreateEntryFromFile(manifest, "Packages/manifest.json", System.IO.Compression.CompressionLevel.Optimal);

                var note = zip.CreateEntry("_note.txt");
                using var w = new StreamWriter(note.Open());
                w.WriteLine("Safety backup before restore");
                w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
        }

        static void AddDir(ZipArchive zip, string src, string relRoot)
        {
            if (!Directory.Exists(src)) return;
            var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                var rel = relRoot + f.Substring(src.Length).Replace('\\','/');
                zip.CreateEntryFromFile(f, rel, System.IO.Compression.CompressionLevel.Optimal);
            }
        }
    }
}
#endif
