using UnityEngine;

namespace Aim2Pro.AIGG
{
    public enum AIGGMode { LOCAL, OPENAI }

    // Lives at: Assets/StickerDash/AIGG/Resources/AIGG_Settings.asset
    public class AIGGSettings : ScriptableObject
    {
        // Mode & active model
        public AIGGMode mode = AIGGMode.LOCAL;
        public string model = "local-mock";

        // Optional per-mode memory (safe if unused)
        public string localModel  = "local-mock";
        public string openAIModel = "gpt-4o-mini";

        // OpenAI credentials
        [TextArea] public string openAIKey = "";

        // NEW: API behavior
        public bool useResponsesBeta = false;  // default off; chat is primary
        public int  timeoutSeconds   = 20;     // HTTP timeout

        public static string ResourcePath => "AIGG_Settings";

        public static AIGGSettings LoadOrCreate()
        {
            var found = Resources.Load<AIGGSettings>(ResourcePath);
            if (found) return found;

#if UNITY_EDITOR
            var inst = ScriptableObject.CreateInstance<AIGGSettings>();
            var dir = "Assets/StickerDash/AIGG/Resources";
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, "AIGG_Settings.asset");
            UnityEditor.AssetDatabase.CreateAsset(inst, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.SetLabels(inst, new[] { "AIGGSettings" });
            return inst;
#else
            return null;
#endif
        }
    }
}
