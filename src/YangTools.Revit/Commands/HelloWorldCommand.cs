using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    /// <summary>
    /// 最基础的 HelloWorld 命令示例
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(panelName: "系统管理区", buttonText: "你好，Revit", tooltip: "一个简单的 Hello World 验证测试", largeIcon: "Icons/hello_32.png", smallIcon: "Icons/hello_16.png", isSlideOut: true)]
    public class HelloWorldCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData, 
            ref string message, 
            ElementSet elements)
        {
            try
            {
                var window = new AssistantWindow(selectedTabIndex: 3);
                new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
