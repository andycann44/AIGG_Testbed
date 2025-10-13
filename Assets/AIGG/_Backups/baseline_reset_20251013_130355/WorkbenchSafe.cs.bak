// WorkbenchSafe.cs
// A tiny facade so WorkbenchWindow never crashes if dependencies are missing.
using System;
using System.Reflection;
using UnityEngine;

namespace Aim2Pro.AIGG.Workbench
{
    internal static class WorkbenchSafe
    {
        // Try call AIGG_LocalIntentEngine.RunToJson(string) -> string JSON
        // Returns (success, jsonOrMessage)
        public static (bool ok, string payload) TryParseLocal(string nl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nl)) return (false, "[WB] NL empty.");
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                Type t = null;
                MethodInfo m = null;

                foreach (var a in asms)
                {
                    // Preferred fully-qualified name
                    t = a.GetType("Aim2Pro.AIGG.AIGG_LocalIntentEngine");
                    if (t != null)
                    {
                        m = t.GetMethod("RunToJson", BindingFlags.Public | BindingFlags.Static);
                        break;
                    }
                }
                if (t == null || m == null)
                    return (false, "[WB] Local intent engine not found (Aim2Pro.AIGG.AIGG_LocalIntentEngine.RunToJson).");

                var json = m.Invoke(null, new object[] { nl }) as string ?? "";
                if (string.IsNullOrEmpty(json))
                    return (false, "[WB] Local engine returned empty JSON.");
                return (true, json);
            }
            catch (Exception e)
            {
                return (false, "[WB] Local parse failed: " + e.Message);
            }
        }

        // Route canonical JSON to Pre-Merge Router if present. Returns status message.
        public static string RouteJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return "[WB] No JSON to route.";
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                Type api = null;
                MethodInfo route = null;

                foreach (var a in asms)
                {
                    api = a.GetType("Aim2Pro.AIGG.PreMergeRouterAPI");
                    if (api != null)
                    {
                        route = api.GetMethod("Route", new[] { typeof(string) });
                        break;
                    }
                }
                if (api != null && route != null)
                {
                    route.Invoke(null, new object[] { json });
                    return "[WB] Routed to PreMergeRouterAPI.Route.";
                }

                // Fallback: copy to clipboard so user can paste into Paste & Merge
                GUIUtility.systemCopyBuffer = json;
                return "[WB] Router missing. JSON copied to clipboard.";
            }
            catch (Exception e)
            {
                return "[WB] Route failed: " + e.Message;
            }
        }
    }
}
