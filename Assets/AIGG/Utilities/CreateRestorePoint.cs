#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.IO.Compression;

// Aliases to avoid ambiguity with Unity's Debug and CompressionLevel
using UDebug = UnityEngine.Debug;
using ZipArchive = System.IO.Compression.ZipArchive;
using ZipFile = System.IO.Compression.ZipFile;
using ZipArchiveMode = System.IO.Compression.ZipArchiveMode;
using ZipCompressionLevel = System.IO.Compression.CompressionLevel;
using Proc = System.Diagnostics.Process;

namespace Aim2Pro
{
    public static class RestorePoint
    {
        [MenuItem("Window/Aim2Pro/Tools/Create Restore Point", priority = 2000)]
        public static void CreateRestorePoint()
        {
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;

                // Keep bash helper in sync if pref is on
                if (ScriptPrefs.AutoCreateBash) EnsureBashHelper(projectRoot);

                var backupsDir = Path.Combine(projectRoot, "_Backups");
                Directory.CreateDirectory(backupsDir);

                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var zipPath = Path.Combine(backupsDir, $"SDv1-{stamp}.zip");

                using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    AddDirToZip(Path.Combine(projectRoot, "Assets"), "Assets", zip);
                    AddDirToZip(Path.Combine(projectRoot, "ProjectSettings"), "ProjectSettings", zip);

                    var manifest = Path.Combine(projectRoot, "Packages", "manifest.json");
                    if (File.Exists(manifest))
                        zip.CreateEntryFromFile(manifest, "Packages/manifest.json", ZipCompressionLevel.Optimal);

                    var metaEntry = zip.CreateEntry("_restorepoint.txt");
                    using var writer = new StreamWriter(metaEntry.Open());
                    writer.WriteLine($"Project: {Path.GetFileName(projectRoot)}");
                    writer.WriteLine($"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"Unity: {Application.unityVersion}");
                    writer.WriteLine("Includes: Assets/, ProjectSettings/, Packages/manifest.json");
                }

                PruneOldBackups(backupsDir, keep: 20);

                EditorUtility.DisplayDialog("Restore Point Created", $"Saved:\n{zipPath}", "OK");
                UDebug.Log($"[Aim2Pro] Restore point created → {zipPath}");
            }
            catch (Exception ex)
            {
                UDebug.LogError($"[Aim2Pro] Restore point failed: {ex}");
                EditorUtility.DisplayDialog("Restore Point Failed", ex.Message, "OK");
            }
        }

        static void AddDirToZip(string srcDir, string relRoot, ZipArchive zip)
        {
            if (!Directory.Exists(srcDir)) return;
            var files = Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                var rel = relRoot + f.Substring(srcDir.Length).Replace('\\','/');
                zip.CreateEntryFromFile(f, rel, ZipCompressionLevel.Optimal);
            }
        }

        static void PruneOldBackups(string backupsDir, int keep)
        {
            var zips = Directory.GetFiles(backupsDir, "*.zip")
                .OrderByDescending(File.GetCreationTimeUtc)
                .ToList();
            foreach (var old in zips.Skip(keep)) { try { File.Delete(old); } catch {} }
        }

        static void EnsureBashHelper(string projectRoot)
        {
            var path = Path.Combine(projectRoot, "a2p_restore_point.sh");
            var marker = "# Aim2Pro Restore Point";
            var needsWrite = !File.Exists(path) || !File.ReadAllText(path).Contains(marker);

            if (needsWrite)
            {
                File.WriteAllText(path, BashScript);
#if UNITY_EDITOR_WIN
                // no chmod on Windows
#else
                try { Proc.Start("/bin/chmod", $"+x \"{path}\""); } catch {}
#endif
                AssetDatabase.Refresh();
                UDebug.Log("[Aim2Pro] Wrote a2p_restore_point.sh (bash helper).");
            }
        }

        const string BashScript = @"#!/usr/bin/env bash
# Aim2Pro Restore Point
set -euo pipefail
here=""$(cd ""$(dirname ""${BASH_SOURCE[0]}"")"" && pwd)""
cd ""$here""
BACKUPS_DIR=""${BACKUPS_DIR:-_Backups}""
KEEP=""${KEEP:-20}""
STAMP=""$(date +""%Y%m%d_%H%M%S"")""
ZIP=""${BACKUPS_DIR}/SDv1-${STAMP}.zip""
mkdir -p ""$BACKUPS_DIR""
if [[ ! -d ""Assets"" || ! -d ""ProjectSettings"" ]]; then
  echo ""Run from Unity project root (must contain Assets/ and ProjectSettings/)"" >&2
  exit 1
fi
zip -rq ""$ZIP"" Assets ProjectSettings Packages/manifest.json
if [[ ""${UNPACKED:-0}"" == ""1"" || ""${1:-}"" == ""--unpacked"" ]]; then
  FOLDER=""${BACKUPS_DIR}/SDv1-${STAMP}""
  mkdir -p ""$FOLDER""
  rsync -a --exclude='Library/' --exclude='Temp/' --exclude='Logs/' Assets ProjectSettings ""$FOLDER/""
  mkdir -p ""$FOLDER/Packages""
  cp -f Packages/manifest.json ""$FOLDER/Packages/manifest.json"" || true
fi
ls -1t ""${BACKUPS_DIR}""/SDv1-*.zip 2>/dev/null | awk ""NR>${KEEP}"" | while read -r f; do rm -f ""$f""; done
echo ""Restore point created -> $ZIP""";
    }
}
#endif
