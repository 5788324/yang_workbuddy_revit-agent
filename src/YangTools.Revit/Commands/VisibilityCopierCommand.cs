using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("视图修改区", "可见性拷贝", "将视图覆盖设置复制到其他视图", "Icons/visibility_copier_32.png", "Icons/visibility_copier_16.png")]
    public class VisibilityCopierCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument.Document;

                var window = new VisibilityCopierWindow(doc);
                var helper = new System.Windows.Interop.WindowInteropHelper(window)
                {
                    Owner = uiApp.MainWindowHandle
                };

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
