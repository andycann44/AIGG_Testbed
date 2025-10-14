#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using Aim2Pro.AIGG;              // MiniJSON, PreMergeRouterAPI
using Aim2Pro.AIGG.PreMerge;     // PreMergeEngine

namespace Aim2Pro.AIGG.PreMerge
{
  public class PreMergeWindow : EditorWindow
  {
    [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge (NL)")]
    public static void Open() {
      var w = GetWindow<PreMergeWindow>("Pre-Merge");
      w.minSize = new Vector2(760, 560);
      w.LoadPrefs();
    }

    string _nl = "";
    string _normalized = "";
    string _issues = "";
    string _missing = "";
    string _canonical = "{ }";

    Vector2 sv1, sv2, sv3, sv4;

    // AI settings
    string apiKey = "";
    string model = "gpt-4o-mini";
    bool useAI = false;

    // AI output folder
    static readonly string AIOutRoot = "Assets/AIGG/AIOut";
    void EnsureDir(string p) { var d = System.IO.Path.GetDirectoryName(p); if (!Directory.Exists(d)) Directory.CreateDirectory(d); }
    string Stamp() => DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

    void LoadPrefs() {
      apiKey = EditorPrefs.GetString("AIGG_OpenAI_API_Key", "");
      model  = EditorPrefs.GetString("AIGG_OpenAI_Model", "gpt-4o-mini");
      useAI  = EditorPrefs.GetBool("AIGG_OpenAI_Use", false);
    }
    void SavePrefs() {
      EditorPrefs.SetString("AIGG_OpenAI_API_Key", apiKey ?? "");
      EditorPrefs.SetString("AIGG_OpenAI_Model", model ?? "gpt-4o-mini");
      EditorPrefs.SetBool("AIGG_OpenAI_Use", useAI);
    }

    void OnGUI() {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Natural Language (input)", EditorStyles.boldLabel);
      sv1 = EditorGUILayout.BeginScrollView(sv1, GUILayout.MinHeight(100));
      _nl = EditorGUILayout.TextArea(_nl, GUILayout.ExpandHeight(true));
      EditorGUILayout.EndScrollView();

      using (new EditorGUILayout.HorizontalScope()) {
        if (GUILayout.Button("Run Pre-Merge")) RunPreMerge();
        if (GUILayout.Button("Local Fix Only")) RunPreMerge(localOnly:true);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Send → Paste & Merge")) {
          PreMergeRouterAPI.Route(_canonical);
        }
      }

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Normalized", EditorStyles.boldLabel);
      sv2 = EditorGUILayout.BeginScrollView(sv2, GUILayout.MinHeight(80));
      EditorGUILayout.TextArea(_normalized, GUILayout.ExpandHeight(true));
      EditorGUILayout.EndScrollView();

      EditorGUILayout.LabelField("Issues / Missing", EditorStyles.boldLabel);
      sv3 = EditorGUILayout.BeginScrollView(sv3, GUILayout.MinHeight(80));
      EditorGUILayout.TextArea(_issues + (_missing.Length>0 ? ("\nMissing:\n" + _missing) : ""), GUILayout.ExpandHeight(true));
      EditorGUILayout.EndScrollView();

      EditorGUILayout.LabelField("Canonical JSON (preview)", EditorStyles.boldLabel);
      sv4 = EditorGUILayout.BeginScrollView(sv4, GUILayout.MinHeight(160));
      EditorGUILayout.TextArea(_canonical, GUILayout.ExpandHeight(true));
      EditorGUILayout.EndScrollView();

      EditorGUILayout.Space();
      using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
        EditorGUILayout.LabelField("AI Assist (optional)", EditorStyles.boldLabel);
        useAI = EditorGUILayout.ToggleLeft("Use OpenAI if local checks still missing", useAI);
        apiKey = EditorGUILayout.PasswordField("API Key", apiKey);
        model  = EditorGUILayout.TextField("Model", string.IsNullOrEmpty(model) ? "gpt-4o-mini" : model);
        using (new EditorGUILayout.HorizontalScope()) {
          if (GUILayout.Button("Save AI Settings", GUILayout.Width(160))) SavePrefs();
          if (GUILayout.Button("Ask AI Now", GUILayout.Width(120))) AskAI();
          if (GUILayout.Button("Reveal AI Output Folder", GUILayout.Width(200))) EditorUtility.RevealInFinder(AIOutRoot);
        }
      }
    }

    void RunPreMerge(bool localOnly=false) {
      var engine = new PreMergeEngine();
      var res = engine.Process(_nl);
      _normalized = res.normalized;

      var sb = new StringBuilder();
      foreach (var i in res.issues) sb.AppendLine("• " + i);
      _issues = sb.ToString().Trim();

      var mb = new StringBuilder();
      foreach (var m in res.missing) mb.AppendLine("• " + m);
      _missing = mb.ToString().Trim();

      _canonical = res.canonicalJson;

      if (!localOnly && useAI && res.missing.Count > 0) {
        AskAI();
      }
    }

    void AskAI() {
      if (string.IsNullOrEmpty(apiKey)) { ShowNotification(new GUIContent("Set API key first.")); return; }

      var system = "You are a strict JSON generator for Unity editor tooling. " +
                   "If multiple spec files are needed, return this envelope: " +
                   "{\"files\":{\"lexicon.json\":{...},\"fieldmap.json\":{...}}}. " +
                   "Otherwise return a single JSON object. No code fences, no comments.";
      var user   = "NL:\n" + _nl + "\n\nCurrent canonical JSON (if present):\n" + _canonical + "\n\nReturn corrected/complete JSON only.";

      try {
        var json = OpenAIChat(apiKey, model, system, user);
        if (!string.IsNullOrEmpty(json)) {
          _canonical = json;
          var saved = TrySplitAndSaveAIOutput(json, routeFirst:true);
          if (saved > 1) {
            _issues = (_issues + "\n").Trim() + $"Saved {saved} AI files in {AIOutRoot}. Opened the first in Paste & Merge.";
          }
          Repaint();
          ShowNotification(new GUIContent("AI completed."));
        }
      } catch (Exception ex) {
        ShowNotification(new GUIContent("AI error: " + ex.Message));
      }
    }

    int TrySplitAndSaveAIOutput(string text, bool routeFirst = true)
    {
      if (string.IsNullOrWhiteSpace(text)) return 0;

      var rawPath = System.IO.Path.Combine(AIOutRoot, "raw", $"ai_{Stamp()}.json");
      EnsureDir(rawPath);
      File.WriteAllText(rawPath, text, Encoding.UTF8);

      try {
        object root;
        if (MiniJSON.TryDeserialize(text, out root) && root is Dictionary<string,object> d)
        {
          if (d.TryGetValue("files", out var filesObj) && filesObj is Dictionary<string,object> files && files.Count>0)
          {
            int saved=0;
            string firstWrapper=null;
            foreach (var kv in files)
            {
              var keyLower = (kv.Key ?? "").ToString().ToLowerInvariant();
              string target =
                keyLower.Contains("lexicon")  ? "Lexicon"  :
                keyLower.Contains("fieldmap") ? "Fieldmap" :
                keyLower.Contains("intents")  ? "Intents"  :
                keyLower.Contains("schema")   ? "Schema"   :
                keyLower.Contains("registry") ? "Registry" :
                keyLower.Contains("commands") ? "Commands" :
                keyLower.Contains("macros")   ? "Macros"   : "Schema";

              var folder = System.IO.Path.Combine(AIOutRoot, target);
              Directory.CreateDirectory(folder);
              var path = System.IO.Path.Combine(folder, $"{target}_{Stamp()}.json");
              File.WriteAllText(path, MiniJSON.Serialize(kv.Value, pretty:true), Encoding.UTF8);
              saved++;

              if (firstWrapper == null) {
                var wrap = new Dictionary<string,object>{{"target",target},{"content",kv.Value}};
                firstWrapper = MiniJSON.Serialize(wrap, pretty:true);
              }
            }
            if (routeFirst && firstWrapper != null) PreMergeRouterAPI.Route(firstWrapper);
            return saved;
          }
        }
      } catch {}

      // single json -> archive under Canonical
      var single = System.IO.Path.Combine(AIOutRoot, "Canonical", $"canonical_{Stamp()}.json");
      EnsureDir(single);
      File.WriteAllText(single, text, Encoding.UTF8);
      return 1;
    }

    string OpenAIChat(string key, string model, string system, string user) {
      var url = "https://api.openai.com/v1/chat/completions";
      var req = (HttpWebRequest)WebRequest.Create(url);
      req.Method = "POST";
      req.ContentType = "application/json";
      req.Headers["Authorization"] = "Bearer " + key;
      req.Timeout = 30000;

      var payload = "{"
        + "\"model\":\"" + Escape(model) + "\","
        + "\"temperature\":0,"
        + "\"messages\":["
        + "{\"role\":\"system\",\"content\":\"" + Escape(system) + "\"},"
        + "{\"role\":\"user\",\"content\":\"" + Escape(user) + "\"}"
        + "]"
        + "}";

      var bytes = Encoding.UTF8.GetBytes(payload);
      using (var stream = req.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);

      string body = null;
      using (var resp = (HttpWebResponse)req.GetResponse())
      using (var rs = resp.GetResponseStream())
      using (var sr = new StreamReader(rs)) body = sr.ReadToEnd();

      object root;
      if (MiniJSON.TryDeserialize(body, out root) && root is Dictionary<string, object> d
          && d.TryGetValue("choices", out var ch) && ch is List<object> arr && arr.Count>0) {
        var c0 = arr[0] as Dictionary<string, object>;
        if (c0 != null && c0.TryGetValue("message", out var msg) && msg is Dictionary<string, object> md
            && md.TryGetValue("content", out var content)) {
          var text = content?.ToString() ?? "";
          var m = Regex.Match(text, "```(?:json)?\\s*([\\s\\S]*?)\\s*```", RegexOptions.IgnoreCase);
          if (m.Success) text = m.Groups[1].Value;
          return text.Trim();
        }
      }
      throw new Exception("Unexpected AI response.");
    }

    string Escape(string s) {
      if (string.IsNullOrEmpty(s)) return "";
      var sb = new StringBuilder();
      foreach (var c in s) {
        if (c == '\\') sb.Append("\\\\");
        else if (c == '\"') sb.Append("\\\"");
        else if (c == '\n') sb.Append("\\n");
        else if (c == '\r') sb.Append("\\r");
        else if (c == '\t') sb.Append("\\t");
        else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
        else sb.Append(c);
      }
      return sb.ToString();
    }
  }
}
#endif
