using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("项目信息区", "文件统计", "查看项目基本信息与统计", "Icons/project_info_32.png", "Icons/project_info_16.png")]
    public class ProjectInfoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                Document doc = uiapp.ActiveUIDocument.Document;

                if (doc.IsFamilyDocument)
                {
                    TaskDialog.Show("提示", "此功能主要用于项目文件。");
                    return Result.Cancelled;
                }

                ProjectInfoWindow window = new ProjectInfoWindow(uiapp);
                new System.Windows.Interop.WindowInteropHelper(window).Owner = uiapp.MainWindowHandle;
                window.ShowDialog();

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
}
