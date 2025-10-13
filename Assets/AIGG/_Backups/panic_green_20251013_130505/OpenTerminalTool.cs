#if UNITY_EDITOR
using UnityEditor;
using System.Diagnostics;
using System.IO;

namespace Aim2Pro.AIGG.Tools
{
    public static class OpenTerminalTool
    {
        // ⌘ + ⇧ + T → Open Terminal at Unity project root
        [MenuItem("Window/Aim2Pro/Tools/Open Terminal at Project Root %#t")]
        public static void OpenTerminalHere()
        {
            var path = Path.GetFullPath(".");
            Process.Start("open", $"-a Terminal \"{path}\"");
        }
    }
}
#endif

