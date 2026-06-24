using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("总控中心", "批处理与云链接", tooltip: "通过 JSON 配置文件批量链接模型和导出各种格式", largeIcon: "Icons/batch_task_32.png", smallIcon: "Icons/batch_task_16.png")]
    public class BatchTaskCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var window = new BatchTaskWindow(commandData.Application);
            new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.ShowDialog();
            
            return Result.Succeeded;
        }
    }
}
