// SPDX-License-Identifier: MIT
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    public static class PreMergeRouterAPI
    {
        private static string _lastJson = string.Empty;
        public static string LastJson => _lastJson;

        [MenuItem("Window/Aim2Pro/Aigg/Pre-Merge Router")]
        public static void OpenWindow()
        {
            PreMergeRouterWindow.Open();
        }

        public static void Route(string json)
        {
            _lastJson = json ?? string.Empty;
            // Clipboard so user can paste if merge window is missing
            EditorGUIUtility.systemCopyBuffer = _lastJson;
            PreMergeRouterWindow.Open();
            PreMergeRouterWindow.SetInput(_lastJson);
        }

        internal static bool TrySendToPasteMerge(string json)
        {
            try
            {
                var specType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(t => t.Name == "SpecPasteMergeWindow");
                if (specType == null) return false;

                var staticOpen = specType.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (staticOpen != null) { staticOpen.Invoke(null, new object[] { json }); return true; }

                var instOpen = specType.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (instOpen != null)
                {
                    var win = EditorWindow.GetWindow(specType);
                    instOpen.Invoke(win, new object[] { json });
                    win.Show(); win.Focus();
                    return true;
                }

                // As last resort just open the window
                EditorWindow.GetWindow(specType)?.Show();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AIGG] Paste & Merge reflection failed: " + e.Message);
                return false;
            }
        }
    }
}
