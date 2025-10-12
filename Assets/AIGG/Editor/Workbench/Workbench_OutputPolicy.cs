#if UNITY_EDITOR
namespace Aim2Pro.AIGG.Editor
{
    public static class Workbench_OutputPolicy
    {
        // Keep the Workbench output box empty until the AI returns a final JSON.
        public static bool KeepEmptyUntilAIFinal = true;

        public static bool AiPending  { get; private set; }
        public static bool HasFinal   { get; private set; }
        public static string FinalJson { get; private set; } = "";

        public static void BeginAI()
        {
            AiPending = true;
            HasFinal = false;
            FinalJson = "";
        }

        public static void SetFinal(string json)
        {
            FinalJson = json ?? "";
            HasFinal = true;
            AiPending = false;
        }

        public static void Clear()
        {
            AiPending = false;
            HasFinal = false;
            FinalJson = "";
        }
    }
}
#endif
