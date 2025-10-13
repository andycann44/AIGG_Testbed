#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Aim2Pro.AIGG.Editor
{
    public class AIGG_OpenAI_Runner : EditorWindow
    {
        private string nl = "";
        private Vector2 sv;

        [MenuItem("Window/Aim2Pro/Aigg/OpenAI_Router_Only_Legacy")]
        public static void Open()
        {
            var w = GetWindow<AIGG_OpenAI_Runner>("OpenAI → Router");
            w.minSize = new Vector2(720, 320);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Natural language prompt", EditorStyles.boldLabel);
            sv = EditorGUILayout.BeginScrollView(sv, GUILayout.MinHeight(120));
            nl = EditorGUILayout.TextArea(nl, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            GUILayout.Space(8);
            if (GUILayout.Button("Run OpenAI → Pre-Merge Router", GUILayout.Height(28)))
                Run(nl);

            GUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Sends FINAL canonical JSON directly to the Pre-Merge Router.\n" +
                "Does NOT touch Workbench.", MessageType.Info);
        }

        async void Run(string input)
        {
            string json = await RequestCanonicalAsync(input);
            if (!IsJsonLikely(json)) return;               // ignore non-final/empty
            if (SendToRouter(json)) return;                // route split tabs
            FallbackToPaste(json);                         // last resort
        }

        static bool IsJsonLikely(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            return s.StartsWith("{") && s.EndsWith("}") && s.Contains(":");
        }

        // Try: Aim2Pro.AIGG.Aigg.PreMergeRouterWindow.ReceiveFromOpenAI(json, true)
        // Fallbacks to (json) if no focus param.
        static bool SendToRouter(string json)
        {
            try
            {
                Type router = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    router = asm.GetType("Aim2Pro.AIGG.Aigg.PreMergeRouterWindow")
                          ?? asm.GetType("Aim2Pro.AIGG.PreMergeRouterWindow");
                    if (router != null) break;
                }
                if (router == null) return false;

                var m = router.GetMethod("ReceiveFromOpenAI", BindingFlags.Public | BindingFlags.Static);
                if (m == null) return false;

                var ps = m.GetParameters();
                if (ps.Length >= 2) { m.Invoke(null, new object[] { json ?? string.Empty, true }); }
                else               { m.Invoke(null, new object[] { json ?? string.Empty }); }
                return true;
            }
            catch { return false; }
        }

        // If router is missing, at least open Paste & Replace with the payload ready.
        static void FallbackToPaste(string json)
        {
            try { EditorGUIUtility.systemCopyBuffer = json ?? ""; } catch { }
            // Try to open your paste window by menu
            if (!EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Aigg/Paste & Replace"))
                EditorApplication.ExecuteMenuItem("Window/Aim2Pro/Aigg/Paste & Merge");
        }

        // Hook into your client if present; otherwise return "" (no echo).
        static async Task<string> RequestCanonicalAsync(string input)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Aim2Pro.AIGG.OpenAI.Client");
                    if (t != null)
                    {
                        var m = t.GetMethod("RequestCanonicalAsync", BindingFlags.Public | BindingFlags.Static);
                        if (m != null)
                        {
                            var task = (Task<string>)m.Invoke(null, new object[] { input });
                            return await task;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Aim2Pro] OpenAI reflection fallback: " + ex.Message);
            }

            await Task.Delay(150);
            return ""; // keep Workbench uninvolved; only act on real JSON
        }
    }
}
#endif
