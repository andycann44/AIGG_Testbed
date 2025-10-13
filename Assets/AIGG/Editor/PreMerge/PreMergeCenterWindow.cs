using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    // Menu: Window/Aim2Pro/Aigg/Pre-Merge Center
    public class PreMergeCenterWindow : EditorWindow
    {
        private static string _incoming = "";
        private Vector2 _scrollIncoming, _scrollPreview;
        private int _specIndex = 0;
        private readonly string[] _specFiles = new string[] {
            "intents.json","lexicon.json","macros.json","commands.json","fieldmap.json","registry.json","schema.json"
        };
        private string _status = "Ready.";
        private string _preview = "";
        private bool _autoPreview = true;

        [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge Center", priority = 1900)]
        public static void ShowWindow()
        {
            var w = GetWindow<PreMergeCenterWindow>("Pre-Merge Center");
            w.minSize = new Vector2(780, 520);
            w.Show();
        }

        // Called by API
        public static void OpenWithJson(string json)
        {
            _incoming = json ?? "";
            var w = GetWindow<PreMergeCenterWindow>("Pre-Merge Center");
            w.Focus();
            if (w._autoPreview) w.TryBuildPreview();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                _specIndex = EditorGUILayout.Popup("Target Spec", _specIndex, _specFiles);
                _autoPreview = EditorGUILayout.ToggleLeft("Auto Preview", _autoPreview, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate JSON", GUILayout.Height(22))) ValidateIncoming();
                if (GUILayout.Button("Build Preview", GUILayout.Height(22))) TryBuildPreview();
            }

            EditorGUILayout.Space();

            // Split: Incoming (left) | Preview (right)
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(position.width/2 - 10)))
                {
                    EditorGUILayout.LabelField("Incoming Patch JSON", EditorStyles.boldLabel);
                    using (var s = new EditorGUILayout.ScrollViewScope(_scrollIncoming, GUILayout.ExpandHeight(true)))
                    {
                        _scrollIncoming = s.scrollPosition;
                        _incoming = EditorGUILayout.TextArea(_incoming ?? "", GUILayout.ExpandHeight(true));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Paste", GUILayout.Height(22)))
                        {
                            _incoming = EditorGUIUtility.systemCopyBuffer;
                            if (_autoPreview) TryBuildPreview();
                        }
                        if (GUILayout.Button("Copy", GUILayout.Height(22)))
                        {
                            EditorGUIUtility.systemCopyBuffer = _incoming ?? "";
                        }
                        if (GUILayout.Button("Open in External", GUILayout.Height(22)))
                        {
                            var temp = WriteTemp("_incoming_patch.json", _incoming ?? "");
                            EditorUtility.OpenWithDefaultApp(temp);
                        }
                    }
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Preview (will be written)", EditorStyles.boldLabel);
                    using (var s = new EditorGUILayout.ScrollViewScope(_scrollPreview, GUILayout.ExpandHeight(true)))
                    {
                        _scrollPreview = s.scrollPosition;
                        EditorGUILayout.TextArea(_preview ?? "", GUILayout.ExpandHeight(true));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Backup Current", GUILayout.Height(22))) BackupCurrent();
                        if (GUILayout.Button("Apply to Disk", GUILayout.Height(22))) ApplyToDisk();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        private string TargetPath()
        {
            var fn = _specFiles[_specIndex];
            return ("Assets/AIGG/Spec/" + fn).Replace("\\","/");
        }

        private void SetStatus(string msg)
        {
            _status = msg ?? "";
            Repaint();
            Debug.Log("[PreMergeCenter] " + _status);
        }

        private void ValidateIncoming()
        {
            if (string.IsNullOrWhiteSpace(_incoming)) { SetStatus("Incoming is empty."); return; }
            // very coarse JSON  ensure braces balancecheck 
            int braces = 0; foreach (var c in _incoming) { if (c=='{') braces++; else if (c=='}') braces--; }
            if (braces != 0) { SetStatus("Incoming JSON braces appear unbalanced."); return; }
            SetStatus("Incoming JSON looks syntactically OK.");
        }

        private void TryBuildPreview()
        {
            var targetPath = TargetPath();
            string baseText = "{}";
            try { if (File.Exists(targetPath)) baseText = File.ReadAllText(targetPath); }
            catch (Exception e) { SetStatus("Failed to read target: " + e.Message); return; }

            // For now, preview is a simple "incoming patch" plus base shown side by side (keeps it safe).
            // You can replace this with your structured merge later.
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"_preview_note\": \"Replace with your merge result.\",");
            sb.AppendLine("  \"_target_file\": \"" + _specFiles[_specIndex] + "\",");
            sb.AppendLine("  \"_base_length\": " + (baseText ?? "").Length + ",");
            sb.AppendLine("  \"_incoming_length\": " + (_incoming ?? "").Length + ",");
            sb.AppendLine("  \"_incoming_sample\": " + Quote(Shorten(_incoming, 256)) + "");
            sb.AppendLine("}");
            _preview = sb.ToString();
            SetStatus("Preview built (stub). Use Apply to write preview text exactly.");
        }

        private void BackupCurrent()
        {
            var p = TargetPath();
            try
            {
                var ts = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
                var dstDir = "StickerDash_Status/_Backups/Spec_" + ts;
                Directory.CreateDirectory(dstDir);
                var dst = Path.Combine(dstDir, Path.GetFileName(p));
                if (File.Exists(p)) File.Copy(p, dst, true);
                SetStatus("Backed up current spec to " + dst);
            }
            catch (Exception e) { SetStatus("Backup failed: " + e.Message); }
        }

        private void ApplyToDisk()
        {
            try
            {
                var p = TargetPath();
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, _preview ?? "");
                AssetDatabase.Refresh();
                SetStatus("Applied preview to " + p);
            }
            catch (Exception e) { SetStatus("Apply failed: " + e.Message); }
        }

        private static string WriteTemp(string name, string content)
        {
            var p = "StickerDash_Status/_Temp";
            Directory.CreateDirectory(p);
            var full = Path.Combine(p, name);
            File.WriteAllText(full, content ?? "");
            return full;
        }

        private static string Shorten(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= n) return s;
            return s.Substring(0, n) + "...";
        }

        private static string Quote(string s)
        {
            if (s == null) return "\"\"";
            return "\"" + s.Replace("\\","\\\\").Replace("\"","\\\"") + "\"";
        }
    }
}
