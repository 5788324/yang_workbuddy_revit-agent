using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("项目管理区", "族文档管理", "管理与重载族", "Icons/family_manager_32.png", "Icons/family_manager_16.png")]
    public class FamilyManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var window = new FamilyManagerWindow(commandData.Application);
                
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = commandData.Application.MainWindowHandle;

                window.Show();

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
