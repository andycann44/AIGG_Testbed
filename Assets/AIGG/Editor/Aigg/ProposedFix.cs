using System;
using System.Collections.Generic;

namespace Aim2Pro.AIGG {
  [Serializable]
  public class ProposedFix {
    public List<string> commands = new List<string>();
    public List<string> macros   = new List<string>();
    public List<FieldPair> fieldMap = new List<FieldPair>();
    public string canonical = "";
    public Dictionary<string,string> files = new Dictionary<string,string>();
  }
}
