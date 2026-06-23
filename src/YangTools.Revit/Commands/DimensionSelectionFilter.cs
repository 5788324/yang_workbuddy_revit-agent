using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace YangTools.Revit.Commands;

public class DimensionSelectionFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		return elem is Dimension;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return false;
	}
}
