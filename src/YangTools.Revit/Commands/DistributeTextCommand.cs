using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton("文本工具区", "等距分布", "按指定顺序、间距及排列方式分布文本", "Icons/distribute_text_32.png", "Icons/distribute_text_16.png")]
public class DistributeTextCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIApplication application = commandData.Application;
		UIDocument activeUIDocument = application.ActiveUIDocument;
		if (activeUIDocument == null)
		{
			TaskDialog.Show("提示", "请先打开一个项目文档。");
			return Result.Cancelled;
		}
		Document doc = activeUIDocument.Document;
		try
		{
			ICollection<ElementId> preSelectedIds = activeUIDocument.Selection.GetElementIds();
			List<TextNote> list2 = preSelectedIds.Select(id => doc.GetElement(id) as TextNote).Where(t => t != null).ToList();

			if (list2.Count < 2)
			{
				IList<Reference> list = activeUIDocument.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(), "请框选要分布或对齐的文本(至少两个)");
				list2 = list.Select(r => doc.GetElement(r) as TextNote).Where(t => t != null).ToList();
			}

			if (list2.Count < 2)
			{
				TaskDialog.Show("提示", "请至少选择两个文本。");
				return Result.Cancelled;
			}
			DistributeTextWindow distributeTextWindow = new DistributeTextWindow();
			new WindowInteropHelper(distributeTextWindow).Owner = application.MainWindowHandle;
			if (distributeTextWindow.ShowDialog() == true)
			{
				using (Transaction val = new Transaction(doc, "文本等距分布"))
				{
					try
					{
						val.Start();
						switch (distributeTextWindow.SortMode)
						{
						case SortMode.XAsc:
							list2 = list2.OrderBy((TextNote tx) => tx.Coord.X).ToList();
							break;
						case SortMode.XDesc:
							list2 = list2.OrderByDescending((TextNote tx) => tx.Coord.X).ToList();
							break;
						case SortMode.YAsc:
							list2 = list2.OrderBy((TextNote tx) => tx.Coord.Y).ToList();
							break;
						case SortMode.YDesc:
							list2 = list2.OrderByDescending((TextNote tx) => tx.Coord.Y).ToList();
							break;
						}
						XYZ coord = list2[0].Coord;
						double num = UnitUtils.ConvertToInternalUnits(distributeTextWindow.SpacingMm, UnitTypeId.Millimeters);
						int num2 = ((distributeTextWindow.SortMode != SortMode.XDesc) ? 1 : (-1));
						int num3 = ((distributeTextWindow.SortMode != SortMode.YDesc) ? 1 : (-1));
						if (distributeTextWindow.SortMode == SortMode.SelectionOrder && list2.Count > 1)
						{
							num2 = (!(list2[1].Coord.X < coord.X) ? 1 : (-1));
							num3 = (!(list2[1].Coord.Y < coord.Y) ? 1 : (-1));
						}
						for (int i = 0; i < list2.Count; i++)
						{
							TextNote val2 = list2[i];
							XYZ coord2 = val2.Coord;
							double num4 = coord2.X;
							double num5 = coord2.Y;
							if (distributeTextWindow.AlignMode == AlignMode.AlignX)
							{
								num4 = coord.X;
							}
							else if (distributeTextWindow.AlignMode == AlignMode.AlignY)
							{
								num5 = coord.Y;
							}
							if (distributeTextWindow.DistributeDir == DistributeDirection.X)
							{
								num4 = coord.X + (double)num2 * num * (double)i;
							}
							else if (distributeTextWindow.DistributeDir == DistributeDirection.Y)
							{
								num5 = coord.Y + (double)num3 * num * (double)i;
							}
							double num6 = num4 - coord2.X;
							double num7 = num5 - coord2.Y;
							if (Math.Abs(num6) > 1E-06 || Math.Abs(num7) > 1E-06)
							{
								XYZ val3 = new XYZ(num6, num7, 0.0);
								ElementTransformUtils.MoveElement(doc, val2.Id, val3);
							}
						}
						TransactionHelper.ShowSuccessAndCommit(val, "成功", "文本等距分布操作成功！", activeUIDocument);
					}
					catch (Exception ex)
					{
						if (val.GetStatus() == TransactionStatus.Started) val.RollBack();
						TaskDialog.Show("错误", $"操作失败，已撤销。\n错误信息: {ex.Message}");
						message = ex.Message;
						return Result.Failed;
					}
				}
			}
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			TaskDialog.Show("错误", $"发生异常。\n错误信息: {ex.Message}");
			return Result.Failed;
		}
	}
}
