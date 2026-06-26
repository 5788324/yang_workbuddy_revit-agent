using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Models
{
    public class EntityGeneratorState
    {
        public List<FamilyInstance> Profiles { get; set; } = new List<FamilyInstance>();
        public string TemplatePath { get; set; } = "";
        public string FamilyName { get; set; } = "GeneratedLoftFamily";
        public string MaterialName { get; set; } = "";
        public BuiltInCategory TargetCategory { get; set; } = BuiltInCategory.OST_GenericModel;
        public bool IsVoid { get; set; } = false;
        public bool CutWithVoids { get; set; } = false;
        public ElementId TargetLevelId { get; set; } = ElementId.InvalidElementId;
    }
}
