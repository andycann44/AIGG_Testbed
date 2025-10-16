// ASCII only
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG {
  public static class TempMerge {
    public static readonly string Root = Path.Combine(Directory.GetCurrentDirectory(), "AIGG_TempMerge");

    public static string NewBatchDir() {
      Directory.CreateDirectory(Root);
      var dir = Path.Combine(Root, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
      Directory.CreateDirectory(dir);
      return dir;
    }

    public static string LatestBatchDir() {
      if (!Directory.Exists(Root)) return null;
      var dirs = Directory.GetDirectories(Root);
      if (dirs.Length == 0) return null;
      return dirs.OrderByDescending(d => d).First();
    }

    static void Write(string dir, string name, string text) {
      if (string.IsNullOrEmpty(text)) return;
      File.WriteAllText(Path.Combine(dir, name), text);
    }

    public static string SaveFromAI(ProposedFix fix, string canonical) {
      var dir = NewBatchDir();
      try {
        if (fix != null) {
          if (fix.commands != null && fix.commands.Count > 0) {
            // {"commands":[{"name":"..."}]}
            var items = string.Join(",", fix.commands.ConvertAll(c => "{ \"name\": \"" + c + "\" }"));
            Write(dir, "commands.temp.json", "{\n  \"commands\": [ " + items + " ]\n}\n");
          }
          if (fix.macros != null && fix.macros.Count > 0) {
            Write(dir, "macros.temp.txt", string.Join("\n", fix.macros) + "\n");
          }
          if (fix.fieldMap != null && fix.fieldMap.Count > 0) {
            // lines: key => value
            var lines = new List<string>();
            foreach (var p in fix.fieldMap) if (!string.IsNullOrEmpty(p.key) && !string.IsNullOrEmpty(p.value))
              lines.Add(p.key + " => " + p.value);
            if (lines.Count > 0) Write(dir, "fieldmap.temp.txt", string.Join("\n", lines) + "\n");
          }
        }
        if (!string.IsNullOrEmpty(canonical))
          Write(dir, "canonical.temp.json", canonical.Trim() + "\n");
      } catch (Exception ex) {
        Debug.LogWarning("[TempMerge] SaveFromAI failed: " + ex.Message);
      }
      return dir;
    }
  }
}
