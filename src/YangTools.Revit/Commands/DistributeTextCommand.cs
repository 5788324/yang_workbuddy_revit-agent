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
		UIApplication uiApp = commandData.Application;
		UIDocument uiDoc = uiApp.ActiveUIDocument;
		if (uiDoc == null)
		{
			TaskDialog.Show("提示", "请先打开一个项目文档。");
			return Result.Cancelled;
		}
		Document doc = uiDoc.Document;
		try
		{
			ICollection<ElementId> preSelectedIds = uiDoc.Selection.GetElementIds();
			List<TextNote> textNotes = preSelectedIds.Select(id => doc.GetElement(id) as TextNote).Where(t => t != null).ToList();

			if (textNotes.Count < 2)
			{
				IList<Reference> pickRefs = uiDoc.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(), "请框选要分布或对齐的文本(至少两个)");
				textNotes = pickRefs.Select(r => doc.GetElement(r) as TextNote).Where(t => t != null).ToList();
			}

			if (textNotes.Count < 2)
			{
				TaskDialog.Show("提示", "请至少选择两个文本。");
				return Result.Cancelled;
			}
			DistributeTextWindow optionsWindow = new DistributeTextWindow();
			new WindowInteropHelper(optionsWindow).Owner = uiApp.MainWindowHandle;
			if (optionsWindow.ShowDialog() == true)
			{
				using (Transaction t = new Transaction(doc, "文本等距分布"))
				{
					try
					{
						t.Start();
						switch (optionsWindow.SortMode)
						{
						case SortMode.XAsc:
							textNotes = textNotes.OrderBy(tx => tx.Coord.X).ToList();
							break;
						case SortMode.XDesc:
							textNotes = textNotes.OrderByDescending(tx => tx.Coord.X).ToList();
							break;
						case SortMode.YAsc:
							textNotes = textNotes.OrderBy(tx => tx.Coord.Y).ToList();
							break;
						case SortMode.YDesc:
							textNotes = textNotes.OrderByDescending(tx => tx.Coord.Y).ToList();
							break;
						}
						XYZ firstCoord = textNotes[0].Coord;
						double spacing = UnitUtils.ConvertToInternalUnits(optionsWindow.SpacingMm, UnitTypeId.Millimeters);
						int dirXSign = (optionsWindow.SortMode != SortMode.XDesc) ? 1 : (-1);
						int dirYSign = (optionsWindow.SortMode != SortMode.YDesc) ? 1 : (-1);
						if (optionsWindow.SortMode == SortMode.SelectionOrder && textNotes.Count > 1)
						{
							dirXSign = (!(textNotes[1].Coord.X < firstCoord.X)) ? 1 : (-1);
							dirYSign = (!(textNotes[1].Coord.Y < firstCoord.Y)) ? 1 : (-1);
						}
						for (int i = 0; i < textNotes.Count; i++)
						{
							TextNote note = textNotes[i];
							XYZ coord = note.Coord;
							double newX = coord.X;
							double newY = coord.Y;
							if (optionsWindow.AlignMode == AlignMode.AlignX)
							{
								newX = firstCoord.X;
							}
							else if (optionsWindow.AlignMode == AlignMode.AlignY)
							{
								newY = firstCoord.Y;
							}
							if (optionsWindow.DistributeDir == DistributeDirection.X)
							{
								newX = firstCoord.X + (double)dirXSign * spacing * (double)i;
							}
							else if (optionsWindow.DistributeDir == DistributeDirection.Y)
							{
								newY = firstCoord.Y + (double)dirYSign * spacing * (double)i;
							}
							double deltaX = newX - coord.X;
							double deltaY = newY - coord.Y;
							if (Math.Abs(deltaX) > 1E-06 || Math.Abs(deltaY) > 1E-06)
							{
								XYZ offset = new XYZ(deltaX, deltaY, 0.0);
								ElementTransformUtils.MoveElement(doc, note.Id, offset);
							}
						}
						TransactionHelper.ShowSuccessAndCommit(t, "成功", "文本等距分布操作成功！", uiDoc);
					}
					catch (Exception ex)
					{
						if (t.GetStatus() == TransactionStatus.Started) t.RollBack();
						TaskDialog.Show("错误", $"操作失败，已撤销。\n错误信息: {ex.Message}");
						message = ex.Message;
						return Result.Failed;
					}
				}
			}
			return Result.Succeeded;
		}
		catch (OperationCanceledException)
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
