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
[RibbonButton("文本工具区", "对齐文本", "使多个文本对齐到目标文本的X、Y或旋转角度", "Icons/align_text_32.png", "Icons/align_text_16.png")]
public class AlignTextToTextCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIApplication application = commandData.Application;
		UIDocument activeUIDocument = application.ActiveUIDocument;
		Document doc = activeUIDocument.Document;
		try
		{
			ICollection<ElementId> preSelectedIds = activeUIDocument.Selection.GetElementIds();
			List<TextNote> preSelectedTexts = preSelectedIds.Select(id => doc.GetElement(id) as TextNote).Where(t => t != null).ToList();

			TextNote sourceText = null;
			List<TextNote> list2 = new List<TextNote>();

			if (preSelectedTexts.Count > 0)
			{
				Reference sourceRef = activeUIDocument.Selection.PickObject(ObjectType.Element, new TextNoteSelectionFilter(), "已检测到预选文本。请在屏幕上点击指定【基准文本】");
				sourceText = doc.GetElement(sourceRef) as TextNote;
				if (sourceText == null) return Result.Cancelled;

				list2 = preSelectedTexts.Where(t => t.Id != sourceText.Id).ToList();
				
				if (list2.Count == 0)
				{
					IList<Reference> targetRefs = activeUIDocument.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(), "请框选要对齐的【目标文本】(可多选)");
					list2 = targetRefs.Select(r => doc.GetElement(r) as TextNote).Where(t => t != null && t.Id != sourceText.Id).ToList();
				}
			}
			else
			{
				Reference sourceRef = activeUIDocument.Selection.PickObject(ObjectType.Element, new TextNoteSelectionFilter(), "选择源文本(基准)");
				sourceText = doc.GetElement(sourceRef) as TextNote;
				if (sourceText == null) return Result.Cancelled;

				IList<Reference> targetRefs = activeUIDocument.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(), "选择要对齐的目标文本(可多选)");
				list2 = targetRefs.Select(r => doc.GetElement(r) as TextNote).Where(t => t != null && t.Id != sourceText.Id).ToList();
			}

			if (list2.Count == 0)
			{
				return Result.Cancelled;
			}
			AlignTextToTextWindow alignTextToTextWindow = new AlignTextToTextWindow();
			new WindowInteropHelper(alignTextToTextWindow).Owner = application.MainWindowHandle;
			if (alignTextToTextWindow.ShowDialog() == true)
			{
				bool alignX = alignTextToTextWindow.AlignX;
				bool alignY = alignTextToTextWindow.AlignY;
				bool alignRotation = alignTextToTextWindow.AlignRotation;
				if (!alignX && !alignY && !alignRotation)
				{
					return Result.Cancelled;
				}
				using (Transaction val2 = new Transaction(doc, "对齐文本到文本"))
				{
					try
					{
						val2.Start();
						XYZ coord = sourceText.Coord;
						double textNoteAngle = GetTextNoteAngle(sourceText);
						foreach (TextNote item in list2)
						{
							if (alignRotation)
							{
								double textNoteAngle2 = GetTextNoteAngle(item);
								double num = textNoteAngle - textNoteAngle2;
								if (Math.Abs(num) > 1E-06)
								{
									Line val3 = Line.CreateBound(item.Coord, item.Coord + XYZ.BasisZ);
									ElementTransformUtils.RotateElement(doc, item.Id, val3, num);
								}
							}
							if (alignX || alignY)
							{
								XYZ coord2 = item.Coord;
								double num2 = (alignX ? (coord.X - coord2.X) : 0.0);
								double num3 = (alignY ? (coord.Y - coord2.Y) : 0.0);
								if (Math.Abs(num2) > 1E-06 || Math.Abs(num3) > 1E-06)
								{
									XYZ val4 = new XYZ(num2, num3, 0.0);
									ElementTransformUtils.MoveElement(doc, item.Id, val4);
								}
							}
						}
						TransactionHelper.ShowSuccessAndCommit(val2, "成功", "文本对齐操作成功！", activeUIDocument);
					}
					catch (Exception ex)
					{
						if (val2.GetStatus() == TransactionStatus.Started) val2.RollBack();
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
