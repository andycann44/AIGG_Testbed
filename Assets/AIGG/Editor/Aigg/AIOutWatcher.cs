using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    /// <summary>
    /// Watches Assets/AIGG/Temp/ai_out.json. On change/create, runs AISeparator.SplitAndOpen().
    /// No changes to Pre-Merge required.
    /// </summary>
    [InitializeOnLoad]
    internal static class AIOutWatcher
    {
        static readonly string Root = "Assets/AIGG/Temp";
        static readonly string FileRel = "ai_out.json";
        static readonly string FileProj = Path.Combine(Root, FileRel);
        static readonly string FileAbs = Path.GetFullPath(Path.Combine(Application.dataPath, "../", FileProj));
        static FileSystemWatcher _fsw;
        static double _nextAllowed;
        const double DebounceSec = 0.35;

        static AIOutWatcher()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FileAbs));
                _fsw = new FileSystemWatcher(Path.GetDirectoryName(FileAbs), Path.GetFileName(FileAbs));
                _fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName;
                _fsw.Changed += OnPing;
                _fsw.Created += OnPing;
                _fsw.Renamed += OnPing;
                _fsw.EnableRaisingEvents = true;
                EditorApplication.update += Tick;
                Debug.Log($"[AIOutWatcher] Watching: {FileProj}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AIOutWatcher] Failed to start watcher: " + ex.Message);
            }
        }

        static volatile bool _pending;
        static void OnPing(object s, FileSystemEventArgs e)
        {
            // Debounce file write bursts
            _pending = true;
            _nextAllowed = EditorApplication.timeSinceStartup + DebounceSec;
        }

        static void Tick()
        {
            if (!_pending) return;
            if (EditorApplication.timeSinceStartup < _nextAllowed) return;
            _pending = false;

            try
            {
                if (!File.Exists(FileAbs))
                {
                    Debug.LogWarning($"[AIOutWatcher] Event received but file missing: {FileProj}");
                    return;
                }
                // Give filesystem a breath
                System.Threading.Thread.Sleep(50);

                Debug.Log($"[AIOutWatcher] Detected update: {FileProj}");
                var wrote = AISeparator.SplitAndOpen(FileProj);
                Debug.Log($"[AISeparator] Wrote {wrote} bucket file(s) from {FileProj}");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIOutWatcher] Error handling update: " + ex);
            }
        }
    }
}
