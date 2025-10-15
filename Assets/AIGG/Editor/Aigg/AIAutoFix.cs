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
        var user = new {
          model = model,
          temperature = 0.2f,
          response_format = new { type = "json_object" },
          messages = new object[] {
            new { role = "system", content =
              "You help update a game spec. Return ONLY JSON with keys: "+
              "\"commands\": [string], \"macros\": [string], \"fieldMap\": [{\"key\":\"<phrase>\",\"value\":\"<canonical.path>\"}]. "+
              "Do not include any other keys. Be conservative: propose only entries clearly justified by the input."
            },
            new { role = "user", content =
              "Natural language: " + nl + "\n" +
              "Normalized: " + normalized + "\n" +
              "Canonical preview: " + (canonical ?? "") + "\n" +
              "Unknown plan commands: " + string.Join(", ", missingCmds ?? new List<string>()) + "\n" +
              "Unmatched tokens: " + string.Join(", ", unmatched ?? new List<string>()) + "\n" +
              "Common mappings: 'tiles missing' -> 'track.missingTileChance'. If you think this is relevant, include it in fieldMap."
            }
          }
        };

        var json = JsonUtility.ToJson(user);
        using (var req = new UnityWebRequest(ChatUrl, "POST")) {
          byte[] body = Encoding.UTF8.GetBytes(json);
          req.uploadHandler = new UploadHandlerRaw(body);
          req.downloadHandler = new DownloadHandlerBuffer();
          req.SetRequestHeader("Authorization", "Bearer " + apiKey);
          req.SetRequestHeader("Content-Type", "application/json");
          var op = req.SendWebRequest();
          while (!op.isDone) { } // editor: blocking; simple

#if UNITY_2020_2_OR_NEWER
          if (req.result != UnityWebRequest.Result.Success)
#else
          if (req.isNetworkError || req.isHttpError)
#endif
          { error = "HTTP: " + req.responseCode + " - " + req.error; return result; }

          var txt = req.downloadHandler.text ?? "";
          // pull JSON content from choices[0].message.content
          var m = Regex.Match(txt, "\"content\"\\s*:\\s*\"([\\s\\S]*?)\"\\s*\\}\\s*\\]\\s*\\}", RegexOptions.Multiline);
          if (!m.Success) { error = "No content in response."; return result; }
          var content = Regex.Unescape(m.Groups[1].Value);

          // Parse minimal JSON pieces with regex (no extra deps)
          foreach (Match q in Regex.Matches(content, "\"commands\"\\s*:\\s*\\[(.*?)\\]")) {
            foreach (Match i in Regex.Matches(q.Groups[1].Value, "\"([^\"]+)\""))
              result.commands.Add(i.Groups[1].Value);
          }
          foreach (Match q in Regex.Matches(content, "\"macros\"\\s*:\\s*\\[(.*?)\\]")) {
            foreach (Match i in Regex.Matches(q.Groups[1].Value, "\"([^\"]+)\""))
              result.macros.Add(i.Groups[1].Value);
          }
          foreach (Match q in Regex.Matches(content, "\"fieldMap\"\\s*:\\s*\\[(.*?)\\]")) {
            foreach (Match obj in Regex.Matches(q.Groups[1].Value, "\\{(.*?)\\}")) {
              var k = Regex.Match(obj.Value, "\"key\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
              var v = Regex.Match(obj.Value, "\"value\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
              if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                result.fieldMap.Add(new FieldPair{ key=k, value=v });
            }
          }
        }
      } catch (Exception ex) {
        error = ex.Message;
      }
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
      } catch (Exception ex) {
        Debug.LogWarning("[AIAutoFix] Apply failed: " + ex.Message);
      }
      return changed;
    }
  }

  public static class AIGGEditorPrefs {
    const string KEY = "AIGG_AI_API_KEY";
    const string MODEL = "AIGG_AI_MODEL";
    const string AUTO = "AIGG_AI_AUTO_APPLY";

    public static void Save(string apiKey, string model, bool auto) {
      if (apiKey != null) EditorPrefs.SetString(KEY, apiKey);
      if (model != null) EditorPrefs.SetString(MODEL, model);
      EditorPrefs.SetBool(AUTO, auto);
    }
    public static string LoadKey()  => EditorPrefs.GetString(KEY, "");
    public static string LoadModel()=> EditorPrefs.GetString(MODEL, "gpt-4o-mini");
    public static bool LoadAuto()   => EditorPrefs.GetBool(AUTO, false);
  }
}
