/*
 * Aim2Pro — Tabbed Paste & Merge (safe, add-only)
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
            w.minSize = new Vector2(760, 460);
            PasteMergeTemp.EnsureRoot();
            w.Repaint();
        }

        void OnEnable() => PasteMergeTemp.EnsureRoot();

        string Root => PasteMergeTemp.Root;
        string PathFor(int i) => System.IO.Path.Combine(Root, Slots[i].file);

        bool HasContent(int i)
        {
            var p = PathFor(i);
            try { if (!File.Exists(p)) return false; var s = File.ReadAllText(p).Trim(); return s.Length > 0 && s != "{}" && s != "[]"; }
            catch { return false; }
        }
        string ReadOrEmpty(int i) { var p = PathFor(i); try { return File.Exists(p) ? File.ReadAllText(p) : ""; } catch { return ""; } }
        void Write(int i, string text) { var p = PathFor(i); File.WriteAllText(p, text ?? ""); AssetDatabase.Refresh(); }

        void Header()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80))) Repaint();
                if (GUILayout.Button("Merge All (dry-run)", EditorStyles.toolbarButton, GUILayout.Width(150))) MergeAll();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Temp", EditorStyles.toolbarButton, GUILayout.Width(90))) EditorUtility.RevealInFinder(Root);
                if (GUILayout.Button("Clear Temp", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    if (EditorUtility.DisplayDialog("Clear Temp", "Delete all temp_*.json files?", "Yes", "No"))
                    {
                        PasteMergeTemp.ClearAll();
                        Repaint();
                    }
                }
            }
        }

        void Tabs()
        {
            var labels = Slots.Select((s, i) => (HasContent(i) ? "● " : "") + s.tab).ToArray();
            _tab = GUILayout.Toolbar(_tab, labels);
        }

        void Body()
        {
            EditorGUILayout.Space(4);
            var fp = PathFor(_tab);
            EditorGUILayout.LabelField($"{Slots[_tab].tab} — {fp}", EditorStyles.boldLabel);

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
                if (GUILayout.Button("Copy JSON"))
                {
                    EditorGUIUtility.systemCopyBuffer = text ?? "";
                    Debug.Log($"[TabbedPaste] Copied {Slots[_tab].tab} JSON to clipboard.");
                }
                if (GUILayout.Button("Merge This"))
                {
                    // Non-invasive route: copy JSON; you paste into your existing Paste & Merge
                    EditorGUIUtility.systemCopyBuffer = text ?? "";
                    Debug.Log($"[TabbedPaste] Ready to paste '{Slots[_tab].tab}' JSON into Paste & Merge.");
                }
            }
        }

        void MergeAll()
        {
            int n = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                var txt = ReadOrEmpty(i)?.Trim();
                if (!string.IsNullOrEmpty(txt) && txt != "{}" && txt != "[]")
                {
                    n++;
                }
            }
            Debug.Log($"[TabbedPaste] MergeAll (dry-run): {n} tab(s) have content. Copy from each tab and paste into your Paste & Merge as needed.");
        }

        void OnGUI()
        {
            Header();
            Tabs();
            Body();
        }
    }
}
