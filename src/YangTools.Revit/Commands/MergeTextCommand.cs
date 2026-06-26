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
[RibbonButton("文本工具区", "文本合并", "用指定分隔符合并多个文本", "Icons/merge_text_32.png", "Icons/merge_text_16.png")]
public class MergeTextCommand : IExternalCommand
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
				IList<Reference> pickRefs = uiDoc.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(), "请框选要合并的文本(至少两个)");
				textNotes = pickRefs.Select(r => doc.GetElement(r) as TextNote).Where(t => t != null).ToList();
			}

			if (textNotes.Count < 2)
			{
				TaskDialog.Show("提示", "请至少选择两个文本进行合并。");
				return Result.Cancelled;
			}
			MergeTextWindow optionsWindow = new MergeTextWindow();
			new WindowInteropHelper(optionsWindow).Owner = uiApp.MainWindowHandle;
			if (optionsWindow.ShowDialog() == true)
			{
				using (Transaction t = new Transaction(doc, "文本合并"))
				{
					try
					{
						t.Start();
						switch (optionsWindow.SortMode)
						{
						case MergeSortMode.XAsc:
							textNotes = textNotes.OrderBy(tx => tx.Coord.X).ToList();
							break;
						case MergeSortMode.XDesc:
							textNotes = textNotes.OrderByDescending(tx => tx.Coord.X).ToList();
							break;
						case MergeSortMode.YAsc:
							textNotes = textNotes.OrderBy(tx => tx.Coord.Y).ToList();
							break;
						case MergeSortMode.YDesc:
							textNotes = textNotes.OrderByDescending(tx => tx.Coord.Y).ToList();
							break;
						}
						string separator = optionsWindow.UseNewlineSeparator ? "\r" : optionsWindow.SeparatorText;
						List<string> values = textNotes.Select(tx => tx.Text).ToList();
						string mergedText = string.Join(separator, values);
						textNotes[0].Text = mergedText;
						for (int i = 1; i < textNotes.Count; i++)
						{
							doc.Delete(textNotes[i].Id);
						}
						TransactionHelper.ShowSuccessAndCommit(t, "成功", "文本合并操作成功！", uiDoc);
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
