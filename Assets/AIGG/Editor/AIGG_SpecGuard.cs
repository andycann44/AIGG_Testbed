using System.IO;
using UnityEditor;
using UnityEngine;
namespace Aim2Pro.AIGG
{
    [InitializeOnLoad]
    public static class AIGG_SpecGuard
    {
        static readonly string SpecDir="Assets/AIGG/Spec";
        static readonly string[] Files=new[]{ "intents.json","lexicon.json","macros.json","commands.json","fieldmap.json","registry.json","schema.json" };
        static AIGG_SpecGuard(){ EnsureSpec(); }
        [MenuItem("Window/Aim2Pro/Aigg/Run Spec Guard Now", priority = 50)]
        public static void EnsureSpec()
        {
            if (!Directory.Exists(SpecDir)) Directory.CreateDirectory(SpecDir);
            foreach (var f in Files)
            {
                var p = Path.Combine(SpecDir, f).Replace("\\","/");
                if (!File.Exists(p)) File.WriteAllText(p, GetDefaultFor(f));
            }
        }
        static string GetDefaultFor(string name) => name switch
        {
            "intents.json"  => "{ \"intents\": [] }",
            "lexicon.json"  => "{ \"lexicon\": [] }",
            "macros.json"   => "{ \"macros\": [] }",
            "commands.json" => "{ \"commands\": [] }",
            "fieldmap.json" => "{ \"fields\": [] }",
            "registry.json" => "{ \"registry\": [] }",
            "schema.json"   => "{ \"schema\": {} }",
            _ => "{}"
        };
    }
}
