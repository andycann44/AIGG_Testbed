#if UNITY_EDITOR
namespace Aim2Pro.AIGG.Track {
  public static class AIGG_NLInterpreter {
    public static string RunToJson(string nl){
      return Aim2Pro.AIGG.Generators.NLToJson.GenerateFromPrompt(nl);
    }
  }
}
#endif
