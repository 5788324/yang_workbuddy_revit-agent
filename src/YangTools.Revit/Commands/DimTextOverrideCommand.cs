using System;
using System.Collections.Generic;
using System.Diagnostics;
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
[RibbonButton("标注工具区", "标注替换", "拾取标注并替换显示文本", "Icons/dim_override_32.png", "Icons/dim_override_16.png")]
public class DimTextOverrideCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		try
		{
			UIDocument activeUIDocument = commandData.Application.ActiveUIDocument;
			if (activeUIDocument == null)
			{
				TaskDialog.Show("YangTools 提示", "未找到当前正处于活动状态的 Revit 文档！");
				return Result.Cancelled;
			}
			Document document = activeUIDocument.Document;
			IList<Reference> list;
			try
			{
				list = activeUIDocument.Selection.PickObjects(ObjectType.Element, new DimensionSelectionFilter(), "请选择要修改的标注");
			}
			catch (Autodesk.Revit.Exceptions.OperationCanceledException)
			{
				return Result.Cancelled;
			}
			if (list == null || list.Count == 0)
			{
				TaskDialog.Show("YangTools 提示", "未选择任何标注图元！");
				return Result.Cancelled;
			}
			DimTextOverrideWindow dimTextOverrideWindow = new DimTextOverrideWindow(list.Count);
			IntPtr mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
			new WindowInteropHelper(dimTextOverrideWindow).Owner = mainWindowHandle;
			if (dimTextOverrideWindow.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			string text = (dimTextOverrideWindow.IsClear ? "" : dimTextOverrideWindow.OverrideText);
			int num = 0;
			
			using (Transaction val2 = new Transaction(document, "标注文本替换"))
			{
				try
				{
					val2.Start();
					foreach (Reference item in list)
					{
						Element element = document.GetElement(item);
						Dimension val3 = element as Dimension;
						if (val3 == null)
						{
							continue;
						}
						if (val3.Segments.Size > 0)
						{
							foreach (DimensionSegment segment in val3.Segments)
							{
								segment.ValueOverride = text;
							}
						}
						else
						{
							val3.ValueOverride = text;
						}
						num++;
					}
					string text2 = (dimTextOverrideWindow.IsClear ? "清除覆盖文本" : ("替换为【" + text + "】"));
					TransactionHelper.ShowSuccessAndCommit(val2, "成功", $"已成功处理 {num} 个标注！\n\n★ 操作类型：{text2}\n★ 处理数量：{num} 个标注", activeUIDocument);
				}
				catch (Exception ex)
				{
					if (val2.GetStatus() == TransactionStatus.Started) val2.RollBack();
					TaskDialog.Show("错误", $"操作失败，已撤销。\n错误信息: {ex.Message}");
					message = ex.Message;
					return Result.Failed;
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
			TaskDialog.Show("错误", "执行 [标注替换] 时捕获异常：\n" + ex.Message);
			return Result.Failed;
		}
	}
}
