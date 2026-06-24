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
[RibbonButton("文本工具区", "对齐到线", "将文本的起点或终点对齐到绘制的虚拟线上", "Icons/align_line_32.png", "Icons/align_line_16.png")]
public class AlignTextToLineCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIApplication application = commandData.Application;
		UIDocument activeUIDocument = application.ActiveUIDocument;
		Document document = activeUIDocument.Document;
		try
		{
			ICollection<ElementId> preSelectedIds = activeUIDocument.Selection.GetElementIds();
			List<TextNote> preSelectedTexts = preSelectedIds.Select(id => document.GetElement(id) as TextNote).Where(t => t != null).ToList();

			TextNote val2 = null;
			if (preSelectedTexts.Count > 0)
			{
				val2 = preSelectedTexts[0];
			}
			else
			{
				Reference val = activeUIDocument.Selection.PickObject(ObjectType.Element, new TextNoteSelectionFilter(), "检测到未选中任何文本，请点选要对齐的文本");
				val2 = document.GetElement(val) as TextNote;
			}

			if (val2 == null)
			{
				return Result.Cancelled;
			}
			XYZ val3 = activeUIDocument.Selection.PickPoint("选择基准线起点");
			XYZ val4 = activeUIDocument.Selection.PickPoint("选择基准线终点");
			AlignTextToLineWindow alignTextToLineWindow = new AlignTextToLineWindow();
			new WindowInteropHelper(alignTextToLineWindow).Owner = application.MainWindowHandle;
			if (alignTextToLineWindow.ShowDialog() == true)
			{
				AlignToLineOptions options = alignTextToLineWindow.Options;
				using (Transaction t = new Transaction(document, "对齐文本到线"))
				{
					try
					{
						t.Start();
						XYZ val6 = (val4 - val3).Normalize();
						double num = XYZ.BasisX.AngleTo(new XYZ(val6.X, val6.Y, 0.0).Normalize());
						if (val6.Y < 0.0)
						{
							num = 0.0 - num;
						}
						if (options.KeepUpright && (num > 1.5717963267948964 || num < -1.5717963267948964))
						{
							for (num += Math.PI; num > Math.PI; num -= Math.PI * 2.0)
							{
							}
							for (; num <= -Math.PI; num += Math.PI * 2.0)
							{
							}
						}
						XYZ coord = val2.Coord;
						double textNoteAngle = GetTextNoteAngle(val2);
						double num2 = num - textNoteAngle;
						Line val7 = Line.CreateBound(coord, coord + XYZ.BasisZ);
						ElementTransformUtils.RotateElement(document, val2.Id, val7, num2);
						XYZ val8 = coord;
						if (options.AlignBase == AlignBasePoint.Start)
						{
							val8 = val3;
						}
						else if (options.AlignBase == AlignBasePoint.End)
						{
							val8 = val4;
						}
						if (options.AlignBase != AlignBasePoint.Retain)
						{
							XYZ val9 = val8 - coord;
							ElementTransformUtils.MoveElement(document, val2.Id, val9);
						}
						TransactionHelper.ShowSuccessAndCommit(t, "成功", "文本对齐操作成功！", activeUIDocument);
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

	private double GetTextNoteAngle(TextNote textNote)
	{
		XYZ baseDirection = ((TextElement)textNote).BaseDirection;
		double num = XYZ.BasisX.AngleTo(new XYZ(baseDirection.X, baseDirection.Y, 0.0).Normalize());
		if (baseDirection.Y < 0.0)
		{
			num = 0.0 - num;
		}
		return num;
	}
}
