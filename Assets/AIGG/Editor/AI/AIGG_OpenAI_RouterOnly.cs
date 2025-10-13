#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Aim2Pro.AIGG.Editor
{
    public class AIGG_OpenAI_RouterOnly : EditorWindow
    {
        string nl = "";
        Vector2 sv;

        [MenuItem("Window/Aim2Pro/Aigg/OpenAI_Router_Only")]
        public static void Open()
        {
            var w = GetWindow<AIGG_OpenAI_RouterOnly>("OpenAI -> Router");
            w.minSize = new Vector2(720, 320);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Natural language prompt", EditorStyles.boldLabel);
            sv = EditorGUILayout.BeginScrollView(sv, GUILayout.MinHeight(120));
            nl = EditorGUILayout.TextArea(nl, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Run OpenAI -> Pre-Merge Router", GUILayout.Height(26)))
                _ = Run(nl);

            EditorGUILayout.HelpBox("Sends FINAL canonical JSON directly to the Pre-Merge Router. Never writes to Workbench.", MessageType.Info);
        }

        async Task Run(string input)
        {
            string json = await RequestCanonicalAsync(input);
            if (!IsJsonLikely(json)) return;
            if (SendToRouter(json)) return;
            try { EditorGUIUtility.systemCopyBuffer = json ?? ""; } catch {}
        }

        static bool IsJsonLikely(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            return s.StartsWith("{") && s.EndsWith("}") && s.Contains(":");
        }

        static bool SendToRouter(string json)
        {
            try
            {
                System.Type router = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    router = asm.GetType("Aim2Pro.AIGG.Aigg.PreMergeRouterWindow")
                          ?? asm.GetType("Aim2Pro.AIGG.PreMergeRouterWindow");
                    if (router != null) break;
                }
                if (router == null) return false;
                var m = router.GetMethod("ReceiveFromOpenAI", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (m == null) return false;
                var ps = m.GetParameters();
                if (ps.Length >= 2) m.Invoke(null, new object[] { json ?? string.Empty, true });
                else               m.Invoke(null, new object[] { json ?? string.Empty });
                return true;
            }
            catch { return false; }
        }

        static async Task<string> RequestCanonicalAsync(string input)
        {
            try
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Aim2Pro.AIGG.OpenAI.Client");
                    if (t != null)
                    {
                        var m = t.GetMethod("RequestCanonicalAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (m != null) return await (Task<string>)m.Invoke(null, new object[] { input });
                    }
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[Aim2Pro] OpenAI reflection fallback: " + ex.Message); }
            await Task.Delay(150);
            return ""; // no echo
        }
    }
}
#endif
