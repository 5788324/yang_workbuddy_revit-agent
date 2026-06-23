using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("系统管理区", "系统设置", "主题配色、Ribbon 面板自定义等全局设置", "Icons/ribbon_settings_32.png", "Icons/ribbon_settings_16.png")]
    public class RibbonSettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var window = new RibbonSettingsWindow();
            new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
