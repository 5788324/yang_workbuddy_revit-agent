using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("项目管理区", "图纸管理", "管理图纸与修订", "Icons/sheet_manager_32.png", "Icons/sheet_manager_16.png")]
    public class SheetManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;

                var window = new SheetManagerWindow(uiapp);
                var helper = new WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

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
