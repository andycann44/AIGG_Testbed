#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;

namespace Aim2Pro.AIGG {
    public static class AIGGApiClient {
        class Pending {
            public UnityWebRequest req;
            public Action<string> onOk;
            public Action<string> onErr;
            public double t0;
        }
        static Pending p;

        public static void ProposeRule(string userPrompt, Action<string> onOk, Action<string> onErr) {
            if (p != null) { onErr?.Invoke("Busy: previous request not finished."); return; }
            var s = AIGGSettings.LoadOrCreate();
            if (!s) { onErr?.Invoke("Missing AIGGSettings asset."); return; }
            if (s.mode == AIGGMode.LOCAL) { onOk?.Invoke("{\"note\":\"LOCAL mode: stub suggestion only\",\"file\":\"commands\",\"name\":\"auto-new\",\"regex\":\"\\\\bexample\\\\b\",\"kernel\":[]}"); return; }
            if (string.IsNullOrEmpty(s.openAIKey)) { onErr?.Invoke("Missing OpenAI API key."); return; }
            if (string.IsNullOrEmpty(s.model))     { onErr?.Invoke("Missing model id."); return; }

            // Strict format instruction. We mirror your ApiSettingsWindow approach to OpenAI chat :contentReference[oaicite:1]{index=1}
            string system = "You are a Unity editor assistant that writes STRICT JSON for NL rules. " +
                            "Return ONE JSON object with keys: file(name 'intents' or 'commands'), name, regex, " +
                            "and either ops (for intents) or kernel (for commands). Do not add prose.";
            string user   = userPrompt;

            var url = "https://api.openai.com/v1/chat/completions";
            var bodyObj = "{\"model\":\"" + Safe(s.model) + "\"," +
                          "\"messages\":[{\"role\":\"system\",\"content\":\"" + Safe(system) + "\"}," +
                                         "{\"role\":\"user\",\"content\":\"" + Safe(user) + "\"}]," +
                          "\"temperature\":0}";
            var req = new UnityWebRequest(url, "POST");
            var body = Encoding.UTF8.GetBytes(bodyObj);
            req.uploadHandler   = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + s.openAIKey);
            req.SetRequestHeader("Content-Type", "application/json");
#if UNITY_2020_1_OR_NEWER
            req.timeout = Math.Max(1, s.timeoutSeconds);
#endif
            p = new Pending{ req=req, onOk=onOk, onErr=onErr, t0=EditorApplication.timeSinceStartup };
            req.SendWebRequest();
            EditorApplication.update += Tick;
        }

        static void Tick() {
            if (p == null) { EditorApplication.update -= Tick; return; }
            if (!p.req.isDone) return;

            try {
                if (p.req.result == UnityWebRequest.Result.Success) {
                    var raw = p.req.downloadHandler.text ?? "";
                    // Extract message content (same strategy as ApiSettingsWindow) :contentReference[oaicite:2]{index=2}
                    var json = Extract(raw, "\"content\"\\s*:\\s*\"(.*?)\"");
                    p.onOk?.Invoke(Unescape(json ?? raw));
                } else {
                    var err = $"HTTP {(int)p.req.responseCode} — {p.req.error}\n{p.req.downloadHandler.text}";
                    p.onErr?.Invoke(err);
                }
            } finally {
                p.req.Dispose(); p=null; EditorApplication.update -= Tick;
            }
        }

        static string Safe(string s)=> string.IsNullOrEmpty(s) ? "" : s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r");
        static string Extract(string json, string pattern) {
            var m = System.Text.RegularExpressions.Regex.Match(json, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value : null;
        }
        static string Unescape(string s)=> s==null? null : s.Replace("\\n","\n").Replace("\\r","\r").Replace("\\t","\t").Replace("\\\"","\"").Replace("\\\\","\\");
    }
}
#endif
