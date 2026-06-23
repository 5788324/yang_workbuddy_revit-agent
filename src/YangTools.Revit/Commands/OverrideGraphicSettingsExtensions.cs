using Autodesk.Revit.DB;

namespace YangTools.Revit.Commands;

public static class OverrideGraphicSettingsExtensions
{
	public static bool HasOverrides(this OverrideGraphicSettings s)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		if (s == null)
		{
			return false;
		}
		if ((int)s.DetailLevel != 0)
		{
			return true;
		}
		if (s.Halftone)
		{
			return true;
		}
		if (s.Transparency != 0)
		{
			return true;
		}
		if (s.ProjectionLineColor.IsValid)
		{
			return true;
		}
		if (s.ProjectionLinePatternId != ElementId.InvalidElementId)
		{
			return true;
		}
		if (s.ProjectionLineWeight != OverrideGraphicSettings.InvalidPenNumber)
		{
			return true;
		}
		if (s.CutLineColor.IsValid)
		{
			return true;
		}
		if (s.CutLinePatternId != ElementId.InvalidElementId)
		{
			return true;
		}
		if (s.CutLineWeight != OverrideGraphicSettings.InvalidPenNumber)
		{
			return true;
		}
		if (s.SurfaceForegroundPatternColor.IsValid)
		{
			return true;
		}
		if (s.SurfaceForegroundPatternId != ElementId.InvalidElementId)
		{
			return true;
		}
		if (!s.IsSurfaceForegroundPatternVisible)
		{
			return true;
		}
		if (s.SurfaceBackgroundPatternColor.IsValid)
		{
			return true;
		}
		if (s.SurfaceBackgroundPatternId != ElementId.InvalidElementId)
		{
			return true;
		}
		if (!s.IsSurfaceBackgroundPatternVisible)
		{
			return true;
		}
		if (s.CutForegroundPatternColor.IsValid)
		{
			return true;
		}
		if (s.CutForegroundPatternId != ElementId.InvalidElementId)
		{
			return true;
		}
		if (!s.IsCutForegroundPatternVisible)
		{
			return true;
		}
		if (s.CutBackgroundPatternColor.IsValid)
		{
			return true;
		}
		if (s.CutBackgroundPatternId != ElementId.InvalidElementId)
		{
			return true;
		}
		if (!s.IsCutBackgroundPatternVisible)
		{
			return true;
		}
		return false;
	}
}
