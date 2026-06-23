using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("模型修改区", "线性布置", "沿路径线性布置族", "Icons/generic_32.png", "Icons/generic_16.png")]
    public class LinearPlacementCommand : IExternalCommand
    {
        private static LinearPlacementWindow? _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;

            if (_window == null)
            {
                _window = new LinearPlacementWindow(uiapp);
                _window.Closed += (s, e) => _window = null;
                
                var handle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                var helper = new System.Windows.Interop.WindowInteropHelper(_window);
                helper.Owner = handle;

                _window.Show();
            }
            else
            {
                _window.Activate();
            }

            return Result.Succeeded;
        }
    }
}
