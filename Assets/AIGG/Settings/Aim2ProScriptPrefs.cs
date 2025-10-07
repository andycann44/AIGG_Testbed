#if UNITY_EDITOR
using UnityEditor;

namespace Aim2Pro
{
    public static class ScriptPrefs
    {
        const string kShellKey = "Aim2Pro.PreferredShell";
        const string kAutoBashKey = "Aim2Pro.AutoCreateBashUtilities";

        public static string PreferredShell => EditorPrefs.GetString(kShellKey, "bash");
        public static bool AutoCreateBash => EditorPrefs.GetBool(kAutoBashKey, true);

        [InitializeOnLoadMethod]
        static void EnsureDefaults()
        {
            if (!EditorPrefs.HasKey(kShellKey)) EditorPrefs.SetString(kShellKey, "bash");
            if (!EditorPrefs.HasKey(kAutoBashKey)) EditorPrefs.SetBool(kAutoBashKey, true);
        }
    }
}
#endif
