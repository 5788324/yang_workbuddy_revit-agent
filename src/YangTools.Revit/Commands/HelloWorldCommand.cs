using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

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
            // 显示标准的 Revit 弹窗
            TaskDialog.Show(
                "YangTools", 
                "您好！Revit 插件已成功动态加载，多版本兼容环境正常运行！\n\n当前操作系统类型：Windows");

            return Result.Succeeded;
        }
    }
}
