using UnityEngine;
using UnityEditor;
namespace Aim2Pro.AIGG
{
    public static class PreMergeRouterAPI
    {
        public static void Route(string json)
        {
            if (string.IsNullOrEmpty(json)) { Debug.LogWarning("[PreMergeRouterAPI] Empty JSON"); return; }
            PreMergeRouterWindow.OpenWithJson(json);
        }
        public static void Route(string json, bool _) { Route(json); } // legacy overload
    }
}
