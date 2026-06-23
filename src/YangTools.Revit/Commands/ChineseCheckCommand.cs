using System;
using System.Diagnostics;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton("检查工具区", "中文检查", "检查项目中所有族、参数、注释是否包含中文字符", "Icons/cn_check_32.png", "Icons/cn_check_16.png")]
public class ChineseCheckCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			UIDocument activeUIDocument = commandData.Application.ActiveUIDocument;
			if (activeUIDocument == null)
			{
				TaskDialog.Show("YangTools", "未检测到任何处于打开或活动状态的项目文档！");
				return (Result)1;
			}
			ChineseCheckWindow chineseCheckWindow = new ChineseCheckWindow(activeUIDocument.Document, activeUIDocument);
			IntPtr mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
			new WindowInteropHelper(chineseCheckWindow).Owner = mainWindowHandle;
			chineseCheckWindow.ShowDialog();
			return (Result)0;
		}
		catch (Exception ex)
		{
			TaskDialog.Show("错误", $"操作失败。\n错误信息: {ex.Message}");
			message = "中文检查执行出错：" + ex.Message;
			return (Result)(-1);
		}
	}
}
