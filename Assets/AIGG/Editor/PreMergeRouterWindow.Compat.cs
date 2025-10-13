namespace Aim2Pro.AIGG
{
    public static class PreMergeRouterWindowCompat
    {
        public static void ReceiveFromOpenAI(string json, bool _)
        {
            PreMergeRouterAPI.Route(json);
        }
    }
}
