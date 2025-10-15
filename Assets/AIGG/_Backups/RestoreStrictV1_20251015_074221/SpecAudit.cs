// ASCII only
using System; using System.IO; using System.Linq; using System.Collections.Generic; using System.Text.RegularExpressions;
namespace Aim2Pro.AIGG {
  public static class SpecAudit {
    private static readonly string SpecDir="Assets/AIGG/Spec"; private static readonly string CommandsFile="commands.json";
    public static List<string> FindMissingCommands(string canonicalJson){
      var miss=new List<string>(); var used=ExtractActions(canonicalJson); var known=LoadKnown();
      foreach(var u in used) if(!known.Contains(u)) miss.Add(u); return miss.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
    private static HashSet<string> LoadKnown(){
      var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var path=Path.Combine(SpecDir,CommandsFile); if(!File.Exists(path)) return set;
      try{
        var txt=File.ReadAllText(path);
        foreach(Match m in Regex.Matches(txt,"\"name\"\\s*:\\s*\"([^\"]+)\"")) set.Add(m.Groups[1].Value);
        foreach(Match m in Regex.Matches(txt,"\"([^\"]+)\"")){
          var s=m.Groups[1].Value; if(s!="commands" && s!="name" && s.IndexOfAny(new[]{'{','}','[',']',':',','})<0) set.Add(s);
        }
      }catch{}
      return set;
    }
    private static List<string> ExtractActions(string cj){ var list=new List<string>(); if(string.IsNullOrEmpty(cj)) return list;
      foreach(Match m in Regex.Matches(cj,"\"action\"\\s*:\\s*\"([^\"]+)\"")) list.Add(m.Groups[1].Value); return list; }
  }
}
