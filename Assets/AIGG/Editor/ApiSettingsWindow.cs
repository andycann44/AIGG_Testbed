#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Aim2Pro.AIGG
{
    public class ApiSettingsWindow : EditorWindow
    {
        string testResult = "";
        string rawJson = "";
        long lastHttp = 0;
        double lastMs = 0;
        string lastReqId = "";
        bool isCalling = false;
        double tStart = 0;

        UnityWebRequest pendingReq = null;
        enum Endpoint { None, Chat, Responses }
        Endpoint endpoint = Endpoint.None;

        [MenuItem("Window/Aim2Pro/Settings/API Settings")]
        public static void Open()
        {
            var w = GetWindow<ApiSettingsWindow>("API Settings");
            w.minSize = new Vector2(560, 360);
        }

        void OnDisable()
        {
            if (pendingReq != null)
            {
                pendingReq.Abort();
                pendingReq.Dispose();
                pendingReq = null;
                isCalling = false;
                endpoint = Endpoint.None;
            }
        }

        void OnGUI()
        {
            var s = AIGGSettings.LoadOrCreate();
            if (!s) { EditorGUILayout.HelpBox("Missing AIGG Settings.", MessageType.Error); return; }

            bool changed = false;
            GUILayout.Label("AIGG Settings", EditorStyles.boldLabel);

            // Mode & model (auto-save)
            var prevMode = s.mode;
            s.mode = (AIGGMode)EditorGUILayout.EnumPopup("Mode", s.mode);
            if (s.mode != prevMode)
            {
                if (s.mode == AIGGMode.OPENAI && !string.IsNullOrEmpty(s.openAIModel)) s.model = s.openAIModel;
                if (s.mode == AIGGMode.LOCAL  && !string.IsNullOrEmpty(s.localModel))  s.model = s.localModel;
                changed = true;
            }

            string newModel = EditorGUILayout.TextField("Model", s.model ?? "");
            if (newModel != s.model)
            {
                s.model = newModel;
                if (s.mode == AIGGMode.OPENAI) s.openAIModel = newModel; else s.localModel = newModel;
                changed = true;
            }

            string newKey = EditorGUILayout.PasswordField("OpenAI API Key", s.openAIKey ?? "");
            if (newKey != s.openAIKey) { s.openAIKey = newKey; changed = true; }

            EditorGUILayout.Space();
            GUILayout.Label("Network", EditorStyles.boldLabel);
            bool newResp = EditorGUILayout.ToggleLeft("Use Responses endpoint (beta)", s.useResponsesBeta);
            if (newResp != s.useResponsesBeta) { s.useResponsesBeta = newResp; changed = true; }
            int newTimeout = EditorGUILayout.IntField("Timeout (seconds)", Mathf.Max(1, s.timeoutSeconds));
            if (newTimeout != s.timeoutSeconds) { s.timeoutSeconds = newTimeout; changed = true; }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(isCalling))
                {
                    if (GUILayout.Button("Test"))
                    {
                        if (s.mode == AIGGMode.LOCAL)
                        {
                            testResult = "hello Andy";
                            rawJson = "{\"mode\":\"LOCAL\",\"output\":\"hello Andy\"}";
                            lastHttp = 0; lastMs = 0; lastReqId = "";
                            Debug.Log("AIGG[LOCAL] Test → hello Andy (no network)");
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(s.openAIKey))
                            {
                                testResult = "Error: Missing OpenAI API key.";
                                rawJson = "";
                                Debug.LogError(testResult);
                            }
                            else if (string.IsNullOrEmpty(s.model))
                            {
                                testResult = "Error: Missing model id (e.g., gpt-4o-mini).";
                                rawJson = "";
                                Debug.LogError(testResult);
                            }
                            else
                            {
                                string prompt = "Reply with exactly: hello Andy from AIGG";
                                // Default: CHAT; if toggle is on, try RESPONSES first then fall back to CHAT
                                if (s.useResponsesBeta) StartResponses(s, prompt);
                                else StartChat(s, prompt);
                            }
                        }
                    }
                }

                if (GUILayout.Button("Copy Raw JSON"))
                {
                    EditorGUIUtility.systemCopyBuffer = string.IsNullOrEmpty(rawJson) ? "{}" : rawJson;
                    ShowNotification(new GUIContent("Raw JSON copied"));
                }

                if (GUILayout.Button("Clear"))
                {
                    testResult = "";
                    rawJson = "";
                    lastHttp = 0; lastMs = 0; lastReqId = "";
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                string http = lastHttp == 0 ? "—" : lastHttp.ToString();
                string ms   = lastMs <= 0 ? "—" : $"{Mathf.RoundToInt((float)lastMs)} ms";
                string ep   = endpoint == Endpoint.Chat ? "chat" : endpoint == Endpoint.Responses ? "responses" : "—";
                string rid  = string.IsNullOrEmpty(lastReqId) ? "—" : lastReqId;
                EditorGUILayout.LabelField($"HTTP: {http}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"Latency: {ms}", GUILayout.Width(140));
                EditorGUILayout.LabelField($"Endpoint: {ep}", GUILayout.Width(140));
                EditorGUILayout.LabelField($"Request-Id: {rid}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Test Result", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(testResult) ? (isCalling ? $"Connecting to OpenAI ({(endpoint==Endpoint.Responses?"responses":"chat")})…" : "—") : testResult, MessageType.Info);

            if (changed || GUI.changed)
            {
                EditorUtility.SetDirty(s);
                AssetDatabase.SaveAssets();
            }
        }

        // ----- CHAT (primary) -----
        void StartChat(AIGGSettings s, string prompt)
        {
            endpoint = Endpoint.Chat;
            var url = "https://api.openai.com/v1/chat/completions";
            var bodyObj = "{\"model\":\"" + Safe(s.model) + "\",\"messages\":[{\"role\":\"user\",\"content\":\"" + Safe(prompt) + "\"}],\"temperature\":0}";
            BeginCall(s.openAIKey, url, bodyObj, s.timeoutSeconds, "chat");
        }

        // ----- RESPONSES (optional/beta) -----
        void StartResponses(AIGGSettings s, string prompt)
        {
            endpoint = Endpoint.Responses;
            // Try a more structured 'input' array (reduces 500s in practice)
            var url = "https://api.openai.com/v1/responses";
            var bodyObj = "{\"model\":\"" + Safe(s.model) + "\",\"input\":[{\"role\":\"user\",\"content\":\"" + Safe(prompt) + "\"}],\"temperature\":0}";
            BeginCall(s.openAIKey, url, bodyObj, s.timeoutSeconds, "responses", onFail: () => {
                // fallback to chat once
                StartChat(s, prompt);
            });
        }

        // ----- Common HTTP -----
        void BeginCall(string apiKey, string url, string bodyObj, int timeoutSec, string label, Action onFail = null)
        {
            rawJson = "";
            testResult = $"Connecting to OpenAI ({label})…";
            lastHttp = 0; lastMs = 0; lastReqId = "";
            tStart = EditorApplication.timeSinceStartup;

            var req = new UnityWebRequest(url, "POST");
            var body = Encoding.UTF8.GetBytes(bodyObj);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
#if UNITY_2020_1_OR_NEWER
            req.timeout = Mathf.Max(1, timeoutSec);
#endif

            pendingReq = req;
            isCalling = true;

            Debug.Log($"AIGG[OPENAI/{label}] Connecting… model='{Extract(bodyObj, "\"model\"\\s*:\\s*\"(.*?)\"")}' url={url}");
            req.SendWebRequest();
            EditorApplication.update += () => Poll(onFail, label);
        }

        void Poll(Action onFail, string label)
        {
            if (pendingReq == null) { isCalling = false; endpoint = Endpoint.None; return; }
            if (!pendingReq.isDone) return;

            try
            {
                lastMs = (EditorApplication.timeSinceStartup - tStart) * 1000.0;
                var headers = pendingReq.GetResponseHeaders();
                if (headers != null && headers.TryGetValue("x-request-id", out var rid)) lastReqId = rid;
                lastHttp = (long)pendingReq.responseCode;

                if (pendingReq.result == UnityWebRequest.Result.Success)
                {
                    rawJson = pendingReq.downloadHandler.text ?? "";
                    // Extract simple content
                    string extracted =
                        endpoint == Endpoint.Chat
                          ? Extract(rawJson, "\"content\"\\s*:\\s*\"(.*?)\"")
                          : (Extract(rawJson, "\"output_text\"\\s*:\\s*\"(.*?)\"") ?? Extract(rawJson, "\"text\"\\s*:\\s*\"(.*?)\""));
                    if (string.IsNullOrEmpty(extracted)) extracted = rawJson;
                    testResult = Unescape(extracted).Trim();
                    Debug.Log($"AIGG[OPENAI/{label}] OK HTTP {lastHttp} in {Mathf.RoundToInt((float)lastMs)} ms (req-id: {lastReqId}) → {testResult}");
                }
                else
                {
                    var raw = pendingReq.downloadHandler != null ? pendingReq.downloadHandler.text : "";
                    rawJson = raw ?? "";
                    testResult = $"HTTP {(int)pendingReq.responseCode} — {pendingReq.error}";
                    if (!string.IsNullOrEmpty(raw)) testResult += "\n" + raw;
                    Debug.LogError($"AIGG[OPENAI/{label}] FAIL HTTP {lastHttp} in {Mathf.RoundToInt((float)lastMs)} ms (req-id: {lastReqId})");

                    // One-shot fallback if provided (i.e., from responses → chat)
                    if (onFail != null) { CleanupReq(); onFail.Invoke(); return; }
                }
            }
            finally
            {
                CleanupReq();
                Repaint();
            }
        }

        void CleanupReq()
        {
            if (pendingReq != null) { pendingReq.Dispose(); pendingReq = null; }
            isCalling = false;
        }

        // ----- Helpers -----
        static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
        static string Extract(string json, string pattern)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = Regex.Match(json, pattern, RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value : null;
        }
        static string Unescape(string s)
        {
            if (s == null) return null;
            return s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
#endif
