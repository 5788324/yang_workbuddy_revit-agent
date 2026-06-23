using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("系统管理区", "AI 助手", "唤醒侧边栏 Copilot", "Icons/copilot_32.png", "Icons/copilot_16.png", isSlideOut: true)]
    public class CopilotCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var dpid = new DockablePaneId(new Guid("d5e89a24-9c56-4c31-97b0-13f56d0285a7"));
            var pane = commandData.Application.GetDockablePane(dpid);
            if (pane.IsShown())
            {
                pane.Hide();
            }
            else
            {
                pane.Show();
            }
            return Result.Succeeded;
        }
    }
}
