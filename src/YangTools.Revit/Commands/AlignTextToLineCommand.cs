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

			TextNote targetText = null;
			if (preSelectedTexts.Count > 0)
			{
				targetText = preSelectedTexts[0];
			}
			else
			{
				Reference pickRef = uiDoc.Selection.PickObject(ObjectType.Element, new TextNoteSelectionFilter(), "检测到未选中任何文本，请点选要对齐的文本");
				targetText = doc.GetElement(pickRef) as TextNote;
			}

			if (targetText == null)
			{
				return Result.Cancelled;
			}

			XYZ lineStart = uiDoc.Selection.PickPoint("选择基准线起点");
			XYZ lineEnd = uiDoc.Selection.PickPoint("选择基准线终点");

			AlignTextToLineWindow optionsWindow = new AlignTextToLineWindow();
			new WindowInteropHelper(optionsWindow).Owner = uiApp.MainWindowHandle;
			if (optionsWindow.ShowDialog() == true)
			{
				AlignToLineOptions options = optionsWindow.Options;
				using (Transaction t = new Transaction(doc, "对齐文本到线"))
				{
					try
					{
						t.Start();
						XYZ lineDirection = (lineEnd - lineStart).Normalize();
						double lineAngle = XYZ.BasisX.AngleTo(new XYZ(lineDirection.X, lineDirection.Y, 0.0).Normalize());
						if (lineDirection.Y < 0.0)
						{
							lineAngle = -lineAngle;
						}
						if (options.KeepUpright && (lineAngle > Math.PI * 0.5 || lineAngle < -Math.PI * 0.5))
						{
							lineAngle += Math.PI;
							while (lineAngle > Math.PI) lineAngle -= Math.PI * 2;
							while (lineAngle <= -Math.PI) lineAngle += Math.PI * 2;
						}

						XYZ textCoord = targetText.Coord;
						double textAngle = TextNoteHelper.GetTextNoteAngle(targetText);
						double rotationAngle = lineAngle - textAngle;

						Line rotationAxis = Line.CreateBound(textCoord, textCoord + XYZ.BasisZ);
						ElementTransformUtils.RotateElement(doc, targetText.Id, rotationAxis, rotationAngle);

						XYZ targetPosition = textCoord;
						if (options.AlignBase == AlignBasePoint.Start)
						{
							targetPosition = lineStart;
						}
						else if (options.AlignBase == AlignBasePoint.End)
						{
							targetPosition = lineEnd;
						}
						if (options.AlignBase != AlignBasePoint.Retain)
						{
							XYZ offset = targetPosition - textCoord;
							ElementTransformUtils.MoveElement(doc, targetText.Id, offset);
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
