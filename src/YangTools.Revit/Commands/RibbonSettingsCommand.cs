using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("系统管理区", "面板设置", "自定义面板显示的图标", "Icons/ribbon_settings_32.png", "Icons/ribbon_settings_16.png")]
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
