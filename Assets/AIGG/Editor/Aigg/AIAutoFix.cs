// ASCII only
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Aim2Pro.AIGG {
  [Serializable] public class ProposedFix {
    public List<string> commands = new List<string>();
    public List<string> macros = new List<string>();
    public List<FieldPair> fieldMap = new List<FieldPair>();
    public string canonical = ""; // optional canonical JSON proposed by AI
  }
  [Serializable] public class FieldPair { public string key = ""; public string value = ""; }

  public static class AIAutoFix {
    const string DefaultModel = "gpt-4o-mini";
    const string ChatUrl = "https://api.openai.com/v1/chat/completions";

    public static ProposedFix Ask(string apiKey, string model, string nl, string normalized, string canonical, List<string> unmatched, List<string> missingCmds, out string error) {
      error = null;
      var result = new ProposedFix();
      if (string.IsNullOrEmpty(apiKey)) { error = "No API key."; return result; }
      if (string.IsNullOrEmpty(model)) model = DefaultModel;

      try {
        // Strong prompt: treat action-like tokens as commands; filler words as macros. Optional 'canonical'.
        var sys =
          "You update a game parsing spec. Return ONLY JSON with keys: " +
          "\"commands\": [string], \"macros\": [string], \"fieldMap\": [{\"key\":\"<phrase>\",\"value\":\"<canonical.path>\"}], " +
          "and optionally \"canonical\": \"<strict canonical JSON>\". " +
          "If an unmatched token looks like an ACTION (verb/noun describing an operation), propose a command name (e.g., 'zigzagRows'). " +
          "Treat filler words like 'then', 'and', 'over' as macros instead. " +
          "If NL implies tiles are missing, include fieldMap for 'tiles missing' -> 'track.missingTileChance'. Be conservative.";

        var user =
          "Natural language: " + nl + "\n" +
          "Normalized: " + normalized + "\n" +
          "Current canonical: " + (canonical ?? "") + "\n" +
          "Unknown plan commands (from canonical): " + string.Join(", ", (missingCmds ?? new List<string>())) + "\n" +
          "Unmatched tokens: " + string.Join(", ", (unmatched ?? new List<string>())) + "\n" +
          "If you can confidently produce a STRICT canonical JSON that matches the NL (including extra steps like 'zigzag'), put it in 'canonical'.";

        var payload = new {
          model = model, temperature = 0.2f, response_format = new { type = "json_object" },
          messages = new object[] { new { role = "system", content = sys }, new { role = "user", content = user } }
        };

        var json = JsonUtility.ToJson(payload);
        using (var req = new UnityWebRequest(ChatUrl, "POST")) {
          req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
          req.downloadHandler = new DownloadHandlerBuffer();
          req.SetRequestHeader("Authorization", "Bearer " + apiKey);
          req.SetRequestHeader("Content-Type", "application/json");
          var op = req.SendWebRequest();
          while (!op.isDone) {}

#if UNITY_2020_2_OR_NEWER
          if (req.result != UnityWebRequest.Result.Success)
#else
          if (req.isNetworkError || req.isHttpError)
#endif
          { error = "HTTP " + req.responseCode + " - " + req.error; return result; }

          var txt = req.downloadHandler.text ?? "";
          var m = Regex.Match(txt, "\"content\"\\s*:\\s*\"([\\s\\S]*?)\"\\s*\\}\\s*\\]\\s*\\}", RegexOptions.Multiline);
          if (!m.Success) { error = "No content in response."; return result; }
          var content = Regex.Unescape(m.Groups[1].Value);

          foreach (Match q in Regex.Matches(content, "\"commands\"\\s*:\\s*\\[(.*?)\\]"))
            foreach (Match i in Regex.Matches(q.Groups[1].Value, "\"([^\"]+)\"")) result.commands.Add(i.Groups[1].Value);

          foreach (Match q in Regex.Matches(content, "\"macros\"\\s*:\\s*\\[(.*?)\\]"))
            foreach (Match i in Regex.Matches(q.Groups[1].Value, "\"([^\"]+)\"")) result.macros.Add(i.Groups[1].Value);

          foreach (Match q in Regex.Matches(content, "\"fieldMap\"\\s*:\\s*\\[(.*?)\\]"))
            foreach (Match obj in Regex.Matches(q.Groups[1].Value, "\\{(.*?)\\}")) {
              var k = Regex.Match(obj.Value, "\"key\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
              var v = Regex.Match(obj.Value, "\"value\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
              if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v)) result.fieldMap.Add(new FieldPair{ key=k, value=v });
            }

          var cm = Regex.Match(content, "\"canonical\"\\s*:\\s*\"([\\s\\S]*?)\"");
          if (cm.Success) result.canonical = Regex.Unescape(cm.Groups[1].Value);
        }
      } catch (Exception ex) { error = ex.Message; }
      return result;
    }

    public static bool Apply(ProposedFix fix) {
      if (fix == null) return false;
      bool changed = false;
      try {
        if (fix.commands != null && fix.commands.Count > 0)
          changed |= SpecAutoFix.EnsureCommandsExist(fix.commands);
        if (fix.macros != null && fix.macros.Count > 0)
          changed |= SpecAutoFix.EnsureMacrosExist(fix.macros);
        if (fix.fieldMap != null && fix.fieldMap.Count > 0) {
          var dict = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
          foreach (var p in fix.fieldMap) if (!string.IsNullOrEmpty(p.key) && !string.IsNullOrEmpty(p.value)) dict[p.key]=p.value;
          changed |= SpecAutoFix.EnsureFieldMapPairs(dict);
        }
      } catch (Exception ex) { Debug.LogWarning("[AIAutoFix] Apply failed: " + ex.Message); }
      return changed;
    }
  }

  public static class AIGGEditorPrefs {
    const string KEY="AIGG_AI_API_KEY", MODEL="AIGG_AI_MODEL", AUTO="AIGG_AI_AUTO_APPLY";
    public static void Save(string apiKey, string model, bool auto) { if (apiKey!=null) EditorPrefs.SetString(KEY,apiKey); if (model!=null) EditorPrefs.SetString(MODEL,model); EditorPrefs.SetBool(AUTO,auto); }
    public static string LoadKey()=>EditorPrefs.GetString(KEY,"");
    public static string LoadModel()=>EditorPrefs.GetString(MODEL,"gpt-4o-mini");
    public static bool LoadAuto()=>EditorPrefs.GetBool(AUTO,false);
  }
}
