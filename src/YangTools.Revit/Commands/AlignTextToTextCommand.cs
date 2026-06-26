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
			List<TextNote> preSelectedTexts = preSelectedIds.Select(id => doc.GetElement(id) as TextNote).Where(t => t != null).ToList();

			TextNote sourceText = null;
			List<TextNote> targetTexts = new List<TextNote>();

			if (preSelectedTexts.Count > 0)
			{
				Reference sourceRef = uiDoc.Selection.PickObject(ObjectType.Element, new TextNoteSelectionFilter(), "已检测到预选文本。请在屏幕上点击指定【基准文本】");
				sourceText = doc.GetElement(sourceRef) as TextNote;
				if (sourceText == null) return Result.Cancelled;

				targetTexts = preSelectedTexts.Where(t => t.Id != sourceText.Id).ToList();

				if (targetTexts.Count == 0)
				{
					IList<Reference> targetRefs = uiDoc.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(), "请框选要对齐的【目标文本】(可多选)");
					targetTexts = targetRefs.Select(r => doc.GetElement(r) as TextNote).Where(t => t != null && t.Id != sourceText.Id).ToList();
				}
			}
			else
			{
				Reference sourceRef = uiDoc.Selection.PickObject(ObjectType.Element, new TextNoteSelectionFilter(), "选择源文本(基准)");
				sourceText = doc.GetElement(sourceRef) as TextNote;
				if (sourceText == null) return Result.Cancelled;

				IList<Reference> targetRefs = uiDoc.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(), "选择要对齐的目标文本(可多选)");
				targetTexts = targetRefs.Select(r => doc.GetElement(r) as TextNote).Where(t => t != null && t.Id != sourceText.Id).ToList();
			}

			if (targetTexts.Count == 0)
			{
				return Result.Cancelled;
			}
			AlignTextToTextWindow optionsWindow = new AlignTextToTextWindow();
			new WindowInteropHelper(optionsWindow).Owner = uiApp.MainWindowHandle;
			if (optionsWindow.ShowDialog() == true)
			{
				bool alignX = optionsWindow.AlignX;
				bool alignY = optionsWindow.AlignY;
				bool alignRotation = optionsWindow.AlignRotation;
				if (!alignX && !alignY && !alignRotation)
				{
					return Result.Cancelled;
				}
				using (Transaction t = new Transaction(doc, "对齐文本到文本"))
				{
					try
					{
						t.Start();
						XYZ sourceCoord = sourceText.Coord;
						double sourceAngle = TextNoteHelper.GetTextNoteAngle(sourceText);
						foreach (TextNote target in targetTexts)
						{
							if (alignRotation)
							{
								double targetAngle = TextNoteHelper.GetTextNoteAngle(target);
								double angleDiff = sourceAngle - targetAngle;
								if (Math.Abs(angleDiff) > 1E-06)
								{
									Line rotationAxis = Line.CreateBound(target.Coord, target.Coord + XYZ.BasisZ);
									ElementTransformUtils.RotateElement(doc, target.Id, rotationAxis, angleDiff);
								}
							}
							if (alignX || alignY)
							{
								XYZ targetCoord = target.Coord;
								double deltaX = alignX ? (sourceCoord.X - targetCoord.X) : 0.0;
								double deltaY = alignY ? (sourceCoord.Y - targetCoord.Y) : 0.0;
								if (Math.Abs(deltaX) > 1E-06 || Math.Abs(deltaY) > 1E-06)
								{
									XYZ offset = new XYZ(deltaX, deltaY, 0.0);
									ElementTransformUtils.MoveElement(doc, target.Id, offset);
								}
							}
						}
						TransactionHelper.ShowSuccessAndCommit(t, "成功", "文本对齐操作成功！", uiDoc);
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
