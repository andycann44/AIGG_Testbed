// SPDX-License-Identifier: MIT
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG.Terminal
{
    public class A2P_TerminalWindow : EditorWindow
    {
        private const string PREF_SHELL = "AIGG.Terminal.Shell";
        private const string PREF_WORKDIR = "AIGG.Terminal.WorkDir";
        private const int MAX_LINES = 4000;

        private string _shell = "/bin/bash";
        private string _workDir;
        private string _command = "echo Hello from AIGG && uname -a && bash --version | head -n 1";
        private readonly List<string> _buffer = new List<string>(1024);
        private Vector2 _scroll;
        private bool _autoScroll = true;
        private int _bashMajor = -1;

        private A2P_TerminalProcess _proc;
        private string _logPath;

        [MenuItem("Window/Aim2Pro/Terminal/Terminal")]
        public static void Open()
        {
            var w = GetWindow<A2P_TerminalWindow>("Terminal");
            w.minSize = new Vector2(760, 460);
            w.Show(); w.Focus();
        }

        private void OnEnable()
        {
            _shell = EditorPrefs.GetString(PREF_SHELL, "/bin/bash");
            _workDir = EditorPrefs.GetString(PREF_WORKDIR, Directory.GetCurrentDirectory());
            _proc = new A2P_TerminalProcess();
            _logPath = Path.Combine("ProjectSettings", "AIGG_Terminal.log");
            _proc.InitLog(_logPath);
            EditorApplication.update += OnUpdate;

            // Ensure we stop on domain reload / playmode changes
            AssemblyReloadEvents.beforeAssemblyReload += HandleAssemblyReload;
            EditorApplication.playModeStateChanged += HandlePlaymodeChange;

            DetectShell();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleAssemblyReload;
            EditorApplication.playModeStateChanged -= HandlePlaymodeChange;
            _proc?.Dispose();
        }

        private void HandleAssemblyReload() { try { _proc?.Stop(); } catch { } }
        private void HandlePlaymodeChange(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode || s == PlayModeStateChange.ExitingPlayMode)
            { try { _proc?.Stop(); } catch { } }
        }

        private void OnGUI()
        {
            GUILayout.Label("AIGG Terminal (bash, crash-safe)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Shell", GUILayout.Width(50));
                _shell = EditorGUILayout.TextField(_shell);
                if (GUILayout.Button("Detect", GUILayout.Width(80))) DetectShell();
                if (GUILayout.Button("Use Homebrew", GUILayout.Width(110))) UseBrewBashIfAvailable();
                if (GUILayout.Button("Save", GUILayout.Width(70))) EditorPrefs.SetString(PREF_SHELL, _shell);
                GUILayout.FlexibleSpace();
                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", GUILayout.Width(110));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("WorkDir", GUILayout.Width(60));
                _workDir = EditorGUILayout.TextField(_workDir);
                if (GUILayout.Button("ProjRoot", GUILayout.Width(90))) _workDir = Directory.GetCurrentDirectory();
                if (GUILayout.Button("SpecDir", GUILayout.Width(90))) _workDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets/AIGG/Spec");
                if (GUILayout.Button("Save", GUILayout.Width(70))) EditorPrefs.SetString(PREF_WORKDIR, _workDir);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Log", GUILayout.Width(100))) EditorUtility.OpenWithDefaultApp(_logPath);
            }

            if (_bashMajor > -1)
            {
                var msg = "Detected bash major: " + _bashMajor + (IsOldBash() ? " (macOS /bin/bash is 3.x; 'mapfile' NOT available)" : "");
                EditorGUILayout.HelpBox(msg, IsOldBash() ? MessageType.Warning : MessageType.Info);
            }

            if (IsOldBash() && (_command.Contains("mapfile") || _command.Contains("readarray")))
            {
                EditorGUILayout.HelpBox("Your command uses 'mapfile/readarray', but /bin/bash 3.x lacks it. Install Homebrew bash and click 'Use Homebrew', or rewrite the script.", MessageType.Error);
            }

            GUILayout.Space(4);
            GUILayout.Label("Command", EditorStyles.miniBoldLabel);
            _command = EditorGUILayout.TextArea(_command, GUILayout.MinHeight(64));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !(_proc?.IsRunning ?? false);
                if (GUILayout.Button("Run", GUILayout.Width(90))) SafeRun();
                GUI.enabled = (_proc?.IsRunning ?? false);
                if (GUILayout.Button("Stop", GUILayout.Width(90))) _proc?.Stop();
                GUI.enabled = true;
                if (GUILayout.Button("Clear", GUILayout.Width(90))) _buffer.Clear();
                if (GUILayout.Button("cd Spec && ls", GUILayout.Width(120))) _command = "cd Assets/AIGG/Spec && ls -la";
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6);
            GUILayout.Label("Output", EditorStyles.miniBoldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _buffer.Count; i++)
                GUILayout.Label(_buffer[i], EditorStyles.label);
            EditorGUILayout.EndScrollView();

            if (_autoScroll)
                _scroll.y = float.MaxValue;
        }

        private void OnUpdate()
        {
            try
            {
                if (_proc == null) return;
                int guard = 0;
                while (_proc.Lines.TryDequeue(out var line))
                {
                    _buffer.Add(line);
                    if (_buffer.Count > MAX_LINES)
                        _buffer.RemoveRange(0, _buffer.Count - MAX_LINES);
                    guard++;
                    if (guard > 1500) break;
                }
            }
            catch (Exception e)
            {
                _buffer.Add("[AIGG Terminal] update error: " + e.Message);
            }
        }

        private void SafeRun()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_command))
                {
                    _buffer.Add("[AIGG Terminal] No command.");
                    return;
                }
                if (IsOldBash() && (_command.Contains("mapfile") || _command.Contains("readarray")))
                {
                    bool cont = EditorUtility.DisplayDialog("AIGG Terminal",
                        "This command uses 'mapfile/readarray', which is not available in macOS /bin/bash (3.x).\nInstall Homebrew bash and click 'Use Homebrew' or rewrite the command.\nContinue anyway?",
                        "Continue", "Cancel");
                    if (!cont) return;
                }
                _proc?.Start(_shell, _command, _workDir);
            }
            catch (Exception e)
            {
                _buffer.Add("[AIGG Terminal] Error: " + e.Message);
            }
        }

        private void DetectShell()
        {
            try
            {
                _bashMajor = A2P_TerminalProcess.GetBashMajorVersion(_shell);
            }
            catch { _bashMajor = -1; }
        }

        private void UseBrewBashIfAvailable()
        {
#if UNITY_EDITOR_OSX
            string[] candidates = { "/opt/homebrew/bin/bash", "/usr/local/bin/bash" };
            foreach (var c in candidates)
            {
                if (File.Exists(c))
                {
                    _shell = c;
                    DetectShell();
                    Repaint();
                    return;
                }
            }
            EditorUtility.DisplayDialog("AIGG Terminal", "Homebrew bash not found at /opt/homebrew/bin/bash or /usr/local/bin/bash.", "OK");
#else
            EditorUtility.DisplayDialog("AIGG Terminal", "Homebrew bash option is macOS-only.", "OK");
#endif
        }

        private bool IsOldBash() => _bashMajor > 0 && _bashMajor < 4;
    }
}
#endif
