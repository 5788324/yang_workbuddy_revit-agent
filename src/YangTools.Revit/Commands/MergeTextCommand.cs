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
				IList<Reference> list = activeUIDocument.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(), "请框选要合并的文本(至少两个)");
				list2 = list.Select(r => doc.GetElement(r) as TextNote).Where(t => t != null).ToList();
			}

			if (list2.Count < 2)
			{
				TaskDialog.Show("提示", "请至少选择两个文本进行合并。");
				return Result.Cancelled;
			}
			MergeTextWindow mergeTextWindow = new MergeTextWindow();
			new WindowInteropHelper(mergeTextWindow).Owner = application.MainWindowHandle;
			if (mergeTextWindow.ShowDialog() == true)
			{
				using (Transaction val = new Transaction(doc, "文本合并"))
				{
					try
					{
						val.Start();
						switch (mergeTextWindow.SortMode)
						{
						case MergeSortMode.XAsc:
							list2 = list2.OrderBy((TextNote tx) => tx.Coord.X).ToList();
							break;
						case MergeSortMode.XDesc:
							list2 = list2.OrderByDescending((TextNote tx) => tx.Coord.X).ToList();
							break;
						case MergeSortMode.YAsc:
							list2 = list2.OrderBy((TextNote tx) => tx.Coord.Y).ToList();
							break;
						case MergeSortMode.YDesc:
							list2 = list2.OrderByDescending((TextNote tx) => tx.Coord.Y).ToList();
							break;
						}
						string separator = mergeTextWindow.UseNewlineSeparator ? "\r" : "";
						List<string> values = list2.Select((TextNote tx) => tx.Text).ToList();
						string text = string.Join(separator, values);
						list2[0].Text = text;
						for (int i = 1; i < list2.Count; i++)
						{
							doc.Delete(list2[i].Id);
						}
						TransactionHelper.ShowSuccessAndCommit(val, "成功", "文本合并操作成功！", activeUIDocument);
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
