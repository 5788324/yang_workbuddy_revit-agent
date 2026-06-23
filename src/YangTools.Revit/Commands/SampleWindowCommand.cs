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
[RibbonButton("系统管理区", "窗口测试", "一个现代色彩的 WPF 交互对话框，读取当前 Revit 文档状态。", "Icons/sample_window_32.png", "Icons/sample_window_16.png", isSlideOut: true)]
public class SampleWindowCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (commandData.Application.ActiveUIDocument == null)
			{
				TaskDialog.Show("YangTools", "未检测到任何处于打开或活动状态的项目文档！");
				return (Result)1;
			}
			AssistantWindow assistantWindow = new AssistantWindow();
			IntPtr mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
			new WindowInteropHelper(assistantWindow).Owner = mainWindowHandle;
			assistantWindow.ShowDialog();
			return (Result)0;
		}
		catch (Exception ex)
		{
			message = "执行界面程序出错：" + ex.Message;
			return (Result)(-1);
		}
	}
}
