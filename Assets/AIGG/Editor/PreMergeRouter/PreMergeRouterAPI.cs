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
            EditorGUIUtility.systemCopyBuffer = _lastJson; // clipboard fallback

            if (!TryForwardToPasteMerge(_lastJson))
            {
                Debug.Log("[AIGG] SpecPasteMergeWindow.OpenWithJson not found. JSON copied to clipboard.");
                PreMergeRouterWindow.Open();
            }
        }

        private static bool TryForwardToPasteMerge(string json)
        {
            try
            {
                var specType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(t => t.Name == "SpecPasteMergeWindow");

                if (specType == null) return false;

                var m = specType.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (m != null) { m.Invoke(null, new object[] { json }); return true; }

                var mInst = specType.GetMethod("OpenWithJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (mInst != null)
                {
                    var win = EditorWindow.GetWindow(specType);
                    mInst.Invoke(win, new object[] { json });
                    win.Show(); win.Focus();
                    return true;
                }

                EditorWindow.GetWindow(specType)?.Show();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AIGG] Router forward failed: " + e.Message);
                return false;
            }
        }
    }
}
