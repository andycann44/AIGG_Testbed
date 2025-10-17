/*
 * Aim2Pro — Tabbed Paste & Merge (Temp-backed)
 * Menu: Window > Aim2Pro > Aigg > Paste & Merge (Tabbed)
 */
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public class SpecPasteMergeTabbedWindow : EditorWindow
    {
        const string TempRoot = "Assets/AIGG/Temp";
        static readonly (string tab, string file)[] Slots = new (string,string)[]
        {
            ("intents","temp_intents.json"),
            ("lexicon","temp_lexicon.json"),
            ("macros","temp_macros.json"),
            ("commands","temp_commands.json"),
            ("fieldmap","temp_fieldmap.json"),
            ("registry","temp_registry.json"),
            ("schema","temp_schema.json"),
            ("router","temp_router.json"),
            ("nl","temp_nl.json"),
            ("canonical","temp_canonical.json"),
            ("diagnostics","temp_diagnostics.json"),
            ("aliases","temp_aliases.json"),
            ("shims","temp_shims.json"),
            ("nullable","temp_nullable.json"),
            ("overrides","temp_overrides.json"),
        };

        int _tab = 0;
        Vector2 _scroll;

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge (Tabbed)")]
        public static void Open()
        {
            var w = GetWindow<SpecPasteMergeTabbedWindow>("Paste & Merge (Tabbed)");
            w.minSize = new Vector2(720, 420);
            w.EnsureTempDir();
            w.Repaint();
        }

        void OnEnable() => EnsureTempDir();
        void EnsureTempDir()
        {
            if (!AssetDatabase.IsValidFolder(TempRoot))
            {
                Directory.CreateDirectory(TempRoot);
                AssetDatabase.Refresh();
            }
        }

        string PathFor(int i) => System.IO.Path.Combine(TempRoot, Slots[i].file);
        bool HasContent(int i)
        {
            var p = PathFor(i);
            try { if (!File.Exists(p)) return false; var s = File.ReadAllText(p).Trim(); return s.Length > 0 && s != "{}" && s != "[]"; }
            catch { return false; }
        }
        string ReadOrEmpty(int i) { var p = PathFor(i); try { return File.Exists(p) ? File.ReadAllText(p) : ""; } catch { return ""; } }
        void Write(int i, string text) { var p = PathFor(i); File.WriteAllText(p, text ?? ""); AssetDatabase.Refresh(); }

        void HeaderBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80))) Repaint();
                if (GUILayout.Button("Merge All (dry-run)", EditorStyles.toolbarButton, GUILayout.Width(150))) MergeAll();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Temp", EditorStyles.toolbarButton, GUILayout.Width(90))) EditorUtility.RevealInFinder(TempRoot);
                if (GUILayout.Button("Clear Temp", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    if (EditorUtility.DisplayDialog("Clear Temp", "Delete all temp_*.json files?", "Yes", "No"))
                    {
                        foreach (var f in Directory.GetFiles(TempRoot, "temp_*.json")) File.Delete(f);
                        AssetDatabase.Refresh(); Repaint();
                    }
                }
            }
        }

        void DrawTabs()
        {
            var labels = Slots.Select((s, i) => (HasContent(i) ? "● " : "") + s.tab).ToArray();
            _tab = GUILayout.Toolbar(_tab, labels);
        }

        void DrawBody()
        {
            EditorGUILayout.Space(4);
            var fp = PathFor(_tab);
            EditorGUILayout.LabelField($"{Slots[_tab].tab}  —  {fp}", EditorStyles.boldLabel);

            var text = ReadOrEmpty(_tab);
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                text = EditorGUILayout.TextArea(text, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                if (check.changed) Write(_tab, text);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy JSON")) { EditorGUIUtility.systemCopyBuffer = text ?? ""; Debug.Log($"[TabbedPaste] Copied {Slots[_tab].tab}."); }
                if (GUILayout.Button("Merge This")) MergeOne(text, Slots[_tab].tab);
            }
        }

        void MergeAll()
        {
            int merged = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                var txt = ReadOrEmpty(i);
                if (!string.IsNullOrWhiteSpace(txt) && txt.Trim() != "{}" && txt.Trim() != "[]") { MergeOne(txt, Slots[i].tab); merged++; }
            }
            Debug.Log($"[TabbedPaste] MergeAll invoked for {merged} tab(s) (dry-run route).");
        }

        static void MergeOne(string json, string tag)
        {
            if (string.IsNullOrWhiteSpace(json)) { Debug.LogWarning($"[TabbedPaste] '{tag}' is empty, skipped."); return; }
            try
            {
                var t = Type.GetType("Aim2Pro.AIGG.SpecPasteMergeWindow, Assembly-CSharp-Editor");
                if (t != null)
                {
                    var m = t.GetMethod("OpenWithJson", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
                    if (m != null)
                    {
                        if (m.IsStatic) m.Invoke(null, new object[] { json });
                        else { var inst = ScriptableObject.CreateInstance(t); m.Invoke(inst, new object[] { json }); }
                        Debug.Log($"[TabbedPaste] Routed '{tag}' to SpecPasteMergeWindow.OpenWithJson."); return;
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[TabbedPaste] Reflection route failed: {ex.Message}"); }
            EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log($"[TabbedPaste] Fallback: copied '{tag}' JSON to clipboard (paste into Paste & Merge).");
        }

        void OnGUI() { HeaderBar(); DrawTabs(); DrawBody(); }
    }
}
