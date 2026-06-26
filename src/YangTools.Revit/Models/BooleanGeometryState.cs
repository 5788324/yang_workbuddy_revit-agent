using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Models
{
    public class BooleanGeometryState
    {
        public List<FamilyInstance> MainInst { get; set; } = new List<FamilyInstance>();
        public List<FamilyInstance> UnionInsts { get; set; } = new List<FamilyInstance>();
        public List<FamilyInstance> CutInsts { get; set; } = new List<FamilyInstance>();
        public List<FamilyInstance> JoinInsts { get; set; } = new List<FamilyInstance>();
        public bool DeleteTargetInst { get; set; } = false;
        public bool DeleteTargetFam { get; set; } = false;
    }
}
