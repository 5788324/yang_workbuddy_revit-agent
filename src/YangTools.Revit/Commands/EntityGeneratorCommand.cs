using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("模型修改区", "实体生成(Loft)", "基于轮廓生成放样/拉伸等三维实体", "Icons/entity_generator_32.png", "Icons/entity_generator_16.png")]
	public class EntityGeneratorCommand : IExternalCommand
	{
		public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
		{
			try
			{
				var uiapp = commandData.Application;
				var uidoc = uiapp.ActiveUIDocument;
				if (uidoc == null)
				{
					TaskDialog.Show("提示", "请先打开一个项目文档。");
					return Result.Cancelled;
				}
				var doc = uidoc.Document;

				// Step 1: Get selected FamilyInstances
				var selectionIds = uidoc.Selection.GetElementIds();
				var instances = new List<FamilyInstance>();

				if (selectionIds.Count > 0)
				{
					foreach (var id in selectionIds)
					{
						if (doc.GetElement(id) is FamilyInstance fi) instances.Add(fi);
					}
				}

				if (instances.Count < 2)
				{
					instances.Clear();
					try
					{
						var refs = uidoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请至少选择两个轮廓实例进行放样");
						foreach (var r in refs)
						{
							if (doc.GetElement(r) is FamilyInstance fi) instances.Add(fi);
						}
					}
					catch (Autodesk.Revit.Exceptions.OperationCanceledException)
					{
						return Result.Cancelled;
					}
				}

				if (instances.Count < 2)
				{
					TaskDialog.Show("Error", "请至少选择两个轮廓实例进行放样。");
					return Result.Cancelled;
				}

				// Spatial sort: order instances along their primary axis to prevent twisted lofts.
				// Wrapped in try/catch - if sort fails, fall back to selection order (harmless).
				try
				{
					var locs = new List<(FamilyInstance fi, XYZ pt)>();
					foreach (var fi in instances)
					{
						XYZ pt = XYZ.Zero;
						if (fi.Location is LocationPoint lp)
						{
							pt = lp.Point;
						}
						else
						{
							// Use Element.get_BoundingBox(null) - safe across all Revit versions
							var bb = fi.get_BoundingBox(null);
							if (bb != null)
							{
								pt = (bb.Min + bb.Max) * 0.5;
							}
						}
						locs.Add((fi, pt));
					}

					// Find the two furthest points to define the primary axis
					double maxDist = 0;
					XYZ p1 = locs[0].pt;
					XYZ p2 = locs.Count > 1 ? locs[1].pt : locs[0].pt;
					for (int i = 0; i < locs.Count; i++)
					{
						for (int j = i + 1; j < locs.Count; j++)
						{
							double d = locs[i].pt.DistanceTo(locs[j].pt);
							if (d > maxDist)
							{
								maxDist = d;
								p1 = locs[i].pt;
								p2 = locs[j].pt;
							}
						}
					}

					// Only sort if points aren't coincident
					if (maxDist > 1e-6)
					{
						XYZ dir = (p2 - p1).Normalize();
						instances = locs.OrderBy(x => (x.pt - p1).DotProduct(dir)).Select(x => x.fi).ToList();
					}
				}
				catch
				{
					// Sort failed - continue with original order, not critical
				}

				// Open window
				var window = new EntityGeneratorWindow(uiapp, instances);
				var helper = new System.Windows.Interop.WindowInteropHelper(window);
				helper.Owner = uiapp.MainWindowHandle;

				window.ShowDialog();

				return Result.Succeeded;
			}
			catch (Exception ex)
			{
				TaskDialog.Show("错误", $"操作失败。\n错误信息: {ex.Message}\n\n堆栈: {ex.StackTrace}");
				message = ex.Message;
				return Result.Failed;
			}
		}
	}
}

