using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("系统管理区", "MCP 状态", "查看后台 MCP 服务状态信息", "Icons/mcp_server_32.png", "Icons/mcp_server_16.png", isSlideOut: true)]
    public class McpStatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var window = new McpStatusWindow();
            new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.ShowDialog();
            
            return Result.Succeeded;
        }
    }
}
