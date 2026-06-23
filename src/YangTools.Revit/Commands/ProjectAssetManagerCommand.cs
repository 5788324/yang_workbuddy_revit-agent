using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("项目管理区", "项目资产管理器", "项目资产浏览器与批量管理", "Icons/project_inspector_32.png", "Icons/project_inspector_16.png")]
    public class ProjectAssetManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;
                var window = new ProjectAssetManagerWindow(uiapp);
                
                var interopHelper = new System.Windows.Interop.WindowInteropHelper(window)
                {
                    Owner = uiapp.MainWindowHandle
                };

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
