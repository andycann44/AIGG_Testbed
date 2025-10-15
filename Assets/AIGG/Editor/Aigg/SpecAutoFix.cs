// ASCII only
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG {
  [Serializable] class AIGGCommand { public string name = ""; }
  [Serializable] class CommandsRoot { public List<AIGGCommand> commands = new List<AIGGCommand>(); }
  [Serializable] class MacrosRoot   { public List<string> macros = new List<string>(); }
  [Serializable] class FieldMapRoot { public List<FieldMapPair> fieldMap = new List<FieldMapPair>(); }
  [Serializable] class FieldMapPair { public string key = ""; public string value = ""; }

  public static class SpecAutoFix {
    public const string SpecDir = "Assets/AIGG/Spec";
    public const string CommandsPath = SpecDir + "/commands.json";
    public const string MacrosPath   = SpecDir + "/macros.json";
    public const string FieldMapPath = SpecDir + "/fieldmap.json";

    // ---------- Commands ----------
    public static bool EnsureCommandsExist(IEnumerable<string> names) {
      Directory.CreateDirectory(SpecDir);
      var root = LoadCommands();
      var set = new HashSet<string>(root.commands.Select(c => c.name), StringComparer.OrdinalIgnoreCase);
      bool added = false;
      foreach (var n in names ?? Array.Empty<string>()) {
        var nn = (n ?? "").Trim();
        if (nn.Length == 0 || set.Contains(nn)) continue;
        root.commands.Add(new AIGGCommand { name = nn });
        set.Add(nn); added = true;
      }
      if (added) SavePretty(CommandsPath, JsonUtility.ToJson(root, true));
      return added;
    }

    // ---------- Macros ----------
    public static bool EnsureMacrosExist(IEnumerable<string> macros) {
      Directory.CreateDirectory(SpecDir);
      var root = LoadMacros();
      var set = new HashSet<string>(root.macros ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
      bool added = false;
      foreach (var m in macros ?? Array.Empty<string>()) {
        var mm = (m ?? "").Trim();
        if (mm.Length == 0 || set.Contains(mm)) continue;
        root.macros.Add(mm); set.Add(mm); added = true;
      }
      if (added) SavePretty(MacrosPath, JsonUtility.ToJson(root, true));
      return added;
    }

    // ---------- Field Map ----------
    public static bool EnsureFieldMapPairs(Dictionary<string,string> pairs) {
      Directory.CreateDirectory(SpecDir);
      var root = LoadFieldMap();
      var dict = root.fieldMap?.ToDictionary(p => p.key, p => p.value, StringComparer.OrdinalIgnoreCase)
                 ?? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

      bool added = false;
      foreach (var kv in pairs ?? new Dictionary<string,string>()) {
        var k = (kv.Key ?? "").Trim(); var v = (kv.Value ?? "").Trim();
        if (k.Length == 0 || v.Length == 0) continue;
        if (dict.TryGetValue(k, out var existing) && string.Equals(existing, v, StringComparison.OrdinalIgnoreCase))
          continue;
        dict[k] = v; added = true;
      }

      if (added) {
        var rootOut = new FieldMapRoot{ fieldMap = dict.Select(kv => new FieldMapPair{ key=kv.Key, value=kv.Value }).ToList() };
        SavePretty(FieldMapPath, JsonUtility.ToJson(rootOut, true));
      }
      return added;
    }

    public static bool HasFieldMapPair(string key, string value) {
      var root = LoadFieldMap();
      foreach (var p in root.fieldMap ?? new List<FieldMapPair>()) {
        if (string.Equals(p.key, key, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.value, value, StringComparison.OrdinalIgnoreCase)) return true;
      }
      return false;
    }

    // ---------- Loaders ----------
    private static CommandsRoot LoadCommands() {
      try { if (File.Exists(CommandsPath)) { var t=File.ReadAllText(CommandsPath); var r=JsonUtility.FromJson<CommandsRoot>(t); if (r!=null && r.commands!=null) return r; } } catch {}
      return new CommandsRoot();
    }
    private static MacrosRoot LoadMacros() {
      try { if (File.Exists(MacrosPath)) { var t=File.ReadAllText(MacrosPath); var r=JsonUtility.FromJson<MacrosRoot>(t); if (r!=null && r.macros!=null) return r; } } catch {}
      return new MacrosRoot{ macros = new List<string>() };
    }
    private static FieldMapRoot LoadFieldMap() {
      try { if (File.Exists(FieldMapPath)) { var t=File.ReadAllText(FieldMapPath); var r=JsonUtility.FromJson<FieldMapRoot>(t); if (r!=null && r.fieldMap!=null) return r; } } catch {}
      return new FieldMapRoot{ fieldMap = new List<FieldMapPair>() };
    }

    // ---------- Utils ----------
    private static void SavePretty(string path, string json) {
      if (File.Exists(path)) {
        var bak = path + ".bak." + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(path, bak, true);
      }
      File.WriteAllText(path, json);
      AssetDatabase.ImportAsset(path);
      AssetDatabase.Refresh();
    }
  }
}
