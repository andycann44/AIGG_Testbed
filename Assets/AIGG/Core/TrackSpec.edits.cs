using System;
using System.Collections.Generic;

namespace Aim2Pro.AIGG.Track
{
    [Serializable]
    public class TrackEditsSpec
    {
        public List<Edit> edits = new();
    }

    [Serializable]
    public class Edit
    {
        public string type; // "deleteTiles", "insertCurve"
        public DeleteTiles where; // for deleteTiles
        public InsertCurve at;    // for insertCurve
        public CurveParams @params;
    }

    [Serializable] public class DeleteTiles { public int row; public List<int> cols; }
    [Serializable] public class InsertCurve { public int row; public int col; }
    [Serializable] public class CurveParams { public string turn; public int arc; public int radius; }
}
