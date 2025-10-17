using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    internal static class PasteMergeTemp
    {
        public const string Root = "Assets/AIGG/Temp";

        public static void EnsureRoot()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Directory.CreateDirectory(Root);
                AssetDatabase.Refresh();
            }
        }

        public static string PathFor(string tag)
        {
            EnsureRoot();
            var safe = string.IsNullOrEmpty(tag) ? "unknown" : tag.ToLowerInvariant();
            return System.IO.Path.Combine(Root, $"temp_{safe}.json");
        }

        public static void Save(string tag, string json)
        {
            var p = PathFor(tag);
            File.WriteAllText(p, json ?? "");
            AssetDatabase.Refresh();
            Debug.Log($"[PasteMergeTemp] wrote {tag} → {p}");
        }

        public static void ClearAll()
        {
            EnsureRoot();
            foreach (var f in Directory.GetFiles(Root, "temp_*.json"))
                File.Delete(f);
            AssetDatabase.Refresh();
            Debug.Log("[PasteMergeTemp] cleared temp_*.json");
        }
    }
}
