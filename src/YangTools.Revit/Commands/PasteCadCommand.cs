using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton("导入工具区", "从CAD粘贴", "提取 AutoCAD 剪贴板中以 Ctrl+C 复制的临时 DWG 图元数据，并根据自定义参数粘贴导入到当前活动视图中。", "Icons/paste_cad_32.png", "Icons/paste_cad_16.png")]
public class PasteCadCommand : IExternalCommand
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
			View activeView = document.ActiveView;
			if (activeView == null)
			{
				TaskDialog.Show("YangTools 提示", "未找到合法的活动视图，请在平面、剖面或三维视图中执行此操作！");
				return Result.Cancelled;
			}
			PasteCadWindow pasteCadWindow = new PasteCadWindow();
			IntPtr mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
			new WindowInteropHelper(pasteCadWindow).Owner = mainWindowHandle;
			if (pasteCadWindow.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			string selectedDwgPath = pasteCadWindow.SelectedDwgPath;
			if (string.IsNullOrEmpty(selectedDwgPath) || !File.Exists(selectedDwgPath))
			{
				TaskDialog.Show("YangTools 错误", "未检测到有效的 AutoCAD 复制临时文件，导入取消。");
				return Result.Failed;
			}
			string text = Path.Combine(Path.GetTempPath(), "YangTools_Temp_" + Path.GetFileName(selectedDwgPath));
			try
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
				File.Copy(selectedDwgPath, text, overwrite: true);
			}
			catch (Exception ex)
			{
				TaskDialog.Show("YangTools 错误", "复制 CAD 临时文件失败，原文件可能被 AutoCAD 锁定中：\n" + ex.Message);
				return Result.Failed;
			}
			try
			{
				using (Transaction val = new Transaction(document, "从CAD粘贴"))
				{
					try
					{
						val.Start();
						DWGImportOptions val2 = new DWGImportOptions
						{
							Unit = pasteCadWindow.SelectedUnit,
							ColorMode = pasteCadWindow.SelectedColorMode,
							Placement = pasteCadWindow.SelectedPlacement,
							ThisViewOnly = pasteCadWindow.SelectedThisViewOnly
						};
						if (activeView.ViewType == ViewType.ThreeD)
						{
							val2.ThisViewOnly = false;
						}
						ElementId invalidElementId = ElementId.InvalidElementId;
						if (document.Import(text, val2, activeView, out invalidElementId) && invalidElementId != ElementId.InvalidElementId)
						{
							TransactionHelper.ShowSuccessAndCommit(val, "成功", "已成功将 CAD 图元导入并粘贴到 Revit 当前视图中！\n\n★ 导入目标视图：" + activeView.Name + "\n" + $"★ 导入图元 ID：{invalidElementId}\n" + "★ 使用文件源：" + Path.GetFileName(selectedDwgPath), activeUIDocument);
							return Result.Succeeded;
						}
						val.RollBack();
						TaskDialog.Show("错误", "Revit API 在解析并导入 CAD 二进制数据时发生故障，请检查数据完整性。");
						return Result.Failed;
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
			finally
			{
				try
				{
					if (File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] PasteCadCommand.cs: {0}", ex.Message); }
			}
		}
		catch (Exception ex2)
		{
			message = ex2.Message;
			TaskDialog.Show("错误", "执行 [从CAD粘贴] 时捕获异常：\n" + ex2.Message);
			return Result.Failed;
		}
	}
}
