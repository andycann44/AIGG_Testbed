// SPDX-License-Identifier: MIT
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG.Terminal
{
    public class A2P_TerminalWindow : EditorWindow
    {
        private const string PREF_SHELL = "AIGG.Terminal.Shell";
        private const int MAX_LINES = 4000;

        private string _shell = "/bin/bash";
        private string _command = "echo Hello from AIGG && uname -a";
        private readonly List<string> _buffer = new List<string>(1024);
        private Vector2 _scroll;
        private bool _autoScroll = true;

        private A2P_TerminalProcess _proc;

        [MenuItem("Window/Aim2Pro/Terminal/Terminal")]
        public static void Open()
        {
            var w = GetWindow<A2P_TerminalWindow>("Terminal");
            w.minSize = new Vector2(720, 420);
            w.Show();
            w.Focus();
        }

        private void OnEnable()
        {
            _shell = EditorPrefs.GetString(PREF_SHELL, "/bin/bash");
            _proc = new A2P_TerminalProcess();
            EditorApplication.update += OnUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            _proc?.Dispose();
        }

        private void OnGUI()
        {
            GUILayout.Label("AIGG Terminal (bash)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Shell", GUILayout.Width(40));
                _shell = EditorGUILayout.TextField(_shell);
                if (GUILayout.Button("Save", GUILayout.Width(60)))
                {
                    if (string.IsNullOrWhiteSpace(_shell)) _shell = "/bin/bash";
                    EditorPrefs.SetString(PREF_SHELL, _shell);
                }
                GUILayout.FlexibleSpace();
                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", GUILayout.Width(100));
            }

            GUILayout.Space(4);

            GUILayout.Label("Command", EditorStyles.miniBoldLabel);
            _command = EditorGUILayout.TextArea(_command, GUILayout.MinHeight(48));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !(_proc?.IsRunning ?? false);
                if (GUILayout.Button("Run", GUILayout.Width(80)))
                    SafeRun();
                GUI.enabled = (_proc?.IsRunning ?? false);
                if (GUILayout.Button("Stop", GUILayout.Width(80)))
                    _proc?.Stop();
                GUI.enabled = true;
                if (GUILayout.Button("Clear", GUILayout.Width(80)))
                    _buffer.Clear();
                if (GUILayout.Button("SpecDir cd", GUILayout.Width(100)))
                    _command = "cd Assets/AIGG/Spec && ls -la";
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
            if (_proc == null) return;
            // Drain output without blocking
            int guard = 0;
            while (_proc.Lines.TryDequeue(out var line))
            {
                _buffer.Add(line);
                if (_buffer.Count > MAX_LINES)
                    _buffer.RemoveRange(0, _buffer.Count - MAX_LINES);
                guard++;
                if (guard > 2000) break; // avoid long stalls on giant bursts
            }
        }

        private void SafeRun()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_command)) { _buffer.Add("[AIGG Terminal] No command."); return; }
                _proc?.Start(_shell, _command, null);
            }
            catch (Exception e)
            {
                _buffer.Add("[AIGG Terminal] Error: " + e.Message);
            }
        }
    }
}
#endif
