#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Aim2Pro.AIGG.Tools
{
    public class GitStatusWindow : EditorWindow
    {
        [MenuItem("Window/Aim2Pro/Git/Repo Status")]
        public static void Open()
        {
            var w = GetWindow<GitStatusWindow>("Git Repo Status");
            w.minSize = new Vector2(680, 420);
            w.Refresh();
            w.Show();
        }

        string _repoRoot;
        string _branch = "?";
        string _aheadBehind = "";
        string _statusShort = "";
        string _logLocal = "";
        string _logRemote = "";
        string _remoteUrl = "";
        Vector2 _scroll;

        void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Repository", string.IsNullOrEmpty(_repoRoot) ? "(not a git repo?)" : _repoRoot);
                EditorGUILayout.LabelField("Branch", _branch);
                EditorGUILayout.LabelField("Ahead/Behind", string.IsNullOrEmpty(_aheadBehind) ? "-" : _aheadBehind);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh")) Refresh();
                    if (GUILayout.Button("Fetch")) RunAndRefresh("git fetch origin");
                    if (GUILayout.Button("Status")) RunAndRefresh(null); // just refresh
                    if (GUILayout.Button("Diff vs origin")) ShowDiff();
                    if (GUILayout.Button("Push")) RunAndRefresh("git push");
                    if (GUILayout.Button("Pull --rebase")) RunAndRefresh("git pull --rebase");
                    if (GUILayout.Button("Open Remote")) OpenRemote();
                }

                EditorGUILayout.Space(6);
                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                EditorGUILayout.LabelField("Short Status (git status -sb)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(_statusShort) ? "(clean)" : _statusShort, MessageType.None);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Last 5 commits (local)", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_logLocal, GUILayout.MinHeight(80));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Last 5 commits (origin)", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_logRemote, GUILayout.MinHeight(80));

                EditorGUILayout.EndScrollView();

                EditorGUILayout.HelpBox(
                    "This window shells out to your local 'git'.\n" +
                    "Make sure git is installed and the project folder is a git repository.",
                    MessageType.Info);
            }
        }

        void Refresh()
        {
            _repoRoot = FindRepoRoot(Application.dataPath);
            if (string.IsNullOrEmpty(_repoRoot))
            {
                _branch = "?";
                _aheadBehind = "";
                _statusShort = "No .git found above Assets/";
                _logLocal = _logRemote = _remoteUrl = "";
                Repaint();
                return;
            }

            _branch = RunGit("rev-parse --abbrev-ref HEAD").Trim();
            _remoteUrl = RunGit("remote get-url origin").Trim();

            // status -sb (gives ahead/behind info on first line)
            _statusShort = RunGit("status -sb");
            var first = _statusShort.Split('\n')[0];
            // pattern like: ## main...origin/main [ahead 1, behind 2]
            var m = Regex.Match(first ?? "", @"##\s+([^\s\.]+)\.\.\.([^\s]+)\s*(\[(.*?)\])?");
            string aheadBehindRaw = m.Success ? (m.Groups.Count >= 5 ? m.Groups[4].Value : "") : "";
            _aheadBehind = string.IsNullOrEmpty(aheadBehindRaw) ? "in sync" : aheadBehindRaw;

            _logLocal = RunGit("log --oneline -n 5");
            var remoteRef = "origin/" + _branch;
            _logRemote = RunGit($"log --oneline -n 5 {remoteRef}");

            Repaint();
        }

        void ShowDiff()
        {
            if (string.IsNullOrEmpty(_branch)) return;
            var diff = RunGit($"diff --stat origin/{_branch}..HEAD");
            EditorUtility.DisplayDialog("Diff vs origin", string.IsNullOrEmpty(diff) ? "(no differences)" : diff, "OK");
        }

        void OpenRemote()
        {
            if (string.IsNullOrEmpty(_remoteUrl))
            {
                EditorUtility.DisplayDialog("Remote URL", "No 'origin' remote configured.", "OK");
                return;
            }
            // Open in browser (supports HTTPS; SSH will open your git host app if handler set)
            Application.OpenURL(_remoteUrl.Replace(".git",""));
        }

        void RunAndRefresh(string cmd)
        {
            if (!string.IsNullOrEmpty(cmd))
            {
                var output = RunGit(cmd);
                UnityEngine.Debug.Log($"[Git] {cmd}\n{output}");
            }
            Refresh();
        }

        // ---------- helpers ----------

        static string FindRepoRoot(string assetsPath)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(Path.Combine(assetsPath, "..")));
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        string RunGit(string args)
        {
            if (string.IsNullOrEmpty(_repoRoot)) return "(no repo)";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = args,
                    WorkingDirectory = _repoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using var p = Process.Start(psi);
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0 && !string.IsNullOrEmpty(stderr))
                    return stdout + "\n" + stderr;
                return stdout;
            }
            catch (Exception e)
            {
                return $"(git error) {e.Message}";
            }
        }
    }
}
#endif
