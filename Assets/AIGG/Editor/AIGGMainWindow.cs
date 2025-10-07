#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Aim2Pro.AIGG
{
    public class AIGGMainWindow : EditorWindow
    {
        // Menu policy assignment: Window/Aim2Pro/Aigg/AIGG
        [MenuItem("Window/Aim2Pro/Aigg/AIGG")]
        public static void Open()
        {
            var w = GetWindow<AIGGMainWindow>("AIGG");
            w.minSize = new Vector2(700, 480);
        }

        // Files we manage
        static readonly string SpecRoot = "Assets/StickerDash/AIGG/Resources/Spec";
        static readonly string[] FileNames = {
            "schema.json","lexicon.json","intents.json","fieldmap.json","registry.json"
        };

        // Simple templates for "Reset to Template"
        static string TemplateFor(string fileName)
        {
            switch (fileName)
            {
                case "schema.json":
                    return "{\n  \"version\":\"v1\",\n  \"description\":\"AIGG custom schema\",\n  \"properties\":{}\n}\n";
                case "lexicon.json":
                    return "{\n  \"synonyms\": {}\n}\n";
                case "intents.json":
                    return "{\n  \"intents\": []\n}\n";
                case "fieldmap.json":
                    return "{\n  \"englishToPath\": {}\n}\n";
                case "registry.json":
                    return "{\n  \"components\": [],\n  \"windows\": []\n}\n";
                default:
                    return "{}\n";
            }
        }

        int selected = 0;
        string currentText = "";
        string status = "";
        string currentPath => Path.Combine(SpecRoot, FileNames[Mathf.Clamp(selected,0,FileNames.Length-1)]);

        void OnEnable()
        {
            Directory.CreateDirectory(SpecRoot);
            LoadCurrent();
        }

        void OnGUI()
        {
            GUILayout.Label("AIGG Creator — Spec Editor", EditorStyles.boldLabel);

            // File selector
            EditorGUILayout.BeginHorizontal();
            int newSel = EditorGUILayout.Popup("Spec File", selected, FileNames);
            if (newSel != selected)
            {
                if (ConfirmDiscardIfDirty()) { selected = newSel; LoadCurrent(); }
            }

            if (GUILayout.Button("Open in Finder", GUILayout.Width(140)))
                EditorUtility.RevealInFinder(currentPath);
            EditorGUILayout.EndHorizontal();

            // Path readout
            EditorGUILayout.LabelField("Path", currentPath);

            // Editor area
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("JSON Content");
            var newText = EditorGUILayout.TextArea(currentText, GUILayout.MinHeight(280));
            if (!ReferenceEquals(newText, currentText) && newText != currentText)
            {
                currentText = newText;
                status = "Modified (unsaved)";
            }

            // Actions
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload"))
            {
                LoadCurrent();
            }
            if (GUILayout.Button("Save"))
            {
                SaveCurrent();
            }
            if (GUILayout.Button("Reset to Template"))
            {
                if (EditorUtility.DisplayDialog("Reset to Template",
                    $"Replace {Path.GetFileName(currentPath)} with default template?\nThis cannot be undone.", "Yes", "No"))
                {
                    currentText = TemplateFor(Path.GetFileName(currentPath));
                    SaveCurrent();
                }
            }
            if (GUILayout.Button("Validate Syntax"))
            {
                var ok = QuickValidateJson(currentText, out string msg);
                status = ok ? "Valid JSON (basic check passed)" : ("Invalid JSON: " + msg);
                if (!ok) Debug.LogError("[AIGG] JSON validation failed: " + msg);
                else Debug.Log("[AIGG] JSON looks valid.");
            }
            EditorGUILayout.EndHorizontal();

            // Status line
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(status) ? "—" : status, MessageType.Info);
        }

        void LoadCurrent()
        {
            try
            {
                if (!File.Exists(currentPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(currentPath));
                    File.WriteAllText(currentPath, TemplateFor(Path.GetFileName(currentPath)), Encoding.UTF8);
                    AssetDatabase.Refresh();
                }
                currentText = File.ReadAllText(currentPath, Encoding.UTF8);
                status = $"Loaded at {DateTime.Now:T}";
            }
            catch (Exception ex)
            {
                status = "Error loading: " + ex.Message;
                Debug.LogError("[AIGG] Load error: " + ex);
            }
        }

        void SaveCurrent()
        {
            try
            {
                File.WriteAllText(currentPath, currentText ?? "", Encoding.UTF8);
                AssetDatabase.Refresh();
                status = $"Saved at {DateTime.Now:T}";
                Debug.Log("[AIGG] Saved " + currentPath);
            }
            catch (Exception ex)
            {
                status = "Error saving: " + ex.Message;
                Debug.LogError("[AIGG] Save error: " + ex);
            }
        }

        bool ConfirmDiscardIfDirty()
        {
            if (status.StartsWith("Modified")) {
                return EditorUtility.DisplayDialog("Discard changes?",
                    "You have unsaved edits. Switch file and discard changes?", "Discard", "Cancel");
            }
            return true;
        }

        // Very lightweight JSON check (brackets/braces, quotes). Not a full parser.
        bool QuickValidateJson(string s, out string msg)
        {
            if (string.IsNullOrEmpty(s)) { msg = "Empty."; return false; }

            int braces=0, brackets=0;
            bool inStr=false, esc=false;
            for (int i=0;i<s.Length;i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (esc) { esc=false; continue; }
                    if (c=='\\') esc=true;
                    else if (c=='"') inStr=false;
                    continue;
                }
                if (c=='"') inStr=true;
                else if (c=='{') braces++;
                else if (c=='}') braces--;
                else if (c=='[') brackets++;
                else if (c==']') brackets--;
                if (braces<0 || brackets<0) { msg="Unexpected closing bracket at pos "+i; return false; }
            }
            if (inStr) { msg="Unclosed string"; return false; }
            if (braces!=0 || brackets!=0) { msg="Mismatched braces/brackets"; return false; }
            msg = "OK"; return true;
        }
    }
}
#endif
