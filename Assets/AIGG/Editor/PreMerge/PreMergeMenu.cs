#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

namespace Aim2Pro.AIGG.PreMerge
{
    public static class PreMergeMenu
    {
        [MenuItem("Window/Aim2Pro/Aigg/Open: Pre-Merge → Paste & Merge", priority = 690)]
        public static void OpenBoth()
        {
            // Always open Pre-Merge
            PreMergeWindow.Open();

            // Try to open PreMergeRouterWindow *if present* (no compile-time dependency)
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(tt => tt.FullName == "Aim2Pro.AIGG.PreMergeRouterWindow");

            if (t != null)
            {
                var win = EditorWindow.GetWindow(t);
                if (win != null) win.titleContent = new GUIContent("Pre-Merge Router");
            }
            else
            {
                Aim2Pro.AIGG._AIGGRouterDialogPatch._AIGGRouterTryOpen();
            }
        }
    }
}
#endif
