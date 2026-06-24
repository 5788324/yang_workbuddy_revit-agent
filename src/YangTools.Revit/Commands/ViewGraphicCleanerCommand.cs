using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton("视图修改区", "覆盖清理", "清除视图中手动覆盖的元素图形设置(By Element)", "Icons/override_cleaner_32.png", "Icons/override_cleaner_16.png")]
	public class ViewGraphicCleanerCommand : IExternalCommand
	{
		public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
		{
			try
			{
				if (commandData.Application.ActiveUIDocument == null)
				{
					TaskDialog.Show("提示", "请先打开一个项目文档。");
					return Result.Cancelled;
				}

				UIApplication application = commandData.Application;
				ViewGraphicCleanerWindow viewGraphicCleanerWindow = new ViewGraphicCleanerWindow(application.ActiveUIDocument.Document);
				new WindowInteropHelper(viewGraphicCleanerWindow).Owner = application.MainWindowHandle;
				viewGraphicCleanerWindow.ShowDialog();
				return Result.Succeeded;
			}
			catch (Exception ex)
			{
				TaskDialog.Show("错误", $"操作失败。\n错误信息: {ex.Message}");
				message = ex.Message;
				return Result.Failed;
			}
		}
	}
