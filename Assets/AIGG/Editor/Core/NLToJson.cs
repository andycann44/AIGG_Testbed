#if UNITY_EDITOR
using System.Text.RegularExpressions; using System.Text;
namespace Aim2Pro.AIGG.Generators {
  public static class NLToJson {
    public static string GenerateFromPrompt(string nl){
      if(string.IsNullOrWhiteSpace(nl)) return null;
      var t=nl.Trim().ToLowerInvariant();
      var mLW=Regex.Match(t,@"\b(\d+)\s*m\s*by\s*(\d+)\s*m\b"); if(!mLW.Success) return null;
      int L=int.Parse(mLW.Groups[1].Value), W=int.Parse(mLW.Groups[2].Value);
      var mC=Regex.Match(t,@"curve\s+rows\s+(\d+)\s*(?:-|to)\s*(\d+)\s+(left|right)\s+(\d+)\s*deg");
      bool hasC=mC.Success; int S=0,E=0,D=0; string side="left";
      if(hasC){ S=int.Parse(mC.Groups[1].Value); E=int.Parse(mC.Groups[2].Value); side=mC.Groups[3].Value; D=int.Parse(mC.Groups[4].Value); }
      var mK=Regex.Match(t,@"spikes?\s+every\s+(\d+)\s*m\b"); bool hasK=mK.Success; int K=hasK?int.Parse(mK.Groups[1].Value):0;
      var inv=System.Globalization.CultureInfo.InvariantCulture; var sb=new StringBuilder();
      sb.Append("{\"trackTemplate\":{"); sb.AppendFormat(inv,"\"lengthMeters\":{0},\"tileWidth\":{1},\"tileSize\":{2},\"killzoneY\":{3}",L,W,1.0,-5.0); sb.Append("},\"segments\":[");
      bool first=true; if(hasC){ sb.AppendFormat(inv,"{{\"op\":\"CurveRows\",\"start\":{0},\"end\":{1},\"side\":\"{2}\",\"deg\":{3}}}",S,E,side,D); first=false; }
      if(hasK){ if(!first) sb.Append(","); sb.AppendFormat(inv,"{{\"op\":\"SpawnEvery\",\"type\":\"spike\",\"every\":{0}}}",K); }
      sb.Append("]}"); return sb.ToString();
    }
  }
}
#endif
