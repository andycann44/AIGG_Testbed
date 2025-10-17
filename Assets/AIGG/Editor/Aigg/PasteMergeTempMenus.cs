using UnityEditor;

namespace Aim2Pro.AIGG
{
    internal static class PasteMergeTempMenus
    {
        // Create a submenu "Open Temp" under Paste & Merge with one item per bucket.
        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/intents", priority = 210)]
        public static void PM_Open_intents()  => PasteMergeTempBridge.LoadBucketIntoPM("intents");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/lexicon", priority = 211)]
        public static void PM_Open_lexicon()  => PasteMergeTempBridge.LoadBucketIntoPM("lexicon");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/macros", priority = 212)]
        public static void PM_Open_macros()   => PasteMergeTempBridge.LoadBucketIntoPM("macros");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/commands", priority = 213)]
        public static void PM_Open_commands() => PasteMergeTempBridge.LoadBucketIntoPM("commands");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/fieldmap", priority = 214)]
        public static void PM_Open_fieldmap() => PasteMergeTempBridge.LoadBucketIntoPM("fieldmap");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/registry", priority = 215)]
        public static void PM_Open_registry() => PasteMergeTempBridge.LoadBucketIntoPM("registry");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/schema", priority = 216)]
        public static void PM_Open_schema()   => PasteMergeTempBridge.LoadBucketIntoPM("schema");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/router", priority = 217)]
        public static void PM_Open_router()   => PasteMergeTempBridge.LoadBucketIntoPM("router");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/nl", priority = 218)]
        public static void PM_Open_nl()       => PasteMergeTempBridge.LoadBucketIntoPM("nl");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/canonical", priority = 219)]
        public static void PM_Open_canonical()=> PasteMergeTempBridge.LoadBucketIntoPM("canonical");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/diagnostics", priority = 220)]
        public static void PM_Open_diag()     => PasteMergeTempBridge.LoadBucketIntoPM("diagnostics");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/aliases", priority = 221)]
        public static void PM_Open_aliases()  => PasteMergeTempBridge.LoadBucketIntoPM("aliases");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/shims", priority = 222)]
        public static void PM_Open_shims()    => PasteMergeTempBridge.LoadBucketIntoPM("shims");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/nullable", priority = 223)]
        public static void PM_Open_nullable() => PasteMergeTempBridge.LoadBucketIntoPM("nullable");

        [MenuItem("Window/Aim2Pro/Aigg/Paste & Merge/Open with Temp/overrides", priority = 224)]
        public static void PM_Open_overrides()=> PasteMergeTempBridge.LoadBucketIntoPM("overrides");
    }
}
