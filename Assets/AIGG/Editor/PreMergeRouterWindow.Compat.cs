namespace Aim2Pro.AIGG
{
    public partial class PreMergeRouterWindow
    {
        // Legacy entry some code still calls
        public static void ReceiveFromOpenAI(string json, bool _)
        {
            PreMergeRouterAPI.Route(json);
        }
    }
}
