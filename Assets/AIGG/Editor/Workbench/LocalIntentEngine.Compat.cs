using System;
using System.Reflection;
namespace Aim2Pro.AIGG.Workbench
{
    public static class LocalIntentEngine
    {
        // Forward to Aim2Pro.AIGG.AIGG_LocalIntentEngine.RunToJson(string) if present.
        public static string RunToJson(string nl)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Aim2Pro.AIGG.AIGG_LocalIntentEngine");
                    if (t == null) continue;
                    var m = t.GetMethod("RunToJson", BindingFlags.Public|BindingFlags.Static);
                    if (m == null) continue;
                    return (string)m.Invoke(null, new object[]{ nl }) ?? "";
                }
                return "{\"error\":\"Local engine not found\"}";
            }
            catch (Exception e)
            {
                return "{\"error\":\"Local engine exception: " + Escape(e.Message) + "\"}";
            }
        }
        static string Escape(string s) => (s??"").Replace("\\","\\\\").Replace("\"","\\\"");
    }
}
