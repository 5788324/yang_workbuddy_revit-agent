using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("项目工具区", "项目微工具", tooltip: "管理并运行项目微工具", largeIcon: "Icons/assistant_32.png", smallIcon: "Icons/assistant_16.png")]
    public class MicroToolCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var window = new MicroToolWindow();
                if (window.ShowDialog() == true && !string.IsNullOrEmpty(window.SelectedDllPath))
                {
                    return MicroToolEngine.ExecuteMicroTool(window.SelectedDllPath, commandData, ref message, elements);
                }
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
}
