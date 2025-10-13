// PreMergeRouterAPI.cs
// Namespace: Aim2Pro.AIGG
// Purpose: Stable API entry point Workbench can call: PreMergeRouterAPI.Route(json)

using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public static class PreMergeRouterAPI
    {
        /// <summary>
        /// Route canonical JSON to the Pre-Merge Router window.
        /// If the Paste & Merge window (SpecPasteMergeWindow) exposes OpenWithJson(string),
        /// this will forward to it. Otherwise it will copy to clipboard and open Pre-Merge Router.
        /// </summary>
        public static void Route(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[PreMergeRouterAPI] Empty JSON payload.");
                return;
            }
            PreMergeRouterWindow.OpenWithJson(json);
        }
    }
}
